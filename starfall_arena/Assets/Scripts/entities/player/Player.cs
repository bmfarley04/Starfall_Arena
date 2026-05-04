using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using StarfallArena.UI;
using TMPro;
using System;
using System.Collections;

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

    [Tooltip("Logs which aiming path is active and the values driving rotation.")]
    public bool debugRotation;
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

[System.Serializable]
public struct HUDConfig
{
    [Header("Health")]
    [Tooltip("Segmented bar for health display")]
    public SegmentedBar healthBar;
    [Tooltip("Text displaying current health number")]
    public TextMeshProUGUI healthText;

    [Header("Shield")]
    [Tooltip("Segmented bar for shield display")]
    public SegmentedBar shieldBar;
    [Tooltip("Text displaying current shield number")]
    public TextMeshProUGUI shieldText;
}

public abstract class Player : Entity
{
    // ===== ABILITIES =====
    [HideInInspector]
    public Ability ability1,ability2,ability3,ability4;
#if UNITY_EDITOR
    [Tooltip("Drag your Ability script here")]
    public MonoScript[] abilitySlots = new MonoScript[4];
#endif


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

    // ===== HUD =====
    [Header("HUD")]
    public HUDConfig hud;

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
    [Tooltip("Explosion sound on death")]
    public SoundEffect explosionSound;

    [Header("Audio System")]
    [Tooltip("Number of AudioSources in the pool for overlapping sounds")]
    public int audioSourcePoolSize = 10;

    [Header("Presentation")]
    [Tooltip("Uniform ship-size multiplier used by augment and ability presentation prefabs")]
    [Min(0.01f)]
    [SerializeField] private float shipSize = 1f;

    // ===== PROTECTED STATE (for derived classes) =====
    protected float fireCooldown = 0.5f;  // Can be overridden in derived classes

    // ===== MOVEMENT LOCK =====
    [HideInInspector] public bool isMovementLocked = false;

    /// <summary>
    /// When true, Player skips its own movement and rotation logic in FixedUpdate/Update.
    /// Used by external systems (e.g. NetMovement) that take over physics control.
    /// Input callbacks (OnThrust, OnLook, etc.) still fire so external systems can read input state.
    /// </summary>
    [HideInInspector] public bool externalMovementControl = false;

    // ===== STAT TRACKING =====
    public const int InvalidAttackId = -1;
    [HideInInspector] public int shotsFired;
    [HideInInspector] public int shotsHit;
    [HideInInspector] public float damageDealt;
    [HideInInspector] public float damageTaken;

    // PUBLIC GET PROTECTED SET
    public string thisPlayerTag { get; protected set; }
    public string enemyTag { get; protected set; }
    public float PrimaryFireCooldown => fireCooldown;
    public float ShipSize => Mathf.Max(0.01f, shipSize);

    // ===== PRIVATE STATE =====
    private List<Ability> abilities;
    private bool _isThrustPressed = false;
    private bool _frictionEnabled = false;
    private Vector2 _lookInput;
    protected float _lastFireTime = -999f;
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
    private bool _anchorInputHeld = false;
    private float _anchorDragAccumulator = 0f;
    private PlayerInput _playerInput;
    private int _nextAttackId = 1;
    private readonly HashSet<int> _registeredHitAttackIds = new HashSet<int>();

    // Public getter so augments and other systems can check whether the player is anchored
    public bool IsAnchored => _isAnchored;
    public bool IsAnchorInputHeld => _anchorInputHeld;

    // ===== READ-ONLY INPUT STATE (for external systems like NetMovement) =====
    public bool IsThrustPressed => _isThrustPressed;
    public Vector2 LookInput => _lookInput;
    public bool IsFrictionEnabled => _frictionEnabled;
    public float FrictionTimer => _frictionTimer;
    public System.Action<bool> onFrictionToggled;

