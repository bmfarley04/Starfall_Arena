using UnityEngine;

[CreateAssetMenu(fileName = "SiegeCarrierBossBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Siege Carrier Boss", order = 30)]
public class SiegeCarrierBossBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Pattern sequencing, movement bands, projectile budget, and boss phase cadence. Used only by prefabs with SiegeCarrierBossEnemyBrain3D.")]
    public SiegeCarrierBossBrainStats brain = new SiegeCarrierBossBrainStats
    {
        thinkInterval = 0.05f,
        targetHistorySampleInterval = 0.12f,
        targetHistorySamples = 16,
        engagementRange = 260f,
        approachRangeBuffer = 180f,
        approachSpeedScale = 0.25f,
        anchorCreepSpeedScale = 0.05f,
        minimumPatternCooldown = 1.6f,
        maxShotsPerPattern = 32,
        phaseTwoHealthPercent = 0.66f,
        phaseThreeHealthPercent = 0.33f,
        phaseTwoCooldownMultiplier = 0.85f,
        phaseThreeCooldownMultiplier = 0.7f,
        rakeShotCount = 14,
        rakeShotInterval = 0.12f,
        rakeHistorySeconds = 0.6f,
        fanLaneCount = 5,
        fanTotalSpreadDegrees = 34f,
        fanLaneInterval = 0.04f,
        fanUseLeadAim = true,
        fanLeadProjectileSpeed = 140f,
        curtainLaneCount = 13,
        curtainArcDegrees = 140f,
        curtainEscapeDoorDegrees = 26f,
        curtainDoorDriftDegrees = 18f,
        curtainLaneInterval = 0.05f,
        beamFenceTelegraphDuration = 0.75f,
        beamFenceActiveDuration = 1.2f,
        beamFenceMaxBeams = 4,
        beamFenceAimRefreshInterval = 0.05f
    };

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<SiegeCarrierBossEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
