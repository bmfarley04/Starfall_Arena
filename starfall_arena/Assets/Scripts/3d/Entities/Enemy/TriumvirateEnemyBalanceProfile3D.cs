using UnityEngine;

[CreateAssetMenu(fileName = "TriumvirateEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Triumvirate", order = 28)]
public class TriumvirateEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Squad linking, triangle formation, final beam, damage scaling, cooldown, and full-triad slow tuning. Applied to every TriumvirateEnemyBrain3D under the prefab.")]
    public TriumvirateBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<TriumvirateEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
