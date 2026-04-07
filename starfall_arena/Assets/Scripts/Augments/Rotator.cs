using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Rotator", menuName = "Starfall Arena/Augments/Rotator", order = 2)]
public class Rotator : Augment
{
    [Header("Presentation")]
    [Tooltip("Prefab activated while the ship is turning")]
    public GameObject turningPrefab;

    [Header("Gameplay")]
    [Tooltip("Multiplier applied to the player's rotation speed while this augment is active")]
    public float rotationMultiplier = 1.3f;

    [Tooltip("Minimum turn rate (degrees per second) required to consider the ship as turning")]
    public float turnRateThreshold = 15f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new RotatorRuntime(this);
    }
}
