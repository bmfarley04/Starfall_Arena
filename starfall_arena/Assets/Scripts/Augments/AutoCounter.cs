using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "AutoCounter", menuName = "Starfall Arena/Augments/AutoCounter", order = 2)]
public class AutoCounter : Augment
{
    [Header("AutoCast")]
    [Tooltip("Seconds between automatic shield activations")]
    public float autocastInterval = 7f;

    [Tooltip("Max active duration before shield turns off")]
    public float activeDuration = 2.5f;

    [Tooltip("If the shield reflects a projectile, it stays up for this delay then turns off")]
    public float delayedTurnOffAfterHit = 0.35f;

    [Header("Shield Visual")]
    [Tooltip("Optional visual shield prefab used while active")]
    public ReflectShield reflectShieldPrefab;

    [Tooltip("Reflection color")]
    public Color reflectedProjectileColor = Color.cyan;

    public override IAugmentRuntime CreateRuntime()
    {
        return new AutoCounterRuntime(this);
    }
}