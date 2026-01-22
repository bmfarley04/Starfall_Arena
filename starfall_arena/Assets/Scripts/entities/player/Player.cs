using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public abstract class Player : Entity
{
    // ===== CONTROLLER & INPUT =====
    [Header("Controller Settings")]
    [Tooltip("Deadzone threshold for controller look input (0-1)")]
    [Range(0f, 0.5f)]
    public float controllerLookDeadzone = 0.1f;
    [Tooltip("Minimum mouse movement (pixels) to detect mouse input")]
    public float mouseMovementThreshold = 5f;

    // ===== FRICTION =====
    [Header("Friction Settings")]
    [Tooltip("how long (seconds) after thrust ends before friction starts")]
    public float frictionDelay = 0.25f;
    [Tooltip("how fast velocity is reduced (units per second) once friction is active")]
    public float frictionDeceleration = 6f;
    [Tooltip("if true, prints friction debug logs")]
    public bool frictionDebug = false;

    // ===== COMBAT =====
    [Header("Combat Settings")]
    [Tooltip("cooldown between normal fire shots (seconds)")]
    public float fireCooldown = 0.5f;

    // ===== SHIELD REGENERATION =====
    [Header("Shield Regeneration")]
    [Tooltip("Time in seconds without taking damage before shields start regenerating")]
    public float shieldRegenDelay = 3f;
    [Tooltip("Amount of shield restored per second")]
    public float shieldRegenRate = 10f;

    // ===== VISUAL FEEDBACK =====
    [Header("Visual Feedback")]
    [Tooltip("enable/disable chromatic aberration on taking damage")]
    public bool enableChromaticAberration = true;
    [Tooltip("max chromatic aberration intensity")]
    public float maxChromaticIntensity = 1f;
    [Tooltip("intensity increase per damage point")]
    public float chromaticIntensityPerDamage = 0.05f;
    [Tooltip("how fast chromatic aberration fades (units per second)")]
    public float chromaticFadeSpeed = 2f;
    [Tooltip("time window to detect beam hits (seconds)")]
    public float beamDetectionWindow = 0.2f;
    public float _projectileMultiplier = 2f;

    // ===== SOUND EFFECTS =====
    [Header("Sound Effects")]
    [Tooltip("Basic projectile fire sound")]
    public AudioClip projectileFireSound;
    [Tooltip("Shield damage sound")]
    public AudioClip shieldDamageSound;
    [Tooltip("Hull damage sound")]
    public AudioClip hullDamageSound;
    [Tooltip("Beam hit loop sound (loops while taking beam damage)")]
    public AudioClip beamHitLoopSound;

    [Header("Audio System")]
    [Tooltip("Number of AudioSources in the pool for overlapping sounds")]
    public int audioSourcePoolSize = 10;

    [System.Serializable]
    public struct AudioVolumeConfig
    {
        [Header("Weapon Sounds")]
        [Range(0f, 3f)]
        [Tooltip("Volume for projectile fire sound")]
        public float projectileFireVolume;

        [Header("Damage Sounds")]
        [Range(0f, 3f)]
        [Tooltip("Volume for shield damage sound")]
        public float shieldDamageVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for hull damage sound")]
        public float hullDamageVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for beam hit loop sound")]
        public float beamHitLoopVolume;
    }

    [Header("Audio Volume Controls")]
    public AudioVolumeConfig audioVolume = new AudioVolumeConfig
    {
        projectileFireVolume = 0.7f,
        shieldDamageVolume = 0.7f,
        hullDamageVolume = 0.7f,
        beamHitLoopVolume = 0.7f
    };

    // ===== PRIVATE STATE =====
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private bool _frictionEnabled = false;
    private Vector2 _lookInput;
    private bool _usingControllerLook = false;
    private float _lastControllerLookTime = 0f;
    private Vector2 _lastMousePosition;
    private float _lastMouseMoveTime = 0f;
    private float _lastFireTime = -999f;
    private bool _isFiring = false;
    private float _frictionTimer = 0f;
    private float _lastShieldHitTime;
    private ChromaticAberration _chromaticAberration;
    private Coroutine _chromaticFadeCoroutine;
    private float _currentChromaticIntensity = 0f;
    private float _lastDamageTime;
    private float _damageAccumulator;
    private Unity.Cinemachine.CinemachineImpulseSource _impulseSource;
    private AudioSource[] _audioSourcePool;
    private AudioSource _beamHitLoopSource;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();

        _lastShieldHitTime = -shieldRegenDelay;
        _lastMousePosition = Mouse.current.position.ReadValue();
        _lastMouseMoveTime = Time.time;

        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput != null)
        {
            _moveAction = _playerInput.actions["Move"];
        }

        _impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        if (_impulseSource == null)
        {
            Debug.LogWarning("CinemachineImpulseSource not found on player. Screen shake effects will be disabled.", this);
        }

        var volume = FindObjectOfType<Volume>();
        if (volume != null && volume.profile.TryGet(out ChromaticAberration ca))
        {
            _chromaticAberration = ca;
        }
        else
        {
            Debug.LogWarning("ChromaticAberration not found in Volume profile. Visual feedback will be disabled.", this);
        }

        InitializeAudioSystem();
    }

    private void InitializeAudioSystem()
    {
        _audioSourcePool = new AudioSource[audioSourcePoolSize];
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            _audioSourcePool[i] = gameObject.AddComponent<AudioSource>();
            _audioSourcePool[i].playOnAwake = false;
            _audioSourcePool[i].spatialBlend = 1f;
            _audioSourcePool[i].rolloffMode = AudioRolloffMode.Linear;
            _audioSourcePool[i].minDistance = 10f;
            _audioSourcePool[i].maxDistance = 50f;
            _audioSourcePool[i].dopplerLevel = 0f;
        }

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
                return source;
            }
        }
        return _audioSourcePool[0];
    }

    protected void PlayOneShotSound(AudioClip clip, float volume = 1f, AudioClipType clipType = AudioClipType.Default)
    {
        if (clip == null) return;

        float volumeMultiplier = GetVolumeMultiplier(clipType);
        AudioSource source = GetAvailableAudioSource();
        source.PlayOneShot(clip, volume * volumeMultiplier);
    }

    protected enum AudioClipType
    {
        Default,
        ProjectileFire,
        ShieldDamage,
        HullDamage,
        BeamHitLoop
    }

    private float GetVolumeMultiplier(AudioClipType clipType)
    {
        return clipType switch
        {
            AudioClipType.ProjectileFire => audioVolume.projectileFireVolume,
            AudioClipType.ShieldDamage => audioVolume.shieldDamageVolume,
            AudioClipType.HullDamage => audioVolume.hullDamageVolume,
            AudioClipType.BeamHitLoop => audioVolume.beamHitLoopVolume,
            _ => 1f
        };
    }

    // ===== UPDATE LOOPS =====
    protected override void Update()
    {
        base.Update();
        HandleRotation();
        HandleShieldRegeneration();

        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            float timeSinceLastHit = Time.time - _lastDamageTime;
            if (timeSinceLastHit > beamDetectionWindow)
            {
                _beamHitLoopSource.Stop();
            }
        }

        if (_isFiring)
        {
            TryFireProjectile();
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        bool movePressed = _moveAction != null && _moveAction.IsPressed();

        if (movePressed)
        {
            _isThrusting = true;
            Vector2 thrustDirection = transform.up;
            _rb.AddForce(thrustDirection * thrustForce);
            ApplyLateralDamping();
            _frictionTimer = 0f;
        }
        else
        {
            _isThrusting = false;

            if (_frictionEnabled)
            {
                _frictionTimer += Time.fixedDeltaTime;

                if (_frictionTimer >= frictionDelay)
                {
                    ApplyFriction();
                }
                else if (frictionDebug)
                {
                    Debug.Log($"friction waiting: {_frictionTimer:F2}/{frictionDelay:F2}");
                }
            }
        }

        ClampVelocity();
    }

    // ===== MOVEMENT =====
    void ApplyFriction()
    {
        Vector2 currentVel = _rb.linearVelocity;
        Vector2 newVel = Vector2.MoveTowards(currentVel, Vector2.zero, frictionDeceleration * Time.fixedDeltaTime);
        _rb.linearVelocity = newVel;

        if (frictionDebug)
        {
            Debug.Log($"applying friction: vel {currentVel.magnitude:F2} -> {newVel.magnitude:F2}");
        }
    }

    // ===== INPUT CALLBACKS =====
    void OnMove()
    {
    }

    void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();

        if (_lookInput.magnitude > controllerLookDeadzone)
        {
            _usingControllerLook = true;
            _lastControllerLookTime = Time.time;
        }
    }

    void OnToggleFriction()
    {
        _frictionEnabled = !_frictionEnabled;
        _frictionTimer = 0f;
        Debug.Log($"friction: {(_frictionEnabled ? "ON" : "OFF")}");
    }

    void OnFire(InputValue value)
    {
        _isFiring = value.Get<float>() > 0f;
    }

    void OnPause()
    {
        if (sceneManager != null)
        {
            sceneManager.TogglePause();
        }
    }

    // ===== ROTATION =====
    protected virtual void HandleRotation()
    {
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        Vector2 currentMousePosition = Mouse.current.position.ReadValue();
        if (Vector2.Distance(currentMousePosition, _lastMousePosition) > mouseMovementThreshold)
        {
            _lastMousePosition = currentMousePosition;
            _lastMouseMoveTime = Time.time;
        }

        bool controllerUsedRecently = _lookInput.magnitude > controllerLookDeadzone;
        bool mouseUsedMoreRecently = _lastMouseMoveTime > _lastControllerLookTime;

        if (controllerUsedRecently)
        {
            RotateWithController();
        }
        else if (mouseUsedMoreRecently)
        {
            RotateTowardMouse();
        }
    }

    protected virtual void RotateWithController()
    {
        float targetAngle = Mathf.Atan2(_lookInput.y, _lookInput.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected virtual void RotateTowardMouse()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        Vector2 direction = mouseWorldPosition - transform.position;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    // ===== COMBAT =====
    void TryFireProjectile()
    {
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        foreach (var turret in turrets)
        {
            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.transform.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = "Enemy";
                projectileScript.Initialize(
                    transform.up,
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

        _lastFireTime = Time.time;
    }

    // ===== SHIELD REGENERATION =====
    private void HandleShieldRegeneration()
    {
        if (currentShield >= maxShield || maxShield <= 0)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        if (Time.time < _lastShieldHitTime + shieldRegenDelay)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        currentShield += shieldRegenRate * Time.deltaTime;

        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }

        if (currentShield > 0 && !hasShield)
        {
            hasShield = true;
        }

        OnShieldChanged();
        if (shieldController != null) shieldController.SetRegeneration(true);
    }

    // ===== DAMAGE HANDLING =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        float previousShield = currentShield;

        _lastShieldHitTime = Time.time;

        base.TakeDamage(damage, impactForce, hitPoint, source);

        if (source == DamageSource.LaserBeam)
        {
            if (beamHitLoopSound != null && _beamHitLoopSource != null && !_beamHitLoopSource.isPlaying)
            {
                _beamHitLoopSource.clip = beamHitLoopSound;
                _beamHitLoopSource.volume = audioVolume.beamHitLoopVolume;
                _beamHitLoopSource.Play();
            }
        }
        else
        {
            if (previousShield > 0f)
            {
                PlayOneShotSound(shieldDamageSound, 1f, AudioClipType.ShieldDamage);
            }
            else
            {
                PlayOneShotSound(hullDamageSound, 1f, AudioClipType.HullDamage);
            }
        }

        if (enableChromaticAberration && _chromaticAberration != null)
        {
            HandleChromaticAberration(impactForce);
        }
    }

    protected override void Die()
    {
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        if (sceneManager != null)
        {
            sceneManager.PlayPlayerExplosionSound();
            sceneManager.OnPlayerDestroyed();
        }
        else
        {
            Debug.LogError("sceneManagerScript reference missing! Cannot notify of player destruction.", this);
        }

        base.Die();
    }

    // ===== CHROMATIC ABERRATION =====
    private void HandleChromaticAberration(float impactForce)
    {
        float timeSinceLastHit = Time.time - _lastDamageTime;

        bool isBeamHit = timeSinceLastHit < beamDetectionWindow;

        if (isBeamHit)
        {
            _damageAccumulator += impactForce;
            float targetIntensity = Mathf.Min(_damageAccumulator * chromaticIntensityPerDamage, maxChromaticIntensity);
            _currentChromaticIntensity = Mathf.Lerp(_currentChromaticIntensity, targetIntensity, Time.deltaTime * 5f);
        }
        else
        {
            _damageAccumulator = impactForce;
            _projectileMultiplier = 2f;
            _currentChromaticIntensity = Mathf.Min(impactForce * chromaticIntensityPerDamage * _projectileMultiplier, maxChromaticIntensity);
        }

        _chromaticAberration.intensity.value = _currentChromaticIntensity;
        _lastDamageTime = Time.time;

        if (_chromaticFadeCoroutine != null)
        {
            StopCoroutine(_chromaticFadeCoroutine);
        }
        _chromaticFadeCoroutine = StartCoroutine(FadeChromaticAberration());
    }

    private System.Collections.IEnumerator FadeChromaticAberration()
    {
        yield return null;

        while (Time.time - _lastDamageTime < beamDetectionWindow)
        {
            yield return null;
        }

        while (_currentChromaticIntensity > 0.01f)
        {
            _damageAccumulator = Mathf.Max(0f, _damageAccumulator - chromaticFadeSpeed * Time.deltaTime);
            _currentChromaticIntensity = Mathf.Max(0f, _currentChromaticIntensity - chromaticFadeSpeed * Time.deltaTime);

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = _currentChromaticIntensity;
            }

            yield return null;
        }

        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = 0f;
        }
        _currentChromaticIntensity = 0f;
        _damageAccumulator = 0f;
    }

    public void SetChromaticAberrationIntensity(float intensity)
    {
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = Mathf.Clamp(intensity, 0f, maxChromaticIntensity * 2f);
        }
    }

    public float GetChromaticAberrationIntensity()
    {
        if (_chromaticAberration != null)
        {
            return _chromaticAberration.intensity.value;
        }
        return 0f;
    }

    protected override void OnHealthChanged()
    {
    }

    protected override void OnShieldChanged()
    {
    }
}
