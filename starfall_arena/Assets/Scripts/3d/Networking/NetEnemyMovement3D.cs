using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ShipFlight3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
public class NetEnemyMovement3D : NetworkBehaviour
{
    private const int InterpolationBufferSize = 64;

    [Header("Enemy Interpolation")]
    [SerializeField] private int interpolationBufferTicks = 2;

    private ShipFlight3D _shipFlight;
    private EnemyAIFlightController3D _aiInput;
    private Rigidbody _rb;
    private NetStateSnapshot3D[] _interpolationBuffer;
    private MovementState3D _serverState;
    private bool _serverStateInitialized;
    private bool _serverFrictionEnabled;
    private int _lastPublishedTick = -1;
    private int _interpWriteIndex;
    private int _interpCount;
    private float _interpTimer;
    private Vector3 _lastAppliedVisualVelocity;
    private bool _hasLastAppliedVisualVelocity;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();
        _shipFlight.SetExternalSimulationEnabled(true);
        _serverFrictionEnabled = _shipFlight.IsFrictionEnabled;
        _serverState = CaptureCurrentState();
        _serverStateInitialized = true;

        if (!IsServer)
        {
            _interpolationBuffer = new NetStateSnapshot3D[InterpolationBufferSize];
            _rb.isKinematic = true;
        }
        else
        {
            _rb.isKinematic = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_shipFlight != null)
        {
            _shipFlight.SetExternalSimulationEnabled(false);
        }

        if (_rb != null)
        {
            _rb.isKinematic = false;
        }

        base.OnNetworkDespawn();
    }

    private void FixedUpdate()
    {
        if (!NetTickUtil.IsActive || !IsSpawned)
        {
            return;
        }

        if (IsServer)
        {
            ServerTick();
        }
        else
        {
            InterpolateRemote();
        }
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

        if (_serverStateInitialized)
        {
            _serverState.Velocity += velocityDelta;
        }
    }

    private void ServerTick()
    {
        int tick = NetTickUtil.CurrentTick;
        if (tick <= _lastPublishedTick)
        {
            return;
        }

        _lastPublishedTick = tick;
        float dt = GetTickDeltaTime();
        Vector3 previousVelocity = _serverState.Velocity;

        NetInputSnapshot3D input = new NetInputSnapshot3D
        {
            Tick = tick,
            LookInput = _aiInput != null ? _aiInput.LookInput : Vector2.zero,
            ThrustInput = _aiInput != null ? _aiInput.ThrustInput : 0f,
            FrictionEnabled = _serverFrictionEnabled,
            BaseRotationMultiplier = 1f,
            AbilityRotationMultiplier = 1f,
            ThrustMultiplier = 1f,
            SlowMultiplier = 1f
        };

        Entity3D entity = GetComponent<Entity3D>();
        if (entity != null)
        {
            input.AbilityRotationMultiplier = entity.GetAbilityRotationMultiplier();
            input.ThrustMultiplier = entity.GetCombinedThrustMultiplier();
            input.SlowMultiplier = entity.GetSlowMultiplier();
        }

        MovementSimulation3D.SimulateTick(
            ref _serverState,
            in input,
            _shipFlight.FlightConfig,
            _shipFlight.FlightAssistConfig,
            _shipFlight.LockToWorldYPlane,
            _shipFlight.LockedWorldY,
            dt);

        ApplySimulationState(_serverState);
        ApplyFlightTelemetry(input.LookInput, input.ThrustInput, _serverFrictionEnabled, _serverState, previousVelocity, dt);
        BroadcastStateClientRpc(ToSnapshot(tick, _serverState, input.ThrustInput, _serverFrictionEnabled));
    }

    [ClientRpc]
    private void BroadcastStateClientRpc(NetStateSnapshot3D snapshot)
    {
        if (IsServer)
        {
            return;
        }

        BufferInterpolationState(snapshot);
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
            TurnRates = Vector2.Lerp(from.TurnRates, to.TurnRates, t)
        };

        ApplySimulationState(interpolatedState);
        Vector3 previousVelocity = _hasLastAppliedVisualVelocity ? _lastAppliedVisualVelocity : from.Velocity;
        ApplyFlightTelemetry(interpolatedState.FilteredLookInput, Mathf.Lerp(from.ThrustInput, to.ThrustInput, t), to.FrictionEnabled, interpolatedState, previousVelocity, tickInterval);
        _lastAppliedVisualVelocity = interpolatedState.Velocity;
        _hasLastAppliedVisualVelocity = true;

        if (t >= 1f)
        {
            _interpTimer -= duration;
        }
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
            TurnRates = Vector2.zero
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
            FrictionEnabled = frictionEnabled
        };
    }

    private float GetTickDeltaTime()
    {
        float tickInterval = NetTickUtil.TickInterval;
        return tickInterval > 0f ? tickInterval : Time.fixedDeltaTime;
    }

    private void CacheReferences()
    {
        _shipFlight ??= GetComponent<ShipFlight3D>();
        _aiInput ??= GetComponent<EnemyAIFlightController3D>();
        _rb ??= GetComponent<Rigidbody>();
    }
}
