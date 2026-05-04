using UnityEngine;

[CreateAssetMenu(fileName = "SwarmScoutEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Swarm Scout", order = 29)]
public class SwarmScoutEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Fast swarm orbit, survivor-gated alert, and target broadcast tuning. Used only by prefabs with SwarmScoutEnemyBrain3D.")]
    public SwarmScoutBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<SwarmScoutEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
