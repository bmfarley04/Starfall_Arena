using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public struct ShieldRegenConfig
{
    [Tooltip("Time in seconds without taking damage before shields start regenerating")]
    public float regenDelay;
    [Tooltip("Amount of shield restored per second")]
    public float regenRate;
}

[System.Serializable]
public struct InputConfig
{
    [Tooltip("Deadzone threshold for controller look input (0-1)")]
    [Range(0f, 1f)]
    public float controllerLookDeadzone;
    [Tooltip("Minimum mouse movement (pixels) to detect mouse input")]
    public float mouseMovementThreshold;
}

[System.Serializable]
public struct FrictionConfig
{
    [Tooltip("How long (seconds) after thrust ends before friction starts")]
    public float frictionDelay;
    [Tooltip("How fast velocity is reduced (units per second) once friction is active")]
    public float frictionDeceleration;
    [Tooltip("If true, prints friction debug logs")]
    public bool debugMode;
}

[System.Serializable]
public struct VisualFeedbackConfig
{
    [Header("Chromatic Aberration")]
    [Tooltip("Enable/disable chromatic aberration on taking damage")]
    public bool enableChromaticAberration;
    [Tooltip("Max chromatic aberration intensity")]
    public float maxChromaticIntensity;
    [Tooltip("Intensity increase per damage point")]
    public float chromaticIntensityPerDamage;
    [Tooltip("How fast chromatic aberration fades (units per second)")]
    public float chromaticFadeSpeed;

    [Header("Detection")]
    [Tooltip("Time window to detect beam hits (seconds)")]
    public float beamDetectionWindow;
    public float projectileMultiplier;
}

[System.Serializable]
public struct ScreenShakeConfig
{
    [Tooltip("Enable/disable screen shake on taking damage")]
    public bool enableScreenShake;
    [Tooltip("Screen shake intensity multiplier for projectile hits")]
    public float projectileShakeMultiplier;
    [Tooltip("Screen shake intensity multiplier for laser beam hits")]
    public float laserShakeMultiplier;
}

public abstract class Player : Entity
{
    // ===== SHIELD REGENERATION =====
    [Header("Shield Regeneration")]
    public ShieldRegenConfig shieldRegen;

    // ===== INPUT SETTINGS =====
    [Header("Input Settings")]
    public InputConfig input;

    // ===== FRICTION =====
    [Header("Friction System")]
    public FrictionConfig friction;

    // ===== VISUAL FEEDBACK =====
    [Header("Visual Feedback")]
    public VisualFeedbackConfig visualFeedback;

    // ===== SCREEN SHAKE =====
    [Header("Screen Shake")]
    public ScreenShakeConfig screenShake;

    // ===== SOUND EFFECTS =====
    [Header("Sound Effects")]
    [Tooltip("Basic projectile fire sound")]
    public SoundEffect projectileFireSound;
    [Tooltip("Shield damage sound")]
    public SoundEffect shieldDamageSound;
    [Tooltip("Hull damage sound")]
    public SoundEffect hullDamageSound;
    [Tooltip("Beam hit loop sound (loops while taking beam damage)")]
    public SoundEffect beamHitLoopSound;

    [Header("Audio System")]
    [Tooltip("Number of AudioSources in the pool for overlapping sounds")]
    public int audioSourcePoolSize = 10;

    // ===== PROTECTED STATE (for derived classes) =====
    protected float fireCooldown = 0.5f;  // Can be overridden in derived classes
    protected string thisPlayerTag;
    protected string enemyTag;

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
    private float _originalRotationSpeed;
    private bool _isAnchored = false;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        _originalRotationSpeed = movement.rotationSpeed;
        if(gameObject.CompareTag("Player1"))
        {
            thisPlayerTag = "Player1";
            enemyTag = "Player2";
        }
        else if (gameObject.CompareTag("Player2"))
        {
            thisPlayerTag = "Player2";
            enemyTag = "Player1";
        }
        else
        {
            thisPlayerTag = "Player";
            enemyTag = "Enemy";
        }

        _lastShieldHitTime = -shieldRegen.regenDelay;
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

