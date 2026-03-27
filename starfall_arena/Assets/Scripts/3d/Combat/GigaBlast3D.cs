using UnityEngine;
using UnityEngine.InputSystem;

public class GigaBlast3D : Ability3D
{
    [System.Serializable]
    public struct GigaBlastAbilityConfig3D
    {
        [Header("Timing")]
        public TimingConfig timing;

        [Header("Charge Tier Thresholds")]
        public TierThresholdsConfig tierThresholds;

        [Header("Movement Penalties Per Tier")]
        public MovementPenaltiesConfig movementPenalties;

        [Header("Projectile Scaling Per Tier")]
        public ProjectileScalingConfig projectileScaling;

        [Header("Pierce Behavior")]
        public PierceConfig pierce;

        [Header("Visual Effects")]
        public VisualConfig visual;

        [Header("Spawn")]
        [Tooltip("Forward offset from the spawn anchor along its local forward axis.")]
        public float offsetDistance;
        [Tooltip("Vertical offset from the spawn anchor along its local up axis.")]
        public float verticalOffset;
        [Tooltip("Optional single spawn anchor. Falls back to the entity transform if unset.")]
        public Transform muzzle;

        [Header("Sound Effects")]
        public SoundEffect chargeSound;
        public float chargeSoundFadeDuration;
        public SoundEffect tier1FireSound;
        public SoundEffect tier2FireSound;
        public SoundEffect tier3FireSound;
        public SoundEffect tier4FireSound;

        [System.Serializable]
        public struct TimingConfig
        {
            public float cooldown;
            public float minChargeTime;
            public float maxChargeTime;
            public float projectileLifetime;
        }

        [System.Serializable]
        public struct TierThresholdsConfig
        {
            public float tier1Time;
            public float tier2Time;
            public float tier3Time;
            public float tier4Time;
        }

        [System.Serializable]
        public struct MovementPenaltiesConfig
        {
            [Range(0f, 1f)] public float tier1ThrustMultiplier;
            [Range(0f, 1f)] public float tier1RotationMultiplier;
            [Range(0f, 1f)] public float tier2ThrustMultiplier;
            [Range(0f, 1f)] public float tier2RotationMultiplier;
            [Range(0f, 1f)] public float tier3ThrustMultiplier;
            [Range(0f, 1f)] public float tier3RotationMultiplier;
            [Range(0f, 1f)] public float tier4ThrustMultiplier;
            [Range(0f, 1f)] public float tier4RotationMultiplier;
        }

        [System.Serializable]
        public struct ProjectileScalingConfig
        {
            [Header("Tier 1")]
            public float tier1SpeedMultiplier;
            public float tier1DamageMultiplier;
            public float tier1RecoilMultiplier;
            public float tier1ImpactMultiplier;
            [Tooltip("Additional forward spawn offset for tier 1 to compensate for projectile pivot length.")]
            public float tier1SpawnOffset;

            [Header("Tier 2")]
            public float tier2SpeedMultiplier;
            public float tier2DamageMultiplier;
            public float tier2RecoilMultiplier;
            public float tier2ImpactMultiplier;
            [Tooltip("Additional forward spawn offset for tier 2 to compensate for projectile pivot length.")]
            public float tier2SpawnOffset;

            [Header("Tier 3")]
            public float tier3SpeedMultiplier;
            public float tier3DamageMultiplier;
            public float tier3RecoilMultiplier;
            public float tier3ImpactMultiplier;
            [Tooltip("Additional forward spawn offset for tier 3 to compensate for projectile pivot length.")]
            public float tier3SpawnOffset;

            [Header("Tier 4")]
            public float tier4SpeedMultiplier;
            public float tier4DamageMultiplier;
            public float tier4RecoilMultiplier;
            public float tier4ImpactMultiplier;
            [Tooltip("Additional forward spawn offset for tier 4 to compensate for projectile pivot length.")]
            public float tier4SpawnOffset;
        }

