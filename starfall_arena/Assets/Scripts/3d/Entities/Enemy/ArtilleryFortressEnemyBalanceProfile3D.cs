using UnityEngine;

[CreateAssetMenu(fileName = "ArtilleryFortressEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Artillery Fortress", order = 22)]
public class ArtilleryFortressEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Siege cannon, missile, turret, charge, and engagement-range tuning. Used only by prefabs with ArtilleryFortressEnemyBrain3D.")]
    public ArtilleryFortressBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<ArtilleryFortressEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
