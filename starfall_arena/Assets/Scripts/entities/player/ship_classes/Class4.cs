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

        NetMovement netMovement = GetComponent<NetMovement>();
        bool useNetwork = NetTickUtil.IsActive && netMovement != null && netMovement.IsSpawned && netMovement.IsOwner;
        for (int i = 0; i < burstCount; i++)
        {
            int shotTick = NetTickUtil.CurrentTick;
            bool ignoreCooldown = i > 0;

            if (useNetwork)
            {
                FireBurstShotNetworked(netMovement, shotTick, ignoreCooldown);
            }
            else
            {
                FireBurstShotLocal();
                PrimaryFireExecutionBus.Raise(this, PrimaryFireExecutionSource.PlayerInput);
            }

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

    private void FireBurstShotLocal()
    {
        foreach (var turret in turrets)
        {
            int attackId = BeginTrackedAttack();
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
                    this,
                    attackId
                );
            }
        }

        ApplyRecoil(projectileWeapon.recoilForce);
    }

    private void FireBurstShotNetworked(NetMovement netMovement, int shotTick, bool ignoreCooldown)
    {
        for (int turretIndex = 0; turretIndex < turrets.Length; turretIndex++)
        {
            Transform turret = turrets[turretIndex];
            Vector3 direction = GetFireDirection(turret);

            if (!netMovement.IsServer)
            {
                GameObject cosmeticProjectile = Instantiate(projectileWeapon.prefab, turret.position, Quaternion.identity);
                if (cosmeticProjectile.TryGetComponent(out ProjectileScript cosmeticScript))
                {
                    cosmeticScript.targetTag = enemyTag;
                    cosmeticScript.SetCosmeticOnly(true);
                    cosmeticScript.Initialize(
                        direction,
                        Vector2.zero,
                        projectileWeapon.speed,
                        projectileWeapon.damage,
                        projectileWeapon.lifetime,
                        projectileWeapon.impactForce,
                        this);
                }
            }

            netMovement.RequestPrimaryFire(new NetFireRequest
            {
                Tick = shotTick,
                SpawnPosition = turret.position,
                Direction = direction.normalized,
                InheritedVelocity = Vector2.zero,
                Speed = projectileWeapon.speed,
                Damage = projectileWeapon.damage,
                Lifetime = projectileWeapon.lifetime,
                ImpactForce = projectileWeapon.impactForce,
                RecoilForce = projectileWeapon.recoilForce,
                ApplyRecoil = turretIndex == 0,
                PierceMultiplier = 1f,
                SlowMultiplier = 1f,
                SlowDuration = 0f,
                CanPierce = false,
                AppliesSlow = false,
                VisualType = NetProjectileVisualType.Primary,
                IgnoreCooldown = ignoreCooldown,
                OwnerPredicted = true,
                FireSource = (byte)PrimaryFireExecutionSource.PlayerInput,
            });
        }

        if (!netMovement.IsServer)
        {
            ApplyRecoil(projectileWeapon.recoilForce);
        }
    }
}
