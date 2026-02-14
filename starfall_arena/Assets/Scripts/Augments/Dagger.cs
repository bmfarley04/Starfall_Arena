using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Dagger", menuName = "Starfall Arena/Augments/Dagger", order = 2)]
public class Dagger : Augment
{
    [Tooltip("Multiplier applied to the player's damage output after taking damage")]
    public float damageMultiplier = 1.5f;

    [Tooltip("How long (seconds) the damage boost lasts after taking damage")]
    public float boostDuration = 5f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new DaggerRuntime(this);
    }
}
