using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== CLASS4 IMPLEMENTATION =====
public class Class4 : Player
{
    // ===== BURST FIRE CONFIG =====
    [Header("Primary Weapon")]
    [Tooltip("Time in seconds between bursts")]
    [SerializeField] private float baseFireCooldown = 0.5f;

    [Header("Burst Fire")]
    [Tooltip("Number of shots per burst")]
    [SerializeField] private int burstCount = 3;
    [Tooltip("Time in seconds between each shot in the burst")]
    [SerializeField] private float burstInterval = 0.08f;

    private bool _isBursting = false;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        fireCooldown = baseFireCooldown;
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    // ===== BURST FIRE =====
    protected override void TryFireProjectile()
    {
        if (isMovementLocked) return;
        if (projectileWeapon.prefab == null) return;
        if (Time.time < _lastFireTime + fireCooldown) return;
        if (_isBursting) return;

        _lastFireTime = Time.time;
        StartCoroutine(FireBurst());
    }

    private IEnumerator FireBurst()
    {
        _isBursting = true;

        for (int i = 0; i < burstCount; i++)
        {
            shotsFired += turrets.Length;

            foreach (var turret in turrets)
            {
                GameObject projectile = Instantiate(projectileWeapon.prefab, turret.position, transform.rotation);

                if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
                {
                    projectileScript.targetTag = enemyTag;
                    projectileScript.Initialize(
                        GetFireDirection(turret),
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

            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        _isBursting = false;
    }
}
