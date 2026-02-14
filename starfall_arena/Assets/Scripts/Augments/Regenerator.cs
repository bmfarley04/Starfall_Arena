using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Regenerator", menuName = "Starfall Arena/Augments/Regenerator", order = 2)]
public class Regenerator : Augment
{
    [Header("Regeneration")]
    [Tooltip("Amount of health restored per second while anchored")]
    public float healRate = 5f;

    [Tooltip("Seconds the player must remain anchored before healing starts")]
    public float healDelay = 1.0f;

    [Tooltip("Seconds after taking damage before healing can resume")]
    public float damageInterruptCooldown = 1.5f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new RegeneratorRuntime(this);
    }
}
