using UnityEngine;

public enum FiringPattern
{
    RotatingBarrage,
    AlternatingPairs,
    AllAtOnceSalvo,
    LaserSpin
}

/// <summary>
/// Bullet hell enemy that maintains distance and fires continuous projectile patterns from 6 turrets
/// Randomly selects one of 4 firing patterns at spawn
/// </summary>
public class BulletHellEnemyScript : EnemyScript
{
    [Header("Bullet Hell Settings")]
    [Tooltip("Minimum distance to keep from player")]
    public float keepAwayDistance = 8f;

    [Tooltip("Optimal combat range from player")]
    public float optimalRange = 12f;

    [Tooltip("Distance buffer for range management")]
    public float rangeBuffer = 2f;

    [Tooltip("If true, recoil force is significantly reduced")]
    public bool reduceRecoil = true;

    [Tooltip("Recoil reduction multiplier (0.1 = 90% reduction)")]
    public float recoilMultiplier = 0.1f;

    [Tooltip("Number of volleys to fire before switching to a new pattern")]
    public int volleysPerPattern = 5;

    [Header("Pattern-Specific Settings")]
    [Tooltip("Rotating Barrage: Delay between each turret fire (seconds)")]
    public float rotatingBarrageDelay = 0.05f;

    [Tooltip("Rotating Barrage: Shots per second (overrides base fireRate)")]
    public float rotatingBarrageFireRate = 20f;

    [Tooltip("Alternating Pairs: Shots per second (overrides base fireRate)")]
    public float alternatingPairsFireRate = 5f;

    [Tooltip("All-at-Once Salvo: Shots per second (overrides base fireRate)")]
    public float allAtOnceSalvoFireRate = 3f;

    [Tooltip("Laser Spin: Duration in seconds to fire lasers")]
    public float laserSpinDuration = 3f;

    [Tooltip("Laser Spin: Turret offset distance to prevent self-collision")]
    public float laserTurretOffset = 1f;

    [Header("Spin Settings")]
    [Tooltip("Rotation speed in degrees per second while spinning")]
    public float spinRotationSpeed = 360f;

    [Tooltip("Should the ship spin during Rotating Barrage pattern?")]
    public bool spinDuringRotatingBarrage = true;

    [Tooltip("Should the ship spin during Laser Spin pattern?")]
    public bool spinDuringLaserSpin = true;

    [Header("Debug Settings")]
    [Tooltip("Enable to lock to a specific pattern for testing")]
    public bool debugLockPattern = false;

    [Tooltip("Pattern to use when debugLockPattern is enabled")]
    public FiringPattern debugPattern = FiringPattern.RotatingBarrage;

    private FiringPattern _currentFiringPattern;
    private int _rotatingBarrageIndex = 0;
    private bool _isEvenCycle = true;
    private float _nextRotatingFireTime = 0f;
    private int _volleyCount = 0;
    private LaserBeam[] _activeBeams;
    private float _laserSpinStartTime = 0f;
    private bool _isSpinning = false;

    protected override void Start()
    {
        base.Start();

        // Check if debug mode is enabled
        if (debugLockPattern)
        {
            _currentFiringPattern = debugPattern;
            // Debug.Log($"[{gameObject.name}] DEBUG MODE: Locked to pattern: {_currentFiringPattern}");
        }
        else
        {
            // Start with a random pattern
            _currentFiringPattern = (FiringPattern)Random.Range(0, 4);
            // Debug.Log($"[{gameObject.name}] Starting with firing pattern: {_currentFiringPattern}");
        }

        // Initialize laser beam array
        if (turrets != null)
        {
            _activeBeams = new LaserBeam[turrets.Count];
        }

        // Set initial spinning state based on starting pattern
        if (_currentFiringPattern == FiringPattern.RotatingBarrage)
        {
            _isSpinning = spinDuringRotatingBarrage;
        }
        else if (_currentFiringPattern == FiringPattern.LaserSpin)
        {
            _isSpinning = spinDuringLaserSpin;
        }
    }

    private void SwitchToNextPattern()
    {
        // Don't switch if debug lock is enabled
        if (debugLockPattern)
        {
            _volleyCount = 0; // Reset volley count but keep same pattern
            return;
        }

        // Clean up any active beams when switching patterns
        StopAllBeams();

        // Stop spinning when switching patterns
        _isSpinning = false;

        // Cycle to next pattern
        _currentFiringPattern = (FiringPattern)(((int)_currentFiringPattern + 1) % 4);
        _volleyCount = 0;

        // Set spinning state for new pattern
        if (_currentFiringPattern == FiringPattern.RotatingBarrage)
        {
            _isSpinning = spinDuringRotatingBarrage;
        }
        else if (_currentFiringPattern == FiringPattern.LaserSpin)
        {
            _isSpinning = spinDuringLaserSpin;
        }

        // Debug.Log($"[{gameObject.name}] Switched to firing pattern: {_currentFiringPattern}");
    }

