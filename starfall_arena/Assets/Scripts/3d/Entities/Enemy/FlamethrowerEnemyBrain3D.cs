using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
[RequireComponent(typeof(EnemyFlamethrowerWeapon3D))]
[RequireComponent(typeof(EnemyStrafeMover3D))]
public class FlamethrowerEnemyBrain3D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Short-range flame weapon controlled by this brain. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyFlamethrowerWeapon3D flamethrowerWeapon;

    [Tooltip("Optional generic charge driver. When assigned, the brain starts this telegraphed windup instead of starting the flamethrower burst immediately.")]
    [SerializeField] private EnemyProjectileChargeAttack3D chargeAttack;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance used while approaching the player.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional inter-agent separation used while approaching or retreating so multiple flamethrowers do not stack perfectly.")]
    [SerializeField] private EnemySeparation3D separation;

    [Tooltip("World-space strafe overlay used to orbit the player while the flame burst is active.")]
    [SerializeField] private EnemyStrafeMover3D strafeMover;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Network combat helper used to replicate flame visuals. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Movement Range Bands")]
    [Tooltip("Distance where the flamethrower starts backing away while still facing the target.")]
    [SerializeField] private float tooCloseRetreatDistance = 14f;

    [Tooltip("Lower edge of the desired flame pocket. The enemy holds or backs away until it is at least this far from the target.")]
    [SerializeField] private float preferredRangeMin = 20f;

    [Tooltip("Upper edge of the desired flame pocket. The enemy can start firing once inside this range.")]
    [SerializeField] private float preferredRangeMax = 30f;

    [Tooltip("Distance beyond which the enemy approaches at full speed. Between Preferred Range Max and this value, approach speed scales down.")]
    [SerializeField] private float fullApproachDistance = 55f;

    [Tooltip("Speed scale used when backing away from a target that gets too close.")]
    [SerializeField, Range(0f, 1f)] private float retreatSpeedScale = 0.75f;

    [Tooltip("World-space strafe speed used to slowly orbit around the target while the flamethrower is active.")]
    [SerializeField] private float flameOrbitStrafeSpeed = 10f;

    [Tooltip("Small vertical component added to the orbit strafe so the enemy does not look perfectly planar in full 3D space.")]
    [SerializeField, Range(0f, 1f)] private float flameOrbitVerticalBias = 0.12f;

    [Tooltip("Seconds between orbit direction flips while flame is active. Set to 0 to keep one orbit direction for the enemy's lifetime.")]
    [SerializeField] private float flameOrbitDirectionChangeInterval = 3f;

    [Tooltip("If true, route approach and retreat steering through EnemySeparation3D when assigned.")]
    [SerializeField] private bool useSeparation = true;

    [Tooltip("If true, route approach steering through EnemyObstacleAvoidance3D when assigned.")]
    [SerializeField] private bool useObstacleAvoidance;

    [Header("Firing")]
    [Tooltip("Max angle in degrees between the muzzle forward direction and the target before a flame burst can start.")]
    [SerializeField, Range(0f, 180f)] private float aimToleranceDegrees = 22f;

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _nextOrbitDirectionChangeTime;
    private int _orbitSign = 1;
    private Entity3D _currentTarget;

    private void Awake()
    {
        flamethrowerWeapon ??= GetComponent<EnemyFlamethrowerWeapon3D>();
        chargeAttack ??= GetComponent<EnemyProjectileChargeAttack3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        strafeMover ??= GetComponent<EnemyStrafeMover3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        _networkObject = GetComponent<NetworkObject>();
        _orbitSign = GetInstanceID() % 2 == 0 ? 1 : -1;
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        tooCloseRetreatDistance = Mathf.Max(0f, tooCloseRetreatDistance);
        preferredRangeMin = Mathf.Max(tooCloseRetreatDistance + 0.01f, preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, preferredRangeMax);
        fullApproachDistance = Mathf.Max(preferredRangeMax + 0.01f, fullApproachDistance);
        aimToleranceDegrees = Mathf.Clamp(aimToleranceDegrees, 0f, 180f);
        flameOrbitStrafeSpeed = Mathf.Max(0f, flameOrbitStrafeSpeed);
        flameOrbitDirectionChangeInterval = Mathf.Max(0f, flameOrbitDirectionChangeInterval);
    }

    public void ApplyProfile(EnemyBalanceProfile3D.FlamethrowerBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        aimToleranceDegrees = Mathf.Clamp(stats.aimToleranceDegrees, 0f, 180f);
        tooCloseRetreatDistance = Mathf.Max(0f, stats.tooCloseRetreatDistance);
        preferredRangeMin = Mathf.Max(tooCloseRetreatDistance + 0.01f, stats.preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, stats.preferredRangeMax);
        fullApproachDistance = Mathf.Max(preferredRangeMax + 0.01f, stats.fullApproachDistance);
        retreatSpeedScale = Mathf.Clamp01(stats.retreatSpeedScale);
        flameOrbitStrafeSpeed = Mathf.Max(0f, stats.flameOrbitStrafeSpeed);
        flameOrbitVerticalBias = Mathf.Clamp01(stats.flameOrbitVerticalBias);
        flameOrbitDirectionChangeInterval = Mathf.Max(0f, stats.flameOrbitDirectionChangeInterval);
    }

    private void OnDisable()
    {
        chargeAttack?.CancelCharge(immediate: true);
        StopFlame();
        strafeMover?.StopStrafe();
        flightController?.ClearFlightIntent();
        _currentTarget = null;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            StopFlame();
            flightController?.ClearFlightIntent();
            _currentTarget = null;
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
        _currentTarget = target;
        if (target == null)
        {
            chargeAttack?.CancelCharge();
            StopFlame();
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            chargeAttack?.CancelCharge();
            StopFlame();
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 targetDirection = toTarget / distanceToTarget;
        UpdateMovement(targetDirection, distanceToTarget);
        UpdateFlameOrbit(targetDirection);
        UpdateFlame(targetDirection, distanceToTarget);
    }

    private void UpdateMovement(Vector3 targetDirection, float distanceToTarget)
    {
        if (distanceToTarget < preferredRangeMin)
        {
            Vector3 retreatDirection = ResolveSteeringDirection(-targetDirection, allowObstacleAvoidance: false);
            float speedScale = distanceToTarget <= tooCloseRetreatDistance ? retreatSpeedScale : ResolveRetreatBlend(distanceToTarget);
            flightController?.SetFlightIntent(retreatDirection, targetDirection, speedScale, moveBackward: true);
            return;
        }

        if (distanceToTarget > preferredRangeMax)
        {
            Vector3 approachDirection = ResolveSteeringDirection(targetDirection, allowObstacleAvoidance: true);
            flightController?.SetFlightIntent(approachDirection, targetDirection, ResolveApproachSpeedScale(distanceToTarget), moveBackward: false);
            return;
        }

        flightController?.SetFacingDirection(targetDirection);
    }

    private void UpdateFlame(Vector3 targetDirection, float distanceToTarget)
    {
        if (flamethrowerWeapon == null)
        {
            return;
        }

        if (distanceToTarget > preferredRangeMax || distanceToTarget < tooCloseRetreatDistance)
        {
            chargeAttack?.CancelCharge();
            return;
        }

        if (Vector3.Angle(transform.forward, targetDirection) > Mathf.Max(0f, aimToleranceDegrees))
        {
            chargeAttack?.CancelCharge();
            return;
        }

        if (chargeAttack != null)
        {
            if (!chargeAttack.IsCharging)
            {
                chargeAttack.TryBeginCharge(Faction3D.PlayerTeam, targetDirection);
            }

            return;
        }

        if (!flamethrowerWeapon.CanStartBurst())
        {
            return;
        }

        StartFlame();
    }

    private void UpdateFlameOrbit(Vector3 targetDirection)
    {
        if (strafeMover == null || flamethrowerWeapon == null || !flamethrowerWeapon.IsBurstActive)
        {
            return;
        }

        if (flameOrbitStrafeSpeed <= 0f)
        {
            strafeMover.StopStrafe();
            return;
        }

        if (flameOrbitDirectionChangeInterval > 0f && Time.time >= _nextOrbitDirectionChangeTime)
        {
            _orbitSign *= -1;
            _nextOrbitDirectionChangeTime = Time.time + flameOrbitDirectionChangeInterval;
        }

        Vector3 lateral = Vector3.ProjectOnPlane(transform.right * _orbitSign, targetDirection);
        if (lateral.sqrMagnitude <= 0.0001f)
        {
            lateral = Vector3.Cross(targetDirection, Vector3.up) * _orbitSign;
        }

        if (lateral.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 orbitDirection = (lateral.normalized + Vector3.up * flameOrbitVerticalBias).normalized;
        float strafeDuration = Mathf.Max(0.05f, thinkInterval * 2f);
        strafeMover.BeginStrafe(orbitDirection * flameOrbitStrafeSpeed, strafeDuration);
    }

    private void StartFlame()
    {
        if (flamethrowerWeapon == null)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetFlamethrowerState(flamethrowerWeapon, true);
            return;
        }

        flamethrowerWeapon.TryStartBurst(authoritativeDamage: true);
    }

    private void StopFlame()
    {
        if (flamethrowerWeapon == null || !flamethrowerWeapon.IsBurstActive)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetFlamethrowerState(flamethrowerWeapon, false);
            return;
        }

        flamethrowerWeapon.StopBurst();
    }

    private Vector3 ResolveSteeringDirection(Vector3 desiredDirection, bool allowObstacleAvoidance)
    {
        Vector3 resolved = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : transform.forward;
        if (useSeparation && separation != null && separation.isActiveAndEnabled)
        {
            resolved = separation.ResolveSteeringDirection(resolved);
        }

        if (allowObstacleAvoidance && useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled)
        {
            resolved = obstacleAvoidance.ResolveSteeringDirection(resolved);
        }

        return resolved;
    }

    private float ResolveApproachSpeedScale(float distanceToTarget)
    {
        if (distanceToTarget >= fullApproachDistance)
        {
            return 1f;
        }

        return Mathf.InverseLerp(preferredRangeMax, fullApproachDistance, distanceToTarget);
    }

    private float ResolveRetreatBlend(float distanceToTarget)
    {
        if (distanceToTarget <= tooCloseRetreatDistance)
        {
            return retreatSpeedScale;
        }

        return Mathf.Lerp(0.25f, retreatSpeedScale, Mathf.InverseLerp(preferredRangeMin, tooCloseRetreatDistance, distanceToTarget));
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
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
        Gizmos.DrawWireSphere(transform.position, tooCloseRetreatDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, preferredRangeMin);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredRangeMax);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, fullApproachDistance);
    }
}
