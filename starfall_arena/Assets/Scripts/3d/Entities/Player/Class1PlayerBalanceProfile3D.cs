using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Class1PlayerBalanceProfile3D", menuName = "Starfall Arena/3D/Player Profiles/Class 1", order = 40)]
public class Class1PlayerBalanceProfile3D : PlayerBalanceProfile3D
{
    [Serializable]
    public struct Class1Stats
    {
        [Min(0f)] public float reflectCooldown;
        [Min(0f)] public float reflectActiveDuration;
        [Range(0f, 5f)] public float reflectedProjectileDamageMultiplier;
        [Min(0f)] public float teleportCooldown;
        [Min(0f)] public float teleportPreDelay;
        [Min(0f)] public float teleportDistance;
        public GigaBlastStats gigaBlast;
    }

    [Serializable]
    public struct GigaBlastStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float minChargeTime;
        [Min(0f)] public float maxChargeTime;
        [Min(0f)] public float projectileLifetime;
        [Min(0f)] public float tier1Time;
        [Min(0f)] public float tier2Time;
        [Min(0f)] public float tier3Time;
        [Min(0f)] public float tier4Time;
        public TierStats tier1;
        public TierStats tier2;
        public TierStats tier3;
        public TierStats tier4;
        [Min(0f)] public float tier3DamageMultiplierPerPierce;
        [Min(0f)] public float tier4DamageMultiplierPerPierce;
    }

    [Serializable]
    public struct TierStats
    {
        [Range(0f, 1f)] public float thrustMultiplier;
        [Range(0f, 1f)] public float rotationMultiplier;
        [Min(0f)] public float speedMultiplier;
        [Min(0f)] public float damageMultiplier;
        [Min(0f)] public float spawnOffset;
    }

    [Header("Class 1")]
    [Tooltip("Class 1-only reflect, teleport, and GigaBlast tuning. Prefabs, particles, audio, and authored shield references stay on the prefab.")]
    public Class1Stats classStats;

    public override void ApplyClassStats(GameObject prefabRoot)
    {
        foreach (Reflector3D reflector in prefabRoot.GetComponentsInChildren<Reflector3D>(true))
        {
            reflector.ApplyProfile(classStats);
        }

        foreach (Teleport3D teleport in prefabRoot.GetComponentsInChildren<Teleport3D>(true))
        {
            teleport.ApplyProfile(classStats);
        }

        foreach (GigaBlastWeapon3D gigaBlast in prefabRoot.GetComponentsInChildren<GigaBlastWeapon3D>(true))
        {
            gigaBlast.ApplyProfile(classStats.gigaBlast);
        }
    }
}
