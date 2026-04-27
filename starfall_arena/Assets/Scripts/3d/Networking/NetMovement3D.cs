using System.Collections.Generic;
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

    private static readonly List<NetMovement3D> ActiveInstances = new List<NetMovement3D>();

    [Header("Reconciliation")]
    [SerializeField] private float reconciliationThreshold = 0.1f;

    [Header("Interpolation")]
    [SerializeField] private int interpolationBufferTicks = 2;

    [Header("Combat Rewind")]
    [SerializeField] private int maxCombatRewindTicks = 6;

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
    private NetStateSnapshot3D[] _interpolationBuffer;

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
    private Vector3 _pendingDodgeDirection;
    private int _lastSentTick = -1;
    private int _lastReceivedServerTick = -1;
    private int _lastProcessedServerTick = -1;
    private int _stateHistoryHead = -1;

    private int _interpWriteIndex;
    private int _interpCount;
    private float _interpTimer;
    private Vector3 _lastAppliedVisualVelocity;
    private bool _hasLastAppliedVisualVelocity;
    private bool _remoteDodgePresentationActive;

    public byte PlayerSlot => _networkPlayerIndex.Value;

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
            _interpolationBuffer = new NetStateSnapshot3D[ServerStateBufferSize];
            _rb.isKinematic = true;
        }
        else
        {
            _rb.isKinematic = false;
        }

        ApplyMovementLock(_movementLocked.Value);
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

        if (_rb != null)
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

    public bool QueuePredictedDodge(Vector3 worldDirection)
    {
        if (!IsOwner || !IsSpawned || _movementLocked.Value || worldDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _pendingDodgeRequested = true;
        _pendingDodgeDirection = worldDirection.normalized;
        return true;
    }

    public void ApplyCombatWarp(Vector3 position)
    {
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
    }

    public void ApplyBoundaryCorrection(Vector3 correctedPosition, Vector3 correctedVelocity)
    {
        if (_rb != null)
        {
            _rb.position = correctedPosition;
            _rb.linearVelocity = correctedVelocity;
        }

        transform.position = correctedPosition;

        if (_ownerStateInitialized)
        {
            _ownerState.Position = correctedPosition;
            _ownerState.Velocity = correctedVelocity;
            _ownerState.DodgeVelocity = Vector3.zero;
            _ownerState.DodgeExitVelocity = Vector3.zero;
            _ownerState.DodgeRemainingTime = 0f;
        }

        if (_serverStateInitialized)
        {
            _serverState.Position = correctedPosition;
            _serverState.Velocity = correctedVelocity;
            _serverState.DodgeVelocity = Vector3.zero;
            _serverState.DodgeExitVelocity = Vector3.zero;
            _serverState.DodgeRemainingTime = 0f;
        }
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

        if (_playerInput3D != null && _playerInput3D.ConsumeToggleFrictionPressed())
        {
            _ownerFrictionEnabled = !_ownerFrictionEnabled;
        }

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
            SlowMultiplier = _player != null ? _player.GetSlowMultiplier() : 1f,
            DodgeRequested = !_movementLocked.Value && _pendingDodgeRequested,
            DodgeDirection = _pendingDodgeDirection
        };
        _pendingDodgeRequested = false;
        _pendingDodgeDirection = Vector3.zero;

        int inputIndex = tick % ClientInputBufferSize;
        _inputBuffer[inputIndex] = input;

        Vector3 previousVelocity = _ownerState.Velocity;
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
            input.DodgeDirection = Vector3.zero;
        }

        if (_player != null)
        {
            input.SlowMultiplier = _player.GetSlowMultiplier();
        }

        _serverFrictionEnabled = input.FrictionEnabled;

        Vector3 previousVelocity = _serverState.Velocity;
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

    [ClientRpc]
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
            ApplyAuthoritativeCorrection(serverState);
            return;
        }

        if (Vector3.Distance(predicted.Position, serverState.Position) <= reconciliationThreshold)
        {
            return;
        }

        MovementState3D replayState = ToMovementState(serverState);
        float dt = GetTickDeltaTime();
        int currentTick = NetTickUtil.CurrentTick;

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
        ApplySimulationState(_ownerState);

        int latestInputIndex = currentTick % ClientInputBufferSize;
        Vector2 latestLookInput = _inputBuffer[latestInputIndex].Tick == currentTick
            ? _inputBuffer[latestInputIndex].LookInput
            : Vector2.zero;
        Vector3 previousVelocity = predicted.Tick == currentTick ? predicted.Velocity : serverState.Velocity;
        ApplyFlightTelemetry(latestLookInput, _predictionBuffer[latestInputIndex].ThrustInput, _predictionBuffer[latestInputIndex].FrictionEnabled, _ownerState, previousVelocity, dt);
    }

    private void ApplyAuthoritativeCorrection(NetStateSnapshot3D snapshot)
    {
        _ownerState = ToMovementState(snapshot);
        ApplySimulationState(_ownerState);
        ApplyFlightTelemetry(snapshot.FilteredLookInput, snapshot.ThrustInput, snapshot.FrictionEnabled, _ownerState, snapshot.Velocity, GetTickDeltaTime());
    }

    private void BufferInterpolationState(NetStateSnapshot3D state)
    {
        if (_interpolationBuffer == null || _interpolationBuffer.Length == 0)
        {
            return;
        }

        int index = _interpWriteIndex % _interpolationBuffer.Length;
        _interpolationBuffer[index] = state;
        _interpWriteIndex++;
        _interpCount = Mathf.Min(_interpCount + 1, _interpolationBuffer.Length);
    }

    private void InterpolateRemote()
    {
        int requiredSamples = Mathf.Max(2, interpolationBufferTicks);
        if (_interpCount < requiredSamples)
        {
            return;
        }

        int newestIndex = (_interpWriteIndex - 1) % _interpolationBuffer.Length;
        if (newestIndex < 0)
        {
            newestIndex += _interpolationBuffer.Length;
        }

        int olderIndex = (_interpWriteIndex - 2) % _interpolationBuffer.Length;
        if (olderIndex < 0)
        {
            olderIndex += _interpolationBuffer.Length;
        }

        NetStateSnapshot3D from = _interpolationBuffer[olderIndex];
        NetStateSnapshot3D to = _interpolationBuffer[newestIndex];
        int tickDelta = to.Tick - from.Tick;
        if (tickDelta <= 0)
        {
            return;
        }

        float tickInterval = GetTickDeltaTime();
        float duration = tickDelta * tickInterval;
        _interpTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_interpTimer / duration);

        MovementState3D interpolatedState = new MovementState3D
        {
            Position = Vector3.Lerp(from.Position, to.Position, t),
            Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
            Velocity = Vector3.Lerp(from.Velocity, to.Velocity, t),
            FilteredLookInput = Vector2.Lerp(from.FilteredLookInput, to.FilteredLookInput, t),
            TurnRates = Vector2.Lerp(from.TurnRates, to.TurnRates, t),
            DodgeVelocity = Vector3.Lerp(from.DodgeVelocity, to.DodgeVelocity, t),
            DodgeExitVelocity = Vector3.Lerp(from.DodgeExitVelocity, to.DodgeExitVelocity, t),
            DodgeRemainingTime = Mathf.Lerp(from.DodgeRemainingTime, to.DodgeRemainingTime, t)
        };

        ApplySimulationState(interpolatedState);
        UpdateRemoteDodgePresentation(interpolatedState);

        Vector3 previousVelocity = _hasLastAppliedVisualVelocity ? _lastAppliedVisualVelocity : from.Velocity;
        ApplyFlightTelemetry(interpolatedState.FilteredLookInput, Mathf.Lerp(from.ThrustInput, to.ThrustInput, t), to.FrictionEnabled, interpolatedState, previousVelocity, tickInterval);
        _lastAppliedVisualVelocity = interpolatedState.Velocity;
        _hasLastAppliedVisualVelocity = true;

        if (t >= 1f)
        {
            _interpTimer -= duration;
        }
    }

    private void SimulateInputTick(
        ref MovementState3D state,
        in NetInputSnapshot3D input,
        float dt,
        bool validateDodge)
    {
        ApplyDodgeFromInput(ref state, in input, validateDodge);

        MovementSimulation3D.SimulateTick(
            ref state,
            in input,
            _shipFlight.FlightConfig,
            _shipFlight.FlightAssistConfig,
            _shipFlight.LockToWorldYPlane,
            _shipFlight.LockedWorldY,
            dt);
    }

    private void ApplyDodgeFromInput(ref MovementState3D state, in NetInputSnapshot3D input, bool validateDodge)
    {
        if (!input.DodgeRequested || input.DodgeDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        CacheReferences();
        if (_dodgeAbility == null)
        {
            return;
        }

        if (validateDodge && !_dodgeAbility.CanAcceptNetworkDodgeRequest())
        {
            return;
        }

        if (!_dodgeAbility.TryResolveNetworkDodge(
            input.DodgeDirection,
            state.Position,
            GetCollisionRadius(),
            out Vector3 dashVelocity,
            out float duration))
        {
            return;
        }

        state.DodgeVelocity = dashVelocity;
        state.DodgeExitVelocity = Vector3.zero;
        state.DodgeRemainingTime = duration;

        if (validateDodge)
        {
            _dodgeAbility.MarkNetworkDodgeAccepted();
        }
    }

    private void UpdateRemoteDodgePresentation(in MovementState3D state)
    {
        bool isDodgeActive = state.DodgeRemainingTime > 0.001f && state.DodgeVelocity.sqrMagnitude > 0.0001f;
        if (isDodgeActive && !_remoteDodgePresentationActive)
        {
            CacheReferences();
            _dodgeAbility?.PlayNetworkDodgePresentation(state.DodgeVelocity);
        }

        _remoteDodgePresentationActive = isDodgeActive;
    }

    private void ApplySimulationState(in MovementState3D state)
    {
        _rb.position = state.Position;
        _rb.rotation = state.Rotation;
        _rb.linearVelocity = state.Velocity;
        transform.SetPositionAndRotation(state.Position, state.Rotation);
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

        Vector3 linearAcceleration = dt > 0f ? (state.Velocity - previousVelocity) / dt : Vector3.zero;
        _shipFlight.ApplyExternalSimulationState(
            rawLookInput,
            state.FilteredLookInput,
            state.TurnRates,
            thrustInput,
            frictionEnabled,
            state.Velocity,
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
            DodgeRemainingTime = 0f
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
            DodgeRemainingTime = snapshot.DodgeRemainingTime
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
            DodgeRemainingTime = state.DodgeRemainingTime
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

        _rb.linearVelocity = Vector3.zero;

        if (_ownerStateInitialized)
        {
            _ownerState.Velocity = Vector3.zero;
            _ownerState.DodgeVelocity = Vector3.zero;
            _ownerState.DodgeExitVelocity = Vector3.zero;
            _ownerState.DodgeRemainingTime = 0f;
        }

        if (_serverStateInitialized)
        {
            _serverState.Velocity = Vector3.zero;
            _serverState.DodgeVelocity = Vector3.zero;
            _serverState.DodgeExitVelocity = Vector3.zero;
            _serverState.DodgeRemainingTime = 0f;
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
