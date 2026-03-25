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

    [Header("Aiming")]
    [SerializeField] private ProjectileAimMode3D aimMode = ProjectileAimMode3D.ScreenCenter;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask aimCollisionMask = ~0;
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private float screenCenterConvergenceDistance = 150f;

    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    [SerializeField] private float muzzleEffectLifetime = 2f;
    [SerializeField] private bool parentMuzzleEffectToMuzzle = true;
    [SerializeField] private Vector3 projectileSpawnLocalOffset = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 muzzleEffectLocalOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Pooling")]
    [SerializeField] private int projectilePrewarmCount = 12;
    [SerializeField] private int muzzleEffectPrewarmCount = 4;

    private float _lastFireTime = -999f;

    private struct AimSolution
    {
        public Vector3 point;
        public Vector3 direction;
    }

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;
    public float CooldownRemaining => Mathf.Max(0f, (_lastFireTime + weaponConfig.cooldown) - Time.time);

    private void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        shipFlight ??= GetComponent<ShipFlight3D>();
        aimCamera ??= Camera.main;

        GameObjectPool3D.Prewarm(weaponConfig.projectilePrefab, projectilePrewarmCount);
        GameObjectPool3D.Prewarm(muzzleEffectPrefab, muzzleEffectPrewarmCount);
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

        AimSolution aim = ResolveAimSolution();

        foreach (Transform muzzle in muzzles)
        {
            Transform spawnMuzzle = muzzle != null ? muzzle : transform;
            SpawnMuzzleEffect(spawnMuzzle);
            SpawnProjectile(spawnMuzzle, aim);
        }

        if (shipFlight != null && weaponConfig.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(weaponConfig.recoilForce);
        }

        _lastFireTime = Time.time;
        return true;
    }

    private void SpawnProjectile(Transform muzzle, AimSolution aim)
    {
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(muzzle);
        Vector3 fireDirection = ResolveFireDirection(muzzle, spawnPosition, aim);
        GameObject projectileObject = GameObjectPool3D.Spawn(weaponConfig.projectilePrefab, spawnPosition, Quaternion.LookRotation(fireDirection, transform.up));
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {weaponConfig.projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        projectile.targetTag = weaponConfig.targetTag;
        projectile.Initialize(
            fireDirection,
            inheritedVelocity,
            weaponConfig.speed,
            weaponConfig.damage,
            weaponConfig.lifetime,
            weaponConfig.impactForce,
            owner
        );
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform muzzle)
    {
        return muzzle.position
            + (transform.right * projectileSpawnLocalOffset.x)
            + (transform.up * projectileSpawnLocalOffset.y)
            + (transform.forward * projectileSpawnLocalOffset.z);
    }

    private AimSolution ResolveAimSolution()
    {
        if (aimMode != ProjectileAimMode3D.ScreenCenter || aimCamera == null)
        {
            return new AimSolution
            {
                point = transform.position + (transform.forward * maxAimDistance),
                direction = transform.forward
            };
        }

        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(centerRay, out RaycastHit hit, maxAimDistance, aimCollisionMask, QueryTriggerInteraction.Ignore))
        {
            float convergenceDistance = Mathf.Max(screenCenterConvergenceDistance, hit.distance);
            return new AimSolution
            {
                point = centerRay.origin + (centerRay.direction * convergenceDistance),
                direction = centerRay.direction
            };
        }

        return new AimSolution
        {
            point = centerRay.origin + (centerRay.direction * Mathf.Max(screenCenterConvergenceDistance, maxAimDistance)),
            direction = centerRay.direction
        };
    }

    private Vector3 ResolveFireDirection(Transform muzzle, Vector3 spawnPosition, AimSolution aim)
    {
        if (aimMode == ProjectileAimMode3D.MuzzleForward)
        {
            return muzzle.forward;
        }

        Vector3 direction = aim.point - spawnPosition;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return aim.direction.sqrMagnitude > 0.0001f ? aim.direction.normalized : transform.forward;
        }

        return direction.normalized;
    }

    private void SpawnMuzzleEffect(Transform muzzle)
    {
        if (muzzleEffectPrefab == null)
        {
            return;
        }

        Transform parent = parentMuzzleEffectToMuzzle ? muzzle : null;
        Vector3 spawnPosition = muzzle.TransformPoint(muzzleEffectLocalOffset);
        GameObject effectObject = GameObjectPool3D.Spawn(muzzleEffectPrefab, spawnPosition, muzzle.rotation, parent);
        PooledObject3D pooled = effectObject != null ? effectObject.GetComponent<PooledObject3D>() : null;
        if (pooled != null)
        {
            pooled.ScheduleDespawn(muzzleEffectLifetime);
        }
    }
}
