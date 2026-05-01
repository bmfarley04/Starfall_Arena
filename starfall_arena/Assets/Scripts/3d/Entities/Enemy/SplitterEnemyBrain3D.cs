using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class SplitterEnemyBrain3D : NetworkBehaviour
{
    private enum SplitterRole
    {
        ParentHybrid,
        ProjectileChild,
        BeamChild
    }

    [Header("Splitter Role")]
    [Tooltip("Runtime role for this Splitter instance. Author the prefab as Parent Hybrid; spawned children are assigned Projectile Child or Beam Child automatically.")]
    [SerializeField] private SplitterRole role = SplitterRole.ParentHybrid;

    [Tooltip("Enemy-only projectile weapon used by the parent at closer ranges and by the projectile child exclusively.")]
    [SerializeField] private ProjectileWeaponEnemy3D projectileWeapon;

    [Tooltip("Beam weapon used by the parent at farther ranges and by the beam child exclusively.")]
    [SerializeField] private BeamWeapon3D beamWeapon;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Network combat helper for replicated enemy projectile and beam fire. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    [Header("Movement")]
    [Tooltip("Seconds between heavier AI decisions such as weapon-choice rolls. Steering/facing still refresh every frame.")]
    [SerializeField] private float thinkInterval = 0.08f;

    [Tooltip("Distance where the Splitter stops advancing and holds pressure.")]
    [SerializeField] private float holdDistance = 24f;

    [Tooltip("Distance beyond which the Splitter moves at full speed toward the target.")]
    [SerializeField] private float fullSpeedDistance = 55f;

    [Tooltip("If true, route approach steering through the obstacle avoidance component when one is assigned.")]
    [SerializeField] private bool useObstacleAvoidance;

    [Header("Weapon Choice")]
    [Tooltip("Max angle in degrees between forward and target before projectile shots are allowed.")]
    [SerializeField] private float projectileAimToleranceDegrees = 12f;

    [Tooltip("Max angle in degrees between beam forward and target before the beam is allowed.")]
    [SerializeField] private float beamAimToleranceDegrees = 8f;

    [Tooltip("Distance where projectile fire has the strongest weight on the parent hybrid.")]
    [SerializeField] private float projectilePreferredDistance = 24f;

    [Tooltip("Distance where beam fire has the strongest weight on the parent hybrid.")]
    [SerializeField] private float beamPreferredDistance = 42f;

    [Tooltip("Parent-hybrid random chance to choose beam instead of projectile when both weapons are reasonably valid at the current distance.")]
    [Range(0f, 1f)]
    [SerializeField] private float mixedRangeBeamChance = 0.45f;

    [Tooltip("Extra distance around both preferred ranges where the parent treats both weapons as reasonable and rolls randomly.")]
    [SerializeField] private float mixedRangeWidth = 14f;

    [Tooltip("Minimum time to keep a chosen attack mode before rolling again, so the parent does not flicker between beam and projectile every think tick.")]
    [SerializeField] private float decisionHoldDuration = 0.45f;

    [Tooltip("Minimum remaining beam energy before this AI starts a new beam burst.")]
    [SerializeField] private float minimumBeamRestartEnergy = 20f;

    [Tooltip("Distance in meters ahead of the Splitter where multi-muzzle projectile volleys converge. Higher values make shots more parallel.")]
    [SerializeField] private float projectileConvergenceDistance = 250f;

    [Header("Debug")]
    [Tooltip("If true, logs every Splitter weapon choice and whether the chosen weapon actually fired.")]
    [SerializeField] private bool logWeaponChoices;

    [Header("Split")]
    [Tooltip("Prefab used for spawned Splitter children. Assign the same Splitter prefab here so the parent and children share one prefab asset.")]
    [SerializeField] private GameObject splitterPrefab;

    [Tooltip("How many smaller Splitter children spawn when the parent dies. Current design expects 2.")]
    [SerializeField] private int splitCount = 2;

    [Tooltip("Distance from the dying parent where each child appears.")]
    [SerializeField] private float splitSpawnRadius = 4f;

    [Tooltip("Random vertical offset range applied to each split spawn so children do not stack in the same flight lane.")]
    [SerializeField] private float verticalSpawnJitter = 1.5f;

    [Tooltip("Local scale multiplier applied to spawned children.")]
    [SerializeField] private float childScaleMultiplier = 0.6f;

    [Tooltip("Move-speed multiplier applied to spawned children so the smaller Splitters are faster than the parent.")]
    [SerializeField] private float childMoveSpeedMultiplier = 1.35f;

    [Tooltip("Max health assigned to each spawned child.")]
    [SerializeField] private float childMaxHealth = 35f;

    [Tooltip("Max shield assigned to each spawned child.")]
    [SerializeField] private float childMaxShield = 0f;

    [Tooltip("Wave manager used to spawn and track child enemies. If empty, the first active InvasionWaveManager3D is found at runtime.")]
    [SerializeField] private InvasionWaveManager3D waveManager;

    [Tooltip("If true, this component logs missing weapon or wave-manager setup instead of failing silently.")]
    [SerializeField] private bool logSetupWarnings = true;

    private Enemy3D _enemy;
    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _nextDecisionTime;
    private bool _prefersBeam;
    private bool _beamActive;
    private bool _hasSplit;

    private void Awake()
    {
        _enemy = GetComponent<Enemy3D>();
        _networkObject = GetComponent<NetworkObject>();
        projectileWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        beamWeapon ??= GetComponent<BeamWeapon3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        ApplyRoleWeaponState();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.SplitterBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        holdDistance = Mathf.Max(0f, stats.holdDistance);
        fullSpeedDistance = Mathf.Max(holdDistance + 0.01f, stats.fullSpeedDistance);
        projectileAimToleranceDegrees = Mathf.Clamp(stats.projectileAimToleranceDegrees, 0f, 180f);
        beamAimToleranceDegrees = Mathf.Clamp(stats.beamAimToleranceDegrees, 0f, 180f);
        projectilePreferredDistance = Mathf.Max(0f, stats.projectilePreferredDistance);
        beamPreferredDistance = Mathf.Max(0f, stats.beamPreferredDistance);
        mixedRangeBeamChance = Mathf.Clamp01(stats.mixedRangeBeamChance);
        mixedRangeWidth = Mathf.Max(0f, stats.mixedRangeWidth);
        decisionHoldDuration = Mathf.Max(0f, stats.decisionHoldDuration);
        minimumBeamRestartEnergy = Mathf.Max(0f, stats.minimumBeamRestartEnergy);
        projectileConvergenceDistance = Mathf.Max(0f, stats.projectileConvergenceDistance);
        splitCount = Mathf.Max(1, stats.splitCount);
        splitSpawnRadius = Mathf.Max(0f, stats.splitSpawnRadius);
        verticalSpawnJitter = Mathf.Max(0f, stats.verticalSpawnJitter);
        childScaleMultiplier = Mathf.Max(0.01f, stats.childScaleMultiplier);
        childMoveSpeedMultiplier = Mathf.Max(0f, stats.childMoveSpeedMultiplier);
        childMaxHealth = Mathf.Max(1f, stats.childMaxHealth);
        childMaxShield = Mathf.Max(0f, stats.childMaxShield);
    }

    private void OnEnable()
    {
        if (_enemy != null)
        {
            _enemy.Died -= HandleEnemyDied;
            _enemy.Died += HandleEnemyDied;
        }

        ApplyRoleWeaponState();
    }

    private void OnDisable()
    {
        if (_enemy != null)
        {
            _enemy.Died -= HandleEnemyDied;
        }

        StopBeam();
        flightController?.ClearFlightIntent();
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            StopBeam();
            flightController?.ClearFlightIntent();
            return;
        }

        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            StopBeam();
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            StopBeam();
            flightController?.SetFacingDirection(transform.forward);
            return;
        }

        Vector3 targetDirection = toTarget / distanceToTarget;
        UpdateMovement(targetDirection, distanceToTarget);
        RefreshActiveBeamAim();

        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
        Think(target, targetDirection, distanceToTarget);
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    public void InitializeSplitChildAsProjectile()
    {
        InitializeSplitChild(SplitterRole.ProjectileChild);
    }

    public void InitializeSplitChildAsBeam()
    {
        InitializeSplitChild(SplitterRole.BeamChild);
    }

    private void InitializeSplitChild(SplitterRole childRole)
    {
        role = childRole;
        _hasSplit = true;
        transform.localScale *= Mathf.Max(0.01f, childScaleMultiplier);
        _enemy?.OverrideMaxHealthAndShield(childMaxHealth, childMaxShield, refillCurrentValues: true);
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        if (flightController != null)
        {
            flightController.OverrideMoveSpeed(flightController.MoveSpeed * Mathf.Max(0f, childMoveSpeedMultiplier));
        }

        ApplyRoleWeaponState();
    }

    private void Think(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        bool wantsBeam = ResolveWantsBeam(distanceToTarget);
        if (wantsBeam)
        {
            bool usedBeam = TryUseBeam(target, out string beamResult);
            LogWeaponChoice("Beam", distanceToTarget, usedBeam ? "started/updated" : beamResult);
            return;
        }

        StopBeam();
        bool firedProjectile = TryFireProjectile(targetDirection, target);
        LogWeaponChoice("Projectile", distanceToTarget, firedProjectile ? "fired" : "blocked");
    }

    private bool ResolveWantsBeam(float distanceToTarget)
    {
        if (role == SplitterRole.BeamChild)
        {
            return true;
        }

        if (role == SplitterRole.ProjectileChild)
        {
            return false;
        }

        if (Time.time < _nextDecisionTime)
        {
            return _prefersBeam;
        }

        _nextDecisionTime = Time.time + Mathf.Max(0.01f, decisionHoldDuration);
        bool inMixedRange = IsInMixedRange(distanceToTarget);
        if (inMixedRange)
        {
            _prefersBeam = Random.value < Mathf.Clamp01(mixedRangeBeamChance);
            return _prefersBeam;
        }

        float projectileDistanceError = Mathf.Abs(distanceToTarget - projectilePreferredDistance);
        float beamDistanceError = Mathf.Abs(distanceToTarget - beamPreferredDistance);
        _prefersBeam = beamDistanceError < projectileDistanceError;
        return _prefersBeam;
    }

    private bool IsInMixedRange(float distanceToTarget)
    {
        float width = Mathf.Max(0f, mixedRangeWidth);
        return Mathf.Abs(distanceToTarget - projectilePreferredDistance) <= width
            || Mathf.Abs(distanceToTarget - beamPreferredDistance) <= width;
    }

    private void UpdateMovement(Vector3 targetDirection, float distanceToTarget)
    {
        float full = Mathf.Max(holdDistance + 0.01f, fullSpeedDistance);
        if (distanceToTarget <= holdDistance)
        {
            flightController?.SetFacingDirection(targetDirection);
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(targetDirection)
            : targetDirection;
        float speedScale = distanceToTarget >= full ? 1f : Mathf.InverseLerp(holdDistance, full, distanceToTarget);
        flightController?.SetFlightIntent(steeringDirection, targetDirection, speedScale, moveBackward: false);
    }

    private bool TryFireProjectile(Vector3 targetDirection, Entity3D target)
    {
        if (projectileWeapon == null || !projectileWeapon.enabled || !IsAimedAtTarget(targetDirection, projectileAimToleranceDegrees))
        {
            return false;
        }

        Vector3 convergencePoint = transform.position + (targetDirection.normalized * Mathf.Max(1f, projectileConvergenceDistance));
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePatternConverged(projectileWeapon, Faction3D.PlayerTeam, convergencePoint, target);
        }

        bool fired = projectileWeapon.TryFireAtFactionConverged(Faction3D.PlayerTeam, convergencePoint);
        if (fired)
        {
            attackReporter?.ReportAttack(target);
        }

        return fired;
    }

    private bool TryUseBeam(Entity3D target, out string blockReason)
    {
        blockReason = "blocked";
        if (beamWeapon == null || !beamWeapon.enabled)
        {
            blockReason = beamWeapon == null ? "blocked: beam weapon missing" : "blocked: beam weapon disabled";
            return false;
        }

        Vector3 fireDirection = beamWeapon.GetBeamForwardDirection();
        Vector3 beamOrigin = beamWeapon.GetBeamOrigin(fireDirection);
        Vector3 toTargetFromBeam = ResolveTargetPoint(target) - beamOrigin;
        float beamToTargetDistance = toTargetFromBeam.magnitude;
        if (beamToTargetDistance <= 0.0001f)
        {
            StopBeam();
            blockReason = "blocked: target overlaps beam origin";
            return false;
        }

        Vector3 targetDirectionFromBeam = toTargetFromBeam / beamToTargetDistance;
        float beamAimAngle = Vector3.Angle(fireDirection, targetDirectionFromBeam);
        float allowedAimAngle = Mathf.Max(0f, beamAimToleranceDegrees);
        bool isAimed = beamAimAngle <= allowedAimAngle;
        bool canStartOrSustain = CanStartOrSustainBeam(out string beamGateReason);
        if (!isAimed || !canStartOrSustain)
        {
            StopBeam();
            blockReason = !isAimed
                ? $"blocked: beam aim {beamAimAngle:F1}deg > {allowedAimAngle:F1}deg; fireDir={FormatVector(fireDirection)} targetDir={FormatVector(targetDirectionFromBeam)}"
                : $"blocked: {beamGateReason}";
            return false;
        }

        StartOrUpdateBeam(fireDirection, target);
        blockReason = $"started/updated: beam aim {beamAimAngle:F1}deg <= {allowedAimAngle:F1}deg";
        return true;
    }

    private bool CanStartOrSustainBeam()
    {
        return CanStartOrSustainBeam(out _);
    }

    private bool CanStartOrSustainBeam(out string blockReason)
    {
        blockReason = "beam ready";
        if (beamWeapon == null)
        {
            blockReason = "beam weapon missing";
            return false;
        }

        if (beamWeapon.IsBeamActive)
        {
            blockReason = "beam already active";
            return true;
        }

        if (!beamWeapon.CanStartBeamNow())
        {
            blockReason = $"beam weapon cannot start now; remainingEnergy={beamWeapon.GetRemainingBeamEnergy():F1}, minimumRestartEnergy={Mathf.Max(0f, minimumBeamRestartEnergy):F1}";
            return false;
        }

        float remainingEnergy = beamWeapon.GetRemainingBeamEnergy();
        float minimumRestartEnergy = Mathf.Max(0f, minimumBeamRestartEnergy);
        bool hasRestartEnergy = remainingEnergy <= 0f || remainingEnergy + 0.001f >= minimumRestartEnergy;
        if (!hasRestartEnergy)
        {
            blockReason = $"beam restart energy too low; remainingEnergy={remainingEnergy:F1}, minimumRestartEnergy={minimumRestartEnergy:F1}";
        }

        return hasRestartEnergy;
    }

    private void RefreshActiveBeamAim()
    {
        if (beamWeapon == null || !beamWeapon.IsBeamActive)
        {
            _beamActive = false;
            return;
        }

        Vector3 fireDirection = beamWeapon.GetBeamForwardDirection();
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.UpdateBeamAim(beamWeapon, fireDirection);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamAim(fireDirection);
        }

        _beamActive = true;
    }

    private void StartOrUpdateBeam(Vector3 aimDirection, Entity3D target)
    {
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, true, aimDirection, target);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamAim(aimDirection);
            beamWeapon.ApplyNetworkBeamState(true, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            attackReporter?.ReportSustainedAttack(target, Mathf.Max(thinkInterval * 2f, 0.25f));
        }

        _beamActive = true;
    }

    private void StopBeam()
    {
        if (!_beamActive && (beamWeapon == null || !beamWeapon.IsBeamActive))
        {
            return;
        }

        if (beamWeapon == null)
        {
            _beamActive = false;
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, false, transform.forward);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamState(false, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            attackReporter?.StopSustainedAttack(null);
        }

        _beamActive = false;
    }

    private void HandleEnemyDied(Entity3D deadEntity)
    {
        if (_hasSplit || role != SplitterRole.ParentHybrid || !HasBrainAuthority())
        {
            return;
        }

        _hasSplit = true;
        StopBeam();

        InvasionWaveManager3D manager = ResolveWaveManager();
        if (manager == null)
        {
            LogSetupWarning("cannot split because no active InvasionWaveManager3D was found.");
            return;
        }

        int childCount = Mathf.Max(0, splitCount);
        for (int i = 0; i < childCount; i++)
        {
            Vector3 spawnPosition = ResolveChildSpawnPosition(i, childCount);
            Vector3 childForward = ResolveChildForward(spawnPosition);
            Quaternion spawnRotation = Quaternion.LookRotation(childForward, ResolveChildUp(childForward));
            SplitterRole childRole = i == 0 ? SplitterRole.BeamChild : SplitterRole.ProjectileChild;
            GameObject prefab = splitterPrefab != null ? splitterPrefab : gameObject;
            if (splitterPrefab == null)
            {
                LogSetupWarning("is using its live GameObject as the split source because Splitter Prefab is not assigned. Assign this same prefab asset to avoid cloning runtime-only state.");
            }

            Enemy3D child = manager.SpawnEnemyAt(prefab, spawnPosition, spawnRotation, spawnedObject => ConfigureChildBeforeNetworkSpawn(spawnedObject, childRole));
            SyncChildToClientsIfNeeded(child);
        }
    }

    private void ConfigureChildBeforeNetworkSpawn(GameObject childObject, SplitterRole childRole)
    {
        if (childObject == null)
        {
            return;
        }

        SplitterEnemyBrain3D childBrain = childObject.GetComponent<SplitterEnemyBrain3D>();
        if (childBrain == null)
        {
            LogSetupWarning("spawned a child from the same prefab, but the child has no SplitterEnemyBrain3D.");
            return;
        }

        childBrain.InitializeSplitChild(childRole);
    }

    private void SyncChildToClientsIfNeeded(Enemy3D child)
    {
        if (!NetTickUtil.IsActive || child == null)
        {
            return;
        }

        SplitterEnemyBrain3D childBrain = child.GetComponent<SplitterEnemyBrain3D>();
        if (childBrain != null && childBrain.IsSpawned && childBrain.IsServer)
        {
            childBrain.ApplySplitChildClientRpc((int)childBrain.role, childBrain.transform.localScale, childBrain.childMaxHealth, childBrain.childMaxShield);
        }
    }

    [ClientRpc]
    private void ApplySplitChildClientRpc(int syncedRole, Vector3 syncedScale, float syncedMaxHealth, float syncedMaxShield)
    {
        if (IsServer)
        {
            return;
        }

        role = (SplitterRole)Mathf.Clamp(syncedRole, 0, 2);
        _hasSplit = true;
        transform.localScale = syncedScale;
        _enemy ??= GetComponent<Enemy3D>();
        _enemy?.OverrideMaxHealthAndShield(syncedMaxHealth, syncedMaxShield, refillCurrentValues: true);
        ApplyRoleWeaponState();
    }

    private InvasionWaveManager3D ResolveWaveManager()
    {
        if (waveManager != null)
        {
            return waveManager;
        }

#if UNITY_2023_1_OR_NEWER
        waveManager = FindFirstObjectByType<InvasionWaveManager3D>();
#else
        waveManager = FindObjectOfType<InvasionWaveManager3D>();
#endif
        return waveManager;
    }

    private void ApplyRoleWeaponState()
    {
        if (projectileWeapon != null)
        {
            projectileWeapon.enabled = role != SplitterRole.BeamChild;
        }

        if (beamWeapon != null)
        {
            if (role == SplitterRole.ProjectileChild)
            {
                StopBeam();
            }

            beamWeapon.enabled = role != SplitterRole.ProjectileChild;
        }
    }

    private Vector3 ResolveChildSpawnPosition(int index, int childCount)
    {
        float radius = Mathf.Max(0f, splitSpawnRadius);
        if (childCount <= 1 || radius <= 0f)
        {
            return transform.position;
        }

        float angle = (Mathf.PI * 2f * index) / childCount;
        Vector3 lateralOffset = (transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle)) * radius;
        float verticalOffset = verticalSpawnJitter > 0f ? Random.Range(-verticalSpawnJitter, verticalSpawnJitter) : 0f;
        return transform.position + lateralOffset + (transform.up * verticalOffset);
    }

    private Vector3 ResolveChildForward(Vector3 spawnPosition)
    {
        Vector3 awayFromParent = spawnPosition - transform.position;
        if (awayFromParent.sqrMagnitude > 0.0001f)
        {
            return awayFromParent.normalized;
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
    }

    private Vector3 ResolveChildUp(Vector3 childForward)
    {
        Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up : Vector3.up;
        Vector3 forward = childForward.sqrMagnitude > 0.0001f ? childForward.normalized : Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.98f)
        {
            up = Vector3.up;
        }

        if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.98f)
        {
            up = Vector3.right;
        }

        return up;
    }

    private bool IsAimedAtTarget(Vector3 targetDirection, float toleranceDegrees)
    {
        return Vector3.Angle(transform.forward, targetDirection) <= Mathf.Max(0f, toleranceDegrees);
    }

    private static Vector3 ResolveTargetPoint(Entity3D target)
    {
        Collider targetCollider = target != null ? target.GetComponentInChildren<Collider>() : null;
        return targetCollider != null ? targetCollider.bounds.center : target != null ? target.transform.position : Vector3.zero;
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

    private void LogSetupWarning(string message)
    {
        if (!logSetupWarnings)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(SplitterEnemyBrain3D)}] {name} {message}", this);
    }

    private void LogWeaponChoice(string choice, float distanceToTarget, string result)
    {
        if (!logWeaponChoices)
        {
            return;
        }

        Debug.Log($"[{nameof(SplitterEnemyBrain3D)}] {name} chose {choice} at {distanceToTarget:F1}m: {result}. Role={role}", this);
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }
}
