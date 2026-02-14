using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Auto-Shields", menuName = "Starfall Arena/Augments/Auto-Shields", order = 2)]
public class AutoShields : Augment
{
    [Tooltip("Once per round/combat, when your shields reach zero, regain all shields instantly.")]
    public bool enabled = true;

    public override IAugmentRuntime CreateRuntime()
    {
        return new AutoShieldsRuntime(this);
    }
}
