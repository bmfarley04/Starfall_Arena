using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyProjectileWeaponBase3D : MonoBehaviour, IEnemyProjectileWeapon3D
{
    [Header("Weapon Core")]
    [SerializeField] protected ProjectileWeaponConfig3D weaponConfig = new ProjectileWeaponConfig3D
    {
        cooldown = 0.25f,
        speed = 120f,
        damage = 10f,
        lifetime = 5f,
        impactForce = 0f,
        recoilForce = 0f,
        energyCost = 0f,
        targetTag = string.Empty,
        targetFaction = Faction3D.PlayerTeam
    };

    [Tooltip("Owning combat entity passed into the projectile as its shooter. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private Entity3D owner;

    [Tooltip("Optional ship flight used only for inherited projectile velocity and recoil. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private ShipFlight3D shipFlight;

    [Tooltip("Optional Rigidbody used for inherited projectile launch velocity on AI movers that do not use ShipFlight3D as their active motor.")]
    [SerializeField] private Rigidbody firingBody;

    [Header("Audio")]
    [Tooltip("One-shot weapon fire sound played when this enemy launches a volley.")]
    [SerializeField] private SoundEffect fireSound;

    [Header("Muzzle FX")]
    [Tooltip("Optional pooled muzzle flash prefab spawned once per firing muzzle.")]
    [SerializeField] private GameObject muzzleEffectPrefab;

    [Tooltip("How long the pooled muzzle effect should stay alive before despawning.")]
    [SerializeField] private float muzzleEffectLifetime = 2f;

    [Tooltip("If true, spawned muzzle FX stay parented to the firing muzzle instead of the scene root.")]
    [SerializeField] private bool parentMuzzleEffectToMuzzle = true;

    [Tooltip("Local offset applied from each muzzle position before spawning the projectile body.")]
    [SerializeField] private Vector3 projectileSpawnLocalOffset = new Vector3(0f, 0f, 2f);

    [Tooltip("Local offset applied from each muzzle when spawning the muzzle flash.")]
    [SerializeField] private Vector3 muzzleEffectLocalOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Pooling")]
    [Tooltip("How many projectile instances to prewarm for this weapon's projectile prefab.")]
    [SerializeField] private int projectilePrewarmCount = 12;

    [Tooltip("How many muzzle-flash instances to prewarm when a muzzle FX prefab is assigned.")]
    [SerializeField] private int muzzleEffectPrewarmCount = 4;

    private float _nextFireTime = float.NegativeInfinity;
    private bool _loggedInvalidProjectilePrefab;
    private bool _loggedMissingProjectileComponent;

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;
    public SoundEffect NetworkFireSound => fireSound;
    public abstract NetProjectileVisualType3D NetworkVisualType { get; }

    protected Entity3D Owner => owner;
    protected ShipFlight3D ShipFlight => shipFlight;

    protected virtual void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        shipFlight ??= GetComponent<ShipFlight3D>();
        firingBody ??= GetComponent<Rigidbody>();

        GameObject projectilePrefab = GetProjectilePrefab();
        if (projectilePrefab != null)
        {
            GameObjectPool3D.Prewarm(projectilePrefab, projectilePrewarmCount);
        }

        if (muzzleEffectPrefab != null)
        {
            GameObjectPool3D.Prewarm(muzzleEffectPrefab, muzzleEffectPrewarmCount);
        }
    }

    public bool TryFireAtFaction(Faction3D targetFaction)
    {
        if (!TryConsumeFireGate())
        {
            return false;
        }

        return FireLocalVolley(targetFaction);
    }

    public bool TryConsumeFireGate()
    {
        if (Time.time < _nextFireTime)
        {
            return false;
        }

        if (!HasValidProjectilePrefab())
        {
            return false;
        }

        _nextFireTime = Time.time + Mathf.Max(0f, weaponConfig.cooldown);
        return true;
    }

    public void BuildNetworkProjectileRequests(Faction3D targetFaction, int tick, List<NetProjectileFireRequest3D> output)
    {
        if (output == null || !HasValidProjectilePrefab())
        {
            return;
        }

        Transform[] muzzles = ResolveFiringMuzzles();
        Vector3 inheritedVelocity = GetInheritedVelocity();

        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            Vector3 fireDirection = ResolveFireDirection(spawnMuzzle);
            output.Add(new NetProjectileFireRequest3D
            {
                Tick = tick,
                SpawnPosition = ResolveProjectileSpawnPosition(spawnMuzzle),
                SpawnRotation = Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection)),
                MuzzleEffectPosition = spawnMuzzle.TransformPoint(muzzleEffectLocalOffset),
                MuzzleEffectRotation = spawnMuzzle.rotation,
                Direction = fireDirection,
                InheritedVelocity = inheritedVelocity,
                Speed = weaponConfig.speed,
                Damage = weaponConfig.damage,
                Lifetime = weaponConfig.lifetime,
                ImpactForce = weaponConfig.impactForce,
                RecoilForce = weaponConfig.recoilForce,
                ApplyRecoil = weaponConfig.recoilForce > 0f && i == 0,
                ProjectileScaleMultiplier = 1f,
                TargetFaction = targetFaction,
                VisualType = NetworkVisualType,
                AccuracyAttackId = PlayerCombatStats3D.InvalidAttackId
            });
        }
    }

    public void SpawnNetworkProjectile(
        NetProjectileFireRequest3D fire,
        string targetTag,
        Faction3D targetFaction,
        bool cosmeticOnly,
        bool playMuzzleEffect,
        bool serverAuthoritativeGameplay)
    {
        GameObject projectilePrefab = GetProjectilePrefab();
        if (projectilePrefab == null)
        {
            return;
        }

        if (playMuzzleEffect)
        {
            SpawnMuzzleEffect(fire.MuzzleEffectPosition, fire.MuzzleEffectRotation);
        }

        SpawnProjectileInstance(projectilePrefab, fire, targetTag, targetFaction, cosmeticOnly, serverAuthoritativeGameplay);
    }

    public bool UsesVisualType(NetProjectileVisualType3D visualType)
    {
        return visualType == NetworkVisualType;
    }

    public GameObject GetProjectilePrefab()
    {
        return weaponConfig.projectilePrefab;
    }

    protected virtual bool ValidateProjectilePrefab(GameObject projectilePrefab)
    {
        return projectilePrefab != null;
    }

    private bool FireLocalVolley(Faction3D targetFaction)
    {
        GameObject projectilePrefab = GetProjectilePrefab();
        if (projectilePrefab == null)
        {
            return false;
        }

        Transform[] muzzles = ResolveFiringMuzzles();
        int spawnedCount = 0;

        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            Vector3 fireDirection = ResolveFireDirection(spawnMuzzle);
            SpawnMuzzleEffect(spawnMuzzle);

            NetProjectileFireRequest3D fire = new NetProjectileFireRequest3D
            {
                Tick = NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1,
                SpawnPosition = ResolveProjectileSpawnPosition(spawnMuzzle),
                SpawnRotation = Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection)),
                Direction = fireDirection,
                InheritedVelocity = GetInheritedVelocity(),
                Speed = weaponConfig.speed,
                Damage = weaponConfig.damage,
                Lifetime = weaponConfig.lifetime,
                ImpactForce = weaponConfig.impactForce,
                TargetFaction = targetFaction,
                VisualType = NetworkVisualType,
                ProjectileScaleMultiplier = 1f,
                AccuracyAttackId = PlayerCombatStats3D.InvalidAttackId
            };

            if (SpawnProjectileInstance(projectilePrefab, fire, string.Empty, targetFaction, cosmeticOnly: false, serverAuthoritativeGameplay: false))
            {
                spawnedCount++;
            }
        }

        if (spawnedCount <= 0)
        {
            return false;
        }

        if (shipFlight != null && weaponConfig.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(weaponConfig.recoilForce);
        }

        fireSound?.PlayAtPoint(transform.position);
        return true;
    }

    private bool SpawnProjectileInstance(
        GameObject projectilePrefab,
        NetProjectileFireRequest3D fire,
        string targetTag,
        Faction3D targetFaction,
        bool cosmeticOnly,
        bool serverAuthoritativeGameplay)
    {
        GameObject projectileObject = GameObjectPool3D.Spawn(projectilePrefab, fire.SpawnPosition, fire.SpawnRotation);
        if (projectileObject == null || !projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            if (!_loggedMissingProjectileComponent)
            {
                Debug.LogWarning($"[{GetType().Name}] Projectile prefab {projectilePrefab.name} is missing Projectile3D.", this);
                _loggedMissingProjectileComponent = true;
            }

            return false;
        }

        projectile.targetTag = targetTag;
        projectile.TargetFaction = targetFaction != Faction3D.Neutral ? targetFaction : fire.TargetFaction;
        projectile.SetCosmeticOnly(cosmeticOnly);
        projectile.SetNetworkAuthority(null, fire.Tick);
        projectile.SetServerAuthoritativeGameplay(serverAuthoritativeGameplay);
        projectile.SetNetworkVisualType(fire.VisualType);
        projectile.Initialize(
            fire.Direction,
            fire.InheritedVelocity,
            fire.Speed,
            fire.Damage,
            fire.Lifetime,
            fire.ImpactForce,
            owner,
            fire.AccuracyAttackId);

        return true;
    }

    private bool HasValidProjectilePrefab()
    {
        GameObject projectilePrefab = GetProjectilePrefab();
        if (projectilePrefab == null)
        {
            return false;
        }

        if (ValidateProjectilePrefab(projectilePrefab))
        {
            return true;
        }

        if (!_loggedInvalidProjectilePrefab)
        {
            Debug.LogWarning($"[{GetType().Name}] {name} has an invalid projectile prefab assignment for this enemy weapon type.", this);
            _loggedInvalidProjectilePrefab = true;
        }

        return false;
    }

    private Transform[] ResolveFiringMuzzles()
    {
        if (weaponConfig.muzzles != null && weaponConfig.muzzles.Length > 0)
        {
            return weaponConfig.muzzles;
        }

        return new[] { transform };
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform muzzle)
    {
        return muzzle.position
            + (muzzle.right * projectileSpawnLocalOffset.x)
            + (muzzle.up * projectileSpawnLocalOffset.y)
            + (muzzle.forward * projectileSpawnLocalOffset.z);
    }

    private Vector3 ResolveFireDirection(Transform muzzle)
    {
        Vector3 fireDirection = muzzle != null ? muzzle.forward : transform.forward;
        if (fireDirection.sqrMagnitude <= 0.0001f)
        {
            fireDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
        }

        return fireDirection.normalized;
    }

    private void SpawnMuzzleEffect(Transform muzzle)
    {
        if (muzzle == null || muzzleEffectPrefab == null)
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

    private Vector3 GetInheritedVelocity()
    {
        if (firingBody != null)
        {
            return firingBody.linearVelocity;
        }

        return shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
    }
}
