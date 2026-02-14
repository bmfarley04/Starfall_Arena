using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Cloak", menuName = "Starfall Arena/Augments/Cloak", order = 2)]
public class Cloak : Augment
{
    [Tooltip("Multiplier applied to the player's movement max speed after taking damage")]
    public float speedMultiplier = 1.5f;

    [Tooltip("How long (seconds) the speed boost lasts after taking damage")]
    public float boostDuration = 5f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new CloakRuntime(this);
    }
}
