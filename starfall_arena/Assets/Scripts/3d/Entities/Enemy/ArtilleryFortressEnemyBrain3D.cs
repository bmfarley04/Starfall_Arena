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

    [Tooltip("AI flight motor that drives the Rigidbody. The fortress normally rotates in place, but can creep forward when the target is just outside cannon range. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Set its detectionRadius high (e.g. 200-300m) on the prefab - that radius is what enforces the fortress's long-range identity. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Network combat helper for replicated firing. Auto-assigned from this GameObject if left empty. Required for multiplayer projectile fire.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Optional local presentation helper for the fortress charge tell. Auto-assigned from this GameObject or children if left empty. Gameplay does not depend on this component.")]
    [SerializeField] private ArtilleryFortressChargeTelegraph3D chargeTelegraph;

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
    private Entity3D _currentTarget;

    private void Awake()
    {
        cannonWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<StaggeredMissileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<MissileWeaponEnemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        chargeTelegraph ??= GetComponentInChildren<ArtilleryFortressChargeTelegraph3D>(true);
        _networkObject = GetComponent<NetworkObject>();
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
        loseTargetMaxDistance = Mathf.Max(0f, loseTargetMaxDistance);
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        StopChargeTelegraphLocal(immediate: true);
        _state = FortressState.Acquiring;
        _currentTarget = null;
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
        Entity3D target = ResolveTrackedTarget();
        if (!IsTargetEngageable(target))
        {
            CancelChargeAndAcquire(clearFlightIntent: true);
            return;
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
        Vector3 aimDirection = ResolveAimDirection(target);
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
        _chargeDuration = Mathf.Max(0f, chargeWindUpDuration);
        _chargeEndsAt = Time.time + _chargeDuration;
        _state = FortressState.Charging;
        StartChargeTelegraph(_chargeDuration);
    }

    private void TickCharging(Entity3D target)
    {
        if (_lockedFireDirection.sqrMagnitude <= 0.0001f)
        {
            CancelChargeAndAcquire(clearFlightIntent: true);
            return;
        }

        if (!IsTargetInsideFiringRange(target))
        {
            CancelChargeAndAcquire(clearFlightIntent: false);
            Vector3 aimDirection = ResolveAimDirection(target);
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
    }

    private void TryFireLockedShot()
    {
        if (cannonWeapon == null)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.TryFireProjectilePattern(cannonWeapon, Faction3D.PlayerTeam, _lockedFireDirection);
            return;
        }

        cannonWeapon.TryFireAtFaction(Faction3D.PlayerTeam, _lockedFireDirection);
    }

    private Vector3 ResolveAimDirection(Entity3D target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        if (!useLeadAim)
        {
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
        }

        float projectileSpeed = cannonWeapon != null ? Mathf.Max(0f, cannonWeapon.WeaponConfig.speed) : 0f;
        if (projectileSpeed <= 0.0001f)
        {
            return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
        }

        Vector3 targetVelocity = ResolveTargetVelocity(target);
        Vector3 leadPoint = target.transform.position;

        for (int pass = 0; pass < Mathf.Clamp(leadAimRefinementPasses, 1, 3); pass++)
        {
            float distance = Vector3.Distance(transform.position, leadPoint);
            float flightTime = distance / projectileSpeed;
            leadPoint = target.transform.position + (targetVelocity * flightTime);
        }

        Vector3 toLead = leadPoint - transform.position;
        return toLead.sqrMagnitude > 0.0001f ? toLead.normalized : Vector3.zero;
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
            return netEnemyCombat.TryFireProjectilePattern(missileWeapon, Faction3D.PlayerTeam, toTarget.normalized);
        }

        return missileWeapon.TryFireAtFaction(Faction3D.PlayerTeam, toTarget.normalized);
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
    }

    private void StartChargeTelegraph(float duration)
    {
        chargeTelegraph?.PlayCharge(duration);

        if (ShouldReplicateTelegraph())
        {
            StartChargeTelegraphClientRpc(duration, ResolveNetworkServerTime());
        }
    }

    private void StopChargeTelegraph()
    {
        StopChargeTelegraphLocal(immediate: false);

        if (ShouldReplicateTelegraph())
        {
            StopChargeTelegraphClientRpc();
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
    private void StartChargeTelegraphClientRpc(float duration, double serverStartTime)
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

        chargeTelegraph?.PlayCharge(duration, elapsed);
    }

    [ClientRpc]
    private void StopChargeTelegraphClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        StopChargeTelegraphLocal(immediate: false);
    }
}
