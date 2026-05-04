using UnityEngine;

[CreateAssetMenu(fileName = "DuelistEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Duelist", order = 27)]
public class DuelistEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Perch, strafe, weapon-band, beam restart, and dodge tuning. Used only by prefabs with DuelistEnemyBrain3D.")]
    public DuelistBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<DuelistEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