        [System.Serializable]
        public struct PierceConfig
        {
            public float tier3DamageMultiplierPerPierce;
            public float tier4DamageMultiplierPerPierce;
        }

        [System.Serializable]
        public struct VisualConfig
        {
            public GameObject tier1ProjectilePrefab;
            public GameObject tier2ProjectilePrefab;
            public GameObject tier3ProjectilePrefab;
            public GameObject tier4ProjectilePrefab;
            public ParticleSystem tier1ParticleSystem;
            public ParticleSystem tier2ParticleSystem;
            public ParticleSystem tier3ParticleSystem;
            public ParticleSystem tier4ParticleSystem;
        }
    }

    [Header("Ability 4 - GigaBlast 3D")]
    [SerializeField] private GigaBlastAbilityConfig3D gigaBlast = new GigaBlastAbilityConfig3D
    {
        timing = new GigaBlastAbilityConfig3D.TimingConfig
        {
            cooldown = 2.5f,
            minChargeTime = 0.5f,
            maxChargeTime = 3f,
            projectileLifetime = 5f
        },
        tierThresholds = new GigaBlastAbilityConfig3D.TierThresholdsConfig
        {
            tier1Time = 0.5f,
            tier2Time = 1f,
            tier3Time = 2f,
            tier4Time = 3f
        },
        movementPenalties = new GigaBlastAbilityConfig3D.MovementPenaltiesConfig
        {
            tier1ThrustMultiplier = 0.8f,
            tier1RotationMultiplier = 0.8f,
            tier2ThrustMultiplier = 0.6f,
            tier2RotationMultiplier = 0.6f,
            tier3ThrustMultiplier = 0.4f,
            tier3RotationMultiplier = 0.4f,
            tier4ThrustMultiplier = 0.2f,
            tier4RotationMultiplier = 0.2f
        },
        projectileScaling = new GigaBlastAbilityConfig3D.ProjectileScalingConfig
        {
            tier1SpeedMultiplier = 0.5f,
            tier1DamageMultiplier = 1f,
            tier1RecoilMultiplier = 1f,
            tier1ImpactMultiplier = 1f,
            tier1SpawnOffset = 0f,
            tier2SpeedMultiplier = 1.5f,
            tier2DamageMultiplier = 3f,
            tier2RecoilMultiplier = 3f,
            tier2ImpactMultiplier = 3f,
            tier2SpawnOffset = 0f,
            tier3SpeedMultiplier = 2f,
            tier3DamageMultiplier = 5f,
            tier3RecoilMultiplier = 5f,
            tier3ImpactMultiplier = 5f,
            tier3SpawnOffset = 0f,
            tier4SpeedMultiplier = 3f,
            tier4DamageMultiplier = 8f,
            tier4RecoilMultiplier = 8f,
            tier4ImpactMultiplier = 10f,
            tier4SpawnOffset = 0f
        },
        pierce = new GigaBlastAbilityConfig3D.PierceConfig
        {
            tier3DamageMultiplierPerPierce = 0.5f,
            tier4DamageMultiplierPerPierce = 1f
        },
        chargeSoundFadeDuration = 0.2f
    };
    [SerializeField] private ProjectileWeapon3D projectileWeapon;
    [SerializeField] private AudioSource chargeAudioSource;

    private bool _isCharging;
    private float _chargeStartTime;
    private int _currentChargeTier;
    private Coroutine _chargeFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        projectileWeapon ??= entity != null ? entity.PrimaryWeapon : GetComponent<ProjectileWeapon3D>();

        if (chargeAudioSource == null)
        {
            chargeAudioSource = gameObject.AddComponent<AudioSource>();
        }

        chargeAudioSource.playOnAwake = false;
        chargeAudioSource.loop = true;
        chargeAudioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (!_isCharging)
        {
            return;
        }

