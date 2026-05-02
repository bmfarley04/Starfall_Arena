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
        targetTag = "Enemy",
        targetFaction = Faction3D.Neutral
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

    public void ApplyProfile(PlayerBalanceProfile3D.ProjectileWeaponStats stats)
    {
        weaponConfig.cooldown = Mathf.Max(0f, stats.cooldown);
        weaponConfig.speed = Mathf.Max(0f, stats.speed);
        weaponConfig.damage = Mathf.Max(0f, stats.damage);
        weaponConfig.lifetime = Mathf.Max(0f, stats.lifetime);
        weaponConfig.energyCost = Mathf.Max(0f, stats.energyCost);
    }

    public PlayerBalanceProfile3D.ProjectileWeaponStats CaptureProfileStats()
    {
        return new PlayerBalanceProfile3D.ProjectileWeaponStats
        {
            cooldown = weaponConfig.cooldown,
            speed = weaponConfig.speed,
            damage = weaponConfig.damage,
            lifetime = weaponConfig.lifetime,
            energyCost = weaponConfig.energyCost
        };
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

    public bool TryFireAtFaction(Faction3D targetFaction, string targetTagOverride = null)
    {
        if (Time.time < _nextFireTime)
        {
            return false;
        }

        ProjectileFireRequest3D request = BuildDefaultFireRequest(weaponConfig);
        request.targetFaction = targetFaction;
        if (targetTagOverride != null)
        {
            request.targetTag = targetTagOverride;
        }

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

    public bool CanFireNow()
    {
        return Time.time >= _nextFireTime && CanSpendResource(weaponConfig.energyCost);
    }

    public bool TryConsumeFireGate()
    {
        if (!CanFireNow() || !TrySpendResource(weaponConfig.energyCost))
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
