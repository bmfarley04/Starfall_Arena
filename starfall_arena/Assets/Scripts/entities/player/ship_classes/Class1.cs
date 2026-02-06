using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== ABILITY CONFIGURATION STRUCTS =====

[System.Serializable]
public struct BeamAbilityConfig
{
    [Header("Beam Settings")]
    public BeamWeaponConfig stats;
    public float offsetDistance;
    [Tooltip("Rotation speed multiplier when beam is active (0.3 = 70% slower)")]
    public float rotationMultiplier;
    [Tooltip("Duration for beam line renderer to fade in (seconds)")]
    public float fadeInDuration;

    [Header("Beam Capacity")]
    [Tooltip("Maximum beam capacity (100 units)")]
    public float capacity;
    [Tooltip("How fast beam drains (units per second)")]
    public float drainRate;
    [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
    public float regenRate;

    [Header("Sound Effects")]
    public SoundEffect beamLoopSound;
    [Tooltip("Fade in duration for beam sound (seconds)")]
    public float soundFadeDuration;
}

[System.Serializable]
public struct TeleportAbilityConfig
{
    [Header("Timing")]
    [Tooltip("Cooldown between uses (seconds)")]
    public float cooldown;
    [Tooltip("Delay before teleport executes (seconds)")]
    public float preTeleportDelay;
    [Tooltip("Distance to teleport in the direction player is facing")]
    public float teleportDistance;

    [Header("Animation")]
    public AnimationConfig animation;

    [Header("Visual Effects")]
    public VisualConfig visual;

    [Header("Sound Effects")]
    [Tooltip("Teleport exit sound (at origin)")]
    public SoundEffect exitSound;
    [Tooltip("Teleport arrival sound (at destination)")]
    public SoundEffect arrivalSound;

    [System.Serializable]
    public struct AnimationConfig
    {
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
    }

    [System.Serializable]
    public struct VisualConfig
    {
        [Tooltip("Enable chromatic aberration flash on teleport")]
        public bool enableChromaticFlash;
        [Tooltip("Chromatic aberration intensity on teleport")]
        [Range(0f, 1f)]
        public float chromaticFlashIntensity;
        [Tooltip("Enable screen shake on teleport")]
        public bool enableScreenShake;
        [Tooltip("Screen shake strength (force)")]
        public float screenShakeStrength;
        [Tooltip("Particle effects at origin and destination")]
        public GameObject[] effects;
    }
}

[System.Serializable]
public struct GigaBlastAbilityConfig
{
    [Header("Cooldown & Timing")]
    public TimingConfig timing;

    [Header("Charge Tier Thresholds")]
    public TierThresholdsConfig tierThresholds;

    [Header("Movement Penalties per Tier")]
    public MovementPenaltiesConfig movementPenalties;

    [Header("Projectile Scaling per Tier")]
    public ProjectileScalingConfig projectileScaling;

    [Header("Pierce Behavior (Tier 3 & 4)")]
    public PierceConfig pierce;

    [Header("Visual Effects")]
    public VisualConfig visual;

    [Header("Sound Effects")]
    [Tooltip("Charging sound (plays during charge, stops on release)")]
    public SoundEffect chargeSound;
    [Tooltip("Fade in duration for GigaBlast charge sound (seconds)")]
    public float chargeSoundFadeDuration;
    public SoundEffect tier1FireSound;
    public SoundEffect tier2FireSound;
    public SoundEffect tier3FireSound;
    public SoundEffect tier4FireSound;

    [System.Serializable]
    public struct TimingConfig
    {
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Minimum charge time before shot can be released (seconds)")]
        public float minChargeTime;
        [Tooltip("Maximum charge time cap (seconds)")]
        public float maxChargeTime;
        [Tooltip("Projectile lifetime (seconds)")]
        public float projectileLifetime;
    }

    [System.Serializable]
    public struct TierThresholdsConfig
    {
        [Tooltip("Time to reach Tier 1 (seconds)")]
        public float tier1Time;
        [Tooltip("Time to reach Tier 2 (seconds)")]
        public float tier2Time;
        [Tooltip("Time to reach Tier 3 (seconds)")]
        public float tier3Time;
        [Tooltip("Time to reach Tier 4 (seconds)")]
        public float tier4Time;
    }

