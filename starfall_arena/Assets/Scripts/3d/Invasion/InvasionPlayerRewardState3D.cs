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
        public float projectileSpeedPercent;
        public float projectileLifetimePercent;
        public float beamDamagePercent;
        public float beamCapacityPercent;
        public float beamRegenPercent;
        public float flatMaxHealth;
        public float flatMaxShield;
        public float shieldRegenDelayReductionPercent;
        public float shieldRegenRatePercent;
        public float thrustAccelerationPercent;
        public float maxSpeedPercent;
        public float turnResponsePercent;
        public float flightAssistDampingPercent;
        public float flightAssistAlignmentPercent;
    }

    [SerializeField] private PlayerBaseRewardSnapshot3D baseSnapshot;
    [SerializeField] private RewardModifierTotals3D modifiers;
    [SerializeField] private bool baseSnapshotCaptured;
    [SerializeField] private bool hasTakenEmergencyReserve;
    [SerializeField] private bool pendingFieldRepair;
    [SerializeField] private List<string> rewardHistory = new List<string>();

    public bool HasBaseSnapshot => baseSnapshotCaptured;
    public bool HasProjectileWeapons => baseSnapshotCaptured && baseSnapshot.hasProjectileWeapons;
    public bool HasBeamWeapons => baseSnapshotCaptured && baseSnapshot.hasBeamWeapons;
    public bool HasTakenEmergencyReserve => hasTakenEmergencyReserve;
    public IReadOnlyList<string> RewardHistory => rewardHistory;

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
            baseSnapshot.guidedMissiles[i] = new GuidedMissileSnapshot3D
            {
                config = guidedMissiles[i].GuidedMissileConfig
            };
        }

        ConvergeBeamWeapon3D[] convergeBeams = player.GetComponentsInChildren<ConvergeBeamWeapon3D>(true);
        baseSnapshot.convergeBeams = new ConvergeBeamSnapshot3D[convergeBeams.Length];
        for (int i = 0; i < convergeBeams.Length; i++)
        {
            baseSnapshot.convergeBeams[i] = new ConvergeBeamSnapshot3D
            {
                config = convergeBeams[i].ConvergeBeamConfig
            };
        }

        baseSnapshot.hasProjectileWeapons = projectileWeapons.Length > 0 || guidedMissiles.Length > 0;
        baseSnapshot.hasBeamWeapons = beamWeapons.Length > 0 || convergeBeams.Length > 0;
        baseSnapshotCaptured = true;
    }

    public bool CanOfferReward(InvasionStatRewardDefinition3D reward, Player3D livePlayer)
    {
        if (reward == null)
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

        if (reward.GrantsExtraLife && hasTakenEmergencyReserve)
        {
            return false;
        }

        if (reward.IsOneTimePerRun && rewardHistory.Contains(reward.RewardId))
        {
            return false;
        }

        if (reward.HasInstantRepair && livePlayer != null)
        {
            bool hullMissing = livePlayer.CurrentHealth < livePlayer.MaxHealth - 0.01f;
            bool shieldMissing = reward.Instant.refillShieldToFull && livePlayer.CurrentShield < livePlayer.MaxShield - 0.01f;
            if (!hullMissing && !shieldMissing)
            {
                return false;
            }
        }

        return true;
    }

    public void ApplyRewardDefinition(InvasionStatRewardDefinition3D reward, Player3D livePlayer)
    {
        if (reward == null)
        {
            return;
        }

        InvasionStatRewardDefinition3D.PersistentRewardPayload3D persistent = reward.Persistent;
        modifiers.allWeaponDamagePercent = Mathf.Min(0.60f, modifiers.allWeaponDamagePercent + Mathf.Max(0f, persistent.allWeaponDamagePercent));
        modifiers.projectileCooldownReductionPercent = Mathf.Min(0.40f, modifiers.projectileCooldownReductionPercent + Mathf.Max(0f, persistent.projectileCooldownReductionPercent));
        modifiers.projectileSpeedPercent += Mathf.Max(0f, persistent.projectileSpeedPercent);
        modifiers.projectileLifetimePercent += Mathf.Max(0f, persistent.projectileLifetimePercent);
        modifiers.beamDamagePercent = Mathf.Min(0.60f, modifiers.beamDamagePercent + Mathf.Max(0f, persistent.beamDamagePercent));
        modifiers.beamCapacityPercent += Mathf.Max(0f, persistent.beamCapacityPercent);
        modifiers.beamRegenPercent += Mathf.Max(0f, persistent.beamRegenPercent);
        modifiers.flatMaxHealth += Mathf.Max(0f, persistent.flatMaxHealth);
        modifiers.flatMaxShield += Mathf.Max(0f, persistent.flatMaxShield);
        modifiers.shieldRegenDelayReductionPercent = Mathf.Min(0.40f, modifiers.shieldRegenDelayReductionPercent + Mathf.Max(0f, persistent.shieldRegenDelayReductionPercent));
        modifiers.shieldRegenRatePercent += Mathf.Max(0f, persistent.shieldRegenRatePercent);
        modifiers.thrustAccelerationPercent = Mathf.Min(0.35f, modifiers.thrustAccelerationPercent + Mathf.Max(0f, persistent.thrustAccelerationPercent));
        modifiers.maxSpeedPercent = Mathf.Min(0.35f, modifiers.maxSpeedPercent + Mathf.Max(0f, persistent.maxSpeedPercent));
        modifiers.turnResponsePercent = Mathf.Min(0.35f, modifiers.turnResponsePercent + Mathf.Max(0f, persistent.turnResponsePercent));
        modifiers.flightAssistDampingPercent = Mathf.Min(0.35f, modifiers.flightAssistDampingPercent + Mathf.Max(0f, persistent.flightAssistDampingPercent));
        modifiers.flightAssistAlignmentPercent = Mathf.Min(0.35f, modifiers.flightAssistAlignmentPercent + Mathf.Max(0f, persistent.flightAssistAlignmentPercent));

        if (reward.GrantsExtraLife)
        {
            hasTakenEmergencyReserve = true;
        }

        if (reward.HasInstantRepair)
        {
            pendingFieldRepair = livePlayer == null;
        }

        rewardHistory.Add(reward.RewardId);
    }

    public void ApplyToPlayer(Player3D player, InvasionStatRewardDefinition3D immediateReward = null)
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
        core.maxHealth = Mathf.Max(1f, baseSnapshot.core.maxHealth + modifiers.flatMaxHealth);
        core.maxShield = Mathf.Max(0f, baseSnapshot.core.maxShield + modifiers.flatMaxShield);
        core.shieldRegenDelay = Mathf.Max(0f, baseSnapshot.core.shieldRegenDelay * (1f - modifiers.shieldRegenDelayReductionPercent));
        core.shieldRegenRate = Mathf.Max(0f, baseSnapshot.core.shieldRegenRate * (1f + modifiers.shieldRegenRatePercent));
        player.ApplyProfile(core);
        player.SetCurrentDurability(core.maxHealth * healthRatio, core.maxShield * shieldRatio);

        ShipFlight3D flight = player.Flight;
        if (flight != null)
        {
            PlayerBalanceProfile3D.FlightStats flightStats = baseSnapshot.flight;
            flightStats.thrustAcceleration = Mathf.Max(0f, baseSnapshot.flight.thrustAcceleration * (1f + modifiers.thrustAccelerationPercent));
            flightStats.maxSpeed = Mathf.Max(0.01f, baseSnapshot.flight.maxSpeed * (1f + modifiers.maxSpeedPercent));
            float turnMultiplier = 1f + modifiers.turnResponsePercent;
            flightStats.pitchSpeed = Mathf.Max(0.01f, baseSnapshot.flight.pitchSpeed * turnMultiplier);
            flightStats.yawSpeed = Mathf.Max(0.01f, baseSnapshot.flight.yawSpeed * turnMultiplier);
            flightStats.pitchAcceleration = Mathf.Max(0.01f, baseSnapshot.flight.pitchAcceleration * turnMultiplier);
            flightStats.pitchDeceleration = Mathf.Max(0.01f, baseSnapshot.flight.pitchDeceleration * turnMultiplier);
            flightStats.yawAcceleration = Mathf.Max(0.01f, baseSnapshot.flight.yawAcceleration * turnMultiplier);
            flightStats.yawDeceleration = Mathf.Max(0.01f, baseSnapshot.flight.yawDeceleration * turnMultiplier);

            PlayerBalanceProfile3D.FlightAssistStats assistStats = baseSnapshot.flightAssist;
            float assistDampingMultiplier = 1f + modifiers.flightAssistDampingPercent;
            assistStats.frictionDeceleration = Mathf.Max(0f, baseSnapshot.flightAssist.frictionDeceleration * assistDampingMultiplier);
            assistStats.activeAngularDamping = Mathf.Max(0f, baseSnapshot.flightAssist.activeAngularDamping * assistDampingMultiplier);
            assistStats.lateralDriftDamping = Mathf.Max(0.01f, baseSnapshot.flightAssist.lateralDriftDamping * assistDampingMultiplier);
            assistStats.verticalDriftDamping = Mathf.Max(0.01f, baseSnapshot.flightAssist.verticalDriftDamping * assistDampingMultiplier);
            assistStats.velocityAlignmentStrength = Mathf.Max(0f, baseSnapshot.flightAssist.velocityAlignmentStrength * (1f + modifiers.flightAssistAlignmentPercent));
            flight.ApplyProfile(flightStats, assistStats);
        }

        ProjectileWeapon3D[] projectileWeapons = player.GetComponentsInChildren<ProjectileWeapon3D>(true);
        for (int i = 0; i < projectileWeapons.Length && i < baseSnapshot.projectileWeapons.Length; i++)
        {
            PlayerBalanceProfile3D.ProjectileWeaponStats stats = baseSnapshot.projectileWeapons[i];
            stats.cooldown = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].cooldown * (1f - modifiers.projectileCooldownReductionPercent));
            stats.speed = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].speed * (1f + modifiers.projectileSpeedPercent));
            stats.damage = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].damage * (1f + modifiers.allWeaponDamagePercent));
            stats.lifetime = Mathf.Max(0f, baseSnapshot.projectileWeapons[i].lifetime * (1f + modifiers.projectileLifetimePercent));
            projectileWeapons[i].ApplyProfile(stats);
        }

        BeamWeapon3D[] beamWeapons = player.GetComponentsInChildren<BeamWeapon3D>(true);
        for (int i = 0; i < beamWeapons.Length && i < baseSnapshot.beamWeapons.Length; i++)
        {
            PlayerBalanceProfile3D.BeamWeaponStats stats = baseSnapshot.beamWeapons[i];
            float beamDamageBonus = modifiers.allWeaponDamagePercent + modifiers.beamDamagePercent;
            stats.damagePerSecond = Mathf.Max(0f, baseSnapshot.beamWeapons[i].damagePerSecond * (1f + beamDamageBonus));
            stats.capacity = Mathf.Max(0f, baseSnapshot.beamWeapons[i].capacity * (1f + modifiers.beamCapacityPercent));
            stats.regenRate = Mathf.Max(0f, baseSnapshot.beamWeapons[i].regenRate * (1f + modifiers.beamRegenPercent));
            beamWeapons[i].ApplyProfile(stats);
        }

        GuidedMissileWeapon3D[] guidedMissiles = player.GetComponentsInChildren<GuidedMissileWeapon3D>(true);
        for (int i = 0; i < guidedMissiles.Length && i < baseSnapshot.guidedMissiles.Length; i++)
        {
            GuidedMissileWeapon3D.GuidedMissileConfig3D config = baseSnapshot.guidedMissiles[i].config;
            config.baseProjectile.cooldown = Mathf.Max(0f, config.baseProjectile.cooldown * (1f - modifiers.projectileCooldownReductionPercent));
            config.baseProjectile.speed = Mathf.Max(0f, config.baseProjectile.speed * (1f + modifiers.projectileSpeedPercent));
            config.baseProjectile.damage = Mathf.Max(0f, config.baseProjectile.damage * (1f + modifiers.allWeaponDamagePercent));
            config.baseProjectile.lifetime = Mathf.Max(0f, config.baseProjectile.lifetime * (1f + modifiers.projectileLifetimePercent));
            guidedMissiles[i].SetGuidedMissileConfig(config);
        }

        ConvergeBeamWeapon3D[] convergeBeams = player.GetComponentsInChildren<ConvergeBeamWeapon3D>(true);
        for (int i = 0; i < convergeBeams.Length && i < baseSnapshot.convergeBeams.Length; i++)
        {
            ConvergeBeamWeapon3D.ConvergeBeamConfig3D config = baseSnapshot.convergeBeams[i].config;
            float beamDamageBonus = modifiers.allWeaponDamagePercent + modifiers.beamDamagePercent;
            config.damagePerSecond = Mathf.Max(0f, config.damagePerSecond * (1f + beamDamageBonus));
            config.capacity = Mathf.Max(0f, config.capacity * (1f + modifiers.beamCapacityPercent));
            config.regenRate = Mathf.Max(0f, config.regenRate * (1f + modifiers.beamRegenPercent));
            convergeBeams[i].SetConvergeBeamConfig(config);
        }

        if (pendingFieldRepair)
        {
            ApplyImmediateRepair(player, 0.25f, true);
            pendingFieldRepair = false;
        }

        if (immediateReward != null && immediateReward.HasInstantRepair)
        {
            ApplyImmediateRepair(player, immediateReward.Instant.repairMissingHullFraction, immediateReward.Instant.refillShieldToFull);
        }
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