    /// <summary>
    /// Returns the rotation multiplier from the currently active ability (if any).
    /// Used by the networked movement path to apply ability rotation penalties/boosts.
    /// </summary>
    public float GetAbilityRotationMultiplier()
    {
        var activeAbility = abilities.FirstOrDefault(a => a != null && a.IsAbilityActive());
        if (activeAbility != null)
        {
            return activeAbility.GetRotationMultiplier();
        }
        return 1f;
    }

    /// <summary>
    /// Returns the thrust multiplier from the currently active ability (if any).
    /// Used by the networked movement path to apply ability speed boosts/penalties.
    /// </summary>
    public float GetAbilityThrustMultiplier()
    {
        var activeAbility = abilities.FirstOrDefault(a => a != null && a.IsAbilityActive());
        if (activeAbility != null)
        {
            return activeAbility.GetThrustMultiplier();
        }
        return 1f;
    }

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();

        abilities = new List<Ability> { ability1, ability2, ability3, ability4 };
        _originalRotationSpeed = movement.rotationSpeed;
        _playerInput = GetComponent<PlayerInput>();
        RefreshCombatTags();

        _lastShieldHitTime = -shieldRegen.regenDelay;

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
        InitializeHUD();
    }

    protected virtual void Start()
    {
        // Spawn systems can set the final player tag after Instantiate/Awake.
        // Resolve again here so projectile targeting always reflects the final tag.
        RefreshCombatTags();
    }

    public void RefreshCombatTags()
    {
        if (gameObject.CompareTag("Player1"))
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
    }
    private void InitializeAudioSystem()
    {
        _audioSourcePool = new AudioSource[audioSourcePoolSize];
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            _audioSourcePool[i] = gameObject.AddComponent<AudioSource>();
            _audioSourcePool[i].playOnAwake = false;
            _audioSourcePool[i].spatialBlend = 0f;
        }

        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 0f;
    }

    public AudioSource GetAvailableAudioSource()
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

    // ===== ABILITY HUD =====
    private StarfallArena.UI.AbilityHUDPanel _abilityHUDPanel;

    public void BindAbilityHUD(StarfallArena.UI.AbilityHUDPanel panel)
    {
        _abilityHUDPanel = panel;
        if (panel != null)
        {
            panel.Bind(this);
        }
    }

    // ===== ABILITY HUD STATE API =====
    // Default implementation uses modular Ability components.
    // Derived classes with inline/non-modular abilities (e.g. Class2)
    // should override these methods.
    protected Ability GetAbilityInSlot(int slotIndex)
    {
        return slotIndex switch
        {
            1 => ability1,
            2 => ability2,
            3 => ability3,
            4 => ability4,
            _ => null
        };
    }

    public virtual float GetAbilityHUDFillRatio(int slotIndex)
    {
        Ability ability = GetAbilityInSlot(slotIndex);
        if (ability == null) return 0f;
        if (ability.isLocked) return 1f;
        return Mathf.Clamp01(ability.GetHUDFillRatio());
    }

    public virtual bool IsAbilityOnCooldownForHUD(int slotIndex)
    {
        Ability ability = GetAbilityInSlot(slotIndex);
        if (ability == null) return false;
        return ability.isLocked || ability.IsOnCooldown();
    }

    public virtual bool IsAbilityResourceBasedForHUD(int slotIndex)
    {
        Ability ability = GetAbilityInSlot(slotIndex);
        if (ability == null) return false;
        // Locked abilities should use cooldown visuals, not resource visuals.
        return !ability.isLocked && ability.IsResourceBased();
    }

    // ===== UPDATE LOOPS =====
    protected override void Update()
    {
        base.Update();

        if (isMovementLocked) return;

        UpdateAimInputFromActiveControlScheme();

        if (!externalMovementControl)
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

        if (_isFiring && !IsAnyAbilityActiveForPrimaryFireLock())
        {
            TryFireProjectile();
        }
    }

    protected override void FixedUpdate()
    {
        if (isMovementLocked) return;
        if (externalMovementControl) { base.FixedUpdate(); return; }

        if (abilities.Any(a => a != null && a.HasThrustMitigation() == true))
        {
            return;
        }
        var activeAbility = abilities.FirstOrDefault(a => a != null && a.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyThrustMultiplier();
        }

        base.FixedUpdate();

        bool movePressed = _isThrustPressed;
        float slowMult = GetSlowMultiplier();

        if (movePressed)
        {
            _isThrusting = true;

            // Dampen lateral (sideways) drift on the existing velocity first,
            // then add thrust.  This matches the old force-based timing where
            // AddForce was integrated by the physics engine AFTER user code ran.
            ApplyLateralDamping();

            Vector2 thrustDirection = transform.up;
            float acceleration = (movement.thrustForce * slowMult) / _rb.mass;
            _rb.linearVelocity += thrustDirection * acceleration * Time.fixedDeltaTime;

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
            // Manual drag that replicates Unity's per-step linear drag formula:
            //   velocity *= 1 / (1 + drag * dt)
            // The accumulator grows each tick just like the old
            // "_rb.linearDamping += .1f" did, producing identical braking.
            _anchorDragAccumulator += 0.1f;
            float dragFactor = 1f / (1f + _anchorDragAccumulator * Time.fixedDeltaTime);
            _rb.linearVelocity *= dragFactor;
        }

        // Apply slow to max speed
        float effectiveMaxSpeed = movement.maxSpeed * slowMult;
        if (_rb.linearVelocity.magnitude > effectiveMaxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * effectiveMaxSpeed;
        }

        // Restore original thrust force
        if (activeAbility != null)
        {
            activeAbility.RestoreThrustMultiplier();
        }
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnAbility1(InputValue value)
    {
        if (isMovementLocked) return;
        if(ability1 != null)
            ability1.TryUseAbility(value);
    }
    void OnAbility2(InputValue value)
    {
        if (isMovementLocked) return;
        if(ability2 != null)
            ability2.TryUseAbility(value);
    }
    void OnAbility3(InputValue value)
    {
        if (isMovementLocked) return;
        if(ability3 != null)
            ability3.TryUseAbility(value);
    }
    void OnAbility4(InputValue value)
    {
        if (isMovementLocked) return;
        if(ability4 != null)
            ability4.TryUseAbility(value);
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

    // // ===== INPUT CALLBACKS =====
    // void OnMove()
    // {
    // }

    void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
    }

    void OnFriction(InputValue value)
    {
        if (isMovementLocked || !value.isPressed) return;

        _frictionEnabled = !_frictionEnabled;
        _frictionTimer = 0f;
        onFrictionToggled?.Invoke(_frictionEnabled);
    }

    void OnToggleFriction(InputValue value)
    {
        OnFriction(value);
    }

    void OnThrust(InputValue value)
    {
        if (isMovementLocked) return;
        _isThrustPressed = value.Get<float>() > 0f;
    }

    void OnFire(InputValue value)
    {
        if (isMovementLocked) return;
        _isFiring = value.Get<float>() > 0f;
    }

    /// <summary>
    /// Returns true when primary fire should be blocked due to an active ability.
    /// Default behavior blocks firing while any modular ability is active.
    /// Inline-ability ship classes should override.
    /// </summary>
    /// missing code?
    protected virtual bool IsAnyAbilityActiveForPrimaryFireLock()
    {
        return abilities.Any(a => a != null && a.IsAbilityActive() && a.BlocksPrimaryFire);
    }


    // ===== ROTATION =====
    protected virtual void HandleRotation()
    {
        string controlScheme = GetActiveControlScheme();

        if (input.debugRotation)
        {
            Debug.Log(
                $"[PlayerRotation] object={name} scheme={controlScheme} lookInput={_lookInput} mousePresent={Mouse.current != null} playerInputEnabled={(_playerInput != null && _playerInput.enabled)}",
                this);
        }

        if (controlScheme == "controller")
        {
            if (_lookInput.magnitude > input.controllerLookDeadzone)
            {
                RotateWithController();
            }
            return;
        }

        if (controlScheme == "key+mouse" && _lookInput.sqrMagnitude > 0.0001f)
        {
            RotateTowardAimInput();
        }
    }

    protected virtual void RotateWithController()
    {
        float originalRotationSpeed = movement.rotationSpeed;

        var activeAbility = abilities.FirstOrDefault(a => a != null && a.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyRotationMultiplier();
        }

        float targetAngle = Mathf.Atan2(_lookInput.y, _lookInput.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, movement.rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);

        movement.rotationSpeed = originalRotationSpeed;
    }

    protected virtual bool ShouldRotateWithMouse()
    {
        return Mouse.current != null && (_playerInput == null || _playerInput.enabled);
    }

    protected virtual string GetActiveControlScheme()
    {
        if (_playerInput == null || !_playerInput.enabled)
        {
            return string.Empty;
        }

        return _playerInput.currentControlScheme ?? string.Empty;
    }

    protected virtual void RotateTowardAimInput()
    {
        float targetAngle = Mathf.Atan2(_lookInput.y, _lookInput.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, movement.rotationSpeed * Time.deltaTime);

        if (input.debugRotation)
        {
            Debug.Log(
                $"[PlayerRotation] aim object={name} lookInput={_lookInput} currentAngle={currentAngle:F2} targetAngle={(targetAngle + ROTATION_OFFSET):F2} newAngle={newAngle:F2}",
                this);
        }

        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    protected virtual void UpdateAimInputFromActiveControlScheme()
    {
        string controlScheme = GetActiveControlScheme();

        if (controlScheme != "key+mouse" || !ShouldRotateWithMouse())
        {
            return;
        }

        Camera aimCamera = _playerInput != null && _playerInput.camera != null
            ? _playerInput.camera
            : Camera.main;

        if (aimCamera == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 aimDirection = mouseWorldPosition - transform.position;

        _lookInput = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection.normalized
            : Vector2.zero;

        if (input.debugRotation)
        {
            Debug.Log(
                $"[PlayerRotation] mouse sample object={name} camera={aimCamera.name} mouseScreen={mouseScreenPosition} mouseWorld={mouseWorldPosition} sampledLookInput={_lookInput}",
                this);
        }
    }

    // Anchor
    void OnAnchor(InputValue value)
    {
        _anchorInputHeld = value.isPressed;
        ForceAnchorState(_anchorInputHeld);
    }

    public void ForceAnchorState(bool anchored)
    {
        if (anchored)
        {
            thrusters.invertColors = true;
            movement.rotationSpeed = _originalRotationSpeed * 3f;
            _isAnchored = true;
            return;
        }

        thrusters.invertColors = false;
        _isAnchored = false;
        _anchorDragAccumulator = 0f;
        movement.rotationSpeed = _originalRotationSpeed;
    }

    // ===== COMBAT =====
    protected virtual void TryFireProjectile()
    {
        if (isMovementLocked) return;

        Invisibility invisibility = ability2 as Invisibility;
        invisibility?.BreakInvisibilityFromAction();

        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        NetMovement netMovement = GetComponent<NetMovement>();
        if (NetTickUtil.IsActive && netMovement != null && netMovement.IsSpawned && netMovement.IsOwner)
        {
            for (int turretIndex = 0; turretIndex < turrets.Length; turretIndex++)
            {
                Transform turret = turrets[turretIndex];
                Vector3 direction = GetFireDirection(turret);
                if (!netMovement.IsServer)
                {
                    GameObject cosmeticProjectile = Instantiate(projectileWeapon.prefab, turret.position, Quaternion.identity);
                    if (cosmeticProjectile.TryGetComponent(out ProjectileScript cosmeticScript))
                    {
                        cosmeticScript.targetTag = enemyTag;
                        cosmeticScript.SetCosmeticOnly(true);
                        cosmeticScript.Initialize(
                            direction,
                            Vector2.zero,
                            projectileWeapon.speed,
                            projectileWeapon.damage,
                            projectileWeapon.lifetime,
                            projectileWeapon.impactForce,
                            this);
                    }
                }

                netMovement.RequestPrimaryFire(new NetFireRequest
                {
                    Tick = NetTickUtil.CurrentTick,
                    SpawnPosition = turret.position,
                    Direction = direction.normalized,
                    InheritedVelocity = Vector2.zero,
                    Speed = projectileWeapon.speed,
                    Damage = projectileWeapon.damage,
                    Lifetime = projectileWeapon.lifetime,
                    ImpactForce = projectileWeapon.impactForce,
                    RecoilForce = projectileWeapon.recoilForce,
                    ApplyRecoil = turretIndex == 0,
                    PierceMultiplier = 1f,
                    SlowMultiplier = 1f,
                    SlowDuration = 0f,
                    CanPierce = false,
                    AppliesSlow = false,
                    VisualType = NetProjectileVisualType.Primary,
                    IgnoreCooldown = false,
                    OwnerPredicted = true,
                    FireSource = (byte)PrimaryFireExecutionSource.PlayerInput,
                });
            }

            if (!netMovement.IsServer)
            {
                ApplyRecoil(projectileWeapon.recoilForce);
            }

            if (projectileFireSound != null)
            {
                projectileFireSound.Play(GetAvailableAudioSource());
            }

            _lastFireTime = Time.time;
            return;
        }

        int attackId = BeginTrackedAttack();

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
                    this,
                    attackId
                );
            }
        }
        ApplyRecoil(projectileWeapon.recoilForce);

        if (projectileFireSound != null)
        {
            projectileFireSound.Play(GetAvailableAudioSource());
        }

        PrimaryFireExecutionBus.Raise(this, PrimaryFireExecutionSource.PlayerInput);
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

    /// <summary>
    /// Resets the shield regen delay timer. Called when authoritative damage
    /// arrives via network RPC so the client's local regen timer stays in sync
    /// with the server.
    /// </summary>
    public void ResetShieldRegenTimer()
    {
        _lastShieldHitTime = Time.time;
    }

    /// <summary>
    /// Runs shield regen logic externally. Called by the server's NetMovement for
    /// client-owned players whose Player component is disabled.
    /// </summary>
    public void TickShieldRegeneration(float deltaTime)
    {
        if (currentShield >= maxShield || maxShield <= 0)
        {
            return;
        }

        if (Time.time < _lastShieldHitTime + shieldRegen.regenDelay)
        {
            return;
        }

        currentShield += shieldRegen.regenRate * deltaTime;
        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }
    }

    // ===== DAMAGE HANDLING =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile, Entity attacker = null, int accuracyAttackId = InvalidAttackId)
    {
        if (abilities.Any(a => a != null && a.HasDamageMitigation() == true))
        {
            return;
        }

        var activeAbility = abilities.FirstOrDefault(a => a != null && a.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyTakeDamageMultiplier(ref damage);
        }

        float previousShield = currentShield;

        _lastShieldHitTime = Time.time;

        base.TakeDamage(damage, impactForce, hitPoint, source, attacker, accuracyAttackId);

        // In networked play, TakeDamage only runs on the server. Audio for non-owner
        // players is handled by PlayNetworkDamageSounds via BroadcastCombatStateClientRpc.
        // Only play audio here for the host's own player to avoid looping sounds on
        // disabled Player components that can never auto-stop.
        bool shouldPlayLocalAudio = !NetTickUtil.IsActive || _IsLocallyOwned();

        if (shouldPlayLocalAudio)
        {
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

        if (explosionSound != null)
        {
            explosionSound.PlayAtPoint(transform.position);
        }

        foreach(var ability in abilities)
        {
            if (ability != null)
            {
                ability.Die();
            }
        }

        base.Die();
    }

    // ===== NETWORK AUDIO/VFX REPLICATION =====

    /// <summary>
    /// Called on non-owner clients via BroadcastCombatStateClientRpc to play damage sounds.
    /// </summary>
    public void PlayNetworkDamageSounds(DamageSource source, bool shieldHit)
    {
        if (source == DamageSource.LaserBeam)
        {
            // Only start the beam hit loop when Player.Update() is running (enabled),
            // because the auto-stop relies on Update(). On the host, non-owner Player
            // components are disabled, so starting a loop here would never stop.
            if (enabled && beamHitLoopSound != null && _beamHitLoopSource != null && !_beamHitLoopSource.isPlaying)
            {
                beamHitLoopSound.Play(_beamHitLoopSource);
            }
            _lastDamageTime = Time.time;
        }
        else
        {
            if (shieldHit)
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
    }

    /// <summary>
    /// Called on non-server clients via BroadcastDeathClientRpc to play death effects.
    /// </summary>
    public void PlayNetworkDeathEffects(Vector2 position, float rotation, Vector2 lastDamageDirection)
    {
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        if (explosionSound != null)
        {
            explosionSound.PlayAtPoint(position);
        }

        // Spawn explosion VFX
        if (visualEffects.explosionEffectPrefab != null)
        {
            Quaternion rot = Quaternion.Euler(0, 0, rotation);
            Vector2? impactDir = lastDamageDirection != Vector2.zero ? lastDamageDirection : (Vector2?)null;

            if (ExplosionPool.Instance != null)
            {
                ExplosionPool.Instance.GetExplosion(position, rot, visualEffects.explosionScale, impactDir);
            }
            else
            {
                GameObject explosion = Instantiate(visualEffects.explosionEffectPrefab, position, rot);
                explosion.transform.localScale = Vector3.one * visualEffects.explosionScale;

                if (impactDir.HasValue)
                {
                    ExplosionScript explosionScript = explosion.GetComponent<ExplosionScript>();
                    if (explosionScript != null)
                    {
                        explosionScript.SetImpactDirection(impactDir.Value);
                    }
                }
            }
        }

        // Scatter ship parts
        _lastDamageDirection = lastDamageDirection;
        ScatterShipParts();
    }

    /// <summary>
    /// Returns true when this player is locally owned (not networked, or owned by this client).
    /// Used to decide whether TakeDamage should play audio locally on the server.
    /// </summary>
    private bool _IsLocallyOwned()
    {
        NetMovement netMovement = GetComponent<NetMovement>();
        return netMovement == null || !netMovement.IsSpawned || netMovement.IsOwner;
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

    public void PrepareForRoundEndFreeze()
    {
        _isThrustPressed = false;
        _isFiring = false;
        _isThrusting = false;
        _lookInput = Vector2.zero;
        _frictionTimer = 0f;

        foreach (var ability in abilities)
        {
            if (ability != null)
            {
                ability.Die();
            }
        }

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        if (_isAnchored)
        {
            _isAnchored = false;
            _anchorInputHeld = false;
            movement.rotationSpeed = _originalRotationSpeed;
            _anchorDragAccumulator = 0f;
            thrusters.invertColors = false;
        }

        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        if (_chromaticFadeCoroutine != null)
        {
            StopCoroutine(_chromaticFadeCoroutine);
            _chromaticFadeCoroutine = null;
        }

        _currentChromaticIntensity = 0f;
        _damageAccumulator = 0f;

        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = 0f;
        }
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

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (abilities.Any(a => a != null && a.HasCollisionModification() == true))
        {
            foreach (var ability in abilities.Where(a => a != null && a.HasCollisionModification() == true))
            {
                ability.ProcessCollisionModification(collider);
            }
        }
    }

    public bool TryProcessIncomingProjectileCollision(Collider2D collider)
    {
        ProjectileScript projectile = collider != null ? collider.GetComponent<ProjectileScript>() : null;
        string originalTargetTag = projectile != null ? projectile.targetTag : string.Empty;
        bool processed = false;

        if (abilities.Any(a => a != null && a.HasCollisionModification()))
        {
            foreach (var ability in abilities.Where(a => a != null && a.HasCollisionModification()))
            {
                ability.ProcessCollisionModification(collider);
                processed = true;

                if (projectile == null)
                {
                    return true;
                }

                if (projectile.targetTag != thisPlayerTag)
                {
                    return true;
                }
            }
        }

        return processed && projectile == null;
    }

    // ===== HUD =====
    private void InitializeHUD()
    {
        if (hud.healthBar != null) hud.healthBar.InitializeBar(currentHealth, maxHealth);
        if (hud.shieldBar != null) hud.shieldBar.InitializeBar(currentShield, maxShield);
        UpdateHUDText();
    }

    private void UpdateHUDText()
    {
        if (hud.healthText != null)
            hud.healthText.text = Mathf.CeilToInt(Mathf.Max(0, currentHealth)).ToString();
        if (hud.shieldText != null)
            hud.shieldText.text = Mathf.CeilToInt(Mathf.Max(0, currentShield)).ToString();
    }

    protected override void OnHealthChanged()
    {
        if (hud.healthBar != null) hud.healthBar.UpdateBar(currentHealth, maxHealth);
        if (hud.healthText != null)
            hud.healthText.text = Mathf.CeilToInt(Mathf.Max(0, currentHealth)).ToString();
    }

    protected override void OnShieldChanged()
    {
        if (hud.shieldBar != null) hud.shieldBar.UpdateBar(currentShield, maxShield);
        if (hud.shieldText != null)
            hud.shieldText.text = Mathf.CeilToInt(Mathf.Max(0, currentShield)).ToString();
    }

    // ===== STAT TRACKING =====
    public void ResetStats()
    {
        shotsFired = 0;
        shotsHit = 0;
        damageDealt = 0f;
        damageTaken = 0f;
        _nextAttackId = 1;
        _registeredHitAttackIds.Clear();
    }

    public bool HasStatsAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        NetMovement netMovement = GetComponent<NetMovement>();
        return netMovement == null || netMovement.IsServer;
    }

    public int BeginTrackedAttack(bool countsTowardAccuracy = true)
    {
        if (!countsTowardAccuracy || !HasStatsAuthority())
        {
            return InvalidAttackId;
        }

        shotsFired++;
        return _nextAttackId++;
    }

    public void RegisterAttackHit(int attackId)
    {
        if (!HasStatsAuthority() || attackId == InvalidAttackId)
        {
            return;
        }

        if (_registeredHitAttackIds.Add(attackId))
        {
            shotsHit++;
        }
    }

    public void RecordDamageDealt(float amount)
    {
        if (!HasStatsAuthority() || amount <= 0f)
        {
            return;
        }

        damageDealt += amount;
    }

    public void RecordDamageTaken(float amount)
    {
        if (!HasStatsAuthority() || amount <= 0f)
        {
            return;
        }

        damageTaken += amount;
    }

    // ===== HUD AUTO-DISCOVERY =====
    public void BindHUD()
    {
        PlayerHUD[] huds = FindObjectsByType<PlayerHUD>(FindObjectsSortMode.None);
        foreach (var ph in huds)
        {
            if (ph.playerTag == thisPlayerTag)
            {
                BindHUD(ph);
                break;
            }
        }
    }

    /// <summary>
    /// Binds directly from a known PlayerHUD reference (avoids FindObjectsByType issues with inactive objects).
    /// </summary>
    public void BindHUD(PlayerHUD ph)
    {
        if (ph == null) return;
        hud.healthBar = ph.healthBar;
        hud.healthText = ph.healthText;
        hud.shieldBar = ph.shieldBar;
        hud.shieldText = ph.shieldText;
        InitializeHUD();
    }

    // ===== ABILITY 4 LOCK/UNLOCK =====
    public virtual void LockAbility4()
    {
        if (ability4 != null) ability4.isLocked = true;
    }

    public virtual void UnlockAbility4()
    {
        if (ability4 != null) ability4.isLocked = false;
        if (_abilityHUDPanel != null) _abilityHUDPanel.Bind(this);
    }
}
