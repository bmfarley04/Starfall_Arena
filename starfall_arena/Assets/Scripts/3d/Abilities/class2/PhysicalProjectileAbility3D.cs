using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicalProjectileAbility3D : Ability3D
{
    [System.Serializable]
    public struct PhysicalProjectileAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between shots in seconds.")]
        public float cooldown;

        [Header("Spawn Point")]
        [Tooltip("Transform used as the projectile spawn anchor.")]
        public Transform spawnPoint;

        [Header("Projectile")]
        [Tooltip("Projectile prefab that should use PhysicalProjectile3D.")]
        public GameObject projectilePrefab;
        [Tooltip("Damage dealt by the projectile.")]
        public float damage;
        [Tooltip("Projectile speed.")]
        public float speed;
        [Tooltip("Projectile lifetime in seconds.")]
        public float lifetime;
        [Tooltip("Impact force applied on hit.")]
        public float impactForce;
        [Tooltip("Recoil applied to the firing ship.")]
        public float recoilForce;

        [Header("Sound Effects")]
        [Tooltip("Sound played when firing.")]
        public SoundEffect fireSound;
    }

    [Header("Ability 4 - Physical Projectile 3D")]
    [SerializeField] private PhysicalProjectileAbilityConfig3D physicalProjectile = new PhysicalProjectileAbilityConfig3D
    {
        cooldown = 2f,
        speed = 80f,
        damage = 20f,
        lifetime = 5f
    };
    [SerializeField] private ProjectileWeapon3D projectileWeapon;

    protected override void Awake()
    {
        base.Awake();
        projectileWeapon ??= entity != null ? entity.PrimaryWeapon : GetComponent<ProjectileWeapon3D>();
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return false;
        }

        if (isLocked)
        {
            Debug.LogWarning("PhysicalProjectileAbility3D is locked and must be unlocked by round flow before it can be used.", this);
            return false;
        }

        if (projectileWeapon == null)
        {
            Debug.LogWarning("PhysicalProjectileAbility3D requires ProjectileWeapon3D on the same entity.", this);
            return false;
        }

        if (physicalProjectile.projectilePrefab == null)
        {
            Debug.LogWarning("PhysicalProjectileAbility3D is missing its projectile prefab.", this);
            return false;
        }

        if (!physicalProjectile.projectilePrefab.TryGetComponent(out PhysicalProjectile3D _))
        {
            Debug.LogWarning("PhysicalProjectileAbility3D requires a projectile prefab that includes PhysicalProjectile3D.", physicalProjectile.projectilePrefab);
            return false;
        }

        if (physicalProjectile.spawnPoint == null)
        {
            Debug.LogWarning("PhysicalProjectileAbility3D is missing its spawn point.", this);
            return false;
        }

        return base.TryUseAbility(value);
    }

    public override void UseAbility(InputValue value)
    {
        ProjectileWeaponConfig3D baseWeapon = projectileWeapon.WeaponConfig;
        ProjectileFireRequest3D request = new ProjectileFireRequest3D
        {
            projectilePrefab = physicalProjectile.projectilePrefab,
            muzzles = null,
            spawnAnchor = physicalProjectile.spawnPoint,
            targetTag = baseWeapon.targetTag,
            speed = physicalProjectile.speed,
            damage = physicalProjectile.damage,
            lifetime = physicalProjectile.lifetime,
            impactForce = physicalProjectile.impactForce,
            recoilForce = physicalProjectile.recoilForce,
            forwardOffset = 0f,
            verticalOffset = 0f
        };

        bool fired = projectileWeapon.Fire(request);
        if (!fired)
        {
            return;
        }

        physicalProjectile.fireSound?.PlayAtPoint(transform.position);
    }

    protected override float GetCooldownDuration()
    {
        return physicalProjectile.cooldown;
    }
}
