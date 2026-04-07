using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Cloak", menuName = "Starfall Arena/Augments/Cloak", order = 2)]
public class Cloak : Augment
{
    [Header("Presentation")]
    [Tooltip("Sound played when Cloak speed boost activates")]
    public SoundEffect activationSound;

    [Tooltip("Prefab activated while Cloak speed boost is active")]
    public GameObject speedBoostPrefab;

    [Header("Gameplay")]
    [Tooltip("Multiplier applied to the player's movement max speed after taking damage")]
    public float speedMultiplier = 1.5f;

    [Tooltip("How long (seconds) the speed boost lasts after taking damage")]
    public float boostDuration = 5f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new CloakRuntime(this);
    }
}
