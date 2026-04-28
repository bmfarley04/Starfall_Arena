using UnityEngine;

[CreateAssetMenu(fileName = "GlassCannonEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Glass Cannon", order = 25)]
public class GlassCannonEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Perch selection, burst, settle, recover, and aim tuning. Used only by prefabs with GlassCannonInterceptorEnemyBrain3D, including gnat-style enemies.")]
    public GlassCannonBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<GlassCannonInterceptorEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