    protected override void MovePursuit()
    {
        if (_target == null) return;

        // Don't move during spinning patterns - freeze position and only spin
        if (_isSpinning)
        {
            _isThrusting = false;
            // Stop all movement - freeze position
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            return; // Spinning handled in RotateTowardTarget()
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        Vector2 directionToTarget = (_target.position - transform.position).normalized;

        // Kiting behavior: maintain optimal range
        if (distanceToPlayer < keepAwayDistance)
        {
            // Too close - move away from player
            _rb.AddForce(-directionToTarget * thrustForce);
            _isThrusting = true;
        }
        else if (distanceToPlayer > optimalRange + rangeBuffer)
        {
            // Too far - move closer
            _rb.AddForce(directionToTarget * thrustForce);
            _isThrusting = true;
        }
        else
        {
            // In optimal range, don't thrust (just drift/maintain position)
            _isThrusting = false;
        }
    }

    private void SpinShip()
    {
        // Rotate the ship continuously
        float currentAngle = transform.eulerAngles.z;
        float newAngle = currentAngle + (spinRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected override void RotateTowardTarget()
    {
        // If spinning, don't rotate toward target - just spin
        if (_isSpinning)
        {
            SpinShip();
            return;
        }

        // Otherwise use default rotation behavior (though we don't aim anyway)
        base.RotateTowardTarget();
    }

    protected override void TryFire()
    {
        // Bullet hell enemy fires in all directions - no aim check needed for any pattern
        switch (_currentFiringPattern)
        {
            case FiringPattern.RotatingBarrage:
                if (projectileWeapon.prefab == null)
                    return;
                // Debug.Log($"[{gameObject.name}] Using Pattern: Rotating Barrage (Volley {_volleyCount}/{volleysPerPattern})");
                TryFireRotatingBarrage();
                break;

            case FiringPattern.AlternatingPairs:
                if (projectileWeapon.prefab == null)
                    return;
                // Debug.Log($"[{gameObject.name}] Using Pattern: Alternating Pairs (Volley {_volleyCount}/{volleysPerPattern})");
                TryFireAlternatingPairs();
                break;

            case FiringPattern.AllAtOnceSalvo:
                if (projectileWeapon.prefab == null)
                    return;
                // Debug.Log($"[{gameObject.name}] Using Pattern: All-at-Once Salvo (Volley {_volleyCount}/{volleysPerPattern})");
                TryFireAllAtOnce();
                break;

            case FiringPattern.LaserSpin:
                // Debug.Log($"[{gameObject.name}] Using Pattern: Laser Spin");
                TryFireLaserSpin();
                break;
        }
    }

    private void TryFireRotatingBarrage()
    {
        if (Time.time < _nextRotatingFireTime || turrets == null || turrets.Count == 0)
            return;

        // Fire single turret in sequence - fires outward from turret position
        FireSingleTurretOutward(_rotatingBarrageIndex);

        // Move to next turret (wrap around)
        _rotatingBarrageIndex = (_rotatingBarrageIndex + 1) % turrets.Count;

        // If we completed a full rotation, count as one volley
        if (_rotatingBarrageIndex == 0)
        {
            _volleyCount++;
            if (_volleyCount >= volleysPerPattern)
            {
                SwitchToNextPattern();
            }
        }

        // Set next fire time (creates rapid sequential firing)
        _nextRotatingFireTime = Time.time + rotatingBarrageDelay;
    }

    private void TryFireAlternatingPairs()
    {
        if (Time.time < _nextFireTime || turrets == null || turrets.Count == 0)
            return;

        // Fire even or odd indexed turrets outward
        for (int i = 0; i < turrets.Count; i++)
        {
            // Even cycle: 0,2,4 | Odd cycle: 1,3,5
            if (_isEvenCycle && i % 2 == 0)
                FireTurretOutward(i);
            else if (!_isEvenCycle && i % 2 == 1)
                FireTurretOutward(i);
        }

        // Toggle cycle
        _isEvenCycle = !_isEvenCycle;
        _nextFireTime = Time.time + (1f / alternatingPairsFireRate);

        ApplyPatternRecoil(3); // 3 turrets fired

        // Play projectile fire sound with layering for more impact (3 turrets = 2 layered sounds)
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);
        PlayOneShotSound(projectileFireSound, 0.7f, AudioClipType.ProjectileFire);

        // Count volleys (2 fires = 1 complete volley)
        if (_isEvenCycle)
        {
            _volleyCount++;
            if (_volleyCount >= volleysPerPattern)
            {
                SwitchToNextPattern();
            }
        }
    }

    private void TryFireAllAtOnce()
    {
        if (Time.time < _nextFireTime || turrets == null || turrets.Count == 0)
            return;

        // Fire all turrets simultaneously outward
        for (int i = 0; i < turrets.Count; i++)
        {
            FireTurretOutward(i);
        }

        _nextFireTime = Time.time + (1f / allAtOnceSalvoFireRate);
        ApplyPatternRecoil(turrets.Count);

        // Play projectile fire sound with triple layering for massive impact (6 turrets = 3 layered sounds)
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);
        PlayOneShotSound(projectileFireSound, 0.8f, AudioClipType.ProjectileFire);
        PlayOneShotSound(projectileFireSound, 0.6f, AudioClipType.ProjectileFire);

        // Count volleys
        _volleyCount++;
        if (_volleyCount >= volleysPerPattern)
        {
            SwitchToNextPattern();
        }
    }

    private void TryFireLaserSpin()
    {
        if (beamWeapon.prefab == null || turrets == null || turrets.Count == 0)
            return;

        // Start laser beams if not already active
        if (_activeBeams[0] == null)
        {
            StartAllBeams();
            _laserSpinStartTime = Time.time;
        }

        // Check if duration has elapsed
        if (Time.time - _laserSpinStartTime >= laserSpinDuration)
        {
            StopAllBeams();
            SwitchToNextPattern();
        }
    }

    private void FireSingleTurretOutward(int turretIndex)
    {
        if (turretIndex < 0 || turretIndex >= turrets.Count)
            return;

        FireTurretOutward(turretIndex);
        ApplyPatternRecoil(1);

        // Play projectile fire sound (single shot)
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);
    }

