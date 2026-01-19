using UnityEngine;
using Unity.Cinemachine;

public enum EnemyState
{
    Patrolling,
    Pursuing,
    Searching
}

public abstract class EnemyScript : ShipBase
{
    [Header("Enemy Movement Settings")]
    public bool useLateralDamping = true;
    public float preferredDistance = 5f;

    [Header("Enemy Detection Settings")]
    public float detectionRange = 10f;
    public float loseTargetRange = 12f;

    [Header("Enemy Combat Settings")]
    public float fireRate = 1f;
    public bool useLeadTargeting = false;
    public float leadTargetingAccuracy = 1f;
    [Tooltip("Angle threshold (degrees) enemy must be aimed at target before firing")]
    public float aimTolerance = 5f;

    [Header("Death Settings")]
    public float deathShakeForceMultiplier = .1f;

    [Header("Sound Effects")]
    [Tooltip("Projectile fire sound")]
    public AudioClip projectileFireSound;
    [Tooltip("Laser beam fire sound")]
    public AudioClip beamFireSound;
    [Tooltip("Sound when shield hit by projectile")]
    public AudioClip projectileShieldHitSound;
    [Tooltip("Sound when hull hit by projectile (no shield)")]
    public AudioClip projectileHullHitSound;
    [Tooltip("Sound when hit by laser beam (loops while being hit)")]
    public AudioClip beamHitSound;
    [Tooltip("Explosion sound on death")]
    public AudioClip explosionSound;
    [Tooltip("Time window to detect continuous beam hits (seconds)")]
    public float beamHitDetectionWindow = 0.2f;

    [System.Serializable]
    public struct AudioVolumeConfig
    {
        [Range(0f, 3f)]
        [Tooltip("Volume for projectile fire sound")]
        public float projectileFireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for beam fire sound")]
        public float beamFireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for projectile shield hit sound")]
        public float projectileShieldHitVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for projectile hull hit sound")]
        public float projectileHullHitVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for beam hit sound")]
        public float beamHitVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for explosion sound")]
        public float explosionVolume;
    }

    [Header("Audio Volume Controls")]
    public AudioVolumeConfig audioVolume = new AudioVolumeConfig
    {
        projectileFireVolume = 0.7f,
        beamFireVolume = 0.7f,
        projectileShieldHitVolume = 0.6f,
        projectileHullHitVolume = 0.7f,
        beamHitVolume = 0.6f,
        explosionVolume = 1f
    };

    protected Transform _target;
    protected float _nextFireTime;
    protected EnemyState _currentState = EnemyState.Patrolling;
    protected CinemachineImpulseSource _impulseSource;

    // Audio system
    protected AudioSource[] _audioSourcePool;
    protected int _audioSourcePoolSize = 5;
    protected AudioSource _beamAudioSource; // Dedicated looping audio source for laser beams
    protected AudioSource _beamHitLoopSource; // Dedicated looping audio source for being hit by beams
    protected float _lastBeamHitTime; // Track when last hit by beam

    protected Vector3 _lastKnownPlayerPosition;
    protected float _searchStartTime;
    protected float _searchDuration = 2f;
    
    protected Vector2 _wanderDirection;
    protected float _wanderChangeTime;
    protected float _wanderChangeInterval = 2.5f;

    protected override void Awake()
    {
        base.Awake();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        InitializeAudioSystem();
    }

    protected virtual void InitializeAudioSystem()
    {
        // Create audio source pool for one-shot sounds
        _audioSourcePool = new AudioSource[_audioSourcePoolSize];
        for (int i = 0; i < _audioSourcePoolSize; i++)
        {
            _audioSourcePool[i] = gameObject.AddComponent<AudioSource>();
            _audioSourcePool[i].playOnAwake = false;
            _audioSourcePool[i].spatialBlend = 1f; // Full 3D sound
            _audioSourcePool[i].rolloffMode = AudioRolloffMode.Linear;
            _audioSourcePool[i].minDistance = 10f; // Full volume within this distance
            _audioSourcePool[i].maxDistance = 50f; // Silent beyond this distance
            _audioSourcePool[i].dopplerLevel = 0f; // Disable doppler effect
        }

        // Create dedicated audio source for looping laser beam sound
        _beamAudioSource = gameObject.AddComponent<AudioSource>();
        _beamAudioSource.playOnAwake = false;
        _beamAudioSource.loop = true;
        _beamAudioSource.spatialBlend = 1f; // Full 3D sound
        _beamAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _beamAudioSource.minDistance = 10f;
        _beamAudioSource.maxDistance = 50f;
        _beamAudioSource.dopplerLevel = 0f;

        // Create dedicated audio source for looping beam hit sound
        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 1f; // Full 3D sound
        _beamHitLoopSource.rolloffMode = AudioRolloffMode.Linear;
        _beamHitLoopSource.minDistance = 10f;
        _beamHitLoopSource.maxDistance = 50f;
        _beamHitLoopSource.dopplerLevel = 0f;
    }

