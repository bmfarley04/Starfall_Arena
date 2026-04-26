using StarfallArena.UI;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BlazeOfGlory", menuName = "Starfall Arena/Augments/BlazeOfGlory", order = 2)]
public class BlazeOfGlory : Augment
{
    [Header("Visual Effects")]
    [Tooltip("Prefab activated while Blaze of Glory damage bonus is active")]
    [FormerlySerializedAs("bogEffect")]
    public GameObject damageBoostPrefab;

    [Header("Gameplay")]
    public float damageMultiplier = 1.5f;
    public float healthThreshold = 0.25f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new BlazeOfGloryRuntime(this);
    }
}
