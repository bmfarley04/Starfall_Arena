using UnityEngine;

[DisallowMultipleComponent]
public class EnemyTargetSensor3D : MonoBehaviour
{
    private static readonly RaycastHit[] LineOfSightHits = new RaycastHit[16];

    [Header("Targeting")]
    [SerializeField] private Faction3D targetFaction = Faction3D.PlayerTeam;
    [SerializeField] private float detectionRange = 800f;
    [SerializeField] private float refreshInterval = 0.15f;

    [Header("Line Of Sight")]
    [Tooltip("Layers that block enemy sight, such as asteroids and world geometry. Leave empty to ignore sight blocking.")]
    [SerializeField] private LayerMask lineOfSightBlockers;
    [SerializeField] private float lineOfSightRadius = 0.5f;

    private Entity3D _currentTarget;
    private float _nextRefreshTime;

    public Entity3D CurrentTarget => _currentTarget;

    private void OnDisable()
    {
        _currentTarget = null;
    }

    public Entity3D RefreshTargetNow()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);
        _currentTarget = FindNearestVisibleTarget();
        return _currentTarget;
    }

    public Entity3D GetTarget()
    {
        if (Time.time >= _nextRefreshTime || !IsStillValid(_currentTarget))
        {
            return RefreshTargetNow();
        }

        return _currentTarget;
    }

    private Entity3D FindNearestVisibleTarget()
    {
        Entity3D[] entities = FindObjectsByType<Entity3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Entity3D nearest = null;
        float nearestDistanceSqr = detectionRange * detectionRange;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D candidate = entities[i];
            if (!IsCandidateTarget(candidate))
            {
                continue;
            }

            Vector3 toCandidate = candidate.transform.position - transform.position;
            float distanceSqr = toCandidate.sqrMagnitude;
            if (distanceSqr > nearestDistanceSqr)
            {
                continue;
            }

            if (!HasLineOfSight(candidate, toCandidate))
            {
                continue;
            }

            nearest = candidate;
            nearestDistanceSqr = distanceSqr;
        }

        return nearest;
    }

    private bool IsStillValid(Entity3D target)
    {
        if (!IsCandidateTarget(target))
        {
            return false;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        return toTarget.sqrMagnitude <= detectionRange * detectionRange && HasLineOfSight(target, toTarget);
    }

    private bool IsCandidateTarget(Entity3D candidate)
    {
        return candidate != null
            && candidate.CurrentHealth > 0f
            && candidate.gameObject.activeInHierarchy
            && !candidate.transform.IsChildOf(transform)
            && FactionMember3D.ResolveFaction(candidate) == targetFaction;
    }

    private bool HasLineOfSight(Entity3D candidate, Vector3 toCandidate)
    {
        if (lineOfSightBlockers.value == 0)
        {
            return true;
        }

        float distance = toCandidate.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        Vector3 direction = toCandidate / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            Mathf.Max(0f, lineOfSightRadius),
            direction,
            LineOfSightHits,
            distance,
            lineOfSightBlockers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = LineOfSightHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(candidate.transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
