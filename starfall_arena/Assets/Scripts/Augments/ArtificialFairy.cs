using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Artificial Fairy", menuName = "Starfall Arena/Augments/Artificial Fairy", order = 2)]
public class ArtificialFairy : Augment
{
    [Header("Revive Presentation")]
    [Tooltip("Flash prefab spawned when Artificial Fairy triggers")]
    public GameObject reviveFlashPrefab;

    [Tooltip("How long the player stays intangible before ship parts start regrouping")]
    public float intangibleDuration = 1.5f;

    [Tooltip("How long scattered ship parts take to fly back and reassemble")]
    public float reassemblyDuration = 0.75f;

    [Header("Revive Gameplay")]
    [Tooltip("Fraction of max health to set when the augment triggers (0-1)")]
    [Range(0f, 1f)]
    public float healFraction = 0.75f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new ArtificialFairyRuntime(this);
    }
}
