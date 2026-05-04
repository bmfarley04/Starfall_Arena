using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class EnemyPatrol3D : MonoBehaviour
{
    private const int MaxWaypointSamples = 12;

    [Header("References")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;
    [SerializeField] private EnemyAIFlightController3D flightController;
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;
    [SerializeField] private EnemySeparation3D separation;

    [Header("Fallback Arena Bounds")]
    [Tooltip("Used when no active ArenaBoundary3D exists. The value is full world-space size, not half extents.")]
    [SerializeField] private Vector3 fallbackArenaSize = new Vector3(5000f, 5000f, 5000f);
    [SerializeField] private Vector3 fallbackArenaCenter = Vector3.zero;

    [Header("Waypoint Sampling")]
    [SerializeField] private float edgeMargin = 80f;
    [SerializeField] private float minLegDistance = 120f;
    [SerializeField] private float arrivalDistance = 30f;
    [SerializeField] private float waypointTimeout = 12f;
    [SerializeField, Range(0f, 1f)] private float forwardBias = 0.35f;

    [Header("Steering")]
    [SerializeField, Range(0f, 1f)] private float patrolSpeedScale = 0.65f;
    [SerializeField] private bool useSeparation = true;
    [SerializeField] private bool useObstacleAvoidance = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private Vector3 _currentWaypoint;
    private Vector3 _lastWaypoint;
    private float _waypointChosenAt;
    private bool _hasWaypoint;
    private int _seed;
    private int _waypointGeneration;

    private void Awake()
    {
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        _seed = unchecked(GetInstanceID() * 73856093);
    }

    private void OnValidate()
    {
        fallbackArenaSize = new Vector3(
            Mathf.Max(1f, fallbackArenaSize.x),
            Mathf.Max(1f, fallbackArenaSize.y),
            Mathf.Max(1f, fallbackArenaSize.z));
        edgeMargin = Mathf.Max(0f, edgeMargin);
        minLegDistance = Mathf.Max(0f, minLegDistance);
        arrivalDistance = Mathf.Max(0.1f, arrivalDistance);
        waypointTimeout = Mathf.Max(0.1f, waypointTimeout);
        patrolSpeedScale = Mathf.Clamp01(patrolSpeedScale);
    }

    private void OnDisable()
    {
        _hasWaypoint = false;
    }

    public bool TryUpdatePatrolIntent()
    {
        if (flightController == null || targetSensor == null)
        {
            return false;
        }

        if (targetSensor.CurrentTarget != null)
        {
            return false;
        }

        Vector3 position = transform.position;
        if (NeedsNewWaypoint(position))
        {
            ChooseNewWaypoint(position);
        }

        Vector3 toWaypoint = _currentWaypoint - position;
        if (toWaypoint.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 steeringDirection = ResolveSteering(toWaypoint.normalized);
        flightController.SetMoveDirection(steeringDirection, patrolSpeedScale);
        return true;
    }

    public void ClearWaypoint()
    {
        _hasWaypoint = false;
    }

    private bool NeedsNewWaypoint(Vector3 position)
    {
        if (!_hasWaypoint)
        {
            return true;
        }

        float arrivalDistanceSqr = arrivalDistance * arrivalDistance;
        if ((_currentWaypoint - position).sqrMagnitude <= arrivalDistanceSqr)
        {
            return true;
        }

        return Time.time - _waypointChosenAt >= waypointTimeout;
    }

    private void ChooseNewWaypoint(Vector3 position)
    {
        Bounds bounds = ResolvePatrolBounds();
        Vector3 best = bounds.ClosestPoint(position);
        float bestScore = float.NegativeInfinity;
        Vector3 forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;

        for (int i = 0; i < MaxWaypointSamples; i++)
        {
            Vector3 candidate = SamplePoint(bounds, i);
            float distance = Vector3.Distance(position, candidate);
            if (distance < minLegDistance && i < MaxWaypointSamples - 1)
            {
                continue;
            }

            float lastDistance = _hasWaypoint ? Vector3.Distance(_lastWaypoint, candidate) : minLegDistance;
            if (_hasWaypoint && lastDistance < minLegDistance * 0.75f && i < MaxWaypointSamples - 1)
            {
                continue;
            }

            Vector3 toCandidate = candidate - position;
            float forwardScore = toCandidate.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01((Vector3.Dot(forward, toCandidate.normalized) + 1f) * 0.5f)
                : 0f;
            float distanceScore = Mathf.Clamp01(distance / Mathf.Max(1f, minLegDistance));
            float score = distanceScore + forwardScore * forwardBias + RandomValue(i + 101) * 0.25f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        _lastWaypoint = _hasWaypoint ? _currentWaypoint : position;
        _currentWaypoint = ClampInsideArena(best);
        _waypointChosenAt = Time.time;
        _hasWaypoint = true;
        _waypointGeneration++;
    }

    private Bounds ResolvePatrolBounds()
    {
        if (ArenaBoundary3D.TryGetActive(out ArenaBoundary3D boundary))
        {
            Bounds activeBounds = boundary.GetCurrentWorldBounds(edgeMargin);
            if (activeBounds.size.sqrMagnitude > 0.0001f)
            {
                return activeBounds;
            }
        }

        Vector3 margin = Vector3.one * Mathf.Max(0f, edgeMargin);
        Vector3 size = new Vector3(
            Mathf.Max(1f, fallbackArenaSize.x - margin.x * 2f),
            Mathf.Max(1f, fallbackArenaSize.y - margin.y * 2f),
            Mathf.Max(1f, fallbackArenaSize.z - margin.z * 2f));
        return new Bounds(fallbackArenaCenter, size);
    }

    private Vector3 SamplePoint(Bounds bounds, int sampleIndex)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new Vector3(
            Mathf.Lerp(min.x, max.x, RandomValue(sampleIndex * 3 + 1)),
            Mathf.Lerp(min.y, max.y, RandomValue(sampleIndex * 3 + 2)),
            Mathf.Lerp(min.z, max.z, RandomValue(sampleIndex * 3 + 3)));
    }

    private Vector3 ClampInsideArena(Vector3 point)
    {
        if (ArenaBoundary3D.TryGetActive(out ArenaBoundary3D boundary))
        {
            return boundary.ClampPositionInside(point, edgeMargin);
        }

        return ResolvePatrolBounds().ClosestPoint(point);
    }

    private Vector3 ResolveSteering(Vector3 desiredDirection)
    {
        Vector3 steeringDirection = desiredDirection;
        if (useSeparation && separation != null && separation.isActiveAndEnabled)
        {
            steeringDirection = separation.ResolveSteeringDirection(steeringDirection);
        }

        if (useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled)
        {
            steeringDirection = obstacleAvoidance.ResolveSteeringDirection(steeringDirection);
        }

        return steeringDirection.sqrMagnitude > 0.0001f ? steeringDirection.normalized : desiredDirection;
    }

    private float RandomValue(int salt)
    {
        unchecked
        {
            uint value = (uint)(_seed + salt * 374761393);
            value += (uint)(_waypointGeneration * 668265263);
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFF) / 16777215f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !_hasWaypoint)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireSphere(_currentWaypoint, arrivalDistance);
        Gizmos.DrawLine(transform.position, _currentWaypoint);
    }
}