    [System.Serializable]
    public struct MovementPenaltiesConfig
    {
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
    }

    [System.Serializable]
    public struct ProjectileScalingConfig
    {
        [Header("Tier 1")]
        public float tier1SpeedMultiplier;
        public float tier1DamageMultiplier;
        public float tier1RecoilMultiplier;
        public float tier1ImpactMultiplier;

        [Header("Tier 2")]
        public float tier2SpeedMultiplier;
        public float tier2DamageMultiplier;
        public float tier2RecoilMultiplier;
        public float tier2ImpactMultiplier;

        [Header("Tier 3")]
        public float tier3SpeedMultiplier;
        public float tier3DamageMultiplier;
        public float tier3RecoilMultiplier;
        public float tier3ImpactMultiplier;

        [Header("Tier 4")]
        public float tier4SpeedMultiplier;
        public float tier4DamageMultiplier;
        public float tier4RecoilMultiplier;
        public float tier4ImpactMultiplier;
    }

    [System.Serializable]
    public struct PierceConfig
    {
        [Tooltip("Damage multiplier per pierce for Tier 3 (e.g. 0.8 = 20% reduction per pierce)")]
        public float tier3DamageMultiplierPerPierce;
        [Tooltip("Damage multiplier per pierce for Tier 4 (e.g. 0.9 = 10% reduction per pierce)")]
        public float tier4DamageMultiplierPerPierce;
    }

    [System.Serializable]
    public struct VisualConfig
    {
        [Tooltip("Tier 1 charged projectile prefab (0.5-1s)")]
        public GameObject tier1ProjectilePrefab;
        [Tooltip("Tier 2 charged projectile prefab (1-2s)")]
        public GameObject tier2ProjectilePrefab;
        [Tooltip("Tier 3 charged projectile prefab (2-3s)")]
        public GameObject tier3ProjectilePrefab;
        [Tooltip("Tier 4 charged projectile prefab (3s+)")]
        public GameObject tier4ProjectilePrefab;

        [Tooltip("Tier 1 particle system at ship tip (0.5-1s)")]
        public ParticleSystem tier1ParticleSystem;
        [Tooltip("Tier 2 particle system at ship tip (1-2s)")]
        public ParticleSystem tier2ParticleSystem;
        [Tooltip("Tier 3 particle system at ship tip (2-3s)")]
        public ParticleSystem tier3ParticleSystem;
        [Tooltip("Tier 4 particle system at ship tip (3s+)")]
        public ParticleSystem tier4ParticleSystem;
    }
}

[System.Serializable]
public struct ReflectAbilityConfig
{
    [Header("Timing")]
    [Tooltip("Cooldown between uses (seconds)")]
    public float cooldown;
    [Tooltip("Shield active duration (seconds)")]
    public float activeDuration;

    [Header("Shield")]
    [Tooltip("ReflectShield component (drag from Hierarchy)")]
    public ReflectShield shield;

    [Header("Reflection")]
    [Tooltip("Color of reflected projectiles")]
    public Color reflectedProjectileColor;
    [Tooltip("Damage multiplier for reflected projectiles (1.0 = same damage, 2.0 = double damage)")]
    [Range(0.5f, 5f)]
    public float reflectedProjectileDamageMultiplier;

    [Header("Sound Effects")]
    [Tooltip("Reflect shield duration sound (loops while active)")]
    public SoundEffect shieldLoopSound;
    [Tooltip("Bullet reflection impact sound")]
    public SoundEffect bulletReflectionSound;
}

[System.Serializable]
public struct AbilitiesConfig
{
    [Header("Ability 1 - Beam Weapon")]
    public BeamAbilityConfig beam;

    [Header("Ability 2 - Reflect Shield (Parry)")]
    public ReflectAbilityConfig reflect;

    [Header("Ability 3 - Teleport")]
    public TeleportAbilityConfig teleport;

