using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Class4PlayerBalanceProfile3D", menuName = "Starfall Arena/3D/Player Profiles/Class 4", order = 42)]
public class Class4PlayerBalanceProfile3D : PlayerBalanceProfile3D
{
    [Serializable]
    public struct Class4Stats
    {
        public BurstStats burst;
        public ConvergeBeamStats convergeBeam;
        public GuidedMissileStats guidedMissile;
        public DodgeStats dodge;
        [Min(0f)] public float empowerCooldown;
        [Min(0f)] public float empowerDuration;
    }

    [Serializable]
    public struct BurstStats
    {
        [Min(0f)] public float burstCooldown;
        [Min(1)] public int burstCount;
        [Min(0f)] public float burstInterval;
    }

    [Serializable]
    public struct ConvergeBeamStats
    {
        [Min(0f)] public float damagePerSecond;
        [Min(0f)] public float maxDistance;
        [Min(0f)] public float rotationMultiplier;
        [Min(1)] public int baseBeamCount;
        [Min(1)] public int empoweredBeamCount;
        [Min(0f)] public float capacity;
        [Min(0f)] public float drainRate;
        [Min(0f)] public float regenRate;
    }

    [Serializable]
    public struct GuidedMissileStats
    {
        public ProjectileWeaponStats baseProjectile;
        public MissileVariantStats regular;
        public MissileVariantStats empowered;
    }

    [Serializable]
    public struct MissileVariantStats
    {
        [Min(0f)] public float damageMultiplier;
        [Min(0f)] public float speedMultiplier;
        [Min(0f)] public float lifetimeOverride;
        [Min(0.01f)] public float sizeMultiplier;
    }

    [Serializable]
    public struct DodgeStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float dodgeDistance;
        [Min(0.01f)] public float slideDuration;
        [Min(0f)] public float primeWindow;
        [Range(0f, 1f)] public float directionInputDeadzone;
        [Min(0f)] public float empoweredCooldown;
    }

    [Header("Class 4")]
    [Tooltip("Class 4-only burst, converge beam, missile, dodge, and empower tuning. Prefabs, hardpoints, visuals, audio, and empower object references stay on the prefab.")]
    public Class4Stats classStats;

    public override void ApplyClassStats(GameObject prefabRoot)
    {
        foreach (Class4BurstWeapon3D burst in prefabRoot.GetComponentsInChildren<Class4BurstWeapon3D>(true))
        {
            burst.ApplyProfile(classStats.burst);
        }

        foreach (ConvergeBeamWeapon3D beam in prefabRoot.GetComponentsInChildren<ConvergeBeamWeapon3D>(true))
        {
            beam.ApplyProfile(classStats.convergeBeam);
        }

        foreach (GuidedMissileWeapon3D missile in prefabRoot.GetComponentsInChildren<GuidedMissileWeapon3D>(true))
        {
            missile.ApplyProfile(classStats.guidedMissile);
        }

        foreach (Dodge3D dodge in prefabRoot.GetComponentsInChildren<Dodge3D>(true))
        {
            dodge.ApplyProfile(classStats.dodge);
        }

        foreach (Empower3D empower in prefabRoot.GetComponentsInChildren<Empower3D>(true))
        {
            empower.ApplyProfile(classStats);
        }
    }
}
