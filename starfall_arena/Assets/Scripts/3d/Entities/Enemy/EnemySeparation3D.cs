using UnityEngine;

/// <summary>
/// Lightweight inter-agent separation steering for enemies.
/// Mirrors the API of <see cref="EnemyObstacleAvoidance3D"/>: enemy brains call
/// <see cref="ResolveSteeringDirection"/> with their desired pursuit/disengage direction
/// and get back a direction biased away from nearby allies (and optionally away from
/// the player at very close range so a swarm fans out on approach).
///
/// This does NOT replace obstacle avoidance - typical wiring is:
///     desired -> separation.ResolveSteeringDirection(...)
///             -> obstacleAvoidance.ResolveSteeringDirection(...)
///             -> flightController.SetMoveDirection(...)
///
/// Cheap: one OverlapSphereNonAlloc per call into a static buffer, no allocations per
/// frame, intended to run at the brain's think tick (default 0.05s).
/// </summary>
[DisallowMultipleComponent]
public class EnemySeparation3D : MonoBehaviour
{
    private static readonly Collider[] OverlapBuffer = new Collider[16];

    [Header("Detection")]
    [Tooltip("Layers that contain ship/agent colliders (player + enemy ships). Leave empty to make this component a no-op. Performance note: keep this mask narrow - the OverlapSphere only checks these layers.")]
    [SerializeField] private LayerMask agentMask;

    [Header("Allies")]
    [Tooltip("Radius (meters) within which same-faction allies repel this agent. Tune to roughly 2-3x ship width so neighbors push apart before their colliders touch.")]
    [SerializeField] private float allyRadius = 6f;

    [Tooltip("How strongly nearby allies push this agent away. 1.0 is a reasonable default; raise for tighter formations falling apart, lower if rammers feel reluctant to commit on approach.")]
    [SerializeField] private float allyRepulsionStrength = 1f;

    [Header("Player Proximity")]
    [Tooltip("Radius (meters) within which non-ally entities (typically the player) contribute a small lateral bias so a converging swarm fans out instead of clipping in from the same vector. Set to 0 to disable. Should be smaller than allyRadius - this is meant for the final approach only.")]
    [SerializeField] private float playerProximityRadius = 4f;

    [Tooltip("How strongly the player (non-ally entity) within playerProximityRadius pushes this agent sideways. Kept gentler than ally repulsion so it doesn't override pursuit / impact mechanics - this is steering bias only, not collision avoidance.")]
    [SerializeField] private float playerRepulsionStrength = 0.5f;

    [Header("Blend")]
    [Tooltip("How strongly the summed separation vector bends the desired direction. 1.0 mirrors EnemyObstacleAvoidance3D defaults. Higher values turn more sharply away from neighbors.")]
    [SerializeField] private float blendStrength = 1f;

    private Entity3D _selfEntity;
    private Faction3D _selfFaction = Faction3D.Neutral;
    private bool _selfFactionResolved;

    private void Awake()
    {
        _selfEntity = GetComponentInParent<Entity3D>();
        ResolveSelfFaction();
    }

    public Vector3 ResolveSteeringDirection(Vector3 desiredDirection)
    {
        bool hasDesired = desiredDirection.sqrMagnitude > 0.0001f;
        Vector3 desired = hasDesired ? desiredDirection.normalized : transform.forward;

        if (agentMask.value == 0)
        {
            return desired;
        }

        if (!_selfFactionResolved)
        {
            ResolveSelfFaction();
        }

        float queryRadius = Mathf.Max(allyRadius, playerProximityRadius);
        if (queryRadius <= 0.01f)
        {
            return desired;
        }

        Vector3 selfPosition = transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(
            selfPosition,
            queryRadius,
            OverlapBuffer,
            agentMask,
            QueryTriggerInteraction.Ignore);

        Vector3 separation = Vector3.zero;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = OverlapBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Entity3D candidate = hit.GetComponentInParent<Entity3D>();
            if (candidate == null || candidate == _selfEntity || candidate.CurrentHealth <= 0f)
            {
                continue;
            }

            Vector3 offset = selfPosition - candidate.transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.0001f)
            {
                // Co-located fallback: push along this agent's right vector so the pair separates deterministically.
                separation += transform.right * allyRepulsionStrength;
                continue;
            }

            Vector3 awayDirection = offset / distance;
            Faction3D candidateFaction = FactionMember3D.ResolveFaction(candidate);
            bool isAlly = _selfFaction != Faction3D.Neutral && candidateFaction == _selfFaction;

            if (isAlly)
            {
                if (distance > allyRadius)
                {
                    continue;
                }

                float falloff = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, allyRadius));
                separation += awayDirection * (falloff * allyRepulsionStrength);
            }
            else
            {
                if (playerProximityRadius <= 0.01f || distance > playerProximityRadius)
                {
                    continue;
                }

                // Bias sideways relative to the desired direction so we don't just shove away from
                // the player (that would defeat the whole point of pursuing them). Pick the side
                // that the agent is already favoring.
                Vector3 lateral = Vector3.Cross(desired, awayDirection);
                if (lateral.sqrMagnitude <= 0.0001f)
                {
                    lateral = transform.right;
                }
                else
                {
                    lateral.Normalize();
                    // Prefer the side the agent is already drifting toward to avoid jittering between sides.
                    if (Vector3.Dot(lateral, transform.right) < 0f)
                    {
                        lateral = -lateral;
                    }
                }

                float falloff = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, playerProximityRadius));
                separation += lateral * (falloff * playerRepulsionStrength);
            }
        }

        if (separation.sqrMagnitude <= 0.0001f)
        {
            return desired;
        }

        Vector3 blended = desired + separation.normalized * Mathf.Max(0f, blendStrength);
        return blended.sqrMagnitude > 0.0001f ? blended.normalized : desired;
    }

    private void ResolveSelfFaction()
    {
        if (_selfEntity == null)
        {
            _selfEntity = GetComponentInParent<Entity3D>();
        }

        _selfFaction = FactionMember3D.ResolveFaction(_selfEntity);
        _selfFactionResolved = _selfEntity != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f); // green = ally radius
        Gizmos.DrawWireSphere(transform.position, allyRadius);

        if (playerProximityRadius > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.9f, 1f); // magenta = player proximity
            Gizmos.DrawWireSphere(transform.position, playerProximityRadius);
        }
    }
}