        int newTier = GetChargeTier(GetCurrentChargeTime());
        if (newTier == _currentChargeTier)
        {
            return;
        }

        StopCurrentChargeParticle();
        _currentChargeTier = newTier;
        PlayChargeParticleForTier(_currentChargeTier);
    }

    protected override bool HandlesReleaseInput() => true;
    protected override bool ShouldMarkAbilityUsedOnPress(InputValue value) => false;

    public override bool TryUseAbility(InputValue value)
    {
        if (value.isPressed)
        {
            if (projectileWeapon == null)
            {
                Debug.LogWarning("GigaBlast3D requires ProjectileWeapon3D on the same entity.", this);
                return false;
            }

            if (IsAnyOtherAbilityActive())
            {
                return false;
            }
        }

        return base.TryUseAbility(value);
    }

    public override void UseAbility(InputValue value)
    {
        if (value.isPressed)
        {
            if (_isCharging)
            {
                return;
            }

            StartCharging();
            return;
        }

        if (!_isCharging)
        {
            return;
        }

        ReleaseCharge();
    }

    public override bool IsAbilityActive()
    {
        return _isCharging;
    }

    public override float GetRotationMultiplier()
    {
        if (!_isCharging)
        {
            return 1f;
        }

        return GetRotationMultiplierForTier(GetChargeTier(GetCurrentChargeTime()));
    }

    public override float GetThrustMultiplier()
    {
        if (!_isCharging)
        {
            return 1f;
        }

        return GetThrustMultiplierForTier(GetChargeTier(GetCurrentChargeTime()));
    }

    public override bool DisablePrimaryFire()
    {
        return _isCharging;
    }

    protected override float GetCooldownDuration()
    {
        return gigaBlast.timing.cooldown;
    }

    public override void Die()
    {
        StopChargingState();
    }

    private void OnDisable()
    {
        StopChargingState();
    }

    private void StartCharging()
    {
        _isCharging = true;
        _chargeStartTime = Time.time;
        _currentChargeTier = GetChargeTier(0f);
        DisableOtherAbilities(true);
        PlayChargeParticleForTier(_currentChargeTier);
        StartChargeSound();
    }

    private void ReleaseCharge()
    {
        float chargeTime = GetCurrentChargeTime();
        bool shouldFire = chargeTime >= gigaBlast.timing.minChargeTime;
        int firedTier = GetChargeTier(chargeTime);

        StopChargingState();

        if (!shouldFire)
        {
            return;
        }

        if (FireChargedShot(firedTier))
        {
            MarkAbilityUsed();
        }
    }

    private void StopChargingState()
    {
        _isCharging = false;
        _chargeStartTime = 0f;
        _currentChargeTier = 0;
        DisableOtherAbilities(false);
        StopAllChargeParticles();
        StopChargeSound();
    }

    private bool FireChargedShot(int tier)
    {
        if (projectileWeapon == null)
        {
            return false;
        }

        ProjectileWeaponConfig3D baseWeapon = projectileWeapon.WeaponConfig;
        GameObject projectilePrefab = GetProjectilePrefabForTier(tier);
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"GigaBlast3D tier {tier} is missing its projectile prefab.", this);
            return false;
        }

        float projectileLifetime = gigaBlast.timing.projectileLifetime > 0f
            ? gigaBlast.timing.projectileLifetime
            : baseWeapon.lifetime;

        ProjectileFireRequest3D request = new ProjectileFireRequest3D
        {
            projectilePrefab = projectilePrefab,
            muzzles = null,
            spawnAnchor = gigaBlast.muzzle != null ? gigaBlast.muzzle : entity.transform,
            targetTag = baseWeapon.targetTag,
            speed = baseWeapon.speed * GetSpeedMultiplierForTier(tier),
            damage = baseWeapon.damage * GetDamageMultiplierForTier(tier),
            lifetime = projectileLifetime,
            impactForce = baseWeapon.impactForce * GetImpactMultiplierForTier(tier),
            recoilForce = baseWeapon.recoilForce * GetRecoilMultiplierForTier(tier),
            forwardOffset = gigaBlast.offsetDistance + GetSpawnOffsetForTier(tier),
            verticalOffset = gigaBlast.verticalOffset,
            onProjectileSpawned = projectile =>
            {
                if (tier < 3 || projectile is not GigaBlastProjectile3D gigaBlastProjectile)
                {
                    return;
                }

                gigaBlastProjectile.EnablePiercing(GetPierceMultiplierForTier(tier));
            }
        };

        bool fired = projectileWeapon.Fire(request);
        if (!fired)
        {
            return false;
        }

        GetFireSoundForTier(tier)?.PlayAtPoint(transform.position);
        return true;
    }

    private float GetCurrentChargeTime()
    {
        if (!_isCharging)
        {
            return 0f;
        }

        float rawChargeTime = Time.time - _chargeStartTime;
        if (gigaBlast.timing.maxChargeTime <= 0f)
        {
            return rawChargeTime;
        }

        return Mathf.Min(rawChargeTime, gigaBlast.timing.maxChargeTime);
    }

    private int GetChargeTier(float chargeTime)
    {
        if (chargeTime >= gigaBlast.tierThresholds.tier4Time) return 4;
        if (chargeTime >= gigaBlast.tierThresholds.tier3Time) return 3;
        if (chargeTime >= gigaBlast.tierThresholds.tier2Time) return 2;
        if (chargeTime >= gigaBlast.tierThresholds.tier1Time) return 1;
        return 1;
    }

    private float GetRotationMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.movementPenalties.tier1RotationMultiplier,
            2 => gigaBlast.movementPenalties.tier2RotationMultiplier,
            3 => gigaBlast.movementPenalties.tier3RotationMultiplier,
            4 => gigaBlast.movementPenalties.tier4RotationMultiplier,
            _ => 1f
        };
    }

    private float GetThrustMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.movementPenalties.tier1ThrustMultiplier,
            2 => gigaBlast.movementPenalties.tier2ThrustMultiplier,
            3 => gigaBlast.movementPenalties.tier3ThrustMultiplier,
            4 => gigaBlast.movementPenalties.tier4ThrustMultiplier,
            _ => 1f
        };
    }

    private float GetSpeedMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.projectileScaling.tier1SpeedMultiplier,
            2 => gigaBlast.projectileScaling.tier2SpeedMultiplier,
            3 => gigaBlast.projectileScaling.tier3SpeedMultiplier,
            4 => gigaBlast.projectileScaling.tier4SpeedMultiplier,
            _ => 1f
        };
    }

    private float GetDamageMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.projectileScaling.tier1DamageMultiplier,
            2 => gigaBlast.projectileScaling.tier2DamageMultiplier,
            3 => gigaBlast.projectileScaling.tier3DamageMultiplier,
            4 => gigaBlast.projectileScaling.tier4DamageMultiplier,
            _ => 1f
        };
    }

    private float GetRecoilMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.projectileScaling.tier1RecoilMultiplier,
            2 => gigaBlast.projectileScaling.tier2RecoilMultiplier,
            3 => gigaBlast.projectileScaling.tier3RecoilMultiplier,
            4 => gigaBlast.projectileScaling.tier4RecoilMultiplier,
            _ => 1f
        };
    }

    private float GetImpactMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.projectileScaling.tier1ImpactMultiplier,
            2 => gigaBlast.projectileScaling.tier2ImpactMultiplier,
            3 => gigaBlast.projectileScaling.tier3ImpactMultiplier,
            4 => gigaBlast.projectileScaling.tier4ImpactMultiplier,
            _ => 1f
        };
    }

    private float GetSpawnOffsetForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.projectileScaling.tier1SpawnOffset,
            2 => gigaBlast.projectileScaling.tier2SpawnOffset,
            3 => gigaBlast.projectileScaling.tier3SpawnOffset,
            4 => gigaBlast.projectileScaling.tier4SpawnOffset,
            _ => 0f
        };
    }

    private float GetPierceMultiplierForTier(int tier)
    {
        return tier switch
        {
            3 => gigaBlast.pierce.tier3DamageMultiplierPerPierce,
            4 => gigaBlast.pierce.tier4DamageMultiplierPerPierce,
            _ => 1f
        };
    }

    private GameObject GetProjectilePrefabForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.visual.tier1ProjectilePrefab,
            2 => gigaBlast.visual.tier2ProjectilePrefab,
            3 => gigaBlast.visual.tier3ProjectilePrefab,
            4 => gigaBlast.visual.tier4ProjectilePrefab,
            _ => null
        };
    }

    private SoundEffect GetFireSoundForTier(int tier)
    {
        return tier switch
        {
            1 => gigaBlast.tier1FireSound,
            2 => gigaBlast.tier2FireSound,
            3 => gigaBlast.tier3FireSound,
            4 => gigaBlast.tier4FireSound,
            _ => null
        };
    }

    private void PlayChargeParticleForTier(int tier)
    {
        ParticleSystem particleSystem = tier switch
        {
            1 => gigaBlast.visual.tier1ParticleSystem,
            2 => gigaBlast.visual.tier2ParticleSystem,
            3 => gigaBlast.visual.tier3ParticleSystem,
            4 => gigaBlast.visual.tier4ParticleSystem,
            _ => null
        };

        particleSystem?.Play();
    }

    private void StopCurrentChargeParticle()
    {
        ParticleSystem particleSystem = _currentChargeTier switch
        {
            1 => gigaBlast.visual.tier1ParticleSystem,
            2 => gigaBlast.visual.tier2ParticleSystem,
            3 => gigaBlast.visual.tier3ParticleSystem,
            4 => gigaBlast.visual.tier4ParticleSystem,
            _ => null
        };

        particleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void StopAllChargeParticles()
    {
        gigaBlast.visual.tier1ParticleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        gigaBlast.visual.tier2ParticleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        gigaBlast.visual.tier3ParticleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        gigaBlast.visual.tier4ParticleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void StartChargeSound()
    {
        if (gigaBlast.chargeSound == null || chargeAudioSource == null)
        {
            return;
        }

        chargeAudioSource.volume = 0f;
        gigaBlast.chargeSound.Play(chargeAudioSource);
        StartChargeFade(gigaBlast.chargeSound.volume, stopAfterFade: false);
    }

    private void StopChargeSound()
    {
        if (chargeAudioSource == null || !chargeAudioSource.isPlaying)
        {
            return;
        }

        StartChargeFade(0f, stopAfterFade: true);
    }

    private void StartChargeFade(float targetVolume, bool stopAfterFade)
    {
        if (_chargeFadeCoroutine != null)
        {
            StopCoroutine(_chargeFadeCoroutine);
        }

        _chargeFadeCoroutine = StartCoroutine(FadeChargeVolume(targetVolume, stopAfterFade));
    }

    private System.Collections.IEnumerator FadeChargeVolume(float targetVolume, bool stopAfterFade)
    {
        if (chargeAudioSource == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, gigaBlast.chargeSoundFadeDuration);
        if (duration <= 0f)
        {
            chargeAudioSource.volume = targetVolume;
            if (stopAfterFade && targetVolume <= 0f)
            {
                chargeAudioSource.Stop();
            }

            _chargeFadeCoroutine = null;
            yield break;
        }

        float startVolume = chargeAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            chargeAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        chargeAudioSource.volume = targetVolume;
        if (stopAfterFade && targetVolume <= 0f)
        {
            chargeAudioSource.Stop();
        }

        _chargeFadeCoroutine = null;
    }
}
