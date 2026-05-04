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
        None = 0,
        LaggingMachineGunRake = 1,
        BeamFence = 2,
        LegacyOrbitalEnergyPillars = 3,
        FormationMissileSalvo = 4,
        LightningSlowBeam = 5,
        EnemySpawnWave = 6,
        HelixSpiralBarrage = 7
    }

    private enum SelectableBossPattern
    {
        None,
        LaggingMachineGunRake,
        BeamFence,
        FormationMissileSalvo,
        LightningSlowBeam,
        EnemySpawnWave,
        HelixSpiralBarrage
    }

    private static readonly BossPattern[] RotatingPatterns =
    {
        BossPattern.LaggingMachineGunRake,
        BossPattern.BeamFence,
        BossPattern.FormationMissileSalvo,
        BossPattern.HelixSpiralBarrage,
        BossPattern.LightningSlowBeam,
        BossPattern.EnemySpawnWave
    };

    private sealed class PatternLaneState
    {
        public readonly int LaneIndex;
        public Entity3D Target;
        public BossPattern ActivePattern;
        public BossPattern LastPattern;
        public float NextPatternAllowedTime;
        public float NextPatternStepTime;
        public float PatternEndsAt;
        public float NextBeamAimRefreshTime;
        public float NextLightningSlowTickTime;
        public int PatternStepIndex;
        public int ActiveBeamCount;
        public int ActiveLightningBeamCount;
        public int HelixActivationIndex;

        public PatternLaneState(int laneIndex)
        {
            LaneIndex = laneIndex;
        }
    }

    [System.Serializable]
    private struct WeaponReferences
    {
        public static readonly WeaponReferences Default = new WeaponReferences();

        [Tooltip("Projectile weapons used by the lagging machine-gun rake. Wire staggered turret weapons here for the best readable hardpoint sequence.")]
        public EnemyProjectileWeaponBase3D[] laggingRakeWeapons;
        [Tooltip("Single helix spiral projectile weapon. This component owns the spiral tuning and optional one-muzzle-at-a-time hardpoint sequence.")]
        public HelixSpiralProjectileWeaponEnemy3D helixSpiralWeapon;
        [Tooltip("Formation missile salvo weapons. Each configured weapon should be a FormationMissileSalvoWeaponEnemy3D that launches a full missile bloom in one activation.")]
        public EnemyProjectileWeaponBase3D[] formationMissileSalvoWeapons;
        [Tooltip("Beam weapons used by the lagging beam convergence pattern. NetEnemyCombat3D replays these by component index, so keep the same BeamWeapon3D component order on host and client prefabs.")]
        public BeamWeapon3D[] beamFenceWeapons;
        [Tooltip("Two lightning beam weapons used by the accurate slow-beam pattern. Configure these BeamWeapon3D components with a lightning beam prefab, PlayerTeam target faction, and moderate damage per second.")]
        public BeamWeapon3D[] lightningSlowBeamWeapons;
        [Tooltip("Enemy spawner weapons used by the carrier spawn-wave pattern. Each assigned spawner controls its own prefab, spawn point, count, and delay.")]
        public EnemySpawnerWeapon3D[] enemySpawnWaveWeapons;
    }

    [System.Serializable]
    private struct MovementSettings
    {
        public static readonly MovementSettings Default = new MovementSettings
        {
            preferredRangeMin = 180f,
            preferredRangeMax = 260f,
            engagementRange = 260f,
            approachRangeBuffer = 180f,
            approachSpeedScale = 0.25f,
            retreatSpeedScale = 0.18f,
            preferredBandDriftSpeedScale = 0.12f,
            targetVerticalFollowWeight = 0.1f,
            planeReturnWeight = 0.35f
        };

        [Tooltip("Inner edge of the carrier's preferred range band. Inside this distance it backs away without trying to face the player.")]
        [Min(0f)] public float preferredRangeMin;
        [Tooltip("Outer edge of the carrier's preferred range band. Beyond this distance it approaches without trying to face the player.")]
        [Min(0f)] public float preferredRangeMax;
        [Tooltip("Maximum distance where the carrier is allowed to run attack patterns.")]
        [Min(0f)] public float engagementRange;
        [Tooltip("Extra distance beyond Engagement Range where the boss slowly approaches instead of idling.")]
        [Min(0f)] public float approachRangeBuffer;
        [Tooltip("Speed scale used while outside Preferred Range Max but inside the approach buffer.")]
        [Range(0f, 1f)] public float approachSpeedScale;
        [Tooltip("Speed scale used when the player gets inside Preferred Range Min.")]
        [Range(0f, 1f)] public float retreatSpeedScale;
        [Tooltip("Speed scale used while the carrier is already inside its preferred range band. Keeps the boss drifting toward its selected player instead of idling in place.")]
        [Range(0f, 1f)] public float preferredBandDriftSpeedScale;
        [Tooltip("How strongly movement preserves the carrier's starting horizontal plane. 0 ignores target height, 1 fully follows target height.")]
        [Range(0f, 1f)] public float targetVerticalFollowWeight;
        [Tooltip("When the carrier has drifted above/below its starting plane, this adds correction back toward that plane. 0 disables plane correction.")]
        [Range(0f, 1f)] public float planeReturnWeight;
    }

    [System.Serializable]
    private struct SequencerSettings
    {
        public static readonly SequencerSettings Default = new SequencerSettings
        {
            thinkInterval = 0.05f,
            targetHistorySampleInterval = 0.05f,
            targetHistorySamples = 16,
            minimumPatternCooldown = 1.6f,
            phaseTwoHealthPercent = 0.66f,
            forcedPatternForTesting = SelectableBossPattern.None
        };

        [Tooltip("Seconds between high-level boss target/movement decisions. Pattern aim refresh may still run more often during active beams.")]
        [Min(0.01f)] public float thinkInterval;
        [Tooltip("Seconds between target-position history samples used only when Rake History Blend is above 0.")]
        [Min(0.02f)] public float targetHistorySampleInterval;
        [Tooltip("Number of recent target positions retained for lagging attacks. Higher values allow older rake targets but use a little more memory.")]
        [Range(2, 32)] public int targetHistorySamples;
        [Tooltip("Minimum seconds between major attack patterns.")]
        [Min(0f)] public float minimumPatternCooldown;
        [Tooltip("Total durability percentage (current shield + current health divided by max shield + max health) where phase two begins and the persistent orbital pillars spawn.")]
        [Range(0.01f, 1f)] public float phaseTwoHealthPercent;
        [Tooltip("Testing override for the next attack pattern. Orbital pillars are intentionally excluded because they are a phase-transition effect, not a rotating attack.")]
        public SelectableBossPattern forcedPatternForTesting;
    }

    [System.Serializable]
    private struct RakeSettings
    {
        public static readonly RakeSettings Default = new RakeSettings
        {
            shotCount = 14,
            shotInterval = 0.12f,
            historySeconds = 0f,
            historyBlend = 0f,
            useLeadAim = true,
            leadProjectileSpeed = 0f,
            leadTimeScale = 1f,
            additionalLeadSeconds = 0.03f,
            maxLeadSeconds = 1.25f
        };

        [Tooltip("Maximum rake shots in one activation before the global Max Shots Per Pattern cap is also applied.")]
        [Min(1)] public int shotCount;
        [Tooltip("Seconds between rake shots.")]
        [Min(0.01f)] public float shotInterval;
        [Tooltip("How far behind the target's current position the optional history target sits, in seconds. Only affects aim when History Blend is above 0.")]
        [Min(0f)] public float historySeconds;
        [Tooltip("How much the rake blends from precise current/lead aim toward historical target positions. 0 is precise follow-fire, 1 is pure lagging trail fire.")]
        [Range(0f, 1f)] public float historyBlend;
        [Tooltip("If true, each rake shot predicts the target's current velocity at fire time instead of using only current position.")]
        public bool useLeadAim;
        [Tooltip("Projectile speed used for rake lead calculation. If 0, the current rake weapon's configured speed is used.")]
        [Min(0f)] public float leadProjectileSpeed;
        [Tooltip("Multiplier applied to the calculated projectile travel time when leading rake shots.")]
        [Range(0f, 2f)] public float leadTimeScale;
        [Tooltip("Extra seconds of target-velocity lead added to every rake shot after projectile travel-time lead is calculated.")]
        [Min(0f)] public float additionalLeadSeconds;
        [Tooltip("Maximum total seconds of target-velocity lead allowed for a rake shot so fast targets do not produce absurd far-ahead aim points.")]
        [Min(0f)] public float maxLeadSeconds;
    }

    [System.Serializable]
    private struct BeamFenceSettings
    {
        public static readonly BeamFenceSettings Default = new BeamFenceSettings
        {
            activeDuration = 1.2f,
            maxBeams = 4,
            aimRefreshInterval = 0.03f,
            convergenceLagSeconds = 0f,
            convergenceLagBlend = 0f,
            convergenceAimSmoothTime = 0.025f,
            allowBehindHardpointAim = true
        };

        [Tooltip("Seconds the damaging converging beams remain active.")]
        [Min(0.01f)] public float activeDuration;
        [Tooltip("Maximum beam hardpoints used in one convergence activation.")]
        [Range(1, 16)] public int maxBeams;
        [Tooltip("Seconds between beam aim refreshes while the convergence is active. Lower is smoother but sends more network updates.")]
        [Min(0.01f)] public float aimRefreshInterval;
        [Tooltip("Seconds behind the target used as the shared convergence point for all active beam hardpoints. Set this to 0 for tight live tracking.")]
        [Min(0f)] public float convergenceLagSeconds;
        [Tooltip("Blend from the target's current position toward the lagged convergence point. 0 tracks current position; 1 uses the full lagged point.")]
        [Range(0f, 1f)] public float convergenceLagBlend;
        [Tooltip("Small smoothing time for beam aim directions. Lower values track more tightly; higher values reduce long-range jitter.")]
        [Min(0f)] public float convergenceAimSmoothTime;
        [Tooltip("Allows explicit boss convergence aim to point behind a beam hardpoint's Direction Reference.")]
        public bool allowBehindHardpointAim;
    }

    [System.Serializable]
    private struct LightningSlowBeamSettings
    {
        public static readonly LightningSlowBeamSettings Default = new LightningSlowBeamSettings
        {
            activeDuration = 1.35f,
            aimRefreshInterval = 0.02f,
            beamCount = 2,
            leadSeconds = 0.12f,
            lagSeconds = 0.18f,
            lagBlend = 0.65f,
            aimSmoothTime = 0.025f,
            allowBehindHardpointAim = true,
            slowRadius = 1.25f,
            collisionMask = ~0,
            slowMultiplier = 0.45f,
            slowDuration = 0.18f,
            slowTickInterval = 0.08f
        };

        [Tooltip("Seconds the accurate lightning slow beams remain active.")]
        [Min(0.01f)] public float activeDuration;
        [Tooltip("Seconds between lightning beam aim refreshes while active. Lower values make the beams track more accurately but send more network aim updates.")]
        [Min(0.01f)] public float aimRefreshInterval;
        [Tooltip("How many lightning beams are allowed to fire in this pattern. Keep at 2 for the intended boss ability.")]
        [Range(1, 2)] public int beamCount;
        [Tooltip("Seconds of target-velocity lead added to the lightning beams.")]
        [Min(0f)] public float leadSeconds;
        [Tooltip("Seconds behind the target used by the lightning beam aim point. This gives the beam a readable follow lag like the beam fence attack.")]
        [Min(0f)] public float lagSeconds;
        [Tooltip("Blend from the lead-adjusted target point toward the lagged lightning aim point. 0 keeps the old accurate tracking, 1 uses the full lagged point.")]
        [Range(0f, 1f)] public float lagBlend;
        [Tooltip("Small smoothing time for lightning aim. Keep low so the slow beams stay threatening and accurate.")]
        [Min(0f)] public float aimSmoothTime;
        [Tooltip("Allows explicit lightning slow-beam aim to point behind a beam hardpoint's Direction Reference.")]
        public bool allowBehindHardpointAim;
        [Tooltip("Radius used by the boss brain's slow check along each lightning beam.")]
        [Min(0f)] public float slowRadius;
        [Tooltip("Layers considered by the boss brain when checking whether a lightning beam has line-of-sight to the player for slow application.")]
        public LayerMask collisionMask;
        [Tooltip("Movement multiplier applied while the lightning slow beam is hitting the player. 0.45 means the player moves at 45% speed.")]
        [Range(0f, 1f)] public float slowMultiplier;
        [Tooltip("Duration of each refreshed slow pulse. This should be slightly longer than Slow Tick Interval so the slow does not flicker between beam ticks.")]
        [Min(0f)] public float slowDuration;
        [Tooltip("Seconds between server-authoritative slow checks while the lightning beams are active.")]
        [Min(0.01f)] public float slowTickInterval;
    }

    [System.Serializable]
    private struct OrbitalPillarSettings
    {
        public static readonly OrbitalPillarSettings Default = new OrbitalPillarSettings
        {
            count = 6,
            ringRadius = 115f,
            gapDegrees = 70f,
            sphereTravelDuration = 0.85f,
            expandDuration = 0.3f,
            orbitDegreesPerSecond = 12f,
            damageRadius = 16f,
            damageHalfHeight = 3000f,
            damagePerSecond = 35f,
            damageTickInterval = 0.1f,
            damageMask = ~0
        };

        [Tooltip("Number of vertical energy pillars placed around the carrier when phase two begins.")]
        [Range(1, 16)] public int count;
        [Tooltip("World-space radius of the ring where the launched orbs settle before becoming pillars. Runtime clamps this above the damage radius so multiple pillars cannot collapse into the same visual target.")]
        [Min(0f)] public float ringRadius;
        [Tooltip("Empty arc centered on the player direction when the phase transition starts. This creates an intentional escape gap in the boss-centered pillar ring.")]
        [Range(0f, 330f)] public float gapDegrees;
        [Tooltip("Seconds the launched orbs take to drift from the carrier face into their ring positions.")]
        [Min(0.01f)] public float sphereTravelDuration;
        [Tooltip("Seconds the cylinders take to expand from the orbs to full damage radius.")]
        [Min(0.01f)] public float expandDuration;
        [Tooltip("Degrees per second that the persistent phase-two pillars orbit around the Siege Carrier.")]
        public float orbitDegreesPerSecond;
        [Tooltip("Gameplay radius of each vertical energy pillar.")]
        [Min(0.01f)] public float damageRadius;
        [Tooltip("Half-height used for server-authoritative damage checks. Keep this taller than the arena so gameplay feels endless without using an actually infinite query.")]
        [Min(0.01f)] public float damageHalfHeight;
        [Tooltip("Damage per second applied to player-team entities inside an active pillar.")]
        [Min(0f)] public float damagePerSecond;
        [Tooltip("Seconds between server-authoritative pillar damage ticks.")]
        [Min(0.01f)] public float damageTickInterval;
        [Tooltip("Layers considered by the boss pillar damage check. Set this to the player hitbox/body layers on the prefab.")]
        public LayerMask damageMask;
    }

    [System.Serializable]
    private struct OrbitalPillarVisualSettings
    {
        public static readonly OrbitalPillarVisualSettings Default = new OrbitalPillarVisualSettings
        {
            launchScale = 10f,
            launchOffset = 18f,
            initialGrowthHeight = 24f
        };

        [Tooltip("Visual prefab launched from the carrier face before it becomes an orbital energy pillar.")]
        public GameObject launchSpearPrefab;
        [Tooltip("Visual prefab used for each blue orbital energy pillar after the launch spear reaches its ring position.")]
        public GameObject bluePillarPrefab;
        [Tooltip("World-space scale applied to each launched spear prefab.")]
        [Min(0.01f)] public float launchScale;
        [Tooltip("World units forward from the carrier origin where launch spears appear.")]
        [Min(0f)] public float launchOffset;
        [Tooltip("World-space pillar height on the first frame of pillar growth. Final height comes from Orbital Pillars Damage Half Height.")]
        [Min(0.01f)] public float initialGrowthHeight;
    }

    [Header("Foldout Sections")]
    [SerializeField] private WeaponReferences weapons = WeaponReferences.Default;
    [SerializeField] private MovementSettings movement = MovementSettings.Default;
    [SerializeField] private SequencerSettings sequencer = SequencerSettings.Default;
    [SerializeField] private RakeSettings rake = RakeSettings.Default;
    [SerializeField] private BeamFenceSettings beamFence = BeamFenceSettings.Default;
    [SerializeField] private LightningSlowBeamSettings lightningSlowBeam = LightningSlowBeamSettings.Default;
    [SerializeField] private OrbitalPillarSettings orbitalPillars = OrbitalPillarSettings.Default;
    [SerializeField] private OrbitalPillarVisualSettings orbitalPillarVisuals = OrbitalPillarVisualSettings.Default;

    [Header("Debug")]
    [Tooltip("Logs the Siege Carrier phase-two health check and orbital-pillar startup result. Enable only while debugging the boss phase transition.")]
    [SerializeField] private bool logPhaseTransitionDebug;

    [Header("Legacy Migration")]
    [SerializeField, HideInInspector] private bool migratedLegacyInspectorSettings;
    [SerializeField, HideInInspector] private EnemyProjectileWeaponBase3D[] laggingRakeWeapons;
    [SerializeField, HideInInspector] private EnemyProjectileWeaponBase3D[] formationMissileSalvoWeapons;
    [SerializeField, HideInInspector] private BeamWeapon3D[] beamFenceWeapons;
    [SerializeField, HideInInspector] private BeamWeapon3D[] lightningSlowBeamWeapons;
    [SerializeField, HideInInspector] private EnemySpawnerWeapon3D[] enemySpawnWaveWeapons;
    [SerializeField, HideInInspector] private float thinkInterval = 0.05f;
    [SerializeField, HideInInspector] private float targetHistorySampleInterval = 0.05f;
    [SerializeField, HideInInspector] private int targetHistorySamples = 16;
    [SerializeField, HideInInspector] private float preferredRangeMin = 180f;
    [SerializeField, HideInInspector] private float preferredRangeMax = 260f;
    [SerializeField, HideInInspector] private float engagementRange = 260f;
    [SerializeField, HideInInspector] private float approachRangeBuffer = 180f;
    [SerializeField, HideInInspector] private float approachSpeedScale = 0.25f;
    [SerializeField, HideInInspector] private float retreatSpeedScale = 0.18f;
    [SerializeField, HideInInspector] private float preferredBandDriftSpeedScale = 0.12f;
    [SerializeField, HideInInspector] private float targetVerticalFollowWeight = 0.1f;
    [SerializeField, HideInInspector] private float planeReturnWeight = 0.35f;
    [SerializeField, HideInInspector] private float minimumPatternCooldown = 1.6f;
    [SerializeField, HideInInspector] private float phaseTwoHealthPercent = 0.66f;
    [SerializeField, HideInInspector] private BossPattern forcedPatternForTesting = BossPattern.None;
    [SerializeField, HideInInspector] private int rakeShotCount = 14;
    [SerializeField, HideInInspector] private float rakeShotInterval = 0.12f;
    [SerializeField, HideInInspector] private float rakeHistorySeconds;
    [SerializeField, HideInInspector] private float rakeHistoryBlend;
    [SerializeField, HideInInspector] private bool rakeUseLeadAim = true;
    [SerializeField, HideInInspector] private float rakeLeadProjectileSpeed;
    [SerializeField, HideInInspector] private float rakeLeadTimeScale = 1f;
    [SerializeField, HideInInspector] private float rakeAdditionalLeadSeconds = 0.03f;
    [SerializeField, HideInInspector] private float rakeMaxLeadSeconds = 1.25f;
    [SerializeField, HideInInspector] private HelixSpiralProjectileWeaponEnemy3D helixSpiralWeapon;
    [SerializeField, HideInInspector] private float beamFenceActiveDuration = 1.2f;
    [SerializeField, HideInInspector] private int beamFenceMaxBeams = 4;
    [SerializeField, HideInInspector] private float beamFenceAimRefreshInterval = 0.03f;
    [SerializeField, HideInInspector] private float beamConvergenceLagSeconds;
    [SerializeField, HideInInspector] private float beamConvergenceLagBlend;
    [SerializeField, HideInInspector] private float beamConvergenceAimSmoothTime = 0.025f;
    [SerializeField, HideInInspector] private bool beamConvergenceAllowBehindHardpointAim = true;
    [SerializeField, HideInInspector] private float lightningSlowBeamActiveDuration = 1.35f;
    [SerializeField, HideInInspector] private float lightningSlowBeamAimRefreshInterval = 0.02f;
    [SerializeField, HideInInspector] private int lightningSlowBeamCount = 2;
    [SerializeField, HideInInspector] private float lightningSlowBeamLeadSeconds = 0.12f;
    [SerializeField, HideInInspector] private float lightningSlowBeamLagSeconds = 0.18f;
    [SerializeField, HideInInspector] private float lightningSlowBeamLagBlend = 0.65f;
    [SerializeField, HideInInspector] private float lightningSlowBeamAimSmoothTime = 0.025f;
    [SerializeField, HideInInspector] private bool lightningSlowBeamAllowBehindHardpointAim = true;
    [SerializeField, HideInInspector] private float lightningSlowBeamSlowRadius = 1.25f;
    [SerializeField, HideInInspector] private LayerMask lightningSlowBeamCollisionMask = ~0;
    [SerializeField, HideInInspector] private float lightningSlowBeamSlowMultiplier = 0.45f;
    [SerializeField, HideInInspector] private float lightningSlowBeamSlowDuration = 0.18f;
    [SerializeField, HideInInspector] private float lightningSlowBeamSlowTickInterval = 0.08f;
    [SerializeField, HideInInspector] private int orbitalPillarCount = 6;
    [SerializeField, HideInInspector] private float orbitalPillarRingRadius = 115f;
    [SerializeField, HideInInspector] private float orbitalPillarGapDegrees = 70f;
    [SerializeField, HideInInspector] private float orbitalPillarSphereTravelDuration = 0.85f;
    [SerializeField, HideInInspector] private float orbitalPillarExpandDuration = 0.3f;
    [SerializeField, HideInInspector] private float orbitalPillarDamageRadius = 16f;
    [SerializeField, HideInInspector] private float orbitalPillarDamageHalfHeight = 3000f;
    [SerializeField, HideInInspector] private float orbitalPillarDamagePerSecond = 35f;
    [SerializeField, HideInInspector] private float orbitalPillarDamageTickInterval = 0.1f;
    [SerializeField, HideInInspector] private LayerMask orbitalPillarDamageMask = ~0;

    [Header("References")]
    [Tooltip("AI flight motor that lets the boss slowly approach outside range and face the current player while anchored.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. The Siege Carrier expects this to target PlayerTeam.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("No-target search fallback. The boss uses this only when it cannot see a player-team target.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Tooltip("Enemy combat broker used for server-authoritative projectile and beam replication.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    private NetworkObject _networkObject;
    private Enemy3D _enemy;
    private Vector3[] _targetHistory;
    private int _historyWriteIndex;
    private int _historyCount;
    private float _nextHistorySampleTime;
    private float _nextThinkTime;
    private float _preferredPlaneY;
    private Vector3[] _beamConvergenceSmoothedDirections;
    private float[] _beamConvergenceLastSmoothTimes;
    private bool[] _beamConvergenceHasSmoothedDirections;
    private Vector3[] _lightningSmoothedDirections;
    private float[] _lightningLastSmoothTimes;
    private bool[] _lightningHasSmoothedDirections;
    private readonly RaycastHit[] _lightningSlowHits = new RaycastHit[8];
    private readonly Collider[] _orbitalPillarHits = new Collider[64];
    private readonly Entity3D[] _orbitalPillarDamagedThisTick = new Entity3D[16];
    private Vector3[] _orbitalPillarCenters;
    private GameObject[] _orbitalPillarLaunchInstances;
    private Transform[] _orbitalPillarLaunchTransforms;
    private GameObject[] _orbitalPillarVisualInstances;
    private Transform[] _orbitalPillarVisualTransforms;
    private Vector3 _orbitalPillarVisualOrigin;
    private Vector3 _orbitalPillarVisualFaceForward;
    private Vector3 _orbitalPillarVisualGapDirection;
    private float _orbitalPillarVisualStartTime;
    private float _orbitalPillarOrbitStartTime;
    private bool _isOrbitalPillarVisualPlaying;
    private int _activeOrbitalPillarCount;
    private bool _isPhaseTwoOrbitalPillarsActive;
    private bool _warnedMissingOrbitalPillarVisuals;
    private bool _loggedPhaseDebugNoAuthority;
    private float _nextPhaseDebugLogTime;
    private float _nextOrbitalPillarDamageTickTime;
    private readonly PatternLaneState[] _patternLanes =
    {
        new PatternLaneState(0),
        new PatternLaneState(1)
    };
    private readonly BossPattern[] _patternSelectionBuffer = new BossPattern[RotatingPatterns.Length];
    private Entity3D _currentTarget;

    private void Awake()
    {
        MigrateLegacyInspectorSettingsIfNeeded();
        if (movement.preferredBandDriftSpeedScale <= 0f)
        {
            movement.preferredBandDriftSpeedScale = MovementSettings.Default.preferredBandDriftSpeedScale;
        }

        _networkObject = GetComponent<NetworkObject>();
        _enemy = GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        _preferredPlaneY = transform.position.y;
        EnsureTargetHistoryBuffer();
        EnsureBeamConvergenceBuffers();
        EnsureLightningSlowBeamBuffers();
        EnsureOrbitalPillarBuffers();
    }

    private void OnValidate()
    {
        MigrateLegacyInspectorSettingsIfNeeded();
        sequencer.thinkInterval = Mathf.Max(0.01f, sequencer.thinkInterval);
        sequencer.targetHistorySampleInterval = Mathf.Max(0.02f, sequencer.targetHistorySampleInterval);
        sequencer.targetHistorySamples = Mathf.Clamp(sequencer.targetHistorySamples, 2, 32);
        movement.preferredRangeMin = Mathf.Max(0f, movement.preferredRangeMin);
        movement.preferredRangeMax = Mathf.Max(movement.preferredRangeMin, movement.preferredRangeMax);
        movement.engagementRange = Mathf.Max(0f, movement.engagementRange);
        movement.approachRangeBuffer = Mathf.Max(0f, movement.approachRangeBuffer);
        movement.approachSpeedScale = Mathf.Clamp01(movement.approachSpeedScale);
        movement.retreatSpeedScale = Mathf.Clamp01(movement.retreatSpeedScale);
        movement.preferredBandDriftSpeedScale = movement.preferredBandDriftSpeedScale > 0f
            ? Mathf.Clamp01(movement.preferredBandDriftSpeedScale)
            : MovementSettings.Default.preferredBandDriftSpeedScale;
        movement.targetVerticalFollowWeight = Mathf.Clamp01(movement.targetVerticalFollowWeight);
        movement.planeReturnWeight = Mathf.Clamp01(movement.planeReturnWeight);
        sequencer.minimumPatternCooldown = Mathf.Max(0f, sequencer.minimumPatternCooldown);
        sequencer.phaseTwoHealthPercent = Mathf.Clamp(sequencer.phaseTwoHealthPercent, 0.01f, 1f);
        rake.shotCount = Mathf.Max(1, rake.shotCount);
        rake.shotInterval = Mathf.Max(0.01f, rake.shotInterval);
        rake.historySeconds = Mathf.Max(0f, rake.historySeconds);
        rake.historyBlend = Mathf.Clamp01(rake.historyBlend);
        rake.leadProjectileSpeed = Mathf.Max(0f, rake.leadProjectileSpeed);
        rake.leadTimeScale = Mathf.Clamp(rake.leadTimeScale, 0f, 2f);
        rake.additionalLeadSeconds = Mathf.Max(0f, rake.additionalLeadSeconds);
        rake.maxLeadSeconds = Mathf.Max(0f, rake.maxLeadSeconds);
        beamFence.activeDuration = Mathf.Max(0.01f, beamFence.activeDuration);
        beamFence.maxBeams = Mathf.Clamp(beamFence.maxBeams, 1, 16);
        beamFence.aimRefreshInterval = Mathf.Max(0.01f, beamFence.aimRefreshInterval);
        beamFence.convergenceLagSeconds = Mathf.Max(0f, beamFence.convergenceLagSeconds);
        beamFence.convergenceLagBlend = Mathf.Clamp01(beamFence.convergenceLagBlend);
        beamFence.convergenceAimSmoothTime = Mathf.Max(0f, beamFence.convergenceAimSmoothTime);
        lightningSlowBeam.activeDuration = Mathf.Max(0.01f, lightningSlowBeam.activeDuration);
        lightningSlowBeam.aimRefreshInterval = Mathf.Max(0.01f, lightningSlowBeam.aimRefreshInterval);
        lightningSlowBeam.beamCount = Mathf.Clamp(lightningSlowBeam.beamCount, 1, 2);
        lightningSlowBeam.leadSeconds = Mathf.Max(0f, lightningSlowBeam.leadSeconds);
        lightningSlowBeam.lagSeconds = Mathf.Max(0f, lightningSlowBeam.lagSeconds);
        lightningSlowBeam.lagBlend = Mathf.Clamp01(lightningSlowBeam.lagBlend);
        lightningSlowBeam.aimSmoothTime = Mathf.Max(0f, lightningSlowBeam.aimSmoothTime);
        lightningSlowBeam.slowRadius = Mathf.Max(0f, lightningSlowBeam.slowRadius);
        lightningSlowBeam.slowMultiplier = Mathf.Clamp01(lightningSlowBeam.slowMultiplier);
        lightningSlowBeam.slowDuration = Mathf.Max(0f, lightningSlowBeam.slowDuration);
        lightningSlowBeam.slowTickInterval = Mathf.Max(0.01f, lightningSlowBeam.slowTickInterval);
        orbitalPillars.count = Mathf.Clamp(orbitalPillars.count, 1, 16);
        orbitalPillars.gapDegrees = Mathf.Clamp(orbitalPillars.gapDegrees, 0f, 330f);
        orbitalPillars.sphereTravelDuration = Mathf.Max(0.01f, orbitalPillars.sphereTravelDuration);
        orbitalPillars.expandDuration = Mathf.Max(0.01f, orbitalPillars.expandDuration);
        orbitalPillars.damageRadius = Mathf.Max(0.01f, orbitalPillars.damageRadius);
        orbitalPillars.ringRadius = ResolveOrbitalPillarRingRadius();
        orbitalPillars.damageHalfHeight = Mathf.Max(orbitalPillars.damageRadius, orbitalPillars.damageHalfHeight);
        orbitalPillars.damagePerSecond = Mathf.Max(0f, orbitalPillars.damagePerSecond);
        orbitalPillars.damageTickInterval = Mathf.Max(0.01f, orbitalPillars.damageTickInterval);
        orbitalPillarVisuals.launchScale = Mathf.Max(0.01f, orbitalPillarVisuals.launchScale);
        orbitalPillarVisuals.launchOffset = Mathf.Max(0f, orbitalPillarVisuals.launchOffset);
        orbitalPillarVisuals.initialGrowthHeight = Mathf.Max(0.01f, orbitalPillarVisuals.initialGrowthHeight);
    }

    private void MigrateLegacyInspectorSettingsIfNeeded()
    {
        if (migratedLegacyInspectorSettings)
        {
            return;
        }

        weapons.laggingRakeWeapons = laggingRakeWeapons;
        weapons.helixSpiralWeapon = helixSpiralWeapon;
        weapons.formationMissileSalvoWeapons = formationMissileSalvoWeapons;
        weapons.beamFenceWeapons = beamFenceWeapons;
        weapons.lightningSlowBeamWeapons = lightningSlowBeamWeapons;
        weapons.enemySpawnWaveWeapons = enemySpawnWaveWeapons;

        sequencer.thinkInterval = thinkInterval;
        sequencer.targetHistorySampleInterval = targetHistorySampleInterval;
        sequencer.targetHistorySamples = targetHistorySamples;
        sequencer.minimumPatternCooldown = minimumPatternCooldown;
        sequencer.phaseTwoHealthPercent = phaseTwoHealthPercent;
        sequencer.forcedPatternForTesting = ToSelectableBossPattern(forcedPatternForTesting);

        movement.preferredRangeMin = preferredRangeMin;
        movement.preferredRangeMax = preferredRangeMax;
        movement.engagementRange = engagementRange;
        movement.approachRangeBuffer = approachRangeBuffer;
        movement.approachSpeedScale = approachSpeedScale;
        movement.retreatSpeedScale = retreatSpeedScale;
        movement.preferredBandDriftSpeedScale = preferredBandDriftSpeedScale;
        movement.targetVerticalFollowWeight = targetVerticalFollowWeight;
        movement.planeReturnWeight = planeReturnWeight;

        rake.shotCount = rakeShotCount;
        rake.shotInterval = rakeShotInterval;
        rake.historySeconds = rakeHistorySeconds;
        rake.historyBlend = rakeHistoryBlend;
        rake.useLeadAim = rakeUseLeadAim;
        rake.leadProjectileSpeed = rakeLeadProjectileSpeed;
        rake.leadTimeScale = rakeLeadTimeScale;
        rake.additionalLeadSeconds = rakeAdditionalLeadSeconds;
        rake.maxLeadSeconds = rakeMaxLeadSeconds;

        beamFence.activeDuration = beamFenceActiveDuration;
        beamFence.maxBeams = beamFenceMaxBeams;
        beamFence.aimRefreshInterval = beamFenceAimRefreshInterval;
        beamFence.convergenceLagSeconds = beamConvergenceLagSeconds;
        beamFence.convergenceLagBlend = beamConvergenceLagBlend;
        beamFence.convergenceAimSmoothTime = beamConvergenceAimSmoothTime;
        beamFence.allowBehindHardpointAim = beamConvergenceAllowBehindHardpointAim;

        lightningSlowBeam.activeDuration = lightningSlowBeamActiveDuration;
        lightningSlowBeam.aimRefreshInterval = lightningSlowBeamAimRefreshInterval;
        lightningSlowBeam.beamCount = lightningSlowBeamCount;
        lightningSlowBeam.leadSeconds = lightningSlowBeamLeadSeconds;
        lightningSlowBeam.lagSeconds = lightningSlowBeamLagSeconds;
        lightningSlowBeam.lagBlend = lightningSlowBeamLagBlend;
        lightningSlowBeam.aimSmoothTime = lightningSlowBeamAimSmoothTime;
        lightningSlowBeam.allowBehindHardpointAim = lightningSlowBeamAllowBehindHardpointAim;
        lightningSlowBeam.slowRadius = lightningSlowBeamSlowRadius;
        lightningSlowBeam.collisionMask = lightningSlowBeamCollisionMask;
        lightningSlowBeam.slowMultiplier = lightningSlowBeamSlowMultiplier;
        lightningSlowBeam.slowDuration = lightningSlowBeamSlowDuration;
        lightningSlowBeam.slowTickInterval = lightningSlowBeamSlowTickInterval;

        orbitalPillars.count = orbitalPillarCount;
        orbitalPillars.ringRadius = orbitalPillarRingRadius;
        orbitalPillars.gapDegrees = orbitalPillarGapDegrees;
        orbitalPillars.sphereTravelDuration = orbitalPillarSphereTravelDuration;
        orbitalPillars.expandDuration = orbitalPillarExpandDuration;
        orbitalPillars.damageRadius = orbitalPillarDamageRadius;
        orbitalPillars.damageHalfHeight = orbitalPillarDamageHalfHeight;
        orbitalPillars.damagePerSecond = orbitalPillarDamagePerSecond;
        orbitalPillars.damageTickInterval = orbitalPillarDamageTickInterval;
        orbitalPillars.damageMask = orbitalPillarDamageMask;

        migratedLegacyInspectorSettings = true;
    }

    private void OnDisable()
    {
        CancelAllPatternLanes();
        StopEnemySpawnWave();
        StopOrbitalEnergyPillars(immediate: true);
        _isPhaseTwoOrbitalPillarsActive = false;
        _currentTarget = null;
        flightController?.ClearFlightIntent();
    }

    private void Update()
    {
        TickOrbitalEnergyPillarVisuals();

        if (!HasBrainAuthority())
        {
            LogPhaseTransitionDebugNoAuthority();
            flightController?.ClearFlightIntent();
            ClearAllPatternLaneState();
            _currentTarget = null;
            return;
        }

        Entity3D target = ResolveTargets();
        SampleTargetHistory(target);
        TickPhaseTransitionEffects(target);

        if (!IsTargetValid(target))
        {
            TickPersistentOrbitalEnergyPillars();
            if (!_isPhaseTwoOrbitalPillarsActive)
            {
                CancelAllPatternLanes();
            }
            PatrolOrClearFlightIntent();
            return;
        }

        UpdateMovement(target);
        TickPersistentOrbitalEnergyPillars();
        if (!IsInsideMaxEngagement(target))
        {
            if (!_isPhaseTwoOrbitalPillarsActive)
            {
                CancelAllPatternLanes();
            }
            return;
        }

        for (int i = 0; i < _patternLanes.Length; i++)
        {
            TickPatternLane(_patternLanes[i]);
        }
    }

    public void ApplyProfile(EnemyBalanceProfile3D.SiegeCarrierBossBrainStats stats)
    {
        sequencer.thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        sequencer.targetHistorySampleInterval = Mathf.Max(0.02f, stats.targetHistorySampleInterval);
        sequencer.targetHistorySamples = Mathf.Clamp(stats.targetHistorySamples, 2, 32);
        movement.preferredRangeMin = Mathf.Max(0f, stats.preferredRangeMin);
        movement.preferredRangeMax = Mathf.Max(movement.preferredRangeMin, stats.preferredRangeMax);
        movement.engagementRange = Mathf.Max(0f, stats.engagementRange);
        movement.approachRangeBuffer = Mathf.Max(0f, stats.approachRangeBuffer);
        movement.approachSpeedScale = Mathf.Clamp01(stats.approachSpeedScale);
        movement.retreatSpeedScale = Mathf.Clamp01(stats.retreatSpeedScale);
        movement.preferredBandDriftSpeedScale = stats.preferredBandDriftSpeedScale > 0f
            ? Mathf.Clamp01(stats.preferredBandDriftSpeedScale)
            : MovementSettings.Default.preferredBandDriftSpeedScale;
        movement.targetVerticalFollowWeight = Mathf.Clamp01(stats.targetVerticalFollowWeight);
        movement.planeReturnWeight = Mathf.Clamp01(stats.planeReturnWeight);
        sequencer.minimumPatternCooldown = Mathf.Max(0f, stats.minimumPatternCooldown);
        sequencer.phaseTwoHealthPercent = Mathf.Clamp(stats.phaseTwoHealthPercent, 0.01f, 1f);
        rake.shotCount = Mathf.Max(1, stats.rakeShotCount);
        rake.shotInterval = Mathf.Max(0.01f, stats.rakeShotInterval);
        rake.historySeconds = Mathf.Max(0f, stats.rakeHistorySeconds);
        rake.historyBlend = Mathf.Clamp01(stats.rakeHistoryBlend);
        rake.useLeadAim = stats.rakeUseLeadAim;
        rake.leadProjectileSpeed = Mathf.Max(0f, stats.rakeLeadProjectileSpeed);
        rake.leadTimeScale = Mathf.Clamp(stats.rakeLeadTimeScale, 0f, 2f);
        rake.additionalLeadSeconds = Mathf.Max(0f, stats.rakeAdditionalLeadSeconds);
        rake.maxLeadSeconds = Mathf.Max(0f, stats.rakeMaxLeadSeconds);
        beamFence.activeDuration = Mathf.Max(0.01f, stats.beamFenceActiveDuration);
        beamFence.maxBeams = Mathf.Clamp(stats.beamFenceMaxBeams, 1, 16);
        beamFence.aimRefreshInterval = Mathf.Max(0.01f, stats.beamFenceAimRefreshInterval);
        beamFence.convergenceLagSeconds = Mathf.Max(0f, stats.beamConvergenceLagSeconds);
        beamFence.convergenceLagBlend = Mathf.Clamp01(stats.beamConvergenceLagBlend);
        beamFence.convergenceAimSmoothTime = Mathf.Max(0f, stats.beamConvergenceAimSmoothTime);
        lightningSlowBeam.activeDuration = Mathf.Max(0.01f, stats.lightningSlowBeamActiveDuration);
        lightningSlowBeam.aimRefreshInterval = Mathf.Max(0.01f, stats.lightningSlowBeamAimRefreshInterval);
        lightningSlowBeam.beamCount = Mathf.Clamp(stats.lightningSlowBeamCount, 1, 2);
        lightningSlowBeam.leadSeconds = Mathf.Max(0f, stats.lightningSlowBeamLeadSeconds);
        lightningSlowBeam.lagSeconds = Mathf.Max(0f, stats.lightningSlowBeamLagSeconds);
        lightningSlowBeam.lagBlend = Mathf.Clamp01(stats.lightningSlowBeamLagBlend);
        lightningSlowBeam.aimSmoothTime = Mathf.Max(0f, stats.lightningSlowBeamAimSmoothTime);
        lightningSlowBeam.slowRadius = Mathf.Max(0f, stats.lightningSlowBeamSlowRadius);
        lightningSlowBeam.slowMultiplier = Mathf.Clamp01(stats.lightningSlowBeamSlowMultiplier);
        lightningSlowBeam.slowDuration = Mathf.Max(0f, stats.lightningSlowBeamSlowDuration);
        lightningSlowBeam.slowTickInterval = Mathf.Max(0.01f, stats.lightningSlowBeamSlowTickInterval);
        orbitalPillars.count = Mathf.Clamp(stats.orbitalPillarCount, 1, 16);
        orbitalPillars.ringRadius = Mathf.Max(0f, stats.orbitalPillarRingRadius);
        orbitalPillars.gapDegrees = Mathf.Clamp(stats.orbitalPillarGapDegrees, 0f, 330f);
        orbitalPillars.sphereTravelDuration = Mathf.Max(0.01f, stats.orbitalPillarSphereTravelDuration);
        orbitalPillars.expandDuration = Mathf.Max(0.01f, stats.orbitalPillarExpandDuration);
        orbitalPillars.orbitDegreesPerSecond = stats.orbitalPillarOrbitDegreesPerSecond;
        orbitalPillars.damageRadius = Mathf.Max(0.01f, stats.orbitalPillarDamageRadius);
        orbitalPillars.damageHalfHeight = Mathf.Max(orbitalPillars.damageRadius, stats.orbitalPillarDamageHalfHeight);
        orbitalPillars.damagePerSecond = Mathf.Max(0f, stats.orbitalPillarDamagePerSecond);
        orbitalPillars.damageTickInterval = Mathf.Max(0.01f, stats.orbitalPillarDamageTickInterval);
        EnsureTargetHistoryBuffer();
        EnsureBeamConvergenceBuffers();
        EnsureLightningSlowBeamBuffers();
        EnsureOrbitalPillarBuffers();
    }

    private Entity3D ResolveTargets()
    {
        if (Time.time >= _nextThinkTime || !IsTargetValid(_currentTarget))
        {
            _nextThinkTime = Time.time + Mathf.Max(0.01f, sequencer.thinkInterval);
            RefreshPatternLaneTargets();
        }

        return _currentTarget;
    }

    private void RefreshPatternLaneTargets()
    {
        Entity3D primaryTarget = null;
        Entity3D secondaryTarget = null;
        float primaryDistanceSqr = float.PositiveInfinity;
        float secondaryDistanceSqr = float.PositiveInfinity;

        Entity3D[] entities = FindObjectsByType<Entity3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D candidate = entities[i];
            if (!IsActivePlayerTarget(candidate) || !IsInsideMaxEngagement(candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < primaryDistanceSqr)
            {
                secondaryTarget = primaryTarget;
                secondaryDistanceSqr = primaryDistanceSqr;
                primaryTarget = candidate;
                primaryDistanceSqr = distanceSqr;
            }
            else if (distanceSqr < secondaryDistanceSqr)
            {
                secondaryTarget = candidate;
                secondaryDistanceSqr = distanceSqr;
            }
        }

        if (primaryTarget == null && targetSensor != null)
        {
            Entity3D sensorTarget = targetSensor.GetTarget();
            if (IsActivePlayerTarget(sensorTarget) && IsInsideMaxEngagement(sensorTarget))
            {
                primaryTarget = sensorTarget;
            }
        }

        AssignLaneTarget(_patternLanes[0], primaryTarget);
        AssignLaneTarget(_patternLanes[1], secondaryTarget);
        _currentTarget = primaryTarget;
    }

    private void AssignLaneTarget(PatternLaneState lane, Entity3D target)
    {
        if (lane == null)
        {
            return;
        }

        if (!IsTargetValid(target))
        {
            lane.Target = null;
            CancelPatternLane(lane);
            return;
        }

        if (lane.Target != target)
        {
            lane.Target = target;
        }
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
        if (distance > movement.preferredRangeMax)
        {
            flightController?.SetFlightIntent(planarDirectionToTarget, planarDirectionToTarget, movement.approachSpeedScale, moveBackward: false);
            return;
        }

        if (distance < movement.preferredRangeMin)
        {
            Vector3 retreatDirection = -planarDirectionToTarget;
            flightController?.SetFlightIntent(retreatDirection, retreatDirection, movement.retreatSpeedScale, moveBackward: false);
            return;
        }

        flightController?.SetFlightIntent(planarDirectionToTarget, planarDirectionToTarget, movement.preferredBandDriftSpeedScale, moveBackward: false);
    }

    private void TickPatternLane(PatternLaneState lane)
    {
        if (lane == null || !IsTargetValid(lane.Target) || !IsInsideMaxEngagement(lane.Target))
        {
            CancelPatternLane(lane);
            return;
        }

        switch (lane.ActivePattern)
        {
            case BossPattern.LaggingMachineGunRake:
                TickLaggingRake(lane);
                break;
            case BossPattern.BeamFence:
                TickBeamFence(lane);
                break;
            case BossPattern.FormationMissileSalvo:
                TickFormationMissileSalvo(lane);
                break;
            case BossPattern.HelixSpiralBarrage:
                TickHelixSpiralBarrage(lane);
                break;
            case BossPattern.LightningSlowBeam:
                TickLightningSlowBeam(lane);
                break;
            case BossPattern.EnemySpawnWave:
                TickEnemySpawnWave(lane);
                break;
        }

        if (lane.ActivePattern == BossPattern.None && Time.time >= lane.NextPatternAllowedTime)
        {
            StartNextPattern(lane);
        }
    }

    private void StartNextPattern(PatternLaneState lane)
    {
        if (lane == null || !IsTargetValid(lane.Target))
        {
            return;
        }

        BossPattern blockedPattern = ResolveOtherLaneActivePattern(lane);
        if (sequencer.forcedPatternForTesting != SelectableBossPattern.None)
        {
            BossPattern forcedPattern = ToBossPattern(sequencer.forcedPatternForTesting);
            if (CanRunPattern(forcedPattern) && forcedPattern != blockedPattern)
            {
                BeginPattern(lane, forcedPattern);
            }
            else
            {
                lane.NextPatternAllowedTime = Time.time + ResolvePatternCooldown();
            }

            return;
        }

        int candidateCount = 0;
        for (int i = 0; i < RotatingPatterns.Length; i++)
        {
            BossPattern candidate = RotatingPatterns[i];
            if (candidate == blockedPattern || !CanRunPattern(candidate))
            {
                continue;
            }

            _patternSelectionBuffer[candidateCount] = candidate;
            candidateCount++;
        }

        if (candidateCount <= 0)
        {
            lane.NextPatternAllowedTime = Time.time + ResolvePatternCooldown();
            return;
        }

        int eligibleCount = candidateCount;
        if (candidateCount > 1 && lane.LastPattern != BossPattern.None)
        {
            eligibleCount = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                BossPattern candidate = _patternSelectionBuffer[i];
                if (candidate == lane.LastPattern)
                {
                    continue;
                }

                _patternSelectionBuffer[eligibleCount] = candidate;
                eligibleCount++;
            }

            if (eligibleCount <= 0)
            {
                eligibleCount = candidateCount;
            }
        }

        BeginPattern(lane, _patternSelectionBuffer[Random.Range(0, eligibleCount)]);
    }

    private void TickPhaseTransitionEffects(Entity3D target)
    {
        bool isInPhaseTwo = IsInPhaseTwoOrLower();
        LogPhaseTransitionDebug(isInPhaseTwo);
        if (isInPhaseTwo)
        {
            StartPersistentOrbitalEnergyPillars(target);
        }
    }

    private void BeginPattern(PatternLaneState lane, BossPattern pattern)
    {
        lane.ActivePattern = pattern;
        lane.PatternStepIndex = 0;
        lane.NextPatternStepTime = Time.time;

        if (pattern == BossPattern.HelixSpiralBarrage)
        {
            lane.HelixActivationIndex++;
            weapons.helixSpiralWeapon.ResetMuzzleSequence();
            return;
        }

        if (pattern == BossPattern.BeamFence)
        {
            lane.ActiveBeamCount = Mathf.Min(weapons.beamFenceWeapons != null ? weapons.beamFenceWeapons.Length : 0, beamFence.maxBeams);
            lane.PatternEndsAt = Time.time + beamFence.activeDuration;
            ResetBeamConvergenceSmoothing();
            return;
        }

        if (pattern == BossPattern.LightningSlowBeam)
        {
            lane.ActiveLightningBeamCount = Mathf.Min(weapons.lightningSlowBeamWeapons != null ? weapons.lightningSlowBeamWeapons.Length : 0, lightningSlowBeam.beamCount);
            lane.PatternEndsAt = Time.time + lightningSlowBeam.activeDuration;
            ResetLightningSlowBeamSmoothing();
            return;
        }
    }

    private void TickLaggingRake(PatternLaneState lane)
    {
        if (lane.PatternStepIndex >= rake.shotCount)
        {
            FinishPattern(lane);
            return;
        }

        if (Time.time < lane.NextPatternStepTime)
        {
            return;
        }

        EnemyProjectileWeaponBase3D weapon = ResolveWeapon(weapons.laggingRakeWeapons, lane.PatternStepIndex);
        Vector3 aimPoint = ResolveRakeAimPoint(lane.Target, weapon);
        FireProjectileConverged(weapon, aimPoint, lane.Target);

        lane.PatternStepIndex++;
        lane.NextPatternStepTime = Time.time + rake.shotInterval;
    }

    private void TickBeamFence(PatternLaneState lane)
    {
        if (lane.ActiveBeamCount <= 0)
        {
            FinishPattern(lane);
            return;
        }

        float activeStartTime = lane.PatternEndsAt - beamFence.activeDuration;
        if (Time.time >= activeStartTime && Time.time < lane.PatternEndsAt)
        {
            if (lane.PatternStepIndex == 0)
            {
                SetBeamFenceState(lane, isFiring: true);
                lane.PatternStepIndex = 1;
                lane.NextBeamAimRefreshTime = Time.time + beamFence.aimRefreshInterval;
            }

            if (Time.time >= lane.NextBeamAimRefreshTime)
            {
                RefreshBeamFenceAim(lane);
                lane.NextBeamAimRefreshTime = Time.time + beamFence.aimRefreshInterval;
            }
            return;
        }

        if (Time.time >= lane.PatternEndsAt)
        {
            StopActiveBeams(lane);
            FinishPattern(lane);
        }
    }

    private void TickFormationMissileSalvo(PatternLaneState lane)
    {
        if (lane.PatternStepIndex > 0)
        {
            FinishPattern(lane);
            return;
        }

        EnemyProjectileWeaponBase3D weapon = ResolveWeapon(weapons.formationMissileSalvoWeapons, lane.LaneIndex);
        FireProjectileDirection(weapon, ResolveDirectionToTarget(lane.Target), lane.Target);

        lane.PatternStepIndex++;
        FinishPattern(lane);
    }

    private void TickHelixSpiralBarrage(PatternLaneState lane)
    {
        HelixSpiralProjectileWeaponEnemy3D weapon = weapons.helixSpiralWeapon;
        if (weapon == null)
        {
            FinishPattern(lane);
            return;
        }

        if (lane.PatternStepIndex >= weapon.ShotCount)
        {
            FinishPattern(lane);
            return;
        }

        if (Time.time < lane.NextPatternStepTime)
        {
            return;
        }

        weapon.PrepareHelixShot(lane.Target, lane.PatternStepIndex, lane.HelixActivationIndex);
        FireProjectileDirection(weapon, ResolveDirectionToTarget(lane.Target), lane.Target);

        lane.PatternStepIndex++;
        lane.NextPatternStepTime = Time.time + weapon.ShotInterval;
    }

    private void TickLightningSlowBeam(PatternLaneState lane)
    {
        if (lane.ActiveLightningBeamCount <= 0)
        {
            FinishPattern(lane);
            return;
        }

        float activeStartTime = lane.PatternEndsAt - lightningSlowBeam.activeDuration;
        if (Time.time >= activeStartTime && Time.time < lane.PatternEndsAt)
        {
            if (lane.PatternStepIndex == 0)
            {
                SetLightningSlowBeamState(lane, isFiring: true);
                lane.PatternStepIndex = 1;
                lane.NextBeamAimRefreshTime = Time.time + lightningSlowBeam.aimRefreshInterval;
                lane.NextLightningSlowTickTime = Time.time;
            }

            if (Time.time >= lane.NextBeamAimRefreshTime)
            {
                RefreshLightningSlowBeamAim(lane);
                lane.NextBeamAimRefreshTime = Time.time + lightningSlowBeam.aimRefreshInterval;
            }

            if (Time.time >= lane.NextLightningSlowTickTime)
            {
                ApplyLightningSlowBeamSlow(lane);
                lane.NextLightningSlowTickTime = Time.time + lightningSlowBeam.slowTickInterval;
            }
            return;
        }

        if (Time.time >= lane.PatternEndsAt)
        {
            StopLightningSlowBeams(lane);
            FinishPattern(lane);
        }
    }

    private void TickEnemySpawnWave(PatternLaneState lane)
    {
        if (lane.PatternStepIndex == 0)
        {
            int startedCount = BeginEnemySpawnWave();
            if (startedCount <= 0)
            {
                FinishPattern(lane);
                return;
            }

            lane.PatternStepIndex = 1;
            return;
        }

        if (!HasActiveEnemySpawnWave())
        {
            FinishPattern(lane);
        }
    }

    private void FinishPattern(PatternLaneState lane)
    {
        StopPatternEffects(lane);
        lane.LastPattern = lane.ActivePattern;
        ClearPatternLaneRuntime(lane);
        lane.NextPatternAllowedTime = Time.time + ResolvePatternCooldown();
    }

    private void CancelPatternLane(PatternLaneState lane)
    {
        if (lane == null || lane.ActivePattern == BossPattern.None)
        {
            return;
        }

        StopPatternEffects(lane);
        ClearPatternLaneRuntime(lane);
        lane.NextPatternAllowedTime = Time.time + ResolvePatternCooldown();
    }

    private bool CanRunPattern(BossPattern pattern)
    {
        switch (pattern)
        {
            case BossPattern.LaggingMachineGunRake:
                return HasAnyWeapon(weapons.laggingRakeWeapons);
            case BossPattern.BeamFence:
                return weapons.beamFenceWeapons != null && weapons.beamFenceWeapons.Length > 0;
            case BossPattern.FormationMissileSalvo:
                return HasAnyWeapon(weapons.formationMissileSalvoWeapons);
            case BossPattern.HelixSpiralBarrage:
                return weapons.helixSpiralWeapon != null;
            case BossPattern.LightningSlowBeam:
                return weapons.lightningSlowBeamWeapons != null && weapons.lightningSlowBeamWeapons.Length > 0;
            case BossPattern.EnemySpawnWave:
                return HasAnySpawnerWeapon(weapons.enemySpawnWaveWeapons);
            default:
                return false;
        }
    }

    private BossPattern ResolveOtherLaneActivePattern(PatternLaneState lane)
    {
        for (int i = 0; i < _patternLanes.Length; i++)
        {
            PatternLaneState otherLane = _patternLanes[i];
            if (otherLane != null && otherLane != lane && otherLane.ActivePattern != BossPattern.None)
            {
                return otherLane.ActivePattern;
            }
        }

        return BossPattern.None;
    }

    private void StopPatternEffects(PatternLaneState lane)
    {
        if (lane == null)
        {
            return;
        }

        switch (lane.ActivePattern)
        {
            case BossPattern.BeamFence:
                StopActiveBeams(lane);
                ResetBeamConvergenceSmoothing();
                break;
            case BossPattern.LightningSlowBeam:
                StopLightningSlowBeams(lane);
                ResetLightningSlowBeamSmoothing();
                break;
            case BossPattern.EnemySpawnWave:
                StopEnemySpawnWave();
                break;
        }
    }

    private void ClearPatternLaneRuntime(PatternLaneState lane)
    {
        if (lane == null)
        {
            return;
        }

        lane.ActivePattern = BossPattern.None;
        lane.PatternStepIndex = 0;
        lane.NextPatternStepTime = 0f;
        lane.PatternEndsAt = 0f;
        lane.NextBeamAimRefreshTime = 0f;
        lane.NextLightningSlowTickTime = 0f;
        lane.ActiveBeamCount = 0;
        lane.ActiveLightningBeamCount = 0;
    }

    private void CancelAllPatternLanes()
    {
        for (int i = 0; i < _patternLanes.Length; i++)
        {
            CancelPatternLane(_patternLanes[i]);
        }
    }

    private void ClearAllPatternLaneState()
    {
        for (int i = 0; i < _patternLanes.Length; i++)
        {
            PatternLaneState lane = _patternLanes[i];
            lane.Target = null;
            ClearPatternLaneRuntime(lane);
        }
    }

    private static BossPattern ToBossPattern(SelectableBossPattern pattern)
    {
        switch (pattern)
        {
            case SelectableBossPattern.LaggingMachineGunRake:
                return BossPattern.LaggingMachineGunRake;
            case SelectableBossPattern.BeamFence:
                return BossPattern.BeamFence;
            case SelectableBossPattern.FormationMissileSalvo:
                return BossPattern.FormationMissileSalvo;
            case SelectableBossPattern.HelixSpiralBarrage:
                return BossPattern.HelixSpiralBarrage;
            case SelectableBossPattern.LightningSlowBeam:
                return BossPattern.LightningSlowBeam;
            case SelectableBossPattern.EnemySpawnWave:
                return BossPattern.EnemySpawnWave;
            default:
                return BossPattern.None;
        }
    }

    private static SelectableBossPattern ToSelectableBossPattern(BossPattern pattern)
    {
        switch (pattern)
        {
            case BossPattern.LaggingMachineGunRake:
                return SelectableBossPattern.LaggingMachineGunRake;
            case BossPattern.BeamFence:
                return SelectableBossPattern.BeamFence;
            case BossPattern.FormationMissileSalvo:
                return SelectableBossPattern.FormationMissileSalvo;
            case BossPattern.HelixSpiralBarrage:
                return SelectableBossPattern.HelixSpiralBarrage;
            case BossPattern.LightningSlowBeam:
                return SelectableBossPattern.LightningSlowBeam;
            case BossPattern.EnemySpawnWave:
                return SelectableBossPattern.EnemySpawnWave;
            default:
                return SelectableBossPattern.None;
        }
    }

    private int BeginEnemySpawnWave()
    {
        if (weapons.enemySpawnWaveWeapons == null)
        {
            return 0;
        }

        int startedCount = 0;
        for (int i = 0; i < weapons.enemySpawnWaveWeapons.Length; i++)
        {
            EnemySpawnerWeapon3D spawnerWeapon = weapons.enemySpawnWaveWeapons[i];
            if (spawnerWeapon == null)
            {
                continue;
            }

            if (spawnerWeapon.IsSpawning || spawnerWeapon.BeginSpawning())
            {
                startedCount++;
            }
        }

        return startedCount;
    }

    private bool HasActiveEnemySpawnWave()
    {
        if (weapons.enemySpawnWaveWeapons == null)
        {
            return false;
        }

        for (int i = 0; i < weapons.enemySpawnWaveWeapons.Length; i++)
        {
            EnemySpawnerWeapon3D spawnerWeapon = weapons.enemySpawnWaveWeapons[i];
            if (spawnerWeapon != null && spawnerWeapon.IsSpawning)
            {
                return true;
            }
        }

        return false;
    }

    private void StopEnemySpawnWave()
    {
        if (weapons.enemySpawnWaveWeapons == null)
        {
            return;
        }

        for (int i = 0; i < weapons.enemySpawnWaveWeapons.Length; i++)
        {
            weapons.enemySpawnWaveWeapons[i]?.StopSpawning();
        }
    }

    private bool FireProjectileDirection(EnemyProjectileWeaponBase3D weapon, Vector3 fireDirection, Entity3D target)
    {
        if (weapon == null || fireDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(weapon, Faction3D.PlayerTeam, fireDirection.normalized, target);
        }

        bool fired = weapon.TryFireAtFaction(Faction3D.PlayerTeam, fireDirection.normalized);
        if (fired)
        {
            attackReporter?.ReportAttack(target);
        }

        return fired;
    }

    private bool FireProjectileConverged(EnemyProjectileWeaponBase3D weapon, Vector3 convergencePoint, Entity3D target)
    {
        if (weapon == null)
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePatternConverged(weapon, Faction3D.PlayerTeam, convergencePoint, target);
        }

        bool fired = weapon.TryFireAtFactionConverged(Faction3D.PlayerTeam, convergencePoint);
        if (fired)
        {
            attackReporter?.ReportAttack(target);
        }

        return fired;
    }

    private void SetBeamFenceState(PatternLaneState lane, bool isFiring)
    {
        Vector3 convergencePoint = ResolveBeamConvergencePoint(lane.Target);
        for (int i = 0; i < lane.ActiveBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = weapons.beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, beamFence.allowBehindHardpointAim);
            Vector3 beamDirection = ResolveBeamConvergenceDirection(beamWeapon, convergencePoint, i);
            if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
            {
                netEnemyCombat.SetBeamState(beamWeapon, isFiring, beamDirection, lane.Target);
            }
            else
            {
                if (isFiring)
                {
                    beamWeapon.ApplyNetworkBeamAim(beamDirection);
                }

                beamWeapon.ApplyNetworkBeamState(isFiring, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
                ReportSustainedAttack(isFiring, lane.Target);
            }
        }
    }

    private void RefreshBeamFenceAim(PatternLaneState lane)
    {
        Vector3 convergencePoint = ResolveBeamConvergencePoint(lane.Target);
        for (int i = 0; i < lane.ActiveBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = weapons.beamFenceWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, beamFence.allowBehindHardpointAim);
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

    private void StopActiveBeams(PatternLaneState lane)
    {
        if (weapons.beamFenceWeapons == null)
        {
            return;
        }

        int count = lane != null && lane.ActiveBeamCount > 0 ? Mathf.Min(lane.ActiveBeamCount, weapons.beamFenceWeapons.Length) : weapons.beamFenceWeapons.Length;
        for (int i = 0; i < count; i++)
        {
            BeamWeapon3D beamWeapon = weapons.beamFenceWeapons[i];
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

    private bool ShouldReplicateBossVisual()
    {
        return NetTickUtil.IsActive
            && NetworkManager.Singleton != null
            && IsServer
            && IsSpawned;
    }

    private void ReportSustainedAttack(bool isFiring, Entity3D target)
    {
        if (isFiring)
        {
            attackReporter?.ReportSustainedAttack(target, 0.25f);
        }
        else
        {
            attackReporter?.StopSustainedAttack(target);
        }
    }

    private double ResolveNetworkServerTime()
    {
        return NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d;
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
        _nextHistorySampleTime = Time.time + sequencer.targetHistorySampleInterval;
    }

    private Vector3 ResolveHistoricalTargetPoint()
    {
        return ResolveHistoricalTargetPoint(rake.historySeconds);
    }

    private void SetLightningSlowBeamState(PatternLaneState lane, bool isFiring)
    {
        for (int i = 0; i < lane.ActiveLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = weapons.lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, lightningSlowBeam.allowBehindHardpointAim);
            Vector3 beamDirection = ResolveLightningSlowBeamDirection(beamWeapon, lane.Target, i);
            if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
            {
                netEnemyCombat.SetBeamState(beamWeapon, isFiring, beamDirection, lane.Target);
            }
            else
            {
                if (isFiring)
                {
                    beamWeapon.ApplyNetworkBeamAim(beamDirection);
                }

                beamWeapon.ApplyNetworkBeamState(isFiring, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
                ReportSustainedAttack(isFiring, lane.Target);
            }
        }
    }

    private void RefreshLightningSlowBeamAim(PatternLaneState lane)
    {
        for (int i = 0; i < lane.ActiveLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = weapons.lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            ConfigureBossBeamAimConstraint(beamWeapon, lightningSlowBeam.allowBehindHardpointAim);
            Vector3 beamDirection = ResolveLightningSlowBeamDirection(beamWeapon, lane.Target, i);
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

    private void StopLightningSlowBeams(PatternLaneState lane)
    {
        if (weapons.lightningSlowBeamWeapons == null)
        {
            return;
        }

        int count = lane != null && lane.ActiveLightningBeamCount > 0 ? Mathf.Min(lane.ActiveLightningBeamCount, weapons.lightningSlowBeamWeapons.Length) : weapons.lightningSlowBeamWeapons.Length;
        for (int i = 0; i < count; i++)
        {
            BeamWeapon3D beamWeapon = weapons.lightningSlowBeamWeapons[i];
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

    private void ApplyLightningSlowBeamSlow(PatternLaneState lane)
    {
        Entity3D target = lane.Target;
        if (!IsTargetValid(target) || lightningSlowBeam.slowDuration <= 0f || lightningSlowBeam.slowMultiplier >= 1f)
        {
            return;
        }

        for (int i = 0; i < lane.ActiveLightningBeamCount; i++)
        {
            BeamWeapon3D beamWeapon = weapons.lightningSlowBeamWeapons[i];
            if (beamWeapon == null)
            {
                continue;
            }

            Vector3 direction = ResolveLightningSlowBeamDirection(beamWeapon, target, i);
            Vector3 origin = beamWeapon.GetBeamOrigin(direction);
            if (DoesLightningBeamReachTarget(origin, direction, target))
            {
                target.ApplySlow(lightningSlowBeam.slowMultiplier, lightningSlowBeam.slowDuration);
                target.ThrusterVfx?.ApplyTemporaryEmissionRateScale(lightningSlowBeam.slowMultiplier, lightningSlowBeam.slowDuration);
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

        float maxDistance = Mathf.Max(0f, movement.engagementRange);
        int hitCount = Physics.SphereCastNonAlloc(
                origin,
                Mathf.Max(0f, lightningSlowBeam.slowRadius),
                direction.normalized,
                _lightningSlowHits,
                maxDistance,
                lightningSlowBeam.collisionMask,
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

    private void StartOrbitalEnergyPillars(Entity3D target)
    {
        Vector3 origin = transform.position;
        Vector3 faceForward = ResolvePlanarDirection(transform.forward, Vector3.forward);
        Vector3 gapDirection = ResolveOrbitalPillarGapDirection(target);
        PlayOrbitalEnergyPillarsLocal(origin, faceForward, gapDirection, elapsed: 0f);

        if (ShouldReplicateBossVisual())
        {
            StartOrbitalEnergyPillarClientRpc(
                origin,
                faceForward,
                gapDirection,
                _activeOrbitalPillarCount,
                ResolveOrbitalPillarRingRadius(),
                orbitalPillars.gapDegrees,
                orbitalPillars.damageRadius,
                orbitalPillars.sphereTravelDuration,
                orbitalPillars.expandDuration,
                orbitalPillars.orbitDegreesPerSecond,
                ResolveNetworkServerTime());
        }
    }

    private void StartPersistentOrbitalEnergyPillars(Entity3D target)
    {
        if (_isPhaseTwoOrbitalPillarsActive)
        {
            return;
        }

        if (!CanStartOrbitalEnergyPillars())
        {
            if (logPhaseTransitionDebug)
            {
                Debug.LogWarning(
                    $"[{nameof(SiegeCarrierBossEnemyBrain3D)}] Phase-two orbital pillars were blocked on {name}: count={orbitalPillars.count}.",
                    this);
            }

            return;
        }

        _activeOrbitalPillarCount = Mathf.Clamp(orbitalPillars.count, 1, 16);
        _isPhaseTwoOrbitalPillarsActive = true;
        _orbitalPillarOrbitStartTime = Time.time;
        _nextOrbitalPillarDamageTickTime = Time.time + orbitalPillars.sphereTravelDuration + orbitalPillars.expandDuration;
        EnsureOrbitalPillarBuffers();
        StartOrbitalEnergyPillars(target);
        UpdateOrbitalPillarCenters();

        if (logPhaseTransitionDebug)
        {
            Debug.Log(
                $"[{nameof(SiegeCarrierBossEnemyBrain3D)}] Phase-two orbital pillars started on {name}. durability={ResolvePhaseTransitionDurabilityPercentForDebug():P1}, threshold={sequencer.phaseTwoHealthPercent:P1}, count={_activeOrbitalPillarCount}, target={(target != null ? target.name : "none")}.",
                this);
        }
    }

    private bool CanStartOrbitalEnergyPillars()
    {
        return orbitalPillars.count > 0;
    }

    private void TickPersistentOrbitalEnergyPillars()
    {
        if (!_isPhaseTwoOrbitalPillarsActive)
        {
            return;
        }

        UpdateOrbitalPillarCenters();
        if (Time.time >= _nextOrbitalPillarDamageTickTime)
        {
            ApplyOrbitalPillarDamage();
            _nextOrbitalPillarDamageTickTime = Time.time + orbitalPillars.damageTickInterval;
        }
    }

    private void PlayOrbitalEnergyPillarsLocal(Vector3 origin, Vector3 faceForward, Vector3 gapDirection, float elapsed)
    {
        if (orbitalPillarVisuals.launchSpearPrefab == null || orbitalPillarVisuals.bluePillarPrefab == null)
        {
            if (!_warnedMissingOrbitalPillarVisuals)
            {
                Debug.LogWarning(
                    $"[{nameof(SiegeCarrierBossEnemyBrain3D)}] Phase-two orbital pillars started on {name}, but pillar visuals are missing. Assign Launch Spear Prefab and Blue Pillar Prefab on the boss brain to show the effect.",
                    this);
                _warnedMissingOrbitalPillarVisuals = true;
            }

            return;
        }

        _orbitalPillarVisualOrigin = origin;
        _orbitalPillarVisualFaceForward = ResolvePlanarDirection(faceForward, Vector3.forward);
        _orbitalPillarVisualGapDirection = ResolvePlanarDirection(gapDirection, _orbitalPillarVisualFaceForward);
        _activeOrbitalPillarCount = Mathf.Clamp(_activeOrbitalPillarCount, 1, 16);
        _orbitalPillarVisualStartTime = Time.time - Mathf.Max(0f, elapsed);
        _orbitalPillarOrbitStartTime = _orbitalPillarVisualStartTime;
        _isOrbitalPillarVisualPlaying = true;

        EnsureOrbitalPillarVisualPools(_activeOrbitalPillarCount);
        EnsureOrbitalPillarBuffers();
        UpdateOrbitalPillarCenters();
        for (int i = 0; i < _activeOrbitalPillarCount; i++)
        {
            SetOrbitalPillarVisualActive(i, true);
        }

        for (int i = _activeOrbitalPillarCount; _orbitalPillarLaunchInstances != null && i < _orbitalPillarLaunchInstances.Length; i++)
        {
            SetOrbitalPillarVisualActive(i, false);
        }

        TickOrbitalEnergyPillarVisuals();
    }

    private void StopOrbitalEnergyPillars(bool immediate)
    {
        if (immediate)
        {
            StopOrbitalEnergyPillarsVisualImmediate();
        }

        if (ShouldReplicateBossVisual())
        {
            StopOrbitalEnergyPillarClientRpc();
        }
    }

    [ClientRpc]
    private void StartOrbitalEnergyPillarClientRpc(
        Vector3 origin,
        Vector3 faceForward,
        Vector3 gapDirection,
        int pillarCount,
        float ringRadius,
        float gapDegrees,
        float pillarRadius,
        float travelDuration,
        float expandDuration,
        float orbitDegreesPerSecond,
        double serverStartTime)
    {
        if (IsServer)
        {
            return;
        }

        _activeOrbitalPillarCount = Mathf.Clamp(pillarCount, 1, 16);
        orbitalPillars.ringRadius = Mathf.Max(ResolveMinimumOrbitalPillarRingRadius(), ringRadius);
        orbitalPillars.gapDegrees = Mathf.Clamp(gapDegrees, 0f, 330f);
        orbitalPillars.damageRadius = Mathf.Max(0.01f, pillarRadius);
        orbitalPillars.sphereTravelDuration = Mathf.Max(0.01f, travelDuration);
        orbitalPillars.expandDuration = Mathf.Max(0.01f, expandDuration);
        orbitalPillars.orbitDegreesPerSecond = orbitDegreesPerSecond;

        float elapsed = 0f;
        if (NetworkManager.Singleton != null && serverStartTime > 0d)
        {
            elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - serverStartTime));
        }

        PlayOrbitalEnergyPillarsLocal(origin, faceForward, gapDirection, elapsed);
    }

    [ClientRpc]
    private void StopOrbitalEnergyPillarClientRpc()
    {
        if (IsServer)
        {
            return;
        }

        StopOrbitalEnergyPillarsVisualImmediate();
    }

    private void TickOrbitalEnergyPillarVisuals()
    {
        if (!_isOrbitalPillarVisualPlaying)
        {
            return;
        }

        float elapsed = Time.time - _orbitalPillarVisualStartTime;
        float travelT = Mathf.Clamp01(elapsed / orbitalPillars.sphereTravelDuration);
        float expandT = Mathf.Clamp01((elapsed - orbitalPillars.sphereTravelDuration) / orbitalPillars.expandDuration);
        bool isExpanding = elapsed >= orbitalPillars.sphereTravelDuration
            && elapsed < orbitalPillars.sphereTravelDuration + orbitalPillars.expandDuration;
        bool showPillar = elapsed >= orbitalPillars.sphereTravelDuration;
        bool showLaunchSpear = !showPillar || isExpanding;
        float launchSpearScale = orbitalPillarVisuals.launchScale * (isExpanding ? 1f - EaseOut(expandT) : 1f);
        Vector3 launchPosition = _orbitalPillarVisualOrigin + _orbitalPillarVisualFaceForward * orbitalPillarVisuals.launchOffset;
        float finalHeight = Mathf.Max(orbitalPillarVisuals.initialGrowthHeight, orbitalPillars.damageHalfHeight * 2f);
        float currentHeight = Mathf.Lerp(orbitalPillarVisuals.initialGrowthHeight, finalHeight, EaseOut(expandT));
        float currentRadius = Mathf.Max(0.01f, orbitalPillars.damageRadius * EaseOut(expandT));

        UpdateOrbitalPillarCenters();
        for (int i = 0; i < _activeOrbitalPillarCount; i++)
        {
            Vector3 center = ResolveOrbitalPillarVisualCenter(i);
            Vector3 launchSpearPosition = Vector3.Lerp(launchPosition, center, EaseInOut(travelT));

            if (_orbitalPillarLaunchTransforms != null && i < _orbitalPillarLaunchTransforms.Length && _orbitalPillarLaunchTransforms[i] != null)
            {
                _orbitalPillarLaunchTransforms[i].gameObject.SetActive(showLaunchSpear);
                _orbitalPillarLaunchTransforms[i].position = launchSpearPosition;
                _orbitalPillarLaunchTransforms[i].rotation = Quaternion.identity;
                _orbitalPillarLaunchTransforms[i].localScale = Vector3.one * launchSpearScale;
            }

            if (_orbitalPillarVisualInstances != null && i < _orbitalPillarVisualInstances.Length && _orbitalPillarVisualInstances[i] != null)
            {
                _orbitalPillarVisualInstances[i].SetActive(showPillar);
            }

            if (_orbitalPillarVisualTransforms != null && i < _orbitalPillarVisualTransforms.Length && _orbitalPillarVisualTransforms[i] != null)
            {
                _orbitalPillarVisualTransforms[i].SetPositionAndRotation(center, Quaternion.identity);
                _orbitalPillarVisualTransforms[i].localScale = new Vector3(currentRadius, currentHeight, currentRadius);
            }
        }
    }

    private void EnsureOrbitalPillarVisualPools(int count)
    {
        count = Mathf.Clamp(count, 1, 16);
        if (_orbitalPillarLaunchInstances != null && _orbitalPillarLaunchInstances.Length >= count)
        {
            return;
        }

        int oldCount = _orbitalPillarLaunchInstances != null ? _orbitalPillarLaunchInstances.Length : 0;
        int newCount = Mathf.Max(count, oldCount);
        System.Array.Resize(ref _orbitalPillarLaunchInstances, newCount);
        System.Array.Resize(ref _orbitalPillarLaunchTransforms, newCount);
        System.Array.Resize(ref _orbitalPillarVisualInstances, newCount);
        System.Array.Resize(ref _orbitalPillarVisualTransforms, newCount);

        for (int i = oldCount; i < newCount; i++)
        {
            CreateOrbitalPillarLaunchVisual(i);
            CreateOrbitalPillarBodyVisual(i);
            SetOrbitalPillarVisualActive(i, false);
        }
    }

    private void CreateOrbitalPillarLaunchVisual(int index)
    {
        GameObject instance = Instantiate(orbitalPillarVisuals.launchSpearPrefab, transform);
        instance.name = $"Orbital Pillar Launch Spear {index + 1}";
        RemoveGameplayColliders(instance);
        _orbitalPillarLaunchInstances[index] = instance;
        _orbitalPillarLaunchTransforms[index] = instance.transform;
    }

    private void CreateOrbitalPillarBodyVisual(int index)
    {
        GameObject instance = Instantiate(orbitalPillarVisuals.bluePillarPrefab, transform);
        instance.name = $"Blue Orbital Energy Pillar {index + 1}";
        RemoveGameplayColliders(instance);
        _orbitalPillarVisualInstances[index] = instance;
        _orbitalPillarVisualTransforms[index] = instance.transform;
    }

    private void SetOrbitalPillarVisualActive(int index, bool active)
    {
        if (_orbitalPillarLaunchInstances != null && index < _orbitalPillarLaunchInstances.Length && _orbitalPillarLaunchInstances[index] != null)
        {
            _orbitalPillarLaunchInstances[index].SetActive(active);
        }

        if (_orbitalPillarVisualInstances != null && index < _orbitalPillarVisualInstances.Length && _orbitalPillarVisualInstances[index] != null)
        {
            _orbitalPillarVisualInstances[index].SetActive(false);
        }
    }

    private void StopOrbitalEnergyPillarsVisualImmediate()
    {
        _isOrbitalPillarVisualPlaying = false;
        if (_orbitalPillarLaunchInstances == null)
        {
            return;
        }

        for (int i = 0; i < _orbitalPillarLaunchInstances.Length; i++)
        {
            SetOrbitalPillarVisualActive(i, false);
        }
    }

    private Vector3 ResolveOrbitalPillarVisualCenter(int index)
    {
        if (_orbitalPillarCenters != null && index >= 0 && index < _orbitalPillarCenters.Length)
        {
            return _orbitalPillarCenters[index];
        }

        float coveredArc = Mathf.Max(0f, 360f - orbitalPillars.gapDegrees);
        float angle;
        if (_activeOrbitalPillarCount <= 1)
        {
            angle = 180f;
        }
        else if (orbitalPillars.gapDegrees <= 0.01f)
        {
            angle = index * (360f / _activeOrbitalPillarCount);
        }
        else
        {
            float t = index / (float)(_activeOrbitalPillarCount - 1);
            angle = (orbitalPillars.gapDegrees * 0.5f) + t * coveredArc;
        }

        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * _orbitalPillarVisualGapDirection;
        return _orbitalPillarVisualOrigin + direction.normalized * ResolveOrbitalPillarRingRadius();
    }

    private static void RemoveGameplayColliders(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Destroy(colliders[i]);
            }
        }
    }

    private void ApplyOrbitalPillarDamage()
    {
        if (_orbitalPillarCenters == null || _activeOrbitalPillarCount <= 0 || orbitalPillars.damagePerSecond <= 0f)
        {
            return;
        }

        float damageThisTick = orbitalPillars.damagePerSecond * orbitalPillars.damageTickInterval;
        float radiusSqr = orbitalPillars.damageRadius * orbitalPillars.damageRadius;
        int damagedCount = 0;

        for (int pillarIndex = 0; pillarIndex < _activeOrbitalPillarCount; pillarIndex++)
        {
            Vector3 center = _orbitalPillarCenters[pillarIndex];
            Vector3 bottom = center - Vector3.up * orbitalPillars.damageHalfHeight;
            Vector3 top = center + Vector3.up * orbitalPillars.damageHalfHeight;
            int hitCount = Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                orbitalPillars.damageRadius,
                _orbitalPillarHits,
                orbitalPillars.damageMask,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _orbitalPillarHits[hitIndex];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Entity3D target = ResolveHitEntity(hit);
                if (target == null
                    || target == _enemy
                    || target.CurrentHealth <= 0f
                    || FactionMember3D.ResolveFaction(target) != Faction3D.PlayerTeam)
                {
                    continue;
                }

                if (WasAlreadyDamagedByPillar(target, damagedCount))
                {
                    continue;
                }

                Vector3 targetPoint = hit.bounds.center;
                Vector2 planarOffset = new Vector2(targetPoint.x - center.x, targetPoint.z - center.z);
                if (planarOffset.sqrMagnitude > radiusSqr || Mathf.Abs(targetPoint.y - center.y) > orbitalPillars.damageHalfHeight)
                {
                    continue;
                }

                target.TakeDamage(damageThisTick, targetPoint, _enemy, DamageSource3D.Beam, PlayerCombatStats3D.InvalidAttackId);
                if (damagedCount < _orbitalPillarDamagedThisTick.Length)
                {
                    _orbitalPillarDamagedThisTick[damagedCount++] = target;
                }
            }
        }

        for (int i = 0; i < damagedCount; i++)
        {
            _orbitalPillarDamagedThisTick[i] = null;
        }
    }

    private bool WasAlreadyDamagedByPillar(Entity3D target, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (_orbitalPillarDamagedThisTick[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ResolveHistoricalTargetPoint(float secondsBehind)
    {
        if (_historyCount <= 0 || _targetHistory == null || _targetHistory.Length == 0)
        {
            return IsTargetValid(_currentTarget) ? _currentTarget.transform.position : transform.position + transform.forward * 20f;
        }

        int samplesBehind = Mathf.Clamp(Mathf.RoundToInt(secondsBehind / Mathf.Max(0.02f, sequencer.targetHistorySampleInterval)), 0, _historyCount - 1);
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
            return ResolveHistoricalTargetPoint(beamFence.convergenceLagSeconds);
        }

        Vector3 currentPoint = target.transform.position;
        Vector3 laggedPoint = _historyCount > 0
            ? ResolveHistoricalTargetPoint(beamFence.convergenceLagSeconds)
            : currentPoint - ResolveTargetVelocity(target) * beamFence.convergenceLagSeconds;

        return Vector3.Lerp(currentPoint, laggedPoint, beamFence.convergenceLagBlend);
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
        if (rake.useLeadAim)
        {
            Vector3 targetVelocity = ResolveTargetVelocity(target);
            float projectileSpeed = rake.leadProjectileSpeed;
            if (projectileSpeed <= 0f && weapon != null)
            {
                projectileSpeed = weapon.WeaponConfig.speed;
            }

            if (projectileSpeed > 0.0001f && targetVelocity.sqrMagnitude > 0.0001f)
            {
                float travelTime = Vector3.Distance(transform.position, target.transform.position) / projectileSpeed;
                float leadSeconds = Mathf.Clamp((travelTime * rake.leadTimeScale) + rake.additionalLeadSeconds, 0f, rake.maxLeadSeconds);
                currentAimPoint += targetVelocity * leadSeconds;
            }
        }

        if (rake.historyBlend <= 0f)
        {
            return currentAimPoint;
        }

        return Vector3.Lerp(currentAimPoint, ResolveHistoricalTargetPoint(), rake.historyBlend);
    }

    private void EnsureTargetHistoryBuffer()
    {
        int size = Mathf.Clamp(sequencer.targetHistorySamples, 2, 32);
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
        int size = Mathf.Max(0, weapons.beamFenceWeapons != null ? weapons.beamFenceWeapons.Length : 0);
        if (_beamConvergenceSmoothedDirections != null
            && _beamConvergenceLastSmoothTimes != null
            && _beamConvergenceHasSmoothedDirections != null
            && _beamConvergenceSmoothedDirections.Length == size
            && _beamConvergenceLastSmoothTimes.Length == size
            && _beamConvergenceHasSmoothedDirections.Length == size)
        {
            return;
        }

        _beamConvergenceSmoothedDirections = new Vector3[size];
        _beamConvergenceLastSmoothTimes = new float[size];
        _beamConvergenceHasSmoothedDirections = new bool[size];
    }

    private void EnsureLightningSlowBeamBuffers()
    {
        int size = Mathf.Max(0, weapons.lightningSlowBeamWeapons != null ? weapons.lightningSlowBeamWeapons.Length : 0);
        if (_lightningSmoothedDirections != null
            && _lightningLastSmoothTimes != null
            && _lightningHasSmoothedDirections != null
            && _lightningSmoothedDirections.Length == size
            && _lightningLastSmoothTimes.Length == size
            && _lightningHasSmoothedDirections.Length == size)
        {
            return;
        }

        _lightningSmoothedDirections = new Vector3[size];
        _lightningLastSmoothTimes = new float[size];
        _lightningHasSmoothedDirections = new bool[size];
    }

    private void EnsureOrbitalPillarBuffers()
    {
        int size = Mathf.Clamp(orbitalPillars.count, 1, 16);
        if (_orbitalPillarCenters != null && _orbitalPillarCenters.Length == size)
        {
            return;
        }

        _orbitalPillarCenters = new Vector3[size];
    }

    private void UpdateOrbitalPillarCenters()
    {
        _activeOrbitalPillarCount = Mathf.Clamp(_activeOrbitalPillarCount, 1, 16);
        if (_orbitalPillarCenters == null || _orbitalPillarCenters.Length < _activeOrbitalPillarCount)
        {
            _orbitalPillarCenters = new Vector3[_activeOrbitalPillarCount];
        }

        float coveredArc = Mathf.Max(0f, 360f - orbitalPillars.gapDegrees);
        float orbitAngleOffset = (Time.time - _orbitalPillarOrbitStartTime) * orbitalPillars.orbitDegreesPerSecond;
        for (int i = 0; i < _activeOrbitalPillarCount; i++)
        {
            float angle;
            if (_activeOrbitalPillarCount <= 1)
            {
                angle = 180f;
            }
            else if (orbitalPillars.gapDegrees <= 0.01f)
            {
                angle = i * (360f / _activeOrbitalPillarCount);
            }
            else
            {
                float t = i / (float)(_activeOrbitalPillarCount - 1);
                angle = (orbitalPillars.gapDegrees * 0.5f) + t * coveredArc;
            }

            Vector3 direction = Quaternion.AngleAxis(angle + orbitAngleOffset, Vector3.up) * _orbitalPillarVisualGapDirection;
            _orbitalPillarCenters[i] = transform.position + direction.normalized * ResolveOrbitalPillarRingRadius();
        }
    }

    private float ResolveOrbitalPillarRingRadius()
    {
        return Mathf.Max(ResolveMinimumOrbitalPillarRingRadius(), orbitalPillars.ringRadius);
    }

    private float ResolveMinimumOrbitalPillarRingRadius()
    {
        return Mathf.Max(1f, orbitalPillars.damageRadius * 2.25f);
    }

    private Vector3 ResolveOrbitalPillarGapDirection(Entity3D target)
    {
        if (IsTargetValid(target))
        {
            Vector3 toTarget = target.transform.position - transform.position;
            return ResolvePlanarDirection(toTarget, transform.forward);
        }

        return ResolvePlanarDirection(transform.forward, Vector3.forward);
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

        if (beamFence.convergenceAimSmoothTime <= 0f)
        {
            return rawDirection;
        }

        EnsureBeamConvergenceBuffers();
        if (_beamConvergenceSmoothedDirections == null
            || _beamConvergenceLastSmoothTimes == null
            || _beamConvergenceHasSmoothedDirections == null
            || index < 0
            || index >= _beamConvergenceSmoothedDirections.Length
            || index >= _beamConvergenceLastSmoothTimes.Length)
        {
            return rawDirection;
        }

        if (!_beamConvergenceHasSmoothedDirections[index])
        {
            _beamConvergenceSmoothedDirections[index] = rawDirection;
            _beamConvergenceLastSmoothTimes[index] = Time.time;
            _beamConvergenceHasSmoothedDirections[index] = true;
            return rawDirection;
        }

        float elapsed = Mathf.Max(Time.deltaTime, Time.time - _beamConvergenceLastSmoothTimes[index]);
        float blend = 1f - Mathf.Exp(-elapsed / Mathf.Max(0.001f, beamFence.convergenceAimSmoothTime));
        Vector3 smoothedDirection = Vector3.Slerp(_beamConvergenceSmoothedDirections[index], rawDirection, blend);
        _beamConvergenceSmoothedDirections[index] = smoothedDirection.sqrMagnitude > 0.0001f ? smoothedDirection.normalized : rawDirection;
        _beamConvergenceLastSmoothTimes[index] = Time.time;
        return _beamConvergenceSmoothedDirections[index];
    }

    private Vector3 ResolveLightningSlowBeamDirection(BeamWeapon3D beamWeapon, Entity3D target, int index)
    {
        if (beamWeapon == null || !IsTargetValid(target))
        {
            return ResolveDirectionToTarget(target);
        }

        Vector3 targetPoint = ResolveLightningSlowBeamAimPoint(target);
        Vector3 provisionalDirection = targetPoint - transform.position;
        if (provisionalDirection.sqrMagnitude <= 0.0001f)
        {
            provisionalDirection = ResolveDirectionToTarget(target);
        }

        Vector3 origin = beamWeapon.GetBeamOrigin(provisionalDirection.normalized);
        Vector3 rawDirection = targetPoint - origin;
        rawDirection = rawDirection.sqrMagnitude > 0.0001f ? rawDirection.normalized : provisionalDirection.normalized;

        if (lightningSlowBeam.aimSmoothTime <= 0f)
        {
            return rawDirection;
        }

        EnsureLightningSlowBeamBuffers();
        if (_lightningSmoothedDirections == null
            || _lightningLastSmoothTimes == null
            || _lightningHasSmoothedDirections == null
            || index < 0
            || index >= _lightningSmoothedDirections.Length
            || index >= _lightningLastSmoothTimes.Length)
        {
            return rawDirection;
        }

        if (!_lightningHasSmoothedDirections[index])
        {
            _lightningSmoothedDirections[index] = rawDirection;
            _lightningLastSmoothTimes[index] = Time.time;
            _lightningHasSmoothedDirections[index] = true;
            return rawDirection;
        }

        float elapsed = Mathf.Max(Time.deltaTime, Time.time - _lightningLastSmoothTimes[index]);
        float blend = 1f - Mathf.Exp(-elapsed / Mathf.Max(0.001f, lightningSlowBeam.aimSmoothTime));
        Vector3 smoothedDirection = Vector3.Slerp(_lightningSmoothedDirections[index], rawDirection, blend);
        _lightningSmoothedDirections[index] = smoothedDirection.sqrMagnitude > 0.0001f ? smoothedDirection.normalized : rawDirection;
        _lightningLastSmoothTimes[index] = Time.time;
        return _lightningSmoothedDirections[index];
    }

    private Vector3 ResolveLightningSlowBeamAimPoint(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return ResolveHistoricalTargetPoint(lightningSlowBeam.lagSeconds);
        }

        Vector3 currentPoint = target.transform.position;
        Vector3 targetVelocity = ResolveTargetVelocity(target);
        Vector3 leadPoint = currentPoint + targetVelocity * lightningSlowBeam.leadSeconds;
        if (lightningSlowBeam.lagBlend <= 0f || lightningSlowBeam.lagSeconds <= 0f)
        {
            return leadPoint;
        }

        Vector3 laggedPoint = _historyCount > 0
            ? ResolveHistoricalTargetPoint(lightningSlowBeam.lagSeconds)
            : currentPoint - targetVelocity * lightningSlowBeam.lagSeconds;

        return Vector3.Lerp(leadPoint, laggedPoint, lightningSlowBeam.lagBlend);
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

    private float ResolvePatternCooldown()
    {
        return Mathf.Max(0f, sequencer.minimumPatternCooldown);
    }

    private bool IsInPhaseTwoOrLower()
    {
        if (_enemy == null)
        {
            return false;
        }

        float maxDurability = Mathf.Max(0f, _enemy.MaxHealth + _enemy.MaxShield);
        if (maxDurability <= 0f)
        {
            return false;
        }

        float currentDurability = Mathf.Clamp(_enemy.CurrentHealth, 0f, _enemy.MaxHealth)
            + Mathf.Clamp(_enemy.CurrentShield, 0f, _enemy.MaxShield);
        return (currentDurability / maxDurability) <= sequencer.phaseTwoHealthPercent;
    }

    private void LogPhaseTransitionDebug(bool isInPhaseTwo)
    {
        if (!logPhaseTransitionDebug || Time.time < _nextPhaseDebugLogTime)
        {
            return;
        }

        _nextPhaseDebugLogTime = Time.time + 0.5f;
        string durabilitySummary = _enemy != null
            ? $"{Mathf.Clamp(_enemy.CurrentShield, 0f, _enemy.MaxShield):0.##}+{Mathf.Clamp(_enemy.CurrentHealth, 0f, _enemy.MaxHealth):0.##}/{Mathf.Max(0f, _enemy.MaxShield):0.##}+{Mathf.Max(0f, _enemy.MaxHealth):0.##} ({ResolvePhaseTransitionDurabilityPercentForDebug():P1})"
            : "missing Enemy3D";
        Debug.Log(
            $"[{nameof(SiegeCarrierBossEnemyBrain3D)}] Phase check on {name}: durability={durabilitySummary}, threshold={sequencer.phaseTwoHealthPercent:P1}, inPhaseTwo={isInPhaseTwo}, pillarsActive={_isPhaseTwoOrbitalPillarsActive}, count={orbitalPillars.count}, activePatterns={ResolveActivePatternSummary()}.",
            this);
    }

    private void LogPhaseTransitionDebugNoAuthority()
    {
        if (!logPhaseTransitionDebug || _loggedPhaseDebugNoAuthority)
        {
            return;
        }

        _loggedPhaseDebugNoAuthority = true;
        Debug.LogWarning(
            $"[{nameof(SiegeCarrierBossEnemyBrain3D)}] Phase check is not running on {name} because this brain does not have authority. NetTickUtil.IsActive={NetTickUtil.IsActive}, NetworkManagerPresent={NetworkManager.Singleton != null}, IsServer={(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)}, IsSpawned={(_networkObject != null && _networkObject.IsSpawned)}.",
            this);
    }

    private float ResolvePhaseTransitionDurabilityPercentForDebug()
    {
        if (_enemy == null)
        {
            return 0f;
        }

        float maxDurability = Mathf.Max(0f, _enemy.MaxHealth + _enemy.MaxShield);
        if (maxDurability <= 0f)
        {
            return 0f;
        }

        float currentDurability = Mathf.Clamp(_enemy.CurrentHealth, 0f, _enemy.MaxHealth)
            + Mathf.Clamp(_enemy.CurrentShield, 0f, _enemy.MaxShield);
        return currentDurability / maxDurability;
    }

    private string ResolveActivePatternSummary()
    {
        return $"lane0={_patternLanes[0].ActivePattern}, lane1={_patternLanes[1].ActivePattern}";
    }

    private bool IsInsideEngagementRange(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return false;
        }

        return (target.transform.position - transform.position).sqrMagnitude <= movement.engagementRange * movement.engagementRange;
    }

    private bool IsInsideMaxEngagement(Entity3D target)
    {
        if (!IsTargetValid(target))
        {
            return false;
        }

        float maxRange = Mathf.Max(movement.engagementRange, movement.preferredRangeMax) + movement.approachRangeBuffer;
        return maxRange <= 0f || (target.transform.position - transform.position).sqrMagnitude <= maxRange * maxRange;
    }

    private Vector3 ResolvePlaneBiasedDirectionToTarget(Entity3D target)
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y *= movement.targetVerticalFollowWeight;

        if (movement.planeReturnWeight > 0f)
        {
            float planeDelta = _preferredPlaneY - transform.position.y;
            offset.y += planeDelta * movement.planeReturnWeight;
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

    private static bool HasAnySpawnerWeapon(EnemySpawnerWeapon3D[] spawnerWeapons)
    {
        if (spawnerWeapons == null)
        {
            return false;
        }

        for (int i = 0; i < spawnerWeapons.Length; i++)
        {
            if (spawnerWeapons[i] != null)
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

    private bool IsActivePlayerTarget(Entity3D target)
    {
        return IsTargetValid(target)
            && !target.transform.IsChildOf(transform)
            && FactionMember3D.ResolveFaction(target) == Faction3D.PlayerTeam;
    }

    private static Vector3 ResolvePlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static float EaseInOut(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOut(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - ((1f - value) * (1f - value));
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
