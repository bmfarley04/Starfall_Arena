using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "BlazeOfGlory", menuName = "Starfall Arena/Augments/BlazeOfGlory", order = 2)]
public class BlazeOfGlory : Augment
{
    public GameObject bogEffect;
    public float damageMultiplier = 1.5f;
    public float healthThreshold = 0.25f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Awake()
    {
        augmentID = this.name; // Use the script name as the unique ID for simplicity
        augmentName = "Blaze of Glory";
        description = "Deal 50% more damage when below 25% health.";
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();
        if (bogEffect != null)
        {
            bogEffect.SetActive(IsAugmentActive()); // Show visual effect when active
            bogEffect.transform.position = player.transform.position; // Follow the player
        }

        // Only update if state changed (avoids spamming logs)
        if (IsAugmentActive() && !player.damageMultipliers.ContainsKey(augmentID))
        {
            AddMultiplier(damageMultiplier, player.damageMultipliers);
        }
        else if (!IsAugmentActive() && player.damageMultipliers.ContainsKey(augmentID))
        {
            RemoveMultiplier(player.damageMultipliers);
        }
    }

    public override bool IsAugmentActive()
    {
        return player.CurrentHealth / player.maxHealth < healthThreshold;
    }
}
