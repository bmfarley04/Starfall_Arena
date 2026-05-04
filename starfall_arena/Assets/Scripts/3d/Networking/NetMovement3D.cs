using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShipFlight3D))]
[RequireComponent(typeof(Player3D))]
public class NetMovement3D : NetworkBehaviour
{
    private const int ClientInputBufferSize = 64;
    private const int ServerStateBufferSize = 120;
    private const int RecentMovementSideEffectWindowTicks = 6;
    private const int MaxBlockerSweepHits = 8;
    private const string NoOwnerCorrectionCause = "none";

    private static readonly List<NetMovement3D> ActiveInstances = new List<NetMovement3D>();
    private static readonly RaycastHit[] BlockerSweepHits = new RaycastHit[MaxBlockerSweepHits];

    private enum OwnerCorrectionCause
    {
        PredictionMissing,
        PositionError,
        AuthoritativeSnap
    }

    private struct RigidbodyDiagnosticState
    {
        public bool HasBody;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public bool IsKinematic;
        public RigidbodyInterpolation Interpolation;
    }

    [Header("Reconciliation")]
    [Tooltip("Position error in world units before a server correction triggers local replay.")]
    [SerializeField] private float reconciliationThreshold = 0.1f;

    [Tooltip("Logs every owner-side reconciliation correction with tick, input, error, and recent movement-side-effect context.")]
    [SerializeField] private bool logOwnerCorrections = true;

    [Header("Owner Movement Diagnostics")]
    [Tooltip("Enables owner-side movement diagnostics for the current 3D Invasion jitter investigation. When disabled, correction counters still update but verbose correction/config logs are suppressed.")]
    [SerializeField] private bool enableOwnerMovementDiagnostics = true;

    [Tooltip("Minimum owner correction distance required before a detailed correction log is printed. Raise this to reduce spam from harmless micro-corrections.")]
    [Min(0f)] [SerializeField] private float correctionLogMinDistance = 0.001f;

    [Tooltip("Seconds between rate-limited correction summary logs for this owner. Set to 0 to disable summary logs.")]
    [Min(0f)] [SerializeField] private float correctionSummaryIntervalSeconds = 1f;

    [Tooltip("Logs the network movement and ShipFlight3D configuration once when this player spawns, helping compare 3dTest against 3D Invasion setup.")]
    [SerializeField] private bool logMovementConfigOnSpawn = true;

    [Tooltip("Adds Rigidbody before/after details to owner correction logs so direct physics writes and snap/replay behavior can be inspected.")]
    [SerializeField] private bool logRigidbodyStateDrift = true;

    [Header("Interpolation")]
    [SerializeField] private NetInterpolationSettings3D interpolationSettings = new NetInterpolationSettings3D();

    [Header("Combat Rewind")]
    [Tooltip("Maximum lag-compensation window for player projectile and beam validation, in ticks.")]
    [SerializeField] private int maxCombatRewindTicks = 6;

    [Header("Movement Collision")]
    [Tooltip("Physics layers that network-predicted player movement treats as solid world blockers. Keep this limited to arena blockers such as asteroids and destroyed ships so prediction does not snag on VFX, projectiles, or HUD-only colliders.")]
    [SerializeField] private LayerMask networkMovementBlockerMask = 1 << 8;

    [Tooltip("Small distance kept between the predicted player body and blocker surfaces after a movement sweep. Raise only if players visibly vibrate against blocker colliders.")]
    [SerializeField] private float blockerSkinWidth = 0.05f;

    [Tooltip("Minimum tick movement distance required before NetMovement3D performs a blocker sweep. Lower values are safer but do more physics queries while nearly stationary.")]
    [SerializeField] private float minimumBlockerSweepDistance = 0.001f;

    private readonly NetworkVariable<bool> _movementLocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> _networkPlayerIndex = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Player3D _player;
    private PlayerInput3D _playerInput3D;
    private PlayerInput _playerInput;
    private PlayerCameraRig3D _cameraRig;
    private ShipFlight3D _shipFlight;
    private Dodge3D _dodgeAbility;
    private Rigidbody _rb;

    private NetInputSnapshot3D[] _inputBuffer;
    private NetStateSnapshot3D[] _predictionBuffer;
    private NetStateSnapshot3D[] _stateHistory;
    private readonly NetSnapshotInterpolator3D _remoteInterpolator = new NetSnapshotInterpolator3D();

    private MovementState3D _ownerState;
    private MovementState3D _serverState;
    private bool _ownerStateInitialized;
    private bool _serverStateInitialized;
    private bool _ownerFrictionEnabled;
    private bool _serverFrictionEnabled;
    private bool _loggedOwnerInputMissing;
    private bool _loggedOwnerShipFlightMissing;
    private bool _loggedMissingCombatBridge;
    private bool _pendingDodgeRequested;
    private NetDodgeKind3D _pendingDodgeKind = NetDodgeKind3D.Generic;
    private Vector3 _pendingDodgeDirection;
    private int _lastAcceptedDodgeTick = -1;
    private int _lastSentTick = -1;
    private int _lastReceivedServerTick = -1;
    private int _lastProcessedServerTick = -1;
    private int _stateHistoryHead = -1;

    private Vector3 _lastAppliedVisualVelocity;
    private bool _hasLastAppliedVisualVelocity;
    private bool _remoteDodgePresentationActive;
    private int _ownerCorrectionCount;
    private float _ownerCorrectionDistanceTotal;
    private Vector3 _lastCombatVelocityDelta;
    private int _lastCombatVelocityDeltaTick = -1;
    private Vector3 _lastCombatWarpPosition;
    private int _lastCombatWarpTick = -1;
    private Vector3 _lastBoundaryCorrectionPosition;
    private Vector3 _lastBoundaryCorrectionVelocity;
    private int _lastBoundaryCorrectionTick = -1;
    private NetDodgeKind3D _lastQueuedDodgeKind = NetDodgeKind3D.Generic;
    private Vector3 _lastQueuedDodgeDirection;
    private int _lastQueuedDodgeTick = -1;
    private int _correctionsInCurrentSummaryWindow;
    private float _correctionSummaryWindowStartedAt;
    private float _latestOwnerCorrectionDistance;
    private float _largestRecentOwnerCorrectionDistance;
    private float _ownerCorrectionsPerSecond;
    private int _blockerClampCount;
    private int _blockerClampsInCurrentSummaryWindow;
    private float _blockerClampSummaryWindowStartedAt;
    private float _blockerClampsPerSecond;
    private float _latestBlockerClampDistance;
    private float _latestBlockerSweepRadius;
    private int _lastBlockerClampTick = -1;
    private string _latestBlockerClampCollider = "none";
    private string _latestOwnerCorrectionLikelyCause = NoOwnerCorrectionCause;
    private string _latestOwnerCorrectionSideEffect = "none";
    private int _lastMovementResetTick = -1;
    private Vector3 _lastMovementResetPosition;
    private bool _lastMovementResetCamera;

    public byte PlayerSlot => _networkPlayerIndex.Value;
    public NetInterpolationDiagnostics3D InterpolationDiagnostics => _remoteInterpolator.Diagnostics;
    public int OwnerCorrectionCount => _ownerCorrectionCount;
    public float AverageOwnerCorrectionDistance => _ownerCorrectionCount > 0 ? _ownerCorrectionDistanceTotal / _ownerCorrectionCount : 0f;
    public float LatestOwnerCorrectionDistance => _latestOwnerCorrectionDistance;
    public float LargestRecentOwnerCorrectionDistance => _largestRecentOwnerCorrectionDistance;
    public float OwnerCorrectionsPerSecond => _ownerCorrectionsPerSecond;
    public string LatestOwnerCorrectionLikelyCause => _latestOwnerCorrectionLikelyCause;
    public string LatestOwnerCorrectionSideEffect => _latestOwnerCorrectionSideEffect;
    public int BlockerClampCount => _blockerClampCount;
    public float BlockerClampsPerSecond => _blockerClampsPerSecond;
    public float LatestBlockerClampDistance => _latestBlockerClampDistance;
    public float LatestBlockerSweepRadius => _latestBlockerSweepRadius;
    public string LatestBlockerClampCollider => _latestBlockerClampCollider;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CacheReferences();
        if (!ActiveInstances.Contains(this))
        {
            ActiveInstances.Add(this);
        }

        _movementLocked.OnValueChanged += HandleMovementLockedChanged;
        _networkPlayerIndex.OnValueChanged += HandlePlayerIndexChanged;

        if (_networkPlayerIndex.Value != 0)
        {
            ApplyPlayerIndex(_networkPlayerIndex.Value);
        }

