using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GuidedMissileWeapon3D : Weapon3D
{
    [System.Serializable]
    public struct MissileVariantConfig3D
    {
        public GameObject missilePrefab;
        public float damageMultiplier;
        public float speedMultiplier;
        public float impactMultiplier;
        public float recoilMultiplier;
        public float lifetimeOverride;
        public float sizeMultiplier;
    }

    [System.Serializable]
    public struct GuidedMissileConfig3D
    {
        [Header("Core")]
        public ProjectileWeaponConfig3D baseProjectile;
        public Transform spawnPoint;
        public float spawnOffset;

        [Header("Empowerment")]
        public Empower3D empowerAbility;
        public bool forceEmpowered;

        [Header("Sound Effects")]
        public SoundEffect fireSound;

        [Header("Regular Missile")]
        public MissileVariantConfig3D regular;

        [Header("Empowered Missile")]
        public MissileVariantConfig3D empowered;
    }

    [Header("Class4 Guided Missile")]
    [SerializeField] private GuidedMissileConfig3D guidedMissile = new GuidedMissileConfig3D
    {
        spawnOffset = 2f
    };

    private float _nextFireTime = float.NegativeInfinity;

    public SoundEffect NetworkFireSound => guidedMissile.fireSound;
    public GameObject RegularProjectilePrefab => guidedMissile.regular.missilePrefab;
    public GameObject EmpoweredProjectilePrefab => guidedMissile.empowered.missilePrefab;

    protected override void Awake()
    {
        base.Awake();
        if (guidedMissile.empowerAbility == null)
        {
            guidedMissile.empowerAbility = GetComponent<Empower3D>();
        }
    }

    protected override float GetConfiguredCooldownDuration()
    {
        return guidedMissile.baseProjectile.cooldown;
    }

    protected override IEnumerable<GameObject> GetPrewarmProjectilePrefabs()
    {
        if (guidedMissile.regular.missilePrefab != null)
        {
            yield return guidedMissile.regular.missilePrefab;
        }

        if (guidedMissile.empowered.missilePrefab != null
            && guidedMissile.empowered.missilePrefab != guidedMissile.regular.missilePrefab)
        {
            yield return guidedMissile.empowered.missilePrefab;
        }
    }

    protected override void OnFireHeld()
    {
        TryFire();
    }

    public override bool IsReticleSpinActive()
    {
        return IsFireHeld;
    }

    public bool TryFire()
    {
        if (Time.time < _nextFireTime)
        {
            return false;
        }

        bool empowered = IsEmpoweredActive();
        MissileVariantConfig3D variant = empowered ? guidedMissile.empowered : guidedMissile.regular;
        GameObject projectilePrefab = variant.missilePrefab != null ? variant.missilePrefab : guidedMissile.baseProjectile.projectilePrefab;
        if (projectilePrefab == null)
        {
            return false;
        }

        ProjectileFireRequest3D request = BuildDefaultFireRequest(guidedMissile.baseProjectile);
        request.projectilePrefab = projectilePrefab;
        request.spawnAnchor = guidedMissile.spawnPoint;
        request.muzzles = null;
        request.forwardOffset = guidedMissile.spawnOffset;
        request.speed = guidedMissile.baseProjectile.speed * Mathf.Max(0f, variant.speedMultiplier);
        request.damage = guidedMissile.baseProjectile.damage * Mathf.Max(0f, variant.damageMultiplier);
        request.impactForce = guidedMissile.baseProjectile.impactForce * Mathf.Max(0f, variant.impactMultiplier);
        request.recoilForce = guidedMissile.baseProjectile.recoilForce * Mathf.Max(0f, variant.recoilMultiplier);
        request.lifetime = variant.lifetimeOverride > 0f ? variant.lifetimeOverride : guidedMissile.baseProjectile.lifetime;
        request.projectileScaleMultiplier = Mathf.Max(0.01f, variant.sizeMultiplier > 0f ? variant.sizeMultiplier : 1f);

        if (!FireProjectilePattern(request, guidedMissile.baseProjectile, guidedMissile.fireSound))
        {
            return false;
        }

        _nextFireTime = Time.time + Mathf.Max(0f, guidedMissile.baseProjectile.cooldown);
        StartCooldown();
        return true;
    }

    public NetProjectileVisualType3D ResolveVisualTypeForProjectile(GameObject projectilePrefab)
    {
        if (IsEmpoweredActive())
        {
            return NetProjectileVisualType3D.Class4GuidedMissileEmpowered;
        }

        return NetProjectileVisualType3D.Class4GuidedMissile;
    }

    private bool IsEmpoweredActive()
    {
        if (guidedMissile.forceEmpowered)
        {
            return true;
        }

        return guidedMissile.empowerAbility != null && guidedMissile.empowerAbility.IsEmpoweredActive;
    }
}
