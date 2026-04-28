using UnityEngine;

[DisallowMultipleComponent]
public class EnemyTargetSensor3D : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private Faction3D targetFaction = Faction3D.PlayerTeam;
    [SerializeField] private float detectionRange = 800f;
    [SerializeField] private float refreshInterval = 0.15f;

    private Entity3D _currentTarget;
    private float _nextRefreshTime;

    public Entity3D CurrentTarget => _currentTarget;

    private void OnDisable()
    {
        _currentTarget = null;
    }

    public void ApplyProfile(EnemyBalanceProfile3D.CoreStats core)
    {
        detectionRange = Mathf.Max(0f, core.detectionRange);
        _currentTarget = null;
        _nextRefreshTime = 0f;
    }

    public Entity3D RefreshTargetNow()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);
        _currentTarget = FindNearestTarget();
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

    private Entity3D FindNearestTarget()
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
        return toTarget.sqrMagnitude <= detectionRange * detectionRange;
    }

    private bool IsCandidateTarget(Entity3D candidate)
    {
        return candidate != null
            && candidate.CurrentHealth > 0f
            && candidate.gameObject.activeInHierarchy
            && !candidate.transform.IsChildOf(transform)
            && FactionMember3D.ResolveFaction(candidate) == targetFaction;
    }
}
