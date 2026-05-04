using UnityEngine;

[CreateAssetMenu(fileName = "FlamethrowerEnemyBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Flamethrower", order = 31)]
public class FlamethrowerEnemyBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Flamethrower Weapon")]
    [Tooltip("Balance-only flame weapon numbers. Prefab references, muzzle, damage mask, particles, light, and audio remain on the prefab.")]
    public FlamethrowerWeaponStats flamethrowerWeapon = new FlamethrowerWeaponStats
    {
        damagePerSecond = 24f,
        range = 32f,
        halfAngleDegrees = 24f,
        damageTickInterval = 0.15f,
        burstDuration = 1.5f,
        cooldown = 3f,
        alignDamageToVisualDrift = true,
        driftVelocityScale = 1f,
        visualFlameSpeed = 42f
    };

    [Header("Flamethrower Brain")]
    [Tooltip("Balance-only movement and firing behavior for the short-range flamethrower enemy.")]
    public FlamethrowerBrainStats brain = new FlamethrowerBrainStats
    {
        thinkInterval = 0.05f,
        aimToleranceDegrees = 22f,
        tooCloseRetreatDistance = 14f,
        preferredRangeMin = 20f,
        preferredRangeMax = 30f,
        fullApproachDistance = 55f,
        retreatSpeedScale = 0.75f,
        flameOrbitStrafeSpeed = 10f,
        flameOrbitVerticalBias = 0.12f,
        flameOrbitDirectionChangeInterval = 3f
    };

    public override void ApplyWeaponStats(GameObject prefabRoot)
    {
        ApplyBrainStats<EnemyFlamethrowerWeapon3D>(prefabRoot, weapon => weapon.ApplyProfile(flamethrowerWeapon));
    }

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<FlamethrowerEnemyBrain3D>(prefabRoot, flamethrowerBrain => flamethrowerBrain.ApplyProfile(brain));
    }
}
