using UnityEngine;

[CreateAssetMenu(fileName = "ArtilleryBeamEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Artillery Beam", order = 21)]
public class ArtilleryBeamEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Long-range beam standoff, aim, and restart-energy tuning. Used only by prefabs with ArtilleryBeamEnemyBrain3D.")]
    public ArtilleryBeamBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<ArtilleryBeamEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
