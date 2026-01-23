using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossPhase
{
    BulletHell,
    // Future phases can be added here
    Phase2,
    Phase3,
    Phase4
}

public enum BulletHellAttack
{
    SpiralBarrage,          // Rotating spiral of bullets
    ConvergingLasers,       // Lasers that track and converge on player
    RadialBurst,            // Massive radial burst with safe zones
    LaserSweep,             // Lasers sweep across the arena
    CombinedAssault,        // Bullets + Lasers simultaneously
    CrossfireChaos,         // Multiple overlapping patterns
    VWaveBarrage,           // Diagonal V-shaped walls of bullets
    SpiralVWaveCombo        // Spiral with V-wave interjections
}

/// <summary>
/// Boss enemy featuring intense bullet hell patterns with converging lasers,
/// massive bullet waves, and combined attacks. Designed to be extremely challenging
/// with small windows for player retaliation.
/// </summary>
public class BossBulletHellScript : EnemyScript
{
    #region Phase Settings
    [Header("Boss Phase Settings")]
    [Tooltip("Current phase of the boss fight")]
    public BossPhase currentPhase = BossPhase.BulletHell;

    [Tooltip("Health threshold percentages to transition to next phase (0-1)")]
    public float[] phaseTransitionThresholds = { 0.75f, 0.5f, 0.25f };

    [Tooltip("Brief invulnerability during phase transitions")]
    public float phaseTransitionInvulnerabilityDuration = 2f;

    [Tooltip("Is boss currently transitioning between phases")]
    private bool _isTransitioning = false;
    #endregion

    #region Bullet Hell Phase - General
    [Header("Bullet Hell Phase - General")]
    [Tooltip("Duration of each attack pattern before switching")]
    public float attackDuration = 5f;

    [Tooltip("Brief pause between attack patterns")]
    public float attackTransitionDelay = 0.5f;

    [Tooltip("Attacks to cycle through in bullet hell phase")]
    public List<BulletHellAttack> bulletHellAttackSequence = new List<BulletHellAttack>
    {
        BulletHellAttack.SpiralBarrage,
        BulletHellAttack.ConvergingLasers,
        BulletHellAttack.RadialBurst,
        BulletHellAttack.CombinedAssault
    };

    [Tooltip("Randomize attack order instead of sequential")]
    public bool randomizeAttacks = false;

    [Tooltip("Always start with Spiral Barrage as the first attack")]
    public bool alwaysStartWithSpiral = true;

    private BulletHellAttack _currentAttack;
    private int _attackIndex = 0;
    private bool _isFirstAttack = true;
    private float _attackStartTime;
    private bool _isAttacking = false;
    private Coroutine _attackCoroutine;
    #endregion

    #region Spiral Barrage Settings
    [Header("Spiral Barrage Attack")]
    [Tooltip("Number of bullet streams in the spiral")]
    public int spiralStreamCount = 8;

    [Tooltip("Rotation speed of the spiral pattern (degrees/second)")]
    public float spiralRotationSpeed = 90f;

    [Tooltip("Bullets fired per second per stream")]
    public float spiralFireRate = 10f;

    [Tooltip("Alternating rotation direction every X seconds (0 = no alternation)")]
    public float spiralDirectionChangeInterval = 3f;

    [Tooltip("Add secondary counter-rotating spiral")]
    public bool dualSpiral = true;

    [Tooltip("Offset angle between primary and secondary spiral")]
    public float dualSpiralOffset = 22.5f;

    private float _spiralAngle = 0f;
    private float _spiralLastDirectionChange = 0f;
    private int _spiralDirection = 1;
    private float _nextSpiralFireTime = 0f;
    #endregion

    #region Converging Lasers Settings
    [Header("Converging Lasers Attack")]
    [Tooltip("Number of lasers to use for converging attack")]
    public int convergingLaserCount = 4;

    [Tooltip("How quickly lasers track toward player position (degrees/second)")]
    public float laserTrackingSpeed = 45f;

    [Tooltip("Delay before lasers start tracking (gives player time to react)")]
    public float laserTrackingDelay = 0.5f;

    [Tooltip("How far behind the player the lasers converge (lag factor)")]
    public float laserConvergenceLag = 0.3f;

    [Tooltip("Laser warm-up time before firing")]
    public float laserWarmupTime = 0.5f;

    [Tooltip("Duration lasers fire continuously")]
    public float laserFireDuration = 3f;

    [Tooltip("Offset distance from boss center for laser spawn points")]
    public float laserSpawnOffset = 2f;

    [Tooltip("Laser turret indices to use (leave empty to auto-assign)")]
    public List<int> convergingLaserTurretIndices = new List<int>();

    [Header("Converging Lasers - Initial Aim Offset")]
    [Tooltip("How far away from the player the lasers initially aim (units). Lasers start pointing this far from the player then track in.")]
    public float laserInitialAimOffset = 5f;

    [Tooltip("Tracking speed multiplier for aggressive tracking (higher = faster convergence)")]
    public float aggressiveTrackingMultiplier = 3f;

    private LaserBeam[] _convergingBeams;
    private Vector2[] _laserTargetDirections;
    private Vector2 _trackedPlayerPosition;
    private float _laserStartTime;
    private bool _lasersActive = false;
    #endregion

    #region Radial Burst Settings
    [Header("Radial Burst Attack")]
    [Tooltip("Number of bullets in each radial burst")]
    public int radialBurstCount = 36;

    [Tooltip("Number of bursts to fire in sequence")]
    public int radialBurstWaves = 5;

    [Tooltip("Delay between burst waves")]
    public float radialBurstInterval = 0.8f;

    [Tooltip("Angle offset between consecutive bursts (creates safe zone rotation)")]
    public float radialBurstAngleOffset = 5f;

    [Tooltip("Safe zone gap in degrees (opening for player)")]
    public float radialSafeZoneAngle = 30f;

    [Tooltip("Number of safe zones in the burst")]
    public int radialSafeZoneCount = 2;

    private float _radialBurstAngle = 0f;
    #endregion

    #region Laser Sweep Settings
    [Header("Laser Sweep Attack")]
    [Tooltip("Number of lasers for sweep attack")]
    public int sweepLaserCount = 2;

    [Tooltip("Sweep rotation speed (degrees/second)")]
    public float sweepRotationSpeed = 60f;

