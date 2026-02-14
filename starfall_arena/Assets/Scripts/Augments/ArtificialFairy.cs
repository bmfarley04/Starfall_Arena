using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Artificial Fairy", menuName = "Starfall Arena/Augments/Artificial Fairy", order = 2)]
public class ArtificialFairy : Augment
{
    [Tooltip("Fraction of max health to set when the augment triggers (0-1)")]
    [Range(0f, 1f)]
    public float healFraction = 0.75f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new ArtificialFairyRuntime(this);
    }
}
