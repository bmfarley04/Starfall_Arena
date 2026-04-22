using UnityEngine;
using System.Collections.Generic;

public class EmpoweredShot3D : Weapon3D
{
    [System.Serializable]
    public struct EmpoweredShotAbilityConfig3D
    {
        [Header("Cooldown")]
        [Tooltip("Cooldown between empowered shots in seconds.")]
        public float cooldown;

        [Header("Projectile")]
        [Tooltip("Projectile prefab used for the empowered shot.")]
        public GameObject projectilePrefab;
        [Tooltip("Damage multiplier relative to ProjectileWeapon3D.")]
        public float damageMultiplier;
        [Tooltip("Speed multiplier relative to ProjectileWeapon3D.")]
        public float speedMultiplier;
        [Tooltip("Impact force multiplier relative to ProjectileWeapon3D.")]
        public float impactMultiplier;
        [Tooltip("Recoil multiplier relative to ProjectileWeapon3D.")]
        public float recoilMultiplier;
        [Tooltip("Optional projectile lifetime override. Uses the base weapon lifetime when zero or negative.")]
        public float lifetime;

        [Header("Slow Effect")]
        [Range(0f, 1f)]
        [Tooltip("Movement multiplier applied to the victim while slowed.")]
        public float slowMultiplier;
        [Tooltip("How long the slow lasts in seconds.")]
        public float slowDuration;
        [Tooltip("Expected default engine emission rate used to compute slowdown scaling.")]
        public float normalEngineEmissionRate;
        [Tooltip("Temporary engine emission rate while the target is slowed.")]
        public float slowedEngineEmissionRate;

        [Header("Sound Effects")]
        [Tooltip("Sound played when firing the empowered shot.")]
        public SoundEffect fireSound;
    }

    [Header("Weapon 2 - Empowered Shot 3D")]
    [SerializeField] private EmpoweredShotAbilityConfig3D empoweredShot = new EmpoweredShotAbilityConfig3D
    {
        cooldown = 1.5f,
        damageMultiplier = 1f,
        speedMultiplier = 1f,
        impactMultiplier = 1f,
        recoilMultiplier = 1f,
        slowMultiplier = 0.5f,
        slowDuration = 1f,
        normalEngineEmissionRate = 30f,
        slowedEngineEmissionRate = 2f
    };
    [SerializeField] private ProjectileWeapon3D projectileWeapon;

    protected override void Awake()
    {
        base.Awake();
        SetAvailabilityMode(AvailabilityMode3D.Cooldown);
        projectileWeapon ??= Owner != null ? Owner.PrimaryWeapon : GetComponent<ProjectileWeapon3D>();
    }

    protected override IEnumerable<GameObject> GetPrewarmProjectilePrefabs()
    {
        if (empoweredShot.projectilePrefab != null)
        {
            yield return empoweredShot.projectilePrefab;
        }
    }

    protected override float GetConfiguredCooldownDuration()
    {
        return empoweredShot.cooldown;
    }

    protected override void OnFireHeld()
    {
        TryFireEmpoweredShot();
    }

    private bool TryFireEmpoweredShot()
    {
        if (projectileWeapon == null)
        {
            Debug.LogWarning("EmpoweredShot3D requires ProjectileWeapon3D on the same entity.", this);
            return false;
        }

        if (empoweredShot.projectilePrefab == null)
        {
            Debug.LogWarning("EmpoweredShot3D is missing its empowered projectile prefab.", this);
            return false;
        }

        if (!empoweredShot.projectilePrefab.TryGetComponent(out Projectile3D _))
        {
            Debug.LogWarning("EmpoweredShot3D requires a projectile prefab that includes Projectile3D.", empoweredShot.projectilePrefab);
            return false;
        }

        if (IsOnCooldown())
        {
            return false;
        }

        ProjectileWeaponConfig3D baseWeapon = projectileWeapon.WeaponConfig;
        float lifetime = empoweredShot.lifetime > 0f ? empoweredShot.lifetime : baseWeapon.lifetime;

        ProjectileFireRequest3D request = new ProjectileFireRequest3D
        {
            projectilePrefab = empoweredShot.projectilePrefab,
            muzzles = baseWeapon.muzzles,
            spawnAnchor = null,
            targetTag = baseWeapon.targetTag,
            speed = baseWeapon.speed * empoweredShot.speedMultiplier,
            damage = baseWeapon.damage * empoweredShot.damageMultiplier,
            lifetime = lifetime,
            impactForce = baseWeapon.impactForce * empoweredShot.impactMultiplier,
            recoilForce = baseWeapon.recoilForce * empoweredShot.recoilMultiplier,
            forwardOffset = 0f,
            verticalOffset = 0f,
            onProjectileSpawned = projectile =>
            {
                projectile.EnableSlow(empoweredShot.slowMultiplier, empoweredShot.slowDuration, GetSlowEngineEmissionScale());
            }
        };

        bool fired = FireProjectilePattern(request, baseWeapon, empoweredShot.fireSound);
        if (!fired)
        {
            return false;
        }

        StartCooldown();
        return true;
    }

    private float GetSlowEngineEmissionScale()
    {
        float normalRate = Mathf.Max(0.0001f, empoweredShot.normalEngineEmissionRate);
        float slowedRate = Mathf.Max(0f, empoweredShot.slowedEngineEmissionRate);
        return Mathf.Clamp01(slowedRate / normalRate);
    }
}