    protected AudioSource GetAvailableAudioSource()
    {
        // Find first non-playing audio source
        foreach (var source in _audioSourcePool)
        {
            if (!source.isPlaying)
            {
                // Clear any lingering clip data to prevent timing issues
                source.clip = null;
                return source;
            }
        }
        // If all busy, reuse the first one and stop it first
        _audioSourcePool[0].Stop();
        _audioSourcePool[0].clip = null;
        return _audioSourcePool[0];
    }

    protected enum AudioClipType
    {
        ProjectileFire,
        BeamFire,
        Explosion
    }

    protected void PlayOneShotSound(AudioClip clip, float volume = 1f, AudioClipType clipType = AudioClipType.ProjectileFire)
    {
        if (clip == null) return;

        float volumeMultiplier = clipType switch
        {
            AudioClipType.ProjectileFire => audioVolume.projectileFireVolume,
            AudioClipType.BeamFire => audioVolume.beamFireVolume,
            AudioClipType.Explosion => audioVolume.explosionVolume,
            _ => 1f
        };

        AudioSource source = GetAvailableAudioSource();
        source.PlayOneShot(clip, volume * volumeMultiplier);
    }

    /// <summary>
    /// Start looping laser beam sound. Call this when beam starts firing.
    /// </summary>
    protected void StartBeamAudio()
    {
        if (beamFireSound == null || _beamAudioSource == null) return;

        // Only start if not already playing
        if (!_beamAudioSource.isPlaying)
        {
            _beamAudioSource.clip = beamFireSound;
            _beamAudioSource.volume = audioVolume.beamFireVolume;
            _beamAudioSource.Play();
        }
    }

    /// <summary>
    /// Stop looping laser beam sound. Call this when beam stops firing.
    /// </summary>
    protected void StopBeamAudio()
    {
        if (_beamAudioSource != null && _beamAudioSource.isPlaying)
        {
            _beamAudioSource.Stop();
        }
    }

    /// <summary>
    /// Play sound when enemy is hit. Automatically selects clip based on damage source and shield state.
    /// </summary>
    protected void PlayHitSound(DamageSource damageSource)
    {
        switch (damageSource)
        {
            case DamageSource.Projectile:
                // Check if shield is active and has health remaining (shield is inherited from ShipBase)
                bool shieldActive = hasShield && currentShield > 0f;

                AudioClip projectileClip;
                float projectileVolume;

                if (shieldActive)
                {
                    projectileClip = projectileShieldHitSound;
                    projectileVolume = audioVolume.projectileShieldHitVolume;
                }
                else
                {
                    projectileClip = projectileHullHitSound;
                    projectileVolume = audioVolume.projectileHullHitVolume;
                }

                if (projectileClip != null)
                {
                    AudioSource source = GetAvailableAudioSource();
                    source.PlayOneShot(projectileClip, projectileVolume);
                }
                break;

            case DamageSource.LaserBeam:
                // Update last beam hit time
                _lastBeamHitTime = Time.time;

                // Start looping beam hit sound if not already playing
                if (beamHitSound != null && _beamHitLoopSource != null && !_beamHitLoopSource.isPlaying)
                {
                    _beamHitLoopSource.clip = beamHitSound;
                    _beamHitLoopSource.volume = audioVolume.beamHitVolume;
                    _beamHitLoopSource.Play();
                }
                break;
        }
    }

