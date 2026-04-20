using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Bubble Shield", menuName = "Starfall Arena/Augments/Bubble Shield", order = 2)]
public class BubbleShield : Augment
{
    [Tooltip("Damage multiplier applied while anchored")]
    public float anchoredDamageMultiplier = 0.6f;

    [Tooltip("Total incoming damage while anchored before the stun triggers")]
    public float damageThresholdBeforeStun = 45f;

    [Tooltip("How long the stun lasts after the threshold is exceeded")]
    public float stunDuration = 1.5f;

    [Tooltip("Movement max-speed multiplier while stunned")]
    public float stunnedSpeedMultiplier = 0.02f;

    [Tooltip("Rotation-speed multiplier while stunned")]
    public float stunnedRotationMultiplier = 0.1f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BubbleShieldRuntime(this);
    }
}