        _shipFlight.SetExternalSimulationEnabled(true);
        _ownerFrictionEnabled = _shipFlight.IsFrictionEnabled;
        _serverFrictionEnabled = _shipFlight.IsFrictionEnabled;
        _ownerState = CaptureCurrentState();
        _serverState = _ownerState;
        _ownerStateInitialized = true;
        _serverStateInitialized = true;
        _lastAcceptedDodgeTick = -1;

        if (IsOwner)
        {
            _inputBuffer = new NetInputSnapshot3D[ClientInputBufferSize];
            _predictionBuffer = new NetStateSnapshot3D[ClientInputBufferSize];
        }

        ConfigurePresentationForCurrentOwnership();

        if (IsServer)
        {
            _stateHistory = new NetStateSnapshot3D[ServerStateBufferSize];
        }

        if (!IsOwner && !IsServer)
        {
            _remoteInterpolator.Initialize(ServerStateBufferSize, interpolationSettings);
            _rb.isKinematic = true;
        }
        else
        {
            _rb.isKinematic = false;
        }

        ApplyMovementLock(_movementLocked.Value);
        LogMovementConfigIfNeeded("OnNetworkSpawn");
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        EnsureOwnerLocalControlReady();
    }

    public override void OnLostOwnership()
    {
        base.OnLostOwnership();
        ConfigureProxyPresentation();
    }

    public override void OnNetworkDespawn()
    {
        ActiveInstances.Remove(this);

        _movementLocked.OnValueChanged -= HandleMovementLockedChanged;
        _networkPlayerIndex.OnValueChanged -= HandlePlayerIndexChanged;

        if (_shipFlight != null)
        {
            _shipFlight.SetExternalSimulationEnabled(false);
        }

        if (_playerInput3D != null)
        {
            _playerInput3D.SetCombatInputSuppressed(false);
            _playerInput3D.enabled = true;
        }

        if (_playerInput != null)
        {
            _playerInput.enabled = true;
            _playerInput.ActivateInput();
        }

        if (_cameraRig != null)
        {
            _cameraRig.SetCameraRigActive(true);
        }

        if (_rb != null)
        {
            _rb.isKinematic = false;
        }

        _remoteInterpolator.Reset();
        base.OnNetworkDespawn();
    }

    private void FixedUpdate()
    {
        if (!NetTickUtil.IsActive)
        {
            return;
        }

        if (IsOwner)
        {
            EnsureOwnerLocalControlReady();
            OwnerTick();
        }

        if (!IsOwner && !IsServer)
        {
            InterpolateRemote();
        }
    }

    public void SetNetworkPlayerIndex(byte index)
    {
        if (!IsServer)
        {
            return;
        }

        _networkPlayerIndex.Value = index;
        ApplyPlayerIndex(index);
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

    public static bool TryGetPlayerBySlot(byte slot, out NetMovement3D movement)
    {
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            NetMovement3D candidate = ActiveInstances[i];
            if (candidate == null || !candidate.IsSpawned || candidate.PlayerSlot != slot)
            {
                continue;
            }

            movement = candidate;
            return true;
        }

        movement = null;
        return false;
    }

    public static bool TryGetPlayerByTag(string playerTag, out NetMovement3D movement)
    {
        for (int i = 0; i < ActiveInstances.Count; i++)
        {
            NetMovement3D candidate = ActiveInstances[i];
            if (candidate == null || !candidate.IsSpawned || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (candidate.CompareTag(playerTag))
            {
                movement = candidate;
                return true;
            }
        }

        movement = null;
        return false;
    }

    public bool TryGetHistoricalState(int requestedTick, out NetStateSnapshot3D snapshot)
    {
        snapshot = default;
        if (_stateHistory == null || _stateHistory.Length == 0)
        {
            return false;
        }

        int newestAllowedTick = _stateHistoryHead;
        int oldestAllowedTick = Mathf.Max(0, newestAllowedTick - Mathf.Max(0, maxCombatRewindTicks));
        int clampedTick = Mathf.Clamp(requestedTick, oldestAllowedTick, newestAllowedTick);
        int index = clampedTick % _stateHistory.Length;
        NetStateSnapshot3D candidate = _stateHistory[index];
        if (candidate.Tick != clampedTick)
        {
            return false;
        }

        snapshot = candidate;
        return true;
    }

    public float GetCollisionRadius()
    {
        Collider collider3D = GetComponent<Collider>();
        if (collider3D != null)
        {
            Bounds bounds = collider3D.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        Collider[] childColliders = GetComponentsInChildren<Collider>();
        float radius = 0.5f;
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider child = childColliders[i];
            if (child == null)
            {
                continue;
            }

            Bounds bounds = child.bounds;
            radius = Mathf.Max(radius, bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        return radius;
    }

    public void ApplyCombatVelocityDelta(Vector3 velocityDelta)
    {
        if (velocityDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        _lastCombatVelocityDelta = velocityDelta;
        _lastCombatVelocityDeltaTick = NetTickUtil.CurrentTick;

        if (_rb != null && !_rb.isKinematic)
        {
            _rb.linearVelocity += velocityDelta;
        }

        if (_ownerStateInitialized)
        {
            _ownerState.Velocity += velocityDelta;
        }

        if (_serverStateInitialized)
        {
            _serverState.Velocity += velocityDelta;
        }
    }

    public bool QueuePredictedDodge(Vector3 worldDirection, NetDodgeKind3D dodgeKind = NetDodgeKind3D.Generic)
    {
        if (!IsOwner || !IsSpawned || _movementLocked.Value || worldDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _pendingDodgeRequested = true;
        _pendingDodgeKind = dodgeKind;
        _pendingDodgeDirection = worldDirection.normalized;
        _lastQueuedDodgeKind = dodgeKind;
        _lastQueuedDodgeDirection = _pendingDodgeDirection;
        _lastQueuedDodgeTick = NetTickUtil.CurrentTick;
        return true;
    }

    public void ApplyCombatWarp(Vector3 position)
    {
        _lastCombatWarpPosition = position;
        _lastCombatWarpTick = NetTickUtil.CurrentTick;

        if (_rb != null)
        {
            _rb.position = position;
        }

        transform.position = position;

        if (_ownerStateInitialized)
        {
            _ownerState.Position = position;
        }

        if (_serverStateInitialized)
        {
            _serverState.Position = position;
        }

        ResetRemoteInterpolationState();
    }

    public void ApplyBoundaryCorrection(Vector3 correctedPosition, Vector3 correctedVelocity)
    {
        _lastBoundaryCorrectionPosition = correctedPosition;
        _lastBoundaryCorrectionVelocity = correctedVelocity;
        _lastBoundaryCorrectionTick = NetTickUtil.CurrentTick;

        if (_rb != null)
        {
            _rb.position = correctedPosition;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = correctedVelocity;
            }
        }

        transform.position = correctedPosition;

        if (_ownerStateInitialized)
        {
            _ownerState.Position = correctedPosition;
            _ownerState.Velocity = correctedVelocity;
            _ownerState.DodgeVelocity = Vector3.zero;
            _ownerState.DodgeExitVelocity = Vector3.zero;
            _ownerState.DodgeRemainingTime = 0f;
            _ownerState.DodgeDuration = 0f;
            _ownerState.DodgeKind = NetDodgeKind3D.Generic;
        }

        if (_serverStateInitialized)
        {
            _serverState.Position = correctedPosition;
            _serverState.Velocity = correctedVelocity;
            _serverState.DodgeVelocity = Vector3.zero;
            _serverState.DodgeExitVelocity = Vector3.zero;
            _serverState.DodgeRemainingTime = 0f;
            _serverState.DodgeDuration = 0f;
            _serverState.DodgeKind = NetDodgeKind3D.Generic;
        }

        ResetRemoteInterpolationState();
    }

    public void ResetMovementState(Vector3 position, Quaternion rotation, bool resetCamera)
    {
        CacheReferences();
        _lastMovementResetTick = NetTickUtil.CurrentTick;
        _lastMovementResetPosition = position;
        _lastMovementResetCamera = resetCamera;

        if (_shipFlight != null)
        {
            _ownerFrictionEnabled = _shipFlight.IsFrictionEnabled;
            _serverFrictionEnabled = _shipFlight.IsFrictionEnabled;
        }

        _pendingDodgeRequested = false;
        _pendingDodgeKind = NetDodgeKind3D.Generic;
        _pendingDodgeDirection = Vector3.zero;
        _lastAcceptedDodgeTick = -1;
        _lastAppliedVisualVelocity = Vector3.zero;
        _hasLastAppliedVisualVelocity = false;
        _remoteDodgePresentationActive = false;

        MovementState3D resetState = new MovementState3D
        {
            Position = position,
            Rotation = rotation,
            Velocity = Vector3.zero,
            FilteredLookInput = Vector2.zero,
            TurnRates = Vector2.zero,
            DodgeVelocity = Vector3.zero,
            DodgeExitVelocity = Vector3.zero,
            DodgeRemainingTime = 0f,
            DodgeDuration = 0f,
            DodgeKind = NetDodgeKind3D.Generic
        };

        _ownerState = resetState;
        _serverState = resetState;
        _ownerStateInitialized = true;
        _serverStateInitialized = true;

        if (_rb != null)
        {
            _rb.position = position;
            _rb.rotation = rotation;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
            }

            _rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(position, rotation);
        _shipFlight?.ResetMotionState(clearVelocity: true);
        _shipFlight?.ApplyExternalSimulationState(
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            0f,
            _ownerFrictionEnabled,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero);

        if (resetCamera)
        {
            _cameraRig?.ResetCameraState(snapToNeutralOffset: true);
        }

        ResetRemoteInterpolationState();
    }

    private void OwnerTick()
    {
        if (_shipFlight == null)
        {
            if (!_loggedOwnerShipFlightMissing)
            {
                Debug.LogError("[NetMovement3D] Owner movement tick skipped because ShipFlight3D is missing.", this);
                _loggedOwnerShipFlightMissing = true;
            }

            return;
        }

        int tick = NetTickUtil.CurrentTick;
        if (tick <= _lastSentTick)
        {
            return;
        }

        _lastSentTick = tick;
        float dt = GetTickDeltaTime();

        if (_playerInput3D == null && !_loggedOwnerInputMissing)
        {
            Debug.LogError("[NetMovement3D] Owner movement tick has no PlayerInput3D; thrust/look input will stay at zero.", this);
            _loggedOwnerInputMissing = true;
        }

        Vector2 lookInput = _movementLocked.Value || _playerInput3D == null ? Vector2.zero : _playerInput3D.LookInput;
        float thrustInput = _movementLocked.Value || _playerInput3D == null ? 0f : _playerInput3D.ThrustInput;

        NetInputSnapshot3D input = new NetInputSnapshot3D
        {
            Tick = tick,
            LookInput = lookInput,
            ThrustInput = thrustInput,
            FrictionEnabled = _ownerFrictionEnabled,
            BaseRotationMultiplier = _player != null ? _player.GetBaseRotationMultiplier() : 1f,
            AbilityRotationMultiplier = _player != null ? _player.GetAbilityRotationMultiplier() : 1f,
            ThrustMultiplier = _player != null ? _player.GetCombinedThrustMultiplier() : 1f,
            MaxSpeedMultiplier = _player != null ? _player.GetCombinedMaxSpeedMultiplier() : 1f,
            SlowMultiplier = _player != null ? _player.GetSlowMultiplier() : 1f,
            DodgeRequested = !_movementLocked.Value && _pendingDodgeRequested,
            DodgeKind = _pendingDodgeKind,
            DodgeDirection = _pendingDodgeDirection
        };
        _pendingDodgeRequested = false;
        _pendingDodgeKind = NetDodgeKind3D.Generic;
        _pendingDodgeDirection = Vector3.zero;

        int inputIndex = tick % ClientInputBufferSize;
        _inputBuffer[inputIndex] = input;

        Vector3 previousVelocity = GetEffectiveVelocity(_ownerState);
        SimulateInputTick(
            ref _ownerState,
            in input,
            dt,
            validateDodge: false);

        ApplySimulationState(_ownerState);
        ApplyFlightTelemetry(input.LookInput, input.ThrustInput, input.FrictionEnabled, _ownerState, previousVelocity, dt);

        _predictionBuffer[inputIndex] = ToSnapshot(tick, _ownerState, input.ThrustInput, input.FrictionEnabled);

        if (IsServer)
        {
            _serverState = _ownerState;
            _serverStateInitialized = true;
            _serverFrictionEnabled = input.FrictionEnabled;
            PublishAuthoritativeState(tick, _serverState, input.ThrustInput, input.FrictionEnabled);
        }
        else
        {
            SubmitInputServerRpc(input);
        }
    }

    [ServerRpc]
    private void SubmitInputServerRpc(NetInputSnapshot3D input)
    {
        if (input.Tick <= _lastProcessedServerTick)
        {
            return;
        }

        _lastProcessedServerTick = input.Tick;
        float dt = GetTickDeltaTime();

        if (_movementLocked.Value)
        {
            input.LookInput = Vector2.zero;
            input.ThrustInput = 0f;
            input.DodgeRequested = false;
            input.DodgeKind = NetDodgeKind3D.Generic;
            input.DodgeDirection = Vector3.zero;
        }

        if (_player != null)
        {
            input.SlowMultiplier = _player.GetSlowMultiplier();
        }

        _serverFrictionEnabled = input.FrictionEnabled;

        Vector3 previousVelocity = GetEffectiveVelocity(_serverState);
        SimulateInputTick(
            ref _serverState,
            in input,
            dt,
            validateDodge: true);

        ApplySimulationState(_serverState);
        ApplyFlightTelemetry(input.LookInput, input.ThrustInput, input.FrictionEnabled, _serverState, previousVelocity, dt);
        PublishAuthoritativeState(input.Tick, _serverState, input.ThrustInput, input.FrictionEnabled);
    }

    private void PublishAuthoritativeState(int tick, in MovementState3D state, float thrustInput, bool frictionEnabled)
    {
        NetStateSnapshot3D snapshot = ToSnapshot(tick, state, thrustInput, frictionEnabled);

        if (_stateHistory != null && _stateHistory.Length > 0)
        {
            int historyIndex = tick % _stateHistory.Length;
            _stateHistory[historyIndex] = snapshot;
            _stateHistoryHead = tick;
        }

        BroadcastStateClientRpc(snapshot);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void BroadcastStateClientRpc(NetStateSnapshot3D snapshot)
    {
        if (IsOwner)
        {
            Reconcile(snapshot);
            return;
        }

        if (!IsServer)
        {
            BufferInterpolationState(snapshot);
        }
    }

    private void Reconcile(NetStateSnapshot3D serverState)
    {
        if (serverState.Tick <= _lastReceivedServerTick)
        {
            return;
        }

        _lastReceivedServerTick = serverState.Tick;

        int predictionIndex = serverState.Tick % ClientInputBufferSize;
        NetStateSnapshot3D predicted = _predictionBuffer[predictionIndex];
        if (predicted.Tick != serverState.Tick)
        {
            ApplyAuthoritativeCorrection(
                serverState,
                OwnerCorrectionCause.PredictionMissing,
                predicted,
                GetBufferedInputForTick(serverState.Tick),
                predictionMatchesServerTick: false);
            return;
        }

        float positionError = Vector3.Distance(predicted.Position, serverState.Position);
        if (positionError <= reconciliationThreshold)
        {
            return;
        }

        MovementState3D replayState = ToMovementState(serverState);
        float dt = GetTickDeltaTime();
        int currentTick = NetTickUtil.CurrentTick;
        RigidbodyDiagnosticState rbBeforeCorrection = CaptureRigidbodyDiagnosticState();

        for (int tick = serverState.Tick + 1; tick <= currentTick; tick++)
        {
            int inputIndex = tick % ClientInputBufferSize;
            NetInputSnapshot3D input = _inputBuffer[inputIndex];
            if (input.Tick != tick)
            {
                continue;
            }

            SimulateInputTick(
                ref replayState,
                in input,
                dt,
                validateDodge: false);

            _predictionBuffer[inputIndex] = ToSnapshot(tick, replayState, input.ThrustInput, input.FrictionEnabled);
        }

        _ownerState = replayState;
        ApplySimulationState(_ownerState, snap: true);
        RigidbodyDiagnosticState rbAfterCorrection = CaptureRigidbodyDiagnosticState();

        RegisterOwnerCorrection(
            OwnerCorrectionCause.PositionError,
            positionError,
            serverState,
            predicted,
            GetBufferedInputForTick(serverState.Tick),
            predictionMatchesServerTick: true,
            ticksReplayed: Mathf.Max(0, currentTick - serverState.Tick),
            rbBeforeCorrection: rbBeforeCorrection,
            rbAfterCorrection: rbAfterCorrection);

        int latestInputIndex = currentTick % ClientInputBufferSize;
        Vector2 latestLookInput = _inputBuffer[latestInputIndex].Tick == currentTick
            ? _inputBuffer[latestInputIndex].LookInput
            : Vector2.zero;
        Vector3 previousVelocity = predicted.Tick == currentTick
            ? GetEffectiveVelocity(ToMovementState(predicted))
            : GetEffectiveVelocity(ToMovementState(serverState));
        ApplyFlightTelemetry(latestLookInput, _predictionBuffer[latestInputIndex].ThrustInput, _predictionBuffer[latestInputIndex].FrictionEnabled, _ownerState, previousVelocity, dt);
    }

    private void ApplyAuthoritativeCorrection(
        NetStateSnapshot3D snapshot,
        OwnerCorrectionCause cause = OwnerCorrectionCause.AuthoritativeSnap,
        NetStateSnapshot3D predicted = default,
        NetInputSnapshot3D input = default,
        bool predictionMatchesServerTick = false)
    {
        RigidbodyDiagnosticState rbBeforeCorrection = CaptureRigidbodyDiagnosticState();
        float correctionDistance = _ownerStateInitialized
            ? Vector3.Distance(_ownerState.Position, snapshot.Position)
            : 0f;

        _ownerState = ToMovementState(snapshot);
        ApplySimulationState(_ownerState, snap: true);
        RigidbodyDiagnosticState rbAfterCorrection = CaptureRigidbodyDiagnosticState();
        if (_ownerStateInitialized)
        {
            RegisterOwnerCorrection(
                cause,
                correctionDistance,
                snapshot,
                predicted,
                input,
                predictionMatchesServerTick,
                ticksReplayed: 0,
                rbBeforeCorrection: rbBeforeCorrection,
                rbAfterCorrection: rbAfterCorrection);
        }

        ApplyFlightTelemetry(snapshot.FilteredLookInput, snapshot.ThrustInput, snapshot.FrictionEnabled, _ownerState, GetEffectiveVelocity(_ownerState), GetTickDeltaTime());
    }

    private void BufferInterpolationState(NetStateSnapshot3D state)
    {
        _remoteInterpolator.AddSnapshot(state, this, "NetMovement3D");
    }

    private void InterpolateRemote()
    {
        float tickInterval = GetTickDeltaTime();
        if (!_remoteInterpolator.TrySample(
            interpolationSettings,
            tickInterval,
            NetTickUtil.ServerTick,
            Time.fixedDeltaTime,
            this,
            "NetMovement3D",
            out MovementState3D interpolatedState,
            out float interpolatedThrust,
            out bool interpolatedFriction))
        {
            return;
        }

        ApplySimulationState(interpolatedState);
        UpdateRemoteDodgePresentation(interpolatedState);

        Vector3 previousVelocity = _hasLastAppliedVisualVelocity ? _lastAppliedVisualVelocity : GetEffectiveVelocity(interpolatedState);
        ApplyFlightTelemetry(interpolatedState.FilteredLookInput, interpolatedThrust, interpolatedFriction, interpolatedState, previousVelocity, tickInterval);
        _lastAppliedVisualVelocity = GetEffectiveVelocity(interpolatedState);
        _hasLastAppliedVisualVelocity = true;
    }

    private void SimulateInputTick(
        ref MovementState3D state,
        in NetInputSnapshot3D input,
        float dt,
        bool validateDodge)
    {
        Vector3 startPosition = state.Position;
        ApplyDodgeFromInput(ref state, in input, validateDodge);

        MovementSimulation3D.SimulateTick(
            ref state,
            in input,
            _shipFlight.FlightConfig,
            _shipFlight.FlightAssistConfig,
            _shipFlight.LockToWorldYPlane,
            _shipFlight.LockedWorldY,
            dt);

        ConstrainPredictedMovementAgainstBlockers(ref state, startPosition);
        bool appliedUprightRecovery = ApplyUprightRecovery(ref state, in input, dt);
        if (appliedUprightRecovery && input.FrictionEnabled)
        {
            ApplyUprightRecoveryVelocityCleanup(ref state, _shipFlight.FlightAssistConfig, dt);
        }
    }

    private void ConstrainPredictedMovementAgainstBlockers(ref MovementState3D state, Vector3 startPosition)
    {
        int blockerMask = networkMovementBlockerMask.value;
        if (blockerMask == 0)
        {
            return;
        }

        Vector3 displacement = state.Position - startPosition;
        float distance = displacement.magnitude;
        if (distance <= Mathf.Max(0f, minimumBlockerSweepDistance))
        {
            return;
        }

        float radius = Mathf.Max(0.01f, GetCollisionRadius());
        Vector3 direction = displacement / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            startPosition,
            radius,
            direction,
            BlockerSweepHits,
            distance + Mathf.Max(0f, blockerSkinWidth),
            blockerMask,
            QueryTriggerInteraction.Ignore);

        int nearestIndex = -1;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = BlockerSweepHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestIndex = i;
            }
        }

        if (nearestIndex < 0)
        {
            return;
        }

        RaycastHit nearestHit = BlockerSweepHits[nearestIndex];
        float allowedDistance = Mathf.Max(0f, nearestHit.distance - Mathf.Max(0f, blockerSkinWidth));
        state.Position = startPosition + direction * Mathf.Min(distance, allowedDistance);
        RegisterBlockerClamp(distance, allowedDistance, radius, nearestHit.collider);

        if (Vector3.Dot(state.Velocity, nearestHit.normal) < 0f)
        {
            state.Velocity = Vector3.ProjectOnPlane(state.Velocity, nearestHit.normal);
        }

        if (state.DodgeRemainingTime > 0f && Vector3.Dot(state.DodgeVelocity, nearestHit.normal) < 0f)
        {
            state.DodgeVelocity = Vector3.ProjectOnPlane(state.DodgeVelocity, nearestHit.normal);
        }
    }

    private bool ApplyUprightRecovery(ref MovementState3D state, in NetInputSnapshot3D input, float dt)
    {
        if (_player == null)
        {
            return false;
        }

        float recoveryDeadZone = _player.UprightRecoveryInputDeadZone;
        bool hasRotationIntent = input.LookInput.sqrMagnitude > recoveryDeadZone * recoveryDeadZone
            || Mathf.Abs(state.TurnRates.x) > 0.01f
            || Mathf.Abs(state.TurnRates.y) > 0.01f;
        bool shouldApplyRecovery = _player.ShouldApplyUprightRecovery(hasRotationIntent);
        if (!shouldApplyRecovery)
        {
            return false;
        }

        state.Rotation = _player.ApplyUprightRecovery(state.Rotation, dt, hasRotationIntent);
        return true;
    }

    private static void ApplyUprightRecoveryVelocityCleanup(ref MovementState3D state, in ShipFlightAssistConfig3D assist, float dt)
    {
        Quaternion inverseRotation = Quaternion.Inverse(state.Rotation);
        Vector3 localVelocity = inverseRotation * state.Velocity;
        float recoveryMultiplier = Mathf.Max(1f, assist.recoveryDriftDampingMultiplier);
        localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, assist.lateralDriftDamping * recoveryMultiplier * dt);
        localVelocity.y = Mathf.MoveTowards(localVelocity.y, 0f, assist.verticalDriftDamping * recoveryMultiplier * dt);

        Vector3 worldVelocity = state.Rotation * localVelocity;
        if (assist.recoveryVelocityAlignmentStrength > 0f && worldVelocity.sqrMagnitude > 0.0001f)
        {
            float lerpFactor = 1f - Mathf.Exp(-assist.recoveryVelocityAlignmentStrength * dt);
            Vector3 alignedDirection = Vector3.Slerp(worldVelocity.normalized, state.Rotation * Vector3.forward, lerpFactor).normalized;
            worldVelocity = alignedDirection * worldVelocity.magnitude;
        }

        state.Velocity = worldVelocity;
    }

    private void ApplyDodgeFromInput(ref MovementState3D state, in NetInputSnapshot3D input, bool validateDodge)
    {
        if (!input.DodgeRequested || input.DodgeDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        CacheReferences();
        bool useClassDodgeAbility = input.DodgeKind == NetDodgeKind3D.Class4Ability;
        if ((useClassDodgeAbility && _dodgeAbility == null) || (!useClassDodgeAbility && _player == null))
        {
            return;
        }

        if (validateDodge && !CanAcceptNetworkDodge(in input))
        {
            return;
        }

        Vector3 dashVelocity;
        float duration;
        bool resolvedDodge = useClassDodgeAbility
            ? _dodgeAbility.TryResolveNetworkDodge(
                input.DodgeDirection,
                state.Position,
                GetCollisionRadius(),
                out dashVelocity,
                out duration)
            : _player.TryResolveNetworkDodge(
                input.DodgeDirection,
                state.Position,
                GetCollisionRadius(),
                out dashVelocity,
                out duration);

        if (!resolvedDodge)
        {
            return;
        }

        state.DodgeVelocity = dashVelocity;
        state.DodgeExitVelocity = state.Velocity;
        state.DodgeRemainingTime = duration;
        state.DodgeDuration = duration;
        state.DodgeKind = input.DodgeKind;

        if (validateDodge)
        {
            _lastAcceptedDodgeTick = input.Tick;
            if (useClassDodgeAbility)
            {
                _dodgeAbility.MarkNetworkDodgeAccepted();
            }
            else
            {
                _player.MarkNetworkDodgeAccepted();
            }
        }
    }

    private bool CanAcceptNetworkDodge(in NetInputSnapshot3D input)
    {
        bool useClassDodgeAbility = input.DodgeKind == NetDodgeKind3D.Class4Ability;
        bool canAcceptState = useClassDodgeAbility
            ? _dodgeAbility.CanAcceptNetworkDodgeState()
            : _player != null && _player.CanAcceptNetworkDodgeState();
        if (!canAcceptState)
        {
            return false;
        }

        float cooldownDuration = Mathf.Max(0f, useClassDodgeAbility
            ? _dodgeAbility.GetNetworkDodgeCooldownDuration()
            : _player.GetNetworkDodgeCooldownDuration());
        int cooldownTicks = Mathf.CeilToInt(cooldownDuration / Mathf.Max(0.0001f, GetTickDeltaTime()));
        return _lastAcceptedDodgeTick < 0 || input.Tick >= _lastAcceptedDodgeTick + cooldownTicks;
    }

    private void UpdateRemoteDodgePresentation(in MovementState3D state)
    {
        bool isDodgeActive = state.DodgeRemainingTime > 0.001f && state.DodgeVelocity.sqrMagnitude > 0.0001f;
        if (isDodgeActive && !_remoteDodgePresentationActive)
        {
            CacheReferences();
            if (state.DodgeKind == NetDodgeKind3D.Class4Ability)
            {
                _dodgeAbility?.PlayNetworkDodgePresentation(state.DodgeVelocity);
            }
            else
            {
                _player?.PlayNetworkDodgePresentation(state.DodgeVelocity);
            }
        }

        _remoteDodgePresentationActive = isDodgeActive;
    }

    private static Vector3 GetEffectiveVelocity(in MovementState3D state)
    {
        return state.DodgeRemainingTime > 0.001f
            ? state.Velocity + MovementSimulation3D.GetCurrentDodgeVelocity(state)
            : state.Velocity;
    }

    private void ApplySimulationState(in MovementState3D state, bool snap = false)
    {
        if (snap)
        {
            _rb.position = state.Position;
            _rb.rotation = state.Rotation;
            transform.SetPositionAndRotation(state.Position, state.Rotation);
        }
        else
        {
            _rb.MovePosition(state.Position);
            _rb.MoveRotation(state.Rotation);
        }

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = GetEffectiveVelocity(state);
        }
    }

    private void ApplyFlightTelemetry(
        Vector2 rawLookInput,
        float thrustInput,
        bool frictionEnabled,
        in MovementState3D state,
        Vector3 previousVelocity,
        float dt)
    {
        if (_shipFlight == null)
        {
            return;
        }

        Vector3 effectiveVelocity = GetEffectiveVelocity(state);
        Vector3 linearAcceleration = dt > 0f ? (effectiveVelocity - previousVelocity) / dt : Vector3.zero;
        _shipFlight.ApplyExternalSimulationState(
            rawLookInput,
            state.FilteredLookInput,
            state.TurnRates,
            thrustInput,
            frictionEnabled,
            effectiveVelocity,
            linearAcceleration,
            Vector3.zero);
    }

    private MovementState3D CaptureCurrentState()
    {
        return new MovementState3D
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Velocity = _rb != null ? _rb.linearVelocity : Vector3.zero,
            FilteredLookInput = Vector2.zero,
            TurnRates = Vector2.zero,
            DodgeVelocity = Vector3.zero,
            DodgeExitVelocity = Vector3.zero,
            DodgeRemainingTime = 0f,
            DodgeDuration = 0f,
            DodgeKind = NetDodgeKind3D.Generic
        };
    }

    private static MovementState3D ToMovementState(NetStateSnapshot3D snapshot)
    {
        return new MovementState3D
        {
            Position = snapshot.Position,
            Rotation = snapshot.Rotation,
            Velocity = snapshot.Velocity,
            FilteredLookInput = snapshot.FilteredLookInput,
            TurnRates = snapshot.TurnRates,
            DodgeVelocity = snapshot.DodgeVelocity,
            DodgeExitVelocity = snapshot.DodgeExitVelocity,
            DodgeRemainingTime = snapshot.DodgeRemainingTime,
            DodgeDuration = snapshot.DodgeDuration,
            DodgeKind = snapshot.DodgeKind
        };
    }

    private static NetStateSnapshot3D ToSnapshot(int tick, in MovementState3D state, float thrustInput, bool frictionEnabled)
    {
        return new NetStateSnapshot3D
        {
            Tick = tick,
            Position = state.Position,
            Rotation = state.Rotation,
            Velocity = state.Velocity,
            FilteredLookInput = state.FilteredLookInput,
            TurnRates = state.TurnRates,
            ThrustInput = thrustInput,
            FrictionEnabled = frictionEnabled,
            DodgeVelocity = state.DodgeVelocity,
            DodgeExitVelocity = state.DodgeExitVelocity,
            DodgeRemainingTime = state.DodgeRemainingTime,
            DodgeDuration = state.DodgeDuration,
            DodgeKind = state.DodgeKind
        };
    }

    private void ConfigureOwnerPresentation()
    {
        Camera gameplayCamera = SelectGameplayCamera();
        CinemachineCamera cinemachineCamera = SelectGameplayCinemachineCamera();

        if (_playerInput3D != null)
        {
            _playerInput3D.enabled = true;
            _playerInput3D.SetCombatInputSuppressed(_movementLocked.Value || !HasNetworkCombatBridgeForOwner());
        }

        if (_playerInput != null)
        {
            _playerInput.enabled = true;
            _playerInput.ActivateInput();
            _playerInput.camera = gameplayCamera;
        }

        if (_cameraRig != null)
        {
            _cameraRig.SetCamera(cinemachineCamera);
            _cameraRig.SetCameraRigActive(true);
        }

        SetOwnedAudioListenersActive(true);
        BindOwnerCameraAndTracking(gameplayCamera, cinemachineCamera);
        SetWeaponAimCamera(gameplayCamera);
        PlayerHUDManager3D.RebindAllAutoManagers();
        _loggedOwnerInputMissing = false;
    }

    private void ConfigureProxyPresentation()
    {
        if (_playerInput3D != null)
        {
            _playerInput3D.SetCombatInputSuppressed(true);
            _playerInput3D.enabled = false;
        }

        if (_playerInput != null)
        {
            _playerInput.DeactivateInput();
            _playerInput.enabled = false;
        }

        if (_cameraRig != null)
        {
            _cameraRig.SetCameraRigActive(false);
        }

        SetOwnedAudioListenersActive(false);
    }

    private void SetOwnedAudioListenersActive(bool isActive)
    {
        AudioListener[] ownedListeners = GetComponentsInChildren<AudioListener>(true);
        if (ownedListeners.Length == 0)
        {
            return;
        }

        if (!isActive)
        {
            for (int i = 0; i < ownedListeners.Length; i++)
            {
                if (ownedListeners[i] != null)
                {
                    ownedListeners[i].enabled = false;
                }
            }

            return;
        }

        AudioListener primaryOwnedListener = null;
        for (int i = 0; i < ownedListeners.Length; i++)
        {
            if (ownedListeners[i] != null)
            {
                primaryOwnedListener = ownedListeners[i];
                break;
            }
        }

        if (primaryOwnedListener == null)
        {
            return;
        }

        AudioListener[] sceneListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneListeners.Length; i++)
        {
            if (sceneListeners[i] != null)
            {
                sceneListeners[i].enabled = sceneListeners[i] == primaryOwnedListener;
            }
        }
    }

    private void SetWeaponAimCamera(Camera gameplayCamera)
    {
        if (_player == null || _player.Weapons == null)
        {
            return;
        }

        for (int i = 0; i < _player.Weapons.Length; i++)
        {
            Weapon3D weapon = _player.Weapons[i];
            if (weapon != null)
            {
                weapon.SetAimCamera(gameplayCamera);
            }
        }
    }

    private Camera SelectGameplayCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                return candidate;
            }
        }

        return null;
    }

    private CinemachineCamera SelectGameplayCinemachineCamera()
    {
        CinemachineCamera[] cinemachineCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        CinemachineCamera fallback = null;

        for (int i = 0; i < cinemachineCameras.Length; i++)
        {
            CinemachineCamera candidate = cinemachineCameras[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (candidate.isActiveAndEnabled)
            {
                return candidate;
            }

            fallback ??= candidate;
        }

        return fallback;
    }

    private void CacheReferences()
    {
        _player ??= GetComponent<Player3D>();
        _playerInput3D ??= GetComponent<PlayerInput3D>();
        _playerInput ??= GetComponent<PlayerInput>();
        _cameraRig ??= GetComponent<PlayerCameraRig3D>();
        _shipFlight ??= GetComponent<ShipFlight3D>();
        _dodgeAbility ??= GetComponent<Dodge3D>();
        _rb ??= GetComponent<Rigidbody>();
    }

    private void ConfigurePresentationForCurrentOwnership()
    {
        if (IsOwner)
        {
            ConfigureOwnerPresentation();
        }
        else
        {
            ConfigureProxyPresentation();
        }
    }

    public void EnsureOwnerLocalControlReady()
    {
        if (!IsOwner)
        {
            return;
        }

        CacheReferences();

        bool needsRecovery = false;
        if (_playerInput3D != null && !_playerInput3D.enabled)
        {
            needsRecovery = true;
        }

        if (_playerInput != null && !_playerInput.enabled)
        {
            needsRecovery = true;
        }

        if (_cameraRig != null && !_cameraRig.enabled)
        {
            needsRecovery = true;
        }

        bool expectedCombatSuppressed = _movementLocked.Value || !HasNetworkCombatBridgeForOwner();
        if (_playerInput3D != null && _playerInput3D.IsCombatInputSuppressed != expectedCombatSuppressed)
        {
            needsRecovery = true;
        }

        if (!needsRecovery)
        {
            return;
        }

        ConfigureOwnerPresentation();
    }

    public void RefreshOwnerPresentationAfterRespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        CacheReferences();
        ConfigureOwnerPresentation();
    }

    public void BindOwnerCameraAndTracking()
    {
        if (!IsOwner)
        {
            return;
        }

        BindOwnerCameraAndTracking(SelectGameplayCamera(), SelectGameplayCinemachineCamera());
    }

    private float GetTickDeltaTime()
    {
        float tickInterval = NetTickUtil.TickInterval;
        return tickInterval > 0f ? tickInterval : Time.fixedDeltaTime;
    }

    private void BindOwnerCameraAndTracking(Camera gameplayCamera, CinemachineCamera cinemachineCamera)
    {
        if (!IsOwner)
        {
            return;
        }

        if (_cameraRig != null)
        {
            _cameraRig.BindTrackingTarget(transform);
            _cameraRig.ResetCameraState(snapToNeutralOffset: true);
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Target.TrackingTarget = transform;
        }

        if (_playerInput != null && gameplayCamera != null)
        {
            _playerInput.camera = gameplayCamera;
        }
    }

    private bool HasNetworkCombatBridgeForOwner()
    {
        NetCombat3D netCombat = GetComponent<NetCombat3D>();
        bool hasBridge = netCombat != null;
        if (!hasBridge && !_loggedMissingCombatBridge)
        {
            Debug.LogError("[NetMovement3D] Owner combat input remains suppressed because this network player prefab is missing NetCombat3D.", this);
            _loggedMissingCombatBridge = true;
        }

        return hasBridge;
    }

    private NetInputSnapshot3D GetBufferedInputForTick(int tick)
    {
        if (_inputBuffer == null || _inputBuffer.Length == 0)
        {
            return default;
        }

        int inputIndex = tick % _inputBuffer.Length;
        return _inputBuffer[inputIndex];
    }

    private void RegisterOwnerCorrection(
        OwnerCorrectionCause cause,
        float distance,
        in NetStateSnapshot3D serverState,
        in NetStateSnapshot3D predicted,
        in NetInputSnapshot3D input,
        bool predictionMatchesServerTick,
        int ticksReplayed,
        in RigidbodyDiagnosticState rbBeforeCorrection,
        in RigidbodyDiagnosticState rbAfterCorrection)
    {
        _ownerCorrectionCount++;
        _ownerCorrectionDistanceTotal += Mathf.Max(0f, distance);
        _latestOwnerCorrectionDistance = Mathf.Max(0f, distance);
        _largestRecentOwnerCorrectionDistance = Mathf.Max(_largestRecentOwnerCorrectionDistance, _latestOwnerCorrectionDistance);
        _correctionsInCurrentSummaryWindow++;

        int currentTick = NetTickUtil.CurrentTick;
        bool recentSideEffect = HasRecentMovementSideEffect(serverState.Tick, currentTick);
        _latestOwnerCorrectionLikelyCause = GetLikelyOwnerCorrectionCause(cause, recentSideEffect);
        _latestOwnerCorrectionSideEffect = GetMostRecentMovementSideEffectSummary(currentTick);
        UpdateOwnerCorrectionSummary(currentTick);

        if (enableOwnerMovementDiagnostics && logOwnerCorrections && distance >= Mathf.Max(0f, correctionLogMinDistance))
        {
            LogOwnerCorrection(
                cause,
                distance,
                in serverState,
                in predicted,
                in input,
                predictionMatchesServerTick,
                ticksReplayed,
                in rbBeforeCorrection,
                in rbAfterCorrection);
        }
    }

    private void LogOwnerCorrection(
        OwnerCorrectionCause cause,
        float correctionDistance,
        in NetStateSnapshot3D serverState,
        in NetStateSnapshot3D predicted,
        in NetInputSnapshot3D input,
        bool predictionMatchesServerTick,
        int ticksReplayed,
        in RigidbodyDiagnosticState rbBeforeCorrection,
        in RigidbodyDiagnosticState rbAfterCorrection)
    {
        int currentTick = NetTickUtil.CurrentTick;
        int serverTimelineTick = NetTickUtil.ServerTick;
        bool inputMatchesCorrectionTick = input.Tick == serverState.Tick;
        bool recentSideEffect = HasRecentMovementSideEffect(serverState.Tick, currentTick);

        Vector3 referencePosition = predictionMatchesServerTick
            ? predicted.Position
            : _ownerStateInitialized ? _ownerState.Position : predicted.Position;
        Vector3 referenceVelocity = predictionMatchesServerTick
            ? predicted.Velocity
            : _ownerStateInitialized ? _ownerState.Velocity : predicted.Velocity;
        Quaternion referenceRotation = predictionMatchesServerTick
            ? predicted.Rotation
            : _ownerStateInitialized ? _ownerState.Rotation : predicted.Rotation;

        float positionError = Vector3.Distance(referencePosition, serverState.Position);
        float velocityError = Vector3.Distance(referenceVelocity, serverState.Velocity);
        float rotationError = Quaternion.Angle(referenceRotation, serverState.Rotation);

        ulong networkObjectId = NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL;
        ulong ownerClientId = NetworkObject != null ? NetworkObject.OwnerClientId : 0UL;

        StringBuilder builder = new StringBuilder(1400);
        builder.AppendLine("[NetMovement3D Correction]");
        builder.AppendLine($"cause={cause}");
        builder.AppendLine($"likelyCause={GetLikelyOwnerCorrectionCause(cause, recentSideEffect)}");
        builder.AppendLine($"object={name} networkObjectId={networkObjectId} ownerClientId={ownerClientId} playerSlot={PlayerSlot} movementLocked={_movementLocked.Value}");
        builder.AppendLine($"serverStateTick={serverState.Tick} localCurrentTick={currentTick} observedServerTick={serverTimelineTick} ticksReplayed={ticksReplayed}");
        builder.AppendLine($"predictionBufferTick={predicted.Tick} predictionMatchesServerTick={predictionMatchesServerTick} inputTick={input.Tick} inputMatchesCorrectionTick={inputMatchesCorrectionTick}");
        builder.AppendLine($"correctionDistance={correctionDistance:0.###} positionError={positionError:0.###} velocityError={velocityError:0.###} rotationErrorDegrees={rotationError:0.###} correctionsPerSecond={_ownerCorrectionsPerSecond:0.##} totalCorrections={_ownerCorrectionCount}");
        builder.AppendLine($"predictedPosition={FormatVector3(predicted.Position)} serverPosition={FormatVector3(serverState.Position)} currentOwnerPosition={FormatVector3(_ownerState.Position)}");
        builder.AppendLine($"predictedVelocity={FormatVector3(predicted.Velocity)} serverVelocity={FormatVector3(serverState.Velocity)} currentOwnerVelocity={FormatVector3(_ownerState.Velocity)}");
        builder.AppendLine($"input thrust={input.ThrustInput:0.###} look={FormatVector2(input.LookInput)} friction={input.FrictionEnabled} baseRot={input.BaseRotationMultiplier:0.###} abilityRot={input.AbilityRotationMultiplier:0.###} thrustMul={input.ThrustMultiplier:0.###} maxSpeedMul={input.MaxSpeedMultiplier:0.###} slowMul={input.SlowMultiplier:0.###}");
        builder.AppendLine($"input dodgeRequest={input.DodgeRequested} dodgeKind={input.DodgeKind} dodgeDirection={FormatVector3(input.DodgeDirection)}");
        builder.AppendLine($"flightConfig={FormatFlightConfig()}");
        builder.AppendLine($"flightAssist={FormatFlightAssistConfig()}");
        builder.AppendLine($"movementCollision blockerMask={networkMovementBlockerMask.value} skinWidth={blockerSkinWidth:0.###} minSweepDistance={minimumBlockerSweepDistance:0.####} lockToWorldY={(_shipFlight != null && _shipFlight.LockToWorldYPlane)} lockedWorldY={(_shipFlight != null ? _shipFlight.LockedWorldY : 0f):0.###}");
        if (logRigidbodyStateDrift)
        {
            builder.AppendLine($"rigidbodyBeforeCorrection={FormatRigidbodyState(rbBeforeCorrection)}");
            builder.AppendLine($"rigidbodyAfterCorrection={FormatRigidbodyState(rbAfterCorrection)}");
        }
        builder.AppendLine($"recentMovementSideEffect={GetMostRecentMovementSideEffectSummary(currentTick)}");
        builder.AppendLine($"lastCombatVelocityDelta={FormatTickedVector(_lastCombatVelocityDeltaTick, _lastCombatVelocityDelta)}");
        builder.AppendLine($"lastCombatWarp={FormatTickedVector(_lastCombatWarpTick, _lastCombatWarpPosition)}");
        builder.AppendLine($"lastBoundaryCorrection={FormatBoundaryCorrection()}");
        builder.AppendLine($"lastQueuedDodge={FormatQueuedDodge()}");
        builder.AppendLine($"lastMovementReset={FormatMovementReset(currentTick)}");

        Debug.Log(builder.ToString(), this);
    }

    private void UpdateOwnerCorrectionSummary(int currentTick)
    {
        if (!enableOwnerMovementDiagnostics || correctionSummaryIntervalSeconds <= 0f)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (_correctionSummaryWindowStartedAt <= 0f)
        {
            _correctionSummaryWindowStartedAt = now;
            return;
        }

        float elapsed = now - _correctionSummaryWindowStartedAt;
        if (elapsed < correctionSummaryIntervalSeconds)
        {
            return;
        }

        _ownerCorrectionsPerSecond = _correctionsInCurrentSummaryWindow / Mathf.Max(0.001f, elapsed);
        if (logOwnerCorrections && _correctionsInCurrentSummaryWindow > 0)
        {
            Debug.Log(
                $"[NetMovement3D Correction Summary] object={name} slot={PlayerSlot} tick={currentTick} corrections={_correctionsInCurrentSummaryWindow} rate={_ownerCorrectionsPerSecond:0.##}/s latest={_latestOwnerCorrectionDistance:0.###} largest={_largestRecentOwnerCorrectionDistance:0.###} likelyCause={_latestOwnerCorrectionLikelyCause} sideEffect={_latestOwnerCorrectionSideEffect}",
                this);
        }

        _correctionsInCurrentSummaryWindow = 0;
        _largestRecentOwnerCorrectionDistance = 0f;
        _correctionSummaryWindowStartedAt = now;
    }

    private void RegisterBlockerClamp(float requestedDistance, float allowedDistance, float sweepRadius, Collider hitCollider)
    {
        _blockerClampCount++;
        _blockerClampsInCurrentSummaryWindow++;
        _latestBlockerClampDistance = Mathf.Max(0f, requestedDistance - allowedDistance);
        _latestBlockerSweepRadius = sweepRadius;
        _latestBlockerClampCollider = hitCollider != null ? hitCollider.name : "unknown";
        _lastBlockerClampTick = NetTickUtil.CurrentTick;

        UpdateBlockerClampSummary();
    }

    private void UpdateBlockerClampSummary()
    {
        if (!enableOwnerMovementDiagnostics || correctionSummaryIntervalSeconds <= 0f)
        {
            return;
        }

        float now = Time.time;
        if (_blockerClampSummaryWindowStartedAt <= 0f)
        {
            _blockerClampSummaryWindowStartedAt = now;
            return;
        }

        float elapsed = now - _blockerClampSummaryWindowStartedAt;
        if (elapsed < correctionSummaryIntervalSeconds)
        {
            return;
        }

        _blockerClampsPerSecond = _blockerClampsInCurrentSummaryWindow / Mathf.Max(0.0001f, elapsed);
        if (_blockerClampsInCurrentSummaryWindow > 0 && logOwnerCorrections)
        {
            Debug.Log(
                $"[NetMovement3D Blocker Clamp Summary] object={name} slot={PlayerSlot} tick={NetTickUtil.CurrentTick} clamps={_blockerClampsInCurrentSummaryWindow} rate={_blockerClampsPerSecond:0.##}/s latestClamp={_latestBlockerClampDistance:0.###} sweepRadius={_latestBlockerSweepRadius:0.###} collider={_latestBlockerClampCollider}",
                this);
        }

        _blockerClampsInCurrentSummaryWindow = 0;
        _blockerClampSummaryWindowStartedAt = now;
    }

    private void LogMovementConfigIfNeeded(string context)
    {
        if (!enableOwnerMovementDiagnostics || !logMovementConfigOnSpawn)
        {
            return;
        }

        StringBuilder builder = new StringBuilder(900);
        builder.AppendLine("[NetMovement3D Movement Config]");
        builder.AppendLine($"context={context} object={name} networkObjectId={(NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL)} ownerClientId={(NetworkObject != null ? NetworkObject.OwnerClientId : 0UL)} playerSlot={PlayerSlot} isOwner={IsOwner} isServer={IsServer}");
        builder.AppendLine($"tick={NetTickUtil.CurrentTick} serverTick={NetTickUtil.ServerTick} tickRate={NetTickUtil.TickRate} tickInterval={NetTickUtil.TickInterval:0.####} movementLocked={_movementLocked.Value}");
        builder.AppendLine($"flightConfig={FormatFlightConfig()}");
        builder.AppendLine($"flightAssist={FormatFlightAssistConfig()}");
        builder.AppendLine($"movementCollision blockerMask={networkMovementBlockerMask.value} skinWidth={blockerSkinWidth:0.###} minSweepDistance={minimumBlockerSweepDistance:0.####}");
        builder.AppendLine($"rigidbody={FormatRigidbodyState(CaptureRigidbodyDiagnosticState())}");
        Debug.Log(builder.ToString(), this);
    }

    public string BuildMovementConfigFingerprint()
    {
        CacheReferences();
        return $"{FormatFlightConfig()} | {FormatFlightAssistConfig()} | blockerMask={networkMovementBlockerMask.value} skin={blockerSkinWidth:0.###} minSweep={minimumBlockerSweepDistance:0.####}";
    }

    private RigidbodyDiagnosticState CaptureRigidbodyDiagnosticState()
    {
        if (_rb == null)
        {
            return default;
        }

        return new RigidbodyDiagnosticState
        {
            HasBody = true,
            Position = _rb.position,
            Rotation = _rb.rotation,
            Velocity = _rb.linearVelocity,
            AngularVelocity = _rb.angularVelocity,
            IsKinematic = _rb.isKinematic,
            Interpolation = _rb.interpolation
        };
    }

    private bool HasRecentMovementSideEffect(int serverTick, int currentTick)
    {
        return IsSideEffectNearTick(_lastCombatVelocityDeltaTick, serverTick, currentTick)
            || IsSideEffectNearTick(_lastCombatWarpTick, serverTick, currentTick)
            || IsSideEffectNearTick(_lastBoundaryCorrectionTick, serverTick, currentTick)
            || IsSideEffectNearTick(_lastBlockerClampTick, serverTick, currentTick)
            || IsSideEffectNearTick(_lastQueuedDodgeTick, serverTick, currentTick);
    }

    private static bool IsSideEffectNearTick(int sideEffectTick, int serverTick, int currentTick)
    {
        if (sideEffectTick < 0)
        {
            return false;
        }

        return Mathf.Abs(serverTick - sideEffectTick) <= RecentMovementSideEffectWindowTicks
            || Mathf.Abs(currentTick - sideEffectTick) <= RecentMovementSideEffectWindowTicks;
    }

    private static string GetLikelyOwnerCorrectionCause(OwnerCorrectionCause cause, bool recentSideEffect)
    {
        if (recentSideEffect)
        {
            return "recent non-replayable movement side effect may be involved";
        }

        if (cause == OwnerCorrectionCause.PredictionMissing)
        {
            return "prediction buffer missing or overwritten";
        }

        if (cause == OwnerCorrectionCause.PositionError)
        {
            return "server/client simulation drift";
        }

        return "authoritative snap applied";
    }

    private string GetMostRecentMovementSideEffectSummary(int currentTick)
    {
        string name = "none";
        int tick = -1;

        SetMostRecentMovementSideEffect(ref name, ref tick, "combat velocity delta", _lastCombatVelocityDeltaTick);
        SetMostRecentMovementSideEffect(ref name, ref tick, "combat warp", _lastCombatWarpTick);
        SetMostRecentMovementSideEffect(ref name, ref tick, "boundary correction", _lastBoundaryCorrectionTick);
        SetMostRecentMovementSideEffect(ref name, ref tick, "blocker clamp", _lastBlockerClampTick);
        SetMostRecentMovementSideEffect(ref name, ref tick, "queued dodge", _lastQueuedDodgeTick);

        if (tick < 0)
        {
            return "none";
        }

        return $"{name} tick={tick} ticksAgo={Mathf.Max(0, currentTick - tick)}";
    }

    private static void SetMostRecentMovementSideEffect(ref string name, ref int tick, string candidateName, int candidateTick)
    {
        if (candidateTick > tick)
        {
            name = candidateName;
            tick = candidateTick;
        }
    }

    private string FormatBoundaryCorrection()
    {
        if (_lastBoundaryCorrectionTick < 0)
        {
            return "none";
        }

        return $"tick={_lastBoundaryCorrectionTick}, position={FormatVector3(_lastBoundaryCorrectionPosition)}, velocity={FormatVector3(_lastBoundaryCorrectionVelocity)}";
    }

    private string FormatMovementReset(int currentTick)
    {
        return _lastMovementResetTick < 0
            ? "none"
            : $"tick={_lastMovementResetTick}, ticksAgo={Mathf.Max(0, currentTick - _lastMovementResetTick)}, position={FormatVector3(_lastMovementResetPosition)}, resetCamera={_lastMovementResetCamera}";
    }

    private string FormatQueuedDodge()
    {
        return _lastQueuedDodgeTick < 0
            ? "none"
            : $"tick={_lastQueuedDodgeTick}, kind={_lastQueuedDodgeKind}, value={FormatVector3(_lastQueuedDodgeDirection)}";
    }

    private string FormatFlightConfig()
    {
        if (_shipFlight == null)
        {
            return "missing ShipFlight3D";
        }

        ShipFlightConfig3D flight = _shipFlight.FlightConfig;
        return $"thrust={flight.thrustAcceleration:0.###}, maxSpeed={flight.maxSpeed:0.###}, precisionThreshold={flight.precisionThrottleInputThreshold:0.###}, precisionMaxFraction={flight.precisionThrottleMaxSpeedFraction:0.###}, precisionAccelMul={flight.precisionThrottleAccelerationMultiplier:0.###}, brake={flight.precisionBrakeDeceleration:0.###}, lookResponse={flight.lookInputResponse:0.###}, pitchSpeed={flight.pitchSpeed:0.###}, yawSpeed={flight.yawSpeed:0.###}, pitchAccel={flight.pitchAcceleration:0.###}, pitchDecel={flight.pitchDeceleration:0.###}, yawAccel={flight.yawAcceleration:0.###}, yawDecel={flight.yawDeceleration:0.###}, invertY={flight.invertY}, minRotAtMaxSpeed={flight.minRotationMultiplierAtMaxSpeed:0.###}, lockToWorldY={_shipFlight.LockToWorldYPlane}, lockedWorldY={_shipFlight.LockedWorldY:0.###}";
    }

    private string FormatFlightAssistConfig()
    {
        if (_shipFlight == null)
        {
            return "missing ShipFlight3D";
        }

        ShipFlightAssistConfig3D assist = _shipFlight.FlightAssistConfig;
        return $"frictionDecel={assist.frictionDeceleration:0.###}, angularDamping={assist.activeAngularDamping:0.###}, lateralDamping={assist.lateralDriftDamping:0.###}, verticalDamping={assist.verticalDriftDamping:0.###}, alignment={assist.velocityAlignmentStrength:0.###}, recoveryDriftMul={assist.recoveryDriftDampingMultiplier:0.###}, recoveryAlignment={assist.recoveryVelocityAlignmentStrength:0.###}, frictionEnabled={_shipFlight.IsFrictionEnabled}, externalSimulation={_shipFlight.IsExternalSimulationEnabled}";
    }

    private static string FormatRigidbodyState(RigidbodyDiagnosticState state)
    {
        if (!state.HasBody)
        {
            return "missing Rigidbody";
        }

        return $"position={FormatVector3(state.Position)}, rotation={FormatQuaternion(state.Rotation)}, velocity={FormatVector3(state.Velocity)}, angularVelocity={FormatVector3(state.AngularVelocity)}, isKinematic={state.IsKinematic}, interpolation={state.Interpolation}";
    }

    private static string FormatTickedVector(int tick, Vector3 value)
    {
        return tick < 0 ? "none" : $"tick={tick}, value={FormatVector3(value)}";
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.###}, {value.y:0.###})";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }

    private static string FormatQuaternion(Quaternion value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###}, {value.w:0.###})";
    }

    private void ResetRemoteInterpolationState()
    {
        if (IsOwner || IsServer)
        {
            return;
        }

        _remoteInterpolator.Reset();
        _hasLastAppliedVisualVelocity = false;
        _remoteDodgePresentationActive = false;
    }

    private void HandleMovementLockedChanged(bool previousValue, bool newValue)
    {
        ApplyMovementLock(newValue);
    }

    private void HandlePlayerIndexChanged(byte previousValue, byte newValue)
    {
        ApplyPlayerIndex(newValue);
    }

    private void ApplyMovementLock(bool isLocked)
    {
        if (IsOwner && _playerInput3D != null)
        {
            _playerInput3D.SetCombatInputSuppressed(isLocked || !HasNetworkCombatBridgeForOwner());
        }

        if (_rb == null)
        {
            return;
        }

        if (!isLocked)
        {
            return;
        }

        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
        }

        _rb.angularVelocity = Vector3.zero;

        if (_ownerStateInitialized)
        {
            _ownerState.Velocity = Vector3.zero;
            _ownerState.FilteredLookInput = Vector2.zero;
            _ownerState.TurnRates = Vector2.zero;
            _ownerState.DodgeVelocity = Vector3.zero;
            _ownerState.DodgeExitVelocity = Vector3.zero;
            _ownerState.DodgeRemainingTime = 0f;
            _ownerState.DodgeDuration = 0f;
            _ownerState.DodgeKind = NetDodgeKind3D.Generic;
        }

        if (_serverStateInitialized)
        {
            _serverState.Velocity = Vector3.zero;
            _serverState.FilteredLookInput = Vector2.zero;
            _serverState.TurnRates = Vector2.zero;
            _serverState.DodgeVelocity = Vector3.zero;
            _serverState.DodgeExitVelocity = Vector3.zero;
            _serverState.DodgeRemainingTime = 0f;
            _serverState.DodgeDuration = 0f;
            _serverState.DodgeKind = NetDodgeKind3D.Generic;
        }

        _shipFlight?.ApplyExternalSimulationState(
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            0f,
            _ownerFrictionEnabled,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero);

        _cameraRig?.ResetCameraState(snapToNeutralOffset: true);
        ResetRemoteInterpolationState();
    }

    private void ApplyPlayerIndex(byte index)
    {
        if (index == 0)
        {
            return;
        }

        gameObject.tag = index == 1 ? "Player1" : "Player2";
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

}
