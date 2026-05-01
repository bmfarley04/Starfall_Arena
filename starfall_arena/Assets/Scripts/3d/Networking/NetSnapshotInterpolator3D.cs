using UnityEngine;

[System.Serializable]
public class NetInterpolationSettings3D
{
    [Tooltip("Minimum remote-object visual delay in milliseconds. More delay gives the buffer room to hide uneven packet arrival.")]
    [Min(0f)] public float minVisualDelayMs = 80f;

    [Tooltip("Maximum remote-object visual delay in milliseconds. The interpolator moves toward this when it repeatedly runs out of future snapshots.")]
    [Min(0f)] public float maxVisualDelayMs = 120f;

    [Tooltip("How many buffer starvation events can happen before a one-shot warning is logged for this object.")]
    [Min(1)] public int bufferStarvationWarningThreshold = 5;

    [Tooltip("Maximum time in milliseconds that a proxy may gently extrapolate from its newest snapshot before holding position.")]
    [Min(0f)] public float maxExtrapolationMs = 40f;

    [Tooltip("Distance from the last rendered proxy position that is treated as a teleport or severe discontinuity and snapped immediately.")]
    [Min(0f)] public float hardSnapDistance = 30f;

    [Tooltip("Optional catch-up smoothing rate after interpolation. Set to 0 to apply the sampled server timeline exactly.")]
    [Min(0f)] public float smoothingCatchUpRate = 0f;
}

public struct NetInterpolationDiagnostics3D
{
    public int ReceivedSnapshots;
    public int OutOfOrderSnapshots;
    public int SamplesRendered;
    public int BufferStarvationEvents;
    public int ExtrapolatedFrames;
    public int HardSnaps;
    public int CurrentBufferDepth;
    public int OldestSnapshotTick;
    public int NewestSnapshotTick;
    public int RenderTick;
    public float CurrentDelayMs;
}

public sealed class NetSnapshotInterpolator3D
{
    private const float DelayRecoveryPerSecond = 20f;

    private NetStateSnapshot3D[] _buffer;
    private bool[] _hasSnapshot;
    private int _bufferSize;
    private int _newestTick = -1;
    private int _oldestTick = -1;
    private int _lastReceivedTick = -1;
    private float _currentDelayMs;
    private bool _hasLastRendered;
    private MovementState3D _lastRenderedState;
    private bool _loggedStarvationWarning;
    private bool _loggedOutOfOrderWarning;
    private NetInterpolationDiagnostics3D _diagnostics;

    public NetInterpolationDiagnostics3D Diagnostics => _diagnostics;

    public void Initialize(int bufferSize, NetInterpolationSettings3D settings)
    {
        _bufferSize = Mathf.Max(4, bufferSize);
        _buffer = new NetStateSnapshot3D[_bufferSize];
        _hasSnapshot = new bool[_bufferSize];
        _currentDelayMs = Mathf.Max(0f, settings != null ? settings.minVisualDelayMs : 80f);
        Reset();
    }

    public void Reset()
    {
        if (_hasSnapshot != null)
        {
            for (int i = 0; i < _hasSnapshot.Length; i++)
            {
                _hasSnapshot[i] = false;
            }
        }

        _newestTick = -1;
        _oldestTick = -1;
        _lastReceivedTick = -1;
        _hasLastRendered = false;
        _loggedStarvationWarning = false;
        _loggedOutOfOrderWarning = false;
        _diagnostics.CurrentBufferDepth = 0;
        _diagnostics.OldestSnapshotTick = -1;
        _diagnostics.NewestSnapshotTick = -1;
        _diagnostics.RenderTick = -1;
    }

