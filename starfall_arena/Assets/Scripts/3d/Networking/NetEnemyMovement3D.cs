using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
public class NetEnemyMovement3D : NetworkBehaviour
{
    private const int InterpolationBufferSize = 64;

    [Header("Enemy Interpolation")]
    [SerializeField] private NetInterpolationSettings3D interpolationSettings = new NetInterpolationSettings3D();

    private Rigidbody _rb;
    private EnemyAIFlightController3D _enemyFlight;
    private readonly NetSnapshotInterpolator3D _remoteInterpolator = new NetSnapshotInterpolator3D();
    private int _lastPublishedTick = -1;

    public NetInterpolationDiagnostics3D InterpolationDiagnostics => _remoteInterpolator.Diagnostics;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();

        if (!IsServer)
        {
            _remoteInterpolator.Initialize(InterpolationBufferSize, interpolationSettings);
            _rb.isKinematic = true;
        }
        else
        {
            _rb.isKinematic = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_rb != null)
        {
            _rb.isKinematic = false;
        }

        _remoteInterpolator.Reset();
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
            PublishServerState();
        }
        else
        {
            InterpolateRemote();
        }
    }

    public void ApplyCombatVelocityDelta(Vector3 velocityDelta)
    {
        if (_rb == null || _rb.isKinematic || velocityDelta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        _rb.linearVelocity += velocityDelta;
    }

    private void PublishServerState()
    {
        int tick = NetTickUtil.CurrentTick;
        if (tick <= _lastPublishedTick)
        {
            return;
        }

        _lastPublishedTick = tick;
        BroadcastStateClientRpc(CaptureSnapshot(tick));
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
        _remoteInterpolator.AddSnapshot(state, this, "NetEnemyMovement3D");
    }

    private void InterpolateRemote()
    {
        if (!_remoteInterpolator.TrySample(
            interpolationSettings,
            GetTickDeltaTime(),
            NetTickUtil.ServerTick,
            Time.fixedDeltaTime,
            this,
            "NetEnemyMovement3D",
            out MovementState3D sampledState,
            out _,
            out _))
        {
            return;
        }

        _rb.position = sampledState.Position;
        _rb.rotation = sampledState.Rotation;
        transform.SetPositionAndRotation(sampledState.Position, sampledState.Rotation);
    }

    private NetStateSnapshot3D CaptureSnapshot(int tick)
    {
        Vector3 velocity = _rb != null ? _rb.linearVelocity : Vector3.zero;
        return new NetStateSnapshot3D
        {
            Tick = tick,
            Position = transform.position,
            Rotation = transform.rotation,
            Velocity = velocity,
            FilteredLookInput = Vector2.zero,
            TurnRates = Vector2.zero,
            ThrustInput = _enemyFlight != null && _enemyFlight.MoveDirection.sqrMagnitude > 0.0001f ? 1f : 0f,
            FrictionEnabled = false
        };
    }

    private float GetTickDeltaTime()
    {
        float tickInterval = NetTickUtil.TickInterval;
        return tickInterval > 0f ? tickInterval : Time.fixedDeltaTime;
    }

    private void CacheReferences()
    {
        _rb ??= GetComponent<Rigidbody>();
        _enemyFlight ??= GetComponent<EnemyAIFlightController3D>();
    }
}