    [Tooltip("Sweep arc (degrees) - lasers sweep back and forth")]
    public float sweepArc = 180f;

    [Tooltip("Pause at sweep endpoints")]
    public float sweepEndpointPause = 0.3f;

    private LaserBeam[] _sweepBeams;
    private float _sweepAngle = 0f;
    private int _sweepDirection = 1;
    private float _sweepPauseTime = 0f;
    #endregion

    #region Combined Assault Settings
    [Header("Combined Assault Attack")]
    [Tooltip("Fire rate multiplier for bullets during combined attack")]
    public float combinedBulletRateMultiplier = 0.7f;

    [Tooltip("Number of bullet streams during combined attack")]
    public int combinedBulletStreams = 6;

    [Tooltip("Use tracking lasers during combined attack")]
    public bool combinedUseTrackingLasers = true;

    [Tooltip("Use sweeping lasers during combined attack")]
    public bool combinedUseSweepingLasers = true;

    [Tooltip("Bullet spread angle for combined attack")]
    public float combinedBulletSpread = 15f;
    #endregion

    #region Crossfire Chaos Settings
    [Header("Crossfire Chaos Attack")]
    [Tooltip("Multiple overlapping spiral patterns")]
    public int crossfireSpiralCount = 3;

    [Tooltip("Speed variation between spirals")]
    public float crossfireSpeedVariation = 0.3f;

    [Tooltip("Random burst interjections per second")]
    public float crossfireRandomBurstRate = 2f;

    [Tooltip("Bullets per random burst")]
    public int crossfireRandomBurstCount = 12;

    private float[] _crossfireSpiralAngles;
    private float[] _crossfireSpiralSpeeds;
    private float _nextCrossfireBurstTime;
    #endregion

    #region V-Wave Barrage Settings
    [Header("V-Wave Barrage Attack")]
    [Tooltip("Number of V shapes stacked in each wave (WARNING: High values cause lag! Bullets per volley = stacks * bulletsPerArm * 2)")]
    public int vWaveStackCount = 3;

    [Tooltip("Angle of the V shape (degrees from center)")]
    public float vWaveAngle = 35f;

    [Tooltip("Spacing between stacked V shapes")]
    public float vWaveStackSpacing = 0.15f;

    [Tooltip("Bullets per arm of each V (WARNING: High values cause lag! Bullets per volley = stacks * bulletsPerArm * 2)")]
    public int vWaveBulletsPerArm = 4;

    [Tooltip("Time between V-wave volleys (higher = less bullets per second)")]
    public float vWaveInterval = 0.6f;

    [Tooltip("V-wave rotation speed (degrees/second) - rotates the whole pattern")]
    public float vWaveRotationSpeed = 45f;

    [Tooltip("Alternate direction of V-waves")]
    public bool vWaveAlternate = true;

    private float _vWaveAngle = 0f;
    private int _vWaveDirection = 1;
    #endregion

    #region Spiral V-Wave Combo Settings
    [Header("Spiral + V-Wave Combo Attack")]
    [Tooltip("V-waves per spiral rotation")]
    public int comboVWavesPerRotation = 4;

    [Tooltip("Spiral stream count during combo")]
    public int comboSpiralStreams = 4;

    [Tooltip("Use lasers during combo attack")]
    public bool comboUseLasers = true;
    #endregion

    #region Projectile Settings
    [Header("Boss Projectile Overrides")]
    [Tooltip("Override projectile speed for boss attacks")]
    public float bossProjectileSpeed = 12f;

    [Tooltip("Projectile speed variation (randomized per shot)")]
    public float projectileSpeedVariation = 2f;

    [Tooltip("Override projectile damage")]
    public float bossProjectileDamage = 10f;

    [Tooltip("Override projectile lifetime")]
    public float bossProjectileLifetime = 5f;

    [Tooltip("Random spray angle added to each bullet (degrees). Makes patterns harder to weave through.")]
    public float bulletSprayAngle = 8f;

    [Tooltip("Additional wobble that oscillates over time (degrees)")]
    public float bulletWobbleAngle = 5f;

    [Tooltip("Speed of the wobble oscillation")]
    public float bulletWobbleSpeed = 3f;

    [Tooltip("Reduce recoil from massive bullet output")]
    public bool reduceRecoil = true;

    [Tooltip("Recoil reduction multiplier")]
    public float recoilMultiplier = 0.05f;
    #endregion

    #region Movement Settings
    [Header("Boss Movement")]
    [Tooltip("Boss aggressively stays within this distance of player")]
    public float preferredCombatDistance = 8f;

    [Tooltip("Maximum distance before boss chases player")]
    public float maxDistanceFromPlayer = 12f;

    [Tooltip("Boss movement speed multiplier during attacks")]
    public float attackMovementMultiplier = 0.5f;

    [Tooltip("Slow drift movement while attacking")]
    public bool driftWhileAttacking = true;

    [Tooltip("Drift direction change interval")]
    public float driftDirectionChangeInterval = 4f;

    private Vector2 _driftDirection;
    private float _lastDriftChange;
    #endregion

    #region Vulnerability Window
    [Header("Vulnerability Windows")]
    [Tooltip("Duration of vulnerability window between attacks")]
    public float vulnerabilityWindowDuration = 2f;

    [Tooltip("Visual indicator for vulnerability (e.g., shield flicker)")]
    public bool showVulnerabilityIndicator = true;

    [Tooltip("Damage multiplier during vulnerability")]
    public float vulnerabilityDamageMultiplier = 1.5f;

    [Tooltip("Audio clip to play when boss shield goes down (vulnerability starts)")]
    public AudioClip shieldDownSound;

    [Tooltip("Volume for shield down sound")]
    [Range(0f, 1f)]
    public float shieldDownVolume = 0.8f;

    [Header("Boss Damage Resistance")]
    [Tooltip("Multiplier for reflected projectile damage (0.5 = half damage from reflections). Set to 0.5 to convert player's 5x reflection to effective 2.5x.")]
    [Range(0.1f, 1f)]
    public float reflectedDamageMultiplier = 0.5f;

    private bool _isVulnerable = false;
    private float _vulnerabilityStartTime;
    #endregion

