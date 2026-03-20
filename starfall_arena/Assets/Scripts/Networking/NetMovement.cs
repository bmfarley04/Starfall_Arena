using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Core networked movement component. Attach to the player prefab alongside Player.
/// When a network session is active this component takes over physics:
///   - Owner:      client-side prediction + reconciliation
///   - Server:     authoritative simulation + state broadcast
///   - Non-owner:  interpolation between server snapshots (Player component disabled)
///
/// When no network session is running (local multiplayer), this component
/// does nothing — Player.FixedUpdate handles movement as before.
///
/// Split into partial classes:
///   - NetMovement.cs          — Core lifecycle, tick loop, prediction, reconciliation, interpolation
///   - NetMovement.Abilities.cs — Ability request/handler/broadcast RPCs
///   - NetMovement.Combat.cs   — Projectile spawning, combat state, death, reflected projectiles, audio
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public partial class NetMovement : NetworkBehaviour
{
    private const char AugmentEntrySeparator = ';';
    private const char AugmentFieldSeparator = ':';

    private static readonly List<NetMovement> ActiveInstances = new List<NetMovement>();
    private readonly NetworkVariable<bool> _movementLocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>
    /// Replicated player index (1 = Player1, 2 = Player2). Used on clients to
    /// set gameObject.tag and call RefreshCombatTags(), because Unity tags are
    /// not replicated by NGO. Without this, remote proxies have wrong enemyTag
    /// and cosmetic projectiles/beams pass through ships visually.
    /// </summary>
    private readonly NetworkVariable<byte> _networkPlayerIndex = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

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
    private int _lastServerAccuracyVolleyTick = -999999;
    private NetProjectileVisualType _lastServerAccuracyVolleyType;
    private int _lastServerAccuracyAttackId = Player.InvalidAttackId;
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

        _movementLocked.OnValueChanged += HandleMovementLockedChanged;
        _networkPlayerIndex.OnValueChanged += HandlePlayerIndexChanged;

        // Apply player index if already set (e.g. late join where the
        // NetworkVariable value arrives with the spawn snapshot).
        if (_networkPlayerIndex.Value != 0)
        {
            ApplyPlayerIndex(_networkPlayerIndex.Value);
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
                _player.enabled = true;
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
                playerInput.ActivateInput();
                AssignOwnerCameraAndTracking(playerInput);
                Debug.Log($"[NetMovement] Owner input activated for {name}. ControlScheme={playerInput.currentControlScheme}, Camera={(playerInput.camera != null ? playerInput.camera.name : "null")}, LocalClientId={NetworkManager.LocalClientId}");
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

            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.DeactivateInput();
                playerInput.enabled = false;
            }

            Debug.Log($"[NetMovement] Remote proxy configured for {name} on client. OwnerClientId={OwnerClientId}, LocalClientId={NetworkManager.LocalClientId}");

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

        ApplyMovementLock(_movementLocked.Value);
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

        _movementLocked.OnValueChanged -= HandleMovementLockedChanged;
        _networkPlayerIndex.OnValueChanged -= HandlePlayerIndexChanged;

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

    private void HandleMovementLockedChanged(bool previousValue, bool newValue)
    {
        ApplyMovementLock(newValue);
    }

    private void HandlePlayerIndexChanged(byte previousValue, byte newValue)
    {
        ApplyPlayerIndex(newValue);
    }

    private void ApplyPlayerIndex(byte index)
    {
        if (index == 0) return;

        string tag = index == 1 ? "Player1" : "Player2";
        gameObject.tag = tag;

        if (_player == null)
        {
            _player = GetComponent<Player>();
        }

        // RefreshCombatTags works on disabled components — it just sets string fields.
        _player?.RefreshCombatTags();
    }

    /// <summary>
    /// Called by the server (SceneManager) after spawning to replicate the player
    /// tag to all clients via NetworkVariable.
    /// </summary>
    public void SetNetworkPlayerIndex(byte index)
    {
        if (!IsServer) return;
        _networkPlayerIndex.Value = index;
    }

    // ===== STATIC HELPERS =====

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

    public void SetMovementLockedAuthoritative(bool isLocked)
    {
        if (!NetTickUtil.IsActive)
        {
            ApplyMovementLock(isLocked);
            return;
        }

        if (IsServer)
        {
            _movementLocked.Value = isLocked;
            ApplyMovementLock(isLocked);
            BroadcastMovementLockClientRpc(isLocked);
            return;
        }

        RequestMovementLockServerRpc(isLocked);
    }

    public void SetAbility4LockedAuthoritative(bool isLocked)
    {
        if (!NetTickUtil.IsActive)
        {
            ApplyAbility4Lock(isLocked);
            return;
        }

        if (IsServer)
        {
            ApplyAbility4Lock(isLocked);
            BroadcastAbility4LockClientRpc(isLocked);
            return;
        }

        RequestAbility4LockServerRpc(isLocked);
    }

    // ===== LAG COMPENSATION =====

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

    private void AssignOwnerCameraAndTracking(PlayerInput playerInput)
    {
        CinemachineCamera targetCinemachine = FindFirstObjectByType<CinemachineCamera>();
        if (targetCinemachine != null)
        {
            targetCinemachine.Target.TrackingTarget = transform;
        }

        Camera mainCamera = SelectGameplayCamera();
        if (mainCamera != null)
        {
            playerInput.camera = mainCamera;
        }
    }

    private Camera SelectGameplayCamera()
    {
        Camera mainCamera = Camera.main;
        if (IsUsableGameplayCamera(mainCamera))
        {
            return mainCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera candidate in cameras)
        {
            if (IsUsableGameplayCamera(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsUsableGameplayCamera(Camera candidate)
    {
        if (candidate == null || !candidate.isActiveAndEnabled)
        {
            return false;
        }

        string cameraName = candidate.name ?? string.Empty;
        if (cameraName.Contains("UI"))
        {
            return false;
        }

        return true;
    }

    private void ApplyMovementLock(bool isLocked)
    {
        if (_player == null)
        {
            _player = GetComponent<Player>();
        }

        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        if (_player == null)
        {
            return;
        }

        _player.isMovementLocked = isLocked;
        if (isLocked && _rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }
    }

    private void ApplyAbility4Lock(bool isLocked)
    {
        if (_player == null)
        {
            _player = GetComponent<Player>();
        }

        if (_player == null)
        {
            return;
        }

        if (isLocked)
        {
            _player.LockAbility4();
        }
        else
        {
            _player.UnlockAbility4();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMovementLockServerRpc(bool isLocked)
    {
        _movementLocked.Value = isLocked;
        ApplyMovementLock(isLocked);
        BroadcastMovementLockClientRpc(isLocked);
    }

    [ClientRpc]
    private void BroadcastMovementLockClientRpc(bool isLocked)
    {
        if (IsServer)
        {
            return;
        }

        ApplyMovementLock(isLocked);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAbility4LockServerRpc(bool isLocked)
    {
        ApplyAbility4Lock(isLocked);
        BroadcastAbility4LockClientRpc(isLocked);
    }

    [ClientRpc]
    private void BroadcastAbility4LockClientRpc(bool isLocked)
    {
        if (IsServer)
        {
            return;
        }

        ApplyAbility4Lock(isLocked);
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

            // Player.Update is disabled on server copies, so HandleShieldRegeneration
            // and UpdateSlowVisuals never run. Drive them here.
            if (_player != null)
            {
                float prevShield = _player.currentShield;
                _player.TickShieldRegeneration(Time.fixedDeltaTime);

                // Drive regen visual on the host for the client's ship
                bool isRegenerating = _player.currentShield > prevShield && _player.currentShield < _player.maxShield;
                if (_player.shieldController != null)
                {
                    _player.shieldController.SetRegeneration(isRegenerating);
                }

                _player.TickAugmentEffects();

                // Stop slow particle when slow expires
                _player.TickSlowVisuals();
            }
        }

        if (!IsOwner && !IsServer)
        {
            InterpolateRemote();
        }
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

        // Drive thruster visuals on the owner (Player.FixedUpdate skips the
        // _isThrusting assignment when externalMovementControl is true).
        _player.ApplyNetworkThrustState(_player.IsThrustPressed);

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
            AbilityRotationMultiplier = _player.GetAbilityRotationMultiplier(),
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
            _rb.mass, dt, _player.GetSlowMultiplier(),
            abilityRotationMultiplier: input.AbilityRotationMultiplier);

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
                dt,
                input.Thrust);
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
            _rb.mass, dt, _player.GetSlowMultiplier(),
            abilityRotationMultiplier: input.AbilityRotationMultiplier);

        // Apply velocity and rotation only — let physics integrate position
        _rb.linearVelocity = velocity;
        transform.rotation = Quaternion.Euler(0, 0, rotation);
        if (_player != null && !_player.enabled)
        {
            _player.ApplyExternalVisualTiltState(input.VisualBankAngle, input.VisualPitchAngle);
            _player.ApplyNetworkThrustState(input.Thrust);
        }

        PublishAuthoritativeState(
            input.Tick,
            velocity,
            rotation,
            input.VisualBankAngle,
            input.VisualPitchAngle,
            _serverFrictionTimer,
            _serverAnchorDragAccumulator,
            dt,
            input.Thrust);
    }

    private void PublishAuthoritativeState(
        int tick,
        Vector2 velocity,
        float rotation,
        float visualBankAngle,
        float visualPitchAngle,
        float frictionTimer,
        float anchorDragAccumulator,
        float dt,
        bool thrust)
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
            Thrust = thrust,
            Shield = _player != null ? _player.currentShield : 0f,
            Health = _player != null ? _player.CurrentHealth : 0f,
        };

        int histIdx = tick % SERVER_STATE_BUFFER_SIZE;
        _stateHistory[histIdx] = state;
        _stateHistoryHead = tick;

        BroadcastStateClientRpc(state);
    }

    // ===== STATE BROADCAST =====

    [ClientRpc]
    private void BroadcastStateClientRpc(NetStateSnapshot serverState)
    {
        if (IsOwner)
        {
            if (_player != null)
            {
                _player.ApplyNetworkHealthState(serverState.Health);
                _player.ApplyNetworkShieldState(serverState.Shield);
            }

            Reconcile(serverState);
        }
        else if (!IsServer)
        {
            BufferInterpolationState(serverState);
        }
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
                _rb.mass, dt, _player.GetSlowMultiplier(),
                abilityRotationMultiplier: input.AbilityRotationMultiplier);

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

    private float _remoteLastShield = -1f;

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

        // Drive thruster visuals from authoritative thrust state
        if (_player != null)
        {
            _player.ApplyNetworkThrustState(to.Thrust);
        }

        // Drive shield regen visuals from authoritative shield value
        if (_player != null)
        {
            float prevShield = _remoteLastShield < 0f ? to.Shield : _remoteLastShield;
            _player.ApplyNetworkHealthState(to.Health);
            _player.ApplyNetworkShieldState(to.Shield);

            bool isRegenerating = to.Shield > prevShield && to.Shield < _player.maxShield;
            if (_player.shieldController != null)
            {
                _player.shieldController.SetRegeneration(isRegenerating);
            }
            _remoteLastShield = to.Shield;

            _player.TickAugmentEffects();

            // Stop slow particle when slow expires
            _player.TickSlowVisuals();
        }

        // When we've finished interpolating to 'to', advance
        if (t >= 1f)
        {
            _interpTimer -= totalDuration;
        }
    }

    public void SyncCurrentAugmentLoadoutAuthoritative(int currentRound, List<NetworkAugmentLoadoutEntry> entries)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsServer)
        {
            return;
        }

        BroadcastAugmentLoadoutClientRpc(SerializeAugmentLoadout(entries), currentRound);
    }

    [ClientRpc]
    private void BroadcastAugmentLoadoutClientRpc(string serializedLoadout, int currentRound)
    {
        if (IsServer)
        {
            return;
        }

        if (_player == null)
        {
            _player = GetComponent<Player>();
        }

        if (_player == null)
        {
            return;
        }

        _player.ImportNetworkAugmentLoadout(DeserializeAugmentLoadout(serializedLoadout), currentRound);
    }

    private static string SerializeAugmentLoadout(List<NetworkAugmentLoadoutEntry> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(entries.Count * 40);
        for (int i = 0; i < entries.Count; i++)
        {
            NetworkAugmentLoadoutEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.augmentId))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(AugmentEntrySeparator);
            }

            builder.Append(entry.augmentId);
            builder.Append(AugmentFieldSeparator);
            builder.Append(entry.roundAcquired);
            builder.Append(AugmentFieldSeparator);
            builder.Append(entry.stateFlags);
        }

        return builder.ToString();
    }

    private static List<NetworkAugmentLoadoutEntry> DeserializeAugmentLoadout(string serializedLoadout)
    {
        List<NetworkAugmentLoadoutEntry> entries = new List<NetworkAugmentLoadoutEntry>();
        if (string.IsNullOrWhiteSpace(serializedLoadout))
        {
            return entries;
        }

        string[] serializedEntries = serializedLoadout.Split(AugmentEntrySeparator);
        for (int i = 0; i < serializedEntries.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(serializedEntries[i]))
            {
                continue;
            }

            string[] fields = serializedEntries[i].Split(AugmentFieldSeparator);
            if (fields.Length < 2)
            {
                continue;
            }

            if (!int.TryParse(fields[1], out int roundAcquired))
            {
                continue;
            }

            byte stateFlags = 0;
            if (fields.Length >= 3)
            {
                byte.TryParse(fields[2], out stateFlags);
            }

            entries.Add(new NetworkAugmentLoadoutEntry
            {
                augmentId = fields[0],
                roundAcquired = roundAcquired,
                stateFlags = stateFlags
            });
        }

        return entries;
    }
}
