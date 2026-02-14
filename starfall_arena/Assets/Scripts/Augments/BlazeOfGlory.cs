using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "BlazeOfGlory", menuName = "Starfall Arena/Augments/BlazeOfGlory", order = 2)]
public class BlazeOfGlory : Augment
{
    public GameObject bogEffect;
    public float damageMultiplier = 1.5f;
    public float healthThreshold = 0.25f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BlazeOfGloryRuntime(this);
    }
}
