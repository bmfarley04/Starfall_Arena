using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "TwinFire", menuName = "Starfall Arena/Augments/TwinFire", order = 2)]
public class TwinFire : Augment
{
    [Header("Gameplay")]
    [Tooltip("Base damage multiplier applied to all primary shots while Twin Fire is active")]
    public float baseDamageMultiplier = 0.7f;

    [Tooltip("Damage multiplier used by the delayed second shot")]
    public float secondShotDamageMultiplier = 0.65f;

    [Tooltip("Delay between the first shot and the mirrored second shot")]
    public float secondShotDelay = 0.08f;

    [Tooltip("If true, the second shot ignores normal primary-fire cooldown checks")]
    public bool ignoreCooldownForSecondShot = true;

    [Header("Presentation")]
    [Tooltip("Optional sound played when the delayed second shot fires")]
    public SoundEffect secondShotSound;

    public override IAugmentRuntime CreateRuntime()
    {
        return new TwinFireRuntime(this);
    }
}
