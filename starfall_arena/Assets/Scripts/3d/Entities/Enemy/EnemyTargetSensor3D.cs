using UnityEngine;

[DisallowMultipleComponent]
public class EnemyTargetSensor3D : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private Faction3D targetFaction = Faction3D.PlayerTeam;
    [SerializeField] private float detectionRange = 800f;
    [SerializeField] private float refreshInterval = 0.15f;

    private Entity3D _currentTarget;
    private Entity3D _alertedTarget;
    private float _alertedTargetExpiresAt;
    private float _nextRefreshTime;

    public Entity3D CurrentTarget => ResolveCurrentTarget();

    private void OnDisable()
    {
        _currentTarget = null;
        _alertedTarget = null;
        _alertedTargetExpiresAt = 0f;
    }

    public void ApplyProfile(EnemyBalanceProfile3D.CoreStats core)
    {
        detectionRange = Mathf.Max(0f, core.detectionRange);
        _currentTarget = null;
        _alertedTarget = null;
        _alertedTargetExpiresAt = 0f;
        _nextRefreshTime = 0f;
    }

    public void ReceiveTargetAlert(Entity3D target, float duration)
    {
        if (duration <= 0f || !IsCandidateTarget(target))
        {
            return;
        }

        _alertedTarget = target;
        _alertedTargetExpiresAt = Mathf.Max(_alertedTargetExpiresAt, Time.time + duration);
    }

    public Entity3D RefreshTargetNow()
    {
        _nextRefreshTime = Time.time + Mathf.Max(0.02f, refreshInterval);
        _currentTarget = FindNearestTarget();
        return ResolveCurrentTarget();
    }

    public Entity3D GetTarget()
    {
        bool hasValidNormalTarget = IsStillValid(_currentTarget);
        bool hasValidAlertTarget = IsAlertStillValid();
        if (Time.time >= _nextRefreshTime || (!hasValidNormalTarget && !hasValidAlertTarget))
        {
            return RefreshTargetNow();
        }

        return hasValidNormalTarget ? _currentTarget : _alertedTarget;
    }

    private Entity3D ResolveCurrentTarget()
    {
        if (IsStillValid(_currentTarget))
        {
            return _currentTarget;
        }

        if (IsAlertStillValid())
        {
            return _alertedTarget;
        }

        _alertedTarget = null;
        _alertedTargetExpiresAt = 0f;
        return null;
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

    private bool IsAlertStillValid()
    {
        return Time.time < _alertedTargetExpiresAt && IsCandidateTarget(_alertedTarget);
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
