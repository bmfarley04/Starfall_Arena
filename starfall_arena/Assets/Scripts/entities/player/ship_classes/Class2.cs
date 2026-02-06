using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Class2 : Player
{
    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    [SerializeField] private float _fireCooldown = 0.5f;

    [Header("Convergence Settings")]
    [Tooltip("Distance ahead of ship where all projectiles converge (all shots meet at this point)")]
    [SerializeField] private float convergenceDistance = 20f;

    protected override void Awake()
    {
        base.Awake();
        // Sync Inspector value to parent's protected field
        fireCooldown = _fireCooldown;
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void TryFireProjectile()
    {
        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        // Calculate convergence point ahead of ship
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;

        for (int i = 0; i < turrets.Length; i++)
        {
            Transform turret = turrets[i];

            // Calculate direction from turret to convergence point
            Vector3 directionToConvergence = (convergencePoint - turret.position).normalized;

            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    projectileWeapon.speed,
                    projectileWeapon.damage,
                    projectileWeapon.lifetime,
                    projectileWeapon.impactForce,
                    this
                );
            }
        }

        ApplyRecoil(projectileWeapon.recoilForce);

        if (projectileFireSound != null)
        {
            projectileFireSound.Play(GetAvailableAudioSource());
        }

        _lastFireTime = Time.time;
    }
}
