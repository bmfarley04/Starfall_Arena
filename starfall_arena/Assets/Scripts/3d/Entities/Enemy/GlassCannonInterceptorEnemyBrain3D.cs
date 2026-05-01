using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class GlassCannonInterceptorEnemyBrain3D : MonoBehaviour
{
    private enum InterceptorState
    {
        Reposition,
        Settle,
        Burst,
        Recover
    }

    [Header("Glass Cannon Interceptor")]
    [Tooltip("Enemy-only direct-fire weapon used for the interceptor's high-damage burst. Configure this weapon for roughly 10 damage, 0.2s cooldown, and fast bolt speed.")]
    [SerializeField] private ProjectileWeaponEnemy3D burstWeapon;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Optional inter-agent separation steering. Useful if multiple interceptors are active and should not stack on the same perch.")]
    [SerializeField] private EnemySeparation3D separation;

    [Tooltip("Network combat helper for replicated enemy projectile fire. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI steering decisions. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Perch Movement")]
    [Tooltip("Minimum preferred distance (meters) from the target when choosing the next firing perch.")]
    [SerializeField] private float preferredRangeMin = 40f;

    [Tooltip("Maximum preferred distance (meters) from the target when choosing the next firing perch.")]
    [SerializeField] private float preferredRangeMax = 50f;

    [Tooltip("Distance (meters) from the selected perch that counts as arrived. Higher values make the interceptor settle and shoot sooner.")]
    [SerializeField] private float perchArrivalDistance = 4f;

    [Tooltip("Maximum seconds spent flying toward one selected perch before settling anyway. Prevents the interceptor from chasing an unreachable point forever.")]
    [SerializeField] private float maxRepositionDuration = 1.8f;

    [Tooltip("How strongly the next perch is biased sideways around the player. Higher values make bigger lateral jukes between bursts.")]
    [SerializeField] private float lateralPerchBias = 1f;

    [Tooltip("How strongly the next perch is biased above/below the player. Higher values make the interceptor use more vertical space.")]
    [SerializeField] private float verticalPerchBias = 0.65f;

    [Tooltip("How much of the current away-from-player direction is retained when choosing the next perch. Higher values produce smoother arcs; lower values produce sharper jukes.")]
    [SerializeField] private float outwardPerchBias = 0.35f;

    [Header("Burst Timing")]
    [Tooltip("Seconds the interceptor sits still and rotates onto the player before firing. This is the player's pre-burst accuracy-check window.")]
    [SerializeField] private float preBurstSettleDuration = 0.35f;

    [Tooltip("Maximum seconds the interceptor will wait in the settle state if it has not aimed at the target yet.")]
    [SerializeField] private float maxSettleDuration = 1f;

    [Tooltip("Shots per burst. The weapon cooldown still gates the exact shot spacing.")]
    [SerializeField] private int shotsPerBurst = 3;

    [Tooltip("Desired seconds between burst shots. Match this to the assigned weapon cooldown, usually around 0.2s.")]
    [SerializeField] private float burstShotInterval = 0.2f;

    [Tooltip("Maximum angle (degrees) between the interceptor's forward direction and the target direction before a burst shot is allowed. This is only a permission gate; burst shots are launched along the target direction so a few degrees of allowed facing error does not become a guaranteed miss at range.")]
    [SerializeField] private float aimToleranceDegrees = 8f;

    [Tooltip("Seconds the interceptor remains stationary after finishing or timing out a burst. This is the post-burst punish window before it relocates.")]
    [SerializeField] private float postBurstRecoverDuration = 0.3f;

    [Tooltip("Maximum seconds allowed for a single burst before the interceptor gives up and relocates. Prevents indefinite waiting if the target keeps crossing its nose.")]
    [SerializeField] private float maxBurstDuration = 1.25f;

    [Header("Steering Composition")]
    [Tooltip("If true, route reposition steering through the separation component when one is assigned.")]
    [SerializeField] private bool useSeparation = true;

    [Tooltip("If true, route reposition steering through the obstacle avoidance component when one is assigned.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    private NetworkObject _networkObject;
    private InterceptorState _state = InterceptorState.Reposition;
    private Entity3D _currentTarget;
    private Vector3 _perchPosition;
    private float _nextThinkTime;
    private float _stateStartedAt;
    private float _stateEndsAt;
    private float _nextShotTime;
    private int _shotsFiredInBurst;
    private int _perchSequence;

    private void Awake()
    {
        burstWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.GlassCannonBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        preferredRangeMin = Mathf.Max(0f, stats.preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, stats.preferredRangeMax);
        perchArrivalDistance = Mathf.Max(0.1f, stats.perchArrivalDistance);
        maxRepositionDuration = Mathf.Max(0.1f, stats.maxRepositionDuration);
        lateralPerchBias = Mathf.Max(0f, stats.lateralPerchBias);
        verticalPerchBias = Mathf.Max(0f, stats.verticalPerchBias);
        outwardPerchBias = Mathf.Max(0f, stats.outwardPerchBias);
        preBurstSettleDuration = Mathf.Max(0f, stats.preBurstSettleDuration);
        maxSettleDuration = Mathf.Max(0.01f, stats.maxSettleDuration);
        shotsPerBurst = Mathf.Max(1, stats.shotsPerBurst);
        burstShotInterval = Mathf.Max(0.01f, stats.burstShotInterval);
        aimToleranceDegrees = Mathf.Clamp(stats.aimToleranceDegrees, 0f, 180f);
        postBurstRecoverDuration = Mathf.Max(0f, stats.postBurstRecoverDuration);
        maxBurstDuration = Mathf.Max(0.1f, stats.maxBurstDuration);
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        preferredRangeMin = Mathf.Max(0f, preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, preferredRangeMax);
        perchArrivalDistance = Mathf.Max(0.1f, perchArrivalDistance);
        maxRepositionDuration = Mathf.Max(0.1f, maxRepositionDuration);
        lateralPerchBias = Mathf.Max(0f, lateralPerchBias);
        verticalPerchBias = Mathf.Max(0f, verticalPerchBias);
        outwardPerchBias = Mathf.Max(0f, outwardPerchBias);
        preBurstSettleDuration = Mathf.Max(0f, preBurstSettleDuration);
        maxSettleDuration = Mathf.Max(0.01f, maxSettleDuration);
        shotsPerBurst = Mathf.Max(1, shotsPerBurst);
        burstShotInterval = Mathf.Max(0.01f, burstShotInterval);
        aimToleranceDegrees = Mathf.Clamp(aimToleranceDegrees, 0f, 180f);
        postBurstRecoverDuration = Mathf.Max(0f, postBurstRecoverDuration);
        maxBurstDuration = Mathf.Max(0.1f, maxBurstDuration);
    }

    private void OnEnable()
    {
        _state = InterceptorState.Reposition;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time;
        _nextShotTime = Time.time;
        _shotsFiredInBurst = 0;
        _perchSequence = 0;
        flightController?.ClearFlightIntent();
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

        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
        Think();
    }

    private void Think()
    {
        Entity3D target = ResolveTarget();
        if (target == null)
        {
            PatrolOrClearFlightIntent();
            return;
        }

        switch (_state)
        {
            case InterceptorState.Reposition:
                UpdateReposition(target);
                break;
            case InterceptorState.Settle:
                UpdateSettle(target);
                break;
            case InterceptorState.Burst:
                UpdateBurst(target);
                break;
            case InterceptorState.Recover:
                UpdateRecover(target);
                break;
        }
    }

    private void UpdateReposition(Entity3D target)
    {
        Vector3 toPerch = _perchPosition - transform.position;
        bool reachedPerch = toPerch.magnitude <= Mathf.Max(0.1f, perchArrivalDistance);
        bool timedOut = Time.time >= _stateEndsAt;
        if (reachedPerch || timedOut)
        {
            BeginSettle();
            return;
        }

        Vector3 desired = toPerch.sqrMagnitude > 0.0001f ? toPerch.normalized : DirectionToTarget(target);
        Vector3 steered = ResolveSteering(desired);
        flightController?.SetMoveDirection(steered, 1f);
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private void UpdateSettle(Entity3D target)
    {
        FaceTargetWithoutMoving(target);

        bool waitedLongEnough = Time.time - _stateStartedAt >= Mathf.Max(0f, preBurstSettleDuration);
        bool aimed = IsAimedAtTarget(target);
        bool timedOut = Time.time >= _stateEndsAt;
        if ((waitedLongEnough && aimed) || timedOut)
        {
            BeginBurst();
        }
    }

    private void UpdateBurst(Entity3D target)
    {
        FaceTargetWithoutMoving(target);

        if (_shotsFiredInBurst >= Mathf.Max(1, shotsPerBurst) || Time.time >= _stateEndsAt)
        {
            BeginRecover();
            return;
        }

        if (Time.time < _nextShotTime || !IsAimedAtTarget(target))
        {
            return;
        }

        if (TryFireBurstShot(DirectionToTarget(target), target))
        {
            _shotsFiredInBurst++;
            _nextShotTime = Time.time + Mathf.Max(0.01f, burstShotInterval);
        }
    }

    private void UpdateRecover(Entity3D target)
    {
        FaceTargetWithoutMoving(target);

        if (Time.time < _stateEndsAt)
        {
            return;
        }

        BeginReposition(target);
    }

    private void BeginReposition(Entity3D target)
    {
        _currentTarget = target;
        _perchPosition = ChooseNextPerch(target);
        _state = InterceptorState.Reposition;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time + Mathf.Max(0.1f, maxRepositionDuration);
    }

    private void BeginSettle()
    {
        _state = InterceptorState.Settle;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time + Mathf.Max(0.01f, maxSettleDuration);
        flightController?.ClearFlightIntent();
    }

    private void BeginBurst()
    {
        _state = InterceptorState.Burst;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time + Mathf.Max(0.1f, maxBurstDuration);
        _nextShotTime = Time.time;
        _shotsFiredInBurst = 0;
        flightController?.ClearFlightIntent();
    }

    private void BeginRecover()
    {
        _state = InterceptorState.Recover;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time + Mathf.Max(0f, postBurstRecoverDuration);
        flightController?.ClearFlightIntent();
    }

    private Entity3D ResolveTarget()
    {
        if (IsTargetValid(_currentTarget))
        {
            return _currentTarget;
        }

        _currentTarget = targetSensor != null ? targetSensor.GetTarget() : null;
        if (_currentTarget != null && _perchPosition.sqrMagnitude <= 0.0001f)
        {
            BeginReposition(_currentTarget);
        }

        return _currentTarget;
    }

    private Vector3 ChooseNextPerch(Entity3D target)
    {
        if (target == null)
        {
            return transform.position + transform.forward * preferredRangeMin;
        }

        Vector3 targetPosition = target.transform.position;
        Vector3 fromTarget = transform.position - targetPosition;
        if (fromTarget.sqrMagnitude <= 0.0001f)
        {
            fromTarget = -target.transform.forward;
        }

        Vector3 away = fromTarget.normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, away);
        if (lateral.sqrMagnitude <= 0.0001f)
        {
            lateral = Vector3.Cross(transform.up, away);
        }
        lateral = lateral.sqrMagnitude > 0.0001f ? lateral.normalized : transform.right;

        int sequence = _perchSequence++;
        float sideSign = sequence % 2 == 0 ? 1f : -1f;
        float verticalSign = sequence % 3 == 0 ? 1f : sequence % 3 == 1 ? -1f : 0.35f;

        Vector3 perchDirection =
            away * Mathf.Max(0f, outwardPerchBias)
            + lateral * (sideSign * Mathf.Max(0f, lateralPerchBias))
            + Vector3.up * (verticalSign * Mathf.Max(0f, verticalPerchBias));

        if (perchDirection.sqrMagnitude <= 0.0001f)
        {
            perchDirection = away;
        }

        float minRange = Mathf.Max(0f, preferredRangeMin);
        float maxRange = Mathf.Max(minRange + 0.01f, preferredRangeMax);
        float rangeBlend = sequence % 2 == 0 ? 0.35f : 0.85f;
        float range = Mathf.Lerp(minRange, maxRange, rangeBlend);
        return targetPosition + perchDirection.normalized * range;
    }

    private Vector3 ResolveSteering(Vector3 desiredDirection)
    {
        Vector3 result = desiredDirection;

        if (useSeparation && separation != null && separation.isActiveAndEnabled)
        {
            result = separation.ResolveSteeringDirection(result);
        }

        if (useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled)
        {
            result = obstacleAvoidance.ResolveSteeringDirection(result);
        }

        return result;
    }

    private void FaceTargetWithoutMoving(Entity3D target)
    {
        Vector3 toTarget = DirectionToTarget(target);
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        flightController?.SetFacingDirection(toTarget);
    }

    private bool TryFireBurstShot(Vector3 fireDirection, Entity3D target)
    {
        if (burstWeapon == null)
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(burstWeapon, Faction3D.PlayerTeam, fireDirection, target);
        }

        bool fired = burstWeapon.TryFireAtFaction(Faction3D.PlayerTeam, fireDirection);
        if (fired)
        {
            attackReporter?.ReportAttack(target);
        }

        return fired;
    }

    private bool IsAimedAtTarget(Entity3D target)
    {
        Vector3 toTarget = DirectionToTarget(target);
        return toTarget.sqrMagnitude > 0.0001f
            && Vector3.Angle(transform.forward, toTarget) <= Mathf.Max(0f, aimToleranceDegrees);
    }

    private Vector3 DirectionToTarget(Entity3D target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
    }

    private static bool IsTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredRangeMin);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, preferredRangeMax);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_perchPosition, perchArrivalDistance);
            Gizmos.DrawLine(transform.position, _perchPosition);
        }
    }
}
