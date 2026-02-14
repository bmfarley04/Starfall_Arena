using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Augmentor", menuName = "Starfall Arena/Augments/Augmentor", order = 2)]
public class Augmentor : Augment
{
    public override IAugmentRuntime CreateRuntime()
    {
        return new AugmentorRuntime(this);
    }
}
