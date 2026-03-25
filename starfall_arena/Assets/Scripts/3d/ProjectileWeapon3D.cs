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

    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    [SerializeField] private float muzzleEffectLifetime = 2f;
    [SerializeField] private bool parentMuzzleEffectToMuzzle = true;
    [SerializeField] private Vector3 muzzleEffectLocalOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Pooling")]
    [SerializeField] private int projectilePrewarmCount = 12;
    [SerializeField] private int muzzleEffectPrewarmCount = 4;

    private float _lastFireTime = -999f;

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

        Vector3 aimPoint = ResolveAimPoint();

        foreach (Transform muzzle in muzzles)
        {
            Transform spawnMuzzle = muzzle != null ? muzzle : transform;
            SpawnMuzzleEffect(spawnMuzzle);
            SpawnProjectile(spawnMuzzle, aimPoint);
        }

        if (shipFlight != null && weaponConfig.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(weaponConfig.recoilForce);
        }

        _lastFireTime = Time.time;
        return true;
    }

    private void SpawnProjectile(Transform muzzle, Vector3 aimPoint)
    {
        Vector3 fireDirection = ResolveFireDirection(muzzle, aimPoint);
        GameObject projectileObject = GameObjectPool3D.Spawn(weaponConfig.projectilePrefab, muzzle.position, Quaternion.LookRotation(fireDirection, transform.up));
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

    private Vector3 ResolveAimPoint()
    {
        if (aimMode != ProjectileAimMode3D.ScreenCenter || aimCamera == null)
        {
            return transform.position + transform.forward * maxAimDistance;
        }

        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(centerRay, out RaycastHit hit, maxAimDistance, aimCollisionMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return centerRay.origin + (centerRay.direction * maxAimDistance);
    }

    private Vector3 ResolveFireDirection(Transform muzzle, Vector3 aimPoint)
    {
        if (aimMode == ProjectileAimMode3D.MuzzleForward)
        {
            return muzzle.forward;
        }

        Vector3 direction = aimPoint - muzzle.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return transform.forward;
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
