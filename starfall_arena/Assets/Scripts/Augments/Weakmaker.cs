using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Weakmaker", menuName = "Starfall Arena/Augments/Weakmaker", order = 2)]
public class Weakmaker : Augment
{
    [Tooltip("Max pointer distance")]
    public float pointerRange = 35f;

    [Tooltip("Extra damage multiplier while the target is being pointed at")]
    public float pointedDamageMultiplier = 1.2f;

    [Tooltip("How long weak exposure persists after pointer contact is refreshed")]
    public float exposureRefreshDuration = 0.2f;

    [Tooltip("Width of the pointer line renderer")]
    public float pointerWidth = 0.06f;

    [Tooltip("Color of the pointer line")]
    public Color pointerColor = new Color(1f, 0.2f, 0.2f, 0.85f);

    public override IAugmentRuntime CreateRuntime()
    {
        return new WeakmakerRuntime(this);
    }
}