    private void FireTurretOutward(int turretIndex)
    {
        // Calculate direction from ship center to turret (outward direction)
        Vector2 outwardDirection = (turrets[turretIndex].transform.position - transform.position).normalized;
        FireProjectileFromTurret(turretIndex, outwardDirection);
    }

    private void FireProjectileFromTurret(int turretIndex, Vector2 direction)
    {
        // Calculate rotation based on fire direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion projectileRotation = Quaternion.Euler(0, 0, angle + ROTATION_OFFSET);

        GameObject projectile = Instantiate(
            projectileWeapon.prefab,
            turrets[turretIndex].transform.position,
            projectileRotation
        );

        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = "Player";
            projectileScript.Initialize(
                direction,
                Vector2.zero,
                projectileWeapon.speed,
                projectileWeapon.damage,
                projectileWeapon.lifetime,
                projectileWeapon.impactForce,
                this
            );
        }
    }

    private void ApplyPatternRecoil(int turretsFired)
    {
        if (reduceRecoil)
        {
            // Scale down recoil significantly for bullet hell
            ApplyRecoil(projectileWeapon.recoilForce * turretsFired * recoilMultiplier);
        }
        else
        {
            ApplyRecoil(projectileWeapon.recoilForce * turretsFired);
        }
    }

    private void StartAllBeams()
    {
        if (turrets == null || beamWeapon.prefab == null)
            return;

        for (int i = 0; i < turrets.Count; i++)
        {
            if (_activeBeams[i] != null)
                continue; // Already has a beam

            // Calculate outward direction from turret
            Vector2 outwardDirection = (turrets[i].transform.position - transform.position).normalized;

            // Calculate rotation to point outward
            float angle = Mathf.Atan2(outwardDirection.y, outwardDirection.x) * Mathf.Rad2Deg;
            Quaternion beamRotation = Quaternion.Euler(0, 0, angle + ROTATION_OFFSET);

            // Spawn beam at turret position with offset to prevent self-collision
            Vector3 spawnPosition = turrets[i].transform.position + (Vector3)outwardDirection * laserTurretOffset;

            GameObject beamObj = Instantiate(beamWeapon.prefab, spawnPosition, beamRotation, turrets[i].transform);
            _activeBeams[i] = beamObj.GetComponent<LaserBeam>();

            if (_activeBeams[i] != null)
            {
                _activeBeams[i].Initialize(
                    "Player",
                    beamWeapon.damagePerSecond,
                    beamWeapon.maxDistance,
                    beamWeapon.recoilForcePerSecond,
                    beamWeapon.impactForce,
                    this
                );
                _activeBeams[i].StartFiring();
            }
        }

        // Start looping beam audio (once for all beams starting)
        StartBeamAudio();
    }

    private void StopAllBeams()
    {
        if (_activeBeams == null)
            return;

        for (int i = 0; i < _activeBeams.Length; i++)
        {
            if (_activeBeams[i] != null)
            {
                _activeBeams[i].StopFiring();
                Destroy(_activeBeams[i].gameObject);
                _activeBeams[i] = null;
            }
        }

        // Stop looping beam audio
        StopBeamAudio();
    }

    protected override void FixedUpdate()
    {
        // If spinning, lock position and skip all physics
        if (_isSpinning)
        {
            // Completely freeze position
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            // Skip base.FixedUpdate() to prevent any movement
            return;
        }

        base.FixedUpdate();

        // Apply recoil from active beams (only when not spinning)
        if (_activeBeams != null)
        {
            for (int i = 0; i < _activeBeams.Length; i++)
            {
                if (_activeBeams[i] != null)
                {
                    float recoilForceThisFrame = _activeBeams[i].GetRecoilForcePerSecond() * Time.fixedDeltaTime;
                    ApplyRecoil(recoilForceThisFrame);
                }
            }
        }
    }

    void OnDestroy()
    {
        // Clean up beams when enemy is destroyed
        StopAllBeams();
    }

    // Visualize ranges in editor
    void OnDrawGizmosSelected()
    {
        // Keep away distance (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, keepAwayDistance);

        // Optimal range (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        // Optimal range + buffer (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, optimalRange + rangeBuffer);
    }
}