    public void AddSnapshot(NetStateSnapshot3D snapshot, Object logContext, string label)
    {
        if (_buffer == null || _buffer.Length == 0)
        {
            return;
        }

        if (snapshot.Tick <= _lastReceivedTick)
        {
            _diagnostics.OutOfOrderSnapshots++;
            if (!_loggedOutOfOrderWarning)
            {
                Debug.LogWarning($"[{label}] Received out-of-order snapshot tick {snapshot.Tick} after tick {_lastReceivedTick}. The buffered timeline will keep it if it is still inside the ring.", logContext);
                _loggedOutOfOrderWarning = true;
            }
        }

        if (_oldestTick >= 0 && snapshot.Tick < _oldestTick)
        {
            return;
        }

        _lastReceivedTick = Mathf.Max(_lastReceivedTick, snapshot.Tick);
        int index = PositiveModulo(snapshot.Tick, _buffer.Length);
        bool wasEmpty = !_hasSnapshot[index] || _buffer[index].Tick != snapshot.Tick;
        _buffer[index] = snapshot;
        _hasSnapshot[index] = true;

        if (_newestTick < 0 || snapshot.Tick > _newestTick)
        {
            _newestTick = snapshot.Tick;
            _oldestTick = Mathf.Max(0, _newestTick - _buffer.Length + 1);
            TrimSnapshotsOlderThan(_oldestTick);
        }
        else if (_oldestTick < 0)
        {
            _oldestTick = snapshot.Tick;
        }

        _diagnostics.ReceivedSnapshots++;
        if (wasEmpty)
        {
            _diagnostics.CurrentBufferDepth = CountBufferedSnapshots();
        }

        _diagnostics.OldestSnapshotTick = _oldestTick;
        _diagnostics.NewestSnapshotTick = _newestTick;
    }

    public bool TrySample(
        NetInterpolationSettings3D settings,
        float tickInterval,
        int serverTick,
        float deltaTime,
        Object logContext,
        string label,
        out MovementState3D sampledState,
        out float sampledThrust,
        out bool sampledFriction)
    {
        sampledState = default;
        sampledThrust = 0f;
        sampledFriction = false;

        if (_buffer == null || _buffer.Length == 0 || _newestTick < 0 || tickInterval <= 0f)
        {
            return false;
        }

        NetInterpolationSettings3D safeSettings = settings ?? new NetInterpolationSettings3D();
        float minDelayMs = Mathf.Max(0f, safeSettings.minVisualDelayMs);
        float maxDelayMs = Mathf.Max(minDelayMs, safeSettings.maxVisualDelayMs);
        _currentDelayMs = Mathf.Clamp(_currentDelayMs, minDelayMs, maxDelayMs);

        int resolvedServerTick = serverTick >= 0 ? serverTick : _newestTick;
        float renderTickFloat = resolvedServerTick - (_currentDelayMs / 1000f / tickInterval);
        int renderTick = Mathf.FloorToInt(renderTickFloat);
        _diagnostics.RenderTick = renderTick;
        _diagnostics.CurrentDelayMs = _currentDelayMs;
        _diagnostics.CurrentBufferDepth = CountBufferedSnapshots();

        bool hasFrom = TryFindAtOrBefore(renderTick, out NetStateSnapshot3D from);
        bool hasTo = TryFindAtOrAfter(renderTick + 1, out NetStateSnapshot3D to);

        if (hasFrom && hasTo)
        {
            float denominator = Mathf.Max(1, to.Tick - from.Tick);
            float t = Mathf.Clamp01((renderTickFloat - from.Tick) / denominator);
            sampledState = Interpolate(from, to, t);
            sampledThrust = Mathf.Lerp(from.ThrustInput, to.ThrustInput, t);
            sampledFriction = t < 0.5f ? from.FrictionEnabled : to.FrictionEnabled;
            _currentDelayMs = Mathf.Max(minDelayMs, _currentDelayMs - DelayRecoveryPerSecond * deltaTime);
            ApplyPostSampleSmoothing(safeSettings, deltaTime, ref sampledState);
            _diagnostics.SamplesRendered++;
            return true;
        }

        if (hasFrom)
        {
            float ticksBeyondNewest = Mathf.Max(0f, renderTickFloat - from.Tick);
            float maxExtrapolationTicks = Mathf.Max(0f, safeSettings.maxExtrapolationMs) / 1000f / tickInterval;
            sampledState = ToMovementState(from);
            sampledThrust = from.ThrustInput;
            sampledFriction = from.FrictionEnabled;

            if (ticksBeyondNewest > 0f && ticksBeyondNewest <= maxExtrapolationTicks)
            {
                sampledState.Position += GetEffectiveVelocity(sampledState) * ticksBeyondNewest * tickInterval;
                _diagnostics.ExtrapolatedFrames++;
            }
            else if (ticksBeyondNewest > maxExtrapolationTicks)
            {
                RegisterStarvation(safeSettings, maxDelayMs, logContext, label);
            }

            ApplyPostSampleSmoothing(safeSettings, deltaTime, ref sampledState);
            _diagnostics.SamplesRendered++;
            return true;
        }

        if (hasTo)
        {
            sampledState = ToMovementState(to);
            sampledThrust = to.ThrustInput;
            sampledFriction = to.FrictionEnabled;
            RegisterUnderfilledBuffer(minDelayMs);
            ApplyPostSampleSmoothing(safeSettings, deltaTime, ref sampledState);
            _diagnostics.SamplesRendered++;
            return true;
        }

        if (renderTick < _oldestTick)
        {
            RegisterUnderfilledBuffer(minDelayMs);
        }
        else
        {
            RegisterStarvation(safeSettings, maxDelayMs, logContext, label);
        }

        return false;
    }

