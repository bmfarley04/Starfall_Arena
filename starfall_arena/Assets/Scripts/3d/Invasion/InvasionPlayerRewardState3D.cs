using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InvasionPlayerRewardState3D
{
    [Serializable]
    public struct GuidedMissileSnapshot3D
    {
        public GuidedMissileWeapon3D.GuidedMissileConfig3D config;
    }

    [Serializable]
    public struct ConvergeBeamSnapshot3D
    {
        public ConvergeBeamWeapon3D.ConvergeBeamConfig3D config;
    }

    [Serializable]
    public struct PlayerBaseRewardSnapshot3D
    {
        public PlayerBalanceProfile3D.CoreStats core;
        public PlayerBalanceProfile3D.FlightStats flight;
        public PlayerBalanceProfile3D.FlightAssistStats flightAssist;
        public PlayerBalanceProfile3D.ProjectileWeaponStats[] projectileWeapons;
        public PlayerBalanceProfile3D.BeamWeaponStats[] beamWeapons;
        public GuidedMissileSnapshot3D[] guidedMissiles;
        public ConvergeBeamSnapshot3D[] convergeBeams;
        public bool hasProjectileWeapons;
        public bool hasBeamWeapons;
    }

    [Serializable]
    public struct RewardModifierTotals3D
    {
        public float allWeaponDamagePercent;
        public float projectileCooldownReductionPercent;
        public float projectileEnergyCostReductionPercent;
        public float projectileSpeedPercent;
        public float projectileLifetimePercent;
        public float projectileHitRadiusBonus;
        public float beamDamagePercent;
        public float beamCapacityPercent;
        public float beamRegenPercent;
        public float abilityCooldownReductionPercent;
        public float maxHealthPercent;
        public float maxShieldPercent;
        public float flatMaxHealth;
        public float flatMaxShield;
        public float shieldRegenDelayReductionPercent;
        public float shieldRegenRatePercent;
        public float incomingDamageReductionPercent;
        public float incomingDamageTakenPercent;
        public float thrustAccelerationPercent;
        public float maxSpeedPercent;
        public float turnResponsePercent;
        public float flightAssistDampingPercent;
        public float flightAssistAlignmentPercent;
        public float extraDodgeInvulnerabilitySeconds;
        public float aimAssistConeAngleBonus;
        public float aimAssistMaxCorrectionBonus;
        public float aimAssistRangeBonus;
        public bool primaryWeaponPierces;
        public int primaryWeaponPierceCount;
        public float primaryWeaponPierceDamageMultiplier;
        public bool restoreFullShieldOnBreakOncePerWave;
        public float shieldOverchargePercent;
        public float noDamageRampDelaySeconds;
        public float noDamageRampPercentPerSecond;
        public float noDamageRampMaxPercent;
        public bool executionLotteryEnabled;
        public float executionLotteryChance;
        public float executionLotteryPerTargetCooldown;
        public float futureStatBoostMultiplierBonus;
    }

    [Serializable]
    public struct RewardHistoryEntry3D
    {
        public string rewardId;
        public InvasionRewardTier3D tier;
    }

    [SerializeField] private PlayerBaseRewardSnapshot3D baseSnapshot;
    [SerializeField] private RewardModifierTotals3D modifiers;
    [SerializeField] private bool baseSnapshotCaptured;
    [SerializeField] private bool hasTakenEmergencyReserve;
    [SerializeField] private bool pendingFieldRepair;
    [SerializeField] private float pendingFieldRepairMissingHullFraction;
    [SerializeField] private bool pendingFieldRepairRefillShieldToFull;
    [SerializeField] private List<RewardHistoryEntry3D> rewardHistory = new List<RewardHistoryEntry3D>();

    public bool HasBaseSnapshot => baseSnapshotCaptured;
    public bool HasProjectileWeapons => baseSnapshotCaptured && baseSnapshot.hasProjectileWeapons;
    public bool HasBeamWeapons => baseSnapshotCaptured && baseSnapshot.hasBeamWeapons;
    public bool HasTakenEmergencyReserve => hasTakenEmergencyReserve;
    public IReadOnlyList<RewardHistoryEntry3D> RewardHistory => rewardHistory;
    public float FutureStatBoostMultiplier => 1f + Mathf.Max(0f, modifiers.futureStatBoostMultiplierBonus);

    public void CaptureBaseSnapshot(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        baseSnapshot.core = player.CaptureCoreStats();
        baseSnapshot.flight = player.Flight != null ? player.Flight.CaptureFlightStats() : default;
        baseSnapshot.flightAssist = player.Flight != null ? player.Flight.CaptureFlightAssistStats() : default;

        ProjectileWeapon3D[] projectileWeapons = player.GetComponentsInChildren<ProjectileWeapon3D>(true);
        baseSnapshot.projectileWeapons = new PlayerBalanceProfile3D.ProjectileWeaponStats[projectileWeapons.Length];
        for (int i = 0; i < projectileWeapons.Length; i++)
        {
            baseSnapshot.projectileWeapons[i] = projectileWeapons[i].CaptureProfileStats();
        }

        BeamWeapon3D[] beamWeapons = player.GetComponentsInChildren<BeamWeapon3D>(true);
        baseSnapshot.beamWeapons = new PlayerBalanceProfile3D.BeamWeaponStats[beamWeapons.Length];
        for (int i = 0; i < beamWeapons.Length; i++)
        {
            baseSnapshot.beamWeapons[i] = beamWeapons[i].CaptureProfileStats();
        }

        GuidedMissileWeapon3D[] guidedMissiles = player.GetComponentsInChildren<GuidedMissileWeapon3D>(true);
        baseSnapshot.guidedMissiles = new GuidedMissileSnapshot3D[guidedMissiles.Length];
        for (int i = 0; i < guidedMissiles.Length; i++)
        {
            baseSnapshot.guidedMissiles[i] = new GuidedMissileSnapshot3D { config = guidedMissiles[i].GuidedMissileConfig };
        }

        ConvergeBeamWeapon3D[] convergeBeams = player.GetComponentsInChildren<ConvergeBeamWeapon3D>(true);
        baseSnapshot.convergeBeams = new ConvergeBeamSnapshot3D[convergeBeams.Length];
        for (int i = 0; i < convergeBeams.Length; i++)
        {
            baseSnapshot.convergeBeams[i] = new ConvergeBeamSnapshot3D { config = convergeBeams[i].ConvergeBeamConfig };
        }

        baseSnapshot.hasProjectileWeapons = projectileWeapons.Length > 0 || guidedMissiles.Length > 0;
        baseSnapshot.hasBeamWeapons = beamWeapons.Length > 0 || convergeBeams.Length > 0;
        baseSnapshotCaptured = true;
    }

    public bool CanOfferReward(InvasionStatRewardDefinition3D reward, InvasionRewardTier3D tier, Player3D livePlayer)
    {
        if (reward == null || !reward.IsEligibleForTier(tier))
        {
            return false;
        }

        if (reward.IsEarlyRewardsOnly && rewardHistory.Count >= 2)
        {
            return false;
        }

        if (reward.RequiresProjectileWeapon && !HasProjectileWeapons)
        {
            return false;
        }

        if (reward.RequiresBeamWeapon && !HasBeamWeapons)
        {
            return false;
        }

        if (reward.GrantsExtraLife(tier) && hasTakenEmergencyReserve)
        {
            return false;
        }

        if (reward.IsOneTimePerRun && HasRewardInHistory(reward.RewardId))
        {
            return false;
        }

        if (reward.HasInstantRepair(tier) && livePlayer != null)
        {
            InvasionStatRewardDefinition3D.InstantRewardPayload3D instant = reward.GetInstantPayload(tier);
            bool hullMissing = livePlayer.CurrentHealth < livePlayer.MaxHealth - 0.01f;
            bool shieldMissing = instant.refillShieldToFull && livePlayer.CurrentShield < livePlayer.MaxShield - 0.01f;
            if (!hullMissing && !shieldMissing)
            {
                return false;
            }
        }

        return true;
    }

    public void ApplyRewardDefinition(InvasionStatRewardDefinition3D reward, InvasionRewardTier3D tier, Player3D livePlayer)
    {
        if (reward == null)
        {
            return;
        }

        InvasionStatRewardDefinition3D.PersistentRewardPayload3D persistent = reward.GetPersistentPayload(tier);
        if (reward.IsStatBoost)
        {
            InvasionStatRewardDefinition3D.ScaleStatPayload(ref persistent, FutureStatBoostMultiplier);
        }

        InvasionStatRewardDefinition3D.InstantRewardPayload3D instant = reward.GetInstantPayload(tier);
        AddPersistentPayload(persistent);

        if (instant.grantExtraLife)
        {
            hasTakenEmergencyReserve = true;
        }

        if (instant.repairMissingHullFraction > 0f || instant.refillShieldToFull)
        {
            pendingFieldRepair = livePlayer == null;
            pendingFieldRepairMissingHullFraction = livePlayer == null ? instant.repairMissingHullFraction : 0f;
            pendingFieldRepairRefillShieldToFull = livePlayer == null && instant.refillShieldToFull;
        }

        rewardHistory.Add(new RewardHistoryEntry3D { rewardId = reward.RewardId, tier = tier });
    }

    public void ApplyToPlayer(Player3D player, InvasionStatRewardDefinition3D immediateReward = null, InvasionRewardTier3D immediateRewardTier = InvasionRewardTier3D.Common)
    {
        if (player == null)
        {
            return;
        }

        if (!baseSnapshotCaptured)
        {
            CaptureBaseSnapshot(player);
        }

        float healthRatio = player.MaxHealth > 0f ? Mathf.Clamp01(player.CurrentHealth / player.MaxHealth) : 1f;
        float shieldRatio = player.MaxShield > 0f ? Mathf.Clamp01(player.CurrentShield / player.MaxShield) : 1f;

        PlayerBalanceProfile3D.CoreStats core = baseSnapshot.core;
        core.maxHealth = Mathf.Max(1f, (baseSnapshot.core.maxHealth * Mathf.Max(0.05f, 1f + modifiers.maxHealthPercent)) + modifiers.flatMaxHealth);
        core.maxShield = Mathf.Max(0f, (baseSnapshot.core.maxShield * Mathf.Max(0.05f, 1f + modifiers.maxShieldPercent)) + modifiers.flatMaxShield);
        core.shieldRegenDelay = Mathf.Max(0f, baseSnapshot.core.shieldRegenDelay * Mathf.Max(0.05f, 1f - modifiers.shieldRegenDelayReductionPercent));
        core.shieldRegenRate = Mathf.Max(0f, baseSnapshot.core.shieldRegenRate * Mathf.Max(0f, 1f + modifiers.shieldRegenRatePercent));
        player.ApplyProfile(core);
        player.SetCurrentDurability(core.maxHealth * healthRatio, core.maxShield * shieldRatio);

        ApplyFlight(player);
        ApplyWeapons(player);

        player.ApplyInvasionRewardRuntimeModifiers(
            modifiers.extraDodgeInvulnerabilitySeconds,
            modifiers.allWeaponDamagePercent,
            modifiers.incomingDamageTakenPercent,
            modifiers.incomingDamageReductionPercent,
            modifiers.abilityCooldownReductionPercent,
            modifiers.shieldOverchargePercent,
            modifiers.noDamageRampDelaySeconds,
            modifiers.noDamageRampPercentPerSecond,
            modifiers.noDamageRampMaxPercent,
            modifiers.projectileHitRadiusBonus,
            modifiers.primaryWeaponPierces,
            modifiers.primaryWeaponPierceCount,
            modifiers.primaryWeaponPierceDamageMultiplier,
            modifiers.aimAssistConeAngleBonus,
            modifiers.aimAssistRangeBonus,
            modifiers.aimAssistMaxCorrectionBonus,
            modifiers.executionLotteryEnabled,
            modifiers.executionLotteryChance,
            modifiers.executionLotteryPerTargetCooldown,
            modifiers.restoreFullShieldOnBreakOncePerWave);

        if (pendingFieldRepair)
        {
            ApplyImmediateRepair(player, pendingFieldRepairMissingHullFraction, pendingFieldRepairRefillShieldToFull);
            pendingFieldRepair = false;
            pendingFieldRepairMissingHullFraction = 0f;
            pendingFieldRepairRefillShieldToFull = false;
        }

        if (immediateReward != null && immediateReward.HasInstantRepair(immediateRewardTier))
        {
            InvasionStatRewardDefinition3D.InstantRewardPayload3D instant = immediateReward.GetInstantPayload(immediateRewardTier);
            ApplyImmediateRepair(player, instant.repairMissingHullFraction, instant.refillShieldToFull);
        }
    }

    private void ApplyFlight(Player3D player)
    {
        ShipFlight3D flight = player.Flight;
        if (flight == null)
        {
            return;
        }

        PlayerBalanceProfile3D.FlightStats flightStats = baseSnapshot.flight;
        flightStats.thrustAcceleration = Mathf.Max(0f, baseSnapshot.flight.thrustAcceleration * Mathf.Max(0.05f, 1f + modifiers.thrustAccelerationPercent));
        flightStats.maxSpeed = Mathf.Max(0.01f, baseSnapshot.flight.maxSpeed * Mathf.Max(0.05f, 1f + modifiers.maxSpeedPercent));
        float turnMultiplier = Mathf.Max(0.05f, 1f + modifiers.turnResponsePercent);
        flightStats.pitchSpeed = Mathf.Max(0.01f, baseSnapshot.flight.pitchSpeed * turnMultiplier);
        flightStats.yawSpeed = Mathf.Max(0.01f, baseSnapshot.flight.yawSpeed * turnMultiplier);
        flightStats.pitchAcceleration = Mathf.Max(0.01f, baseSnapshot.flight.pitchAcceleration * turnMultiplier);
        flightStats.pitchDeceleration = Mathf.Max(0.01f, baseSnapshot.flight.pitchDeceleration * turnMultiplier);
        flightStats.yawAcceleration = Mathf.Max(0.01f, baseSnapshot.flight.yawAcceleration * turnMultiplier);
        flightStats.yawDeceleration = Mathf.Max(0.01f, baseSnapshot.flight.yawDeceleration * turnMultiplier);

        PlayerBalanceProfile3D.FlightAssistStats assistStats = baseSnapshot.flightAssist;
        float assistDampingMultiplier = Mathf.Max(0.05f, 1f + modifiers.flightAssistDampingPercent);
        assistStats.frictionDeceleration = Mathf.Max(0f, baseSnapshot.flightAssist.frictionDeceleration * assistDampingMultiplier);
        assistStats.activeAngularDamping = Mathf.Max(0f, baseSnapshot.flightAssist.activeAngularDamping * assistDampingMultiplier);
        assistStats.lateralDriftDamping = Mathf.Max(0.01f, baseSnapshot.flightAssist.lateralDriftDamping * assistDampingMultiplier);
        assistStats.verticalDriftDamping = Mathf.Max(0.01f, baseSnapshot.flightAssist.verticalDriftDamping * assistDampingMultiplier);
        assistStats.velocityAlignmentStrength = Mathf.Max(0f, baseSnapshot.flightAssist.velocityAlignmentStrength * Mathf.Max(0f, 1f + modifiers.flightAssistAlignmentPercent));
        flight.ApplyProfile(flightStats, assistStats);
    }

    private void ApplyWeapons(Player3D player)
    {
        ProjectileWeapon3D[] projectileWeapons = player.GetComponentsInChildren<ProjectileWeapon3D>(true);
        for (int i = 0; i < projectileWeapons.Length && i < baseSnapshot.projectileWeapons.Length; i++)
        {
            PlayerBalanceProfile3D.ProjectileWeaponStats stats = baseSnapshot.projectileWeapons[i];
            stats.cooldown = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].cooldown * Mathf.Max(0.05f, 1f - modifiers.projectileCooldownReductionPercent));
            stats.energyCost = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].energyCost * Mathf.Max(0f, 1f - modifiers.projectileEnergyCostReductionPercent));
            stats.speed = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].speed * Mathf.Max(0f, 1f + modifiers.projectileSpeedPercent));
            stats.damage = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].damage * Mathf.Max(0f, 1f + modifiers.allWeaponDamagePercent));
            stats.lifetime = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].lifetime * Mathf.Max(0f, 1f + modifiers.projectileLifetimePercent));
            projectileWeapons[i].ApplyProfile(stats);
        }

        BeamWeapon3D[] beamWeapons = player.GetComponentsInChildren<BeamWeapon3D>(true);
        for (int i = 0; i < beamWeapons.Length && i < baseSnapshot.beamWeapons.Length; i++)
        {
            PlayerBalanceProfile3D.BeamWeaponStats stats = baseSnapshot.beamWeapons[i];
            float beamDamageBonus = modifiers.allWeaponDamagePercent + modifiers.beamDamagePercent;
            stats.damagePerSecond = Mathf.Max(0f, baseSnapshot.beamWeapons[i].damagePerSecond * Mathf.Max(0f, 1f + beamDamageBonus));
            stats.capacity = Mathf.Max(0f, baseSnapshot.beamWeapons[i].capacity * Mathf.Max(0f, 1f + modifiers.beamCapacityPercent));
            stats.regenRate = Mathf.Max(0f, baseSnapshot.beamWeapons[i].regenRate * Mathf.Max(0f, 1f + modifiers.beamRegenPercent));
            beamWeapons[i].ApplyProfile(stats);
        }

        GuidedMissileWeapon3D[] guidedMissiles = player.GetComponentsInChildren<GuidedMissileWeapon3D>(true);
        for (int i = 0; i < guidedMissiles.Length && i < baseSnapshot.guidedMissiles.Length; i++)
        {
            GuidedMissileWeapon3D.GuidedMissileConfig3D config = baseSnapshot.guidedMissiles[i].config;
            config.baseProjectile.cooldown = Mathf.Max(0f, config.baseProjectile.cooldown * Mathf.Max(0.05f, 1f - modifiers.projectileCooldownReductionPercent));
            config.baseProjectile.energyCost = Mathf.Max(0f, config.baseProjectile.energyCost * Mathf.Max(0f, 1f - modifiers.projectileEnergyCostReductionPercent));
            config.baseProjectile.speed = Mathf.Max(0f, config.baseProjectile.speed * Mathf.Max(0f, 1f + modifiers.projectileSpeedPercent));
            config.baseProjectile.damage = Mathf.Max(0f, config.baseProjectile.damage * Mathf.Max(0f, 1f + modifiers.allWeaponDamagePercent));
            config.baseProjectile.lifetime = Mathf.Max(0f, config.baseProjectile.lifetime * Mathf.Max(0f, 1f + modifiers.projectileLifetimePercent));
            guidedMissiles[i].SetGuidedMissileConfig(config);
        }

        ConvergeBeamWeapon3D[] convergeBeams = player.GetComponentsInChildren<ConvergeBeamWeapon3D>(true);
        for (int i = 0; i < convergeBeams.Length && i < baseSnapshot.convergeBeams.Length; i++)
        {
            ConvergeBeamWeapon3D.ConvergeBeamConfig3D config = baseSnapshot.convergeBeams[i].config;
            float beamDamageBonus = modifiers.allWeaponDamagePercent + modifiers.beamDamagePercent;
            config.damagePerSecond = Mathf.Max(0f, config.damagePerSecond * Mathf.Max(0f, 1f + beamDamageBonus));
            config.capacity = Mathf.Max(0f, config.capacity * Mathf.Max(0f, 1f + modifiers.beamCapacityPercent));
            config.regenRate = Mathf.Max(0f, config.regenRate * Mathf.Max(0f, 1f + modifiers.beamRegenPercent));
            convergeBeams[i].SetConvergeBeamConfig(config);
        }
    }

    private void AddPersistentPayload(InvasionStatRewardDefinition3D.PersistentRewardPayload3D payload)
    {
        modifiers.allWeaponDamagePercent += payload.allWeaponDamagePercent;
        modifiers.projectileCooldownReductionPercent += payload.projectileCooldownReductionPercent;
        modifiers.projectileEnergyCostReductionPercent += payload.projectileEnergyCostReductionPercent;
        modifiers.projectileSpeedPercent += payload.projectileSpeedPercent;
        modifiers.projectileLifetimePercent += payload.projectileLifetimePercent;
        modifiers.projectileHitRadiusBonus += payload.projectileHitRadiusBonus;
        modifiers.beamDamagePercent += payload.beamDamagePercent;
        modifiers.beamCapacityPercent += payload.beamCapacityPercent;
        modifiers.beamRegenPercent += payload.beamRegenPercent;
        modifiers.abilityCooldownReductionPercent += payload.abilityCooldownReductionPercent;
        modifiers.maxHealthPercent += payload.maxHealthPercent;
        modifiers.maxShieldPercent += payload.maxShieldPercent;
        modifiers.flatMaxHealth += payload.flatMaxHealth;
        modifiers.flatMaxShield += payload.flatMaxShield;
        modifiers.shieldRegenDelayReductionPercent += payload.shieldRegenDelayReductionPercent;
        modifiers.shieldRegenRatePercent += payload.shieldRegenRatePercent;
        modifiers.incomingDamageReductionPercent += payload.incomingDamageReductionPercent;
        modifiers.incomingDamageTakenPercent += payload.incomingDamageTakenPercent;
        modifiers.thrustAccelerationPercent += payload.thrustAccelerationPercent;
        modifiers.maxSpeedPercent += payload.maxSpeedPercent;
        modifiers.turnResponsePercent += payload.turnResponsePercent;
        modifiers.flightAssistDampingPercent += payload.flightAssistDampingPercent;
        modifiers.flightAssistAlignmentPercent += payload.flightAssistAlignmentPercent;
        modifiers.extraDodgeInvulnerabilitySeconds += payload.extraDodgeInvulnerabilitySeconds;
        modifiers.aimAssistConeAngleBonus += payload.aimAssistConeAngleBonus;
        modifiers.aimAssistMaxCorrectionBonus += payload.aimAssistMaxCorrectionBonus;
        modifiers.aimAssistRangeBonus += payload.aimAssistRangeBonus;
        modifiers.primaryWeaponPierces |= payload.primaryWeaponPierces;
        modifiers.primaryWeaponPierceCount = Mathf.Max(modifiers.primaryWeaponPierceCount, payload.primaryWeaponPierceCount);
        modifiers.primaryWeaponPierceDamageMultiplier = payload.primaryWeaponPierceDamageMultiplier > 0f
            ? payload.primaryWeaponPierceDamageMultiplier
            : modifiers.primaryWeaponPierceDamageMultiplier;
        modifiers.restoreFullShieldOnBreakOncePerWave |= payload.restoreFullShieldOnBreakOncePerWave;
        modifiers.shieldOverchargePercent += payload.shieldOverchargePercent;
        modifiers.noDamageRampDelaySeconds = payload.noDamageRampMaxPercent > 0f ? payload.noDamageRampDelaySeconds : modifiers.noDamageRampDelaySeconds;
        modifiers.noDamageRampPercentPerSecond += payload.noDamageRampPercentPerSecond;
        modifiers.noDamageRampMaxPercent += payload.noDamageRampMaxPercent;
        modifiers.executionLotteryEnabled |= payload.executionLotteryEnabled;
        modifiers.executionLotteryChance = Mathf.Max(modifiers.executionLotteryChance, payload.executionLotteryChance);
        modifiers.executionLotteryPerTargetCooldown = payload.executionLotteryPerTargetCooldown > 0f ? payload.executionLotteryPerTargetCooldown : modifiers.executionLotteryPerTargetCooldown;
        modifiers.futureStatBoostMultiplierBonus += payload.futureStatBoostMultiplierBonus;
    }

    private bool HasRewardInHistory(string rewardId)
    {
        for (int i = 0; i < rewardHistory.Count; i++)
        {
            if (rewardHistory[i].rewardId == rewardId)
            {
                return true;
            }
        }

        return false;
    }

    public static void ApplyImmediateRepair(Player3D player, float missingHullFraction, bool refillShieldToFull)
    {
        if (player == null)
        {
            return;
        }

        float currentHealth = player.CurrentHealth;
        float maxHealth = Mathf.Max(1f, player.MaxHealth);
        float repairedHealth = currentHealth + ((maxHealth - currentHealth) * Mathf.Clamp01(missingHullFraction));
        float currentShield = refillShieldToFull ? player.MaxShield : player.CurrentShield;
        player.SetCurrentDurability(repairedHealth, currentShield);
    }
}
