using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class Ability : MonoBehaviour
{
    [System.Serializable]
    public struct AbilityStats
    {
        [Tooltip("Cooldown time between uses (seconds)")]
        public float cooldown;
        [Tooltip("Duration of ability effect (seconds)")]
        public float duration;
    }
    public AbilityStats stats;

    protected bool isDisabledByOtherAbility = false;
    protected Player player;
    protected LayerMask originalLayer;
    protected LayerMask thisPlayerLayer;
    protected float lastUsedAbility = 0;
    protected virtual void Awake()
    {
        player = GetComponent<Player>();
        originalLayer = gameObject.layer;
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
        Debug.Log("Ability activated for " + stats.duration + " seconds.");
    }

    [HideInInspector] public bool isLocked = false;

    public bool CanUseAbility()
    {
        if (isLocked) return false;
        if (Time.time < lastUsedAbility + stats.cooldown)
        {
            Debug.Log($"Ability on cooldown: {(lastUsedAbility + stats.cooldown - Time.time):F1}s remaining");
            return false;
        } else if (isDisabledByOtherAbility)
        {
            Debug.Log("Cannot use ability: another active ability is disabling this one.");
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
        List<Ability> abilities = new List<Ability> { player.ability1, player.ability2, player.ability3, player.ability4 };
        foreach (var ability in abilities)
        {
            if (ability != null && ability != this && ability.IsAbilityActive())
            {
                return true;
            }
        }
        return false;
    }

    protected virtual void DisableOtherAbilities(bool shouldDisable)
    {
        List<Ability> abilities = new List<Ability> { player.ability1, player.ability2, player.ability3, player.ability4 };
        foreach (var ability in abilities)
        {
            if (ability != null && ability != this && shouldDisable)
            {
                ability.isDisabledByOtherAbility = true;
            } else
            {
                ability.isDisabledByOtherAbility = false;
            }
        }
    }



    public virtual void ApplyRotationMultiplier()
    {
    }

    public virtual void ApplyThrustMultiplier()
    {
    }

    public virtual void ApplyTakeDamageMultiplier(ref float damage)
    {
    }

    public virtual void RestoreThrustMultiplier()
    {
    }

    public virtual bool HasDamageMitigation()
    {
        return false;
    }

    public virtual bool HasThrustMitigation()
    {
        return false;
    }

    public virtual bool HasCollisionModification()
    {
        return false;
    }

    public virtual bool DisablePrimaryFire()
    {
        return false;
    }

    public virtual void ProcessCollisionModification(Collider2D collider)
    {
    }

    public virtual void Magic(object obj)
    {
    }

    public virtual void Die()
    {
    }

    // ===== HUD STATE =====

    /// <summary>
    /// Returns fill ratio for HUD display. 0 = ready/full resource, 1 = fully on cooldown/depleted.
    /// Override in subclasses with custom cooldown tracking.
    /// </summary>
    public virtual float GetHUDFillRatio()
    {
        if (stats.cooldown <= 0f) return 0f;
        float elapsed = Time.time - lastUsedAbility;
        if (elapsed >= stats.cooldown) return 0f;
        return 1f - (elapsed / stats.cooldown);
    }

    /// <summary>
    /// Returns true if ability uses a resource bar instead of cooldown.
    /// Resource abilities only drive fill amount, no material swap.
    /// </summary>
    public virtual bool IsResourceBased()
    {
        return false;
    }

    /// <summary>
    /// Returns true if ability is currently on cooldown (drives material swap).
    /// Resource abilities should return false.
    /// </summary>
    public virtual bool IsOnCooldown()
    {
        if (stats.cooldown <= 0f) return false;
        return Time.time < lastUsedAbility + stats.cooldown;
    }
}
