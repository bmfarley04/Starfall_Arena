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
    [SerializeField] private int interpolationBufferTicks = 2;

    private Rigidbody _rb;
    private EnemyAIFlightController3D _enemyFlight;
    private NetStateSnapshot3D[] _interpolationBuffer;
    private int _lastPublishedTick = -1;
    private int _interpWriteIndex;
    private int _interpCount;
    private float _interpTimer;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();

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
            PublishServerState();
        }
        else
        {
            InterpolateRemote();
        }
    }

    public void ApplyCombatVelocityDelta(Vector3 velocityDelta)
    {
        if (_rb == null || velocityDelta.sqrMagnitude <= 0.000001f)
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

        float duration = tickDelta * GetTickDeltaTime();
        if (duration <= 0f)
        {
            return;
        }

        _interpTimer += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_interpTimer / duration);
        Vector3 position = Vector3.Lerp(from.Position, to.Position, t);
        Quaternion rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
        Vector3 velocity = Vector3.Lerp(from.Velocity, to.Velocity, t);

        _rb.position = position;
        _rb.rotation = rotation;
        _rb.linearVelocity = velocity;
        transform.SetPositionAndRotation(position, rotation);

        if (t >= 1f)
        {
            _interpTimer -= duration;
        }
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
