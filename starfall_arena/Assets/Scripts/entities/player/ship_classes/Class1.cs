using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Class1 : Player
{
    // ===== BEAM WEAPON =====
    [Header("Beam Weapon Settings")]
    public float beamOffsetDistance = 1f;
    [Tooltip("rotation speed multiplier when beam is active (0.3 = 70% slower)")]
    public float beamRotationMultiplier = 0.3f;
    [Tooltip("Duration for beam line renderer to fade in (seconds)")]
    public float beamFadeInDuration = 0.3f;

    // ===== BEAM CAPACITY =====
    [Header("Beam Capacity")]
    [Tooltip("Maximum beam capacity (100 units)")]
    public float beamCapacity = 100f;
    [Tooltip("How fast beam drains (units per second)")]
    public float beamCapacityDrainRate = 20f;
    [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
    public float beamCapacityRegenRate = 5f;

    // ===== ABILITY SOUND EFFECTS =====
    [Header("Ability Sounds - Beam")]
    [Tooltip("Laser beam sound (loops while beam is active)")]
    public AudioClip laserBeamSound;
    [Tooltip("Fade in duration for beam sound (seconds)")]
    public float beamSoundFadeDuration = 0.1f;

    [Header("Ability Sounds - Teleport")]
    [Tooltip("Teleport exit sound (at origin)")]
    public AudioClip teleportExitSound;
    [Tooltip("Teleport arrival sound (at destination)")]
    public AudioClip teleportArrivalSound;

    [Header("Ability Sounds - GigaBlast")]
    [Tooltip("Charging sound (plays during charge, stops on release)")]
    public AudioClip gigaBlastChargeSound;
    [Tooltip("Tier 1 fire sound")]
    public AudioClip gigaBlastTier1FireSound;
    [Tooltip("Tier 2 fire sound")]
    public AudioClip gigaBlastTier2FireSound;
    [Tooltip("Tier 3 fire sound")]
    public AudioClip gigaBlastTier3FireSound;
    [Tooltip("Tier 4 fire sound")]
    public AudioClip gigaBlastTier4FireSound;
    [Tooltip("Fade in duration for GigaBlast charge sound (seconds)")]
    public float gigaBlastChargeSoundFadeDuration = 0.1f;

    [Header("Ability Sounds - Reflect")]
    [Tooltip("Reflect shield duration sound (loops while active)")]
    public AudioClip reflectShieldLoopSound;
    [Tooltip("Bullet reflection impact sound")]
    public AudioClip bulletReflectionSound;

    // ===== ABILITY CONFIGS =====
    [System.Serializable]
    public struct ReflectAbilityConfig
    {
        [Header("Cooldown & Duration")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Shield active duration (seconds)")]
        public float activeDuration;

        [Header("Visual Settings")]
        [Tooltip("ReflectShield component on player (drag from Hierarchy)")]
        public ReflectShield shield;
        [Tooltip("Color of reflected projectiles")]
        public Color reflectedProjectileColor;

        [Header("Damage Multiplier")]
        [Tooltip("Damage multiplier for reflected projectiles (1.0 = same damage, 2.0 = double damage)")]
        [Range(0.5f, 5f)]
        public float reflectedProjectileDamageMultiplier;
    }

    [Header("Reflect Ability")]
    public ReflectAbilityConfig reflectAbility;

    [System.Serializable]
    public struct TeleportAbilityConfig
    {
        [Header("Cooldown & Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Delay before teleport executes (seconds)")]
        public float preTeleportDelay;

        [Header("Scale Animation")]
        [Tooltip("Shrink duration at origin (seconds)")]
        public float shrinkDuration;
        [Tooltip("Grow duration at destination (seconds)")]
        public float growDuration;
        [Tooltip("Target X scale at origin (squeeze width, e.g. 0.1)")]
        public float originScaleX;
        [Tooltip("Target Y scale at origin (stretch height, e.g. 2.0)")]
        public float originScaleY;
        [Tooltip("Overshoot scale at destination (pop effect, e.g. 1.2)")]
        public float destinationOvershootScale;
        [Tooltip("Normal scale (usually 1.0)")]
        public float normalScale;

        [Header("Physics")]
        [Tooltip("Reset velocity after teleport")]
        public bool resetVelocity;
        [Tooltip("Preserve momentum but reduce magnitude")]
        public bool dampVelocity;
        [Tooltip("Velocity multiplier if dampVelocity true")]
        [Range(0f, 1f)]
        public float velocityDampFactor;

        [Header("Visual Effects")]
        [Tooltip("Enable chromatic aberration flash on teleport")]
        public bool enableChromaticFlash;
        [Tooltip("Chromatic aberration intensity on teleport")]
        [Range(0f, 1f)]
        public float chromaticFlashIntensity;
        [Tooltip("Enable screen shake on teleport")]
        public bool enableScreenShake;
        [Tooltip("Screen shake strength (force)")]
        public float screenShakeStrength;
        [Tooltip("Optional particle effect at origin (leave null for none)")]
        public GameObject teleportOriginEffect;
        [Tooltip("Optional particle effect at destination (leave null for none)")]
        public GameObject teleportDestinationEffect;
    }

    [Header("Teleport Ability")]
    public TeleportAbilityConfig teleportAbility;

    [System.Serializable]
    public struct GigaBlastAbilityConfig
    {
        [Header("Cooldown & Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Minimum charge time before shot can be released (seconds)")]
        public float minChargeTime;
        [Tooltip("Maximum charge time cap (seconds)")]
        public float maxChargeTime;

        [Header("Charge Tier Thresholds")]
        [Tooltip("Time to reach Tier 1 (seconds)")]
        public float tier1Time;
        [Tooltip("Time to reach Tier 2 (seconds)")]
        public float tier2Time;
        [Tooltip("Time to reach Tier 3 (seconds)")]
        public float tier3Time;
        [Tooltip("Time to reach Tier 4 (seconds)")]
        public float tier4Time;

        [Header("Particle Colors per Tier")]
        [Tooltip("Particle color at Tier 1 (0.5-1s)")]
        public Color tier1Color;
        [Tooltip("Particle color at Tier 2 (1-2s)")]
        public Color tier2Color;
        [Tooltip("Particle color at Tier 3 (2-3s)")]
        public Color tier3Color;
        [Tooltip("Particle color at Tier 4 (3s+)")]
        public Color tier4Color;

        [Header("Movement Penalties per Tier")]
        [Tooltip("Thrust speed multiplier for Tier 1")]
        [Range(0f, 1f)]
        public float tier1ThrustMultiplier;
        [Tooltip("Rotation speed multiplier for Tier 1")]
        [Range(0f, 1f)]
        public float tier1RotationMultiplier;
        [Tooltip("Thrust speed multiplier for Tier 2")]
        [Range(0f, 1f)]
        public float tier2ThrustMultiplier;
        [Tooltip("Rotation speed multiplier for Tier 2")]
        [Range(0f, 1f)]
        public float tier2RotationMultiplier;
        [Tooltip("Thrust speed multiplier for Tier 3")]
        [Range(0f, 1f)]
        public float tier3ThrustMultiplier;
        [Tooltip("Rotation speed multiplier for Tier 3")]
        [Range(0f, 1f)]
        public float tier3RotationMultiplier;
        [Tooltip("Thrust speed multiplier for Tier 4")]
        [Range(0f, 1f)]
        public float tier4ThrustMultiplier;
        [Tooltip("Rotation speed multiplier for Tier 4")]
        [Range(0f, 1f)]
        public float tier4RotationMultiplier;

        [Header("Projectile Scaling - Tier Multipliers")]
        [Tooltip("Tier 1 speed multiplier (base projectile speed * this)")]
        public float tier1SpeedMultiplier;
        [Tooltip("Tier 2 speed multiplier")]
        public float tier2SpeedMultiplier;
        [Tooltip("Tier 3 speed multiplier")]
        public float tier3SpeedMultiplier;
        [Tooltip("Tier 4 speed multiplier")]
        public float tier4SpeedMultiplier;
        [Tooltip("Tier 1 damage multiplier (base projectile damage * this)")]
        public float tier1DamageMultiplier;
        [Tooltip("Tier 2 damage multiplier")]
        public float tier2DamageMultiplier;
        [Tooltip("Tier 3 damage multiplier")]
        public float tier3DamageMultiplier;
        [Tooltip("Tier 4 damage multiplier")]
        public float tier4DamageMultiplier;
        [Tooltip("Tier 1 recoil force multiplier (base recoil * this)")]
        public float tier1RecoilMultiplier;
        [Tooltip("Tier 2 recoil force multiplier")]
        public float tier2RecoilMultiplier;
        [Tooltip("Tier 3 recoil force multiplier")]
        public float tier3RecoilMultiplier;
        [Tooltip("Tier 4 recoil force multiplier")]
        public float tier4RecoilMultiplier;
        [Tooltip("Tier 1 impact force multiplier (base impact * this)")]
        public float tier1ImpactMultiplier;
        [Tooltip("Tier 2 impact force multiplier")]
        public float tier2ImpactMultiplier;
        [Tooltip("Tier 3 impact force multiplier")]
        public float tier3ImpactMultiplier;
        [Tooltip("Tier 4 impact force multiplier")]
        public float tier4ImpactMultiplier;

        [Header("Pierce Behavior")]
        [Tooltip("Damage multiplier for Tier 3 pierce (0 = no pierce)")]
        public float tier3PierceMultiplier;
        [Tooltip("Damage multiplier for Tier 4 pierce (0 = no pierce)")]
        public float tier4PierceMultiplier;

        [Header("Visual Effects")]
        [Tooltip("Tier 1 charged projectile prefab (0.5-1s)")]
        public GameObject tier1ProjectilePrefab;
        [Tooltip("Tier 2 charged projectile prefab (1-2s)")]
        public GameObject tier2ProjectilePrefab;
        [Tooltip("Tier 3 charged projectile prefab (2-3s)")]
        public GameObject tier3ProjectilePrefab;
        [Tooltip("Tier 4 charged projectile prefab (3s+)")]
        public GameObject tier4ProjectilePrefab;
        [Tooltip("Tier 1 particle system at ship tip (0.5-1s)")]
        public ParticleSystem tier1ParticleEffect;
        [Tooltip("Tier 2 particle system at ship tip (1-2s)")]
        public ParticleSystem tier2ParticleEffect;
        [Tooltip("Tier 3 particle system at ship tip (2-3s)")]
        public ParticleSystem tier3ParticleEffect;
        [Tooltip("Tier 4 particle system at ship tip (3s+)")]
        public ParticleSystem tier4ParticleEffect;
        [Tooltip("Projectile lifetime (seconds)")]
        public float projectileLifetime;
    }

    [Header("GigaBlast Ability")]
    public GigaBlastAbilityConfig gigaBlastAbility;

    // ===== AUDIO VOLUME =====
    [System.Serializable]
    public struct AbilityAudioVolumeConfig
    {
        [Header("Beam")]
        [Range(0f, 3f)]
        [Tooltip("Volume for laser beam sound")]
        public float laserBeamVolume;

        [Header("Teleport")]
        [Range(0f, 3f)]
        [Tooltip("Volume for teleport exit sound")]
        public float teleportExitVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for teleport arrival sound")]
        public float teleportArrivalVolume;

        [Header("GigaBlast")]
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast charge sound")]
        public float gigaBlastChargeVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 1 fire sound")]
        public float gigaBlastTier1FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 2 fire sound")]
        public float gigaBlastTier2FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 3 fire sound")]
        public float gigaBlastTier3FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 4 fire sound")]
        public float gigaBlastTier4FireVolume;
        [Range(0.5f, 2f)]
        [Tooltip("Pitch multiplier for GigaBlast Tier 3 charge sound")]
        public float gigaBlastTier3ChargePitch;
        [Range(0.5f, 2f)]
        [Tooltip("Pitch multiplier for GigaBlast Tier 4 charge sound")]
        public float gigaBlastTier4ChargePitch;

        [Header("Reflect")]
        [Range(0f, 3f)]
        [Tooltip("Volume for reflect shield loop sound")]
        public float reflectShieldLoopVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for bullet reflection impact sound")]
        public float bulletReflectionVolume;
    }

    [Header("Ability Audio Volume Controls")]
    public AbilityAudioVolumeConfig abilityAudioVolume = new AbilityAudioVolumeConfig
    {
        laserBeamVolume = 0.7f,
        teleportExitVolume = 0.7f,
        teleportArrivalVolume = 0.7f,
        gigaBlastChargeVolume = 0.7f,
        gigaBlastTier1FireVolume = 0.7f,
        gigaBlastTier2FireVolume = 0.7f,
        gigaBlastTier3FireVolume = 0.7f,
        gigaBlastTier4FireVolume = 0.7f,
        reflectShieldLoopVolume = 0.7f,
        bulletReflectionVolume = 0.7f,
        gigaBlastTier3ChargePitch = 1f,
        gigaBlastTier4ChargePitch = 1f
    };

    // ===== PRIVATE STATE =====
    private LaserBeam _activeBeam;
    private float _currentBeamCapacity;
    private float _lastReflectTime = -999f;
    private Coroutine _reflectCoroutine;
    private float _lastTeleportTime = -999f;
    private Coroutine _teleportCoroutine;
    private bool _isTeleporting = false;
    private float _lastGigaBlastTime = -999f;
    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private int _currentChargeTier = 0;
    private AudioSource _laserBeamSource;
    private AudioSource _reflectShieldSource;
    private AudioSource _gigaBlastChargeSource;
    private Coroutine _beamFadeCoroutine;
    private Coroutine _gigaBlastChargeFadeCoroutine;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();

        _currentBeamCapacity = 0f;

        _laserBeamSource = gameObject.AddComponent<AudioSource>();
        _laserBeamSource.playOnAwake = false;
        _laserBeamSource.loop = true;
        _laserBeamSource.spatialBlend = 1f;
        _laserBeamSource.rolloffMode = AudioRolloffMode.Linear;
        _laserBeamSource.minDistance = 10f;
        _laserBeamSource.maxDistance = 50f;
        _laserBeamSource.dopplerLevel = 0f;

        _reflectShieldSource = gameObject.AddComponent<AudioSource>();
        _reflectShieldSource.playOnAwake = false;
        _reflectShieldSource.loop = true;
        _reflectShieldSource.spatialBlend = 1f;
        _reflectShieldSource.rolloffMode = AudioRolloffMode.Linear;
        _reflectShieldSource.minDistance = 10f;
        _reflectShieldSource.maxDistance = 50f;
        _reflectShieldSource.dopplerLevel = 0f;

        _gigaBlastChargeSource = gameObject.AddComponent<AudioSource>();
        _gigaBlastChargeSource.playOnAwake = false;
        _gigaBlastChargeSource.loop = true;
        _gigaBlastChargeSource.spatialBlend = 1f;
        _gigaBlastChargeSource.rolloffMode = AudioRolloffMode.Linear;
        _gigaBlastChargeSource.minDistance = 10f;
        _gigaBlastChargeSource.maxDistance = 50f;
        _gigaBlastChargeSource.dopplerLevel = 0f;
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();

        if (_activeBeam == null && _currentBeamCapacity > 0f)
        {
            _currentBeamCapacity = Mathf.Max(_currentBeamCapacity - beamCapacityRegenRate * Time.deltaTime, 0f);
        }

        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int newTier = GetChargeTier(chargeTime);

            if (newTier != _currentChargeTier)
            {
                StopCurrentChargeParticle();
                _currentChargeTier = newTier;
                PlayChargeParticleForTier(newTier);
            }
        }
    }

    protected override void FixedUpdate()
    {
        if (_isTeleporting)
        {
            return;
        }

        // Apply movement penalty for charging GigaBlast
        float originalThrustForce = thrustForce;
        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float thrustMultiplier = tier switch
            {
                1 => gigaBlastAbility.tier1ThrustMultiplier,
                2 => gigaBlastAbility.tier2ThrustMultiplier,
                3 => gigaBlastAbility.tier3ThrustMultiplier,
                4 => gigaBlastAbility.tier4ThrustMultiplier,
                _ => 1f
            };
            thrustForce *= thrustMultiplier;
        }

        base.FixedUpdate();

        // Restore original thrust force
        thrustForce = originalThrustForce;

        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            ApplyRecoil(recoilForceThisFrame);

            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + beamCapacityDrainRate * Time.fixedDeltaTime, beamCapacity);

            if (_currentBeamCapacity >= beamCapacity)
            {
                Debug.Log("Beam capacity full! Stopping beam.");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnFireBeam(InputValue value)
    {
        Debug.Log($"Fire Beam input received - isPressed: {value.isPressed}");

        if (sceneManager != null && sceneManager.IsPaused() && value.isPressed)
            return;

        if (value.isPressed)
        {
            if (_isCharging)
            {
                Debug.Log("Cannot fire beam while charging GigaBlast");
                return;
            }

            if (_currentBeamCapacity >= beamCapacity)
            {
                Debug.Log("Cannot fire beam: capacity full (overheated)");
                return;
            }

            if (_activeBeam == null && beamWeapon.prefab != null)
            {
                Debug.Log("Creating and starting beam");

                Vector3 spawnPosition = transform.position + transform.up * beamOffsetDistance;

                GameObject beamObj = Instantiate(beamWeapon.prefab, spawnPosition, transform.rotation, transform);
                _activeBeam = beamObj.GetComponent<LaserBeam>();
                _activeBeam.Initialize(
                    "Enemy",
                    beamWeapon.damagePerSecond,
                    beamWeapon.maxDistance,
                    beamWeapon.recoilForcePerSecond,
                    beamWeapon.impactForce,
                    this
                );
                _activeBeam.StartFiring();

                if (laserBeamSound != null && _laserBeamSource != null)
                {
                    _laserBeamSource.clip = laserBeamSound;
                    _laserBeamSource.volume = 0f;
                    _laserBeamSource.Play();

                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(abilityAudioVolume.laserBeamVolume));
                }
            }
        }
        else
        {
            if (_activeBeam != null)
            {
                Debug.Log("Stopping and destroying beam");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }

    void OnReflect()
    {
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        if (Time.time < _lastReflectTime + reflectAbility.cooldown)
        {
            Debug.Log($"Reflect on cooldown: {(_lastReflectTime + reflectAbility.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (reflectAbility.shield == null)
        {
            Debug.LogWarning("Reflect shield not assigned!");
            return;
        }

        _lastReflectTime = Time.time;

        if (_reflectCoroutine != null)
        {
            StopCoroutine(_reflectCoroutine);
        }
        _reflectCoroutine = StartCoroutine(ActivateReflectShield());
    }

    void OnTeleport()
    {
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        if (Time.time < _lastTeleportTime + teleportAbility.cooldown)
        {
            Debug.Log($"Teleport on cooldown: {(_lastTeleportTime + teleportAbility.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (_isTeleporting)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 targetWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        targetWorldPosition.z = transform.position.z;

        _lastTeleportTime = Time.time;

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
        }
        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetWorldPosition));
    }

    void OnGigaBlast(InputValue value)
    {
        Debug.Log($"GigaBlast input received - isPressed: {value.isPressed}");

        if (sceneManager != null && sceneManager.IsPaused() && value.isPressed)
            return;

        if (value.isPressed)
        {
            if (!_isCharging && Time.time >= _lastGigaBlastTime + gigaBlastAbility.cooldown)
            {
                if (_activeBeam != null || _isTeleporting ||
                    (reflectAbility.shield != null && reflectAbility.shield.IsActive()))
                {
                    Debug.Log("Cannot charge GigaBlast: other abilities active");
                    return;
                }

                _isCharging = true;
                _chargeStartTime = Time.time;
                _currentChargeTier = 1;

                PlayChargeParticleForTier(1);

                if (gigaBlastChargeSound != null && _gigaBlastChargeSource != null)
                {
                    _gigaBlastChargeSource.clip = gigaBlastChargeSound;
                    _gigaBlastChargeSource.volume = 0f;
                    _gigaBlastChargeSource.Play();

                    if (_gigaBlastChargeFadeCoroutine != null)
                    {
                        StopCoroutine(_gigaBlastChargeFadeCoroutine);
                    }
                    _gigaBlastChargeFadeCoroutine = StartCoroutine(FadeGigaBlastChargeVolume(abilityAudioVolume.gigaBlastChargeVolume));
                }

                Debug.Log("GigaBlast charging started");
            }
        }
        else
        {
            if (_isCharging)
            {
                float chargeTime = Time.time - _chargeStartTime;

                if (chargeTime >= gigaBlastAbility.minChargeTime)
                {
                    FireChargedShot(chargeTime);
                    _lastGigaBlastTime = Time.time;
                }
                else
                {
                    Debug.Log($"GigaBlast released too early: {chargeTime:F2}s < {gigaBlastAbility.minChargeTime:F2}s");
                }

                _isCharging = false;
                _currentChargeTier = 0;
                StopAllChargeParticles();

                if (_gigaBlastChargeSource != null && _gigaBlastChargeSource.isPlaying)
                {
                    if (_gigaBlastChargeFadeCoroutine != null)
                    {
                        StopCoroutine(_gigaBlastChargeFadeCoroutine);
                    }
                    _gigaBlastChargeFadeCoroutine = StartCoroutine(FadeGigaBlastChargeVolume(0f, stopAfterFade: true));
                }
            }
        }
    }

    // ===== REFLECT ABILITY =====
    private System.Collections.IEnumerator ActivateReflectShield()
    {
        reflectAbility.shield.Activate(reflectAbility.reflectedProjectileColor);

        if (reflectShieldLoopSound != null && _reflectShieldSource != null)
        {
            _reflectShieldSource.clip = reflectShieldLoopSound;
            _reflectShieldSource.volume = abilityAudioVolume.reflectShieldLoopVolume;
            _reflectShieldSource.Play();
        }

        yield return new WaitForSeconds(reflectAbility.activeDuration);

        reflectAbility.shield.Deactivate();

        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
    }

    // ===== TELEPORT ABILITY =====
    private System.Collections.IEnumerator ExecuteTeleport(Vector3 targetPosition)
    {
        _isTeleporting = true;

        Vector3 originalScale = transform.localScale;
        Vector3 normalScale = originalScale * teleportAbility.normalScale;

        Vector3 originSqueezeScale = new Vector3(
            originalScale.x * teleportAbility.originScaleX,
            originalScale.y * teleportAbility.originScaleY,
            originalScale.z
        );

        Vector3 destinationPopScale = originalScale * teleportAbility.destinationOvershootScale;

        Collider2D playerCollider = GetComponent<Collider2D>();
        bool colliderWasEnabled = false;
        if (playerCollider != null)
        {
            colliderWasEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        if (teleportAbility.preTeleportDelay > 0)
        {
            yield return new WaitForSeconds(teleportAbility.preTeleportDelay);
        }

        float elapsed = 0f;
        while (elapsed < teleportAbility.shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleportAbility.shrinkDuration;
            transform.localScale = Vector3.Lerp(originalScale, originSqueezeScale, t);
            yield return null;
        }
        transform.localScale = originSqueezeScale;

        if (teleportAbility.teleportOriginEffect != null)
        {
            Instantiate(teleportAbility.teleportOriginEffect, transform.position, Quaternion.identity);
        }

        PlayAbilitySound(teleportExitSound, 1f, AbilityAudioClipType.TeleportExit);

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bool spriteWasEnabled = false;
        if (spriteRenderer != null)
        {
            spriteWasEnabled = spriteRenderer.enabled;
            spriteRenderer.enabled = false;
        }

        Vector3 previousPosition = transform.position;
        transform.position = targetPosition;

        var cinemachineCameras = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cinemachineCameras)
        {
            if (cam.Target.TrackingTarget == transform)
            {
                cam.OnTargetObjectWarped(transform, targetPosition - previousPosition);
            }
        }

        if (teleportAbility.resetVelocity)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        else if (teleportAbility.dampVelocity)
        {
            _rb.linearVelocity *= teleportAbility.velocityDampFactor;
        }

        if (teleportAbility.enableChromaticFlash)
        {
            SetChromaticAberrationIntensity(GetChromaticAberrationIntensity() + teleportAbility.chromaticFlashIntensity);
        }

        if (teleportAbility.enableScreenShake)
        {
            var impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(teleportAbility.screenShakeStrength);
            }
        }

        if (teleportAbility.teleportDestinationEffect != null)
        {
            Instantiate(teleportAbility.teleportDestinationEffect, transform.position, Quaternion.identity);
        }

        PlayAbilitySound(teleportArrivalSound, 1f, AbilityAudioClipType.TeleportArrival);

        if (spriteRenderer != null && spriteWasEnabled)
        {
            spriteRenderer.enabled = true;
        }

        elapsed = 0f;
        while (elapsed < teleportAbility.growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleportAbility.growDuration;
            transform.localScale = Vector3.Lerp(destinationPopScale, normalScale, t);
            yield return null;
        }
        transform.localScale = normalScale;

        if (playerCollider != null && colliderWasEnabled)
        {
            playerCollider.enabled = true;
        }

        _isTeleporting = false;
    }

    // ===== GIGABLAST ABILITY =====
    private void PlayChargeParticleForTier(int tier)
    {
        ParticleSystem particleToPlay = tier switch
        {
            1 => gigaBlastAbility.tier1ParticleEffect,
            2 => gigaBlastAbility.tier2ParticleEffect,
            3 => gigaBlastAbility.tier3ParticleEffect,
            4 => gigaBlastAbility.tier4ParticleEffect,
            _ => null
        };

        if (particleToPlay != null)
        {
            Debug.Log($"Playing Tier {tier} particle effect: {particleToPlay.name}");
            particleToPlay.Play();
        }
        else
        {
            Debug.LogWarning($"Tier {tier} particle effect is null!");
        }
    }

    private void StopCurrentChargeParticle()
    {
        ParticleSystem particleToStop = _currentChargeTier switch
        {
            1 => gigaBlastAbility.tier1ParticleEffect,
            2 => gigaBlastAbility.tier2ParticleEffect,
            3 => gigaBlastAbility.tier3ParticleEffect,
            4 => gigaBlastAbility.tier4ParticleEffect,
            _ => null
        };

        if (particleToStop != null)
        {
            Debug.Log($"Stopping Tier {_currentChargeTier} particle effect: {particleToStop.name}");
            particleToStop.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StopAllChargeParticles()
    {
        Debug.Log("Stopping all GigaBlast charge particles");
        if (gigaBlastAbility.tier1ParticleEffect != null) gigaBlastAbility.tier1ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier2ParticleEffect != null) gigaBlastAbility.tier2ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier3ParticleEffect != null) gigaBlastAbility.tier3ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier4ParticleEffect != null) gigaBlastAbility.tier4ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private int GetChargeTier(float chargeTime)
    {
        if (chargeTime >= gigaBlastAbility.tier4Time) return 4;
        if (chargeTime >= gigaBlastAbility.tier3Time) return 3;
        if (chargeTime >= gigaBlastAbility.tier2Time) return 2;
        if (chargeTime >= gigaBlastAbility.tier1Time) return 1;
        return 1;
    }

    private void FireChargedShot(float chargeTime)
    {
        chargeTime = Mathf.Min(chargeTime, gigaBlastAbility.maxChargeTime);

        int tier = GetChargeTier(chargeTime);

        float baseSpeed = projectileWeapon.speed;
        float baseDamage = projectileWeapon.damage;
        float baseRecoil = projectileWeapon.recoilForce;
        float baseImpact = projectileWeapon.impactForce;

        float speedMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1SpeedMultiplier,
            2 => gigaBlastAbility.tier2SpeedMultiplier,
            3 => gigaBlastAbility.tier3SpeedMultiplier,
            4 => gigaBlastAbility.tier4SpeedMultiplier,
            _ => 1f
        };

        float damageMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1DamageMultiplier,
            2 => gigaBlastAbility.tier2DamageMultiplier,
            3 => gigaBlastAbility.tier3DamageMultiplier,
            4 => gigaBlastAbility.tier4DamageMultiplier,
            _ => 1f
        };

        float recoilMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1RecoilMultiplier,
            2 => gigaBlastAbility.tier2RecoilMultiplier,
            3 => gigaBlastAbility.tier3RecoilMultiplier,
            4 => gigaBlastAbility.tier4RecoilMultiplier,
            _ => 1f
        };

        float impactMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1ImpactMultiplier,
            2 => gigaBlastAbility.tier2ImpactMultiplier,
            3 => gigaBlastAbility.tier3ImpactMultiplier,
            4 => gigaBlastAbility.tier4ImpactMultiplier,
            _ => 1f
        };

        float finalSpeed = baseSpeed * speedMultiplier;
        float finalDamage = baseDamage * damageMultiplier;
        float finalRecoil = baseRecoil * recoilMultiplier;
        float finalImpact = baseImpact * impactMultiplier;

        GameObject projectilePrefab = tier switch
        {
            1 => gigaBlastAbility.tier1ProjectilePrefab,
            2 => gigaBlastAbility.tier2ProjectilePrefab,
            3 => gigaBlastAbility.tier3ProjectilePrefab,
            4 => gigaBlastAbility.tier4ProjectilePrefab,
            _ => null
        };

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"GigaBlast Tier {tier} projectile prefab not assigned!");
            return;
        }

        Vector3 spawnPosition = transform.position + transform.up * beamOffsetDistance;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, transform.rotation);

        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = "Enemy";
            projectileScript.Initialize(
                transform.up,
                Vector2.zero,
                finalSpeed,
                finalDamage,
                gigaBlastAbility.projectileLifetime,
                finalImpact,
                this
            );

            if (tier >= 3)
            {
                float pierceMultiplier = (tier == 3) ? gigaBlastAbility.tier3PierceMultiplier : gigaBlastAbility.tier4PierceMultiplier;
                projectileScript.EnablePiercing(pierceMultiplier);
            }
        }

        ApplyRecoil(finalRecoil);

        AudioClip fireSound = tier switch
        {
            1 => gigaBlastTier1FireSound,
            2 => gigaBlastTier2FireSound,
            3 => gigaBlastTier3FireSound,
            4 => gigaBlastTier4FireSound,
            _ => null
        };
        AbilityAudioClipType fireSoundType = tier switch
        {
            1 => AbilityAudioClipType.GigaBlastTier1Fire,
            2 => AbilityAudioClipType.GigaBlastTier2Fire,
            3 => AbilityAudioClipType.GigaBlastTier3Fire,
            4 => AbilityAudioClipType.GigaBlastTier4Fire,
            _ => AbilityAudioClipType.LaserBeam
        };
        float fireSoundPitch = tier switch
        {
            3 => abilityAudioVolume.gigaBlastTier3ChargePitch,
            4 => abilityAudioVolume.gigaBlastTier4ChargePitch,
            _ => 1f
        };
        PlayAbilitySound(fireSound, 1f, fireSoundType, fireSoundPitch);

        Debug.Log($"GigaBlast fired! Tier: {tier}, Charge: {chargeTime:F2}s, Damage: {finalDamage:F1}, Speed: {finalSpeed:F1}");
    }

    // ===== AUDIO =====
    protected enum AbilityAudioClipType
    {
        LaserBeam,
        TeleportExit,
        TeleportArrival,
        GigaBlastTier1Fire,
        GigaBlastTier2Fire,
        GigaBlastTier3Fire,
        GigaBlastTier4Fire,
        BulletReflection
    }

    private void PlayAbilitySound(AudioClip clip, float volume = 1f, AbilityAudioClipType clipType = AbilityAudioClipType.LaserBeam, float pitch = 1f)
    {
        if (clip == null) return;

        float volumeMultiplier = clipType switch
        {
            AbilityAudioClipType.LaserBeam => abilityAudioVolume.laserBeamVolume,
            AbilityAudioClipType.TeleportExit => abilityAudioVolume.teleportExitVolume,
            AbilityAudioClipType.TeleportArrival => abilityAudioVolume.teleportArrivalVolume,
            AbilityAudioClipType.GigaBlastTier1Fire => abilityAudioVolume.gigaBlastTier1FireVolume,
            AbilityAudioClipType.GigaBlastTier2Fire => abilityAudioVolume.gigaBlastTier2FireVolume,
            AbilityAudioClipType.GigaBlastTier3Fire => abilityAudioVolume.gigaBlastTier3FireVolume,
            AbilityAudioClipType.GigaBlastTier4Fire => abilityAudioVolume.gigaBlastTier4FireVolume,
            AbilityAudioClipType.BulletReflection => abilityAudioVolume.bulletReflectionVolume,
            _ => 1f
        };

        AudioSource source = GetAvailableAudioSource();
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * volumeMultiplier);
    }

    private System.Collections.IEnumerator FadeBeamVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_laserBeamSource == null) yield break;

        float startVolume = _laserBeamSource.volume;
        float elapsed = 0f;

        while (elapsed < beamSoundFadeDuration)
        {
            if (_laserBeamSource == null || (!_laserBeamSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / beamSoundFadeDuration;
            _laserBeamSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_laserBeamSource == null) yield break;

        _laserBeamSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
    }

    private System.Collections.IEnumerator FadeGigaBlastChargeVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_gigaBlastChargeSource == null) yield break;

        float startVolume = _gigaBlastChargeSource.volume;
        float elapsed = 0f;

        while (elapsed < gigaBlastChargeSoundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / gigaBlastChargeSoundFadeDuration;
            _gigaBlastChargeSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        _gigaBlastChargeSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade)
        {
            _gigaBlastChargeSource.Stop();
        }
    }

    // ===== ROTATION OVERRIDES =====
    protected override void RotateWithController()
    {
        float originalRotationSpeed = rotationSpeed;

        if (_activeBeam != null)
        {
            rotationSpeed *= beamRotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => gigaBlastAbility.tier1RotationMultiplier,
                2 => gigaBlastAbility.tier2RotationMultiplier,
                3 => gigaBlastAbility.tier3RotationMultiplier,
                4 => gigaBlastAbility.tier4RotationMultiplier,
                _ => 1f
            };
            rotationSpeed *= rotationMultiplier;
        }

        base.RotateWithController();

        rotationSpeed = originalRotationSpeed;
    }

    protected override void RotateTowardMouse()
    {
        float originalRotationSpeed = rotationSpeed;

        if (_activeBeam != null)
        {
            rotationSpeed *= beamRotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => gigaBlastAbility.tier1RotationMultiplier,
                2 => gigaBlastAbility.tier2RotationMultiplier,
                3 => gigaBlastAbility.tier3RotationMultiplier,
                4 => gigaBlastAbility.tier4RotationMultiplier,
                _ => 1f
            };
            rotationSpeed *= rotationMultiplier;
        }

        base.RotateTowardMouse();

        rotationSpeed = originalRotationSpeed;
    }

    // ===== OVERRIDES =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        if (reflectAbility.shield != null && reflectAbility.shield.IsActive())
        {
            return;
        }

        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        if (_laserBeamSource != null && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
        if (_gigaBlastChargeSource != null && _gigaBlastChargeSource.isPlaying)
        {
            _gigaBlastChargeSource.Stop();
        }

        if (_beamFadeCoroutine != null)
        {
            StopCoroutine(_beamFadeCoroutine);
        }
        if (_gigaBlastChargeFadeCoroutine != null)
        {
            StopCoroutine(_gigaBlastChargeFadeCoroutine);
        }

        base.Die();
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (reflectAbility.shield != null && reflectAbility.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == "Player")
            {
                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                reflectAbility.shield.OnReflectHit(hitPoint);
                reflectAbility.shield.ReflectProjectile(projectile);

                projectile.MarkAsReflected();

                projectile.ApplyDamageMultiplier(reflectAbility.reflectedProjectileDamageMultiplier);

                PlayAbilitySound(bulletReflectionSound, 1f, AbilityAudioClipType.BulletReflection);
            }
        }
    }
}
