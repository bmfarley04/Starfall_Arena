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

        if (!string.IsNullOrEmpty(projectile.targetTag) && !entity.CompareTag(projectile.targetTag))
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

    private IEnumerator ActivateReflectShield()
    {
        reflect.shield.Activate(reflect.reflectedProjectileColor);
        yield return new WaitForSeconds(reflect.activeDuration);
        reflect.shield.Deactivate();
        _reflectCoroutine = null;
    }
}
