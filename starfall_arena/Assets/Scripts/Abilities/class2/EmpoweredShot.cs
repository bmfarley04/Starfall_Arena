using UnityEngine;
using UnityEngine.InputSystem;

public class EmpoweredShot : Ability
{
    [System.Serializable]
    public struct EmpoweredShotAbilityConfig
    {
        [Header("Cooldown")]
        [Tooltip("Cooldown between empowered shots (seconds)")]
        public float cooldown;

        [Header("Projectile")]
        [Tooltip("Empowered projectile prefab (different visual from normal shots)")]
        public GameObject projectilePrefab;
        [Tooltip("Damage multiplier compared to normal shot")]
        public float damageMultiplier;
        [Tooltip("Speed multiplier compared to normal shot")]
        public float speedMultiplier;
        [Tooltip("Impact force multiplier compared to normal shot")]
        public float impactMultiplier;
        [Tooltip("Recoil multiplier compared to normal shot")]
        public float recoilMultiplier;
        [Tooltip("Projectile lifetime (seconds)")]
        public float lifetime;

        [Header("Slow Effect")]
        [Tooltip("Slow multiplier applied to target (0.5 = 50% speed)")]
        [Range(0f, 1f)]
        public float slowMultiplier;
        [Tooltip("Duration of slow effect (seconds)")]
        public float slowDuration;

        [Header("Sound Effects")]
        [Tooltip("Sound played when firing empowered shot")]
        public SoundEffect fireSound;
    }

    [Header("Ability 1 - Empowered Shot")]
    public EmpoweredShotAbilityConfig empoweredShot;

    private float _lastEmpoweredShotTime = -999f;
    private Class2 _class2;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _class2 = player as Class2;
        _netMovement = GetComponent<NetMovement>();
    }

    public override bool TryUseAbility(InputValue value)
    {
        stats.cooldown = empoweredShot.cooldown;

        if (!CanUseAbility())
        {
            return false;
        }

        if (empoweredShot.projectilePrefab == null)
        {
            Debug.LogWarning("Empowered Shot projectile prefab not assigned!");
            return false;
        }

        _lastEmpoweredShotTime = Time.time;
        lastUsedAbility = Time.time;
        FireEmpoweredShot();
        return true;
    }

    public override float GetHUDFillRatio()
    {
        if (empoweredShot.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastEmpoweredShotTime;
        if (elapsed >= empoweredShot.cooldown) return 0f;
        return 1f - (elapsed / empoweredShot.cooldown);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastEmpoweredShotTime + empoweredShot.cooldown;
    }

    private void FireEmpoweredShot()
    {
        float damage = player.projectileWeapon.damage * empoweredShot.damageMultiplier;
        float speed = player.projectileWeapon.speed * empoweredShot.speedMultiplier;
        float impactForce = player.projectileWeapon.impactForce * empoweredShot.impactMultiplier;
        float recoil = player.projectileWeapon.recoilForce * empoweredShot.recoilMultiplier;

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        int attackId = useNetworkPath ? Player.InvalidAttackId : player.BeginTrackedAttack();
        for (int i = 0; i < player.turrets.Length; i++)
        {
            Transform turret = player.turrets[i];
            Vector3 directionToConvergence = _class2 != null
                ? _class2.GetConvergingFireDirection(turret)
                : transform.up;

            if (useNetworkPath)
            {
                if (!_netMovement.IsServer)
                {
                    GameObject cosmeticProjectile = Instantiate(empoweredShot.projectilePrefab, turret.position, Quaternion.identity);
                    if (cosmeticProjectile.TryGetComponent(out ProjectileScript cosmeticScript))
                    {
                        cosmeticScript.targetTag = player.enemyTag;
                        cosmeticScript.SetCosmeticOnly(true);
                        cosmeticScript.Initialize(
                            directionToConvergence,
                            Vector2.zero,
                            speed,
                            damage,
                            empoweredShot.lifetime,
                            impactForce,
                            player);
                        cosmeticScript.EnableSlow(empoweredShot.slowMultiplier, empoweredShot.slowDuration);
                    }
                }

                _netMovement.RequestPrimaryFire(new NetFireRequest
                {
                    Tick = NetTickUtil.CurrentTick,
                    SpawnPosition = turret.position,
                    Direction = directionToConvergence.normalized,
                    InheritedVelocity = Vector2.zero,
                    Speed = speed,
                    Damage = damage,
                    Lifetime = empoweredShot.lifetime,
                    ImpactForce = impactForce,
                    RecoilForce = recoil,
                    ApplyRecoil = i == 0,
                    PierceMultiplier = 1f,
                    SlowMultiplier = empoweredShot.slowMultiplier,
                    SlowDuration = empoweredShot.slowDuration,
                    CanPierce = false,
                    AppliesSlow = true,
                    VisualType = NetProjectileVisualType.Class2EmpoweredShot,
                    IgnoreCooldown = false,
                    OwnerPredicted = true,
                    FireSource = (byte)PrimaryFireExecutionSource.PlayerInput,
                });

                continue;
            }

            GameObject projectile = Instantiate(empoweredShot.projectilePrefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = player.enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    speed,
                    damage,
                    empoweredShot.lifetime,
                    impactForce,
                    player,
                    attackId
                );

                projectileScript.EnableSlow(empoweredShot.slowMultiplier, empoweredShot.slowDuration);
            }
        }

        if (!useNetworkPath || !_netMovement.IsServer)
        {
            player.ApplyRecoil(recoil);
        }

        if (empoweredShot.fireSound != null)
        {
            empoweredShot.fireSound.Play(player.GetAvailableAudioSource());
        }
    }
}
