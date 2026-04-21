using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Bubble Shield", menuName = "Starfall Arena/Augments/Bubble Shield", order = 2)]
public class BubbleShield : Augment
{
    [Header("Presentation")]
    [Tooltip("Shield prefab shown around the player while Bubble Shield protection is active")]
    public GameObject bubbleShieldPrefab;

    [Tooltip("Sound played when Bubble Shield successfully blocks/mitigates incoming damage")]
    public SoundEffect blockSound;

    [Tooltip("Bubble scale multiplier when fully healthy (0 threshold progress)")]
    public float maxVisualScaleMultiplier = 1f;

    [Tooltip("Bubble scale multiplier when close to breaking (near threshold)")]
    public float minVisualScaleMultiplier = 0.35f;

    [Header("Gameplay")]
    [Tooltip("Damage multiplier applied while anchored")]
    public float anchoredDamageMultiplier = 0.6f;

    [Tooltip("Total incoming damage while anchored before the stun triggers")]
    public float damageThresholdBeforeStun = 45f;

    [Tooltip("Seconds after the last anchored hit before Bubble Shield starts recovering")]
    public float damageRegenDelay = 2.5f;

    [Tooltip("How much stored Bubble Shield damage is recovered per second")]
    public float damageRegenPerSecond = 12f;

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