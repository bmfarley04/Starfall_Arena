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

    private void Start()
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
        profile.ApplyWeaponStats(gameObject);
        profile.ApplyBrainStats(gameObject);
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

}
