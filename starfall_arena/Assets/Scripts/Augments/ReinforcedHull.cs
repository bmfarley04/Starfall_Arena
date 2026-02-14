using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "ReinforcedHull", menuName = "Starfall Arena/Augments/Reinforced Hull", order = 2)]
public class ReinforcedHull : Augment
{
    [Tooltip("Multiplier applied to max health while augment is active")]
    public float healthMultiplier = 1.5f;

    private void Reset()
    {
        rounds = 3;
    }

    public override IAugmentRuntime CreateRuntime()
    {
        return new ReinforcedHullRuntime(this);
    }
}
