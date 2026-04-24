using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "BodyBinding", menuName = "Starfall Arena/Augments/BodyBinding", order = 2)]
public class BodyBinding : Augment
{
    [Header("Gameplay")]
    [Tooltip("Nearby enemy radius affected by BodyBinding")]
    public float bindingRadius = 14f;

    [Tooltip("Enemy speed multiplier at max owner speed and point-blank range")]
    public float maxSlowMultiplier = 0.6f;

    [Tooltip("Owner speed ratio required before BodyBinding starts applying slow")]
    public float minOwnerSpeedRatioToAffect = 0.1f;

    [Tooltip("Optional exponent to shape distance falloff (1 = linear)")]
    public float distanceFalloffExponent = 1f;

    [Header("Presentation")]
    [Tooltip("Arc-of-light settings used to render links to nearby bound enemies")]
    public BindingLinkVisualSettings linkVisual = new BindingLinkVisualSettings();

    public override IAugmentRuntime CreateRuntime()
    {
        return new BodyBindingRuntime(this);
    }
}
