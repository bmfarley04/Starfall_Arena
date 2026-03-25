using UnityEngine;

public class ProjectileWeapon3D : MonoBehaviour
{
    [SerializeField] private ProjectileWeaponConfig3D weaponConfig = new ProjectileWeaponConfig3D
    {
        cooldown = 0.25f,
        speed = 120f,
        damage = 10f,
        lifetime = 5f,
        impactForce = 0f,
        recoilForce = 0f,
        targetTag = "Enemy"
    };

    [SerializeField] private Entity3D owner;
    [SerializeField] private ShipFlight3D shipFlight;

    private float _lastFireTime = -999f;

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;
    public float CooldownRemaining => Mathf.Max(0f, (_lastFireTime + weaponConfig.cooldown) - Time.time);

    private void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        shipFlight ??= GetComponent<ShipFlight3D>();
    }

    public void SetOwner(Entity3D newOwner)
    {
        owner = newOwner;
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetWeaponConfig(ProjectileWeaponConfig3D config)
    {
        weaponConfig = config;
    }

    public bool TryFire()
    {
        if (weaponConfig.projectilePrefab == null)
        {
            return false;
        }

        if (Time.time < _lastFireTime + weaponConfig.cooldown)
        {
            return false;
        }

        Transform[] muzzles = weaponConfig.muzzles != null && weaponConfig.muzzles.Length > 0
            ? weaponConfig.muzzles
            : new[] { transform };

        foreach (Transform muzzle in muzzles)
        {
            SpawnProjectile(muzzle != null ? muzzle : transform);
        }

        if (shipFlight != null && weaponConfig.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(weaponConfig.recoilForce);
        }

        _lastFireTime = Time.time;
        return true;
    }

    private void SpawnProjectile(Transform muzzle)
    {
        GameObject projectileObject = Instantiate(weaponConfig.projectilePrefab, muzzle.position, muzzle.rotation);
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {weaponConfig.projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        projectile.targetTag = weaponConfig.targetTag;
        projectile.Initialize(
            muzzle.forward,
            inheritedVelocity,
            weaponConfig.speed,
            weaponConfig.damage,
            weaponConfig.lifetime,
            weaponConfig.impactForce,
            owner
        );
    }
}
