using System;
using UnityEngine;

public abstract class EnemyBalanceProfile3D : ScriptableObject
{
    [Serializable]
    public struct CoreStats
    {
        [Min(1f)] public float maxHealth;
        [Min(0f)] public float maxShield;
        [Min(0f)] public float moveSpeed;
        [Min(0f)] public float rotationDegreesPerSecond;
        [Min(0f)] public float detectionRange;
    }

    [Serializable]
    public struct ProjectileWeaponStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float speed;
        [Min(0f)] public float damage;
        [Min(0f)] public float lifetime;
    }

    [Serializable]
    public struct BeamWeaponStats
    {
        [Min(0f)] public float damagePerSecond;
        [Min(0f)] public float maxDistance;
        [Min(0f)] public float capacity;
        [Min(0f)] public float drainRate;
        [Min(0f)] public float regenRate;
        [Min(0f)] public float minimumStartEnergy;
        [Min(0f)] public float rotationMultiplier;
        [Min(0f)] public float postFireRotationPenaltyDuration;
    }

    [Serializable]
    public struct BasicShooterBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Range(0f, 180f)] public float aimToleranceDegrees;
        [Min(0f)] public float stopDistance;
        [Min(0f)] public float fullSpeedDistance;
    }

    [Serializable]
    public struct ArtilleryBeamBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Range(0f, 180f)] public float aimToleranceDegrees;
        [Min(0f)] public float keepAwayDistance;
        [Min(0f)] public float retreatFullSpeedDistance;
        [Min(0f)] public float optimalRange;
        [Min(0f)] public float rangeBuffer;
        [Min(0f)] public float maxEngagementRange;
        [Min(0f)] public float minimumBeamRestartEnergy;
    }

    [Serializable]
    public struct ArtilleryFortressBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Range(0f, 180f)] public float aimToleranceDegrees;
        public bool useLeadAim;
        [Range(1, 3)] public int leadAimRefinementPasses;
        [Min(0f)] public float chargeWindUpDuration;
        [Min(0.01f)] public float maxFiringRange;
        [Min(0f)] public float approachRangeBuffer;
        [Range(0f, 1f)] public float outOfRangeApproachSpeedScale;
        [Min(0f)] public float maxMissileRange;
        [Range(0f, 180f)] public float missileAimToleranceDegrees;
        [Min(0f)] public float missileToCannonStaggerDelay;
        [Min(0f)] public float maxTurretRange;
        [Min(0f)] public float turretToCannonStaggerDelay;
        [Min(0f)] public float loseTargetMaxDistance;
    }

    [Serializable]
    public struct SuicideDroneBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Min(0f)] public float detonationDamage;
        [Min(0f)] public float detonationRadius;
        [Min(0f)] public float contactDetonationDistance;
    }

    [Serializable]
    public struct TankBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Range(0f, 180f)] public float cannonAimToleranceDegrees;
        [Range(0f, 180f)] public float missileAimToleranceDegrees;
        [Min(0f)] public float weaponStaggerDelay;
        [Min(0f)] public float stopDistance;
        [Min(0f)] public float fullSpeedDistance;
    }

    [Serializable]
    public struct GlassCannonBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Min(0f)] public float preferredRangeMin;
        [Min(0f)] public float preferredRangeMax;
        [Min(0.1f)] public float perchArrivalDistance;
        [Min(0.1f)] public float maxRepositionDuration;
        [Min(0f)] public float lateralPerchBias;
        [Min(0f)] public float verticalPerchBias;
        [Min(0f)] public float outwardPerchBias;
        [Min(0f)] public float preBurstSettleDuration;
        [Min(0.01f)] public float maxSettleDuration;
        [Min(1)] public int shotsPerBurst;
        [Min(0.01f)] public float burstShotInterval;
        [Range(0f, 180f)] public float aimToleranceDegrees;
        [Min(0f)] public float postBurstRecoverDuration;
        [Min(0.1f)] public float maxBurstDuration;
    }

    [Serializable]
    public struct SplitterBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Min(0f)] public float holdDistance;
        [Min(0f)] public float fullSpeedDistance;
        [Range(0f, 180f)] public float projectileAimToleranceDegrees;
        [Range(0f, 180f)] public float beamAimToleranceDegrees;
        [Min(0f)] public float projectilePreferredDistance;
        [Min(0f)] public float beamPreferredDistance;
        [Range(0f, 1f)] public float mixedRangeBeamChance;
        [Min(0f)] public float mixedRangeWidth;
        [Min(0f)] public float decisionHoldDuration;
        [Min(0f)] public float minimumBeamRestartEnergy;
        [Min(0f)] public float projectileConvergenceDistance;
        [Min(1)] public int splitCount;
        [Min(0f)] public float splitSpawnRadius;
        [Min(0f)] public float verticalSpawnJitter;
        [Min(0.01f)] public float childScaleMultiplier;
        [Min(0f)] public float childMoveSpeedMultiplier;
        [Min(1f)] public float childMaxHealth;
        [Min(0f)] public float childMaxShield;
    }

    [Serializable]
    public struct WeaponRangeBandStats
    {
        [Min(0f)] public float preferredCenter;
        [Min(0f)] public float halfWidth;
        [Range(0f, 180f)] public float aimToleranceDegrees;
    }

    [Serializable]
    public struct DuelistBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Min(0f)] public float preferredRangeMin;
        [Min(0f)] public float preferredRangeMax;
        [Min(0.1f)] public float perchArrivalDistance;
        [Min(0.1f)] public float maxRepositionDuration;
        [Min(0.1f)] public float perchRefreshInterval;
        [Min(0f)] public float lateralPerchBias;
        [Min(0f)] public float verticalPerchBias;
        [Min(0f)] public float outwardPerchBias;
        [Range(0f, 1f)] public float forwardArcAvoidanceWeight;
        [Range(2, 16)] public int perchCandidateCount;
        [Min(0.1f)] public float strafeRollInterval;
        [Min(0f)] public float orbitStrafeSpeed;
        [Range(0f, 1f)] public float orbitVerticalTiltAmount;
        public WeaponRangeBandStats projectileBand;
        public WeaponRangeBandStats missileBand;
        public WeaponRangeBandStats beamBand;
        [Range(0f, 1f)] public float vibesChance;
        [Min(0f)] public float minimumBeamRestartEnergy;
        [Min(0.02f)] public float threatScanInterval;
        [Min(0.1f)] public float threatScanRadius;
        [Range(0f, 1f)] public float dodgeChancePerThreat;
        [Min(0f)] public float dodgeCooldown;
        [Min(0.05f)] public float dodgeDuration;
        [Min(0f)] public float dodgeSpeed;
        [Range(0f, 1f)] public float threatHeadingDotThreshold;
        [Range(1, 64)] public int threatScanBufferSize;
    }

    [Serializable]
    public struct TriumvirateBrainStats
    {
        [Min(0f)] public float autoLinkRadius;
        [Min(0.02f)] public float autoLinkRetryInterval;
        [Min(0f)] public float formationDistanceFromTarget;
        [Min(0f)] public float triangleRadius;
        public bool anchorFormationNearCurrentSquad;
        [Min(0f)] public float verticalTriangleWidth;
        [Min(0f)] public float verticalTriangleHeight;
        [Min(0.01f)] public float formationTolerance;
        [Range(0f, 1f)] public float formationSpeedScale;
        public bool keepFormationOnWorldYPlane;
        [Min(0f)] public float settleDuration;
        [Min(0f)] public float linkStepDuration;
        [Min(0f)] public float finalChargeDelay;
        [Min(1)] public int linkPointCount;
        [Min(0f)] public float linkAmplitude;
        [Min(0.01f)] public float linkJitterInterval;
        [Min(0f)] public float finalBeamRange;
        [Min(0f)] public float finalBeamHitscanRadius;
        [Min(0f)] public float oneMemberDamagePerSecond;
        [Min(0f)] public float twoMemberDamagePerSecond;
        [Min(0f)] public float threeMemberDamagePerSecond;
        [Min(0f)] public float finalBeamDuration;
        [Min(0f)] public float attackCooldown;
        [Range(0f, 1f)] public float fullTriadSlowMultiplier;
        [Min(0f)] public float fullTriadSlowDuration;
        [Range(0f, 1f)] public float fullTriadSlowEngineEmissionScale;
    }

    [Serializable]
    public struct SwarmScoutBrainStats
    {
        [Tooltip("Seconds between scout steering decisions.")]
        [Min(0.01f)] public float thinkInterval;
        [Tooltip("Maximum distance used when nearby scouts discover each other as one swarm.")]
        [Min(0f)] public float autoLinkRadius;
        [Tooltip("Expected full swarm size used for slot and phase spacing.")]
        [Min(1)] public int intendedSwarmSize;
        [Tooltip("Living linked scouts required before an alert can be broadcast.")]
        [Min(1)] public int requiredSurvivorsForAlert;
        [Tooltip("Preferred orbit radius around the player target.")]
        [Min(0f)] public float orbitRadius;
        [Tooltip("Per-slot radius variation so scouts do not stack perfectly.")]
        [Min(0f)] public float orbitThickness;
        [Tooltip("Strength of inward/outward correction toward the assigned orbit band.")]
        [Min(0f)] public float radialCorrectionWeight;
        [Tooltip("Strength of tangential movement around the player.")]
        [Min(0f)] public float tangentialWeight;
        [Tooltip("Height of the vertical corkscrew motion around the target.")]
        [Min(0f)] public float verticalAmplitude;
        [Tooltip("Speed of the vertical corkscrew motion.")]
        [Min(0f)] public float verticalFrequency;
        [Tooltip("Distance from the player where survivors count toward alert warmup.")]
        [Min(0f)] public float alertProbeRange;
        [Tooltip("Seconds the required survivors must stay near the player before alerting.")]
        [Min(0f)] public float alertWarmupSeconds;
        [Tooltip("Enemy sensors within this radius of the player receive the target alert.")]
        [Min(0f)] public float alertBroadcastRadius;
        [Tooltip("Seconds alerted enemies remember the player if normal detection does not take over.")]
        [Min(0f)] public float alertDuration;
        [Tooltip("Minimum seconds between repeated alert broadcasts from the same scout.")]
        [Min(0f)] public float alertCooldown;
    }

    [Header("Shared Core")]
    [Tooltip("Health, shield, movement, and target-acquisition numbers shared by every Enemy3D component under the profiled prefab.")]
    public CoreStats core = new CoreStats
    {
        maxHealth = 100f,
        maxShield = 50f,
        moveSpeed = 35f,
        rotationDegreesPerSecond = 180f,
        detectionRange = 800f
    };

    [Header("Weapons")]
    [Tooltip("Projectile and missile weapon stats, applied by component order to EnemyProjectileWeaponBase3D components under the prefab. This intentionally excludes prefab, muzzle, recoil, impact force, audio, and pooling setup.")]
    public ProjectileWeaponStats[] projectileWeapons = Array.Empty<ProjectileWeaponStats>();

    [Tooltip("Beam weapon stats, applied by component order to BeamWeapon3D components under the prefab. This intentionally excludes beam prefab, muzzle, target faction, offsets, recoil, impact force, and audio setup.")]
    public BeamWeaponStats[] beamWeapons = Array.Empty<BeamWeaponStats>();

    public abstract void ApplyBrainStats(GameObject prefabRoot);

    protected void ApplyBrainStats<TBrain>(GameObject prefabRoot, Action<TBrain> apply)
        where TBrain : MonoBehaviour
    {
        TBrain[] brains = prefabRoot.GetComponentsInChildren<TBrain>(true);
        for (int i = 0; i < brains.Length; i++)
        {
            apply(brains[i]);
        }
    }
}
