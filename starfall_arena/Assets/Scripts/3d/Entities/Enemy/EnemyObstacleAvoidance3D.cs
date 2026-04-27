using UnityEngine;

[DisallowMultipleComponent]
public class EnemyObstacleAvoidance3D : MonoBehaviour
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[12];

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float lookAheadDistance = 45f;
    [SerializeField] private float probeRadius = 3f;
    [SerializeField] private float avoidanceStrength = 1.5f;
    [Range(0f, 75f)]
    [SerializeField] private float whiskerAngle = 28f;

    public Vector3 ResolveSteeringDirection(Vector3 desiredDirection)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f || obstacleMask.value == 0)
        {
            return desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : transform.forward;
        }

        Vector3 desired = desiredDirection.normalized;
        Vector3 avoidance = Vector3.zero;
        avoidance += Probe(desired, 1f);
        avoidance += Probe(Quaternion.AngleAxis(whiskerAngle, transform.up) * desired, 0.65f);
        avoidance += Probe(Quaternion.AngleAxis(-whiskerAngle, transform.up) * desired, 0.65f);
        avoidance += Probe(Quaternion.AngleAxis(whiskerAngle, transform.right) * desired, 0.5f);
        avoidance += Probe(Quaternion.AngleAxis(-whiskerAngle, transform.right) * desired, 0.5f);

        if (avoidance.sqrMagnitude <= 0.0001f)
        {
            return desired;
        }

        Vector3 steered = desired + avoidance.normalized * Mathf.Max(0f, avoidanceStrength);
        return steered.sqrMagnitude > 0.0001f ? steered.normalized : desired;
    }

    private Vector3 Probe(Vector3 direction, float weight)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            Mathf.Max(0.01f, probeRadius),
            direction.normalized,
            HitBuffer,
            Mathf.Max(0.01f, lookAheadDistance),
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        Vector3 avoidance = Vector3.zero;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            float distanceRatio = lookAheadDistance > 0f ? 1f - Mathf.Clamp01(hit.distance / lookAheadDistance) : 1f;
            Vector3 awayFromHit = transform.position - hit.point;
            if (awayFromHit.sqrMagnitude <= 0.0001f)
            {
                awayFromHit = hit.normal;
            }

            avoidance += (hit.normal + awayFromHit.normalized) * distanceRatio * weight;
        }

        return avoidance;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * lookAheadDistance);
        Gizmos.DrawWireSphere(transform.position + transform.forward * lookAheadDistance, probeRadius);
    }
}
