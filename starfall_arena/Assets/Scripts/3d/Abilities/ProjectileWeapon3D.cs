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

    public ProjectileWeaponConfig3D WeaponConfig => weaponConfig;

    public void SetWeaponConfig(ProjectileWeaponConfig3D config)
    {
        weaponConfig = config;
    }

    public bool TryFire()
    {
        if (IsOnCooldown())
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
