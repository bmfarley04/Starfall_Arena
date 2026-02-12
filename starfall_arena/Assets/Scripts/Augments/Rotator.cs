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
            AddRotationMultiplier(rotationMultiplier);
        }
        else if (!IsAugmentActive() && player.rotationMultipliers.ContainsKey(augmentID))
        {
            RemoveRotationMultiplier();
        }
    }

    public void AddRotationMultiplier(float mult)
    {
        if (player == null) return;
        if (!player.rotationMultipliers.ContainsKey(augmentID))
        {
            player.rotationMultipliers.Add(augmentID, mult);
            player.SetAugmentVariables(); // Update player's movement values immediately
            Debug.Log($"{augmentName} activated: rotation speed x{mult}");
        }
    }

    public void RemoveRotationMultiplier()
    {
        if (player == null) return;
        if (player.rotationMultipliers.ContainsKey(augmentID))
        {
            player.rotationMultipliers.Remove(augmentID);
            player.SetAugmentVariables();
            Debug.Log($"{augmentName} deactivated: rotation speed returned to normal");
        }
    }

    public override bool IsAugmentActive()
    {
        // Default behavior: use rounds lifetime from base class
        return base.IsAugmentActive();
    }
}
