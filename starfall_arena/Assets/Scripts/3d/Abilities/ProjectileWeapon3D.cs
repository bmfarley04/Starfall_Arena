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
        energyCost = 18f,
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
    [Range(0f, 1f)]
    [SerializeField] private float screenCenterDirectionBlend = 0.35f;

    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    [SerializeField] private float muzzleEffectLifetime = 2f;
    [SerializeField] private bool parentMuzzleEffectToMuzzle = true;
    [SerializeField] private Vector3 projectileSpawnLocalOffset = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 muzzleEffectLocalOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Pooling")]
    [SerializeField] private int projectilePrewarmCount = 12;
    [SerializeField] private int muzzleEffectPrewarmCount = 4;

    [Header("Audio")]
    [SerializeField] private SoundEffect fireSound;

    private float _lastFireTime = -999f;
    private float _lastSuccessfulFireTime = -999f;

    private struct AimSolution
    {
        public Vector3 point;
        public Vector3 direction;
    }

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;
    public float CooldownRemaining => Mathf.Max(0f, (_lastFireTime + weaponConfig.cooldown) - Time.time);
    public float LastFireTime => _lastFireTime;
    public float LastSuccessfulFireTime => _lastSuccessfulFireTime;
    public Camera AimCamera => aimCamera;

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
        if (Time.time < _lastFireTime + weaponConfig.cooldown)
        {
            return false;
        }

        ProjectileFireRequest3D request = BuildDefaultFireRequest();
        if (request.projectilePrefab == null)
        {
            return false;
        }

        if (owner != null && !owner.TrySpendPrimaryWeaponEnergy(weaponConfig.energyCost))
        {
            return false;
        }

        return Fire(request, consumeCooldown: true);
    }

    public bool Fire(ProjectileFireRequest3D request, bool consumeCooldown = false)
    {
        if (request.projectilePrefab == null)
        {
            return false;
        }

        Transform[] muzzles = request.muzzles != null && request.muzzles.Length > 0
            ? request.muzzles
            : weaponConfig.muzzles != null && weaponConfig.muzzles.Length > 0
                ? weaponConfig.muzzles
                : new[] { transform };

        string resolvedTargetTag = !string.IsNullOrEmpty(request.targetTag)
            ? request.targetTag
            : weaponConfig.targetTag;

        AimSolution aim = ResolveAimSolution();
        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            SpawnMuzzleEffect(spawnMuzzle);
            SpawnProjectile(spawnMuzzle, aim, request, resolvedTargetTag);
        }

        if (shipFlight != null && request.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(request.recoilForce);
        }

        fireSound?.PlayAtPoint(transform.position);
        _lastSuccessfulFireTime = Time.time;

        if (consumeCooldown)
        {
            _lastFireTime = Time.time;
        }

        return true;
    }

    private ProjectileFireRequest3D BuildDefaultFireRequest()
    {
        return new ProjectileFireRequest3D
        {
            projectilePrefab = weaponConfig.projectilePrefab,
            muzzles = weaponConfig.muzzles,
            spawnAnchor = null,
            targetTag = weaponConfig.targetTag,
            speed = weaponConfig.speed,
            damage = weaponConfig.damage,
            lifetime = weaponConfig.lifetime,
            impactForce = weaponConfig.impactForce,
            recoilForce = weaponConfig.recoilForce,
            forwardOffset = 0f,
            verticalOffset = 0f
        };
    }

    public Ray GetScreenCenterAimRay()
    {
        Camera resolvedCamera = aimCamera != null ? aimCamera : Camera.main;
        if (resolvedCamera == null)
        {
            return new Ray(transform.position, transform.forward);
        }

        return resolvedCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private void SpawnProjectile(Transform muzzle, AimSolution aim, ProjectileFireRequest3D request, string targetTag)
    {
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(muzzle, request);
        Vector3 fireDirection = ResolveFireDirection(muzzle, spawnPosition, aim);
        GameObject projectileObject = GameObjectPool3D.Spawn(request.projectilePrefab, spawnPosition, Quaternion.LookRotation(fireDirection, transform.up));
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {request.projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        projectile.targetTag = targetTag;
        projectile.Initialize(
            fireDirection,
            inheritedVelocity,
            request.speed,
            request.damage,
            request.lifetime,
            request.impactForce,
            owner
        );
        request.onProjectileSpawned?.Invoke(projectile);
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform muzzle, ProjectileFireRequest3D request)
    {
        if (request.spawnAnchor != null)
        {
            return request.spawnAnchor.position
                + (request.spawnAnchor.forward * request.forwardOffset)
                + (request.spawnAnchor.up * request.verticalOffset);
        }

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

        Vector3 resolvedDirection = direction.normalized;
        if (aimMode == ProjectileAimMode3D.ScreenCenter && aim.direction.sqrMagnitude > 0.0001f && screenCenterDirectionBlend > 0f)
        {
            resolvedDirection = Vector3.Slerp(resolvedDirection, aim.direction.normalized, Mathf.Clamp01(screenCenterDirectionBlend)).normalized;
        }

        return resolvedDirection;
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
