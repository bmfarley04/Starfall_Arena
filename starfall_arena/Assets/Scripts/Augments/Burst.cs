using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Burst", menuName = "Starfall Arena/Augments/Burst", order = 2)]
public class Burst : Augment
{
    [Header("Presentation")]
    [Tooltip("Prefab activated while Burst speed boost is active")]
    public GameObject speedUpEffectPrefab;

    [Header("Gameplay")]
    [Tooltip("Speed multiplier applied after contacting an enemy")]
    public float speedMultiplier = 1.7f;

    [Tooltip("How long the burst speed buff lasts")]
    public float burstDuration = 0.65f;

    [Tooltip("Cooldown between contact-triggered bursts")]
    public float contactCooldown = 0.75f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BurstRuntime(this);
    }
}