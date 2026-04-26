using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "SoulBinding", menuName = "Starfall Arena/Augments/SoulBinding", order = 2)]
public class SoulBinding : Augment
{
    [Header("Gameplay")]
    [Tooltip("Nearby enemy radius affected when you lose health")]
    public float bindingRadius = 14f;

    [Tooltip("Health-loss to enemy multiplier at point-blank range")]
    public float pointBlankTransferMultiplier = 0.9f;

    [Tooltip("Health-loss to enemy multiplier at edge of radius")]
    public float edgeTransferMultiplier = 0.25f;

    [Tooltip("Ignore tiny health changes below this value")]
    public float minHealthLossToTrigger = 0.05f;

    [Header("Presentation")]
    [Tooltip("Optional one-shot effect spawned on self when transfer damage occurs")]
    public GameObject triggerEffectPrefab;

    [Tooltip("Arc-of-light settings used to render links to nearby bound enemies")]
    public BindingLinkVisualSettings linkVisual = new BindingLinkVisualSettings();

    public override IAugmentRuntime CreateRuntime()
    {
        return new SoulBindingRuntime(this);
    }
}
