using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "MindBinding", menuName = "Starfall Arena/Augments/MindBinding", order = 2)]
public class MindBinding : Augment
{
    [Header("Gameplay")]
    [Tooltip("Nearby enemy radius that can trigger mirrored primary fire")]
    public float bindingRadius = 18f;

    [Tooltip("Damage multiplier for mirrored primary shots")]
    public float mirroredShotDamageMultiplier = 1f;

    [Tooltip("Minimum delay between mirrored shots")]
    public float mirroredShotCooldown = 0.12f;

    [Tooltip("If true, mirrored shots ignore normal primary-fire cooldown checks")]
    public bool ignoreCooldownForMirroredShot = true;

    [Header("Presentation")]
    [Tooltip("Optional sound played when MindBinding triggers")]
    public SoundEffect mirroredShotSound;

    [Tooltip("Arc-of-light settings used to render links to nearby bound enemies")]
    public BindingLinkVisualSettings linkVisual = new BindingLinkVisualSettings();

    public override IAugmentRuntime CreateRuntime()
    {
        return new MindBindingRuntime(this);
    }
}
