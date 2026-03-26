using UnityEngine;
using UnityEngine.InputSystem;

public class Ability3D : MonoBehaviour
{
    [System.Serializable]
    public struct AbilityStats3D
    {
        [Tooltip("Cooldown time between uses (seconds)")]
        public float cooldown;
        [Tooltip("Duration of ability effect (seconds)")]
        public float duration;
    }

    public AbilityStats3D stats;

    protected Entity3D entity;
    protected float lastUsedAbility = -999f;
    protected bool isDisabledByOtherAbility = false;
    [HideInInspector] public bool isLocked = false;

    protected virtual void Awake()
    {
        entity = GetComponent<Entity3D>();
        lastUsedAbility = -stats.cooldown;
    }

    public virtual bool TryUseAbility(InputValue value)
    {
        if (CanUseAbility())
        {
            lastUsedAbility = Time.time;
            UseAbility(value);
            return true;
        }
        return false;
    }

    public virtual void UseAbility(InputValue value)
    {
    }

    public bool CanUseAbility()
    {
        if (isLocked)
        {
            return false;
        }
        if (Time.time < lastUsedAbility + stats.cooldown)
        {
            return false;
        }
        if (isDisabledByOtherAbility)
        {
            return false;
        }
        return true;
    }

    public virtual bool IsAbilityActive()
    {
        return Time.time < lastUsedAbility + stats.duration;
    }

    protected virtual bool IsAnyOtherAbilityActive()
    {
        Ability3D[] abilities = entity.Abilities;
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null && abilities[i] != this && abilities[i].IsAbilityActive())
            {
                return true;
            }
        }
        return false;
    }

    protected virtual void DisableOtherAbilities(bool shouldDisable)
    {
        Ability3D[] abilities = entity.Abilities;
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null && abilities[i] != this)
            {
                abilities[i].isDisabledByOtherAbility = shouldDisable;
            }
        }
    }

    public virtual float GetRotationMultiplier()
    {
        return 1f;
    }

    public virtual float GetThrustMultiplier()
    {
        return 1f;
    }

    public virtual bool DisablePrimaryFire()
    {
        return false;
    }

    public virtual void Die()
    {
    }

    // ===== HUD STATE =====

    public virtual float GetHUDFillRatio()
    {
        if (stats.cooldown <= 0f) return 0f;
        float elapsed = Time.time - lastUsedAbility;
        if (elapsed >= stats.cooldown) return 0f;
        return 1f - (elapsed / stats.cooldown);
    }

    public virtual bool IsResourceBased()
    {
        return false;
    }

    public virtual bool IsOnCooldown()
    {
        if (stats.cooldown <= 0f) return false;
        return Time.time < lastUsedAbility + stats.cooldown;
    }
}
