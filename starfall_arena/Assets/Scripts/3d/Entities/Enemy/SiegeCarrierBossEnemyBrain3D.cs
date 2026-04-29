using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class SiegeCarrierBossEnemyBrain3D : NetworkBehaviour
{
    private enum BossPattern
    {
        None,
        LaggingMachineGunRake,
        PredictiveSplitFan,
        BeamFence,
        CurtainWithEscapeDoor
    }

    [Header("Pattern Weapons")]
    [Tooltip("Projectile weapons used by the lagging machine-gun rake. Wire staggered turret weapons here for the best readable hardpoint sequence.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] laggingRakeWeapons;

    [Tooltip("Projectile weapons used as fan lanes. Each component represents one authored lane; the brain supplies the per-lane fire direction.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] predictiveFanWeapons;

    [Tooltip("Projectile weapons used by the escape-door curtain. More authored weapons allow more simultaneous or staggered lanes, but the boss still obeys Max Shots Per Pattern.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] curtainWeapons;

    [Tooltip("Beam weapons used by the beam fence. NetEnemyCombat3D now replays these by component index, so keep the same BeamWeapon3D component order on host and client prefabs.")]
    [SerializeField] private BeamWeapon3D[] beamFenceWeapons;

    [Tooltip("Optional charge/telegraph visuals paired by index with Beam Fence Weapons. These are presentation-only; beam damage still comes from BeamWeapon3D.")]
    [SerializeField] private ProjectileChargeTelegraph3D[] beamFenceTelegraphs;

    [Header("References")]
    [Tooltip("AI flight motor that lets the boss slowly approach outside range and face the current player while anchored.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. The Siege Carrier expects this to target PlayerTeam.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("No-target search fallback. The boss uses this only when it cannot see a player-team target.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Enemy combat broker used for server-authoritative projectile and beam replication.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Header("Think Loop")]
    [Tooltip("Seconds between high-level boss decisions. Pattern aim refresh may still run more often during active beams.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Tooltip("Seconds between target-position history samples used by the lagging rake.")]
    [SerializeField] private float targetHistorySampleInterval = 0.12f;

    [Tooltip("Number of recent target positions retained for lagging attacks. Higher values allow older rake targets but use a little more memory.")]
    [Range(2, 32)]
    [SerializeField] private int targetHistorySamples = 16;

    [Header("Movement Bands")]
    [Tooltip("Distance where the Siege Carrier is allowed to run attack patterns.")]
    [SerializeField] private float engagementRange = 260f;

    [Tooltip("Extra distance beyond Engagement Range where the boss slowly approaches instead of idling.")]
    [SerializeField] private float approachRangeBuffer = 180f;

    [Tooltip("Speed scale used while the boss is outside Engagement Range but inside the approach buffer.")]
    [Range(0f, 1f)]
    [SerializeField] private float approachSpeedScale = 0.25f;

    [Tooltip("Tiny speed scale used while in range so the carrier can creep forward without becoming a chaser. Set to 0 for a fully stationary boss.")]
    [Range(0f, 1f)]
    [SerializeField] private float anchorCreepSpeedScale = 0.05f;

    [Header("Sequencer")]
    [Tooltip("Minimum seconds between major attack patterns before phase multipliers are applied.")]
    [SerializeField] private float minimumPatternCooldown = 1.6f;

    [Tooltip("Hard cap on projectile shots per single pattern activation. This is the main bullet-hell performance/readability safety valve.")]
    [Range(1, 128)]
    [SerializeField] private int maxShotsPerPattern = 32;

    [Tooltip("Health percentage where phase two begins. Later phases shorten pattern cooldowns without increasing projectile budgets.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float phaseTwoHealthPercent = 0.66f;

    [Tooltip("Health percentage where phase three begins. Later phases shorten pattern cooldowns without increasing projectile budgets.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float phaseThreeHealthPercent = 0.33f;

    [Tooltip("Pattern cooldown multiplier while health is at or below Phase Two Health Percent.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float phaseTwoCooldownMultiplier = 0.85f;

    [Tooltip("Pattern cooldown multiplier while health is at or below Phase Three Health Percent.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float phaseThreeCooldownMultiplier = 0.7f;

    [Header("Lagging Machine-Gun Rake")]
    [Tooltip("Maximum rake shots in one activation before the global Max Shots Per Pattern cap is also applied.")]
    [SerializeField] private int rakeShotCount = 14;

    [Tooltip("Seconds between rake shots.")]
    [SerializeField] private float rakeShotInterval = 0.12f;

    [Tooltip("How far behind the target's current position the rake aims, in seconds. This rewards clean drift paths.")]
    [SerializeField] private float rakeHistorySeconds = 0.6f;

    [Header("Predictive Split Fan")]
    [Tooltip("Number of fan lanes attempted. The actual count is also limited by the number of configured Predictive Fan Weapons and Max Shots Per Pattern.")]
    [Range(1, 31)]
    [SerializeField] private int fanLaneCount = 5;

    [Tooltip("Total fan spread in degrees centered on the target/lead direction.")]
    [Range(0f, 180f)]
    [SerializeField] private float fanTotalSpreadDegrees = 34f;

    [Tooltip("Seconds between fan lanes. Use a small value for a near-simultaneous fan while still avoiding one-frame projectile spikes.")]
    [SerializeField] private float fanLaneInterval = 0.04f;

    [Tooltip("If true, the fan centers on a simple target-velocity lead point instead of the target's current position.")]
    [SerializeField] private bool fanUseLeadAim = true;

    [Tooltip("Projectile speed used for simple fan lead calculation. If 0, the first fan weapon's configured speed is used.")]
    [SerializeField] private float fanLeadProjectileSpeed = 140f;

    [Header("Curtain With Escape Door")]
    [Tooltip("Number of curtain lanes attempted across the arc, including lanes skipped for the escape door.")]
    [Range(1, 31)]
    [SerializeField] private int curtainLaneCount = 13;

    [Tooltip("Total curtain arc in degrees centered around the target direction.")]
    [Range(0f, 270f)]
    [SerializeField] private float curtainArcDegrees = 140f;

    [Tooltip("Width in degrees of the intentionally empty escape sector inside the curtain.")]
    [Range(0f, 180f)]
    [SerializeField] private float curtainEscapeDoorDegrees = 26f;

    [Tooltip("Degrees the escape door shifts after each curtain activation, so the safe lane does not always appear in the same place.")]
    [SerializeField] private float curtainDoorDriftDegrees = 18f;

    [Tooltip("Seconds between curtain lanes.")]
    [SerializeField] private float curtainLaneInterval = 0.05f;

    [Header("Beam Fence")]
    [Tooltip("Seconds of warning before beam fence damage begins.")]
    [SerializeField] private float beamFenceTelegraphDuration = 0.75f;

    [Tooltip("Seconds the damaging beam fence remains active.")]
    [SerializeField] private float beamFenceActiveDuration = 1.2f;

    [Tooltip("Maximum beam hardpoints used in one fence activation.")]
    [Range(1, 16)]
    [SerializeField] private int beamFenceMaxBeams = 4;

    [Tooltip("Seconds between beam aim refreshes while the fence is active. Lower is smoother but sends more network updates.")]
    [SerializeField] private float beamFenceAimRefreshInterval = 0.05f;

    private NetworkObject _networkObject;
    private Enemy3D _enemy;
    private Vector3[] _targetHistory;
    private int _historyWriteIndex;
    private int _historyCount;
    private float _nextHistorySampleTime;
    private float _nextThinkTime;
    private float _nextPatternAllowedTime;
    private float _nextPatternStepTime;
    private float _patternEndsAt;
    private float _nextBeamAimRefreshTime;
    private float _curtainDoorOffset;
    private int _patternCursor;
    private int _patternShotsFired;
    private int _patternStepIndex;
    private int _activeBeamCount;
    private BossPattern _activePattern;
    private Entity3D _currentTarget;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();
        _enemy = GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        EnsureTargetHistoryBuffer();
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        targetHistorySampleInterval = Mathf.Max(0.02f, targetHistorySampleInterval);
        targetHistorySamples = Mathf.Clamp(targetHistorySamples, 2, 32);
        engagementRange = Mathf.Max(0f, engagementRange);
        approachRangeBuffer = Mathf.Max(0f, approachRangeBuffer);
        approachSpeedScale = Mathf.Clamp01(approachSpeedScale);
        anchorCreepSpeedScale = Mathf.Clamp01(anchorCreepSpeedScale);
        minimumPatternCooldown = Mathf.Max(0f, minimumPatternCooldown);
        maxShotsPerPattern = Mathf.Clamp(maxShotsPerPattern, 1, 128);
        phaseTwoHealthPercent = Mathf.Clamp(phaseTwoHealthPercent, 0.01f, 1f);
        phaseThreeHealthPercent = Mathf.Clamp(phaseThreeHealthPercent, 0.01f, phaseTwoHealthPercent);
        phaseTwoCooldownMultiplier = Mathf.Clamp(phaseTwoCooldownMultiplier, 0.1f, 1f);
        phaseThreeCooldownMultiplier = Mathf.Clamp(phaseThreeCooldownMultiplier, 0.1f, 1f);
        rakeShotCount = Mathf.Max(1, rakeShotCount);
        rakeShotInterval = Mathf.Max(0.01f, rakeShotInterval);
        rakeHistorySeconds = Mathf.Max(0f, rakeHistorySeconds);
        fanLaneCount = Mathf.Clamp(fanLaneCount, 1, 31);
        fanTotalSpreadDegrees = Mathf.Clamp(fanTotalSpreadDegrees, 0f, 180f);
        fanLaneInterval = Mathf.Max(0.01f, fanLaneInterval);
        fanLeadProjectileSpeed = Mathf.Max(0f, fanLeadProjectileSpeed);
        curtainLaneCount = Mathf.Clamp(curtainLaneCount, 1, 31);
        curtainArcDegrees = Mathf.Clamp(curtainArcDegrees, 0f, 270f);
        curtainEscapeDoorDegrees = Mathf.Clamp(curtainEscapeDoorDegrees, 0f, 180f);
        curtainLaneInterval = Mathf.Max(0.01f, curtainLaneInterval);
        beamFenceTelegraphDuration = Mathf.Max(0f, beamFenceTelegraphDuration);
        beamFenceActiveDuration = Mathf.Max(0.01f, beamFenceActiveDuration);
        beamFenceMaxBeams = Mathf.Clamp(beamFenceMaxBeams, 1, 16);
        beamFenceAimRefreshInterval = Mathf.Max(0.01f, beamFenceAimRefreshInterval);
    }

    private void OnDisable()
    {
        StopActiveBeams();
        StopBeamTelegraphs(immediate: true);
        _activePattern = BossPattern.None;
        _currentTarget = null;
        flightController?.ClearFlightIntent();
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            flightController?.ClearFlightIntent();
            _activePattern = BossPattern.None;
            _currentTarget = null;
            return;
        }

        Entity3D target = ResolveTarget();
        SampleTargetHistory(target);

        if (!IsTargetValid(target))
        {
            CancelActivePattern();
            PatrolOrClearFlightIntent();
            return;
        }

        UpdateMovement(target);
        if (!IsInsideMaxEngagement(target))
        {
            CancelActivePattern();
            return;
        }

        TickActivePattern(target);
        if (_activePattern == BossPattern.None && Time.time >= _nextPatternAllowedTime)
        {
            StartNextPattern(target);
        }
    }

    public void ApplyProfile(EnemyBalanceProfile3D.SiegeCarrierBossBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        targetHistorySampleInterval = Mathf.Max(0.02f, stats.targetHistorySampleInterval);
        targetHistorySamples = Mathf.Clamp(stats.targetHistorySamples, 2, 32);
        engagementRange = Mathf.Max(0f, stats.engagementRange);
        approachRangeBuffer = Mathf.Max(0f, stats.approachRangeBuffer);
        approachSpeedScale = Mathf.Clamp01(stats.approachSpeedScale);
        anchorCreepSpeedScale = Mathf.Clamp01(stats.anchorCreepSpeedScale);
        minimumPatternCooldown = Mathf.Max(0f, stats.minimumPatternCooldown);
        maxShotsPerPattern = Mathf.Clamp(stats.maxShotsPerPattern, 1, 128);
        phaseTwoHealthPercent = Mathf.Clamp(stats.phaseTwoHealthPercent, 0.01f, 1f);
        phaseThreeHealthPercent = Mathf.Clamp(stats.phaseThreeHealthPercent, 0.01f, phaseTwoHealthPercent);
        phaseTwoCooldownMultiplier = Mathf.Clamp(stats.phaseTwoCooldownMultiplier, 0.1f, 1f);
        phaseThreeCooldownMultiplier = Mathf.Clamp(stats.phaseThreeCooldownMultiplier, 0.1f, 1f);
        rakeShotCount = Mathf.Max(1, stats.rakeShotCount);
        rakeShotInterval = Mathf.Max(0.01f, stats.rakeShotInterval);
        rakeHistorySeconds = Mathf.Max(0f, stats.rakeHistorySeconds);
        fanLaneCount = Mathf.Clamp(stats.fanLaneCount, 1, 31);
        fanTotalSpreadDegrees = Mathf.Clamp(stats.fanTotalSpreadDegrees, 0f, 180f);
        fanLaneInterval = Mathf.Max(0.01f, stats.fanLaneInterval);
        fanUseLeadAim = stats.fanUseLeadAim;
        fanLeadProjectileSpeed = Mathf.Max(0f, stats.fanLeadProjectileSpeed);
        curtainLaneCount = Mathf.Clamp(stats.curtainLaneCount, 1, 31);
        curtainArcDegrees = Mathf.Clamp(stats.curtainArcDegrees, 0f, 270f);
        curtainEscapeDoorDegrees = Mathf.Clamp(stats.curtainEscapeDoorDegrees, 0f, 180f);
        curtainDoorDriftDegrees = stats.curtainDoorDriftDegrees;
        curtainLaneInterval = Mathf.Max(0.01f, stats.curtainLaneInterval);
        beamFenceTelegraphDuration = Mathf.Max(0f, stats.beamFenceTelegraphDuration);
        beamFenceActiveDuration = Mathf.Max(0.01f, stats.beamFenceActiveDuration);
        beamFenceMaxBeams = Mathf.Clamp(stats.beamFenceMaxBeams, 1, 16);
        beamFenceAimRefreshInterval = Mathf.Max(0.01f, stats.beamFenceAimRefreshInterval);
        EnsureTargetHistoryBuffer();
    }

    private Entity3D ResolveTarget()
    {
        if (Time.time >= _nextThinkTime || !IsTargetValid(_currentTarget))
        {
            _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
            _currentTarget = targetSensor != null ? targetSensor.GetTarget() : null;
        }

        return _currentTarget;
    }

    private void UpdateMovement(Entity3D target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 targetDirection = toTarget.normalized;
        if (IsInsideEngagementRange(target))
        {
            if (anchorCreepSpeedScale > 0f)
            {
                flightController?.SetFlightIntent(targetDirection, targetDirection, anchorCreepSpeedScale, moveBackward: false);
            }
            else
            {
                flightController?.SetFacingDirection(targetDirection);
            }
            return;
        }

        flightController?.SetFlightIntent(targetDirection, targetDirection, approachSpeedScale, moveBackward: false);
    }

    private void TickActivePattern(Entity3D target)
    {
        switch (_activePattern)
        {
            case BossPattern.LaggingMachineGunRake:
                TickLaggingRake();
                break;
            case BossPattern.PredictiveSplitFan:
                TickPredictiveFan(target);
                break;
            case BossPattern.BeamFence:
                TickBeamFence(target);
                break;
            case BossPattern.CurtainWithEscapeDoor:
                TickCurtain(target);
                break;
        }
    }

    private void StartNextPattern(Entity3D target)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            BossPattern next = (BossPattern)((_patternCursor % 4) + 1);
            _patternCursor++;
            if (!CanRunPattern(next))
            {
                continue;
            }

            BeginPattern(next, target);
            return;
        }

        _nextPatternAllowedTime = Time.time + ResolvePatternCooldown();
    }

    private void BeginPattern(BossPattern pattern, Entity3D target)
    {
        _activePattern = pattern;
        _patternShotsFired = 0;
        _patternStepIndex = 0;
        _nextPatternStepTime = Time.time;

        if (pattern == BossPattern.BeamFence)
        {
            _activeBeamCount = Mathf.Min(beamFenceWeapons != null ? beamFenceWeapons.Length : 0, beamFenceMaxBeams);
            _patternEndsAt = Time.time + beamFenceTelegraphDuration + beamFenceActiveDuration;
            StartBeamTelegraphs();
            return;
        }

        if (pattern == BossPattern.CurtainWithEscapeDoor)
        {
            _curtainDoorOffset = NormalizeSignedAngle(_curtainDoorOffset + curtainDoorDriftDegrees);
        }
    }

    private void TickLaggingRake()
    {
        int shotLimit = Mathf.Min(rakeShotCount, maxShotsPerPattern);
        if (_patternStepIndex >= shotLimit || _patternShotsFired >= maxShotsPerPattern)
        {
            FinishPattern();
            return;
        }

        if (Time.time < _nextPatternStepTime)
        {
            return;
        }

        EnemyProjectileWeaponBase3D weapon = ResolveWeapon(laggingRakeWeapons, _patternStepIndex);
        Vector3 aimPoint = ResolveHistoricalTargetPoint();
        if (weapon != null && FireProjectileConverged(weapon, aimPoint))
        {
            _patternShotsFired++;
        }

        _patternStepIndex++;
        _nextPatternStepTime = Time.time + rakeShotInterval;
    }

    private void TickPredictiveFan(Entity3D target)
    {
        int laneLimit = Mathf.Min(fanLaneCount, maxShotsPerPattern);
        if (_patternStepIndex >= laneLimit || _patternShotsFired >= maxShotsPerPattern)
        {
            FinishPattern();
            return;
        }

        if (Time.time < _nextPatternStepTime)
        {
            return;
        }

        EnemyProjectileWeaponBase3D weapon = ResolveWeapon(predictiveFanWeapons, _patternStepIndex);
        if (weapon != null)
        {
            Vector3 centerDirection = ResolveFanCenterDirection(target);
            Vector3 laneDirection = RotateDirectionAroundBossUp(centerDirection, ResolveLaneAngle(_patternStepIndex, laneLimit, fanTotalSpreadDegrees));
            if (FireProjectileDirection(weapon, laneDirection))
            {
                _patternShotsFired++;
            }
        }

        _patternStepIndex++;
        _nextPatternStepTime = Time.time + fanLaneInterval;
    }

    private void TickCurtain(Entity3D target)
    {
        int laneLimit = Mathf.Min(curtainLaneCount, maxShotsPerPattern);
        if (_patternStepIndex >= laneLimit || _patternShotsFired >= maxShotsPerPattern)
        {
            FinishPattern();
            return;
        }

        if (Time.time < _nextPatternStepTime)
        {
            return;
        }

        Vector3 centerDirection = ResolveDirectionToTarget(target);
        float laneAngle = ResolveLaneAngle(_patternStepIndex, laneLimit, curtainArcDegrees);
        if (Mathf.Abs(NormalizeSignedAngle(laneAngle - _curtainDoorOffset)) > curtainEscapeDoorDegrees * 0.5f)
        {
            EnemyProjectileWeaponBase3D weapon = ResolveWeapon(curtainWeapons, _patternStepIndex);
            Vector3 fireDirection = RotateDirectionAroundBossUp(centerDirection, laneAngle);
            if (weapon != null && FireProjectileDirection(weapon, fireDirection))
            {
                _patternShotsFired++;
            }
        }

        _patternStepIndex++;
        _nextPatternStepTime = Time.time + curtainLaneInterval;
    }

    private void TickBeamFence(Entity3D target)
    {
        if (_activeBeamCount <= 0)
        {
            FinishPattern();
            return;
        }

        float activeStartTime = _patternEndsAt - beamFenceActiveDuration;
        if (Time.time >= activeStartTime && Time.time < _patternEndsAt)
        {
            if (_patternStepIndex == 0)
            {
                StopBeamTelegraphs(immediate: false);
                SetBeamFenceState(target, isFiring: true);
                _patternStepIndex = 1;
                _nextBeamAimRefreshTime = Time.time;
            }

            if (Time.time >= _nextBeamAimRefreshTime)
            {
                RefreshBeamFenceAim(target);
                _nextBeamAimRefreshTime = Time.time + beamFenceAimRefreshInterval;
            }
            return;
        }

        if (Time.time >= _patternEndsAt)
        {
            StopActiveBeams();
            FinishPattern();
        }
    }

    private void FinishPattern()
    {
        StopBeamTelegraphs(immediate: false);
        StopActiveBeams();
        _activePattern = BossPattern.None;
        _nextPatternAllowedTime = Time.time + ResolvePatternCooldown();
    }

    private void CancelActivePattern()
    {
        if (_activePattern == BossPattern.None)
        {
            return;
        }

        StopBeamTelegraphs(immediate: true);
        StopActiveBeams();
        _activePattern = BossPattern.None;
        _nextPatternAllowedTime = Time.time + ResolvePatternCooldown();
    }

    private bool CanRunPattern(BossPattern pattern)
    {
        switch (pattern)
        {
            case BossPattern.LaggingMachineGunRake:
                return HasAnyWeapon(laggingRakeWeapons);
            case BossPattern.PredictiveSplitFan:
                return HasAnyWeapon(predictiveFanWeapons);
            case BossPattern.BeamFence:
                return beamFenceWeapons != null && beamFenceWeapons.Length > 0;
            case BossPattern.CurtainWithEscapeDoor:
                return HasAnyWeapon(curtainWeapons);
            default:
                return false;
        }
    }

    private bool FireProjectileDirection(EnemyProjectileWeaponBase3D weapon, Vector3 fireDirection)
    {
        if (weapon == null || fireDirection.sqrMagnitude <= 0.0001f || _patternShotsFired >= maxShotsPerPattern)
        {
            return false;
        }

        return NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned
            ? netEnemyCombat.TryFireProjectilePattern(weapon, Faction3D.PlayerTeam, fireDirection.normalized)
            : weapon.TryFireAtFaction(Faction3D.PlayerTeam, fireDirection.normalized);
    }

    private bool FireProjectileConverged(EnemyProjectileWeaponBase3D weapon, Vector3 convergencePoint)
    {
        if (weapon == null || _patternShotsFired >= maxShotsPerPattern)
        {
            return false;
        }

        return NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned
            ? netEnemyCombat.TryFireProjectilePatternConverged(weapon, Faction3D.PlayerTeam, convergencePoint)
            : weapon.TryFireAtFactionConverged(Faction3D.PlayerTeam, convergencePoint);
    }

    private void SetBeamFenceState(Entity3D target, bool isFiring)
    {
        Vector3 aimDirection = ResolveDirectionToTarget(target);
        for (int i = 0; i < _activeBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            Vector3 beamDirection = ResolveBeamFenceDirection(beamWeapon, aimDirection, i, _activeBeamCount);
            if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
            {
                netEnemyCombat.SetBeamState(beamWeapon, isFiring, beamDirection);
            }
            else
            {
                if (isFiring)
                {
                    beamWeapon.ApplyNetworkBeamAim(beamDirection);
                }

                beamWeapon.ApplyNetworkBeamState(isFiring, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            }
        }
    }

    private void RefreshBeamFenceAim(Entity3D target)
    {
        Vector3 aimDirection = ResolveDirectionToTarget(target);
        for (int i = 0; i < _activeBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            Vector3 beamDirection = ResolveBeamFenceDirection(beamWeapon, aimDirection, i, _activeBeamCount);
            if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
            {
                netEnemyCombat.UpdateBeamAim(beamWeapon, beamDirection);
            }
            else
            {
                beamWeapon.ApplyNetworkBeamAim(beamDirection);
            }
        }
    }

    private void StopActiveBeams()
    {
        if (beamFenceWeapons == null)
        {
            return;
        }

        int count = _activeBeamCount > 0 ? Mathf.Min(_activeBeamCount, beamFenceWeapons.Length) : beamFenceWeapons.Length;
        for (int i = 0; i < count; i++)
        {
            BeamWeapon3D beamWeapon = beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
            {
                netEnemyCombat.SetBeamState(beamWeapon, false, transform.forward);
            }
            else
            {
                beamWeapon.ApplyNetworkBeamState(false, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
            }
        }
    }

    private void StartBeamTelegraphs()
    {
        StartBeamTelegraphsLocal(beamFenceTelegraphDuration, elapsed: 0f);
        if (ShouldReplicateTelegraph())
        {
            StartBeamFenceTelegraphClientRpc(beamFenceTelegraphDuration, ResolveNetworkServerTime());
        }
    }

    private void StartBeamTelegraphsLocal(float duration, float elapsed)
    {
        if (beamFenceTelegraphs == null)
        {
            return;
        }

        int count = Mathf.Min(_activeBeamCount, beamFenceTelegraphs.Length);
        for (int i = 0; i < count; i++)
        {
            beamFenceTelegraphs[i]?.PlayCharge(duration, elapsed);
        }
    }

    private void StopBeamTelegraphs(bool immediate)
    {
        StopBeamTelegraphsLocal(immediate);
        if (!immediate && ShouldReplicateTelegraph())
        {
            StopBeamFenceTelegraphClientRpc();
        }
    }

    private void StopBeamTelegraphsLocal(bool immediate)
    {
        if (beamFenceTelegraphs == null)
        {
            return;
        }

        for (int i = 0; i < beamFenceTelegraphs.Length; i++)
        {
            beamFenceTelegraphs[i]?.StopCharge(immediate);
        }
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

    [ClientRpc]
    private void StartBeamFenceTelegraphClientRpc(float duration, double serverStartTime)
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

        StartBeamTelegraphsLocal(duration, elapsed);
    }

    [ClientRpc]
    private void StopBeamFenceTelegraphClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        StopBeamTelegraphsLocal(immediate: false);
    }

    private void SampleTargetHistory(Entity3D target)
    {
        if (!IsTargetValid(target) || Time.time < _nextHistorySampleTime)
        {
            return;
        }

        EnsureTargetHistoryBuffer();
        _targetHistory[_historyWriteIndex] = target.transform.position;
        _historyWriteIndex = (_historyWriteIndex + 1) % _targetHistory.Length;
        _historyCount = Mathf.Min(_historyCount + 1, _targetHistory.Length);
        _nextHistorySampleTime = Time.time + targetHistorySampleInterval;
    }

    private Vector3 ResolveHistoricalTargetPoint()
    {
        if (_historyCount <= 0 || _targetHistory == null || _targetHistory.Length == 0)
        {
            return IsTargetValid(_currentTarget) ? _currentTarget.transform.position : transform.position + transform.forward * 20f;
        }

        int samplesBehind = Mathf.Clamp(Mathf.RoundToInt(rakeHistorySeconds / Mathf.Max(0.02f, targetHistorySampleInterval)), 0, _historyCount - 1);
        int index = _historyWriteIndex - 1 - samplesBehind;
        while (index < 0)
        {
            index += _targetHistory.Length;
        }

        return _targetHistory[index % _targetHistory.Length];
    }

    private void EnsureTargetHistoryBuffer()
    {
        int size = Mathf.Clamp(targetHistorySamples, 2, 32);
        if (_targetHistory != null && _targetHistory.Length == size)
        {
            return;
        }

        _targetHistory = new Vector3[size];
        _historyWriteIndex = 0;
        _historyCount = 0;
    }

    private Vector3 ResolveFanCenterDirection(Entity3D target)
    {
        Vector3 aimPoint = target.transform.position;
        if (fanUseLeadAim)
        {
            float projectileSpeed = fanLeadProjectileSpeed;
            if (projectileSpeed <= 0f)
            {
                EnemyProjectileWeaponBase3D firstWeapon = ResolveWeapon(predictiveFanWeapons, 0);
                projectileSpeed = firstWeapon != null ? firstWeapon.WeaponConfig.speed : 0f;
            }

            if (projectileSpeed > 0.0001f)
            {
                Vector3 velocity = ResolveTargetVelocity(target);
                float travelTime = Vector3.Distance(transform.position, target.transform.position) / projectileSpeed;
                aimPoint += velocity * travelTime;
            }
        }

        Vector3 direction = aimPoint - transform.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    private Vector3 ResolveBeamFenceDirection(BeamWeapon3D beamWeapon, Vector3 centerDirection, int index, int count)
    {
        if (beamWeapon == null)
        {
            return centerDirection;
        }

        float spread = Mathf.Min(40f, Mathf.Max(12f, count * 10f));
        float angle = ResolveLaneAngle(index, Mathf.Max(1, count), spread);
        return RotateDirectionAroundBossUp(centerDirection, angle);
    }

    private Vector3 ResolveDirectionToTarget(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        }

        Vector3 direction = target.transform.position - transform.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    private Vector3 ResolveTargetVelocity(Entity3D target)
    {
        Rigidbody rb = target != null ? target.GetComponent<Rigidbody>() : null;
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }

    private Vector3 RotateDirectionAroundBossUp(Vector3 direction, float angleDegrees)
    {
        Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
        return Quaternion.AngleAxis(angleDegrees, up) * direction.normalized;
    }

    private float ResolveLaneAngle(int index, int count, float totalSpreadDegrees)
    {
        if (count <= 1 || totalSpreadDegrees <= 0f)
        {
            return 0f;
        }

        float t = count > 1 ? index / (float)(count - 1) : 0.5f;
        return Mathf.Lerp(-totalSpreadDegrees * 0.5f, totalSpreadDegrees * 0.5f, t);
    }

    private float ResolvePatternCooldown()
    {
        float multiplier = 1f;
        if (_enemy != null && _enemy.MaxHealth > 0f)
        {
            float healthPercent = _enemy.CurrentHealth / _enemy.MaxHealth;
            if (healthPercent <= phaseThreeHealthPercent)
            {
                multiplier = phaseThreeCooldownMultiplier;
            }
            else if (healthPercent <= phaseTwoHealthPercent)
            {
                multiplier = phaseTwoCooldownMultiplier;
            }
        }

        return Mathf.Max(0f, minimumPatternCooldown * multiplier);
    }

    private bool IsInsideEngagementRange(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return false;
        }

        return (target.transform.position - transform.position).sqrMagnitude <= engagementRange * engagementRange;
    }

    private bool IsInsideMaxEngagement(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return false;
        }

        float maxRange = engagementRange + approachRangeBuffer;
        return maxRange <= 0f || (target.transform.position - transform.position).sqrMagnitude <= maxRange * maxRange;
    }

    private static bool HasAnyWeapon(EnemyProjectileWeaponBase3D[] weapons)
    {
        if (weapons == null)
        {
            return false;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static EnemyProjectileWeaponBase3D ResolveWeapon(EnemyProjectileWeaponBase3D[] weapons, int stepIndex)
    {
        if (weapons == null || weapons.Length == 0)
        {
            return null;
        }

        for (int offset = 0; offset < weapons.Length; offset++)
        {
            EnemyProjectileWeaponBase3D weapon = weapons[(stepIndex + offset) % weapons.Length];
            if (weapon != null)
            {
                return weapon;
            }
        }

        return null;
    }

    private static bool IsTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
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

        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
    }
}