    [Header("Ability 4 - Giga Blast")]
    public GigaBlastAbilityConfig gigaBlast;
}

// ===== CLASS1 IMPLEMENTATION =====

public class Class1 : Player
{
    // ===== ABILITIES =====
    [Header("Abilities")]
    public AbilitiesConfig abilities;

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
            _currentBeamCapacity = Mathf.Max(_currentBeamCapacity - abilities.beam.regenRate * Time.deltaTime, 0f);
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
        float originalThrustForce = movement.thrustForce;
        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float thrustMultiplier = tier switch
            {
                1 => abilities.gigaBlast.movementPenalties.tier1ThrustMultiplier,
                2 => abilities.gigaBlast.movementPenalties.tier2ThrustMultiplier,
                3 => abilities.gigaBlast.movementPenalties.tier3ThrustMultiplier,
                4 => abilities.gigaBlast.movementPenalties.tier4ThrustMultiplier,
                _ => 1f
            };
            movement.thrustForce *= thrustMultiplier;
        }

        base.FixedUpdate();

        // Restore original thrust force
        movement.thrustForce = originalThrustForce;

        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            ApplyRecoil(recoilForceThisFrame);

            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + abilities.beam.drainRate * Time.fixedDeltaTime, abilities.beam.capacity);

            if (_currentBeamCapacity >= abilities.beam.capacity)
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
    void OnAbility3(InputValue value)
    {
        Debug.Log($"Fire Beam input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            if (_isCharging)
            {
                Debug.Log("Cannot fire beam while charging GigaBlast");
                return;
            }

            if (_currentBeamCapacity >= abilities.beam.capacity)
            {
                Debug.Log("Cannot fire beam: capacity full (overheated)");
                return;
            }

            if (_activeBeam == null && abilities.beam.stats.prefab != null)
            {
                Debug.Log("Creating and starting beam");

                Vector3 spawnPosition = transform.position + transform.up * abilities.beam.offsetDistance;

                GameObject beamObj = Instantiate(abilities.beam.stats.prefab, spawnPosition, transform.rotation, transform);
                _activeBeam = beamObj.GetComponent<LaserBeam>();
                _activeBeam.Initialize(
                    enemyTag,
                    abilities.beam.stats.damagePerSecond,
                    abilities.beam.stats.maxDistance,
                    abilities.beam.stats.recoilForcePerSecond,
                    abilities.beam.stats.impactForce,
                    this
                );
                _activeBeam.StartFiring();

                if (abilities.beam.beamLoopSound != null && _laserBeamSource != null)
                {
                    _laserBeamSource.volume = 0f;
                    abilities.beam.beamLoopSound.Play(_laserBeamSource);

                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(abilities.beam.beamLoopSound.volume));
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

    void OnAbility4()
    {
        if (Time.time < _lastReflectTime + abilities.reflect.cooldown)
        {
            Debug.Log($"Reflect on cooldown: {(_lastReflectTime + abilities.reflect.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (abilities.reflect.shield == null)
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

    void OnAbility2()
    {
        if (Time.time < _lastTeleportTime + abilities.teleport.cooldown)
        {
            Debug.Log($"Teleport on cooldown: {(_lastTeleportTime + abilities.teleport.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (_isTeleporting)
        {
            return;
        }

        Vector3 teleportDirection = transform.up;
        Vector3 targetWorldPosition = transform.position + teleportDirection * abilities.teleport.teleportDistance;
        targetWorldPosition.z = transform.position.z;

        _lastTeleportTime = Time.time;

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
        }
        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetWorldPosition));
    }

    void OnAbility1(InputValue value)
    {
        Debug.Log($"GigaBlast input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            if (!_isCharging && Time.time >= _lastGigaBlastTime + abilities.gigaBlast.timing.cooldown)
            {
                if (_activeBeam != null || _isTeleporting ||
                    (abilities.reflect.shield != null && abilities.reflect.shield.IsActive()))
                {
                    Debug.Log("Cannot charge GigaBlast: other abilities active");
                    return;
                }

                _isCharging = true;
                _chargeStartTime = Time.time;
                _currentChargeTier = 1;

                PlayChargeParticleForTier(1);

                if (abilities.gigaBlast.chargeSound != null && _gigaBlastChargeSource != null)
                {
                    _gigaBlastChargeSource.volume = 0f;
                    abilities.gigaBlast.chargeSound.Play(_gigaBlastChargeSource);

                    if (_gigaBlastChargeFadeCoroutine != null)
                    {
                        StopCoroutine(_gigaBlastChargeFadeCoroutine);
                    }
                    _gigaBlastChargeFadeCoroutine = StartCoroutine(FadeGigaBlastChargeVolume(abilities.gigaBlast.chargeSound.volume));
                }

                Debug.Log("GigaBlast charging started");
            }
        }
        else
        {
            if (_isCharging)
            {
                float chargeTime = Time.time - _chargeStartTime;

                if (chargeTime >= abilities.gigaBlast.timing.minChargeTime)
                {
                    FireChargedShot(chargeTime);
                    _lastGigaBlastTime = Time.time;
                }
                else
                {
                    Debug.Log($"GigaBlast released too early: {chargeTime:F2}s < {abilities.gigaBlast.timing.minChargeTime:F2}s");
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
        abilities.reflect.shield.Activate(abilities.reflect.reflectedProjectileColor);

        if (abilities.reflect.shieldLoopSound != null && _reflectShieldSource != null)
        {
            abilities.reflect.shieldLoopSound.Play(_reflectShieldSource);
        }

        yield return new WaitForSeconds(abilities.reflect.activeDuration);

        abilities.reflect.shield.Deactivate();

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
        Vector3 normalScale = originalScale * abilities.teleport.animation.normalScale;

        Vector3 originSqueezeScale = new Vector3(
            originalScale.x * abilities.teleport.animation.originScaleX,
            originalScale.y * abilities.teleport.animation.originScaleY,
            originalScale.z
        );

        Vector3 destinationPopScale = originalScale * abilities.teleport.animation.destinationOvershootScale;

        Collider2D playerCollider = GetComponent<Collider2D>();
        bool colliderWasEnabled = false;
        if (playerCollider != null)
        {
            colliderWasEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        if (abilities.teleport.preTeleportDelay > 0)
        {
            yield return new WaitForSeconds(abilities.teleport.preTeleportDelay);
        }

        float elapsed = 0f;
        while (elapsed < abilities.teleport.animation.shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / abilities.teleport.animation.shrinkDuration;
            transform.localScale = Vector3.Lerp(originalScale, originSqueezeScale, t);
            yield return null;
        }
        transform.localScale = originSqueezeScale;

        if (abilities.teleport.visual.effects != null && abilities.teleport.visual.effects.Length > 0 &&
            abilities.teleport.visual.effects[0] != null)
        {
            Instantiate(abilities.teleport.visual.effects[0], transform.position, Quaternion.identity);
        }

        if (abilities.teleport.exitSound != null)
        {
            abilities.teleport.exitSound.Play(GetAvailableAudioSource());
        }

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

        if (abilities.teleport.visual.enableChromaticFlash)
        {
            SetChromaticAberrationIntensity(GetChromaticAberrationIntensity() + abilities.teleport.visual.chromaticFlashIntensity);
        }

        if (abilities.teleport.visual.enableScreenShake)
        {
            var impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(abilities.teleport.visual.screenShakeStrength);
            }
        }

        if (abilities.teleport.visual.effects != null && abilities.teleport.visual.effects.Length > 1 &&
            abilities.teleport.visual.effects[1] != null)
        {
            Instantiate(abilities.teleport.visual.effects[1], transform.position, Quaternion.identity);
        }

        if (abilities.teleport.arrivalSound != null)
        {
            abilities.teleport.arrivalSound.Play(GetAvailableAudioSource());
        }

        if (spriteRenderer != null && spriteWasEnabled)
        {
            spriteRenderer.enabled = true;
        }

        elapsed = 0f;
        while (elapsed < abilities.teleport.animation.growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / abilities.teleport.animation.growDuration;
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
            1 => abilities.gigaBlast.visual.tier1ParticleSystem,
            2 => abilities.gigaBlast.visual.tier2ParticleSystem,
            3 => abilities.gigaBlast.visual.tier3ParticleSystem,
            4 => abilities.gigaBlast.visual.tier4ParticleSystem,
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
            1 => abilities.gigaBlast.visual.tier1ParticleSystem,
            2 => abilities.gigaBlast.visual.tier2ParticleSystem,
            3 => abilities.gigaBlast.visual.tier3ParticleSystem,
            4 => abilities.gigaBlast.visual.tier4ParticleSystem,
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
        if (abilities.gigaBlast.visual.tier1ParticleSystem != null) abilities.gigaBlast.visual.tier1ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (abilities.gigaBlast.visual.tier2ParticleSystem != null) abilities.gigaBlast.visual.tier2ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (abilities.gigaBlast.visual.tier3ParticleSystem != null) abilities.gigaBlast.visual.tier3ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (abilities.gigaBlast.visual.tier4ParticleSystem != null) abilities.gigaBlast.visual.tier4ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private int GetChargeTier(float chargeTime)
    {
        if (chargeTime >= abilities.gigaBlast.tierThresholds.tier4Time) return 4;
        if (chargeTime >= abilities.gigaBlast.tierThresholds.tier3Time) return 3;
        if (chargeTime >= abilities.gigaBlast.tierThresholds.tier2Time) return 2;
        if (chargeTime >= abilities.gigaBlast.tierThresholds.tier1Time) return 1;
        return 1;
    }

    private void FireChargedShot(float chargeTime)
    {
        chargeTime = Mathf.Min(chargeTime, abilities.gigaBlast.timing.maxChargeTime);

        int tier = GetChargeTier(chargeTime);

        float baseSpeed = projectileWeapon.speed;
        float baseDamage = projectileWeapon.damage;
        float baseRecoil = projectileWeapon.recoilForce;
        float baseImpact = projectileWeapon.impactForce;

        float speedMultiplier = tier switch
        {
            1 => abilities.gigaBlast.projectileScaling.tier1SpeedMultiplier,
            2 => abilities.gigaBlast.projectileScaling.tier2SpeedMultiplier,
            3 => abilities.gigaBlast.projectileScaling.tier3SpeedMultiplier,
            4 => abilities.gigaBlast.projectileScaling.tier4SpeedMultiplier,
            _ => 1f
        };

        float damageMultiplier = tier switch
        {
            1 => abilities.gigaBlast.projectileScaling.tier1DamageMultiplier,
            2 => abilities.gigaBlast.projectileScaling.tier2DamageMultiplier,
            3 => abilities.gigaBlast.projectileScaling.tier3DamageMultiplier,
            4 => abilities.gigaBlast.projectileScaling.tier4DamageMultiplier,
            _ => 1f
        };

        float recoilMultiplier = tier switch
        {
            1 => abilities.gigaBlast.projectileScaling.tier1RecoilMultiplier,
            2 => abilities.gigaBlast.projectileScaling.tier2RecoilMultiplier,
            3 => abilities.gigaBlast.projectileScaling.tier3RecoilMultiplier,
            4 => abilities.gigaBlast.projectileScaling.tier4RecoilMultiplier,
            _ => 1f
        };

        float impactMultiplier = tier switch
        {
            1 => abilities.gigaBlast.projectileScaling.tier1ImpactMultiplier,
            2 => abilities.gigaBlast.projectileScaling.tier2ImpactMultiplier,
            3 => abilities.gigaBlast.projectileScaling.tier3ImpactMultiplier,
            4 => abilities.gigaBlast.projectileScaling.tier4ImpactMultiplier,
            _ => 1f
        };

        float finalSpeed = baseSpeed * speedMultiplier;
        float finalDamage = baseDamage * damageMultiplier;
        float finalRecoil = baseRecoil * recoilMultiplier;
        float finalImpact = baseImpact * impactMultiplier;

        GameObject projectilePrefab = tier switch
        {
            1 => abilities.gigaBlast.visual.tier1ProjectilePrefab,
            2 => abilities.gigaBlast.visual.tier2ProjectilePrefab,
            3 => abilities.gigaBlast.visual.tier3ProjectilePrefab,
            4 => abilities.gigaBlast.visual.tier4ProjectilePrefab,
            _ => null
        };

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"GigaBlast Tier {tier} projectile prefab not assigned!");
            return;
        }

        Vector3 spawnPosition = transform.position + transform.up * abilities.beam.offsetDistance;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, transform.rotation);

        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = enemyTag;
            projectileScript.Initialize(
                transform.up,
                Vector2.zero,
                finalSpeed,
                finalDamage,
                abilities.gigaBlast.timing.projectileLifetime,
                finalImpact,
                this
            );

            if (tier >= 3)
            {
                float pierceMultiplier = (tier == 3) ? abilities.gigaBlast.pierce.tier3DamageMultiplierPerPierce : abilities.gigaBlast.pierce.tier4DamageMultiplierPerPierce;
                projectileScript.EnablePiercing(pierceMultiplier);
            }
        }

        ApplyRecoil(finalRecoil);

        SoundEffect fireSound = tier switch
        {
            1 => abilities.gigaBlast.tier1FireSound,
            2 => abilities.gigaBlast.tier2FireSound,
            3 => abilities.gigaBlast.tier3FireSound,
            4 => abilities.gigaBlast.tier4FireSound,
            _ => null
        };

        if (fireSound != null)
        {
            fireSound.Play(GetAvailableAudioSource());
        }

        Debug.Log($"GigaBlast fired! Tier: {tier}, Charge: {chargeTime:F2}s, Damage: {finalDamage:F1}, Speed: {finalSpeed:F1}");
    }

    // ===== AUDIO =====
    private System.Collections.IEnumerator FadeBeamVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_laserBeamSource == null) yield break;

        float startVolume = _laserBeamSource.volume;
        float elapsed = 0f;

        while (elapsed < abilities.beam.soundFadeDuration)
        {
            if (_laserBeamSource == null || (!_laserBeamSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / abilities.beam.soundFadeDuration;
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

        while (elapsed < abilities.gigaBlast.chargeSoundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / abilities.gigaBlast.chargeSoundFadeDuration;
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
        float originalRotationSpeed = movement.rotationSpeed;

        if (_activeBeam != null)
        {
            movement.rotationSpeed *= abilities.beam.rotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => abilities.gigaBlast.movementPenalties.tier1RotationMultiplier,
                2 => abilities.gigaBlast.movementPenalties.tier2RotationMultiplier,
                3 => abilities.gigaBlast.movementPenalties.tier3RotationMultiplier,
                4 => abilities.gigaBlast.movementPenalties.tier4RotationMultiplier,
                _ => 1f
            };
            movement.rotationSpeed *= rotationMultiplier;
        }

        base.RotateWithController();

        movement.rotationSpeed = originalRotationSpeed;
    }

    protected override void RotateTowardMouse()
    {
        float originalRotationSpeed = movement.rotationSpeed;

        if (_activeBeam != null)
        {
            movement.rotationSpeed *= abilities.beam.rotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => abilities.gigaBlast.movementPenalties.tier1RotationMultiplier,
                2 => abilities.gigaBlast.movementPenalties.tier2RotationMultiplier,
                3 => abilities.gigaBlast.movementPenalties.tier3RotationMultiplier,
                4 => abilities.gigaBlast.movementPenalties.tier4RotationMultiplier,
                _ => 1f
            };
            movement.rotationSpeed *= rotationMultiplier;
        }

        base.RotateTowardMouse();

        movement.rotationSpeed = originalRotationSpeed;
    }

    // ===== OVERRIDES =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        if (abilities.reflect.shield != null && abilities.reflect.shield.IsActive())
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
        if (abilities.reflect.shield != null && abilities.reflect.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == thisPlayerTag)
            {
                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                abilities.reflect.shield.OnReflectHit(hitPoint);
                abilities.reflect.shield.ReflectProjectile(projectile, enemyTag);

                projectile.MarkAsReflected();

                projectile.ApplyDamageMultiplier(abilities.reflect.reflectedProjectileDamageMultiplier);

                if (abilities.reflect.bulletReflectionSound != null)
                {
                    abilities.reflect.bulletReflectionSound.Play(GetAvailableAudioSource());
                }
            }
        }
    }
}