    #region Debug
    [Header("Debug Settings")]
    [Tooltip("Lock to specific attack for testing")]
    public bool debugLockAttack = false;

    [Tooltip("Attack to lock to when debug mode is enabled")]
    public BulletHellAttack debugLockedAttack = BulletHellAttack.SpiralBarrage;

    [Tooltip("Show attack pattern gizmos")]
    public bool showDebugGizmos = true;
    #endregion

    #region Visual Pulse
    [Header("Visual Pulse (Rhythmic Effect)")]
    [Tooltip("Enable rhythmic scale pulse effect")]
    public bool enableVisualPulse = true;

    [Tooltip("Beats per minute - controls pulse timing (set to match your music)")]
    public float pulseBPM = 120f;

    [Tooltip("Scale multiplier at peak of pulse (1.05 = 5% larger)")]
    public float pulseScaleMultiplier = 1.05f;

    [Tooltip("How long the pulse takes (seconds) - shorter = snappier")]
    public float pulseDuration = 0.1f;

    [Tooltip("Pulse on every Nth beat (1 = every beat, 2 = every other beat, 4 = once per measure)")]
    public int pulseEveryNthBeat = 1;

    // Pulse state
    private Vector3 _originalScale;
    private float _pulseTimer = 0f;
    private float _pulseInterval;
    private Coroutine _pulseCoroutine;
    #endregion

    #region Boss Audio
    [Header("Boss Audio Overrides")]
    [Tooltip("Override fire sound for boss (uses base projectileFireSound if null)")]
    public AudioClip bossFireSound;

    [Tooltip("Volume for boss projectile sounds (0-1)")]
    [Range(0f, 1f)]
    public float bossFireVolume = 0.3f;

    [Tooltip("Only play fire sound every Nth shot (reduces audio spam)")]
    public int playSoundEveryNthShot = 4;

    private int _shotCounter = 0;
    #endregion

    #region Private State
    private List<LaserBeam> _allActiveBeams = new List<LaserBeam>();
    private float _baseRotationSpeed;
    private bool _initialized = false;
    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        base.Start();
        
        _baseRotationSpeed = rotationSpeed;
        _driftDirection = Random.insideUnitCircle.normalized;
        _lastDriftChange = Time.time;
        _originalScale = transform.localScale;
        
        // Calculate pulse interval from BPM
        _pulseInterval = (60f / pulseBPM) * pulseEveryNthBeat;
        _pulseTimer = 0f;
        
        InitializeCrossfireData();
        
        _initialized = true;
        
