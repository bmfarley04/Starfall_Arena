using UnityEngine;

[CreateAssetMenu(fileName = "SuicideDroneEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Suicide Drone", order = 23)]
public class SuicideDroneEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Detonation damage, radius, proximity, and think cadence. Used only by prefabs with SuicideDroneEnemyBrain3D.")]
    public SuicideDroneBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<SuicideDroneEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
