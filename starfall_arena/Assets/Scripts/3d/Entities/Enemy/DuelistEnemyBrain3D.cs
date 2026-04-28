using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyStrafeMover3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class DuelistEnemyBrain3D : MonoBehaviour
{
    private enum DuelistState
    {
        Reposition,
        Engage
    }

    private enum DuelistWeaponChoice
    {
        None,
        Projectile,
        Missile,
        Beam
    }

    [System.Serializable]
    private struct WeaponRangeBand
    {
        [Tooltip("Distance (meters) where this weapon's score peaks. The duelist most prefers to use it at this range.")]
        public float preferredCenter;

        [Tooltip("Soft range (meters) above and below Preferred Center where this weapon still scores. Outside Center +/- Half Width the weapon scores 0 and is not picked.")]
        public float halfWidth;

        [Tooltip("Maximum angle (degrees) between the duelist's forward and the target direction before this weapon is allowed to fire.")]
        public float aimToleranceDegrees;
    }

    [Header("Weapons")]
    [Tooltip("Close-range projectile weapon (typically a fast bolt). Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private ProjectileWeaponEnemy3D projectileWeapon;

    [Tooltip("Mid-range missile weapon (guided). Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private MissileWeaponEnemy3D missileWeapon;

    [Tooltip("Long-range beam weapon. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private BeamWeapon3D beamWeapon;

    [Header("Movement & Sensing")]
    [Tooltip("AI flight motor that drives the Rigidbody (rotation + forward/backward thrust). Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("World-space lateral/vertical strafe overlay. Used both for the orbit strafe and for reactive dodges. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyStrafeMover3D strafeMover;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty. Set its Detection Range to at least Beam Range Center + Beam Half Width on the prefab.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Network combat helper for replicated enemy projectile and beam fire. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Optional inter-agent separation steering. Useful when multiple duelists are active so they do not stack on the same perch.")]
    [SerializeField] private EnemySeparation3D separation;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable Use Obstacle Avoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI decision ticks. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.06f;

    [Header("Perch / Orbit Range")]
    [Tooltip("Minimum preferred distance (meters) the duelist holds from the target while engaging.")]
    [SerializeField] private float preferredRangeMin = 100f;

    [Tooltip("Maximum preferred distance (meters) the duelist holds from the target while engaging.")]
    [SerializeField] private float preferredRangeMax = 200f;

    [Tooltip("Distance (meters) from the chosen perch that counts as arrived. Higher values make the duelist commit to engaging sooner.")]
    [SerializeField] private float perchArrivalDistance = 12f;

    [Tooltip("Maximum seconds spent flying toward one perch before the duelist commits to engaging anyway. Prevents chasing an unreachable point forever.")]
    [SerializeField] private float maxRepositionDuration = 4f;

    [Tooltip("Seconds between perch re-rolls during the Engage state. Each re-roll picks a new perch around the target so the duelist drifts instead of holding one spot.")]
    [SerializeField] private float perchRefreshInterval = 3.5f;

    [Header("Perch Bias")]
    [Tooltip("How strongly the next perch is biased sideways around the target.")]
    [SerializeField] private float lateralPerchBias = 1f;

    [Tooltip("How strongly the next perch is biased above/below the target. Higher values use more vertical airspace.")]
    [SerializeField] private float verticalPerchBias = 0.6f;

    [Tooltip("How much of the duelist's current away-from-target direction is retained when picking the next perch. Higher values produce smoother arcs; lower values produce sharper jukes.")]
    [SerializeField] private float outwardPerchBias = 0.3f;

    [Tooltip("How strongly the duelist avoids perches that fall inside the target's forward arc. 0 disables; 1 strongly suppresses head-on perches in favor of flank/rear positions.")]
    [Range(0f, 1f)]
    [SerializeField] private float forwardArcAvoidanceWeight = 0.7f;

    [Tooltip("Number of candidate perch directions sampled per pick. The duelist scores them all and picks the highest-weight one. 6-10 gives smooth flank seeking without much CPU cost.")]
    [Range(2, 16)]
    [SerializeField] private int perchCandidateCount = 8;

    [Header("Orbit Strafe")]
    [Tooltip("Seconds between orbit-strafe direction re-rolls during the Engage state. Lower values make the duelist juke more frantically; higher values make smooth slides.")]
    [SerializeField] private float strafeRollInterval = 1.2f;

    [Tooltip("World-space strafe speed (m/s) the duelist applies during the Engage orbit. Should be lower than Dodge Speed and below the Strafe Mover's Max Strafe Speed cap.")]
    [SerializeField] private float orbitStrafeSpeed = 18f;

    [Tooltip("How much vertical tilt the orbit strafe direction can pick up (0 = pure horizontal lateral, 1 = full 3D random tilt).")]
    [Range(0f, 1f)]
    [SerializeField] private float orbitVerticalTiltAmount = 0.5f;

    [Header("Weapon Selection")]
    [Tooltip("Range/aim band for the close-range projectile weapon. Aim tolerance is intentionally generous (default 30) because the player can cross huge angles around the duelist at close range faster than the flight controller can rotate; the actual shot direction is still resolved toward the target, so wide aim tolerance does not become wide miss error.")]
    [SerializeField]
    private WeaponRangeBand projectileBand = new WeaponRangeBand
    {
        preferredCenter = 110f,
        halfWidth = 30f,
        aimToleranceDegrees = 30f
    };

    [Tooltip("Range/aim band for the mid-range missile weapon. Missiles can use looser aim tolerance because they steer after launch.")]
    [SerializeField]
    private WeaponRangeBand missileBand = new WeaponRangeBand
    {
        preferredCenter = 150f,
        halfWidth = 35f,
        aimToleranceDegrees = 30f
    };

    [Tooltip("Range/aim band for the long-range beam weapon.")]
    [SerializeField]
    private WeaponRangeBand beamBand = new WeaponRangeBand
    {
        preferredCenter = 190f,
        halfWidth = 30f,
        aimToleranceDegrees = 6f
    };

    [Tooltip("Probability per think tick that the duelist picks a random valid weapon instead of its highest-scoring one. Adds expressive variety so it doesn't always pick the obvious option.")]
    [Range(0f, 1f)]
    [SerializeField] private float vibesChance = 0.15f;

    [Tooltip("Minimum remaining beam energy before this AI starts a new beam burst. Prevents thrashing when the beam is nearly drained.")]
    [SerializeField] private float minimumBeamRestartEnergy = 20f;

    [Header("Threat Sense (Dodge Trigger)")]
    [Tooltip("Seconds between physics scans for incoming player projectiles.")]
    [SerializeField] private float threatScanInterval = 0.15f;

    [Tooltip("Radius (meters) of the physics overlap scan that looks for incoming player projectiles. Larger values give earlier dodge reactions but cost more CPU per scan.")]
    [SerializeField] private float threatScanRadius = 60f;

    [Tooltip("Layers to include in the threat scan. Set to the layers your projectile prefabs live on under Assets/Prefabs/3d_weapons/projectiles/.")]
    [SerializeField] private LayerMask projectileLayers = ~0;

    [Tooltip("Probability the duelist actually dodges when a valid threat is detected. Below 1 so dodges feel like a reactive instinct rather than a guaranteed read.")]
    [Range(0f, 1f)]
    [SerializeField] private float dodgeChancePerThreat = 0.45f;

    [Tooltip("Minimum seconds between successive dodges. Prevents the duelist from chain-dodging every projectile in a stream.")]
    [SerializeField] private float dodgeCooldown = 1f;

    [Tooltip("Seconds the dodge strafe impulse is applied for. Short values feel snappy; longer values feel like sustained slides.")]
    [SerializeField] private float dodgeDuration = 0.35f;

    [Tooltip("World-space speed (m/s) of the dodge strafe. Should be higher than Orbit Strafe Speed so dodges visibly read as faster than the idle orbit drift.")]
    [SerializeField] private float dodgeSpeed = 45f;

    [Tooltip("Required dot product between an incoming projectile's velocity and the direction from the projectile to the duelist before that projectile is treated as a real threat. 1 = perfectly heading toward us; 0 = perpendicular; lower threshold catches more projectiles but produces more false positives.")]
    [Range(0f, 1f)]
    [SerializeField] private float threatHeadingDotThreshold = 0.6f;

    [Tooltip("Maximum colliders considered per threat scan. Higher values catch more in dense fire but cost more CPU per scan.")]
    [Range(1, 64)]
    [SerializeField] private int threatScanBufferSize = 16;

    [Header("Steering Composition")]
    [Tooltip("If true, route reposition steering through the separation component when one is assigned.")]
    [SerializeField] private bool useSeparation = true;

    [Tooltip("If true, route reposition steering through the obstacle avoidance component when one is assigned.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    [Header("Debug")]
    [Tooltip("If true, logs duelist weapon picks, dodge triggers, and state transitions for tuning.")]
    [SerializeField] private bool logDecisions;

    private NetworkObject _networkObject;
    private Collider[] _threatBuffer;

    private DuelistState _state = DuelistState.Reposition;
    private Entity3D _currentTarget;
    private Vector3 _perchPosition;
    private float _nextThinkTime;
    private float _stateStartedAt;
    private float _stateEndsAt;
    private float _nextPerchRefreshAt;
    private float _nextStrafeRollAt;
    private float _nextThreatScanAt;
    private float _nextDodgeAllowedAt;
    private float _suppressFireUntil;
    private int _perchSequence;
    private bool _beamActive;
    private DuelistWeaponChoice _activeBeamSlot = DuelistWeaponChoice.None;

    private void Awake()
    {
        projectileWeapon ??= GetComponent<ProjectileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<MissileWeaponEnemy3D>();
        beamWeapon ??= GetComponent<BeamWeapon3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        strafeMover ??= GetComponent<EnemyStrafeMover3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        _networkObject = GetComponent<NetworkObject>();
        _threatBuffer = new Collider[Mathf.Max(1, threatScanBufferSize)];
    }

    public void ApplyProfile(EnemyBalanceProfile3D.DuelistBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        preferredRangeMin = Mathf.Max(0f, stats.preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, stats.preferredRangeMax);
        perchArrivalDistance = Mathf.Max(0.1f, stats.perchArrivalDistance);
        maxRepositionDuration = Mathf.Max(0.1f, stats.maxRepositionDuration);
        perchRefreshInterval = Mathf.Max(0.1f, stats.perchRefreshInterval);
        lateralPerchBias = Mathf.Max(0f, stats.lateralPerchBias);
        verticalPerchBias = Mathf.Max(0f, stats.verticalPerchBias);
        outwardPerchBias = Mathf.Max(0f, stats.outwardPerchBias);
        forwardArcAvoidanceWeight = Mathf.Clamp01(stats.forwardArcAvoidanceWeight);
        perchCandidateCount = Mathf.Clamp(stats.perchCandidateCount, 2, 16);
        strafeRollInterval = Mathf.Max(0.1f, stats.strafeRollInterval);
        orbitStrafeSpeed = Mathf.Max(0f, stats.orbitStrafeSpeed);
        orbitVerticalTiltAmount = Mathf.Clamp01(stats.orbitVerticalTiltAmount);
        projectileBand = ToWeaponRangeBand(stats.projectileBand);
        missileBand = ToWeaponRangeBand(stats.missileBand);
        beamBand = ToWeaponRangeBand(stats.beamBand);
        vibesChance = Mathf.Clamp01(stats.vibesChance);
        minimumBeamRestartEnergy = Mathf.Max(0f, stats.minimumBeamRestartEnergy);
        threatScanInterval = Mathf.Max(0.02f, stats.threatScanInterval);
        threatScanRadius = Mathf.Max(0.1f, stats.threatScanRadius);
        dodgeChancePerThreat = Mathf.Clamp01(stats.dodgeChancePerThreat);
        dodgeCooldown = Mathf.Max(0f, stats.dodgeCooldown);
        dodgeDuration = Mathf.Max(0.05f, stats.dodgeDuration);
        dodgeSpeed = Mathf.Max(0f, stats.dodgeSpeed);
        threatHeadingDotThreshold = Mathf.Clamp01(stats.threatHeadingDotThreshold);
        threatScanBufferSize = Mathf.Clamp(stats.threatScanBufferSize, 1, 64);
        _threatBuffer = new Collider[threatScanBufferSize];
    }

    private static WeaponRangeBand ToWeaponRangeBand(EnemyBalanceProfile3D.WeaponRangeBandStats stats)
    {
        return new WeaponRangeBand
        {
            preferredCenter = Mathf.Max(0f, stats.preferredCenter),
            halfWidth = Mathf.Max(0f, stats.halfWidth),
            aimToleranceDegrees = Mathf.Clamp(stats.aimToleranceDegrees, 0f, 180f)
        };
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        preferredRangeMin = Mathf.Max(0f, preferredRangeMin);
        preferredRangeMax = Mathf.Max(preferredRangeMin + 0.01f, preferredRangeMax);
        perchArrivalDistance = Mathf.Max(0.1f, perchArrivalDistance);
        maxRepositionDuration = Mathf.Max(0.1f, maxRepositionDuration);
        perchRefreshInterval = Mathf.Max(0.1f, perchRefreshInterval);
        strafeRollInterval = Mathf.Max(0.1f, strafeRollInterval);
        orbitStrafeSpeed = Mathf.Max(0f, orbitStrafeSpeed);
        threatScanInterval = Mathf.Max(0.02f, threatScanInterval);
        threatScanRadius = Mathf.Max(0.1f, threatScanRadius);
        dodgeCooldown = Mathf.Max(0f, dodgeCooldown);
        dodgeDuration = Mathf.Max(0.05f, dodgeDuration);
        dodgeSpeed = Mathf.Max(0f, dodgeSpeed);
        minimumBeamRestartEnergy = Mathf.Max(0f, minimumBeamRestartEnergy);
        threatScanBufferSize = Mathf.Clamp(threatScanBufferSize, 1, 64);
    }

    private void OnEnable()
    {
        _state = DuelistState.Reposition;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time;
        _nextThinkTime = 0f;
        _nextPerchRefreshAt = 0f;
        _nextStrafeRollAt = 0f;
        _nextThreatScanAt = 0f;
        _nextDodgeAllowedAt = 0f;
        _suppressFireUntil = 0f;
        _perchSequence = 0;
        _beamActive = false;
        _activeBeamSlot = DuelistWeaponChoice.None;
        flightController?.ClearFlightIntent();
        strafeMover?.StopStrafe();
    }

    private void OnDisable()
    {
        StopBeam();
        flightController?.ClearFlightIntent();
        strafeMover?.StopStrafe();
        _currentTarget = null;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            StopBeam();
            flightController?.ClearFlightIntent();
            strafeMover?.StopStrafe();
            _currentTarget = null;
            return;
        }

        Entity3D target = ResolveTarget();
        if (target == null)
        {
            StopBeam();
            strafeMover?.StopStrafe();
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
        TickThreatScan(target, targetDirection);
        RefreshActiveBeamAim();

        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
        Think(target, targetDirection, distanceToTarget);
    }

    private void Think(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        switch (_state)
        {
            case DuelistState.Reposition:
                UpdateReposition(target, targetDirection, distanceToTarget);
                break;
            case DuelistState.Engage:
                UpdateEngage(target, targetDirection, distanceToTarget);
                break;
        }
    }

    private void UpdateReposition(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        bool tooFar = distanceToTarget > preferredRangeMax;
        bool timedOut = Time.time >= _stateEndsAt;

        if (timedOut)
        {
            BeginEngage(targetDirection, distanceToTarget);
            return;
        }

        if (tooFar)
        {
            // Direct pursuit at full speed. Don't waste time detouring to a perch on the far
            // side of the player when we are simply out of range. The orbit/perch refresh
            // during Engage handles flank seeking once we are back in band.
            Vector3 steeredPursuit = ResolveSteering(targetDirection);
            flightController?.SetFlightIntent(steeredPursuit, targetDirection, 1f, moveBackward: false);
            return;
        }

        // In-band perch fly: drift to the chosen flank position so the duelist does not
        // sit at one orbit angle for the entire fight.
        Vector3 toPerch = _perchPosition - transform.position;
        float toPerchDistance = toPerch.magnitude;
        bool reachedPerch = toPerchDistance <= Mathf.Max(0.1f, perchArrivalDistance);
        if (reachedPerch)
        {
            BeginEngage(targetDirection, distanceToTarget);
            return;
        }

        Vector3 desired = toPerchDistance > 0.0001f ? toPerch / toPerchDistance : targetDirection;
        Vector3 steered = ResolveSteering(desired);
        flightController?.SetFlightIntent(steered, targetDirection, 1f, moveBackward: false);
    }

    private void UpdateEngage(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        // Only kick out of engage when too FAR from the band, or when the perch refresh timer
        // expires. Being closer than preferredRangeMin is fine: keep facing/firing/orbit-strafing
        // and let the natural drift bring us back out. Otherwise the brain repositions every tick
        // when the player closes inside us, which silently suppresses all fire.
        bool tooFar = distanceToTarget > preferredRangeMax;
        if (tooFar || Time.time >= _nextPerchRefreshAt)
        {
            BeginReposition(target);
            return;
        }

        flightController?.SetFacingDirection(targetDirection);
        UpdateOrbitStrafe(targetDirection);

        if (Time.time < _suppressFireUntil)
        {
            StopBeam();
            return;
        }

        PickWeaponAndFire(target, targetDirection, distanceToTarget);
    }

    private void BeginReposition(Entity3D target)
    {
        _state = DuelistState.Reposition;
        _stateStartedAt = Time.time;
        _stateEndsAt = Time.time + Mathf.Max(0.1f, maxRepositionDuration);
        _perchPosition = ChooseNextPerch(target);
        StopBeam();
        if (logDecisions)
        {
            Debug.Log($"[{nameof(DuelistEnemyBrain3D)}] {name} BeginReposition perch={_perchPosition}", this);
        }
    }

    private void BeginEngage(Vector3 targetDirection, float distanceToTarget)
    {
        _state = DuelistState.Engage;
        _stateStartedAt = Time.time;
        _nextPerchRefreshAt = Time.time + Mathf.Max(0.1f, perchRefreshInterval);
        _nextStrafeRollAt = 0f;
        flightController?.SetFacingDirection(targetDirection);
        if (logDecisions)
        {
            Debug.Log($"[{nameof(DuelistEnemyBrain3D)}] {name} BeginEngage at distance {distanceToTarget:F1}m", this);
        }
    }

    private void UpdateOrbitStrafe(Vector3 targetDirection)
    {
        if (strafeMover == null || orbitStrafeSpeed <= 0f)
        {
            return;
        }

        // Don't override an active dodge.
        if (strafeMover.IsStrafing && Time.time < _nextDodgeAllowedAt)
        {
            return;
        }

        if (Time.time < _nextStrafeRollAt && strafeMover.IsStrafing)
        {
            return;
        }

        _nextStrafeRollAt = Time.time + Mathf.Max(0.1f, strafeRollInterval);

        Vector3 horizontalTangent = Vector3.Cross(Vector3.up, targetDirection);
        if (horizontalTangent.sqrMagnitude <= 0.0001f)
        {
            horizontalTangent = Vector3.Cross(transform.right, targetDirection);
        }
        if (horizontalTangent.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        horizontalTangent.Normalize();
        float lateralSign = Random.value < 0.5f ? -1f : 1f;
        float verticalMix = Mathf.Clamp01(orbitVerticalTiltAmount) * Random.Range(-1f, 1f);

        Vector3 strafeDir = (horizontalTangent * lateralSign) + (Vector3.up * verticalMix);
        if (strafeDir.sqrMagnitude <= 0.0001f)
        {
            strafeDir = horizontalTangent * lateralSign;
        }

        strafeMover.BeginStrafe(strafeDir.normalized * orbitStrafeSpeed, Mathf.Max(0.1f, strafeRollInterval));
    }

    private void PickWeaponAndFire(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        float forwardAimAngle = Vector3.Angle(transform.forward, targetDirection);

        float projectileScore = ScoreWeaponBand(distanceToTarget, projectileBand, forwardAimAngle, projectileWeapon != null && projectileWeapon.IsFireGateReady);
        float missileScore = ScoreWeaponBand(distanceToTarget, missileBand, forwardAimAngle, missileWeapon != null && missileWeapon.IsFireGateReady);
        float beamScore = ScoreBeamBand(distanceToTarget, target, forwardAimAngle);

        DuelistWeaponChoice choice = ResolveWeaponChoice(projectileScore, missileScore, beamScore);
        if (choice == DuelistWeaponChoice.None)
        {
            // No weapon valid this tick; if a beam was active but it's no longer the pick, stop it.
            if (_beamActive && _activeBeamSlot == DuelistWeaponChoice.Beam)
            {
                StopBeam();
            }
            return;
        }

        // Single-weapon-at-a-time rule: if beam was active but a different weapon won, stop the beam first.
        if (_beamActive && choice != DuelistWeaponChoice.Beam)
        {
            StopBeam();
        }

        bool fired = false;
        switch (choice)
        {
            case DuelistWeaponChoice.Projectile:
                fired = TryFireProjectileWeapon(projectileWeapon, targetDirection);
                break;
            case DuelistWeaponChoice.Missile:
                fired = TryFireProjectileWeapon(missileWeapon, targetDirection);
                break;
            case DuelistWeaponChoice.Beam:
                fired = TryUseBeam(target);
                break;
        }

        if (logDecisions)
        {
            Debug.Log($"[{nameof(DuelistEnemyBrain3D)}] {name} pick={choice} dist={distanceToTarget:F1} aim={forwardAimAngle:F1} fired={fired} (P={projectileScore:F2} M={missileScore:F2} B={beamScore:F2})", this);
        }
    }

    private float ScoreWeaponBand(float distanceToTarget, WeaponRangeBand band, float forwardAimAngle, bool weaponReady)
    {
        if (!weaponReady)
        {
            return 0f;
        }
        if (forwardAimAngle > Mathf.Max(0f, band.aimToleranceDegrees))
        {
            return 0f;
        }
        // Asymmetric falloff: full score at any distance up to and including Preferred Center
        // (a close-range gun should still fire at point-blank), and a soft falloff above center
        // out to center + Half Width. Beyond that the weapon scores 0 and another weapon should
        // win the pick.
        float halfWidth = Mathf.Max(0.01f, band.halfWidth);
        if (distanceToTarget <= band.preferredCenter)
        {
            return 1f;
        }
        float distanceError = distanceToTarget - band.preferredCenter;
        return 1f - Mathf.Clamp01(distanceError / halfWidth);
    }

    private float ScoreBeamBand(float distanceToTarget, Entity3D target, float forwardAimAngle)
    {
        if (beamWeapon == null || !beamWeapon.enabled)
        {
            return 0f;
        }

        if (forwardAimAngle > Mathf.Max(0f, beamBand.aimToleranceDegrees))
        {
            return 0f;
        }

        if (!CanStartOrSustainBeam())
        {
            return 0f;
        }

        // Use the beam's own forward direction (which may be a separate hardpoint) for the
        // precise aim check, in addition to the cheap chassis-forward gate above.
        Vector3 beamForward = beamWeapon.GetBeamForwardDirection();
        Vector3 beamOrigin = beamWeapon.GetBeamOrigin(beamForward);
        Vector3 toTargetFromBeam = ResolveTargetPoint(target) - beamOrigin;
        if (toTargetFromBeam.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        float beamAimAngle = Vector3.Angle(beamForward, toTargetFromBeam.normalized);
        if (beamAimAngle > Mathf.Max(0f, beamBand.aimToleranceDegrees))
        {
            return 0f;
        }

        float halfWidth = Mathf.Max(0.01f, beamBand.halfWidth);
        if (distanceToTarget <= beamBand.preferredCenter)
        {
            return 1f;
        }
        float distanceError = distanceToTarget - beamBand.preferredCenter;
        return 1f - Mathf.Clamp01(distanceError / halfWidth);
    }

    private DuelistWeaponChoice ResolveWeaponChoice(float projectileScore, float missileScore, float beamScore)
    {
        int validCount = 0;
        if (projectileScore > 0f) validCount++;
        if (missileScore > 0f) validCount++;
        if (beamScore > 0f) validCount++;

        if (validCount == 0)
        {
            return DuelistWeaponChoice.None;
        }

        if (validCount > 1 && Random.value < Mathf.Clamp01(vibesChance))
        {
            int pick = Random.Range(0, validCount);
            int index = 0;
            if (projectileScore > 0f) { if (index == pick) return DuelistWeaponChoice.Projectile; index++; }
            if (missileScore > 0f) { if (index == pick) return DuelistWeaponChoice.Missile; index++; }
            if (beamScore > 0f) { if (index == pick) return DuelistWeaponChoice.Beam; }
        }

        DuelistWeaponChoice best = DuelistWeaponChoice.None;
        float bestScore = 0f;
        if (projectileScore > bestScore) { bestScore = projectileScore; best = DuelistWeaponChoice.Projectile; }
        if (missileScore > bestScore) { bestScore = missileScore; best = DuelistWeaponChoice.Missile; }
        if (beamScore > bestScore) { bestScore = beamScore; best = DuelistWeaponChoice.Beam; }
        return best;
    }

    private bool TryFireProjectileWeapon(EnemyProjectileWeaponBase3D weapon, Vector3 targetDirection)
    {
        if (weapon == null)
        {
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(weapon, Faction3D.PlayerTeam, targetDirection);
        }

        return weapon.TryFireAtFaction(Faction3D.PlayerTeam, targetDirection);
    }

    // ---- Beam helpers (mirrors SplitterEnemyBrain3D's beam path) ----

    private bool TryUseBeam(Entity3D target)
    {
        if (beamWeapon == null || !beamWeapon.enabled)
        {
            return false;
        }

        Vector3 fireDirection = beamWeapon.GetBeamForwardDirection();
        if (fireDirection.sqrMagnitude <= 0.0001f)
        {
            StopBeam();
            return false;
        }

        StartOrUpdateBeam(fireDirection);
        return true;
    }

    private bool CanStartOrSustainBeam()
    {
        if (beamWeapon == null)
        {
            return false;
        }

        if (beamWeapon.IsBeamActive)
        {
            return true;
        }

        if (!beamWeapon.CanStartBeamNow())
        {
            return false;
        }

        float remainingEnergy = beamWeapon.GetRemainingBeamEnergy();
        float minimumRestartEnergy = Mathf.Max(0f, minimumBeamRestartEnergy);
        return remainingEnergy <= 0f || remainingEnergy + 0.001f >= minimumRestartEnergy;
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

    private void StartOrUpdateBeam(Vector3 aimDirection)
    {
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, true, aimDirection);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamAim(aimDirection);
            beamWeapon.ApplyNetworkBeamState(true, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
        }

        _beamActive = true;
        _activeBeamSlot = DuelistWeaponChoice.Beam;
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
            _activeBeamSlot = DuelistWeaponChoice.None;
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, false, transform.forward);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamState(false, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
        }

        _beamActive = false;
        _activeBeamSlot = DuelistWeaponChoice.None;
    }

    // ---- Threat scan + dodge ----

    private void TickThreatScan(Entity3D target, Vector3 targetDirection)
    {
        if (Time.time < _nextThreatScanAt)
        {
            return;
        }

        _nextThreatScanAt = Time.time + Mathf.Max(0.02f, threatScanInterval);

        if (Time.time < _nextDodgeAllowedAt)
        {
            return;
        }

        if (strafeMover == null)
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, threatScanRadius, _threatBuffer, projectileLayers, QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return;
        }

        float bestDot = threatHeadingDotThreshold;
        Vector3 bestProjectileVelocity = Vector3.zero;
        bool foundThreat = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _threatBuffer[i];
            if (col == null)
            {
                continue;
            }

            Projectile3D projectile = col.GetComponentInParent<Projectile3D>();
            if (projectile == null)
            {
                continue;
            }

            // Filter for projectiles that target our faction (i.e. fired by the opposing team).
            if (projectile.TargetFaction != Faction3D.EnemyTeam)
            {
                continue;
            }

            Vector3 projectileVelocity = projectile.Direction * projectile.Speed;
            float velSqr = projectileVelocity.sqrMagnitude;
            if (velSqr <= 0.0001f)
            {
                continue;
            }

            Vector3 toUs = transform.position - projectile.transform.position;
            if (toUs.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            float dot = Vector3.Dot(projectileVelocity / Mathf.Sqrt(velSqr), toUs.normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestProjectileVelocity = projectileVelocity;
                foundThreat = true;
            }
        }

        if (!foundThreat)
        {
            return;
        }

        if (Random.value > Mathf.Clamp01(dodgeChancePerThreat))
        {
            return;
        }

        BeginDodge(bestProjectileVelocity, targetDirection);
    }

    private void BeginDodge(Vector3 incomingVelocity, Vector3 targetDirection)
    {
        Vector3 incomingDir = incomingVelocity.sqrMagnitude > 0.0001f
            ? incomingVelocity.normalized
            : -targetDirection;

        // Pick a perpendicular plane to the incoming projectile, then a random perpendicular dir within it.
        Vector3 reference = Mathf.Abs(Vector3.Dot(incomingDir, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
        Vector3 perpA = Vector3.Cross(incomingDir, reference).normalized;
        Vector3 perpB = Vector3.Cross(incomingDir, perpA).normalized;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 dodgeDir = (perpA * Mathf.Cos(angle)) + (perpB * Mathf.Sin(angle));
        if (dodgeDir.sqrMagnitude <= 0.0001f)
        {
            dodgeDir = perpA;
        }

        strafeMover.BeginStrafe(dodgeDir.normalized * dodgeSpeed, Mathf.Max(0.05f, dodgeDuration));
        _nextDodgeAllowedAt = Time.time + Mathf.Max(0f, dodgeCooldown) + dodgeDuration;
        _suppressFireUntil = Time.time + dodgeDuration;
        _nextStrafeRollAt = _nextDodgeAllowedAt;

        if (logDecisions)
        {
            Debug.Log($"[{nameof(DuelistEnemyBrain3D)}] {name} Dodge dir={dodgeDir} speed={dodgeSpeed} duration={dodgeDuration}", this);
        }
    }

    // ---- Perch picking ----

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

        Vector3 targetForward = target.transform.forward.sqrMagnitude > 0.0001f
            ? target.transform.forward.normalized
            : Vector3.zero;

        float minRange = Mathf.Max(0f, preferredRangeMin);
        float maxRange = Mathf.Max(minRange + 0.01f, preferredRangeMax);

        Vector3 bestDir = away;
        float bestScore = float.NegativeInfinity;
        int candidates = Mathf.Clamp(perchCandidateCount, 2, 16);

        for (int i = 0; i < candidates; i++)
        {
            float t = (i + Random.value * 0.5f) / candidates;
            float angle = t * Mathf.PI * 2f;
            float verticalSign = ((i + _perchSequence) % 3) switch { 0 => 1f, 1 => -1f, _ => 0.35f };

            Vector3 candidate =
                (away * Mathf.Max(0f, outwardPerchBias))
                + (lateral * Mathf.Cos(angle) * Mathf.Max(0f, lateralPerchBias))
                + (Vector3.Cross(away, lateral) * Mathf.Sin(angle) * Mathf.Max(0f, lateralPerchBias))
                + (Vector3.up * verticalSign * Mathf.Max(0f, verticalPerchBias));

            if (candidate.sqrMagnitude <= 0.0001f)
            {
                continue;
            }
            candidate.Normalize();

            float forwardArcPenalty = 0f;
            if (targetForward.sqrMagnitude > 0.0001f && forwardArcAvoidanceWeight > 0f)
            {
                float forwardDot = Mathf.Clamp01(Vector3.Dot(candidate, targetForward));
                forwardArcPenalty = forwardDot * forwardArcAvoidanceWeight;
            }

            float score = 1f - forwardArcPenalty + Random.value * 0.05f;
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = candidate;
            }
        }

        _perchSequence++;
        float range = Mathf.Lerp(minRange, maxRange, Random.value);
        return targetPosition + bestDir * range;
    }

    // ---- Misc helpers ----

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

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private Entity3D ResolveTarget()
    {
        if (IsTargetValid(_currentTarget))
        {
            return _currentTarget;
        }

        _currentTarget = targetSensor != null ? targetSensor.GetTarget() : null;
        if (_currentTarget != null)
        {
            BeginReposition(_currentTarget);
        }
        return _currentTarget;
    }

    private static bool IsTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }

    private static Vector3 ResolveTargetPoint(Entity3D target)
    {
        Collider targetCollider = target != null ? target.GetComponentInChildren<Collider>() : null;
        return targetCollider != null
            ? targetCollider.bounds.center
            : target != null ? target.transform.position : Vector3.zero;
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

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, threatScanRadius);
        }
    }
}
