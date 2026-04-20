using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Burner", menuName = "Starfall Arena/Augments/Burner", order = 2)]
public class Burner : Augment
{
    [Tooltip("Tiny burn damage dealt per second")]
    public float burnDamagePerSecond = 1.2f;

    [Tooltip("How long burn lasts after a primary projectile hit")]
    public float burnDuration = 2.5f;

    [Tooltip("Maximum refresh frequency for reapplying burn to same target")]
    public float reapplyThrottle = 0.08f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BurnerRuntime(this);
    }
}