        // Start the attack cycle
        StartCoroutine(AttackCycleCoroutine());
    }

    protected override void Update()
    {
        base.Update();
        
        if (!_initialized || _target == null) return;
        
        // Update drift direction periodically
        if (Time.time - _lastDriftChange > driftDirectionChangeInterval)
        {
            _driftDirection = Random.insideUnitCircle.normalized;
            _lastDriftChange = Time.time;
        }
        
        // Visual pulse timer
        if (enableVisualPulse)
        {
            _pulseTimer += Time.deltaTime;
            if (_pulseTimer >= _pulseInterval)
            {
                _pulseTimer -= _pulseInterval;
                TriggerPulse();
            }
        }
        
        // Check for phase transitions
        CheckPhaseTransition();
    }

    private void TriggerPulse()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            transform.localScale = _originalScale;
        }
        _pulseCoroutine = StartCoroutine(PulseCoroutine());
    }

    /// <summary>
    /// Visual pulse effect - boss quickly scales up then back down.
    /// Looks like a heartbeat or speaker thump.
    /// </summary>
    private IEnumerator PulseCoroutine()
    {
        float elapsed = 0f;
        float halfDuration = pulseDuration / 2f;
        Vector3 targetScale = _originalScale * pulseScaleMultiplier;
        
        // Scale up (fast)
        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            t = 1f - (1f - t) * (1f - t); // easeOutQuad
            transform.localScale = Vector3.Lerp(_originalScale, targetScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Scale down (fast)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            t = t * t; // easeInQuad
            transform.localScale = Vector3.Lerp(targetScale, _originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = _originalScale;
        _pulseCoroutine = null;
    }

    protected override void FixedUpdate()
    {
        // Call base for physics tracking
        if (_rb == null) return;
        
        // Track velocity for banking effects
        Vector2 currentVelocity = _rb.linearVelocity;
        
        // Boss always uses custom movement, regardless of enemy state
        if (_target != null)
        {
            HandleBossMovement();
        }
        
        // Apply lateral damping
        if (useLateralDamping)
        {
            ApplyLateralDamping();
        }
        
        ClampVelocity();
        
        // Update active beam positions
        UpdateActiveBeams();
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        CleanupAllBeams();
    }

    #endregion

    #region Phase Management

    private void CheckPhaseTransition()
    {
        if (_isTransitioning) return;
        
        float healthPercent = currentHealth / maxHealth;
        int targetPhase = 0;
        
        for (int i = 0; i < phaseTransitionThresholds.Length; i++)
        {
            if (healthPercent <= phaseTransitionThresholds[i])
            {
                targetPhase = i + 1;
            }
        }
        
        BossPhase newPhase = (BossPhase)Mathf.Min(targetPhase, System.Enum.GetValues(typeof(BossPhase)).Length - 1);
        
        if (newPhase != currentPhase)
        {
            StartCoroutine(TransitionToPhase(newPhase));
        }
    }

    private IEnumerator TransitionToPhase(BossPhase newPhase)
    {
        _isTransitioning = true;
        
        // Stop current attack
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }
        CleanupAllBeams();
        _isAttacking = false;
        
        // Brief invulnerability
        // TODO: Add visual effect for phase transition
        yield return new WaitForSeconds(phaseTransitionInvulnerabilityDuration);
        
        currentPhase = newPhase;
        _isTransitioning = false;
        
        // Restart attack cycle for new phase
        StartCoroutine(AttackCycleCoroutine());
    }

    #endregion

    #region Attack Cycle

    private IEnumerator AttackCycleCoroutine()
    {
        while (!_isTransitioning && currentHealth > 0)
        {
            // Select next attack
            if (_isFirstAttack && alwaysStartWithSpiral)
            {
                // Always start with Spiral Barrage
                _currentAttack = BulletHellAttack.SpiralBarrage;
                _isFirstAttack = false;
            }
            else if (debugLockAttack)
            {
                _currentAttack = debugLockedAttack;
            }
            else if (randomizeAttacks && bulletHellAttackSequence.Count > 0)
            {
                _currentAttack = bulletHellAttackSequence[Random.Range(0, bulletHellAttackSequence.Count)];
            }
            else if (bulletHellAttackSequence.Count > 0)
            {
                _currentAttack = bulletHellAttackSequence[_attackIndex];
                _attackIndex = (_attackIndex + 1) % bulletHellAttackSequence.Count;
            }
            
            // Execute attack
            _isAttacking = true;
            _isVulnerable = false;
            _attackStartTime = Time.time;
            
            yield return StartCoroutine(ExecuteAttack(_currentAttack));
            
            // Cleanup after attack
            CleanupAllBeams();
            _isAttacking = false;
            
            // Vulnerability window
            _isVulnerable = true;
            _vulnerabilityStartTime = Time.time;
            
            // Play shield down sound
            if (shieldDownSound != null)
            {
                PlayOneShotSound(shieldDownSound, shieldDownVolume, AudioClipType.Explosion);
            }
            
            yield return new WaitForSeconds(vulnerabilityWindowDuration);
            
            _isVulnerable = false;
            
            // Transition delay
            yield return new WaitForSeconds(attackTransitionDelay);
        }
    }

    private IEnumerator ExecuteAttack(BulletHellAttack attack)
    {
        float endTime = Time.time + attackDuration;
        
        switch (attack)
        {
            case BulletHellAttack.SpiralBarrage:
                yield return StartCoroutine(SpiralBarrageAttack(endTime));
                break;
                
            case BulletHellAttack.ConvergingLasers:
                yield return StartCoroutine(ConvergingLasersAttack(endTime));
                break;
                
            case BulletHellAttack.RadialBurst:
                yield return StartCoroutine(RadialBurstAttack(endTime));
                break;
                
            case BulletHellAttack.LaserSweep:
                yield return StartCoroutine(LaserSweepAttack(endTime));
                break;
                
            case BulletHellAttack.CombinedAssault:
                yield return StartCoroutine(CombinedAssaultAttack(endTime));
                break;
                
            case BulletHellAttack.CrossfireChaos:
                yield return StartCoroutine(CrossfireChaosAttack(endTime));
                break;
                
            case BulletHellAttack.VWaveBarrage:
                yield return StartCoroutine(VWaveBarrageAttack(endTime));
                break;
                
            case BulletHellAttack.SpiralVWaveCombo:
                yield return StartCoroutine(SpiralVWaveComboAttack(endTime));
                break;
        }
    }

    #endregion

    #region Spiral Barrage Attack

    private IEnumerator SpiralBarrageAttack(float endTime)
    {
        _spiralAngle = 0f;
        _spiralLastDirectionChange = Time.time;
        _spiralDirection = 1;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Direction change
            if (spiralDirectionChangeInterval > 0 && 
                Time.time - _spiralLastDirectionChange > spiralDirectionChangeInterval)
            {
                _spiralDirection *= -1;
                _spiralLastDirectionChange = Time.time;
            }
            
            // Update spiral angle
            _spiralAngle += spiralRotationSpeed * _spiralDirection * Time.deltaTime;
            
            // Fire bullets on timer
            if (Time.time >= _nextSpiralFireTime)
            {
                FireSpiralBullets(_spiralAngle);
                
                if (dualSpiral)
                {
                    FireSpiralBullets(_spiralAngle + 180f + dualSpiralOffset);
                }
                
                _nextSpiralFireTime = Time.time + (1f / spiralFireRate);
            }
            
            yield return null;
        }
    }

    private void FireSpiralBullets(float baseAngle)
    {
        float angleStep = 360f / spiralStreamCount;
        
        for (int i = 0; i < spiralStreamCount; i++)
        {
            float angle = baseAngle + (i * angleStep);
            Vector2 direction = AngleToDirection(angle);
            FireProjectile(direction);
        }
    }

    #endregion

    #region Converging Lasers Attack

    private IEnumerator ConvergingLasersAttack(float endTime)
    {
        // Use all turrets if available, otherwise fall back to calculated positions
        int laserCount = (turrets != null && turrets.Count > 0) ? turrets.Count : convergingLaserCount;
        _convergingBeams = new LaserBeam[laserCount];
        _laserTargetDirections = new Vector2[laserCount];
        
        Vector2 playerPos = _target.position;
        
        // Initialize lasers at turret positions, but aim offset from player
        for (int i = 0; i < laserCount; i++)
        {
            // Spawn at turret position on the boss ship
            Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
            
            // Calculate initial aim point: offset from player position
            // Each laser aims at a point in a ring around the player
            float angle = (360f / laserCount) * i;
            Vector2 offsetFromPlayer = AngleToDirection(angle) * laserInitialAimOffset;
            Vector2 initialAimPoint = playerPos + offsetFromPlayer;
            
            // Initial direction points toward the offset position (not directly at player)
            _laserTargetDirections[i] = (initialAimPoint - spawnPos).normalized;
            _convergingBeams[i] = SpawnLaserBeam(spawnPos, _laserTargetDirections[i]);
            
            if (_convergingBeams[i] != null)
            {
                _allActiveBeams.Add(_convergingBeams[i]);
            }
            else
            {
                Debug.LogWarning($"[BossBulletHell] Failed to spawn laser {i} - check beamWeapon.prefab is assigned!");
            }
        }
        
        // Warm-up phase
        yield return new WaitForSeconds(laserWarmupTime);
        
        _lasersActive = true;
        _laserStartTime = Time.time;
        _trackedPlayerPosition = _target.position;
        
        // Tracking phase - lasers at turret positions aggressively track player
        while (Time.time < endTime && !_isTransitioning)
        {
            // Update tracked position with reduced lag for aggressive tracking
            _trackedPlayerPosition = Vector2.Lerp(
                _trackedPlayerPosition, 
                _target.position, 
                (1f - laserConvergenceLag * 0.5f) * Time.deltaTime * 15f
            );
            
            // Update laser directions to converge on tracked position
            for (int i = 0; i < laserCount; i++)
            {
                if (_convergingBeams[i] == null) continue;
                
                // Lasers stay at turret positions on the boss
                Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
                Vector2 targetDir = (_trackedPlayerPosition - spawnPos).normalized;
                
                // Aggressive rotation toward target
                _laserTargetDirections[i] = Vector2.Lerp(
                    _laserTargetDirections[i],
                    targetDir,
                    laserTrackingSpeed * aggressiveTrackingMultiplier * Time.deltaTime / 90f
                ).normalized;
                
                // Update beam
                UpdateLaserBeam(_convergingBeams[i], spawnPos, _laserTargetDirections[i]);
            }
            
            yield return null;
        }
        
        _lasersActive = false;
        
        // Cleanup lasers
        for (int i = 0; i < _convergingBeams.Length; i++)
        {
            if (_convergingBeams[i] != null)
            {
                _allActiveBeams.Remove(_convergingBeams[i]);
                Destroy(_convergingBeams[i].gameObject);
                _convergingBeams[i] = null;
            }
        }
    }

    private Vector2 GetLaserSpawnPosition(int index, int total)
    {
        // Distribute lasers evenly around boss
        float angle = (360f / total) * index;
        Vector2 offset = AngleToDirection(angle) * laserSpawnOffset;
        return (Vector2)transform.position + offset;
    }

    /// <summary>
    /// Gets position from turret array if available, otherwise calculates position around boss.
    /// </summary>
    private Vector2 GetTurretOrCalculatedPosition(int index, int total)
    {
        // Use turret position if available
        if (turrets != null && index < turrets.Count && turrets[index] != null)
        {
            return turrets[index].transform.position;
        }
        
        // Fall back to calculated position
        return GetLaserSpawnPosition(index, total);
    }

    #endregion

    #region Radial Burst Attack

    private IEnumerator RadialBurstAttack(float endTime)
    {
        _radialBurstAngle = 0f;
        int wavesFired = 0;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Fire a burst wave
            FireRadialBurst(_radialBurstAngle);
            wavesFired++;
            
            // Offset angle for next wave
            _radialBurstAngle += radialBurstAngleOffset;
            
            // Check if we've completed a set of waves
            if (wavesFired >= radialBurstWaves)
            {
                wavesFired = 0;
                yield return new WaitForSeconds(radialBurstInterval * 2f);
            }
            else
            {
                yield return new WaitForSeconds(radialBurstInterval);
            }
        }
    }

    private void FireRadialBurst(float baseAngle)
    {
        float safeZoneSize = radialSafeZoneAngle;
        float safeZoneSpacing = 360f / radialSafeZoneCount;
        
        float angleStep = 360f / radialBurstCount;
        
        for (int i = 0; i < radialBurstCount; i++)
        {
            float angle = baseAngle + (i * angleStep);
            
            // Check if this angle is in a safe zone
            bool inSafeZone = false;
            for (int sz = 0; sz < radialSafeZoneCount; sz++)
            {
                float safeZoneCenter = sz * safeZoneSpacing;
                float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle, safeZoneCenter));
                if (angleDiff < safeZoneSize / 2f)
                {
                    inSafeZone = true;
                    break;
                }
            }
            
            if (!inSafeZone)
            {
                Vector2 direction = AngleToDirection(angle);
                FireProjectile(direction);
            }
        }
    }

    #endregion

    #region Laser Sweep Attack

    private IEnumerator LaserSweepAttack(float endTime)
    {
        // Use all turrets if available
        int laserCount = (turrets != null && turrets.Count > 0) ? turrets.Count : sweepLaserCount;
        _sweepBeams = new LaserBeam[laserCount];
        _sweepAngle = -sweepArc / 2f;
        _sweepDirection = 1;
        
        // Spawn sweep lasers at turret positions
        for (int i = 0; i < laserCount; i++)
        {
            float offsetAngle = (360f / laserCount) * i; // Distribute evenly around boss
            Vector2 direction = AngleToDirection(_sweepAngle + offsetAngle);
            Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
            
            _sweepBeams[i] = SpawnLaserBeam(spawnPos, direction);
            
            if (_sweepBeams[i] != null)
            {
                _allActiveBeams.Add(_sweepBeams[i]);
            }
        }
        
        // Warm-up
        yield return new WaitForSeconds(laserWarmupTime);
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Check for endpoint pause
            if (_sweepPauseTime > 0)
            {
                _sweepPauseTime -= Time.deltaTime;
                yield return null;
                continue;
            }
            
            // Update sweep angle
            _sweepAngle += sweepRotationSpeed * _sweepDirection * Time.deltaTime;
            
            // Check for sweep boundaries
            if (_sweepAngle >= sweepArc / 2f)
            {
                _sweepAngle = sweepArc / 2f;
                _sweepDirection = -1;
                _sweepPauseTime = sweepEndpointPause;
            }
            else if (_sweepAngle <= -sweepArc / 2f)
            {
                _sweepAngle = -sweepArc / 2f;
                _sweepDirection = 1;
                _sweepPauseTime = sweepEndpointPause;
            }
            
            // Update laser positions - lasers stay at turret positions but rotate
            for (int i = 0; i < laserCount; i++)
            {
                if (_sweepBeams[i] == null) continue;
                
                float offsetAngle = (360f / laserCount) * i;
                Vector2 direction = AngleToDirection(_sweepAngle + offsetAngle);
                Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
                
                UpdateLaserBeam(_sweepBeams[i], spawnPos, direction);
            }
            
            yield return null;
        }
        
        // Cleanup
        for (int i = 0; i < _sweepBeams.Length; i++)
        {
            if (_sweepBeams[i] != null)
            {
                _allActiveBeams.Remove(_sweepBeams[i]);
                Destroy(_sweepBeams[i].gameObject);
                _sweepBeams[i] = null;
            }
        }
    }

    #endregion

    #region Combined Assault Attack

    private IEnumerator CombinedAssaultAttack(float endTime)
    {
        // Start tracking lasers if enabled
        Coroutine laserCoroutine = null;
        if (combinedUseTrackingLasers && beamWeapon.prefab != null)
        {
            laserCoroutine = StartCoroutine(CombinedTrackingLasers(endTime));
        }
        
        // Fire bullet streams
        float fireInterval = (1f / spiralFireRate) / combinedBulletRateMultiplier;
        float streamAngle = 0f;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Rotate bullet streams
            streamAngle += spiralRotationSpeed * 0.5f * Time.deltaTime;
            
            // Fire from each stream with spread
            float streamStep = 360f / combinedBulletStreams;
            for (int i = 0; i < combinedBulletStreams; i++)
            {
                float baseAngle = streamAngle + (i * streamStep);
                
                // Fire with spread
                for (float spreadOffset = -combinedBulletSpread; spreadOffset <= combinedBulletSpread; spreadOffset += combinedBulletSpread)
                {
                    Vector2 direction = AngleToDirection(baseAngle + spreadOffset);
                    FireProjectile(direction);
                }
            }
            
            yield return new WaitForSeconds(fireInterval);
        }
        
        // Wait for laser coroutine if running
        if (laserCoroutine != null)
        {
            // Lasers will clean up themselves when endTime is reached
        }
    }

    private IEnumerator CombinedTrackingLasers(float endTime)
    {
        // Use all turrets if available
        int laserCount = (turrets != null && turrets.Count > 0) ? turrets.Count : 2;
        LaserBeam[] beams = new LaserBeam[laserCount];
        Vector2[] directions = new Vector2[laserCount];
        
        for (int i = 0; i < laserCount; i++)
        {
            float angle = (360f / laserCount) * i;
            directions[i] = AngleToDirection(angle);
            Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
            beams[i] = SpawnLaserBeam(spawnPos, directions[i]);
            
            if (beams[i] != null)
            {
                _allActiveBeams.Add(beams[i]);
            }
        }
        
        yield return new WaitForSeconds(laserWarmupTime);
        
        Vector2 trackedPos = _target.position;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            trackedPos = Vector2.Lerp(trackedPos, _target.position, 0.5f * Time.deltaTime * 10f);
            
            for (int i = 0; i < laserCount; i++)
            {
                if (beams[i] == null) continue;
                
                Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
                Vector2 targetDir = (trackedPos - spawnPos).normalized;
                
                directions[i] = Vector2.Lerp(directions[i], targetDir, laserTrackingSpeed * 0.5f * Time.deltaTime / 90f).normalized;
                
                UpdateLaserBeam(beams[i], spawnPos, directions[i]);
            }
            
            yield return null;
        }
        
        // Cleanup
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null)
            {
                _allActiveBeams.Remove(beams[i]);
                Destroy(beams[i].gameObject);
                beams[i] = null;
            }
        }
    }

    #endregion

    #region Crossfire Chaos Attack

    private void InitializeCrossfireData()
    {
        _crossfireSpiralAngles = new float[crossfireSpiralCount];
        _crossfireSpiralSpeeds = new float[crossfireSpiralCount];
        
        for (int i = 0; i < crossfireSpiralCount; i++)
        {
            _crossfireSpiralAngles[i] = (360f / crossfireSpiralCount) * i;
            _crossfireSpiralSpeeds[i] = spiralRotationSpeed * (1f + Random.Range(-crossfireSpeedVariation, crossfireSpeedVariation));
            
            // Alternate direction
            if (i % 2 == 1)
            {
                _crossfireSpiralSpeeds[i] *= -1f;
            }
        }
    }

    private IEnumerator CrossfireChaosAttack(float endTime)
    {
        InitializeCrossfireData();
        _nextCrossfireBurstTime = Time.time + (1f / crossfireRandomBurstRate);
        
        float fireInterval = 1f / (spiralFireRate * 0.7f);
        float nextFireTime = Time.time;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Update all spiral angles
            for (int i = 0; i < crossfireSpiralCount; i++)
            {
                _crossfireSpiralAngles[i] += _crossfireSpiralSpeeds[i] * Time.deltaTime;
            }
            
            // Fire from all spirals
            if (Time.time >= nextFireTime)
            {
                for (int spiral = 0; spiral < crossfireSpiralCount; spiral++)
                {
                    int streamsPerSpiral = Mathf.Max(2, spiralStreamCount / crossfireSpiralCount);
                    float angleStep = 360f / streamsPerSpiral;
                    
                    for (int stream = 0; stream < streamsPerSpiral; stream++)
                    {
                        float angle = _crossfireSpiralAngles[spiral] + (stream * angleStep);
                        Vector2 direction = AngleToDirection(angle);
                        FireProjectile(direction);
                    }
                }
                
                nextFireTime = Time.time + fireInterval;
            }
            
            // Random burst interjection
            if (Time.time >= _nextCrossfireBurstTime)
            {
                FireRandomBurst();
                _nextCrossfireBurstTime = Time.time + (1f / crossfireRandomBurstRate);
            }
            
            yield return null;
        }
    }

    private void FireRandomBurst()
    {
        float baseAngle = Random.Range(0f, 360f);
        float spreadStep = 360f / crossfireRandomBurstCount;
        
        for (int i = 0; i < crossfireRandomBurstCount; i++)
        {
            Vector2 direction = AngleToDirection(baseAngle + (i * spreadStep));
            FireProjectile(direction, bossProjectileSpeed * 1.5f); // Faster burst projectiles
        }
    }

    #endregion

    #region V-Wave Barrage Attack

    /// <summary>
    /// Fires walls of bullets in V-shapes that stack together, creating
    /// diagonal walls that are tricky to navigate through.
    /// </summary>
    private IEnumerator VWaveBarrageAttack(float endTime)
    {
        _vWaveAngle = 0f;
        _vWaveDirection = 1;
        float nextFireTime = Time.time;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Rotate the V-wave pattern
            _vWaveAngle += vWaveRotationSpeed * Time.deltaTime;
            
            if (Time.time >= nextFireTime)
            {
                // Fire stacked V-waves
                FireStackedVWaves(_vWaveAngle, _vWaveDirection);
                
                // Alternate direction for next volley
                if (vWaveAlternate)
                {
                    _vWaveDirection *= -1;
                }
                
                nextFireTime = Time.time + vWaveInterval;
            }
            
            yield return null;
        }
    }

    /// <summary>
    /// Fires multiple V shapes stacked together, creating a diagonal wall pattern.
    /// </summary>
    private void FireStackedVWaves(float baseAngle, int direction)
    {
        // Fire multiple V shapes stacked vertically
        for (int stack = 0; stack < vWaveStackCount; stack++)
        {
            // Calculate timing offset for this stack (creates staggered launch)
            float stackDelay = stack * vWaveStackSpacing;
            float speedMultiplier = 1f - (stack * 0.08f); // Slightly slower for outer stacks
            
            // Left arm of the V
            float leftArmAngle = baseAngle - (vWaveAngle * direction);
            // Right arm of the V
            float rightArmAngle = baseAngle + (vWaveAngle * direction);
            
            // Fire bullets along each arm
            for (int bullet = 0; bullet < vWaveBulletsPerArm; bullet++)
            {
                // Calculate position along the arm (0 = center, 1 = outer edge)
                float t = (float)bullet / (vWaveBulletsPerArm - 1);
                
                // Bullets spread outward along the V arms
                float leftAngle = Mathf.Lerp(baseAngle, leftArmAngle, t);
                float rightAngle = Mathf.Lerp(baseAngle, rightArmAngle, t);
                
                // Offset speed based on position to create diagonal wall effect
                float bulletSpeed = (bossProjectileSpeed * speedMultiplier) * (1f + t * 0.3f);
                
                Vector2 leftDir = AngleToDirection(leftAngle);
                Vector2 rightDir = AngleToDirection(rightAngle);
                
                FireProjectile(leftDir, bulletSpeed);
                FireProjectile(rightDir, bulletSpeed);
            }
        }
    }

    #endregion

    #region Spiral V-Wave Combo Attack

    /// <summary>
    /// Combines a spiral pattern with periodic V-wave bursts and optional lasers.
    /// Creates an intense, layered bullet hell experience.
    /// </summary>
    private IEnumerator SpiralVWaveComboAttack(float endTime)
    {
        // Start lasers if enabled
        Coroutine laserCoroutine = null;
        if (comboUseLasers && beamWeapon.prefab != null && turrets != null && turrets.Count > 0)
        {
            laserCoroutine = StartCoroutine(ComboTrackingLasers(endTime));
        }
        
        float spiralAngle = 0f;
        float vWaveTimer = 0f;
        float vWaveInterval = 360f / (spiralRotationSpeed * comboVWavesPerRotation);
        float fireInterval = 1f / (spiralFireRate * 0.8f);
        float nextFireTime = Time.time;
        int vWaveDir = 1;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            // Update spiral
            spiralAngle += spiralRotationSpeed * Time.deltaTime;
            vWaveTimer += Time.deltaTime;
            
            // Fire spiral bullets
            if (Time.time >= nextFireTime)
            {
                float angleStep = 360f / comboSpiralStreams;
                for (int i = 0; i < comboSpiralStreams; i++)
                {
                    float angle = spiralAngle + (i * angleStep);
                    Vector2 direction = AngleToDirection(angle);
                    FireProjectile(direction);
                }
                nextFireTime = Time.time + fireInterval;
            }
            
            // Fire V-wave bursts at intervals
            if (vWaveTimer >= vWaveInterval)
            {
                // Fire a V-wave aimed toward the player
                float toPlayerAngle = Mathf.Atan2(
                    _target.position.y - transform.position.y,
                    _target.position.x - transform.position.x
                ) * Mathf.Rad2Deg;
                
                FireStackedVWaves(toPlayerAngle, vWaveDir);
                vWaveDir *= -1;
                vWaveTimer = 0f;
            }
            
            yield return null;
        }
    }

    /// <summary>
    /// Tracking lasers for the combo attack - uses all turrets.
    /// </summary>
    private IEnumerator ComboTrackingLasers(float endTime)
    {
        int laserCount = turrets.Count;
        LaserBeam[] beams = new LaserBeam[laserCount];
        Vector2[] directions = new Vector2[laserCount];
        
        for (int i = 0; i < laserCount; i++)
        {
            float angle = (360f / laserCount) * i;
            directions[i] = AngleToDirection(angle);
            Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
            beams[i] = SpawnLaserBeam(spawnPos, directions[i]);
            
            if (beams[i] != null)
            {
                _allActiveBeams.Add(beams[i]);
            }
        }
        
        yield return new WaitForSeconds(laserWarmupTime);
        
        Vector2 trackedPos = _target.position;
        
        while (Time.time < endTime && !_isTransitioning)
        {
            trackedPos = Vector2.Lerp(trackedPos, _target.position, laserConvergenceLag * Time.deltaTime * 5f);
            
            for (int i = 0; i < laserCount; i++)
            {
                if (beams[i] == null) continue;
                
                Vector2 spawnPos = GetTurretOrCalculatedPosition(i, laserCount);
                Vector2 targetDir = (trackedPos - spawnPos).normalized;
                
                directions[i] = Vector2.Lerp(directions[i], targetDir, laserTrackingSpeed * Time.deltaTime / 90f).normalized;
                
                UpdateLaserBeam(beams[i], spawnPos, directions[i]);
            }
            
            yield return null;
        }
        
        // Cleanup
        for (int i = 0; i < beams.Length; i++)
        {
            if (beams[i] != null)
            {
                _allActiveBeams.Remove(beams[i]);
                Destroy(beams[i].gameObject);
                beams[i] = null;
            }
        }
    }

    #endregion

    #region Projectile Firing

    private void FireProjectile(Vector2 direction, float? speedOverride = null)
    {
        if (projectileWeapon.prefab == null) return;
        
        // Add spray angle - random deviation + time-based wobble
        float randomSpray = Random.Range(-bulletSprayAngle, bulletSprayAngle);
        float wobble = Mathf.Sin(Time.time * bulletWobbleSpeed) * bulletWobbleAngle;
        float totalSpray = randomSpray + wobble;
        
        // Rotate direction by spray angle
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float sprayedAngle = baseAngle + totalSpray;
        Vector2 sprayedDirection = AngleToDirection(sprayedAngle);
        
        Vector2 spawnPos = (Vector2)transform.position + sprayedDirection * 1f;
        float angle = sprayedAngle - 90f;
        
        GameObject projectile = Instantiate(
            projectileWeapon.prefab,
            spawnPos,
            Quaternion.Euler(0, 0, angle)
        );
        
        // Play fire sound (throttled to reduce audio spam)
        _shotCounter++;
        if (_shotCounter >= playSoundEveryNthShot)
        {
            _shotCounter = 0;
            AudioClip fireClip = bossFireSound != null ? bossFireSound : projectileFireSound;
            if (fireClip != null)
            {
                PlayOneShotSound(fireClip, bossFireVolume, AudioClipType.ProjectileFire);
            }
        }
        
        float speed = speedOverride ?? (bossProjectileSpeed + Random.Range(-projectileSpeedVariation, projectileSpeedVariation));
        
        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = "Player";
            projectileScript.Initialize(
                sprayedDirection,  // Use sprayed direction
                Vector2.zero,
                speed,
                bossProjectileDamage,
                bossProjectileLifetime,
                projectileWeapon.impactForce,
                this
            );
        }
        
        // Reduced recoil
        if (reduceRecoil)
        {
            ApplyRecoil(projectileWeapon.recoilForce * recoilMultiplier);
        }
    }

    #endregion

    #region Laser Management

    private LaserBeam SpawnLaserBeam(Vector2 position, Vector2 direction)
    {
        if (beamWeapon.prefab == null) return null;
        
        // LaserBeam fires in transform.up direction, so we need -90 degree offset
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        GameObject beamObj = Instantiate(beamWeapon.prefab, position, Quaternion.Euler(0, 0, angle));
        
        LaserBeam beam = beamObj.GetComponent<LaserBeam>();
        if (beam != null)
        {
            beam.Initialize(
                "Player",
                beamWeapon.damagePerSecond,
                beamWeapon.maxDistance,
                beamWeapon.recoilForcePerSecond,
                beamWeapon.impactForce,
                this
            );
            beam.StartFiring();
            
            // Start beam audio if this is the first active beam
            if (_allActiveBeams.Count == 0)
            {
                StartBeamAudio();
            }
        }
        
        return beam;
    }

    private void UpdateLaserBeam(LaserBeam beam, Vector2 position, Vector2 direction)
    {
        if (beam == null) return;
        
        beam.transform.position = position;
        // LaserBeam fires in transform.up direction, so we need -90 degree offset
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        beam.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void UpdateActiveBeams()
    {
        // Apply recoil from active beams
        foreach (var beam in _allActiveBeams)
        {
            if (beam != null && reduceRecoil)
            {
                ApplyRecoil(beamWeapon.recoilForcePerSecond * recoilMultiplier * Time.fixedDeltaTime);
            }
        }
    }

    private void CleanupAllBeams()
    {
        foreach (var beam in _allActiveBeams)
        {
            if (beam != null)
            {
                Destroy(beam.gameObject);
            }
        }
        _allActiveBeams.Clear();
        
        _convergingBeams = null;
        _sweepBeams = null;
        
        // Stop beam audio
        StopBeamAudio();
    }

    #endregion

    #region Movement

    private void HandleBossMovement()
    {
        if (_target == null) return;
        
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        Vector2 directionToPlayer = ((Vector2)_target.position - (Vector2)transform.position).normalized;
        
        // Boss always moves aggressively - reduced multiplier penalty during attacks
        float moveMultiplier = _isAttacking ? Mathf.Max(attackMovementMultiplier, 0.6f) : 1f;
        
        // Always chase if beyond preferred distance
        if (distanceToPlayer > preferredCombatDistance)
        {
            // Chase player - stronger force the further away
            float chaseIntensity = Mathf.Clamp01((distanceToPlayer - preferredCombatDistance) / (maxDistanceFromPlayer - preferredCombatDistance));
            float forceMultiplier = Mathf.Lerp(0.7f, 1.2f, chaseIntensity);
            
            _rb.AddForce(directionToPlayer * thrustForce * moveMultiplier * forceMultiplier);
            _isThrusting = true;
        }
        else if (driftWhileAttacking && _isAttacking)
        {
            // In range and attacking - menacing drift with constant pressure toward player
            Vector2 driftWithPressure = (_driftDirection * 0.5f + directionToPlayer * 0.5f).normalized;
            _rb.AddForce(driftWithPressure * thrustForce * moveMultiplier * 0.5f);
            _isThrusting = true;
        }
        else
        {
            // In range, not attacking - still drift toward player
            _rb.AddForce(directionToPlayer * thrustForce * 0.3f);
            _isThrusting = false;
        }
    }

    protected override void RotateTowardTarget()
    {
        // Boss doesn't need to aim at player - it fires in all directions
        // Slow rotation to face general direction of player
        if (_target == null) return;
        
        Vector2 directionToTarget = (_target.position - transform.position).normalized;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg + ROTATION_OFFSET;
        
        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * 0.2f * Time.deltaTime);
        
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    #endregion

    #region Damage Override

    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        // Note: Reflected damage reduction is applied in TakeDamageFromProjectile
        
        // Apply vulnerability multiplier
        if (_isVulnerable)
        {
            damage *= vulnerabilityDamageMultiplier;
        }
        
        // Ignore damage during phase transition
        if (_isTransitioning) return;
        
        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    /// <summary>
    /// Called by ProjectileScript to apply damage. Reduces reflected projectile damage.
    /// </summary>
    public void TakeDamageFromProjectile(float damage, float impactForce, Vector3 hitPoint, bool isReflected)
    {
        // Apply reflected damage reduction
        if (isReflected)
        {
            damage *= reflectedDamageMultiplier;
        }
        
        TakeDamage(damage, impactForce, hitPoint, DamageSource.Projectile);
    }

    #endregion

    #region Utility

    private Vector2 AngleToDirection(float angleDegrees)
    {
        float angleRad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }

    // Override to prevent default enemy firing behavior
    protected override void TryFire()
    {
        // Boss uses its own attack system, not the base enemy firing
    }

    protected override void Fire()
    {
        // Boss uses its own attack system
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Draw preferred combat range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredCombatDistance);
        
        // Draw max chase distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDistanceFromPlayer);
        
        // Draw laser spawn offset
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, laserSpawnOffset);
        
        // Draw spiral streams preview
        if (_currentAttack == BulletHellAttack.SpiralBarrage || !Application.isPlaying)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            float angleStep = 360f / spiralStreamCount;
            for (int i = 0; i < spiralStreamCount; i++)
            {
                float angle = i * angleStep;
                Vector3 dir = AngleToDirection(angle);
                Gizmos.DrawRay(transform.position, dir * 5f);
            }
        }
    }

    #endregion
}
