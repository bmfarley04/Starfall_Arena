using UnityEngine;
using Unity.Cinemachine;

public enum EnemyState
{
    Patrolling,
    Pursuing,
    Searching
}

public abstract class Enemy : Entity
{
    // ===== MOVEMENT =====
    [Header("Enemy Movement Settings")]
    public bool useLateralDamping = true;
    public float preferredDistance = 5f;

    // ===== DETECTION =====
    [Header("Enemy Detection Settings")]
    public float detectionRange = 10f;
    public float loseTargetRange = 12f;

    // ===== COMBAT =====
    [Header("Enemy Combat Settings")]
    public float fireRate = 1f;
    public bool useLeadTargeting = false;
    public float leadTargetingAccuracy = 1f;
    [Tooltip("Angle threshold (degrees) enemy must be aimed at target before firing")]
    public float aimTolerance = 5f;

    // ===== DEATH =====
    [Header("Death Settings")]
    public float deathShakeForceMultiplier = .1f;

    // ===== SOUND EFFECTS =====
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

    // ===== PROTECTED STATE =====
    protected Transform _target;
    protected float _nextFireTime;
    protected EnemyState _currentState = EnemyState.Patrolling;
    protected CinemachineImpulseSource _impulseSource;
    protected AudioSource[] _audioSourcePool;
    protected int _audioSourcePoolSize = 5;
    protected AudioSource _beamAudioSource;
    protected AudioSource _beamHitLoopSource;
    protected float _lastBeamHitTime;
    protected Vector3 _lastKnownPlayerPosition;
    protected float _searchStartTime;
    protected float _searchDuration = 2f;
    protected Vector2 _wanderDirection;
    protected float _wanderChangeTime;
    protected float _wanderChangeInterval = 2.5f;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        InitializeAudioSystem();
    }

    protected virtual void InitializeAudioSystem()
    {
        _audioSourcePool = new AudioSource[_audioSourcePoolSize];
        for (int i = 0; i < _audioSourcePoolSize; i++)
        {
            _audioSourcePool[i] = gameObject.AddComponent<AudioSource>();
            _audioSourcePool[i].playOnAwake = false;
            _audioSourcePool[i].spatialBlend = 1f;
            _audioSourcePool[i].rolloffMode = AudioRolloffMode.Linear;
            _audioSourcePool[i].minDistance = 10f;
            _audioSourcePool[i].maxDistance = 50f;
            _audioSourcePool[i].dopplerLevel = 0f;
        }

        _beamAudioSource = gameObject.AddComponent<AudioSource>();
        _beamAudioSource.playOnAwake = false;
        _beamAudioSource.loop = true;
        _beamAudioSource.spatialBlend = 1f;
        _beamAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _beamAudioSource.minDistance = 10f;
        _beamAudioSource.maxDistance = 50f;
        _beamAudioSource.dopplerLevel = 0f;

        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 1f;
        _beamHitLoopSource.rolloffMode = AudioRolloffMode.Linear;
        _beamHitLoopSource.minDistance = 10f;
        _beamHitLoopSource.maxDistance = 50f;
        _beamHitLoopSource.dopplerLevel = 0f;
    }

    protected AudioSource GetAvailableAudioSource()
    {
        foreach (var source in _audioSourcePool)
        {
            if (!source.isPlaying)
            {
                source.clip = null;
                return source;
            }
        }
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

    protected void StartBeamAudio()
    {
        if (beamFireSound == null || _beamAudioSource == null) return;

        if (!_beamAudioSource.isPlaying)
        {
            _beamAudioSource.clip = beamFireSound;
            _beamAudioSource.volume = audioVolume.beamFireVolume;
            _beamAudioSource.Play();
        }
    }

    protected void StopBeamAudio()
    {
        if (_beamAudioSource != null && _beamAudioSource.isPlaying)
        {
            _beamAudioSource.Stop();
        }
    }

    protected void PlayHitSound(DamageSource damageSource)
    {
        switch (damageSource)
        {
            case DamageSource.Projectile:
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
                _lastBeamHitTime = Time.time;

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

    // ===== UPDATE LOOPS =====
    protected override void Update()
    {
        base.Update();

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

    // ===== MOVEMENT =====
    protected virtual void Move()
    {
        _isThrusting = false;

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

    // ===== ROTATION =====
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

    // ===== COMBAT =====
    protected virtual bool IsAimedAtTarget()
    {
        if (_target == null) return false;

        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 directionToTarget = (targetPosition - transform.position).normalized;
        float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.z;

        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle + ROTATION_OFFSET);

        return Mathf.Abs(angleDifference) <= aimTolerance;
    }

    protected virtual void TryFire()
    {
        if (Time.time >= _nextFireTime && projectileWeapon.prefab != null)
        {
            if (IsAimedAtTarget())
            {
                Fire();
                _nextFireTime = Time.time + (1f / fireRate);
            }
        }
    }

    protected virtual void Fire()
    {
        if (projectileWeapon.prefab == null)
            return;

        Vector3 targetPosition = CalculateTargetPosition();
        Vector2 fireDirection = (targetPosition - transform.position).normalized;

        if (turrets != null && turrets.Count > 0)
        {
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

    // ===== AI STATE =====
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

    // ===== PUBLIC ACCESSORS =====
    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    // ===== DAMAGE & DEATH =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        PlayHitSound(source);
        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
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

        if (_beamAudioSource != null && _beamAudioSource.isPlaying)
        {
            _beamAudioSource.Stop();
        }

        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        if (explosionSound != null)
        {
            Play2DAudioAtPoint(explosionSound, transform.position, audioVolume.explosionVolume);
        }

        if (_impulseSource != null)
        {
            float shakeForce = maxHealth * deathShakeForceMultiplier;
            _impulseSource.GenerateImpulse(shakeForce);
        }

        base.Die();
    }

    private static void Play2DAudioAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("No AudioListener found in scene! 3D spatial audio will not work correctly. Add an AudioListener component to your main camera.");
        }

        GameObject tempAudio = new GameObject("TempAudio_Explosion");
        tempAudio.transform.position = position;
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;

        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 10f;
        audioSource.maxDistance = 50f;
        audioSource.dopplerLevel = 0f;

        audioSource.Play();
        Object.Destroy(tempAudio, clip.length);
    }
}