    private void ApplyPostSampleSmoothing(NetInterpolationSettings3D settings, float deltaTime, ref MovementState3D sampledState)
    {
        if (!_hasLastRendered)
        {
            _lastRenderedState = sampledState;
            _hasLastRendered = true;
            return;
        }

        float snapDistance = Mathf.Max(0f, settings.hardSnapDistance);
        if (snapDistance > 0f && Vector3.Distance(_lastRenderedState.Position, sampledState.Position) > snapDistance)
        {
            _diagnostics.HardSnaps++;
            _lastRenderedState = sampledState;
            return;
        }

        float smoothingRate = Mathf.Max(0f, settings.smoothingCatchUpRate);
        if (smoothingRate > 0f && deltaTime > 0f)
        {
            float t = 1f - Mathf.Exp(-smoothingRate * deltaTime);
            sampledState = Interpolate(_lastRenderedState, sampledState, t);
        }

        _lastRenderedState = sampledState;
    }

    private void RegisterStarvation(NetInterpolationSettings3D settings, float maxDelayMs, Object logContext, string label)
    {
        _diagnostics.BufferStarvationEvents++;
        _currentDelayMs = Mathf.Min(maxDelayMs, _currentDelayMs + Mathf.Max(1f, settings.minVisualDelayMs * 0.25f));

        if (!_loggedStarvationWarning && _diagnostics.BufferStarvationEvents >= Mathf.Max(1, settings.bufferStarvationWarningThreshold))
        {
            Debug.LogWarning($"[{label}] Interpolation buffer starved {_diagnostics.BufferStarvationEvents} times. Consider raising visual delay or inspecting packet cadence. newestTick={_newestTick}, renderTick={_diagnostics.RenderTick}, bufferDepth={_diagnostics.CurrentBufferDepth}", logContext);
            _loggedStarvationWarning = true;
        }
    }

    private void RegisterUnderfilledBuffer(float minDelayMs)
    {
        _diagnostics.BufferStarvationEvents++;
        _currentDelayMs = Mathf.Max(minDelayMs, _currentDelayMs - Mathf.Max(1f, minDelayMs * 0.25f));
    }

    private bool TryFindAtOrBefore(int tick, out NetStateSnapshot3D snapshot)
    {
        int oldest = _oldestTick < 0 ? 0 : _oldestTick;
        for (int candidateTick = Mathf.Min(tick, _newestTick); candidateTick >= oldest; candidateTick--)
        {
            if (TryGetSnapshot(candidateTick, out snapshot))
            {
                return true;
            }
        }

        snapshot = default;
        return false;
    }