    // ===== UPDATE LOOPS =====
    protected override void Update()
    {
        base.Update();
        HandleRotation();
        HandleShieldRegeneration();

        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            float timeSinceLastHit = Time.time - _lastDamageTime;
            if (timeSinceLastHit > visualFeedback.beamDetectionWindow)
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
            _rb.AddForce(thrustDirection * movement.thrustForce);
            ApplyLateralDamping();
            _frictionTimer = 0f;
        }
        else
        {
            _isThrusting = false;

            if (_frictionEnabled)
            {
                _frictionTimer += Time.fixedDeltaTime;

                if (_frictionTimer >= friction.frictionDelay)
                {
                    ApplyFriction();
                }
                else if (friction.debugMode)
                {
                    Debug.Log($"friction waiting: {_frictionTimer:F2}/{friction.frictionDelay:F2}");
                }
            }
        }
        if (_isAnchored)
        {
            _rb.linearDamping += .1f;
        }
        ClampVelocity();
    }

    // ===== MOVEMENT =====
    void ApplyFriction()
    {
        Vector2 currentVel = _rb.linearVelocity;
        Vector2 newVel = Vector2.MoveTowards(currentVel, Vector2.zero, friction.frictionDeceleration * Time.fixedDeltaTime);
        _rb.linearVelocity = newVel;

        if (friction.debugMode)
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

        if (_lookInput.magnitude > input.controllerLookDeadzone)
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


    // ===== ROTATION =====
    protected virtual void HandleRotation()
    {
        Vector2 currentMousePosition = Mouse.current.position.ReadValue();
        if (Vector2.Distance(currentMousePosition, _lastMousePosition) > input.mouseMovementThreshold)
        {
            _lastMousePosition = currentMousePosition;
            _lastMouseMoveTime = Time.time;
        }

        bool controllerUsedRecently = _lookInput.magnitude > input.controllerLookDeadzone;
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
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, movement.rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected virtual void RotateTowardMouse()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        Vector2 direction = mouseWorldPosition - transform.position;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, movement.rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    // Anchor
    void OnAnchor(InputValue value)
    {
        if (value.isPressed)
        {
            thrusters.invertColors = true;
            Debug.Log("Anchor Activated: Rotate " + movement.rotationSpeed);
            movement.rotationSpeed *= 3;
            _isAnchored = true;
        }
        else
        {
            thrusters.invertColors = false;
            _isAnchored = false;
            _rb.linearDamping = 0f;
            movement.rotationSpeed = _originalRotationSpeed;
            Debug.Log("Anchor Deactivated: Rotate " + _originalRotationSpeed);
        }
    }

    // ===== COMBAT =====
    void TryFireProjectile()
    {
        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        foreach (var turret in turrets)
        {
            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    GetFireDirection(turret),
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

        if (projectileFireSound != null)
        {
            projectileFireSound.Play(GetAvailableAudioSource());
        }

        _lastFireTime = Time.time;
    }

    protected virtual Vector3 GetFireDirection(Transform turret)
    {
        return transform.up;
    }

    // ===== SHIELD REGENERATION =====
    private void HandleShieldRegeneration()
    {
        if (currentShield >= maxShield || maxShield <= 0)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        if (Time.time < _lastShieldHitTime + shieldRegen.regenDelay)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        currentShield += shieldRegen.regenRate * Time.deltaTime;

        if (currentShield > maxShield)
        {
            currentShield = maxShield;
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
                beamHitLoopSound.Play(_beamHitLoopSource);
            }
        }
        else
        {
            if (previousShield > 0f)
            {
                if (shieldDamageSound != null)
                {
                    shieldDamageSound.Play(GetAvailableAudioSource());
                }
            }
            else
            {
                if (hullDamageSound != null)
                {
                    hullDamageSound.Play(GetAvailableAudioSource());
                }
            }
        }

        if (visualFeedback.enableChromaticAberration && _chromaticAberration != null)
        {
            HandleChromaticAberration(impactForce);
        }

        if (screenShake.enableScreenShake && _impulseSource != null)
        {
            HandleScreenShake(damage, impactForce, source);
        }
    }

    protected override void Die()
    {
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        base.Die();
    }

    // ===== CHROMATIC ABERRATION =====
    private void HandleChromaticAberration(float impactForce)
    {
        float timeSinceLastHit = Time.time - _lastDamageTime;

        bool isBeamHit = timeSinceLastHit < visualFeedback.beamDetectionWindow;

        if (isBeamHit)
        {
            _damageAccumulator += impactForce;
            float targetIntensity = Mathf.Min(_damageAccumulator * visualFeedback.chromaticIntensityPerDamage, visualFeedback.maxChromaticIntensity);
            _currentChromaticIntensity = Mathf.Lerp(_currentChromaticIntensity, targetIntensity, Time.deltaTime * 5f);
        }
        else
        {
            _damageAccumulator = impactForce;
            _currentChromaticIntensity = Mathf.Min(impactForce * visualFeedback.chromaticIntensityPerDamage * visualFeedback.projectileMultiplier, visualFeedback.maxChromaticIntensity);
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

        while (Time.time - _lastDamageTime < visualFeedback.beamDetectionWindow)
        {
            yield return null;
        }

        while (_currentChromaticIntensity > 0.01f)
        {
            _damageAccumulator = Mathf.Max(0f, _damageAccumulator - visualFeedback.chromaticFadeSpeed * Time.deltaTime);
            _currentChromaticIntensity = Mathf.Max(0f, _currentChromaticIntensity - visualFeedback.chromaticFadeSpeed * Time.deltaTime);

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
            _chromaticAberration.intensity.value = Mathf.Clamp(intensity, 0f, visualFeedback.maxChromaticIntensity * 2f);
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

    // ===== SCREEN SHAKE =====
    private void HandleScreenShake(float damage, float impactForce, DamageSource source)
    {
        float shakeIntensity = 0f;

        if (source == DamageSource.LaserBeam)
        {
            // For laser beams, use a constant multiplier (continuous damage)
            shakeIntensity = screenShake.laserShakeMultiplier;
        }
        else
        {
            // For projectiles and other sources, scale by damage
            shakeIntensity = damage * screenShake.projectileShakeMultiplier;
        }

        if (shakeIntensity > 0f)
        {
            _impulseSource.GenerateImpulse(shakeIntensity);
        }
    }

    protected override void OnHealthChanged()
    {
    }

    protected override void OnShieldChanged()
    {
    }
}
