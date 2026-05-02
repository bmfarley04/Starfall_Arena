using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class EnemyObstacleAvoidance3D : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const float MinProbeRadius = 0.01f;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[12];

    [Header("Obstacle Avoidance")]
    [Tooltip("Physics layers treated as avoidable world geometry. Do not include players, enemies, or projectiles.")]
    [FormerlySerializedAs("obstacleMask")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Body clearance radius used for obstacle spherecasts. Start near the enemy ship's collision width.")]
    [FormerlySerializedAs("probeRadius")]
    [SerializeField] private float probeRadius = 3f;

    [Tooltip("Distance in meters checked along the desired movement direction before avoidance turns on.")]
    [FormerlySerializedAs("lookAheadDistance")]
    [SerializeField] private float forwardLookAheadDistance = 45f;

    [Tooltip("Distance in meters checked along each right/left/up/down escape candidate when forward movement is blocked.")]
    [SerializeField] private float escapeCheckDistance = 30f;

    [Tooltip("How strongly the chosen escape direction bends the original desired movement direction.")]
    [FormerlySerializedAs("avoidanceStrength")]
    [SerializeField] private float avoidanceStrength = 1.25f;

    [Tooltip("Seconds to keep using the chosen escape side after a forward hit so centered obstacles do not cause left/right jitter.")]
    [SerializeField] private float escapeDirectionHoldTime = 0.35f;

    private Vector3 _heldEscapeDirection;
    private float _heldEscapeUntilTime;

    public Vector3 ResolveSteeringDirection(Vector3 desiredDirection)
    {
        Vector3 desired = desiredDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? desiredDirection.normalized
            : transform.forward;

        if (desired.sqrMagnitude <= MinDirectionSqrMagnitude || obstacleLayers.value == 0)
        {
            ClearHeldEscape();
            return desired.sqrMagnitude > MinDirectionSqrMagnitude ? desired.normalized : Vector3.forward;
        }

        if (!IsBlocked(desired, forwardLookAheadDistance))
        {
            ClearHeldEscape();
            return desired;
        }

        Vector3 escapeDirection = ResolveEscapeDirection(desired);
        Vector3 steered = desired + escapeDirection * avoidanceStrength;
        return steered.sqrMagnitude > MinDirectionSqrMagnitude ? steered.normalized : desired;
    }

    private void OnValidate()
    {
        probeRadius = Mathf.Max(MinProbeRadius, probeRadius);
        forwardLookAheadDistance = Mathf.Max(0f, forwardLookAheadDistance);
        escapeCheckDistance = Mathf.Max(0f, escapeCheckDistance);
        avoidanceStrength = Mathf.Max(0f, avoidanceStrength);
        escapeDirectionHoldTime = Mathf.Max(0f, escapeDirectionHoldTime);
    }

    private Vector3 ResolveEscapeDirection(Vector3 desired)
    {
        if (HasHeldEscape())
        {
            return _heldEscapeDirection;
        }

        BuildEscapeBasis(desired, out Vector3 right, out Vector3 up);

        Vector3 bestDirection = right;
        float bestClearance = GetClearance(right, escapeCheckDistance);
        TestEscapeCandidate(-right, ref bestDirection, ref bestClearance);
        TestEscapeCandidate(up, ref bestDirection, ref bestClearance);
        TestEscapeCandidate(-up, ref bestDirection, ref bestClearance);

        HoldEscape(bestDirection);
        return bestDirection;
    }

    private void TestEscapeCandidate(Vector3 candidate, ref Vector3 bestDirection, ref float bestClearance)
    {
        float clearance = GetClearance(candidate, escapeCheckDistance);
        if (clearance > bestClearance)
        {
            bestDirection = candidate;
            bestClearance = clearance;
        }
    }

    private void BuildEscapeBasis(Vector3 desired, out Vector3 right, out Vector3 up)
    {
        Vector3 upReference = Mathf.Abs(Vector3.Dot(desired, Vector3.up)) < 0.95f
            ? Vector3.up
            : transform.up;

        right = Vector3.Cross(upReference, desired);
        if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            right = transform.right.sqrMagnitude > MinDirectionSqrMagnitude ? transform.right : Vector3.right;
        }

        right.Normalize();

        up = Vector3.Cross(desired, right);
        if (up.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            up = transform.up.sqrMagnitude > MinDirectionSqrMagnitude ? transform.up : Vector3.up;
        }

        up.Normalize();
    }

    private bool IsBlocked(Vector3 direction, float distance)
    {
        return TryGetNearestHitDistance(direction, distance, out _);
    }

    private float GetClearance(Vector3 direction, float distance)
    {
        if (distance <= 0f)
        {
            return 0f;
        }

        return TryGetNearestHitDistance(direction, distance, out float nearestHitDistance)
            ? nearestHitDistance
            : distance;
    }

    private bool TryGetNearestHitDistance(Vector3 direction, float distance, out float nearestHitDistance)
    {
        nearestHitDistance = distance;
        if (distance <= 0f || direction.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            Mathf.Max(MinProbeRadius, probeRadius),
            direction.normalized,
            HitBuffer,
            distance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);

        bool foundHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            nearestHitDistance = Mathf.Min(nearestHitDistance, hit.distance);
            foundHit = true;
        }

        return foundHit;
    }

    private bool HasHeldEscape()
    {
        return _heldEscapeDirection.sqrMagnitude > MinDirectionSqrMagnitude
            && Time.time < _heldEscapeUntilTime;
    }

    private void HoldEscape(Vector3 escapeDirection)
    {
        _heldEscapeDirection = escapeDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? escapeDirection.normalized
            : Vector3.zero;
        _heldEscapeUntilTime = Time.time + escapeDirectionHoldTime;
    }

    private void ClearHeldEscape()
    {
        _heldEscapeDirection = Vector3.zero;
        _heldEscapeUntilTime = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 desired = transform.forward.sqrMagnitude > MinDirectionSqrMagnitude
            ? transform.forward.normalized
            : Vector3.forward;

        Gizmos.color = Color.yellow;
        DrawProbeGizmo(desired, forwardLookAheadDistance);

        BuildEscapeBasis(desired, out Vector3 right, out Vector3 up);
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 1f);
        DrawProbeGizmo(right, escapeCheckDistance);
        DrawProbeGizmo(-right, escapeCheckDistance);
        DrawProbeGizmo(up, escapeCheckDistance);
        DrawProbeGizmo(-up, escapeCheckDistance);
    }

    private void DrawProbeGizmo(Vector3 direction, float distance)
    {
        if (distance <= 0f || direction.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            return;
        }

        Vector3 normalizedDirection = direction.normalized;
        Gizmos.DrawRay(transform.position, normalizedDirection * distance);
        Gizmos.DrawWireSphere(transform.position + normalizedDirection * distance, Mathf.Max(MinProbeRadius, probeRadius));
    }
}
