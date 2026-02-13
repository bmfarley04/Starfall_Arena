using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Rotator", menuName = "Starfall Arena/Augments/Rotator", order = 2)]
public class Rotator : Augment
{
    [Tooltip("Multiplier applied to the player's rotation speed while this augment is active")]
    public float rotationMultiplier = 1.3f;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Rotator";
        description = "Increases rotation speed by 30%.";
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (player == null) return;

        // Only update if state changed
        if (IsAugmentActive() && !player.rotationMultipliers.ContainsKey(augmentID))
        {
            AddMultiplier(rotationMultiplier, player.rotationMultipliers);
        }
        else if (!IsAugmentActive() && player.rotationMultipliers.ContainsKey(augmentID))
        {
            RemoveMultiplier(player.rotationMultipliers);
        }
    }

    public override bool IsAugmentActive()
    {
        // Default behavior: use rounds lifetime from base class
        return base.IsAugmentActive();
    }
}
