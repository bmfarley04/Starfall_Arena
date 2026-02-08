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

    protected Player player;
    protected LayerMask originalLayer;
    protected LayerMask thisPlayerLayer;
    protected float lastUsedAbility = 0;
    protected virtual void Awake()
    {
        player = GetComponent<Player>();
        originalLayer = gameObject.layer;
    }

    public virtual bool TryUseAbility()
    {
        if (CanUseAbility())
        {
            lastUsedAbility = Time.time;
            UseAbility();
            return true;
        }
        return false;
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

    public virtual void UseAbility()
    {
        Debug.Log("Ability activated for " + stats.duration + " seconds.");
    }

    public virtual void UseAbility(InputValue value)
    {
        Debug.Log("Ability activated for " + stats.duration + " seconds.");
    }

    public bool CanUseAbility()
    {
        if (Time.time < lastUsedAbility + stats.cooldown)
        {
            Debug.Log($"Ability on cooldown: {(lastUsedAbility + stats.cooldown - Time.time):F1}s remaining");
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
        foreach (var ability in player.abilities)
        {
            if (ability != null && ability.name != this.name && ability.IsAbilityActive())
            {
                return true;
            }
        }
        return false;
    }

    public virtual void ApplyRotationMultiplier()
    {
    }

    public virtual void ApplyThrustMultiplier()
    {
    }

    public virtual void RestoreThrustMultiplier()
    {
    }

    public virtual bool HasDamageMitigation()
    {
        return false;
    }

    public virtual bool HasCollisionModification()
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
}
