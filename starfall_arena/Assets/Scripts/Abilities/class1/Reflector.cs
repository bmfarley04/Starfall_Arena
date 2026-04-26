using UnityEngine;
using UnityEngine.InputSystem;

public class Reflector : Ability
{
    [System.Serializable]
    public struct ReflectAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Shield active duration (seconds)")]
        public float activeDuration;

        [Header("Shield")]
        [Tooltip("ReflectShield component (drag from Hierarchy)")]
        public ReflectShield shield;

        [Header("Reflection")]
        [Tooltip("Color of reflected projectiles")]
        public Color reflectedProjectileColor;
        [Tooltip("Damage multiplier for reflected projectiles (1.0 = same damage, 2.0 = double damage)")]
        [Range(0.5f, 5f)]
        public float reflectedProjectileDamageMultiplier;

        [Header("Sound Effects")]
        [Tooltip("Reflect shield duration sound (loops while active)")]
        public SoundEffect shieldLoopSound;
        [Tooltip("Bullet reflection impact sound")]
        public SoundEffect bulletReflectionSound;
    }

    [Header("Ability 2 - Reflect Shield (Parry)")]
    public ReflectAbilityConfig reflect;

    // ===== PRIVATE STATE =====
    private float _lastReflectTime = -999f;
    private Coroutine _reflectCoroutine;
    private AudioSource _reflectShieldSource;
    private NetMovement _netMovement;


    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
        _reflectShieldSource = gameObject.AddComponent<AudioSource>();
        _reflectShieldSource.playOnAwake = false;
        _reflectShieldSource.loop = true;
        _reflectShieldSource.spatialBlend = 0f;
    }

    protected void Update()
    {

    }

    void FixedUpdate()
    {

    }
    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        if (Time.time < _lastReflectTime + reflect.cooldown)
        {
            return;
        }

        if (reflect.shield == null)
        {
            Debug.LogWarning("Reflect shield not assigned!");
            return;
        }

        _lastReflectTime = Time.time;
        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        ApplyNetworkReflectActivation(authoritative: useNetworkPath && _netMovement.IsServer);
        if (useNetworkPath)
        {
            _netMovement.RequestReflectActivation();
        }
    }

    public override bool IsAbilityActive()
    {
        return reflect.shield != null && reflect.shield.IsActive();
    }

    public override bool HasDamageMitigation()
    {
        return reflect.shield != null && reflect.shield.IsActive();
    }

    public override bool HasCollisionModification()
    {
        return reflect.shield != null && reflect.shield.IsActive();
    }

    public override void ProcessCollisionModification(Collider2D collider)
    {
        if (reflect.shield != null && reflect.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == player.thisPlayerTag)
            {
                // Guided missiles aren't reflected — Missile.OnTriggerEnter2D detonates them harmlessly on contact.
                if (projectile is Missile)
                {
                    return;
                }

                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                reflect.shield.OnReflectHit(hitPoint);
                reflect.shield.ReflectProjectile(projectile, player.enemyTag);

                projectile.MarkAsReflected();

                projectile.ApplyDamageMultiplier(reflect.reflectedProjectileDamageMultiplier);

                if (reflect.bulletReflectionSound != null)
                {
                    reflect.bulletReflectionSound.Play(player.GetAvailableAudioSource());
                }

                // Broadcast reflected projectile to clients
                if (NetTickUtil.IsActive && _netMovement != null && _netMovement.IsServer)
                {
                    _netMovement.BroadcastReflectedProjectile(new NetReflectedProjectileData
                    {
                        SpawnPosition = projectile.transform.position,
                        Direction = projectile.GetDirection(),
                        Speed = projectile.GetSpeed(),
                        Damage = projectile.GetDamage(),
                        Lifetime = projectile.GetLifetime(),
                        ImpactForce = projectile.GetImpactForce(),
                        ReflectColor = reflect.reflectedProjectileColor,
                        VisualType = NetProjectileVisualType.Primary,
                    });
                }
            }
        }
    }

    // ===== HUD STATE =====
    public override float GetHUDFillRatio()
    {
        if (reflect.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastReflectTime;
        if (elapsed >= reflect.cooldown) return 0f;
        return 1f - (elapsed / reflect.cooldown);
    }
    public override bool IsOnCooldown()
    {
        return Time.time < _lastReflectTime + reflect.cooldown;
    }

    public override void Die()
    {
        if (_reflectCoroutine != null)
        {
            StopCoroutine(_reflectCoroutine);
            _reflectCoroutine = null;
        }

        if (reflect.shield != null && reflect.shield.IsActive())
        {
            reflect.shield.Deactivate();
        }

        base.Die();
        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
    }

    // ===== COROUTINES =====
    private System.Collections.IEnumerator ActivateReflectShield()
    {
        reflect.shield.Activate(reflect.reflectedProjectileColor);

        if (reflect.shieldLoopSound != null && _reflectShieldSource != null)
        {
            reflect.shieldLoopSound.Play(_reflectShieldSource);
        }

        yield return new WaitForSeconds(reflect.activeDuration);

        reflect.shield.Deactivate();

        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
    }

    public void ApplyNetworkReflectActivation(bool authoritative)
    {
        _lastReflectTime = Time.time;

        if (_reflectCoroutine != null)
        {
            StopCoroutine(_reflectCoroutine);
        }

        _reflectCoroutine = StartCoroutine(ActivateReflectShield());
    }
}
