using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class RammerEnemyBrain3D : MonoBehaviour
{
    [Header("Rammer Enemy")]
    [Tooltip("The Enemy3D this brain belongs to. Auto-assigned from this GameObject if left empty. Used as the attacker reference when applying ram damage.")]
    [SerializeField] private Enemy3D enemy;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Ram Impact")]
    [Tooltip("Distance (meters) at which a player-team entity in front of/around the rammer counts as a contact and triggers a ram hit. Mirrors the suicide drone's contact distance pattern.")]
    [SerializeField] private float ramDetectionDistance = 2.5f;

    [Tooltip("Damage applied to the player on a successful ram hit. The knockback is the threat - keep this small.")]
    [SerializeField] private float ramDamage = 15f;

    [Tooltip("Velocity (m/s) added to the player's existing motion in the away-from-rammer direction on hit. Routed through NetMovement3D.ApplyCombatVelocityDelta so the impulse replicates correctly across the network.")]
    [SerializeField] private float knockbackVelocity = 25f;

    [Tooltip("Optional small upward component (m/s) added on top of the away-direction knockback to give the hit a vertical jolt feel. Default 0 - 3D space combat reads weird with arbitrary up impulses.")]
    [SerializeField] private float knockbackUpwardBias = 0f;

    [Header("Disengage")]
    [Tooltip("Seconds the rammer steers away from the target after a successful ram hit before re-engaging. Lets it visibly arc out and turn around for another pass instead of grinding on the player's hull.")]
    [SerializeField] private float disengageDuration = 1.25f;

    [Tooltip("Distance (meters) the rammer must reach during disengage before it is allowed to re-engage early. Whichever happens first - this distance or disengageDuration - ends the disengage state.")]
    [SerializeField] private float disengageDistance = 30f;

    [Tooltip("If true, route steering through the obstacle avoidance component when one is assigned. If false or no avoidance component exists, the rammer steers straight at/away from the target.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    private readonly Collider[] _overlapResults = new Collider[8];

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _disengageEndsAt;
    private bool _isDisengaging;

    private void Awake()
    {
        enemy ??= GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        _isDisengaging = false;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            flightController?.ClearFlightIntent();
            return;
        }

        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
        Think();
    }

    private void Think()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            flightController?.ClearFlightIntent();
            _isDisengaging = false;
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 toTargetDirection = toTarget / distanceToTarget;

        if (_isDisengaging)
        {
            UpdateDisengage(toTargetDirection, distanceToTarget);
            return;
        }

        UpdatePursuit(toTargetDirection, distanceToTarget, target);
    }

    private void UpdatePursuit(Vector3 toTargetDirection, float distanceToTarget, Entity3D target)
    {
        if (distanceToTarget <= Mathf.Max(0.01f, ramDetectionDistance))
        {
            ApplyRamHit(target, toTargetDirection);
            return;
        }

        if (TryAcquireContact(out Entity3D contactEntity))
        {
            Vector3 awayDirection = ResolveAwayDirection(contactEntity);
            ApplyRamHit(contactEntity, -awayDirection);
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(toTargetDirection)
            : toTargetDirection;

        flightController?.SetMoveDirection(steeringDirection, 1f);
    }

    private void UpdateDisengage(Vector3 toTargetDirection, float distanceToTarget)
    {
        if (Time.time >= _disengageEndsAt || distanceToTarget >= Mathf.Max(0f, disengageDistance))
        {
            _isDisengaging = false;
            return;
        }

        Vector3 awayDirection = -toTargetDirection;
        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(awayDirection)
            : awayDirection;

        flightController?.SetMoveDirection(steeringDirection, 1f);
    }

    private bool TryAcquireContact(out Entity3D contactEntity)
    {
        contactEntity = null;
        float radius = Mathf.Max(0.01f, ramDetectionDistance);
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Entity3D candidate = ResolveEntity(_overlapResults[i]);
            if (IsTargetEntity(candidate))
            {
                contactEntity = candidate;
                return true;
            }
        }

        return false;
    }

    private void ApplyRamHit(Entity3D target, Vector3 toTargetDirection)
    {
        if (target == null || enemy == null)
        {
            return;
        }

        Vector3 awayDirection = ResolveAwayDirection(target);
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = toTargetDirection.sqrMagnitude > 0.0001f ? toTargetDirection.normalized : transform.forward;
        }

        Vector3 knockback = awayDirection * Mathf.Max(0f, knockbackVelocity);
        if (knockbackUpwardBias > 0f)
        {
            knockback += Vector3.up * knockbackUpwardBias;
        }

        ApplyKnockbackToTarget(target, knockback);
        target.TakeDamage(ramDamage, transform.position, enemy, DamageSource3D.Direct);

        _isDisengaging = true;
        _disengageEndsAt = Time.time + Mathf.Max(0f, disengageDuration);
    }

    private static void ApplyKnockbackToTarget(Entity3D target, Vector3 knockback)
    {
        if (knockback.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        NetMovement3D netMovement = target.GetComponent<NetMovement3D>();
        if (netMovement != null)
        {
            netMovement.ApplyCombatVelocityDelta(knockback);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity += knockback;
        }
    }

    private Vector3 ResolveAwayDirection(Entity3D target)
    {
        Vector3 away = target.transform.position - transform.position;
        return away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
    }

    private static bool IsTargetEntity(Entity3D candidate)
    {
        return candidate != null
            && candidate.CurrentHealth > 0f
            && FactionMember3D.ResolveFaction(candidate) == Faction3D.PlayerTeam;
    }

    private static Entity3D ResolveEntity(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        return collider.GetComponentInParent<Entity3D>();
    }

    private bool HasBrainAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return !_networkObject.IsSpawned
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, ramDetectionDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, disengageDistance);
    }
}
