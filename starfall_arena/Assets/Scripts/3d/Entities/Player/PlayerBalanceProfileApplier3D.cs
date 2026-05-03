using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerBalanceProfileApplier3D : MonoBehaviour
{
    [Tooltip("Balance profile that owns designer-tuned player ship numbers for this prefab. It does not replace prefab wiring such as models, cameras, projectiles, muzzles, audio, UI, layers, or network references.")]
    [SerializeField] private PlayerBalanceProfile3D profile;

    public PlayerBalanceProfile3D Profile => profile;

    private void Awake()
    {
        ApplyProfile();
    }

    public void ApplyProfile()
    {
        if (profile == null)
        {
            Debug.LogWarning($"[{nameof(PlayerBalanceProfileApplier3D)}] {name} has no balance profile assigned.", this);
            return;
        }

        foreach (Player3D player in GetComponentsInChildren<Player3D>(true))
        {
            player.ApplyProfile(profile.core);
        }

        foreach (ShipFlight3D flight in GetComponentsInChildren<ShipFlight3D>(true))
        {
            flight.ApplyProfile(profile.flight, profile.flightAssist);
        }

        PlayerBalanceProfile3D.ProjectileWeaponStats[] projectileStats = profile.projectileWeapons ?? System.Array.Empty<PlayerBalanceProfile3D.ProjectileWeaponStats>();
        ProjectileWeapon3D[] projectileWeapons = GetComponentsInChildren<ProjectileWeapon3D>(true);
        int projectileCount = Mathf.Min(projectileWeapons.Length, projectileStats.Length);
        for (int i = 0; i < projectileCount; i++)
        {
            projectileWeapons[i].ApplyProfile(projectileStats[i]);
        }

        PlayerBalanceProfile3D.BeamWeaponStats[] beamStats = profile.beamWeapons ?? System.Array.Empty<PlayerBalanceProfile3D.BeamWeaponStats>();
        BeamWeapon3D[] beamWeapons = GetComponentsInChildren<BeamWeapon3D>(true);
        int beamCount = Mathf.Min(beamWeapons.Length, beamStats.Length);
        for (int i = 0; i < beamCount; i++)
        {
            beamWeapons[i].ApplyProfile(beamStats[i]);
        }

        profile.ApplyClassStats(gameObject);
    }
}
