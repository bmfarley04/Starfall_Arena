using UnityEngine;

[CreateAssetMenu(fileName = "SiegeCarrierBossBalanceProfile3D", menuName = "Starfall Arena/3D/Enemy Profiles/Siege Carrier Boss", order = 30)]
public class SiegeCarrierBossBalanceProfile3D : EnemyBalanceProfile3D
{
    [Header("Brain")]
    [Tooltip("Pattern sequencing, movement bands, and boss phase settings. Used only by prefabs with SiegeCarrierBossEnemyBrain3D.")]
    public SiegeCarrierBossBrainStats brain = new SiegeCarrierBossBrainStats
    {
        thinkInterval = 0.05f,
        targetHistorySampleInterval = 0.05f,
        targetHistorySamples = 16,
        preferredRangeMin = 180f,
        preferredRangeMax = 260f,
        engagementRange = 260f,
        approachRangeBuffer = 180f,
        approachSpeedScale = 0.25f,
        retreatSpeedScale = 0.18f,
        targetVerticalFollowWeight = 0.1f,
        planeReturnWeight = 0.35f,
        minimumPatternCooldown = 1.6f,
        phaseTwoHealthPercent = 0.66f,
        rakeShotCount = 14,
        rakeShotInterval = 0.12f,
        rakeHistorySeconds = 0f,
        rakeHistoryBlend = 0f,
        rakeUseLeadAim = true,
        rakeLeadProjectileSpeed = 0f,
        rakeLeadTimeScale = 1f,
        rakeAdditionalLeadSeconds = 0.03f,
        rakeMaxLeadSeconds = 1.25f,
        beamFenceActiveDuration = 1.2f,
        beamFenceMaxBeams = 4,
        beamFenceAimRefreshInterval = 0.03f,
        beamConvergenceLagSeconds = 0f,
        beamConvergenceLagBlend = 0f,
        beamConvergenceAimSmoothTime = 0.025f,
        lightningSlowBeamActiveDuration = 1.35f,
        lightningSlowBeamAimRefreshInterval = 0.02f,
        lightningSlowBeamCount = 2,
        lightningSlowBeamLeadSeconds = 0.12f,
        lightningSlowBeamAimSmoothTime = 0.025f,
        lightningSlowBeamSlowRadius = 1.25f,
        lightningSlowBeamSlowMultiplier = 0.45f,
        lightningSlowBeamSlowDuration = 0.18f,
        lightningSlowBeamSlowTickInterval = 0.08f,
        orbitalPillarCount = 6,
        orbitalPillarRingRadius = 115f,
        orbitalPillarGapDegrees = 70f,
        orbitalPillarSphereTravelDuration = 0.85f,
        orbitalPillarExpandDuration = 0.3f,
        orbitalPillarOrbitDegreesPerSecond = 12f,
        orbitalPillarDamageRadius = 16f,
        orbitalPillarDamageHalfHeight = 3000f,
        orbitalPillarDamagePerSecond = 35f,
        orbitalPillarDamageTickInterval = 0.1f
    };

    public override void ApplyBrainStats(GameObject prefabRoot)
    {
        ApplyBrainStats<SiegeCarrierBossEnemyBrain3D>(prefabRoot, target => target.ApplyProfile(brain));
    }
}
