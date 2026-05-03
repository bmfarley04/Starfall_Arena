using UnityEngine;

[CreateAssetMenu(fileName = "TankEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Tank", order = 24)]
public class TankEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Heavy cannon, missile, stagger, and hold-distance tuning. Used only by prefabs with TankEnemyBrain3D.")]
    public TankBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<TankEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