    private bool TryFindAtOrAfter(int tick, out NetStateSnapshot3D snapshot)
    {
        int newest = _newestTick;
        for (int candidateTick = Mathf.Max(tick, _oldestTick); candidateTick <= newest; candidateTick++)
        {
            if (TryGetSnapshot(candidateTick, out snapshot))
            {
                return true;
            }
        }

        snapshot = default;
        return false;
    }

    private bool TryGetSnapshot(int tick, out NetStateSnapshot3D snapshot)
    {
        if (_buffer == null || _buffer.Length == 0 || tick < 0)
        {
            snapshot = default;
            return false;
        }

        int index = PositiveModulo(tick, _buffer.Length);
        if (!_hasSnapshot[index] || _buffer[index].Tick != tick)
        {
            snapshot = default;
            return false;
        }

        snapshot = _buffer[index];
        return true;
    }

    private void TrimSnapshotsOlderThan(int oldestAllowedTick)
    {
        if (_buffer == null)
        {
            return;
        }

        for (int i = 0; i < _buffer.Length; i++)
        {
            if (_hasSnapshot[i] && _buffer[i].Tick < oldestAllowedTick)
            {
                _hasSnapshot[i] = false;
            }
        }
    }

    private int CountBufferedSnapshots()
    {
        if (_hasSnapshot == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < _hasSnapshot.Length; i++)
        {
            if (_hasSnapshot[i])
            {
                count++;
            }
        }

        return count;
    }

    private static MovementState3D Interpolate(NetStateSnapshot3D from, NetStateSnapshot3D to, float t)
    {
        return new MovementState3D
        {
            Position = Vector3.Lerp(from.Position, to.Position, t),
            Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
            Velocity = Vector3.Lerp(from.Velocity, to.Velocity, t),
            FilteredLookInput = Vector2.Lerp(from.FilteredLookInput, to.FilteredLookInput, t),
            TurnRates = Vector2.Lerp(from.TurnRates, to.TurnRates, t),
            DodgeVelocity = Vector3.Lerp(from.DodgeVelocity, to.DodgeVelocity, t),
            DodgeExitVelocity = Vector3.Lerp(from.DodgeExitVelocity, to.DodgeExitVelocity, t),
            DodgeRemainingTime = Mathf.Lerp(from.DodgeRemainingTime, to.DodgeRemainingTime, t),
            DodgeDuration = Mathf.Lerp(from.DodgeDuration, to.DodgeDuration, t)
        };
    }

    private static MovementState3D Interpolate(in MovementState3D from, in MovementState3D to, float t)
    {
        return new MovementState3D
        {
            Position = Vector3.Lerp(from.Position, to.Position, t),
            Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
            Velocity = Vector3.Lerp(from.Velocity, to.Velocity, t),
            FilteredLookInput = Vector2.Lerp(from.FilteredLookInput, to.FilteredLookInput, t),
            TurnRates = Vector2.Lerp(from.TurnRates, to.TurnRates, t),
            DodgeVelocity = Vector3.Lerp(from.DodgeVelocity, to.DodgeVelocity, t),
            DodgeExitVelocity = Vector3.Lerp(from.DodgeExitVelocity, to.DodgeExitVelocity, t),
            DodgeRemainingTime = Mathf.Lerp(from.DodgeRemainingTime, to.DodgeRemainingTime, t),
            DodgeDuration = Mathf.Lerp(from.DodgeDuration, to.DodgeDuration, t)
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
            DodgeDuration = snapshot.DodgeDuration
        };
    }

    private static Vector3 GetEffectiveVelocity(in MovementState3D state)
    {
        return state.DodgeRemainingTime > 0.001f
            ? state.Velocity + MovementSimulation3D.GetCurrentDodgeVelocity(state)
            : state.Velocity;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
