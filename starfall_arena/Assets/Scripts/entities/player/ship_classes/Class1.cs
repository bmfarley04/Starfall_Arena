using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== ABILITY CONFIGURATION STRUCTS =====

[System.Serializable]
public struct GigaBlastAbilityConfig
{
    public float offsetDistance;

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
    [Header("Ability 2 - Reflect Shield (Parry)")]
    public ReflectAbilityConfig reflect;

    [Header("Ability 4 - Giga Blast")]
    public GigaBlastAbilityConfig gigaBlast;
}

// ===== CLASS1 IMPLEMENTATION =====

public class Class1 : Player
{
    // ===== ABILITIES =====
    [Header("Abilities")]
    public AbilitiesConfig abilities;

    // ===== PRIVATE SET STATE =====
    public bool _isCharging { get; private set; } = false;

    // ===== PRIVATE STATE =====
    private float _lastReflectTime = -999f;
    private Coroutine _reflectCoroutine;
    private float _lastGigaBlastTime = -999f;
    private float _chargeStartTime = 0f;
    private int _currentChargeTier = 0;
    private AudioSource _reflectShieldSource;
    private AudioSource _gigaBlastChargeSource;
    private Coroutine _gigaBlastChargeFadeCoroutine;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();

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
        if (ability2.IsAbilityActive()) // This assumes ability2 is the teleport ability; adjust if needed
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
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnAbility3(InputValue value)
    {
        ability3.TryUseAbility(value);
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
        ability2.TryUseAbility();
    }

    void OnAbility1(InputValue value)
    {
        Debug.Log($"GigaBlast input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            if (!_isCharging && Time.time >= _lastGigaBlastTime + abilities.gigaBlast.timing.cooldown)
            {
                if (ability3.IsAbilityActive() || ability2.IsAbilityActive() ||
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

        Vector3 spawnPosition = transform.position + transform.up * abilities.gigaBlast.offsetDistance;
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

        List<Ability> abilityScripts = new List<Ability> { ability1, ability2, ability3, ability4 };
        var activeAbility = abilityScripts.FirstOrDefault(a => a?.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyRotationMultiplier();
        }
        else 
        if (_isCharging)
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

        List<Ability> abilityScripts = new List<Ability> {ability1,ability2,ability3,ability4};
        var activeAbility = abilityScripts.FirstOrDefault(a => a.IsAbilityActive());
        if (activeAbility != null)
        {
            activeAbility.ApplyRotationMultiplier();
        }
        else
        if (_isCharging)
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
        ability3.Die();

        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
        if (_gigaBlastChargeSource != null && _gigaBlastChargeSource.isPlaying)
        {
            _gigaBlastChargeSource.Stop();
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
