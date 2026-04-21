using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Burner", menuName = "Starfall Arena/Augments/Burner", order = 2)]
public class Burner : Augment
{
    [Header("Presentation")]
    [Tooltip("Prefab spawned on the burning target each burn tick")]
    public GameObject burnTickEffectPrefab;

    [Tooltip("Fallback random radius around the target when no renderer bounds are available")]
    public float burnTickRandomRadius = 0.75f;

    [Header("Gameplay")]
    [Tooltip("Tiny burn damage dealt per second")]
    public float burnDamagePerSecond = 1.2f;

    [Tooltip("How often burn damage is applied in discrete ticks")]
    public float burnTickInterval = 0.25f;

    [Tooltip("How long burn lasts after a primary projectile hit")]
    public float burnDuration = 2.5f;

    [Tooltip("Maximum refresh frequency for reapplying burn to same target")]
    public float reapplyThrottle = 0.08f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BurnerRuntime(this);
    }
}