using UnityEngine;
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

    protected LayerMask originalLayer;
    protected LayerMask thisPlayerLayer;
    protected float lastUsedAbility = 0;
    protected virtual void Awake()
    {
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

    public virtual bool TryUseAbility(object inputVar)
    {
        if (CanUseAbility())
        {
            lastUsedAbility = Time.time;
            UseAbility(inputVar);
            return true;
        }
        return false;
    }

    public virtual void UseAbility()
    {
        Debug.Log("Ability activated for " + stats.duration + " seconds.");
    }

    public virtual void UseAbility(object inputVar)
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
}
