using UnityEngine;

[CreateAssetMenu(fileName = "SplitterEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Splitter", order = 26)]
public class SplitterEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Hybrid weapon-choice, split count, child health/shield/speed/scale, and parent range tuning. Used only by prefabs with SplitterEnemyBrain3D.")]
    public SplitterBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<SplitterEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
