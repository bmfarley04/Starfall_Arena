using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class TankEnemyBrain3D : MonoBehaviour
{
    [Header("Tank Enemy")]
    [Tooltip("Slow heavy direct-fire enemy weapon. Fired only when the tank's nose is closely aimed at the target.")]
    [SerializeField] private ProjectileWeaponEnemy3D cannonWeapon;

    [Tooltip("Bare-bones enemy missile launcher. Assign a missile prefab that contains MissileProjectile3D. Fires on a longer cooldown and uses a wider aim tolerance because missiles guide themselves after launch.")]
    [SerializeField] private MissileWeaponEnemy3D missileWeapon;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Network combat helper for replicated firing. Auto-assigned from this GameObject if left empty. Required for multiplayer projectile/missile fire.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Aim Tolerances")]
    [Tooltip("Max angle (degrees) between the tank's forward and the target direction before the cannon will fire. This gates permission to fire; launched shots use the target direction so allowed facing error does not become projectile error.")]
    [SerializeField] private float cannonAimToleranceDegrees = 8f;

    [Tooltip("Max angle (degrees) between the tank's forward and the target direction before the missile launcher will fire. This gates permission to fire; missiles still launch toward the target before guidance takes over.")]
    [SerializeField] private float missileAimToleranceDegrees = 35f;

    [Header("Fire Cadence")]
    [Tooltip("Seconds the tank waits after firing one weapon before it lets the other weapon fire. Keeps missiles and cannon shots from dumping on the same frame.")]
    [SerializeField] private float weaponStaggerDelay = 0.35f;

    [Header("Movement Range Bands")]
    [Tooltip("Distance (meters) at which the tank stops advancing and holds position to fire. Below this it will sit still.")]
    [SerializeField] private float stopDistance = 35f;

    [Tooltip("Distance (meters) beyond which the tank moves at full speed toward the target. Between stopDistance and this value, speed scales linearly.")]
    [SerializeField] private float fullSpeedDistance = 70f;

    [Tooltip("If true, route steering through the obstacle avoidance component when one is assigned. If false or no avoidance component exists, the tank steers straight at the target.")]
    [SerializeField] private bool useObstacleAvoidance;

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _cannonBlockedUntilTime;
    private float _missileBlockedUntilTime;
    private Entity3D _currentTarget;

    private void Awake()
    {
        cannonWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<MissileWeaponEnemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.TankBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        cannonAimToleranceDegrees = Mathf.Clamp(stats.cannonAimToleranceDegrees, 0f, 180f);
        missileAimToleranceDegrees = Mathf.Clamp(stats.missileAimToleranceDegrees, 0f, 180f);
        weaponStaggerDelay = Mathf.Max(0f, stats.weaponStaggerDelay);
        stopDistance = Mathf.Max(0f, stats.stopDistance);
        fullSpeedDistance = Mathf.Max(stopDistance + 0.01f, stats.fullSpeedDistance);
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        _currentTarget = null;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            flightController?.ClearFlightIntent();
            _currentTarget = null;
            return;
        }

        if (Time.time >= _nextThinkTime)
        {
            _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
            RefreshTrackedTarget();
        }

        Think();
    }

    private void RefreshTrackedTarget()
    {
        _currentTarget = targetSensor != null ? targetSensor.GetTarget() : null;
    }

    private void Think()
    {
        Entity3D target = ResolveTrackedTarget();
        if (target == null)
        {
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(toTarget)
            : toTarget.normalized;

        flightController?.SetMoveDirection(steeringDirection, ResolveDistanceSpeedScale(toTarget.magnitude));

        Vector3 toTargetNormalized = toTarget.normalized;
        bool firedCannon = TryFireWeapon(cannonWeapon, toTargetNormalized, cannonAimToleranceDegrees, _cannonBlockedUntilTime);
        if (firedCannon)
        {
            _missileBlockedUntilTime = Time.time + Mathf.Max(0f, weaponStaggerDelay);
            return;
        }

        if (TryFireWeapon(missileWeapon, toTargetNormalized, missileAimToleranceDegrees, _missileBlockedUntilTime))
        {
            _cannonBlockedUntilTime = Time.time + Mathf.Max(0f, weaponStaggerDelay);
        }
    }

    private Entity3D ResolveTrackedTarget()
    {
        if (IsTrackedTargetValid(_currentTarget))
        {
            return _currentTarget;
        }

        RefreshTrackedTarget();
        return _currentTarget;
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private static bool IsTrackedTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }

    private bool TryFireWeapon(
        IEnemyProjectileWeapon3D weapon,
        Vector3 toTargetNormalized,
        float aimToleranceDegrees,
        float blockedUntilTime)
    {
        if (weapon == null)
        {
            return false;
        }

        if (Time.time < blockedUntilTime)
        {
            return false;
        }

        if (Vector3.Angle(transform.forward, toTargetNormalized) > Mathf.Max(0f, aimToleranceDegrees))
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(weapon, Faction3D.PlayerTeam, toTargetNormalized);
        }

        return weapon.TryFireAtFaction(Faction3D.PlayerTeam, toTargetNormalized);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fullSpeedDistance);
    }
}
