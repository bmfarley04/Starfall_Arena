using Unity.Netcode;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

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
    // ===== CONFIGURATION =====

    [Header("Reconciliation")]
    [Tooltip("Position error (units) before a server correction triggers replay")]
    [SerializeField] private float _reconciliationThreshold = 0.01f;

    [Header("Interpolation")]
    [Tooltip("Number of ticks to buffer for remote player interpolation")]
    [SerializeField] private int _interpolationBufferTicks = 2;

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

    // ===== LIFECYCLE =====

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _player = GetComponent<Player>();
        _rb = GetComponent<Rigidbody2D>();

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
            }
        }

        if (IsServer)
        {
            _stateHistory = new NetStateSnapshot[SERVER_STATE_BUFFER_SIZE];
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_player != null)
        {
            if (IsOwner)
            {
                _player.externalMovementControl = false;
            }
            else
            {
                // Re-enable Player if we disabled it (safety for pooling / round transitions)
                _player.enabled = true;
                _rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        base.OnNetworkDespawn();
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

    // ===== OWNER: CLIENT-SIDE PREDICTION =====

    private void OwnerTick()
    {
        int tick = NetTickUtil.CurrentTick;
        if (tick <= _lastSentTick) return; // don't double-simulate the same tick
        _lastSentTick = tick;

        float dt = Time.fixedDeltaTime;

        // 1. Sample input from Player's read-only getters
        NetInputSnapshot input = new NetInputSnapshot
        {
            Tick = tick,
            Thrust = _player.IsThrustPressed,
            LookInput = _player.LookInput,
            Anchor = _player.IsAnchored,
            FrictionEnabled = _player.IsFrictionEnabled,
        };

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
        _predictionBuffer[bufIdx] = new NetStateSnapshot
        {
            Tick = tick,
            Position = expectedPosition,
            Rotation = rotation,
            Velocity = velocity,
            AnchorDragAccumulator = _ownerAnchorDragAccumulator,
            FrictionTimer = _ownerFrictionTimer,
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

        PublishAuthoritativeState(
            input.Tick,
            velocity,
            rotation,
            _serverFrictionTimer,
            _serverAnchorDragAccumulator,
            dt);
    }

    // ===== CLIENT: RECONCILIATION (Owner) & INTERPOLATION BUFFER (Non-owner) =====

    [ClientRpc]
    private void BroadcastStateClientRpc(NetStateSnapshot serverState)
    {
        if (IsOwner)
        {
            Reconcile(serverState);
        }
        else
        {
            BufferInterpolationState(serverState);
        }
    }

    private void PublishAuthoritativeState(
        int tick,
        Vector2 velocity,
        float rotation,
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
            AnchorDragAccumulator = anchorDragAccumulator,
            FrictionTimer = frictionTimer,
        };

        int histIdx = tick % SERVER_STATE_BUFFER_SIZE;
        _stateHistory[histIdx] = state;
        _stateHistoryHead = tick;

        BroadcastStateClientRpc(state);
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
                AnchorDragAccumulator = anchorDragAccumulator,
                FrictionTimer = frictionTimer,
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

        _rb.position = interpPos;
        transform.rotation = Quaternion.Euler(0, 0, interpRot);
        _rb.linearVelocity = interpVel; // for visual systems that read velocity

        // When we've finished interpolating to 'to', advance
        if (t >= 1f)
        {
            _interpTimer -= totalDuration;
        }
    }
}
