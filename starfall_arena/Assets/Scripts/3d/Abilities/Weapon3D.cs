using System.Collections.Generic;
using System;
using UnityEngine;

public abstract class Weapon3D : MonoBehaviour, IReticleSpinSource3D
{
    public enum AvailabilityMode3D
    {
        ResourceConsumption,
        Cooldown
    }

    [System.Serializable]
    private struct ResourceConfig3D
    {
        public float capacity;
        public float recoveryPerSecond;
    }

    protected struct AimSolution
    {
        public Vector3 point;
        public Vector3 direction;
    }

    [Header("Weapon Core")]
    [SerializeField] private AvailabilityMode3D availabilityMode = AvailabilityMode3D.ResourceConsumption;
    [SerializeField] private Color reticleFillColor = Color.white;
    [SerializeField] protected Entity3D owner;
    [SerializeField] protected ShipFlight3D shipFlight;

    [Header("Resource Availability")]
    [SerializeField] private ResourceConfig3D resource = new ResourceConfig3D
    {
        capacity = 100f,
        recoveryPerSecond = 30f
    };

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

    private bool _isFireHeld;
    private float _currentResourceUsage;
    private float _cooldownReadyTime = float.NegativeInfinity;
    private float _lastReticleSpinPulseTime = float.NegativeInfinity;
    private float _lastAvailabilityChangedReadyRatio = float.NaN;
    private bool _lastAvailabilityChangedOnCooldown;
    private bool _hasAvailabilitySnapshot;
    private NetCombat3D _netCombat;

    public event Action<Weapon3D> AvailabilityChanged;

    public AvailabilityMode3D AvailabilityMode => availabilityMode;
    public Entity3D Owner => owner;
    public ShipFlight3D ShipFlight => shipFlight;
    public Color ReticleFillColor => reticleFillColor;
    public Camera AimCamera => aimCamera;
    public bool IsFireHeld => _isFireHeld;
    public float CurrentResourceUsage => _currentResourceUsage;
    public float ResourceCapacity => Mathf.Max(0f, GetConfiguredResourceCapacity());
    public float AvailableResourceRatio => GetAvailableResourceRatio();
    public float CooldownRemaining => UsesCooldownAvailability ? Mathf.Max(0f, _cooldownReadyTime - Time.time) : 0f;
    public float CooldownDuration => Mathf.Max(0f, GetConfiguredCooldownDuration());
    public float CooldownReadyRatio => GetCooldownReadyRatio();
    public bool UsesResourceAvailability => availabilityMode == AvailabilityMode3D.ResourceConsumption;
    public bool UsesCooldownAvailability => availabilityMode == AvailabilityMode3D.Cooldown;

    protected virtual void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        shipFlight ??= GetComponent<ShipFlight3D>();
        _netCombat ??= GetComponent<NetCombat3D>();
        aimCamera ??= Camera.main;

        foreach (GameObject projectilePrefab in GetPrewarmProjectilePrefabs())
        {
            if (projectilePrefab != null)
            {
                GameObjectPool3D.Prewarm(projectilePrefab, projectilePrewarmCount);
            }
        }

        if (muzzleEffectPrefab != null)
        {
            GameObjectPool3D.Prewarm(muzzleEffectPrefab, muzzleEffectPrewarmCount);
        }

