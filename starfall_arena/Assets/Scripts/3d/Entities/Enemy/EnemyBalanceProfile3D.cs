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
    public struct FlamethrowerWeaponStats
    {
        [Min(0f)] public float damagePerSecond;
        [Min(0f)] public float range;
        [Range(0f, 90f)] public float halfAngleDegrees;
        [Min(0.02f)] public float damageTickInterval;
        [Min(0.01f)] public float burstDuration;
        [Min(0f)] public float cooldown;
        public bool alignDamageToVisualDrift;
        [Min(0f)] public float driftVelocityScale;
        [Min(0.01f)] public float visualFlameSpeed;
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
    public struct FlamethrowerBrainStats
    {
        [Min(0.01f)] public float thinkInterval;
        [Range(0f, 180f)] public float aimToleranceDegrees;
        [Min(0f)] public float tooCloseRetreatDistance;
        [Min(0f)] public float preferredRangeMin;
        [Min(0f)] public float preferredRangeMax;
        [Min(0f)] public float fullApproachDistance;
        [Range(0f, 1f)] public float retreatSpeedScale;
        [Min(0f)] public float flameOrbitStrafeSpeed;
        [Range(0f, 1f)] public float flameOrbitVerticalBias;
        [Min(0f)] public float flameOrbitDirectionChangeInterval;
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
        [Min(0.1f)] public float perchCommitDuration;
        [Min(0f)] public float perchStrafeSpeed;
        [Min(0f)] public float lateralPerchBias;
        [Min(0f)] public float verticalPerchBias;
        [Min(0f)] public float outwardPerchBias;
        [Range(0f, 1f)] public float forwardArcAvoidanceWeight;
        [Range(2, 16)] public int perchCandidateCount;
        [Min(0.1f)] public float strafeRollInterval;
        [Min(0f)] public float orbitStrafeSpeed;
        [Range(0f, 1f)] public float orbitVerticalTiltAmount;
        [Min(0f)] public float orbitWeaveSpeed;
        [Min(0.1f)] public float orbitWeaveRerollInterval;
        [Range(0f, 1f)] public float perchMovementWeight;
        [Range(0f, 1f)] public float outOfRangeApproachSpeedScale;
        [Range(0f, 1f)] public float closeRangeRetreatSpeedScale;
        public WeaponRangeBandStats projectileBand;
        public WeaponRangeBandStats missileBand;
        public WeaponRangeBandStats beamBand;
        [Range(0f, 1f)] public float vibesChance;
        [Min(0.1f)] public float weaponCommitDuration;
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
        [Tooltip("Primary movement behavior for the swarm.")]
        public SwarmScoutMovementPattern movementPattern;
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
        [Tooltip("Radius of the polygon formation around its empty center.")]
        [Min(0f)] public float formationRadius;
        [Tooltip("How far past the player the formation center flies before resetting.")]
        [Min(0f)] public float formationOvershootDistance;
        [Tooltip("Strength used to pull each scout toward its polygon slot.")]
        [Min(0f)] public float formationSlotCorrectionWeight;
        [Tooltip("Strength used to drive the formation forward through the player.")]
        [Min(0f)] public float formationForwardWeight;
        [Tooltip("Minimum distance before a fresh formation run can start.")]
        [Min(0f)] public float formationMinRunStartDistance;
        [Tooltip("Maximum seconds before a formation run resets.")]
        [Min(0.1f)] public float formationMaxRunDuration;
        [Tooltip("Degrees per second that the polygon rolls during a flyby.")]
        [Min(0f)] public float formationRollDegreesPerSecond;
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

    [Serializable]
    public struct SiegeCarrierBossBrainStats
    {
        [Tooltip("Seconds between high-level boss target/movement decisions.")]
        [Min(0.01f)] public float thinkInterval;
        [Tooltip("Seconds between stored target-position samples used only when Rake History Blend is above 0.")]
        [Min(0.02f)] public float targetHistorySampleInterval;
        [Tooltip("How many recent target positions the boss stores when history-blended rake aim is enabled.")]
        [Range(2, 32)] public int targetHistorySamples;
        [Tooltip("Inner edge of the carrier's preferred range band. Inside this distance it backs away without trying to face the player.")]
        [Min(0f)] public float preferredRangeMin;
        [Tooltip("Outer edge of the carrier's preferred range band. Beyond this distance it approaches without trying to face the player.")]
        [Min(0f)] public float preferredRangeMax;
        [Tooltip("Maximum distance where the boss can run attack patterns.")]
        [Min(0f)] public float engagementRange;
        [Tooltip("Extra distance beyond Engagement Range where the boss slowly approaches instead of attacking.")]
        [Min(0f)] public float approachRangeBuffer;
        [Tooltip("Movement speed scale while outside Preferred Range Max but inside the approach buffer.")]
        [Range(0f, 1f)] public float approachSpeedScale;
        [Tooltip("Movement speed scale used when the player gets inside Preferred Range Min.")]
        [Range(0f, 1f)] public float retreatSpeedScale;
        [Tooltip("How strongly movement follows target height. 0 preserves the carrier's starting horizontal plane, 1 fully follows target height.")]
        [Range(0f, 1f)] public float targetVerticalFollowWeight;
        [Tooltip("How strongly movement corrects back toward the carrier's starting plane after vertical drift.")]
        [Range(0f, 1f)] public float planeReturnWeight;
        [Tooltip("Minimum seconds between major patterns before health-phase multipliers are applied.")]
        [Min(0f)] public float minimumPatternCooldown;
        [Tooltip("Hard cap on projectile shots per pattern activation.")]
        [Range(1, 128)] public int maxShotsPerPattern;
        [Tooltip("Health percentage where phase two cadence begins.")]
        [Range(0.01f, 1f)] public float phaseTwoHealthPercent;
        [Tooltip("Health percentage where phase three cadence begins.")]
        [Range(0.01f, 1f)] public float phaseThreeHealthPercent;
        [Tooltip("Pattern cooldown multiplier at or below Phase Two Health Percent.")]
        [Range(0.1f, 1f)] public float phaseTwoCooldownMultiplier;
        [Tooltip("Pattern cooldown multiplier at or below Phase Three Health Percent.")]
        [Range(0.1f, 1f)] public float phaseThreeCooldownMultiplier;
        [Tooltip("Maximum lagging-rake shots in one activation, also limited by Max Shots Per Pattern.")]
        [Min(1)] public int rakeShotCount;
        [Tooltip("Seconds between lagging-rake shots.")]
        [Min(0.01f)] public float rakeShotInterval;
        [Tooltip("Seconds behind the target's current position used by optional history-blended rake aim.")]
        [Min(0f)] public float rakeHistorySeconds;
        [Tooltip("Blend from precise current/lead rake aim toward stored historical target positions. 0 is precise follow-fire, 1 is pure lagging trail fire.")]
        [Range(0f, 1f)] public float rakeHistoryBlend;
        [Tooltip("If true, each rake shot predicts the target's current velocity at fire time.")]
        public bool rakeUseLeadAim;
        [Tooltip("Projectile speed used for rake lead calculation. 0 uses the active rake weapon's configured speed.")]
        [Min(0f)] public float rakeLeadProjectileSpeed;
        [Tooltip("Multiplier applied to calculated projectile travel time when leading rake shots.")]
        [Range(0f, 2f)] public float rakeLeadTimeScale;
        [Tooltip("Extra seconds of target-velocity lead added to each rake shot.")]
        [Min(0f)] public float rakeAdditionalLeadSeconds;
        [Tooltip("Maximum total seconds of target-velocity lead allowed for one rake shot.")]
        [Min(0f)] public float rakeMaxLeadSeconds;
        [Tooltip("Number of predictive fan lanes attempted, also limited by configured fan weapons.")]
        [Range(1, 31)] public int fanLaneCount;
        [Tooltip("Total predictive fan spread in degrees.")]
        [Range(0f, 180f)] public float fanTotalSpreadDegrees;
        [Tooltip("Seconds between predictive fan lane shots.")]
        [Min(0.01f)] public float fanLaneInterval;
        [Tooltip("If true, centers the fan on a simple target-velocity lead point.")]
        public bool fanUseLeadAim;
        [Tooltip("Projectile speed used for fan lead calculation. 0 uses the first fan weapon's configured speed.")]
        [Min(0f)] public float fanLeadProjectileSpeed;
        [Tooltip("Number of curtain lanes attempted across the arc, including lanes skipped by the escape door.")]
        [Range(1, 31)] public int curtainLaneCount;
        [Tooltip("Total curtain arc in degrees centered on the target direction.")]
        [Range(0f, 270f)] public float curtainArcDegrees;
        [Tooltip("Width in degrees of the intentionally empty escape sector.")]
        [Range(0f, 180f)] public float curtainEscapeDoorDegrees;
        [Tooltip("Degrees the escape door shifts after each curtain activation.")]
        [Min(0f)] public float curtainDoorDriftDegrees;
        [Tooltip("Seconds between curtain lane shots.")]
        [Min(0.01f)] public float curtainLaneInterval;
        [Tooltip("Projectile-budget cost charged when one formation missile salvo launches. Match this to the salvo missile count.")]
        [Range(1, 32)] public int formationMissileSalvoBudgetCost;
        [Tooltip("Seconds of warning before damaging converging beams activate.")]
        [Min(0f)] public float beamFenceTelegraphDuration;
        [Tooltip("Seconds the damaging converging beams remain active.")]
        [Min(0.01f)] public float beamFenceActiveDuration;
        [Tooltip("Maximum beam hardpoints used in one beam convergence activation.")]
        [Range(1, 16)] public int beamFenceMaxBeams;
        [Tooltip("Seconds between beam convergence aim refreshes while active.")]
        [Min(0.01f)] public float beamFenceAimRefreshInterval;
        [Tooltip("Seconds behind the target used as the shared convergence point for all active beam hardpoints.")]
        [Min(0f)] public float beamConvergenceLagSeconds;
        [Tooltip("Blend from current target position toward the lagged convergence point. 0 tracks current position; 1 uses full positional lag.")]
        [Range(0f, 1f)] public float beamConvergenceLagBlend;
        [Tooltip("Small smoothing time for beam aim directions. This reduces visual jitter from network/refresh cadence without changing damage ownership.")]
        [Min(0f)] public float beamConvergenceAimSmoothTime;
        [Tooltip("Seconds of warning before the two accurate lightning slow beams activate.")]
        [Min(0f)] public float lightningSlowBeamTelegraphDuration;
        [Tooltip("Seconds the two accurate lightning slow beams remain active.")]
        [Min(0.01f)] public float lightningSlowBeamActiveDuration;
        [Tooltip("Seconds between lightning beam aim refreshes while active.")]
        [Min(0.01f)] public float lightningSlowBeamAimRefreshInterval;
        [Tooltip("How many lightning beams are allowed to fire in this pattern. Keep at 2 for the intended boss ability.")]
        [Range(1, 2)] public int lightningSlowBeamCount;
        [Tooltip("Seconds of target-velocity lead added to the lightning beams.")]
        [Min(0f)] public float lightningSlowBeamLeadSeconds;
        [Tooltip("Small smoothing time for lightning aim. Keep low so the beams stay accurate.")]
        [Min(0f)] public float lightningSlowBeamAimSmoothTime;
        [Tooltip("Radius used by the boss brain's slow check along each lightning beam.")]
        [Min(0f)] public float lightningSlowBeamSlowRadius;
        [Tooltip("Movement multiplier applied while the lightning slow beam is hitting the player.")]
        [Range(0f, 1f)] public float lightningSlowBeamSlowMultiplier;
        [Tooltip("Duration of each refreshed slow pulse.")]
        [Min(0f)] public float lightningSlowBeamSlowDuration;
        [Tooltip("Seconds between server-authoritative slow checks while the lightning beams are active.")]
        [Min(0.01f)] public float lightningSlowBeamSlowTickInterval;
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

    public virtual void ApplyWeaponStats(GameObject prefabRoot)
    {
    }

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
