using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Core networked movement component. Attach to the player prefab alongside Player.
/// When a network session is active this component takes over physics:
///   - Owner:      client-side prediction + reconciliation
///   - Server:     authoritative simulation + state broadcast
///   - Non-owner:  interpolation between server snapshots (Player component disabled)
///
/// When no network session is running (local multiplayer), this component
/// does nothing — Player.FixedUpdate handles movement as before.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NetMovement : NetworkBehaviour
{
    private static readonly List<NetMovement> ActiveInstances = new List<NetMovement>();

    // ===== CONFIGURATION =====

    [Header("Reconciliation")]
    [Tooltip("Position error (units) before a server correction triggers replay")]
    [SerializeField] private float _reconciliationThreshold = 0.01f;

    [Header("Interpolation")]
    [Tooltip("Number of ticks to buffer for remote player interpolation")]
    [SerializeField] private int _interpolationBufferTicks = 2;

    [Header("Combat Rewind")]
    [Tooltip("Maximum lag compensation window for projectile and beam hit validation, in ticks.")]
    [SerializeField] private int _maxCombatRewindTicks = 6;

    [Header("Audio")]
    [Tooltip("Sound played on remote clients when this player fires a basic projectile")]
    [SerializeField] private SoundEffect _primaryFireSound;

    // ===== BUFFER SIZES =====
    // Client input buffer: 64 entries (~1 sec at 60 Hz) — sized for max expected RTT.
    // Server state history: 120 entries (~2 sec at 60 Hz) — sized for lag compensation.
    private const int CLIENT_INPUT_BUFFER_SIZE = 64;
    private const int SERVER_STATE_BUFFER_SIZE = 120;

    // ===== REFERENCES =====
    private Player _player;
    private Rigidbody2D _rb;

    // ===== CLIENT-SIDE PREDICTION STATE (Owner only) =====
    private NetInputSnapshot[] _inputBuffer;
    private NetStateSnapshot[] _predictionBuffer; // predicted state after each tick
    private int _lastSentTick = -1;

    // ===== SERVER STATE (Server/Host only) =====
    private NetStateSnapshot[] _stateHistory;
    private int _stateHistoryHead = 0;

    // Mutable simulation state tracked on the server between ticks
    private float _serverFrictionTimer = 0f;
    private float _serverAnchorDragAccumulator = 0f;

    // ===== INTERPOLATION STATE (Non-owner clients only) =====
    private NetStateSnapshot[] _interpolationBuffer;
    private int _interpWriteIndex = 0;
    private int _interpCount = 0;
    private float _interpTimer = 0f;

    // ===== OWNER MUTABLE SIM STATE =====
    private float _ownerFrictionTimer = 0f;
    private float _ownerAnchorDragAccumulator = 0f;
    private int _lastReceivedServerTick = -1;
    private float _lastServerPrimaryFireTime = -999f;
    private int _lastServerPrimaryFireTick = -999999;
    private bool _lastOwnerFrictionEnabled;
    private bool _lastServerFrictionEnabled;

    // ===== LIFECYCLE =====

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody2D>();
        if (!ActiveInstances.Contains(this))
        {
            ActiveInstances.Add(this);
        }
        if (_player != null)
        {
            _player.onFrictionToggled += HandleFrictionToggled;
            _lastOwnerFrictionEnabled = _player.IsFrictionEnabled;
            _lastServerFrictionEnabled = _player.IsFrictionEnabled;
        }

        if (IsOwner)
        {
            // Owner: set up prediction buffers
            _inputBuffer = new NetInputSnapshot[CLIENT_INPUT_BUFFER_SIZE];
            _predictionBuffer = new NetStateSnapshot[CLIENT_INPUT_BUFFER_SIZE];

            // Tell Player to stop driving physics — NetMovement handles it.
            // Player stays enabled so input callbacks (OnThrust, OnLook, etc.) still fire.
            if (_player != null)
            {
                _player.externalMovementControl = true;
                _player.SetExternalVisualStateEnabled(false);
            }

            // Enable PlayerInput on the owner so this player receives local input.
            // The prefab ships with PlayerInput disabled to prevent device stealing
            // when remote players spawn.
            var playerInput = GetComponent<PlayerInput>();
            if (IsOwner && playerInput != null)
            {
                playerInput.enabled = true;
                AssignOwnerCameraAndTracking(playerInput);
            }
        }
        else if (!IsServer)
        {
            // Pure client-side proxy: disable local gameplay logic and let
            // interpolation drive a kinematic body.
            if (_player != null)
            {
                _player.enabled = false;
                _player.SetExternalVisualStateEnabled(true);
            }

            // Make the Rigidbody kinematic so physics doesn't interfere with interpolation.
            _rb.bodyType = RigidbodyType2D.Kinematic;

            _interpolationBuffer = new NetStateSnapshot[SERVER_STATE_BUFFER_SIZE];
        }
        else
        {
            // Server-authoritative copy of a client-owned player.
            // Keep the Rigidbody dynamic for authoritative simulation, but disable
            // the Player component so local Update/FixedUpdate logic does not run.
            if (_player != null)
            {
                _player.enabled = false;
                _player.SetExternalVisualStateEnabled(false);
            }
        }

        if (IsServer)
        {
            _stateHistory = new NetStateSnapshot[SERVER_STATE_BUFFER_SIZE];
        }
    }

    public override void OnNetworkDespawn()
    {
        ActiveInstances.Remove(this);

        if (_player != null)
        {
            _player.onFrictionToggled -= HandleFrictionToggled;
            if (IsOwner)
            {
                _player.externalMovementControl = false;
                _player.SetExternalVisualStateEnabled(false);
            }
            else
            {
                // Re-enable Player if we disabled it (safety for pooling / round transitions)
                _player.enabled = true;
                _player.SetExternalVisualStateEnabled(false);
                _rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        base.OnNetworkDespawn();
    }

    private void HandleFrictionToggled(bool isEnabled)
    {
        _ownerFrictionTimer = 0f;
        if (IsServer)
        {
            _serverFrictionTimer = 0f;
        }
    }

    public static bool TryGetPlayerByTag(string tag, out NetMovement movement)
    {
        foreach (NetMovement candidate in ActiveInstances)
        {
            if (candidate == null || candidate._player == null)
            {
                continue;
            }

            if (candidate._player.thisPlayerTag == tag)
            {
                movement = candidate;
                return true;
            }
        }

        movement = null;
        return false;
    }

    public static IEnumerable<NetMovement> EnumeratePlayers()
    {
        return ActiveInstances;
    }

    public int MaxCombatRewindTicks => Mathf.Max(0, _maxCombatRewindTicks);

    public bool TryGetHistoricalState(int requestedTick, out NetStateSnapshot snapshot)
    {
        snapshot = default;
        if (_stateHistory == null || _stateHistory.Length == 0)
        {
            return false;
        }

        int newestAllowedTick = _stateHistoryHead;
        int oldestAllowedTick = Mathf.Max(0, newestAllowedTick - Mathf.Max(0, _maxCombatRewindTicks));
        int clampedTick = Mathf.Clamp(requestedTick, oldestAllowedTick, newestAllowedTick);
        int idx = clampedTick % _stateHistory.Length;
        NetStateSnapshot candidate = _stateHistory[idx];
        if (candidate.Tick != clampedTick)
        {
            return false;
        }

        snapshot = candidate;
        return true;
    }

    public float GetCollisionRadius()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            return 0.5f;
        }

        Bounds bounds = col.bounds;
        return Mathf.Max(bounds.extents.x, bounds.extents.y);
    }

    public GameObject ResolveProjectileVisualPrefab(NetProjectileVisualType visualType)
    {
        switch (visualType)
        {
            case NetProjectileVisualType.Primary:
                return _player != null ? _player.projectileWeapon.prefab : null;
            case NetProjectileVisualType.GigaBlastTier1:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier1ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier2:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier2ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier3:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier3ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier4:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier4ProjectilePrefab;
            case NetProjectileVisualType.Class2EmpoweredShot:
                return GetComponent<EmpoweredShot>()?.empoweredShot.projectilePrefab;
            case NetProjectileVisualType.Class2PhysicalProjectile:
                return GetComponent<PhysicalProjectileAbility>()?.physicalProjectile.projectilePrefab;
            default:
                return null;
        }
    }

    private SoundEffect ResolveFireSound(NetProjectileVisualType visualType)
    {
        switch (visualType)
        {
            case NetProjectileVisualType.GigaBlastTier1:
                return GetComponent<GigaBlast>()?.gigaBlast.tier1FireSound;
            case NetProjectileVisualType.GigaBlastTier2:
                return GetComponent<GigaBlast>()?.gigaBlast.tier2FireSound;
            case NetProjectileVisualType.GigaBlastTier3:
                return GetComponent<GigaBlast>()?.gigaBlast.tier3FireSound;
            case NetProjectileVisualType.GigaBlastTier4:
                return GetComponent<GigaBlast>()?.gigaBlast.tier4FireSound;
            case NetProjectileVisualType.Class2EmpoweredShot:
                return GetComponent<EmpoweredShot>()?.empoweredShot.fireSound;
            case NetProjectileVisualType.Class2PhysicalProjectile:
                return GetComponent<PhysicalProjectileAbility>()?.physicalProjectile.fireSound;
            default:
                return _primaryFireSound;
        }
    }

    private void AssignOwnerCameraAndTracking(PlayerInput playerInput)
    {
        CinemachineCamera targetCinemachine = FindFirstObjectByType<CinemachineCamera>();
        if (targetCinemachine == null)
        {
            return;
        }

        targetCinemachine.Target.TrackingTarget = transform;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerInput.camera = mainCamera;
        }
    }

    // ===== TICK LOOP =====

    private void FixedUpdate()
    {
        // If no network session, do nothing — local play uses Player.FixedUpdate
        if (!NetTickUtil.IsActive) return;

        if (IsOwner)
        {
            OwnerTick();
        }

        if (IsServer && !IsOwner)
        {
            // Server ticks are driven by SubmitInputServerRpc for the owning client.
            // For the host (IsServer && IsOwner), the owner tick already ran and
            // the ServerRpc path handles authoritative sim. Nothing extra needed here.
        }

        if (!IsOwner && !IsServer)
        {
            InterpolateRemote();
        }
    }

    public void RequestPrimaryFire(NetFireRequest request)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandlePrimaryFireServer(request);
            return;
        }

        SubmitPrimaryFireServerRpc(request);
    }

    public void RequestBeamState(bool isFiring)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetBeamState state = new NetBeamState
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring
        };

        if (IsServer)
        {
            HandleBeamStateServer(state);
            return;
        }

        SubmitBeamStateServerRpc(state);
    }

    public void RequestFireTrailState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetFireTrailState state = new NetFireTrailState
        {
            Tick = NetTickUtil.CurrentTick,
            IsActive = isActive
        };

        if (IsServer)
        {
            HandleFireTrailStateServer(state);
            return;
        }

        SubmitFireTrailStateServerRpc(state);
    }

    public void RequestReflectActivation()
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandleReflectActivationServer();
            return;
        }

        SubmitReflectActivationServerRpc();
    }

    public void RequestTeleport(Vector2 targetPosition)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTeleportState state = new NetTeleportState
        {
            TargetPosition = targetPosition
        };

        if (IsServer)
        {
            HandleTeleportServer(state);
            return;
        }

        SubmitTeleportServerRpc(state);
    }

    public void RequestClass2ShieldActivation()
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandleClass2ShieldServer(new NetClass2ShieldState { IsActive = true });
            return;
        }

        SubmitClass2ShieldServerRpc(new NetClass2ShieldState { IsActive = true });
    }

    public void RequestTractorBeamState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTractorBeamState state = new NetTractorBeamState
        {
            IsActive = isActive
        };

        if (IsServer)
        {
            HandleTractorBeamServer(state);
            return;
        }

        SubmitTractorBeamServerRpc(state);
    }

    public void RequestTriggerBombLaunch(Vector2 spawnPosition, Vector2 velocity)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTriggerBombLaunchState state = new NetTriggerBombLaunchState
        {
            SpawnPosition = spawnPosition,
            Velocity = velocity
        };

        if (IsServer)
        {
            HandleTriggerBombLaunchServer(state);
            return;
        }

        SubmitTriggerBombLaunchServerRpc(state);
    }

    public void RequestTriggerBombDetonate(Vector2 position)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetTriggerBombDetonateState state = new NetTriggerBombDetonateState
        {
            Position = position
        };

        if (IsServer)
        {
            HandleTriggerBombDetonateServer(state);
            return;
        }

        SubmitTriggerBombDetonateServerRpc(state);
    }

    public void RequestFaerieShiftState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState { IsActive = isActive };
        if (IsServer)
        {
            HandleFaerieShiftServer(state);
            return;
        }

        SubmitFaerieShiftServerRpc(state);
    }

    public void RequestInvisibilityState(bool isActive)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        NetAbilityToggleState state = new NetAbilityToggleState { IsActive = isActive };
        if (IsServer)
        {
            HandleInvisibilityServer(state);
            return;
        }

        SubmitInvisibilityServerRpc(state);
    }

    public void SetInvisibilityStateAuthoritative(bool isActive)
    {
        if (!IsServer)
        {
            return;
        }

        HandleInvisibilityServer(new NetAbilityToggleState { IsActive = isActive });
    }

    // ===== OWNER: CLIENT-SIDE PREDICTION =====

    private void OwnerTick()
    {
        int tick = NetTickUtil.CurrentTick;
        if (tick <= _lastSentTick) return; // don't double-simulate the same tick
        _lastSentTick = tick;

        float dt = Time.fixedDeltaTime;

        float ownerVisualBankAngle = 0f;
        float ownerVisualPitchAngle = 0f;
        if (_player != null)
        {
            _player.GetVisualTiltState(out ownerVisualBankAngle, out ownerVisualPitchAngle);
        }

        // 1. Sample input from Player's read-only getters
        NetInputSnapshot input = new NetInputSnapshot
        {
            Tick = tick,
            Thrust = _player.IsThrustPressed,
            LookInput = _player.LookInput,
            Anchor = _player.IsAnchored,
            FrictionEnabled = _player.IsFrictionEnabled,
            VisualBankAngle = ownerVisualBankAngle,
            VisualPitchAngle = ownerVisualPitchAngle,
        };

        if (input.FrictionEnabled != _lastOwnerFrictionEnabled)
        {
            _ownerFrictionTimer = 0f;
            _lastOwnerFrictionEnabled = input.FrictionEnabled;
        }

        // 2. Store input in circular buffer
        int bufIdx = tick % CLIENT_INPUT_BUFFER_SIZE;
        _inputBuffer[bufIdx] = input;

        // 3. Run prediction locally (velocity + rotation only; physics integrates position)
        Vector2 velocity = _rb.linearVelocity;
        Vector2 position = _rb.position; // position before this tick
        float rotation = transform.eulerAngles.z;

        MovementSimulation.SimulateTick(
            ref velocity, ref position, ref rotation,
            ref _ownerFrictionTimer, ref _ownerAnchorDragAccumulator,
            in input,
            in _player.movement, in _player.friction, in _player.input,
            _rb.mass, dt, _player.GetSlowMultiplier());

        // 4. Apply velocity and rotation only — let physics integrate position
        _rb.linearVelocity = velocity;
        transform.rotation = Quaternion.Euler(0, 0, rotation);

        // 5. Store predicted state for reconciliation comparison
        //    Position stored is where we expect to be AFTER physics integrates.
        Vector2 expectedPosition = _rb.position + velocity * dt;
        float predictedVisualBankAngle = 0f;
        float predictedVisualPitchAngle = 0f;
        if (_player != null)
        {
            _player.GetVisualTiltState(out predictedVisualBankAngle, out predictedVisualPitchAngle);
        }
        _predictionBuffer[bufIdx] = new NetStateSnapshot
        {
            Tick = tick,
            Position = expectedPosition,
            Rotation = rotation,
            Velocity = velocity,
            VisualBankAngle = predictedVisualBankAngle,
            VisualPitchAngle = predictedVisualPitchAngle,
            AnchorDragAccumulator = _ownerAnchorDragAccumulator,
            FrictionTimer = _ownerFrictionTimer,
            FrictionEnabled = input.FrictionEnabled,
        };

        // 6. Submit to the authoritative path.
        // On a host the owner and server live on the same object, so running the
        // server simulation again here would double-apply movement and rotation.
        if (IsServer)
        {
            _serverFrictionTimer = _ownerFrictionTimer;
            _serverAnchorDragAccumulator = _ownerAnchorDragAccumulator;

            PublishAuthoritativeState(
                tick,
                velocity,
                rotation,
                ownerVisualBankAngle,
                ownerVisualPitchAngle,
                _serverFrictionTimer,
                _serverAnchorDragAccumulator,
                dt);
        }
        else
        {
            SubmitInputServerRpc(input);
        }
    }

    // ===== SERVER: AUTHORITATIVE SIMULATION =====

    [ServerRpc]
    private void SubmitInputServerRpc(NetInputSnapshot input, ServerRpcParams rpcParams = default)
    {
        float dt = NetTickUtil.TickInterval;
        if (dt <= 0f) dt = Time.fixedDeltaTime; // fallback

        if (input.FrictionEnabled != _lastServerFrictionEnabled)
        {
            _serverFrictionTimer = 0f;
            _lastServerFrictionEnabled = input.FrictionEnabled;
        }

        // Run authoritative simulation
        Vector2 velocity = _rb.linearVelocity;
        Vector2 position = _rb.position;
        float rotation = transform.eulerAngles.z;

        MovementSimulation.SimulateTick(
            ref velocity, ref position, ref rotation,
            ref _serverFrictionTimer, ref _serverAnchorDragAccumulator,
            in input,
            in _player.movement, in _player.friction, in _player.input,
            _rb.mass, dt, _player.GetSlowMultiplier());

        // Apply velocity and rotation only — let physics integrate position
        _rb.linearVelocity = velocity;
        transform.rotation = Quaternion.Euler(0, 0, rotation);
        if (_player != null && !_player.enabled)
        {
            _player.ApplyExternalVisualTiltState(input.VisualBankAngle, input.VisualPitchAngle);
        }

        PublishAuthoritativeState(
            input.Tick,
            velocity,
            rotation,
            input.VisualBankAngle,
            input.VisualPitchAngle,
            _serverFrictionTimer,
            _serverAnchorDragAccumulator,
            dt);
    }

    [ServerRpc]
    private void SubmitPrimaryFireServerRpc(NetFireRequest request, ServerRpcParams rpcParams = default)
    {
        HandlePrimaryFireServer(request);
    }

    [ServerRpc]
    private void SubmitBeamStateServerRpc(NetBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleBeamStateServer(state);
    }

    [ServerRpc]
    private void SubmitFireTrailStateServerRpc(NetFireTrailState state, ServerRpcParams rpcParams = default)
    {
        HandleFireTrailStateServer(state);
    }

    [ServerRpc]
    private void SubmitReflectActivationServerRpc(ServerRpcParams rpcParams = default)
    {
        HandleReflectActivationServer();
    }

    [ServerRpc]
    private void SubmitTeleportServerRpc(NetTeleportState state, ServerRpcParams rpcParams = default)
    {
        HandleTeleportServer(state);
    }

    [ServerRpc]
    private void SubmitClass2ShieldServerRpc(NetClass2ShieldState state, ServerRpcParams rpcParams = default)
    {
        HandleClass2ShieldServer(state);
    }

    [ServerRpc]
    private void SubmitTractorBeamServerRpc(NetTractorBeamState state, ServerRpcParams rpcParams = default)
    {
        HandleTractorBeamServer(state);
    }

    [ServerRpc]
    private void SubmitTriggerBombLaunchServerRpc(NetTriggerBombLaunchState state, ServerRpcParams rpcParams = default)
    {
        HandleTriggerBombLaunchServer(state);
    }

    [ServerRpc]
    private void SubmitTriggerBombDetonateServerRpc(NetTriggerBombDetonateState state, ServerRpcParams rpcParams = default)
    {
        HandleTriggerBombDetonateServer(state);
    }

    [ServerRpc]
    private void SubmitFaerieShiftServerRpc(NetAbilityToggleState state, ServerRpcParams rpcParams = default)
    {
        HandleFaerieShiftServer(state);
    }

    [ServerRpc]
    private void SubmitInvisibilityServerRpc(NetAbilityToggleState state, ServerRpcParams rpcParams = default)
    {
        HandleInvisibilityServer(state);
    }

    // ===== CLIENT: RECONCILIATION (Owner) & INTERPOLATION BUFFER (Non-owner) =====

    [ClientRpc]
    private void BroadcastStateClientRpc(NetStateSnapshot serverState)
    {
        if (IsOwner)
        {
            Reconcile(serverState);
        }
        else if (!IsServer)
        {
            BufferInterpolationState(serverState);
        }
    }

    [ClientRpc]
    private void BroadcastProjectileSpawnClientRpc(NetProjectileSpawnData spawnData)
    {
        if (IsServer || (IsOwner && !IsServer))
        {
            return;
        }

        GameObject prefab = ResolveProjectileVisualPrefab(spawnData.VisualType);
        if (prefab == null)
        {
            return;
        }

        GameObject projectileObject = Instantiate(prefab, spawnData.SpawnPosition, Quaternion.identity);
        if (!projectileObject.TryGetComponent(out ProjectileScript projectile))
        {
            Destroy(projectileObject);
            return;
        }

        projectile.targetTag = _player != null ? _player.enemyTag : string.Empty;
        projectile.SetCosmeticOnly(true);
        projectile.Initialize(
            spawnData.Direction,
            spawnData.InheritedVelocity,
            spawnData.Speed,
            spawnData.Damage,
            spawnData.Lifetime,
            spawnData.ImpactForce,
            _player);

        if (spawnData.CanPierce)
        {
            projectile.EnablePiercing(spawnData.PierceMultiplier);
        }

        if (spawnData.AppliesSlow)
        {
            projectile.EnableSlow(spawnData.SlowMultiplier, spawnData.SlowDuration);
        }

        // Play fire sound on remote client
        if (_player != null)
        {
            SoundEffect fireSound = ResolveFireSound(spawnData.VisualType);
            fireSound?.Play(_player.GetAvailableAudioSource());
        }
    }

    [ClientRpc]
    private void BroadcastBeamStateClientRpc(NetBeamState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        Beam beamAbility = GetComponent<Beam>();
        beamAbility?.ApplyNetworkBeamState(state.IsFiring, authoritative: false, requestedTick: state.Tick);
    }

    [ClientRpc]
    private void BroadcastFireTrailStateClientRpc(NetFireTrailState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        FireWall fireWall = GetComponent<FireWall>();
        fireWall?.ApplyNetworkTrailState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastReflectActivationClientRpc()
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        Reflector reflector = GetComponent<Reflector>();
        reflector?.ApplyNetworkReflectActivation(authoritative: false);
    }

    [ClientRpc]
    private void BroadcastCombatStateClientRpc(float health, float shield, Vector2 hitPoint, int source, bool shieldHit, bool shieldBreak, float impactForce)
    {
        if (_player == null)
        {
            return;
        }

        DamageSource damageSource = (DamageSource)source;
        _player.ApplyAuthoritativeCombatState(health, shield, hitPoint, damageSource, shieldHit, shieldBreak);
    }

    [ClientRpc]
    private void BroadcastTeleportClientRpc(NetTeleportState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        Teleport teleportAbility = GetComponent<Teleport>();
        teleportAbility?.ApplyNetworkTeleport(state.TargetPosition, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastClass2ShieldClientRpc(NetClass2ShieldState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        Class2Shield shieldAbility = GetComponent<Class2Shield>();
        shieldAbility?.ApplyNetworkShieldActivation(authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTractorBeamClientRpc(NetTractorBeamState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        TractorBeam tractorBeam = GetComponent<TractorBeam>();
        tractorBeam?.ApplyNetworkTractorBeamState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTriggerBombLaunchClientRpc(NetTriggerBombLaunchState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        triggerBomb?.ApplyNetworkBombLaunch(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastTriggerBombDetonateClientRpc(NetTriggerBombDetonateState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        triggerBomb?.ApplyNetworkBombDetonation(state, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastFaerieShiftClientRpc(NetAbilityToggleState state)
    {
        if (IsOwner && !IsServer)
        {
            return;
        }

        FaerieShift faerieShift = GetComponent<FaerieShift>();
        faerieShift?.ApplyNetworkShiftState(state.IsActive, authoritative: false);
    }

    [ClientRpc]
    private void BroadcastInvisibilityClientRpc(NetAbilityToggleState state)
    {
        if (IsOwner && !IsServer && state.IsActive)
        {
            return;
        }

        Invisibility invisibility = GetComponent<Invisibility>();
        invisibility?.ApplyNetworkInvisibilityState(state.IsActive, authoritative: false);
    }

    private void PublishAuthoritativeState(
        int tick,
        Vector2 velocity,
        float rotation,
        float visualBankAngle,
        float visualPitchAngle,
        float frictionTimer,
        float anchorDragAccumulator,
        float dt)
    {
        Vector2 expectedPosition = _rb.position + velocity * dt;
        NetStateSnapshot state = new NetStateSnapshot
        {
            Tick = tick,
            Position = expectedPosition,
            Rotation = rotation,
            Velocity = velocity,
            VisualBankAngle = visualBankAngle,
            VisualPitchAngle = visualPitchAngle,
            AnchorDragAccumulator = anchorDragAccumulator,
            FrictionTimer = frictionTimer,
            FrictionEnabled = _player != null && _player.IsFrictionEnabled,
        };

        int histIdx = tick % SERVER_STATE_BUFFER_SIZE;
        _stateHistory[histIdx] = state;
        _stateHistoryHead = tick;

        BroadcastStateClientRpc(state);
    }

    private void HandlePrimaryFireServer(NetFireRequest request)
    {
        GameObject projectilePrefab = ResolveProjectileVisualPrefab(request.VisualType);
        if (_player == null || projectilePrefab == null)
        {
            return;
        }

        float tickInterval = NetTickUtil.TickInterval > 0f ? NetTickUtil.TickInterval : Time.fixedDeltaTime;
        int cooldownTicks = Mathf.Max(1, Mathf.CeilToInt(_player.PrimaryFireCooldown / Mathf.Max(0.0001f, tickInterval)));
        bool isNewVolleyTick = request.Tick != _lastServerPrimaryFireTick;
        if (isNewVolleyTick && request.Tick < _lastServerPrimaryFireTick + cooldownTicks)
        {
            return;
        }

        if (isNewVolleyTick)
        {
            _lastServerPrimaryFireTime = Time.time;
            _lastServerPrimaryFireTick = request.Tick;
        }

        GameObject projectileObject = Instantiate(projectilePrefab, request.SpawnPosition, Quaternion.identity);
        if (!projectileObject.TryGetComponent(out ProjectileScript projectile))
        {
            Destroy(projectileObject);
            return;
        }

        projectile.targetTag = _player.enemyTag;
        projectile.SetNetworkAuthority(this, request.Tick);
        projectile.Initialize(
            request.Direction,
            request.InheritedVelocity,
            request.Speed,
            request.Damage,
            request.Lifetime,
            request.ImpactForce,
            _player);

        if (request.CanPierce)
        {
            projectile.EnablePiercing(request.PierceMultiplier);
        }

        if (request.AppliesSlow)
        {
            projectile.EnableSlow(request.SlowMultiplier, request.SlowDuration);
        }

        // Play fire sound on host for remote player's projectiles
        if (!IsOwner && _player != null)
        {
            SoundEffect fireSound = ResolveFireSound(request.VisualType);
            fireSound?.Play(_player.GetAvailableAudioSource());
        }

        BroadcastProjectileSpawnClientRpc(new NetProjectileSpawnData
        {
            Tick = request.Tick,
            SpawnPosition = request.SpawnPosition,
            Direction = request.Direction,
            InheritedVelocity = request.InheritedVelocity,
            Speed = request.Speed,
            Damage = request.Damage,
            Lifetime = request.Lifetime,
            ImpactForce = request.ImpactForce,
            RecoilForce = request.RecoilForce,
            ApplyRecoil = request.ApplyRecoil,
            PierceMultiplier = request.PierceMultiplier,
            SlowMultiplier = request.SlowMultiplier,
            SlowDuration = request.SlowDuration,
            CanPierce = request.CanPierce,
            AppliesSlow = request.AppliesSlow,
            VisualType = request.VisualType,
        });

        if (request.ApplyRecoil)
        {
            _player.ApplyRecoil(request.RecoilForce);
        }
    }

    private void HandleBeamStateServer(NetBeamState state)
    {
        Beam beamAbility = GetComponent<Beam>();
        if (beamAbility == null)
        {
            return;
        }

        beamAbility.ApplyNetworkBeamState(state.IsFiring, authoritative: true, requestedTick: state.Tick);
        BroadcastBeamStateClientRpc(state);
    }

    private void HandleFireTrailStateServer(NetFireTrailState state)
    {
        FireWall fireWall = GetComponent<FireWall>();
        if (fireWall == null)
        {
            return;
        }

        fireWall.ApplyNetworkTrailState(state.IsActive, authoritative: true);
        BroadcastFireTrailStateClientRpc(state);
    }

    private void HandleReflectActivationServer()
    {
        Reflector reflector = GetComponent<Reflector>();
        if (reflector == null)
        {
            return;
        }

        reflector.ApplyNetworkReflectActivation(authoritative: true);
        BroadcastReflectActivationClientRpc();
    }

    private void HandleTeleportServer(NetTeleportState state)
    {
        Teleport teleportAbility = GetComponent<Teleport>();
        if (teleportAbility == null)
        {
            return;
        }

        teleportAbility.ApplyNetworkTeleport(state.TargetPosition, authoritative: true);
        BroadcastTeleportClientRpc(state);
    }

    private void HandleClass2ShieldServer(NetClass2ShieldState state)
    {
        Class2Shield shieldAbility = GetComponent<Class2Shield>();
        if (shieldAbility == null || !state.IsActive)
        {
            return;
        }

        shieldAbility.ApplyNetworkShieldActivation(authoritative: true);
        BroadcastClass2ShieldClientRpc(state);
    }

    private void HandleTractorBeamServer(NetTractorBeamState state)
    {
        TractorBeam tractorBeam = GetComponent<TractorBeam>();
        if (tractorBeam == null)
        {
            return;
        }

        tractorBeam.ApplyNetworkTractorBeamState(state.IsActive, authoritative: true);
        BroadcastTractorBeamClientRpc(state);
    }

    private void HandleTriggerBombLaunchServer(NetTriggerBombLaunchState state)
    {
        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        if (triggerBomb == null)
        {
            return;
        }

        triggerBomb.ApplyNetworkBombLaunch(state, authoritative: true);
        BroadcastTriggerBombLaunchClientRpc(state);
    }

    private void HandleTriggerBombDetonateServer(NetTriggerBombDetonateState state)
    {
        TriggerBomb triggerBomb = GetComponent<TriggerBomb>();
        if (triggerBomb == null)
        {
            return;
        }

        triggerBomb.ApplyNetworkBombDetonation(state, authoritative: true);
        BroadcastTriggerBombDetonateClientRpc(state);
    }

    private void HandleFaerieShiftServer(NetAbilityToggleState state)
    {
        FaerieShift faerieShift = GetComponent<FaerieShift>();
        if (faerieShift == null)
        {
            return;
        }

        faerieShift.ApplyNetworkShiftState(state.IsActive, authoritative: true);
        BroadcastFaerieShiftClientRpc(state);
    }

    private void HandleInvisibilityServer(NetAbilityToggleState state)
    {
        Invisibility invisibility = GetComponent<Invisibility>();
        if (invisibility == null)
        {
            return;
        }

        invisibility.ApplyNetworkInvisibilityState(state.IsActive, authoritative: true);
        BroadcastInvisibilityClientRpc(state);
    }

    public void BroadcastFireHazardSpawn(NetFireHazardSpawnData spawnData)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastFireHazardSpawnClientRpc(spawnData);
    }

    [ClientRpc]
    private void BroadcastFireHazardSpawnClientRpc(NetFireHazardSpawnData spawnData)
    {
        if (IsServer)
        {
            return;
        }

        FireWall fireWall = GetComponent<FireWall>();
        fireWall?.SpawnRemoteHazard(spawnData);
    }

    public void BroadcastCombatState(float health, float shield, Vector3 hitPoint, DamageSource source, bool shieldHit, bool shieldBreak, float impactForce)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastCombatStateClientRpc(health, shield, hitPoint, (int)source, shieldHit, shieldBreak, impactForce);
    }

    // ===== RECONCILIATION =====

    private void Reconcile(NetStateSnapshot serverState)
    {
        // Don't process out-of-order or stale snapshots
        if (serverState.Tick <= _lastReceivedServerTick) return;
        _lastReceivedServerTick = serverState.Tick;

        int bufIdx = serverState.Tick % CLIENT_INPUT_BUFFER_SIZE;
        NetStateSnapshot predicted = _predictionBuffer[bufIdx];

        // Compare predicted vs authoritative position
        float posError = Vector2.Distance(predicted.Position, serverState.Position);

        if (posError <= _reconciliationThreshold) return; // close enough, no correction

        // --- Correction needed: rewind and replay ---
        Vector2 velocity = serverState.Velocity;
        Vector2 position = serverState.Position;
        float rotation = serverState.Rotation;
        float frictionTimer = serverState.FrictionTimer;
        float anchorDragAccumulator = serverState.AnchorDragAccumulator;

        float dt = NetTickUtil.TickInterval;
        if (dt <= 0f) dt = Time.fixedDeltaTime;

        int currentTick = NetTickUtil.CurrentTick;

        // Replay all inputs from (serverState.Tick + 1) through currentTick
        for (int tick = serverState.Tick + 1; tick <= currentTick; tick++)
        {
            int idx = tick % CLIENT_INPUT_BUFFER_SIZE;
            NetInputSnapshot input = _inputBuffer[idx];

            // Validate that this buffer slot actually holds data for this tick
            if (input.Tick != tick) continue;

            MovementSimulation.SimulateTick(
                ref velocity, ref position, ref rotation,
                ref frictionTimer, ref anchorDragAccumulator,
                in input,
                in _player.movement, in _player.friction, in _player.input,
                _rb.mass, dt, _player.GetSlowMultiplier());

            // Manually integrate position (no physics step during replay)
            MovementSimulation.IntegratePosition(ref position, velocity, dt);

            // Update prediction buffer with corrected state
            _predictionBuffer[idx] = new NetStateSnapshot
            {
                Tick = tick,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                VisualBankAngle = serverState.VisualBankAngle,
                VisualPitchAngle = serverState.VisualPitchAngle,
                AnchorDragAccumulator = anchorDragAccumulator,
                FrictionTimer = frictionTimer,
                FrictionEnabled = input.FrictionEnabled,
            };
        }

        // Apply corrected state to Rigidbody.
        // Position from replay already includes integration, so set it directly.
        // The next physics step will add velocity*dt, so subtract it to compensate.
        _rb.linearVelocity = velocity;
        _rb.position = position - velocity * dt;
        transform.rotation = Quaternion.Euler(0, 0, rotation);

        // Sync mutable sim state
        _ownerFrictionTimer = frictionTimer;
        _ownerAnchorDragAccumulator = anchorDragAccumulator;
    }

    // ===== INTERPOLATION (Remote players) =====

    private void BufferInterpolationState(NetStateSnapshot state)
    {
        if (_interpolationBuffer == null || _interpolationBuffer.Length == 0)
        {
            return;
        }

        int idx = _interpWriteIndex % _interpolationBuffer.Length;
        _interpolationBuffer[idx] = state;
        _interpWriteIndex++;
        _interpCount = Mathf.Min(_interpCount + 1, _interpolationBuffer.Length);
    }

    private void InterpolateRemote()
    {
        if (_interpCount < 2) return; // need at least 2 snapshots to interpolate

        // Find the two most recent snapshots to interpolate between
        int newestIdx = (_interpWriteIndex - 1) % _interpolationBuffer.Length;
        if (newestIdx < 0) newestIdx += _interpolationBuffer.Length;

        int olderIdx = (_interpWriteIndex - 2) % _interpolationBuffer.Length;
        if (olderIdx < 0) olderIdx += _interpolationBuffer.Length;

        NetStateSnapshot from = _interpolationBuffer[olderIdx];
        NetStateSnapshot to = _interpolationBuffer[newestIdx];

        int tickDelta = to.Tick - from.Tick;
        if (tickDelta <= 0) return;

        float tickInterval = NetTickUtil.TickInterval;
        if (tickInterval <= 0f) tickInterval = Time.fixedDeltaTime;

        float totalDuration = tickDelta * tickInterval;

        _interpTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_interpTimer / totalDuration);

        // Interpolate position and rotation
        Vector2 interpPos = Vector2.Lerp(from.Position, to.Position, t);
        float interpRot = Mathf.LerpAngle(from.Rotation, to.Rotation, t);
        Vector2 interpVel = Vector2.Lerp(from.Velocity, to.Velocity, t);
        float interpBank = Mathf.Lerp(from.VisualBankAngle, to.VisualBankAngle, t);
        float interpPitch = Mathf.Lerp(from.VisualPitchAngle, to.VisualPitchAngle, t);

        _rb.position = interpPos;
        transform.rotation = Quaternion.Euler(0, 0, interpRot);
        _rb.linearVelocity = interpVel; // for visual systems that read velocity
        _player?.ApplyExternalVisualTiltState(interpBank, interpPitch);

        // When we've finished interpolating to 'to', advance
        if (t >= 1f)
        {
            _interpTimer -= totalDuration;
        }
    }
}