        CacheAvailabilitySnapshot();
    }

    private void Update()
    {
        RecoverResource(Time.deltaTime);

        if (_isFireHeld)
        {
            OnFireHeld();
        }

        OnWeaponUpdated(Time.deltaTime);
        RaiseAvailabilityChangedIfNeeded();
    }

    private void FixedUpdate()
    {
        OnWeaponFixedUpdated(Time.fixedDeltaTime);
    }

    public virtual void SetFireHeld(bool isHeld)
    {
        if (_isFireHeld == isHeld)
        {
            return;
        }

        _isFireHeld = isHeld;
        if (_isFireHeld)
        {
            OnFirePressed();
        }
        else
        {
            OnFireReleased();
        }
    }

    public virtual void OnSelected()
    {
    }

    public virtual void OnDeselected()
    {
        SetFireHeld(false);
    }

    public virtual void Die()
    {
    }

    public virtual float GetReticleFillRatio()
    {
        if (UsesResourceAvailability)
        {
            float capacity = ResourceCapacity;
            return capacity > 0f ? Mathf.Clamp01(_currentResourceUsage / capacity) : 0f;
        }

        float cooldown = CooldownDuration;
        if (cooldown <= 0f || !UsesCooldownAvailability)
        {
            return 0f;
        }

        return Mathf.Clamp01(CooldownRemaining / cooldown);
    }

    public virtual bool IsReticleSpinActive()
    {
        return false;
    }

    public float GetReticleSpinPulseTime()
    {
        return _lastReticleSpinPulseTime;
    }

    public virtual float GetRotationMultiplier()
    {
        return 1f;
    }

    public virtual float GetThrustMultiplier()
    {
        return 1f;
    }

    public virtual Ray GetAimRay()
    {
        if (aimMode == ProjectileAimMode3D.ScreenCenter)
        {
            return GetScreenCenterAimRay();
        }

        return new Ray(transform.position, transform.forward);
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

    public void SetOwner(Entity3D newOwner)
    {
        owner = newOwner;
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    protected void SetAvailabilityMode(AvailabilityMode3D mode)
    {
        if (availabilityMode == mode)
        {
            return;
        }

        availabilityMode = mode;
        if (availabilityMode != AvailabilityMode3D.ResourceConsumption)
        {
            _currentResourceUsage = 0f;
        }

        if (availabilityMode != AvailabilityMode3D.Cooldown)
        {
            _cooldownReadyTime = float.NegativeInfinity;
        }

        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected virtual IEnumerable<GameObject> GetPrewarmProjectilePrefabs()
    {
        yield break;
    }

    protected virtual float GetConfiguredCooldownDuration()
    {
        return 0f;
    }

    protected virtual float GetConfiguredResourceCapacity()
    {
        return resource.capacity;
    }

    protected virtual float GetConfiguredResourceRecoveryPerSecond()
    {
        return resource.recoveryPerSecond;
    }

    protected virtual bool ShouldRecoverResource()
    {
        return true;
    }

    protected virtual void OnFirePressed()
    {
    }

    protected virtual void OnFireHeld()
    {
    }

    protected virtual void OnFireReleased()
    {
    }

    protected virtual void OnWeaponUpdated(float deltaTime)
    {
    }

    protected virtual void OnWeaponFixedUpdated(float deltaTime)
    {
    }

    protected bool IsOnCooldown()
    {
        return UsesCooldownAvailability && Time.time < _cooldownReadyTime;
    }

    protected void StartCooldown(float duration = -1f)
    {
        float resolvedDuration = duration >= 0f ? duration : GetConfiguredCooldownDuration();
        _cooldownReadyTime = Time.time + Mathf.Max(0f, resolvedDuration);
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected bool CanSpendResource(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return true;
        }

        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            return false;
        }

        float clampedAmount = Mathf.Max(0f, amount);
        return _currentResourceUsage + clampedAmount <= capacity + 0.001f;
    }

    protected bool TrySpendResource(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return true;
        }

        float clampedAmount = Mathf.Max(0f, amount);
        if (!CanSpendResource(clampedAmount))
        {
            return false;
        }

        if (clampedAmount <= 0f)
        {
            return true;
        }

        SetResourceUsage(_currentResourceUsage + clampedAmount);
        return true;
    }

    protected void AddResourceUsage(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return;
        }

        SetResourceUsage(_currentResourceUsage + Mathf.Max(0f, amount));
    }

    protected void SetResourceUsage(float amount)
    {
        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            _currentResourceUsage = 0f;
            RaiseAvailabilityChangedIfNeeded(force: true);
            return;
        }

        _currentResourceUsage = Mathf.Clamp(amount, 0f, capacity);
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected void RecordReticleSpinPulse()
    {
        _lastReticleSpinPulseTime = Time.time;
    }

    protected ProjectileFireRequest3D BuildDefaultFireRequest(ProjectileWeaponConfig3D weaponConfig)
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

    protected bool FireProjectilePattern(ProjectileFireRequest3D request, ProjectileWeaponConfig3D fallbackConfig, SoundEffect fireSound = null)
    {
        if (request.projectilePrefab == null)
        {
            return false;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned)
        {
            return _netCombat.TryFireProjectilePattern(this, request, fallbackConfig, fireSound);
        }

        return FireProjectilePatternLocal(request, fallbackConfig, fireSound, cosmeticOnly: false, networkAuthority: null, visualType: NetProjectileVisualType3D.Primary);
    }

    internal bool FireProjectilePatternLocal(
        ProjectileFireRequest3D request,
        ProjectileWeaponConfig3D fallbackConfig,
        SoundEffect fireSound,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        NetProjectileVisualType3D visualType)
    {
        if (request.projectilePrefab == null)
        {
            return false;
        }

        Transform[] muzzles = ResolveFiringMuzzles(request, fallbackConfig);

        string resolvedTargetTag = !string.IsNullOrEmpty(request.targetTag)
            ? request.targetTag
            : fallbackConfig.targetTag;

        AimSolution aim = ResolveAimSolution();
        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            SpawnMuzzleEffect(spawnMuzzle);
            SpawnProjectile(spawnMuzzle, aim, request, resolvedTargetTag, cosmeticOnly, networkAuthority, visualType);
        }

        if (!cosmeticOnly && shipFlight != null && request.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(request.recoilForce);
            networkAuthority?.ApplyCombatVelocityDelta(-transform.forward * request.recoilForce);
        }

        fireSound?.PlayAtPoint(transform.position);
        RecordReticleSpinPulse();
        return true;
    }

    internal void BuildNetworkProjectileRequests(
        ProjectileFireRequest3D request,
        ProjectileWeaponConfig3D fallbackConfig,
        NetProjectileVisualType3D visualType,
        int tick,
        List<NetProjectileFireRequest3D> output)
    {
        if (output == null || request.projectilePrefab == null)
        {
            return;
        }

        Transform[] muzzles = ResolveFiringMuzzles(request, fallbackConfig);
        AimSolution aim = ResolveAimSolution();
        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;

        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            Vector3 spawnPosition = ResolveProjectileSpawnPosition(spawnMuzzle, request);
            Vector3 fireDirection = ResolveFireDirection(spawnMuzzle, spawnPosition, aim);
            Quaternion spawnRotation = Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection));

            output.Add(new NetProjectileFireRequest3D
            {
                Tick = tick,
                SpawnPosition = spawnPosition,
                SpawnRotation = spawnRotation,
                MuzzleEffectPosition = spawnMuzzle.TransformPoint(muzzleEffectLocalOffset),
                MuzzleEffectRotation = spawnMuzzle.rotation,
                Direction = fireDirection,
                InheritedVelocity = inheritedVelocity,
                Speed = request.speed,
                Damage = request.damage,
                Lifetime = request.lifetime,
                ImpactForce = request.impactForce,
                RecoilForce = request.recoilForce,
                ApplyRecoil = request.recoilForce > 0f,
                CanPierce = request.canPierce,
                PierceMultiplier = request.pierceMultiplier,
                AppliesSlow = request.appliesSlow,
                SlowMultiplier = request.slowMultiplier,
                SlowDuration = request.slowDuration,
                SlowEngineEmissionScale = request.slowEngineEmissionScale,
                VisualType = visualType
            });
        }
    }

    internal void SpawnNetworkProjectile(
        GameObject projectilePrefab,
        in NetProjectileFireRequest3D fire,
        string targetTag,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        bool playMuzzleEffect)
    {
        if (projectilePrefab == null)
        {
            return;
        }

        if (playMuzzleEffect)
        {
            SpawnMuzzleEffect(fire.MuzzleEffectPosition, fire.MuzzleEffectRotation);
        }

        GameObject projectileObject = GameObjectPool3D.Spawn(projectilePrefab, fire.SpawnPosition, fire.SpawnRotation);
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        projectile.targetTag = targetTag;
        projectile.SetCosmeticOnly(cosmeticOnly);
        projectile.SetNetworkAuthority(networkAuthority, fire.Tick);
        projectile.SetNetworkVisualType(fire.VisualType);
        projectile.Initialize(
            fire.Direction,
            fire.InheritedVelocity,
            fire.Speed,
            fire.Damage,
            fire.Lifetime,
            fire.ImpactForce,
            owner);

        if (fire.CanPierce)
        {
            if (projectile is GigaBlastProjectile3D gigaBlastProjectile)
            {
                gigaBlastProjectile.EnablePiercing(fire.PierceMultiplier);
            }
        }

        if (fire.AppliesSlow)
        {
            projectile.EnableSlow(fire.SlowMultiplier, fire.SlowDuration, fire.SlowEngineEmissionScale);
        }
    }

    private Transform[] ResolveFiringMuzzles(ProjectileFireRequest3D request, ProjectileWeaponConfig3D fallbackConfig)
    {
        if (request.muzzles != null && request.muzzles.Length > 0)
        {
            return request.muzzles;
        }

        if (request.spawnAnchor != null)
        {
            return new[] { request.spawnAnchor };
        }

        if (fallbackConfig.muzzles != null && fallbackConfig.muzzles.Length > 0)
        {
            return fallbackConfig.muzzles;
        }

        return new[] { transform };
    }

    private void RecoverResource(float deltaTime)
    {
        if (!UsesResourceAvailability || deltaTime <= 0f || _currentResourceUsage <= 0f || !ShouldRecoverResource())
        {
            return;
        }

        float recoveryRate = Mathf.Max(0f, GetConfiguredResourceRecoveryPerSecond());
        if (recoveryRate <= 0f)
        {
            return;
        }

        SetResourceUsage(_currentResourceUsage - (recoveryRate * deltaTime));
    }

    private void SpawnProjectile(
        Transform muzzle,
        AimSolution aim,
        ProjectileFireRequest3D request,
        string targetTag,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        NetProjectileVisualType3D visualType)
    {
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(muzzle, request);
        Vector3 fireDirection = ResolveFireDirection(muzzle, spawnPosition, aim);
        GameObject projectileObject = GameObjectPool3D.Spawn(request.projectilePrefab, spawnPosition, Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection)));
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {request.projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        projectile.targetTag = targetTag;
        projectile.SetCosmeticOnly(cosmeticOnly);
        projectile.SetNetworkAuthority(networkAuthority, NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1);
        projectile.SetNetworkVisualType(visualType);
        projectile.Initialize(
            fireDirection,
            inheritedVelocity,
            request.speed,
            request.damage,
            request.lifetime,
            request.impactForce,
            owner
        );

        if (request.canPierce)
        {
            if (projectile is GigaBlastProjectile3D gigaBlastProjectile)
            {
                gigaBlastProjectile.EnablePiercing(request.pierceMultiplier);
            }
        }

        if (request.appliesSlow)
        {
            projectile.EnableSlow(request.slowMultiplier, request.slowDuration, request.slowEngineEmissionScale);
        }

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

    private void SpawnMuzzleEffect(Vector3 position, Quaternion rotation)
    {
        if (muzzleEffectPrefab == null)
        {
            return;
        }

        GameObject effectObject = GameObjectPool3D.Spawn(muzzleEffectPrefab, position, rotation);
        PooledObject3D pooled = effectObject != null ? effectObject.GetComponent<PooledObject3D>() : null;
        if (pooled != null)
        {
            pooled.ScheduleDespawn(muzzleEffectLifetime);
        }
    }

    private Vector3 ResolveUpVector(Vector3 direction)
    {
        Vector3 up = transform.up;
        if (up.sqrMagnitude <= 0.0001f || Mathf.Abs(Vector3.Dot(up.normalized, direction.normalized)) > 0.995f)
        {
            up = Vector3.up;
        }

        return up;
    }

    private float GetAvailableResourceRatio()
    {
        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - (_currentResourceUsage / capacity));
    }

    private float GetCooldownReadyRatio()
    {
        if (!UsesCooldownAvailability)
        {
            return 1f;
        }

        float cooldown = CooldownDuration;
        if (cooldown <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(1f - (CooldownRemaining / cooldown));
    }

    private void CacheAvailabilitySnapshot()
    {
        _lastAvailabilityChangedReadyRatio = UsesCooldownAvailability ? CooldownReadyRatio : GetAvailableResourceRatio();
        _lastAvailabilityChangedOnCooldown = UsesCooldownAvailability && IsOnCooldown();
        _hasAvailabilitySnapshot = true;
    }

    private void RaiseAvailabilityChangedIfNeeded(bool force = false)
    {
        float currentReadyRatio = UsesCooldownAvailability ? CooldownReadyRatio : GetAvailableResourceRatio();
        bool isOnCooldown = UsesCooldownAvailability && IsOnCooldown();

        if (!force && _hasAvailabilitySnapshot)
        {
            bool ratioUnchanged = Mathf.Abs(currentReadyRatio - _lastAvailabilityChangedReadyRatio) <= 0.0001f;
            bool cooldownStateUnchanged = isOnCooldown == _lastAvailabilityChangedOnCooldown;
            if (ratioUnchanged && cooldownStateUnchanged)
            {
                return;
            }
        }

        _lastAvailabilityChangedReadyRatio = currentReadyRatio;
        _lastAvailabilityChangedOnCooldown = isOnCooldown;
        _hasAvailabilitySnapshot = true;
        AvailabilityChanged?.Invoke(this);
    }
}
