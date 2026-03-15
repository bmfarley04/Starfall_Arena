using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicalProjectileAbility : Ability
{
    [System.Serializable]
    public struct PhysicalProjectileAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between shots (seconds)")]
        public float cooldown;

        [Header("Spawn Point")]
        [Tooltip("Transform where the projectile spawns (drag from Hierarchy)")]
        public Transform spawnPoint;

        [Header("Projectile")]
        [Tooltip("Physical projectile prefab (should use PhysicalProjectile script)")]
        public GameObject projectilePrefab;
        [Tooltip("Damage dealt by the projectile")]
        public float damage;
        [Tooltip("Speed of the projectile")]
        public float speed;
        [Tooltip("Projectile lifetime (seconds)")]
        public float lifetime;
        [Tooltip("Impact force applied to target on hit")]
        public float impactForce;
        [Tooltip("Recoil force applied to ship when firing")]
        public float recoilForce;

        [Header("Sound Effects")]
        [Tooltip("Sound played when firing")]
        public SoundEffect fireSound;
    }

    [Header("Ability 4 - Physical Projectile")]
    public PhysicalProjectileAbilityConfig physicalProjectile;

    private float _lastPhysicalProjectileTime = -999f;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
    }

    public override bool TryUseAbility(InputValue value)
    {
        stats.cooldown = physicalProjectile.cooldown;

        if (isLocked)
        {
            Debug.LogWarning("Class2 ability 4 is locked. It unlocks through round flow unless explicitly unlocked for this scene.", this);
            return false;
        }

        if (!CanUseAbility())
        {
            return false;
        }

        if (physicalProjectile.projectilePrefab == null)
        {
            Debug.LogWarning("Physical Projectile prefab not assigned!");
            return false;
        }

        if (physicalProjectile.spawnPoint == null)
        {
            Debug.LogWarning("Physical Projectile spawn point not assigned!");
            return false;
        }

        _lastPhysicalProjectileTime = Time.time;
        lastUsedAbility = Time.time;
        FirePhysicalProjectile();
        return true;
    }

    public override float GetHUDFillRatio()
    {
        if (physicalProjectile.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastPhysicalProjectileTime;
        if (elapsed >= physicalProjectile.cooldown) return 0f;
        return 1f - (elapsed / physicalProjectile.cooldown);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastPhysicalProjectileTime + physicalProjectile.cooldown;
    }

    private void FirePhysicalProjectile()
    {
        player.shotsFired++;

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                GameObject cosmeticProjectile = Instantiate(
                    physicalProjectile.projectilePrefab,
                    physicalProjectile.spawnPoint.position,
                    Quaternion.identity);

                if (cosmeticProjectile.TryGetComponent(out ProjectileScript cosmeticScript))
                {
                    cosmeticScript.targetTag = player.enemyTag;
                    cosmeticScript.SetCosmeticOnly(true);
                    cosmeticScript.Initialize(
                        transform.up,
                        Vector2.zero,
                        physicalProjectile.speed,
                        physicalProjectile.damage,
                        physicalProjectile.lifetime,
                        physicalProjectile.impactForce,
                        player);
                }
            }

            _netMovement.RequestPrimaryFire(new NetFireRequest
            {
                Tick = NetTickUtil.CurrentTick,
                SpawnPosition = physicalProjectile.spawnPoint.position,
                Direction = ((Vector2)transform.up).normalized,
                InheritedVelocity = Vector2.zero,
                Speed = physicalProjectile.speed,
                Damage = physicalProjectile.damage,
                Lifetime = physicalProjectile.lifetime,
                ImpactForce = physicalProjectile.impactForce,
                RecoilForce = physicalProjectile.recoilForce,
                ApplyRecoil = true,
                PierceMultiplier = 1f,
                SlowMultiplier = 1f,
                SlowDuration = 0f,
                CanPierce = false,
                AppliesSlow = false,
                VisualType = NetProjectileVisualType.Class2PhysicalProjectile,
            });

            if (!_netMovement.IsServer)
            {
                player.ApplyRecoil(physicalProjectile.recoilForce);
            }

            physicalProjectile.fireSound?.Play(player.GetAvailableAudioSource());
            return;
        }

        GameObject projectile = Instantiate(
            physicalProjectile.projectilePrefab,
            physicalProjectile.spawnPoint.position,
            transform.rotation
        );

        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = player.enemyTag;
            projectileScript.Initialize(
                transform.up,
                Vector2.zero,
                physicalProjectile.speed,
                physicalProjectile.damage,
                physicalProjectile.lifetime,
                physicalProjectile.impactForce,
                player
            );
        }

        player.ApplyRecoil(physicalProjectile.recoilForce);

        if (physicalProjectile.fireSound != null)
        {
            physicalProjectile.fireSound.Play(player.GetAvailableAudioSource());
        }
    }
}
