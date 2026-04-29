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
        CurtainWithEscapeDoor,
        FormationMissileSalvo,
        LightningSlowBeam
    }

    [Header("Pattern Weapons")]
    [Tooltip("Projectile weapons used by the lagging machine-gun rake. Wire staggered turret weapons here for the best readable hardpoint sequence.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] laggingRakeWeapons;

    [Tooltip("Projectile weapons used as fan lanes. Each component represents one authored lane; the brain supplies the per-lane fire direction.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] predictiveFanWeapons;

    [Tooltip("Projectile weapons used by the escape-door curtain. More authored weapons allow more simultaneous or staggered lanes, but the boss still obeys Max Shots Per Pattern.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] curtainWeapons;

    [Tooltip("Formation missile salvo weapons. Each configured weapon should be a FormationMissileSalvoWeaponEnemy3D that launches a full missile bloom in one activation.")]
    [SerializeField] private EnemyProjectileWeaponBase3D[] formationMissileSalvoWeapons;

    [Tooltip("Beam weapons used by the lagging beam convergence pattern. NetEnemyCombat3D replays these by component index, so keep the same BeamWeapon3D component order on host and client prefabs.")]
    [SerializeField] private BeamWeapon3D[] beamFenceWeapons;

    [Tooltip("Optional charge/telegraph visuals paired by index with Beam Weapons. These are presentation-only; beam damage still comes from BeamWeapon3D.")]
    [SerializeField] private ProjectileChargeTelegraph3D[] beamFenceTelegraphs;

    [Tooltip("Two lightning beam weapons used by the accurate slow-beam pattern. Configure these BeamWeapon3D components with a lightning beam prefab, PlayerTeam target faction, and moderate damage per second.")]
    [SerializeField] private BeamWeapon3D[] lightningSlowBeamWeapons;

    [Tooltip("Optional charge/telegraph visuals paired by index with Lightning Slow Beam Weapons.")]
    [SerializeField] private ProjectileChargeTelegraph3D[] lightningSlowBeamTelegraphs;

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

    [Tooltip("Seconds between target-position history samples used only when Rake History Blend is above 0.")]
    [SerializeField] private float targetHistorySampleInterval = 0.05f;

    [Tooltip("Number of recent target positions retained for lagging attacks. Higher values allow older rake targets but use a little more memory.")]
    [Range(2, 32)]
    [SerializeField] private int targetHistorySamples = 16;

    [Header("Movement Bands")]
    [Tooltip("Inner edge of the Siege Carrier's preferred range band. Inside this distance it backs away without trying to face the player.")]
    [SerializeField] private float preferredRangeMin = 180f;

    [Tooltip("Outer edge of the Siege Carrier's preferred range band. Beyond this distance it approaches without trying to face the player.")]
    [SerializeField] private float preferredRangeMax = 260f;

    [Tooltip("Maximum distance where the Siege Carrier is allowed to run attack patterns.")]
    [SerializeField] private float engagementRange = 260f;

    [Tooltip("Extra distance beyond Engagement Range where the boss slowly approaches instead of idling.")]
    [SerializeField] private float approachRangeBuffer = 180f;

    [Tooltip("Speed scale used while the boss is outside Preferred Range Max but inside the approach buffer.")]
    [Range(0f, 1f)]
    [SerializeField] private float approachSpeedScale = 0.25f;

    [Tooltip("Speed scale used when the player gets inside Preferred Range Min. The carrier backs away along its movement plane instead of rotating to face the player.")]
    [Range(0f, 1f)]
    [SerializeField] private float retreatSpeedScale = 0.18f;

    [Tooltip("How strongly movement preserves the carrier's starting horizontal plane. 0 ignores target height for movement, 1 fully follows target height.")]
    [Range(0f, 1f)]
    [SerializeField] private float targetVerticalFollowWeight = 0.1f;

    [Tooltip("When the carrier has drifted above/below its starting plane, this adds correction back toward that plane while moving. 0 disables plane correction.")]
    [Range(0f, 1f)]
    [SerializeField] private float planeReturnWeight = 0.35f;

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

    [Tooltip("How far behind the target's current position the optional history target sits, in seconds. Only affects aim when Rake History Blend is above 0.")]
    [SerializeField] private float rakeHistorySeconds = 0f;

    [Tooltip("How much the rake blends from precise current/lead aim toward historical target positions. 0 is precise follow-fire, 1 is pure lagging trail fire.")]
    [Range(0f, 1f)]
    [SerializeField] private float rakeHistoryBlend = 0f;

    [Tooltip("If true, each rake shot predicts the target's current velocity at fire time instead of using only current position.")]
    [SerializeField] private bool rakeUseLeadAim = true;

    [Tooltip("Projectile speed used for rake lead calculation. If 0, the current rake weapon's configured speed is used.")]
    [SerializeField] private float rakeLeadProjectileSpeed = 0f;

    [Tooltip("Multiplier applied to the calculated projectile travel time when leading rake shots. Lower than 1 aims closer to the target; higher than 1 leads farther ahead.")]
    [Range(0f, 2f)]
    [SerializeField] private float rakeLeadTimeScale = 1f;

    [Tooltip("Extra seconds of target-velocity lead added to every rake shot after projectile travel-time lead is calculated.")]
    [SerializeField] private float rakeAdditionalLeadSeconds = 0.03f;

    [Tooltip("Maximum total seconds of target-velocity lead allowed for a rake shot so fast targets do not produce absurd far-ahead aim points.")]
    [SerializeField] private float rakeMaxLeadSeconds = 1.25f;

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

    [Header("Formation Missile Salvo")]
    [Tooltip("Projectile-budget cost charged when one formation missile salvo launches. Set this to the salvo missile count so boss pattern budgets stay honest.")]
    [Range(1, 32)]
    [SerializeField] private int formationMissileSalvoBudgetCost = 8;

    [Header("Lagging Beam Convergence")]
    [Tooltip("Seconds of warning before converging beam damage begins.")]
    [SerializeField] private float beamFenceTelegraphDuration = 0.75f;

    [Tooltip("Seconds the damaging converging beams remain active.")]
    [SerializeField] private float beamFenceActiveDuration = 1.2f;

    [Tooltip("Maximum beam hardpoints used in one convergence activation.")]
    [Range(1, 16)]
    [SerializeField] private int beamFenceMaxBeams = 4;

    [Tooltip("Seconds between beam aim refreshes while the convergence is active. Lower is smoother but sends more network updates.")]
    [SerializeField] private float beamFenceAimRefreshInterval = 0.03f;

    [Tooltip("Seconds behind the target used as the shared convergence point for all active beam hardpoints.")]
    [SerializeField] private float beamConvergenceLagSeconds = 0.25f;

    [Tooltip("Blend from the target's current position toward the lagged convergence point. 0 tracks current position; 1 uses the full lagged point.")]
    [Range(0f, 1f)]
    [SerializeField] private float beamConvergenceLagBlend = 0.75f;

    [Tooltip("Small smoothing time for beam aim directions. This reduces visible long-range beam jitter without changing the lagged convergence point.")]
    [SerializeField] private float beamConvergenceAimSmoothTime = 0.08f;

    [Tooltip("Allows explicit boss convergence aim to point behind a beam hardpoint's Direction Reference. Keep enabled for wide/side muzzles that should still converge on the same target point instead of clamping straight ahead outside their forward arc.")]
    [SerializeField] private bool beamConvergenceAllowBehindHardpointAim = true;

    [Header("Lightning Slow Beam")]
    [Tooltip("Seconds of warning before the two accurate lightning slow beams activate.")]
    [SerializeField] private float lightningSlowBeamTelegraphDuration = 0.45f;

    [Tooltip("Seconds the two accurate lightning slow beams remain active.")]
    [SerializeField] private float lightningSlowBeamActiveDuration = 1.35f;

    [Tooltip("Seconds between lightning beam aim refreshes while active. Lower values make the beams track more accurately but send more network aim updates.")]
    [SerializeField] private float lightningSlowBeamAimRefreshInterval = 0.02f;

    [Tooltip("How many lightning beams are allowed to fire in this pattern. Keep at 2 for the intended boss ability.")]
    [Range(1, 2)]
    [SerializeField] private int lightningSlowBeamCount = 2;

    [Tooltip("Seconds of target-velocity lead added to the lightning beams. This makes them more accurate against fast lateral movement.")]
    [SerializeField] private float lightningSlowBeamLeadSeconds = 0.12f;

    [Tooltip("Small smoothing time for lightning aim. Keep low so the slow beams stay threatening and accurate.")]
    [SerializeField] private float lightningSlowBeamAimSmoothTime = 0.025f;

    [Tooltip("Allows explicit lightning slow-beam aim to point behind a beam hardpoint's Direction Reference. Keep enabled when the boss attack should track the player from broadside or offset lightning muzzles.")]
    [SerializeField] private bool lightningSlowBeamAllowBehindHardpointAim = true;

    [Tooltip("Radius used by the boss brain's slow check along each lightning beam. Match or slightly exceed the lightning beam prefab's gameplay hitscan radius.")]
    [SerializeField] private float lightningSlowBeamSlowRadius = 1.25f;

    [Tooltip("Layers considered by the boss brain when checking whether a lightning beam has line-of-sight to the player for slow application.")]
    [SerializeField] private LayerMask lightningSlowBeamCollisionMask = ~0;

    [Tooltip("Movement multiplier applied while the lightning slow beam is hitting the player. 0.45 means the player moves at 45% speed.")]
    [Range(0f, 1f)]
    [SerializeField] private float lightningSlowBeamSlowMultiplier = 0.45f;

    [Tooltip("Duration of each refreshed slow pulse. This should be slightly longer than Slow Tick Interval so the slow does not flicker between beam ticks.")]
    [SerializeField] private float lightningSlowBeamSlowDuration = 0.18f;

    [Tooltip("Seconds between server-authoritative slow checks while the lightning beams are active.")]
    [SerializeField] private float lightningSlowBeamSlowTickInterval = 0.08f;

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
    private float _preferredPlaneY;
    private Vector3[] _beamConvergenceSmoothedDirections;
    private bool[] _beamConvergenceHasSmoothedDirections;
    private Vector3[] _lightningSmoothedDirections;
    private bool[] _lightningHasSmoothedDirections;
    private readonly RaycastHit[] _lightningSlowHits = new RaycastHit[8];
    private int _patternCursor;
    private int _patternShotsFired;
    private int _patternStepIndex;
    private int _activeBeamCount;
    private int _activeLightningBeamCount;
    private float _nextLightningSlowTickTime;
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
        _preferredPlaneY = transform.position.y;
        EnsureTargetHistoryBuffer();
        EnsureBeamConvergenceBuffers();
        EnsureLightningSlowBeamBuffers();
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        targetHistorySampleInterval = Mathf.Max(0.02f, targetHistorySampleInterval);
        targetHistorySamples = Mathf.Clamp(targetHistorySamples, 2, 32);
        preferredRangeMin = Mathf.Max(0f, preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin, preferredRangeMax);
        engagementRange = Mathf.Max(0f, engagementRange);
        approachRangeBuffer = Mathf.Max(0f, approachRangeBuffer);
        approachSpeedScale = Mathf.Clamp01(approachSpeedScale);
        retreatSpeedScale = Mathf.Clamp01(retreatSpeedScale);
        targetVerticalFollowWeight = Mathf.Clamp01(targetVerticalFollowWeight);
        planeReturnWeight = Mathf.Clamp01(planeReturnWeight);
        minimumPatternCooldown = Mathf.Max(0f, minimumPatternCooldown);
        maxShotsPerPattern = Mathf.Clamp(maxShotsPerPattern, 1, 128);
        phaseTwoHealthPercent = Mathf.Clamp(phaseTwoHealthPercent, 0.01f, 1f);
        phaseThreeHealthPercent = Mathf.Clamp(phaseThreeHealthPercent, 0.01f, phaseTwoHealthPercent);
        phaseTwoCooldownMultiplier = Mathf.Clamp(phaseTwoCooldownMultiplier, 0.1f, 1f);
        phaseThreeCooldownMultiplier = Mathf.Clamp(phaseThreeCooldownMultiplier, 0.1f, 1f);
        rakeShotCount = Mathf.Max(1, rakeShotCount);
        rakeShotInterval = Mathf.Max(0.01f, rakeShotInterval);
        rakeHistorySeconds = Mathf.Max(0f, rakeHistorySeconds);
        rakeHistoryBlend = Mathf.Clamp01(rakeHistoryBlend);
        rakeLeadProjectileSpeed = Mathf.Max(0f, rakeLeadProjectileSpeed);
        rakeLeadTimeScale = Mathf.Clamp(rakeLeadTimeScale, 0f, 2f);
        rakeAdditionalLeadSeconds = Mathf.Max(0f, rakeAdditionalLeadSeconds);
        rakeMaxLeadSeconds = Mathf.Max(0f, rakeMaxLeadSeconds);
        fanLaneCount = Mathf.Clamp(fanLaneCount, 1, 31);
        fanTotalSpreadDegrees = Mathf.Clamp(fanTotalSpreadDegrees, 0f, 180f);
        fanLaneInterval = Mathf.Max(0.01f, fanLaneInterval);
        fanLeadProjectileSpeed = Mathf.Max(0f, fanLeadProjectileSpeed);
        curtainLaneCount = Mathf.Clamp(curtainLaneCount, 1, 31);
        curtainArcDegrees = Mathf.Clamp(curtainArcDegrees, 0f, 270f);
        curtainEscapeDoorDegrees = Mathf.Clamp(curtainEscapeDoorDegrees, 0f, 180f);
        curtainLaneInterval = Mathf.Max(0.01f, curtainLaneInterval);
        formationMissileSalvoBudgetCost = Mathf.Clamp(formationMissileSalvoBudgetCost, 1, 32);
        beamFenceTelegraphDuration = Mathf.Max(0f, beamFenceTelegraphDuration);
        beamFenceActiveDuration = Mathf.Max(0.01f, beamFenceActiveDuration);
        beamFenceMaxBeams = Mathf.Clamp(beamFenceMaxBeams, 1, 16);
        beamFenceAimRefreshInterval = Mathf.Max(0.01f, beamFenceAimRefreshInterval);
        beamConvergenceLagSeconds = Mathf.Max(0f, beamConvergenceLagSeconds);
        beamConvergenceLagBlend = Mathf.Clamp01(beamConvergenceLagBlend);
        beamConvergenceAimSmoothTime = Mathf.Max(0f, beamConvergenceAimSmoothTime);
        lightningSlowBeamTelegraphDuration = Mathf.Max(0f, lightningSlowBeamTelegraphDuration);
        lightningSlowBeamActiveDuration = Mathf.Max(0.01f, lightningSlowBeamActiveDuration);
        lightningSlowBeamAimRefreshInterval = Mathf.Max(0.01f, lightningSlowBeamAimRefreshInterval);
        lightningSlowBeamCount = Mathf.Clamp(lightningSlowBeamCount, 1, 2);
        lightningSlowBeamLeadSeconds = Mathf.Max(0f, lightningSlowBeamLeadSeconds);
        lightningSlowBeamAimSmoothTime = Mathf.Max(0f, lightningSlowBeamAimSmoothTime);
        lightningSlowBeamSlowRadius = Mathf.Max(0f, lightningSlowBeamSlowRadius);
        lightningSlowBeamSlowMultiplier = Mathf.Clamp01(lightningSlowBeamSlowMultiplier);
        lightningSlowBeamSlowDuration = Mathf.Max(0f, lightningSlowBeamSlowDuration);
        lightningSlowBeamSlowTickInterval = Mathf.Max(0.01f, lightningSlowBeamSlowTickInterval);
    }

    private void OnDisable()
    {
        StopActiveBeams();
        StopLightningSlowBeams();
        StopBeamTelegraphs(immediate: true);
        StopLightningSlowBeamTelegraphs(immediate: true);
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
        preferredRangeMin = Mathf.Max(0f, stats.preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin, stats.preferredRangeMax);
        engagementRange = Mathf.Max(0f, stats.engagementRange);
        approachRangeBuffer = Mathf.Max(0f, stats.approachRangeBuffer);
        approachSpeedScale = Mathf.Clamp01(stats.approachSpeedScale);
        retreatSpeedScale = Mathf.Clamp01(stats.retreatSpeedScale);
        targetVerticalFollowWeight = Mathf.Clamp01(stats.targetVerticalFollowWeight);
        planeReturnWeight = Mathf.Clamp01(stats.planeReturnWeight);
        minimumPatternCooldown = Mathf.Max(0f, stats.minimumPatternCooldown);
        maxShotsPerPattern = Mathf.Clamp(stats.maxShotsPerPattern, 1, 128);
        phaseTwoHealthPercent = Mathf.Clamp(stats.phaseTwoHealthPercent, 0.01f, 1f);
        phaseThreeHealthPercent = Mathf.Clamp(stats.phaseThreeHealthPercent, 0.01f, phaseTwoHealthPercent);
        phaseTwoCooldownMultiplier = Mathf.Clamp(stats.phaseTwoCooldownMultiplier, 0.1f, 1f);
        phaseThreeCooldownMultiplier = Mathf.Clamp(stats.phaseThreeCooldownMultiplier, 0.1f, 1f);
        rakeShotCount = Mathf.Max(1, stats.rakeShotCount);
        rakeShotInterval = Mathf.Max(0.01f, stats.rakeShotInterval);
        rakeHistorySeconds = Mathf.Max(0f, stats.rakeHistorySeconds);
        rakeHistoryBlend = Mathf.Clamp01(stats.rakeHistoryBlend);
        rakeUseLeadAim = stats.rakeUseLeadAim;
        rakeLeadProjectileSpeed = Mathf.Max(0f, stats.rakeLeadProjectileSpeed);
        rakeLeadTimeScale = Mathf.Clamp(stats.rakeLeadTimeScale, 0f, 2f);
        rakeAdditionalLeadSeconds = Mathf.Max(0f, stats.rakeAdditionalLeadSeconds);
        rakeMaxLeadSeconds = Mathf.Max(0f, stats.rakeMaxLeadSeconds);
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
        formationMissileSalvoBudgetCost = Mathf.Clamp(stats.formationMissileSalvoBudgetCost, 1, 32);
        beamFenceTelegraphDuration = Mathf.Max(0f, stats.beamFenceTelegraphDuration);
        beamFenceActiveDuration = Mathf.Max(0.01f, stats.beamFenceActiveDuration);
        beamFenceMaxBeams = Mathf.Clamp(stats.beamFenceMaxBeams, 1, 16);
        beamFenceAimRefreshInterval = Mathf.Max(0.01f, stats.beamFenceAimRefreshInterval);
        beamConvergenceLagSeconds = Mathf.Max(0f, stats.beamConvergenceLagSeconds);
        beamConvergenceLagBlend = Mathf.Clamp01(stats.beamConvergenceLagBlend);
        beamConvergenceAimSmoothTime = Mathf.Max(0f, stats.beamConvergenceAimSmoothTime);
        lightningSlowBeamTelegraphDuration = Mathf.Max(0f, stats.lightningSlowBeamTelegraphDuration);
        lightningSlowBeamActiveDuration = Mathf.Max(0.01f, stats.lightningSlowBeamActiveDuration);
        lightningSlowBeamAimRefreshInterval = Mathf.Max(0.01f, stats.lightningSlowBeamAimRefreshInterval);
        lightningSlowBeamCount = Mathf.Clamp(stats.lightningSlowBeamCount, 1, 2);
        lightningSlowBeamLeadSeconds = Mathf.Max(0f, stats.lightningSlowBeamLeadSeconds);
        lightningSlowBeamAimSmoothTime = Mathf.Max(0f, stats.lightningSlowBeamAimSmoothTime);
        lightningSlowBeamSlowRadius = Mathf.Max(0f, stats.lightningSlowBeamSlowRadius);
        lightningSlowBeamSlowMultiplier = Mathf.Clamp01(stats.lightningSlowBeamSlowMultiplier);
        lightningSlowBeamSlowDuration = Mathf.Max(0f, stats.lightningSlowBeamSlowDuration);
        lightningSlowBeamSlowTickInterval = Mathf.Max(0.01f, stats.lightningSlowBeamSlowTickInterval);
        EnsureTargetHistoryBuffer();
        EnsureBeamConvergenceBuffers();
        EnsureLightningSlowBeamBuffers();
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

        Vector3 planarDirectionToTarget = ResolvePlaneBiasedDirectionToTarget(target);
        float distance = toTarget.magnitude;
        if (distance > preferredRangeMax)
        {
            flightController?.SetFlightIntent(planarDirectionToTarget, planarDirectionToTarget, approachSpeedScale, moveBackward: false);
            return;
        }

        if (distance < preferredRangeMin)
        {
            Vector3 retreatDirection = -planarDirectionToTarget;
            flightController?.SetFlightIntent(retreatDirection, retreatDirection, retreatSpeedScale, moveBackward: false);
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private void TickActivePattern(Entity3D target)
    {
        switch (_activePattern)
        {
            case BossPattern.LaggingMachineGunRake:
                TickLaggingRake(target);
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
            case BossPattern.FormationMissileSalvo:
                TickFormationMissileSalvo(target);
                break;
            case BossPattern.LightningSlowBeam:
                TickLightningSlowBeam(target);
                break;
        }
    }

    private void StartNextPattern(Entity3D target)
    {
        int patternCount = System.Enum.GetValues(typeof(BossPattern)).Length - 1;
        for (int attempt = 0; attempt < patternCount; attempt++)
        {
            BossPattern next = (BossPattern)((_patternCursor % patternCount) + 1);
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
            ResetBeamConvergenceSmoothing();
            StartBeamTelegraphs();
            return;
        }

        if (pattern == BossPattern.LightningSlowBeam)
        {
            _activeLightningBeamCount = Mathf.Min(lightningSlowBeamWeapons != null ? lightningSlowBeamWeapons.Length : 0, lightningSlowBeamCount);
            _patternEndsAt = Time.time + lightningSlowBeamTelegraphDuration + lightningSlowBeamActiveDuration;
            ResetLightningSlowBeamSmoothing();
            StartLightningSlowBeamTelegraphs();
            return;
        }

        if (pattern == BossPattern.CurtainWithEscapeDoor)
        {
            _curtainDoorOffset = NormalizeSignedAngle(_curtainDoorOffset + curtainDoorDriftDegrees);
        }
    }

    private void TickLaggingRake(Entity3D target)
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
        Vector3 aimPoint = ResolveRakeAimPoint(target, weapon);
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
                _nextBeamAimRefreshTime = Time.time + beamFenceAimRefreshInterval;
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

    private void TickFormationMissileSalvo(Entity3D target)
    {
        if (_patternStepIndex > 0 || _patternShotsFired + formationMissileSalvoBudgetCost > maxShotsPerPattern)
        {
            FinishPattern();
            return;
        }

        EnemyProjectileWeaponBase3D weapon = ResolveWeapon(formationMissileSalvoWeapons, _patternCursor);
        if (weapon != null && FireProjectileDirection(weapon, ResolveDirectionToTarget(target)))
        {
            _patternShotsFired += formationMissileSalvoBudgetCost;
        }

        _patternStepIndex++;
        FinishPattern();
    }

    private void TickLightningSlowBeam(Entity3D target)
    {
        if (_activeLightningBeamCount <= 0)
        {
            FinishPattern();
            return;
        }

        float activeStartTime = _patternEndsAt - lightningSlowBeamActiveDuration;
        if (Time.time >= activeStartTime && Time.time < _patternEndsAt)
        {
            if (_patternStepIndex == 0)
            {
                StopLightningSlowBeamTelegraphs(immediate: false);
                SetLightningSlowBeamState(target, isFiring: true);
                _patternStepIndex = 1;
                _nextBeamAimRefreshTime = Time.time + lightningSlowBeamAimRefreshInterval;
                _nextLightningSlowTickTime = Time.time;
            }

            if (Time.time >= _nextBeamAimRefreshTime)
            {
                RefreshLightningSlowBeamAim(target);
                _nextBeamAimRefreshTime = Time.time + lightningSlowBeamAimRefreshInterval;
            }

            if (Time.time >= _nextLightningSlowTickTime)
            {
                ApplyLightningSlowBeamSlow(target);
                _nextLightningSlowTickTime = Time.time + lightningSlowBeamSlowTickInterval;
            }
            return;
        }

        if (Time.time >= _patternEndsAt)
        {
            StopLightningSlowBeams();
            FinishPattern();
        }
    }

    private void FinishPattern()
    {
        StopBeamTelegraphs(immediate: false);
        StopActiveBeams();
        StopLightningSlowBeamTelegraphs(immediate: false);
        StopLightningSlowBeams();
        ResetBeamConvergenceSmoothing();
        ResetLightningSlowBeamSmoothing();
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
        StopLightningSlowBeamTelegraphs(immediate: true);
        StopLightningSlowBeams();
        ResetBeamConvergenceSmoothing();
        ResetLightningSlowBeamSmoothing();
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
            case BossPattern.FormationMissileSalvo:
                return HasAnyWeapon(formationMissileSalvoWeapons);
            case BossPattern.LightningSlowBeam:
                return lightningSlowBeamWeapons != null && lightningSlowBeamWeapons.Length > 0;
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
        Vector3 convergencePoint = ResolveBeamConvergencePoint(target);
        for (int i = 0; i < _activeBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, beamConvergenceAllowBehindHardpointAim);
            Vector3 beamDirection = ResolveBeamConvergenceDirection(beamWeapon, convergencePoint, i);
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
        Vector3 convergencePoint = ResolveBeamConvergencePoint(target);
        for (int i = 0; i < _activeBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, beamConvergenceAllowBehindHardpointAim);
            Vector3 beamDirection = ResolveBeamConvergenceDirection(beamWeapon, convergencePoint, i);
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

    [ClientRpc]
    private void StartLightningSlowBeamTelegraphClientRpc(float duration, double serverStartTime)
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

        StartLightningSlowBeamTelegraphsLocal(duration, elapsed);
    }

    [ClientRpc]
    private void StopLightningSlowBeamTelegraphClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        StopLightningSlowBeamTelegraphsLocal(immediate: false);
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
        return ResolveHistoricalTargetPoint(rakeHistorySeconds);
    }

    private void SetLightningSlowBeamState(Entity3D target, bool isFiring)
    {
        for (int i = 0; i < _activeLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, lightningSlowBeamAllowBehindHardpointAim);
            Vector3 beamDirection = ResolveLightningSlowBeamDirection(beamWeapon, target, i);
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

    private void RefreshLightningSlowBeamAim(Entity3D target)
    {
        for (int i = 0; i < _activeLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, lightningSlowBeamAllowBehindHardpointAim);
            Vector3 beamDirection = ResolveLightningSlowBeamDirection(beamWeapon, target, i);
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

    private void StopLightningSlowBeams()
    {
        if (lightningSlowBeamWeapons == null)
        {
            return;
        }

        int count = _activeLightningBeamCount > 0 ? Mathf.Min(_activeLightningBeamCount, lightningSlowBeamWeapons.Length) : lightningSlowBeamWeapons.Length;
        for (int i = 0; i < count; i++)
        {
            BeamWeapon3D beamWeapon = lightningSlowBeamWeapons[i];
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

    private void ApplyLightningSlowBeamSlow(Entity3D target)
    {
        if (!IsTargetValid(target) || lightningSlowBeamSlowDuration <= 0f || lightningSlowBeamSlowMultiplier >= 1f)
        {
            return;
        }

        for (int i = 0; i < _activeLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            Vector3 direction = ResolveLightningSlowBeamDirection(beamWeapon, target, i);
            Vector3 origin = beamWeapon.GetBeamOrigin(direction);
            if (DoesLightningBeamReachTarget(origin, direction, target))
            {
                target.ApplySlow(lightningSlowBeamSlowMultiplier, lightningSlowBeamSlowDuration);
                target.ThrusterVfx?.ApplyTemporaryEmissionRateScale(lightningSlowBeamSlowMultiplier, lightningSlowBeamSlowDuration);
                return;
            }
        }
    }

    private bool DoesLightningBeamReachTarget(Vector3 origin, Vector3 direction, Entity3D target)
    {
        if (direction.sqrMagnitude <= 0.0001f || !IsTargetValid(target))
        {
            return false;
        }

        float maxDistance = Mathf.Max(0f, engagementRange);
        int hitCount = Physics.SphereCastNonAlloc(
                origin,
                Mathf.Max(0f, lightningSlowBeamSlowRadius),
                direction.normalized,
                _lightningSlowHits,
                maxDistance,
                lightningSlowBeamCollisionMask,
                QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
        {
            return false;
        }

        float closestDistance = float.PositiveInfinity;
        Entity3D closestEntity = null;
        int clampedHitCount = Mathf.Min(hitCount, _lightningSlowHits.Length);
        for (int i = 0; i < clampedHitCount; i++)
        {
            RaycastHit hit = _lightningSlowHits[i];
            Entity3D hitEntity = ResolveHitEntity(hit.collider);
            if (hitEntity == _enemy)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestEntity = hitEntity;
            }
        }

        return closestEntity == target;
    }

    private void StartLightningSlowBeamTelegraphs()
    {
        StartLightningSlowBeamTelegraphsLocal(lightningSlowBeamTelegraphDuration, elapsed: 0f);
        if (ShouldReplicateTelegraph())
        {
            StartLightningSlowBeamTelegraphClientRpc(lightningSlowBeamTelegraphDuration, ResolveNetworkServerTime());
        }
    }

    private void StartLightningSlowBeamTelegraphsLocal(float duration, float elapsed)
    {
        if (lightningSlowBeamTelegraphs == null)
        {
            return;
        }

        int count = Mathf.Min(_activeLightningBeamCount, lightningSlowBeamTelegraphs.Length);
        for (int i = 0; i < count; i++)
        {
            lightningSlowBeamTelegraphs[i]?.PlayCharge(duration, elapsed);
        }
    }

    private void StopLightningSlowBeamTelegraphs(bool immediate)
    {
        StopLightningSlowBeamTelegraphsLocal(immediate);
        if (!immediate && ShouldReplicateTelegraph())
        {
            StopLightningSlowBeamTelegraphClientRpc();
        }
    }

    private void StopLightningSlowBeamTelegraphsLocal(bool immediate)
    {
        if (lightningSlowBeamTelegraphs == null)
        {
            return;
        }

        for (int i = 0; i < lightningSlowBeamTelegraphs.Length; i++)
        {
            lightningSlowBeamTelegraphs[i]?.StopCharge(immediate);
        }
    }

    private Vector3 ResolveHistoricalTargetPoint(float secondsBehind)
    {
        if (_historyCount <= 0 || _targetHistory == null || _targetHistory.Length == 0)
        {
            return IsTargetValid(_currentTarget) ? _currentTarget.transform.position : transform.position + transform.forward * 20f;
        }

        int samplesBehind = Mathf.Clamp(Mathf.RoundToInt(secondsBehind / Mathf.Max(0.02f, targetHistorySampleInterval)), 0, _historyCount - 1);
        int index = _historyWriteIndex - 1 - samplesBehind;
        while (index < 0)
        {
            index += _targetHistory.Length;
        }

        return _targetHistory[index % _targetHistory.Length];
    }

    private Vector3 ResolveBeamConvergencePoint(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return ResolveHistoricalTargetPoint(beamConvergenceLagSeconds);
        }

        Vector3 currentPoint = target.transform.position;
        Vector3 laggedPoint = _historyCount > 0
            ? ResolveHistoricalTargetPoint(beamConvergenceLagSeconds)
            : currentPoint - ResolveTargetVelocity(target) * beamConvergenceLagSeconds;

        return Vector3.Lerp(currentPoint, laggedPoint, beamConvergenceLagBlend);
    }

    private static void ConfigureBossBeamAimConstraint(BeamWeapon3D beamWeapon, bool allowBehindHardpointAim)
    {
        if (beamWeapon == null)
        {
            return;
        }

        beamWeapon.SetAllowExplicitAimBehindForward(allowBehindHardpointAim);
    }

    private Vector3 ResolveRakeAimPoint(Entity3D target, EnemyProjectileWeaponBase3D weapon)
    {
        if (!IsTargetValid(target))
        {
            return ResolveHistoricalTargetPoint();
        }

        Vector3 currentAimPoint = target.transform.position;
        if (rakeUseLeadAim)
        {
            Vector3 targetVelocity = ResolveTargetVelocity(target);
            float projectileSpeed = rakeLeadProjectileSpeed;
            if (projectileSpeed <= 0f && weapon != null)
            {
                projectileSpeed = weapon.WeaponConfig.speed;
            }

            if (projectileSpeed > 0.0001f && targetVelocity.sqrMagnitude > 0.0001f)
            {
                float travelTime = Vector3.Distance(transform.position, target.transform.position) / projectileSpeed;
                float leadSeconds = Mathf.Clamp((travelTime * rakeLeadTimeScale) + rakeAdditionalLeadSeconds, 0f, rakeMaxLeadSeconds);
                currentAimPoint += targetVelocity * leadSeconds;
            }
        }

        if (rakeHistoryBlend <= 0f)
        {
            return currentAimPoint;
        }

        return Vector3.Lerp(currentAimPoint, ResolveHistoricalTargetPoint(), rakeHistoryBlend);
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

    private void EnsureBeamConvergenceBuffers()
    {
        int size = Mathf.Max(0, beamFenceWeapons != null ? beamFenceWeapons.Length : 0);
        if (_beamConvergenceSmoothedDirections != null && _beamConvergenceSmoothedDirections.Length == size)
        {
            return;
        }

        _beamConvergenceSmoothedDirections = new Vector3[size];
        _beamConvergenceHasSmoothedDirections = new bool[size];
    }

    private void EnsureLightningSlowBeamBuffers()
    {
        int size = Mathf.Max(0, lightningSlowBeamWeapons != null ? lightningSlowBeamWeapons.Length : 0);
        if (_lightningSmoothedDirections != null && _lightningSmoothedDirections.Length == size)
        {
            return;
        }

        _lightningSmoothedDirections = new Vector3[size];
        _lightningHasSmoothedDirections = new bool[size];
    }

    private void ResetBeamConvergenceSmoothing()
    {
        EnsureBeamConvergenceBuffers();
        if (_beamConvergenceHasSmoothedDirections == null)
        {
            return;
        }

        for (int i = 0; i < _beamConvergenceHasSmoothedDirections.Length; i++)
        {
            _beamConvergenceHasSmoothedDirections[i] = false;
        }
    }

    private void ResetLightningSlowBeamSmoothing()
    {
        EnsureLightningSlowBeamBuffers();
        if (_lightningHasSmoothedDirections == null)
        {
            return;
        }

        for (int i = 0; i < _lightningHasSmoothedDirections.Length; i++)
        {
            _lightningHasSmoothedDirections[i] = false;
        }
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

    private Vector3 ResolveBeamConvergenceDirection(BeamWeapon3D beamWeapon, Vector3 convergencePoint, int index)
    {
        if (beamWeapon == null)
        {
            return ResolveDirectionToTarget(_currentTarget);
        }

        Vector3 provisionalDirection = convergencePoint - transform.position;
        if (provisionalDirection.sqrMagnitude <= 0.0001f)
        {
            provisionalDirection = ResolveDirectionToTarget(_currentTarget);
        }

        Vector3 origin = beamWeapon.GetBeamOrigin(provisionalDirection.normalized);
        Vector3 rawDirection = convergencePoint - origin;
        rawDirection = rawDirection.sqrMagnitude > 0.0001f ? rawDirection.normalized : provisionalDirection.normalized;

        if (beamConvergenceAimSmoothTime <= 0f)
        {
            return rawDirection;
        }

        EnsureBeamConvergenceBuffers();
        if (_beamConvergenceSmoothedDirections == null
            || _beamConvergenceHasSmoothedDirections == null
            || index < 0
            || index >= _beamConvergenceSmoothedDirections.Length)
        {
            return rawDirection;
        }

        if (!_beamConvergenceHasSmoothedDirections[index])
        {
            _beamConvergenceSmoothedDirections[index] = rawDirection;
            _beamConvergenceHasSmoothedDirections[index] = true;
            return rawDirection;
        }

        float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, beamConvergenceAimSmoothTime));
        Vector3 smoothedDirection = Vector3.Slerp(_beamConvergenceSmoothedDirections[index], rawDirection, blend);
        _beamConvergenceSmoothedDirections[index] = smoothedDirection.sqrMagnitude > 0.0001f ? smoothedDirection.normalized : rawDirection;
        return _beamConvergenceSmoothedDirections[index];
    }

    private Vector3 ResolveLightningSlowBeamDirection(BeamWeapon3D beamWeapon, Entity3D target, int index)
    {
        if (beamWeapon == null || !IsTargetValid(target))
        {
            return ResolveDirectionToTarget(target);
        }

        Vector3 targetPoint = target.transform.position + ResolveTargetVelocity(target) * lightningSlowBeamLeadSeconds;
        Vector3 provisionalDirection = targetPoint - transform.position;
        if (provisionalDirection.sqrMagnitude <= 0.0001f)
        {
            provisionalDirection = ResolveDirectionToTarget(target);
        }

        Vector3 origin = beamWeapon.GetBeamOrigin(provisionalDirection.normalized);
        Vector3 rawDirection = targetPoint - origin;
        rawDirection = rawDirection.sqrMagnitude > 0.0001f ? rawDirection.normalized : provisionalDirection.normalized;

        if (lightningSlowBeamAimSmoothTime <= 0f)
        {
            return rawDirection;
        }

        EnsureLightningSlowBeamBuffers();
        if (_lightningSmoothedDirections == null
            || _lightningHasSmoothedDirections == null
            || index < 0
            || index >= _lightningSmoothedDirections.Length)
        {
            return rawDirection;
        }

        if (!_lightningHasSmoothedDirections[index])
        {
            _lightningSmoothedDirections[index] = rawDirection;
            _lightningHasSmoothedDirections[index] = true;
            return rawDirection;
        }

        float blend = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, lightningSlowBeamAimSmoothTime));
        Vector3 smoothedDirection = Vector3.Slerp(_lightningSmoothedDirections[index], rawDirection, blend);
        _lightningSmoothedDirections[index] = smoothedDirection.sqrMagnitude > 0.0001f ? smoothedDirection.normalized : rawDirection;
        return _lightningSmoothedDirections[index];
    }

    private Entity3D ResolveHitEntity(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        Entity3D entity = hitCollider.GetComponent<Entity3D>();
        if (entity != null)
        {
            return entity;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            entity = hitCollider.attachedRigidbody.GetComponent<Entity3D>();
            if (entity != null)
            {
                return entity;
            }
        }

        return hitCollider.GetComponentInParent<Entity3D>();
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

        float maxRange = Mathf.Max(engagementRange, preferredRangeMax) + approachRangeBuffer;
        return maxRange <= 0f || (target.transform.position - transform.position).sqrMagnitude <= maxRange * maxRange;
    }

    private Vector3 ResolvePlaneBiasedDirectionToTarget(Entity3D target)
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y *= targetVerticalFollowWeight;

        if (planeReturnWeight > 0f)
        {
            float planeDelta = _preferredPlaneY - transform.position.y;
            offset.y += planeDelta * planeReturnWeight;
        }

        if (offset.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallback = target.transform.position - transform.position;
            fallback.y = 0f;
            if (fallback.sqrMagnitude <= 0.0001f)
            {
                return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
            }

            return fallback.normalized;
        }

        return offset.normalized;
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
