using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class ArtilleryFortressEnemyBrain3D : NetworkBehaviour
{
    private enum FortressState
    {
        Acquiring,
        Charging
    }

    [Header("Artillery Fortress")]
    [Tooltip("Heavy enemy cannon used for the fortress slug. The projectile config on this weapon controls speed/damage/lifetime/cooldown of the cannonball - tune the slow heavy feel there, not on this brain.")]
    [SerializeField] private ProjectileWeaponEnemy3D cannonWeapon;

    [Tooltip("Optional close-range guided missile launcher. Assign StaggeredMissileWeaponEnemy3D when this fortress has multiple launcher transforms that should fire one at a time.")]
    [SerializeField] private MissileWeaponEnemy3D missileWeapon;

    [Tooltip("Optional close-range laser-bolt turret weapons. Each weapon can own many turret muzzle transforms and stagger them independently from the cannon and missile rack.")]
    [SerializeField] private StaggeredProjectileWeaponEnemy3D[] closeRangeTurretWeapons;

    [Tooltip("AI flight motor that drives the Rigidbody. The fortress normally rotates in place, but can creep forward when the target is just outside cannon range. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Set its detectionRadius high (e.g. 200-300m) on the prefab - that radius is what enforces the fortress's long-range identity. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Network combat helper for replicated firing. Auto-assigned from this GameObject if left empty. Required for multiplayer projectile fire.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Optional local presentation helper for the fortress charge tell. Auto-assigned from this GameObject or children if left empty. Gameplay does not depend on this component.")]
    [SerializeField] private ProjectileChargeTelegraph3D chargeTelegraph;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Aim")]
    [Tooltip("Max angle (degrees) between the fortress's forward and the lead-aim direction before it commits to a charged shot. This gates when it is allowed to fire; the shot itself uses the locked lead direction so tolerance error does not become long-range miss error.")]
    [SerializeField] private float aimToleranceDegrees = 4f;

    [Tooltip("If true, the fortress predicts the target's future position using its current Rigidbody velocity and the cannon's projectile speed, then aims at the predicted point. If false, it aims at the target's current position.")]
    [SerializeField] private bool useLeadAim = true;

    [Tooltip("Number of refinement passes for the lead-aim solver. 1 is a single-step prediction; 2 is good balance; 3 is overkill for normal projectile speeds. Higher passes track curving targets slightly better at trivial CPU cost.")]
    [Range(1, 3)]
    [SerializeField] private int leadAimRefinementPasses = 2;

    [Header("Charge / Telegraph")]
    [Tooltip("Visible windup duration (seconds) the fortress holds its locked aim before launching the cannonball. Gives the player a clear 'incoming' read and a real dodge window because the lead direction is locked at charge start. Set to 0 to fire instantly (only for A/B telegraph testing).")]
    [SerializeField] private float chargeWindUpDuration = 1.0f;

    [Header("Range")]
    [Tooltip("Maximum distance (meters) at which the fortress is allowed to start or finish a charged shot. Targets outside this distance force the fortress to approach instead of firing.")]
    [SerializeField] private float maxFiringRange = 200f;

    [Tooltip("Extra distance (meters) beyond Max Firing Range where the fortress will slowly approach to get back into firing range. Targets farther than range + this buffer are ignored by the brain even if the target sensor can see them.")]
    [SerializeField] private float approachRangeBuffer = 100f;

    [Tooltip("Speed scale (0-1) used while the fortress is outside firing range but inside its approach buffer. Keep low so it reads as a heavy siege piece, not a chaser.")]
    [Range(0f, 1f)]
    [SerializeField] private float outOfRangeApproachSpeedScale = 0.2f;

    [Header("Close-Range Missiles")]
    [Tooltip("Maximum distance (meters) at which the fortress can fire guided missiles. Keep this shorter than Max Firing Range so missiles are close-range pressure, not another siege shot.")]
    [SerializeField] private float maxMissileRange = 120f;

    [Tooltip("Max angle (degrees) between the fortress forward direction and target direction before it can launch a guided missile. Missiles can use a looser tolerance than cannonballs because they steer after launch.")]
    [SerializeField] private float missileAimToleranceDegrees = 45f;

    [Tooltip("Seconds after a missile launch before the fortress can start a cannon charge. Prevents close-range missiles and cannon windup from triggering on the same frame.")]
    [SerializeField] private float missileToCannonStaggerDelay = 0.35f;

    [Header("Close-Range Turrets")]
    [Tooltip("Maximum distance (meters) at which the fortress can fire its small laser-bolt turrets. These turrets are independent close-range pressure and do not require the cannon charge state.")]
    [SerializeField] private float maxTurretRange = 100f;

    [Tooltip("Seconds after a turret bolt launch before the fortress can start a cannon charge. Keeps close-range chip fire from starting on the exact same frame as the heavy cannon windup.")]
    [SerializeField] private float turretToCannonStaggerDelay = 0.15f;

    [Header("Targeting Safety")]
    [Tooltip("Optional absolute hard cap on engagement distance, in meters. 0 means the brain uses Max Firing Range + Approach Range Buffer as its cap.")]
    [SerializeField] private float loseTargetMaxDistance = 0f;

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private FortressState _state = FortressState.Acquiring;
    private float _chargeEndsAt;
    private float _chargeDuration;
    private float _cannonBlockedUntilTime;
    private Vector3 _lockedFireDirection;
    private Vector3 _lockedFirePoint;
    private Entity3D _currentTarget;
    private Entity3D _chargeTarget;

    private void Awake()
    {
        cannonWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<StaggeredMissileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<MissileWeaponEnemy3D>();
        if (closeRangeTurretWeapons == null || closeRangeTurretWeapons.Length == 0)
        {
            closeRangeTurretWeapons = GetComponents<StaggeredProjectileWeaponEnemy3D>();
        }

        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        chargeTelegraph ??= GetComponentInChildren<ProjectileChargeTelegraph3D>(true);
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.ArtilleryFortressBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        aimToleranceDegrees = Mathf.Clamp(stats.aimToleranceDegrees, 0f, 180f);
        useLeadAim = stats.useLeadAim;
        leadAimRefinementPasses = Mathf.Clamp(stats.leadAimRefinementPasses, 1, 3);
        chargeWindUpDuration = Mathf.Max(0f, stats.chargeWindUpDuration);
        maxFiringRange = Mathf.Max(0.01f, stats.maxFiringRange);
        approachRangeBuffer = Mathf.Max(0f, stats.approachRangeBuffer);
        outOfRangeApproachSpeedScale = Mathf.Clamp01(stats.outOfRangeApproachSpeedScale);
        maxMissileRange = Mathf.Max(0f, stats.maxMissileRange);
        missileAimToleranceDegrees = Mathf.Clamp(stats.missileAimToleranceDegrees, 0f, 180f);
        missileToCannonStaggerDelay = Mathf.Max(0f, stats.missileToCannonStaggerDelay);
        maxTurretRange = Mathf.Max(0f, stats.maxTurretRange);
        turretToCannonStaggerDelay = Mathf.Max(0f, stats.turretToCannonStaggerDelay);
        loseTargetMaxDistance = Mathf.Max(0f, stats.loseTargetMaxDistance);
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        aimToleranceDegrees = Mathf.Clamp(aimToleranceDegrees, 0f, 180f);
        leadAimRefinementPasses = Mathf.Clamp(leadAimRefinementPasses, 1, 3);
        chargeWindUpDuration = Mathf.Max(0f, chargeWindUpDuration);
        maxFiringRange = Mathf.Max(0.01f, maxFiringRange);
        approachRangeBuffer = Mathf.Max(0f, approachRangeBuffer);
        outOfRangeApproachSpeedScale = Mathf.Clamp01(outOfRangeApproachSpeedScale);
        maxMissileRange = Mathf.Max(0f, maxMissileRange);
        missileAimToleranceDegrees = Mathf.Clamp(missileAimToleranceDegrees, 0f, 180f);
        missileToCannonStaggerDelay = Mathf.Max(0f, missileToCannonStaggerDelay);
        maxTurretRange = Mathf.Max(0f, maxTurretRange);
        turretToCannonStaggerDelay = Mathf.Max(0f, turretToCannonStaggerDelay);
        loseTargetMaxDistance = Mathf.Max(0f, loseTargetMaxDistance);
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        StopChargeTelegraphLocal(immediate: true);
        _state = FortressState.Acquiring;
        _currentTarget = null;
        _chargeTarget = null;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            flightController?.ClearFlightIntent();
            _state = FortressState.Acquiring;
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
        Entity3D target = _state == FortressState.Charging
            ? ResolveChargeTarget()
            : ResolveTrackedTarget();
        if (!IsTargetEngageable(target))
        {
            CancelChargeAndAcquire(clearFlightIntent: false);
            PatrolOrClearFlightIntent();
            return;
        }

        if (TryFireCloseRangeTurret(target))
        {
            _cannonBlockedUntilTime = Time.time + turretToCannonStaggerDelay;
        }

        switch (_state)
        {
            case FortressState.Acquiring:
                TickAcquiring(target);
                break;
            case FortressState.Charging:
                TickCharging(target);
                break;
        }
    }

    private void TickAcquiring(Entity3D target)
    {
        Vector3 aimPoint = ResolveAimPoint(target);
        Vector3 aimDirection = ResolveDirectionFromRoot(aimPoint);
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        if (TryHandleOutOfFiringRange(target, aimDirection))
        {
            return;
        }

        flightController?.SetFacingDirection(aimDirection);

        if (TryFireCloseRangeMissile(target))
        {
            _cannonBlockedUntilTime = Time.time + missileToCannonStaggerDelay;
            return;
        }

        if (Time.time < _cannonBlockedUntilTime || cannonWeapon == null || !cannonWeapon.IsFireGateReady)
        {
            return;
        }

        if (Vector3.Angle(transform.forward, aimDirection) > Mathf.Max(0f, aimToleranceDegrees))
        {
            return;
        }

        _lockedFireDirection = aimDirection;
        _lockedFirePoint = aimPoint;
        _chargeTarget = target;
        _chargeDuration = Mathf.Max(0f, chargeWindUpDuration);
        _chargeEndsAt = Time.time + _chargeDuration;
        _state = FortressState.Charging;
        if (UsesWarningSphereCharge() && !TryResolveWarningTargetAnchor(target, out _))
        {
            CancelChargeAndAcquire(clearFlightIntent: true);
            return;
        }

        StartChargeTelegraph(_chargeDuration, target);
    }

    private void TickCharging(Entity3D target)
    {
        if (UsesWarningSphereCharge() && !TryResolveWarningTargetAnchor(target, out _))
        {
            CancelChargeAndAcquire(clearFlightIntent: true);
            return;
        }

        if (UsesWarningSphereCharge())
        {
            Vector3 liveAimPoint = ResolveAimPoint(target);
            Vector3 liveAimDirection = ResolveDirectionFromRoot(liveAimPoint);
            if (liveAimDirection.sqrMagnitude <= 0.0001f)
            {
                CancelChargeAndAcquire(clearFlightIntent: true);
                return;
            }

            _lockedFirePoint = liveAimPoint;
            _lockedFireDirection = liveAimDirection;
        }

        if (_lockedFireDirection.sqrMagnitude <= 0.0001f)
        {
            CancelChargeAndAcquire(clearFlightIntent: true);
            return;
        }

        if (!IsTargetInsideFiringRange(target))
        {
            CancelChargeAndAcquire(clearFlightIntent: false);
            Vector3 aimPoint = ResolveAimPoint(target);
            Vector3 aimDirection = ResolveDirectionFromRoot(aimPoint);
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                TryHandleOutOfFiringRange(target, aimDirection);
            }
            return;
        }

        flightController?.SetFacingDirection(_lockedFireDirection);

        if (Time.time < _chargeEndsAt)
        {
            return;
        }

        TryFireLockedShot();
        StopChargeTelegraph();
        _state = FortressState.Acquiring;
        _chargeTarget = null;
    }

    private void TryFireLockedShot()
    {
        if (cannonWeapon == null)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.TryFireProjectilePatternConverged(cannonWeapon, Faction3D.PlayerTeam, _lockedFirePoint, _chargeTarget);
            return;
        }

        if (cannonWeapon.TryFireAtFactionConverged(Faction3D.PlayerTeam, _lockedFirePoint))
        {
            attackReporter?.ReportAttack(_chargeTarget);
        }
    }

    private Vector3 ResolveAimPoint(Entity3D target)
    {
        if (!useLeadAim)
        {
            return target.transform.position;
        }

        float projectileSpeed = cannonWeapon != null ? Mathf.Max(0f, cannonWeapon.WeaponConfig.speed) : 0f;
        if (projectileSpeed <= 0.0001f)
        {
            return target.transform.position;
        }

        Vector3 targetVelocity = ResolveTargetVelocity(target);
        Vector3 leadPoint = target.transform.position;

        for (int pass = 0; pass < Mathf.Clamp(leadAimRefinementPasses, 1, 3); pass++)
        {
            float distance = Vector3.Distance(transform.position, leadPoint);
            float flightTime = distance / projectileSpeed;
            leadPoint = target.transform.position + (targetVelocity * flightTime);
        }

        return leadPoint;
    }

    private Vector3 ResolveDirectionFromRoot(Vector3 aimPoint)
    {
        Vector3 toAimPoint = aimPoint - transform.position;
        return toAimPoint.sqrMagnitude > 0.0001f ? toAimPoint.normalized : Vector3.zero;
    }

    private Vector3 ResolveTargetVelocity(Entity3D target)
    {
        Rigidbody rb = target.GetComponent<Rigidbody>();
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }

    private bool TryFireCloseRangeMissile(Entity3D target)
    {
        if (missileWeapon == null || !missileWeapon.IsFireGateReady || !IsTargetInsideMissileRange(target))
        {
            return false;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (Vector3.Angle(transform.forward, toTarget.normalized) > Mathf.Max(0f, missileAimToleranceDegrees))
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(missileWeapon, Faction3D.PlayerTeam, toTarget.normalized, target);
        }

        bool fired = missileWeapon.TryFireAtFaction(Faction3D.PlayerTeam, toTarget.normalized);
        if (fired)
        {
            attackReporter?.ReportAttack(target);
        }

        return fired;
    }

    private bool TryFireCloseRangeTurret(Entity3D target)
    {
        if (closeRangeTurretWeapons == null || closeRangeTurretWeapons.Length == 0 || !IsTargetInsideTurretRange(target))
        {
            return false;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 fireDirection = toTarget.normalized;
        bool firedAny = false;
        for (int i = 0; i < closeRangeTurretWeapons.Length; i++)
        {
            StaggeredProjectileWeaponEnemy3D turretWeapon = closeRangeTurretWeapons[i];
            if (turretWeapon == null || !turretWeapon.IsFireGateReady)
            {
                continue;
            }

            bool fired = NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned
                ? netEnemyCombat.TryFireProjectilePattern(turretWeapon, Faction3D.PlayerTeam, fireDirection, target)
                : turretWeapon.TryFireAtFaction(Faction3D.PlayerTeam, fireDirection);

            if (fired && (!NetTickUtil.IsActive || netEnemyCombat == null || !netEnemyCombat.IsSpawned))
            {
                attackReporter?.ReportAttack(target);
            }

            firedAny |= fired;
        }

        return firedAny;
    }

    private bool TryHandleOutOfFiringRange(Entity3D target, Vector3 aimDirection)
    {
        if (IsTargetInsideFiringRange(target))
        {
            return false;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return true;
        }

        Vector3 moveDirection = toTarget.normalized;
        Vector3 facingDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : moveDirection;
        flightController?.SetFlightIntent(moveDirection, facingDirection, outOfRangeApproachSpeedScale, moveBackward: false);
        return true;
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

    private static bool IsTrackedTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }

    private bool IsTargetEngageable(Entity3D target)
    {
        if (!IsTrackedTargetValid(target))
        {
            return false;
        }

        float maxEngagementDistance = ResolveMaxEngagementDistance();
        if (maxEngagementDistance > 0f)
        {
            float sqr = (target.transform.position - transform.position).sqrMagnitude;
            if (sqr > maxEngagementDistance * maxEngagementDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsTargetInsideFiringRange(Entity3D target)
    {
        if (!IsTrackedTargetValid(target))
        {
            return false;
        }

        float range = Mathf.Max(0.01f, maxFiringRange);
        return (target.transform.position - transform.position).sqrMagnitude <= range * range;
    }

    private bool IsTargetInsideMissileRange(Entity3D target)
    {
        if (!IsTrackedTargetValid(target) || maxMissileRange <= 0f)
        {
            return false;
        }

        float range = Mathf.Max(0.01f, maxMissileRange);
        return (target.transform.position - transform.position).sqrMagnitude <= range * range;
    }

    private bool IsTargetInsideTurretRange(Entity3D target)
    {
        if (!IsTrackedTargetValid(target) || maxTurretRange <= 0f)
        {
            return false;
        }

        float range = Mathf.Max(0.01f, maxTurretRange);
        return (target.transform.position - transform.position).sqrMagnitude <= range * range;
    }

    private float ResolveMaxEngagementDistance()
    {
        if (loseTargetMaxDistance > 0f)
        {
            return loseTargetMaxDistance;
        }

        return Mathf.Max(0.01f, maxFiringRange) + Mathf.Max(0f, approachRangeBuffer);
    }

    private void CancelChargeAndAcquire(bool clearFlightIntent)
    {
        if (_state == FortressState.Charging)
        {
            StopChargeTelegraph();
        }

        if (clearFlightIntent)
        {
            flightController?.ClearFlightIntent();
        }

        _state = FortressState.Acquiring;
        _lockedFireDirection = Vector3.zero;
        _lockedFirePoint = Vector3.zero;
        _chargeTarget = null;
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private void StartChargeTelegraph(float duration, Entity3D intendedTarget)
    {
        chargeTelegraph?.PlayCharge(duration, intendedTarget);

        if (ShouldReplicateTelegraph())
        {
            StartChargeTelegraphClientRpc(duration, ResolveNetworkServerTime(), ResolveNetworkObjectId(intendedTarget));
        }
    }

    private void StopChargeTelegraph()
    {
        StopChargeTelegraphLocal(immediate: false);

        if (ShouldReplicateTelegraph())
        {
            StopChargeTelegraphClientRpc(ResolveNetworkObjectId(_chargeTarget));
        }
    }

    private void StopChargeTelegraphLocal(bool immediate)
    {
        if (chargeTelegraph == null)
        {
            return;
        }

        chargeTelegraph.StopCharge(immediate);
    }

    private bool ShouldReplicateTelegraph()
    {
        return NetTickUtil.IsActive
            && NetworkManager.Singleton != null
            && IsServer
            && IsSpawned;
    }

    private double ResolveNetworkServerTime()
    {
        return NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d;
    }

    private bool HasBrainAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }

    [ClientRpc]
    private void StartChargeTelegraphClientRpc(float duration, double serverStartTime, ulong intendedTargetNetworkObjectId)
    {
        if (IsServer)
        {
            return;
        }

        float elapsed = 0f;
        if (NetworkManager.Singleton != null && serverStartTime > 0d)
        {
            elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - serverStartTime));
        }

        chargeTelegraph?.PlayCharge(duration, elapsed, ResolveNetworkTarget(intendedTargetNetworkObjectId));
    }

    [ClientRpc]
    private void StopChargeTelegraphClientRpc(ulong intendedTargetNetworkObjectId)
    {
        if (IsServer)
        {
            return;
        }

        StopChargeTelegraphLocal(immediate: false);
    }

    private Entity3D ResolveChargeTarget()
    {
        return IsTrackedTargetValid(_chargeTarget) ? _chargeTarget : null;
    }

    private bool UsesWarningSphereCharge()
    {
        return chargeTelegraph != null && chargeTelegraph.UsesWarningSphere;
    }

    private bool TryResolveWarningTargetAnchor(Entity3D target, out Transform warningAnchor)
    {
        warningAnchor = null;
        return chargeTelegraph == null || chargeTelegraph.TryResolveWarningSphereAnchor(target, out warningAnchor);
    }

    private static ulong ResolveNetworkObjectId(Entity3D target)
    {
        if (target == null || !target.TryGetComponent(out NetworkObject networkObject) || !networkObject.IsSpawned)
        {
            return 0UL;
        }

        return networkObject.NetworkObjectId;
    }

    private static Entity3D ResolveNetworkTarget(ulong networkObjectId)
    {
        if (networkObjectId == 0UL || NetworkManager.Singleton == null)
        {
            return null;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject)
            || networkObject == null)
        {
            return null;
        }

        return networkObject.GetComponent<Entity3D>();
    }
}
