using UnityEngine;

[CreateAssetMenu(fileName = "BasicShooterEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Basic Shooter", order = 20)]
public class BasicShooterEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Basic shooter chase and firing gates. Used only by prefabs with BasicShooterEnemyBrain3D.")]
    public BasicShooterBrainStats brain;

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<BasicShooterEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