    protected virtual void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _target = player.transform;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No player found with 'Player' tag!");
        }
        
        InitializeWanderDirection();
    }

    protected override void Update()
    {
        base.Update();

        // Check if beam hit sound should stop playing
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            float timeSinceLastBeamHit = Time.time - _lastBeamHitTime;
            if (timeSinceLastBeamHit > beamHitDetectionWindow)
            {
                _beamHitLoopSource.Stop();
            }
        }

        if (_target == null) return;

        UpdateEnemyState();

        if (_currentState == EnemyState.Patrolling)
        {
            RotateTowardTarget();
        }
        else if (_currentState == EnemyState.Pursuing || _currentState == EnemyState.Searching)
        {
            RotateTowardTarget();
            TryFire();
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (_target == null) return;

        Move();

        if (useLateralDamping)
        {
            ApplyLateralDamping();
        }

        ClampVelocity();
    }

    protected virtual void Move()
    {
        _isThrusting = false; // Default to false, movement methods set to true

        switch (_currentState)
        {
            case EnemyState.Patrolling:
                MovePatrol();
                break;
            case EnemyState.Pursuing:
                MovePursuit();
                break;
            case EnemyState.Searching:
                MoveSearch();
                break;
        }
    }

    protected virtual void MovePatrol()
    {
        UpdateWanderDirection();
        _rb.AddForce(_wanderDirection * thrustForce * 0.7f);
        _isThrusting = true;
    }

    protected virtual void MovePursuit()
    {
        Vector2 directionToTarget = (_target.position - transform.position).normalized;
        _rb.AddForce(directionToTarget * thrustForce);
        _isThrusting = true;
    }

    protected virtual void MoveSearch()
    {
        Vector2 directionToLastKnown = (_lastKnownPlayerPosition - transform.position).normalized;
        float distanceToLastKnown = Vector2.Distance(transform.position, _lastKnownPlayerPosition);

        if (distanceToLastKnown > 1f)
        {
            _rb.AddForce(directionToLastKnown * thrustForce);
            _isThrusting = true;
        }
        else
        {
            UpdateWanderDirection();
            _rb.AddForce(_wanderDirection * thrustForce * 0.5f);
            _isThrusting = true;
        }
    }

    protected virtual void RotateTowardTarget()
    {
        Vector2 direction;

        if (_currentState == EnemyState.Patrolling)
        {
            direction = _wanderDirection;
        }
        else if (_currentState == EnemyState.Pursuing)
        {
            direction = _target.position - transform.position;
        }
        else if (_currentState == EnemyState.Searching)
        {
            direction = _lastKnownPlayerPosition - transform.position;
        }
        else
        {
            return;
        }

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected virtual bool IsAimedAtTarget()
    {
        if (_target == null) return false;

        // Calculate angle to target (using same logic as Fire method for consistency)
        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 directionToTarget = (targetPosition - transform.position).normalized;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        // Get current ship facing angle
        float currentAngle = transform.eulerAngles.z;

        // Calculate smallest angular difference (accounting for ROTATION_OFFSET)
        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle + ROTATION_OFFSET);

        // Return true if angle difference is within tolerance
        return Mathf.Abs(angleDifference) <= aimTolerance;
    }

    protected virtual void TryFire()
    {
        if (Time.time >= _nextFireTime && projectileWeapon.prefab != null)
        {
            // Only fire if aimed at target within tolerance
            if (IsAimedAtTarget())
            {
                Fire();
                _nextFireTime = Time.time + (1f / fireRate);
            }
        }
    }

    protected virtual void Fire()
    {
        // Check if projectile weapon is configured
        if (projectileWeapon.prefab == null)
            return;

        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 fireDirection = (targetPosition - transform.position).normalized;

        // Use turrets if available, otherwise fire from front of ship
        if (turrets != null && turrets.Count > 0)
        {
            // Fire from each turret
            foreach (var turret in turrets)
            {
                GameObject projectile = Instantiate(projectileWeapon.prefab, turret.transform.position, transform.rotation);

                if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
                {
                    projectileScript.targetTag = "Player";
                    projectileScript.Initialize(
                        fireDirection,
                        Vector2.zero,
                        projectileWeapon.speed,
                        projectileWeapon.damage,
                        projectileWeapon.lifetime,
                        projectileWeapon.impactForce,
                        this
                    );
                }
            }
        }
        else
        {
            // No turrets - fire from front of ship (using transform.up like laser beam)
            Vector3 spawnPosition = transform.position + transform.up * 0.5f;
            GameObject projectile = Instantiate(projectileWeapon.prefab, spawnPosition, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = "Player";
                projectileScript.Initialize(
                    fireDirection,
                    Vector2.zero,
                    projectileWeapon.speed,
                    projectileWeapon.damage,
                    projectileWeapon.lifetime,
                    projectileWeapon.impactForce,
                    this
                );
            }
        }

        ApplyRecoil(projectileWeapon.recoilForce);

        // Play projectile fire sound
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);
    }

    protected virtual Vector3 CalculateTargetPosition()
    {
        Vector3 targetPosition;
        
        if (_currentState == EnemyState.Pursuing)
        {
            targetPosition = _target.position;
            
            if (useLeadTargeting && _target != null)
            {
                Rigidbody2D targetRb = _target.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 toTarget = _target.position - transform.position;
                    float distance = toTarget.magnitude;
                    float timeToTarget = distance / projectileWeapon.speed;

                    Vector2 predictedPosition = (Vector2)_target.position + (leadTargetingAccuracy * timeToTarget * targetRb.linearVelocity);
                    targetPosition = predictedPosition;
                }
            }
        }
        else if (_currentState == EnemyState.Searching)
        {
            targetPosition = _lastKnownPlayerPosition;
        }
        else
        {
            targetPosition = transform.position;
        }

        return targetPosition;
    }

    protected virtual bool IsPlayerInRange()
    {
        if (_target == null) return false;
        
        float distanceToPlayer = Vector2.Distance(transform.position, _target.position);
        
        if (_currentState == EnemyState.Patrolling)
        {
            return distanceToPlayer <= detectionRange;
        }
        else
        {
            return distanceToPlayer <= loseTargetRange;
        }
    }

    protected virtual void UpdateEnemyState()
    {
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                if (IsPlayerInRange())
                {
                    _currentState = EnemyState.Pursuing;
                    _lastKnownPlayerPosition = _target.position;
                }
                break;
                
            case EnemyState.Pursuing:
                if (IsPlayerInRange())
                {
                    _lastKnownPlayerPosition = _target.position;
                }
                else
                {
                    _currentState = EnemyState.Searching;
                    _searchStartTime = Time.time;
                }
                break;
                
            case EnemyState.Searching:
                if (IsPlayerInRange())
                {
                    _currentState = EnemyState.Pursuing;
                    _lastKnownPlayerPosition = _target.position;
                }
                else if (Time.time - _searchStartTime > _searchDuration)
                {
                    _currentState = EnemyState.Patrolling;
                    InitializeWanderDirection();
                }
                break;
        }
    }

    protected virtual void InitializeWanderDirection()
    {
        _wanderDirection = Random.insideUnitCircle.normalized;
        _wanderChangeTime = Time.time + _wanderChangeInterval;
    }

    protected virtual void UpdateWanderDirection()
    {
        if (Time.time >= _wanderChangeTime)
        {
            _wanderDirection = Random.insideUnitCircle.normalized;
            _wanderChangeTime = Time.time + _wanderChangeInterval;
        }
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        PlayHitSound(source);
        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        // Stop all audio sources before destruction to prevent sounds from persisting
        if (_audioSourcePool != null)
        {
            foreach (var source in _audioSourcePool)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }
            }
        }

        // Stop beam audio source (for enemy firing beams)
        if (_beamAudioSource != null && _beamAudioSource.isPlaying)
        {
            _beamAudioSource.Stop();
        }

        // Stop beam hit loop source (for being hit by player's beam)
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        // Play explosion sound (2D audio that survives GameObject destruction)
        if (explosionSound != null)
        {
            Play2DAudioAtPoint(explosionSound, transform.position, audioVolume.explosionVolume);
        }

        // Trigger screen shake on death
        if (_impulseSource != null)
        {
            float shakeForce = maxHealth * deathShakeForceMultiplier;
            _impulseSource.GenerateImpulse(shakeForce);
        }

        base.Die();
    }

    // Helper method to play 3D spatial audio that survives GameObject destruction
    private static void Play2DAudioAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        // Check if AudioListener exists in scene
        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("No AudioListener found in scene! 3D spatial audio will not work correctly. Add an AudioListener component to your main camera.");
        }

        GameObject tempAudio = new GameObject("TempAudio_Explosion");
        tempAudio.transform.position = position;
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;

        // Configure 3D spatial audio
        audioSource.spatialBlend = 1f; // Full 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 10f; // Full volume within this distance
        audioSource.maxDistance = 50f; // Silent beyond this distance
        audioSource.dopplerLevel = 0f; // Disable doppler effect for explosions

        audioSource.Play();
        Object.Destroy(tempAudio, clip.length);
    }
}
