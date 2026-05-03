using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Reflector3D : Ability3D
{
    [System.Serializable]
    public struct ReflectAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses in seconds.")]
        public float cooldown;
        [Tooltip("How long the reflect shield stays active in seconds.")]
        public float activeDuration;

        [Header("Shield")]
        [Tooltip("ReflectShield3D component on the shield mesh object.")]
        public ReflectShield3D shield;

        [Header("Reflection")]
        [Tooltip("Shield color while the reflect window is active.")]
        public Color reflectedProjectileColor;
        [Tooltip("Damage multiplier applied when a projectile is reflected.")]
        [Range(0f, 5f)]
        public float reflectedProjectileDamageMultiplier;
        [Tooltip("Sound played when the shield successfully reflects a projectile.")]
        public SoundEffect reflectedProjectileSound;
    }

    [Header("Ability 2 - Reflect Shield")]
    [SerializeField] private ReflectAbilityConfig3D reflect;

    private Coroutine _reflectCoroutine;
    private NetCombat3D _netCombat;

    protected override void Awake()
    {
        base.Awake();
        _netCombat = GetComponent<NetCombat3D>();
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (value.isPressed && reflect.shield == null)
        {
            Debug.LogWarning("Reflector3D is missing its ReflectShield3D reference.", this);
            return false;
        }

        return base.TryUseAbility(value);
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            _netCombat.RequestReflectActivation();
            if (!_netCombat.IsServer)
            {
                ApplyNetworkReflectActivation(authoritative: false);
            }
            return;
        }

        ApplyNetworkReflectActivation(authoritative: true);
    }

    public void ApplyNetworkReflectActivation(bool authoritative)
    {
        if (_reflectCoroutine != null)
        {
            StopCoroutine(_reflectCoroutine);
        }

        _reflectCoroutine = StartCoroutine(ActivateReflectShield());
    }

    public bool TryReflectProjectile(Projectile3D projectile, Vector3 hitPoint)
    {
        if (reflect.shield == null || !reflect.shield.IsActive() || projectile == null || entity == null)
        {
            return false;
        }

        if (projectile.TargetFaction != Faction3D.Neutral
            && FactionMember3D.ResolveFaction(entity) != projectile.TargetFaction)
        {
            return false;
        }

        if (projectile.TargetFaction == Faction3D.Neutral
            && !string.IsNullOrEmpty(projectile.targetTag)
            && !entity.CompareTag(projectile.targetTag))
        {
            return false;
        }

        reflect.shield.OnReflectHit(hitPoint);
        bool reflected = reflect.shield.ReflectProjectile(projectile, reflect.reflectedProjectileColor, reflect.reflectedProjectileDamageMultiplier);
        if (reflected)
        {
            reflect.reflectedProjectileSound?.PlayAtPoint(hitPoint);
        }

        return reflected;
    }

    public override bool IsAbilityActive()
    {
        return reflect.shield != null && reflect.shield.IsActive();
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
    }

    protected override float GetCooldownDuration()
    {
        return reflect.cooldown;
    }

    public void ApplyProfile(Class1PlayerBalanceProfile3D.Class1Stats stats)
    {
        reflect.cooldown = Mathf.Max(0f, stats.reflectCooldown);
        reflect.activeDuration = Mathf.Max(0f, stats.reflectActiveDuration);
        reflect.reflectedProjectileDamageMultiplier = Mathf.Clamp(stats.reflectedProjectileDamageMultiplier, 0f, 5f);
    }

    private IEnumerator ActivateReflectShield()
    {
        reflect.shield.Activate(reflect.reflectedProjectileColor);
        yield return new WaitForSeconds(reflect.activeDuration);
        reflect.shield.Deactivate();
        _reflectCoroutine = null;
    }
}
