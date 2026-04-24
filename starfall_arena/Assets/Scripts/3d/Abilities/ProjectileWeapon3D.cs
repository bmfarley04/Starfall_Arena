using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileWeapon3D : Weapon3D
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

    [Header("Audio")]
    [SerializeField] private SoundEffect fireSound;

    private float _nextFireTime = float.NegativeInfinity;

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;
    public SoundEffect NetworkFireSound => fireSound;

    public void SetWeaponConfig(ProjectileWeaponConfig3D config)
    {
        weaponConfig = config;
    }

    public bool TryFire()
    {
        if (Time.time < _nextFireTime)
        {
            return false;
        }

        ProjectileFireRequest3D request = BuildDefaultFireRequest(weaponConfig);
        if (request.projectilePrefab == null || !TrySpendResource(weaponConfig.energyCost))
        {
            return false;
        }

        if (!FireProjectilePattern(request, weaponConfig, fireSound))
        {
            return false;
        }

        _nextFireTime = Time.time + Mathf.Max(0f, weaponConfig.cooldown);
        StartCooldown();
        return true;
    }

    public bool Fire(ProjectileFireRequest3D request, bool consumeCooldown = false)
    {
        bool fired = FireProjectilePattern(request, weaponConfig, fireSound);
        if (!fired)
        {
            return false;
        }

        if (consumeCooldown)
        {
            _nextFireTime = Time.time + Mathf.Max(0f, weaponConfig.cooldown);
            StartCooldown();
        }

        return true;
    }

    public override bool IsReticleSpinActive()
    {
        return IsFireHeld;
    }

    protected override float GetConfiguredCooldownDuration()
    {
        return weaponConfig.cooldown;
    }

    protected override IEnumerable<GameObject> GetPrewarmProjectilePrefabs()
    {
        if (weaponConfig.projectilePrefab != null)
        {
            yield return weaponConfig.projectilePrefab;
        }
    }

    protected override void OnFireHeld()
    {
        TryFire();
    }
}
