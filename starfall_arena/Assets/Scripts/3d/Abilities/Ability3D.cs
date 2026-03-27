using UnityEngine;
using UnityEngine.InputSystem;

public class Ability3D : MonoBehaviour
{
    protected Entity3D entity;
    protected float lastUsedAbility = -999f;
    protected bool isDisabledByOtherAbility = false;
    [HideInInspector] public bool isLocked = false;

    protected virtual void Awake()
    {
        entity = GetComponent<Entity3D>();
        SetInitialCooldownState(GetCooldownDuration());
    }

    public virtual bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return TryHandleRelease(value);
        }

        if (!CanUseAbility())
        {
            return false;
        }

        if (ShouldMarkAbilityUsedOnPress(value))
        {
            MarkAbilityUsed();
        }
        UseAbility(value);
        return true;
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
        if (IsOnCooldown())
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
        float activeDuration = GetActiveDuration();
        return activeDuration > 0f && Time.time < lastUsedAbility + activeDuration;
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
        float cooldown = GetCooldownDuration();
        if (cooldown <= 0f) return 0f;
        float elapsed = Time.time - lastUsedAbility;
        if (elapsed >= cooldown) return 0f;
        return 1f - (elapsed / cooldown);
    }

    public virtual bool IsResourceBased()
    {
        return false;
    }

    public virtual bool IsOnCooldown()
    {
        float cooldown = GetCooldownDuration();
        return cooldown > 0f && Time.time < lastUsedAbility + cooldown;
    }

    protected virtual float GetCooldownDuration()
    {
        return 0f;
    }

    protected virtual float GetActiveDuration()
    {
        return 0f;
    }

    protected virtual bool HandlesReleaseInput()
    {
        return false;
    }

    protected virtual bool TryHandleRelease(InputValue value)
    {
        if (!HandlesReleaseInput())
        {
            return false;
        }

        UseAbility(value);
        return true;
    }

    protected virtual bool ShouldMarkAbilityUsedOnPress(InputValue value)
    {
        return true;
    }

    protected void MarkAbilityUsed()
    {
        lastUsedAbility = Time.time;
    }

    protected void SetInitialCooldownState(float cooldown)
    {
        lastUsedAbility = cooldown > 0f ? -cooldown : -999f;
    }
}
