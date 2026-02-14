using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Thorns", menuName = "Starfall Arena/Augments/Thorns", order = 2)]
public class Thorns : Augment
{
    [Tooltip("Damage dealt to other entities on contact")]
    public float contactDamage = 10f;

    [Tooltip("Impact force applied to target on contact")]
    public float contactImpactForce = 0f;

    [Tooltip("Seconds to wait before damaging the same object again")]
    public float hitCooldown = 0.5f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new ThornsRuntime(this);
    }
}
