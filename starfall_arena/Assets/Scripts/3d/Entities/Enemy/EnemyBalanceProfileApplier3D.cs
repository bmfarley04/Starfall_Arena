using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBalanceProfileApplier3D : MonoBehaviour
{
    [Tooltip("Balance profile that owns designer-tuned enemy numbers for this prefab. It does not replace prefab wiring such as projectiles, muzzles, audio, layers, or network references.")]
    [SerializeField] private EnemyBalanceProfile3D profile;

    public EnemyBalanceProfile3D Profile => profile;

    private void Awake()
    {
        ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (profile == null)
        {
            Debug.LogWarning($"[{nameof(EnemyBalanceProfileApplier3D)}] {name} has no balance profile assigned.", this);
            return;
        }

        ApplySharedCore();
        ApplyWeapons();
        ApplyBrains();
    }

    private void ApplySharedCore()
    {
        Enemy3D[] enemies = GetComponentsInChildren<Enemy3D>(true);
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].ApplyProfile(profile.core);
        }

        EnemyAIFlightController3D[] flightControllers = GetComponentsInChildren<EnemyAIFlightController3D>(true);
        for (int i = 0; i < flightControllers.Length; i++)
        {
            flightControllers[i].ApplyProfile(profile.core);
        }

        EnemyTargetSensor3D[] targetSensors = GetComponentsInChildren<EnemyTargetSensor3D>(true);
        for (int i = 0; i < targetSensors.Length; i++)
        {
            targetSensors[i].ApplyProfile(profile.core);
        }
    }

    private void ApplyWeapons()
    {
        EnemyProjectileWeaponBase3D[] projectileWeapons = GetComponentsInChildren<EnemyProjectileWeaponBase3D>(true);
        int projectileCount = Mathf.Min(projectileWeapons.Length, profile.projectileWeapons != null ? profile.projectileWeapons.Length : 0);
        for (int i = 0; i < projectileCount; i++)
        {
            projectileWeapons[i].ApplyProfile(profile.projectileWeapons[i]);
        }

        BeamWeapon3D[] beamWeapons = GetComponentsInChildren<BeamWeapon3D>(true);
        int beamCount = Mathf.Min(beamWeapons.Length, profile.beamWeapons != null ? profile.beamWeapons.Length : 0);
        for (int i = 0; i < beamCount; i++)
        {
            beamWeapons[i].ApplyProfile(profile.beamWeapons[i]);
        }
    }

    private void ApplyBrains()
    {
        ApplyBrainStats<BasicShooterEnemyBrain3D>(brain => brain.ApplyProfile(profile.basicShooter));
        ApplyBrainStats<ArtilleryBeamEnemyBrain3D>(brain => brain.ApplyProfile(profile.artilleryBeam));
        ApplyBrainStats<ArtilleryFortressEnemyBrain3D>(brain => brain.ApplyProfile(profile.artilleryFortress));
        ApplyBrainStats<SuicideDroneEnemyBrain3D>(brain => brain.ApplyProfile(profile.suicideDrone));
        ApplyBrainStats<TankEnemyBrain3D>(brain => brain.ApplyProfile(profile.tank));
        ApplyBrainStats<GlassCannonInterceptorEnemyBrain3D>(brain => brain.ApplyProfile(profile.glassCannon));
        ApplyBrainStats<SplitterEnemyBrain3D>(brain => brain.ApplyProfile(profile.splitter));
        ApplyBrainStats<DuelistEnemyBrain3D>(brain => brain.ApplyProfile(profile.duelist));
        ApplyBrainStats<TriumvirateEnemyBrain3D>(brain => brain.ApplyProfile(profile.triumvirate));
    }

    private void ApplyBrainStats<TBrain>(System.Action<TBrain> apply)
        where TBrain : MonoBehaviour
    {
        TBrain[] brains = GetComponentsInChildren<TBrain>(true);
        for (int i = 0; i < brains.Length; i++)
        {
            apply(brains[i]);
        }
    }
}
