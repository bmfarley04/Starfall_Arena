using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Rotator", menuName = "Starfall Arena/Augments/Rotator", order = 2)]
public class Rotator : Augment
{
    [Tooltip("Multiplier applied to the player's rotation speed while this augment is active")]
    public float rotationMultiplier = 1.3f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new RotatorRuntime(this);
    }
}
