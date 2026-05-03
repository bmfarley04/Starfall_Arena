using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Ability3D : MonoBehaviour
{
    protected Entity3D entity;
    protected float lastUsedAbility = -999f;
    protected bool isDisabledByOtherAbility = false;
    [HideInInspector] public bool isLocked = false;

    private float _externalCooldownReductionPercent;
    private float _lastAvailabilityChangedReadyRatio = float.NaN;
    private bool _lastAvailabilityChangedOnCooldown;
    private bool _lastAvailabilityChangedLocked;
    private bool _hasAvailabilitySnapshot;

    public event Action<Ability3D> AvailabilityChanged;

    public float CooldownRemaining => Mathf.Max(0f, GetCooldownReadyTime() - Time.time);
    public float CooldownDuration => GetModifiedCooldownDuration();
    public float CooldownReadyRatio => GetCooldownReadyRatio();
    public bool UsesCooldownAvailability => !IsResourceBased();

    protected virtual void Awake()
    {
        entity = GetComponent<Entity3D>();
        SetInitialCooldownState(GetCooldownDuration());
        CacheAvailabilitySnapshot();
    }

    protected virtual void Update()
    {
        RaiseAvailabilityChangedIfNeeded();
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
        float cooldown = GetModifiedCooldownDuration();
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
        float cooldown = GetModifiedCooldownDuration();
        return cooldown > 0f && Time.time < lastUsedAbility + cooldown;
    }

    protected virtual float GetCooldownDuration()
    {
        return 0f;
    }

    public void SetExternalCooldownReduction(float cooldownReductionPercent)
    {
        _externalCooldownReductionPercent = Mathf.Clamp(cooldownReductionPercent, 0f, 0.85f);
        RaiseAvailabilityChangedIfNeeded(force: true);
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
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected void SetInitialCooldownState(float cooldown)
    {
        lastUsedAbility = cooldown > 0f ? -cooldown : -999f;
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected float GetCooldownReadyTime()
    {
        return lastUsedAbility + GetModifiedCooldownDuration();
    }

    private float GetModifiedCooldownDuration()
    {
        return Mathf.Max(0f, GetCooldownDuration() * (1f - _externalCooldownReductionPercent));
    }

    private float GetCooldownReadyRatio()
    {
        if (isLocked)
        {
            return 0f;
        }

        float cooldown = CooldownDuration;
        if (cooldown <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(1f - (CooldownRemaining / cooldown));
    }

    private void CacheAvailabilitySnapshot()
    {
        _lastAvailabilityChangedReadyRatio = CooldownReadyRatio;
        _lastAvailabilityChangedOnCooldown = IsOnCooldown();
        _lastAvailabilityChangedLocked = isLocked;
        _hasAvailabilitySnapshot = true;
    }

    private void RaiseAvailabilityChangedIfNeeded(bool force = false)
    {
        float currentReadyRatio = CooldownReadyRatio;
        bool isOnCooldown = IsOnCooldown();

        if (!force && _hasAvailabilitySnapshot)
        {
            bool ratioUnchanged = Mathf.Abs(currentReadyRatio - _lastAvailabilityChangedReadyRatio) <= 0.0001f;
            bool cooldownStateUnchanged = isOnCooldown == _lastAvailabilityChangedOnCooldown;
            bool lockedStateUnchanged = isLocked == _lastAvailabilityChangedLocked;
            if (ratioUnchanged && cooldownStateUnchanged && lockedStateUnchanged)
            {
                return;
            }
        }

        _lastAvailabilityChangedReadyRatio = currentReadyRatio;
        _lastAvailabilityChangedOnCooldown = isOnCooldown;
        _lastAvailabilityChangedLocked = isLocked;
        _hasAvailabilitySnapshot = true;
        AvailabilityChanged?.Invoke(this);
    }
}
