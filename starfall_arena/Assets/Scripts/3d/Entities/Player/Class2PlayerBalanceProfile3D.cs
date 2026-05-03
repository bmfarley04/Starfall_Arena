using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Class2PlayerBalanceProfile3D", menuName = "Starfall Arena/3D/Player Profiles/Class 2", order = 41)]
public class Class2PlayerBalanceProfile3D : PlayerBalanceProfile3D
{
    [Serializable]
    public struct Class2Stats
    {
        public EmpoweredShotStats empoweredShot;
        public PhysicalProjectileStats physicalProjectile;
        [Min(0f)] public float shieldCooldown;
        [Min(0f)] public float shieldActiveDuration;
        public TractorBeamStats tractorBeam;
    }

    [Serializable]
    public struct EmpoweredShotStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float damageMultiplier;
        [Min(0f)] public float speedMultiplier;
        [Min(0f)] public float lifetime;
        [Range(0f, 1f)] public float slowMultiplier;
        [Min(0f)] public float slowDuration;
    }

    [Serializable]
    public struct PhysicalProjectileStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float damage;
        [Min(0f)] public float speed;
        [Min(0f)] public float lifetime;
    }

    [Serializable]
    public struct TractorBeamStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float duration;
        [Range(5f, 90f)] public float coneHalfAngle;
        [Min(0f)] public float coneRange;
        [Min(0f)] public float pullSpeed;
        [Min(0f)] public float stopDistance;
    }

    [Header("Class 2")]
    [Tooltip("Class 2-only empowered shot, physical projectile, shield, and tractor tuning. Prefabs, spawn points, target masks, visuals, and audio stay on the prefab.")]
    public Class2Stats classStats;

    public override void ApplyClassStats(GameObject prefabRoot)
    {
        foreach (EmpoweredShot3D empoweredShot in prefabRoot.GetComponentsInChildren<EmpoweredShot3D>(true))
        {
            empoweredShot.ApplyProfile(classStats.empoweredShot);
        }

        foreach (PhysicalProjectileAbility3D physicalProjectile in prefabRoot.GetComponentsInChildren<PhysicalProjectileAbility3D>(true))
        {
            physicalProjectile.ApplyProfile(classStats.physicalProjectile);
        }

        foreach (Class2Shield3D shield in prefabRoot.GetComponentsInChildren<Class2Shield3D>(true))
        {
            shield.ApplyProfile(classStats);
        }

        foreach (TractorBeam3D tractorBeam in prefabRoot.GetComponentsInChildren<TractorBeam3D>(true))
        {
            tractorBeam.ApplyProfile(classStats.tractorBeam);
        }
    }
}
