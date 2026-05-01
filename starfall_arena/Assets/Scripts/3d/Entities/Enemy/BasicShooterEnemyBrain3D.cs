using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class BasicShooterEnemyBrain3D : MonoBehaviour
{
    [Header("Basic Shooter")]
    [Tooltip("Enemy-only direct-fire projectile weapon. The brain gates firing by nose tolerance, then supplies the target direction so long-range shots are aimed at the player instead of inheriting the remaining tolerance error.")]
    [SerializeField] private ProjectileWeaponEnemy3D primaryWeapon;

    [Tooltip("Optional generic charge driver for this enemy's projectile or missile weapon. When assigned, the brain starts this telegraphed windup instead of firing Primary Weapon immediately.")]
    [SerializeField] private EnemyProjectileChargeAttack3D chargeAttack;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Network combat helper for replicated enemy projectile fire. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Tooltip("Max angle (degrees) between the enemy's forward and the target direction before it will fire.")]
    [SerializeField] private float aimToleranceDegrees = 10f;

    [Tooltip("Distance (meters) at which the enemy stops advancing and holds position to fire.")]
    [SerializeField] private float stopDistance = 18f;

    [Tooltip("Distance (meters) beyond which the enemy moves at full speed toward the target.")]
    [SerializeField] private float fullSpeedDistance = 45f;

    [Tooltip("If true, route steering through the obstacle avoidance component when one is assigned.")]
    [SerializeField] private bool useObstacleAvoidance;

    private NetworkObject _networkObject;
    private float _nextThinkTime;

    private void Awake()
    {
        primaryWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        chargeAttack ??= GetComponent<EnemyProjectileChargeAttack3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.BasicShooterBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        aimToleranceDegrees = Mathf.Clamp(stats.aimToleranceDegrees, 0f, 180f);
        stopDistance = Mathf.Max(0f, stats.stopDistance);
        fullSpeedDistance = Mathf.Max(stopDistance + 0.01f, stats.fullSpeedDistance);
    }

    private void OnDisable()
    {
        chargeAttack?.CancelCharge(immediate: true);
        flightController?.ClearFlightIntent();
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
            chargeAttack?.CancelCharge();
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            chargeAttack?.CancelCharge();
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(toTarget)
            : toTarget.normalized;

        flightController?.SetMoveDirection(steeringDirection, ResolveDistanceSpeedScale(toTarget.magnitude));

        if (!IsAimedAtTarget(toTarget))
        {
            return;
        }

        if (chargeAttack != null)
        {
            if (!chargeAttack.IsCharging)
            {
                chargeAttack.TryBeginCharge(Faction3D.PlayerTeam, toTarget.normalized, target);
            }

            return;
        }

        if (primaryWeapon == null)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.TryFireProjectilePattern(primaryWeapon, Faction3D.PlayerTeam, toTarget.normalized, target);
            return;
        }

        if (primaryWeapon.TryFireAtFaction(Faction3D.PlayerTeam, toTarget.normalized))
        {
            attackReporter?.ReportAttack(target);
        }
    }

    private bool IsAimedAtTarget(Vector3 toTarget)
    {
        return Vector3.Angle(transform.forward, toTarget.normalized) <= Mathf.Max(0f, aimToleranceDegrees);
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private float ResolveDistanceSpeedScale(float distanceToTarget)
    {
        float stop = Mathf.Max(0f, stopDistance);
        float full = Mathf.Max(stop + 0.01f, fullSpeedDistance);

        if (distanceToTarget <= stop)
        {
            return 0f;
        }

        if (distanceToTarget >= full)
        {
            return 1f;
        }

        return Mathf.InverseLerp(stop, full, distanceToTarget);
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
}
