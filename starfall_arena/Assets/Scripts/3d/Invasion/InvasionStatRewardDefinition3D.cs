using System.Text;
using UnityEngine;

public enum InvasionRewardTier3D
{
    Common = 1,
    Epic = 2,
    High = 3,
    Tier4 = 4
}

public enum InvasionRewardCategory3D
{
    StatBoost,
    PermanentUpgrade,
    OneTimeBonus,
    CraizanContract
}

[System.Flags]
public enum InvasionRewardTierMask3D
{
    None = 0,
    Common = 1 << 0,
    Epic = 1 << 1,
    High = 1 << 2,
    All = Common | Epic | High
}

[CreateAssetMenu(fileName = "InvasionStatRewardDefinition3D", menuName = "Starfall Arena/3D/Invasion/Stat Reward", order = 60)]
public class InvasionStatRewardDefinition3D : ScriptableObject
{
    [System.Flags]
    public enum RewardEligibility3D
    {
        None = 0,
        RequiresProjectileWeapon = 1 << 0,
        RequiresBeamWeapon = 1 << 1,
        OneTimePerRun = 1 << 2,
        EarlyRewardsOnly = 1 << 3
    }

    [System.Serializable]
    public struct PersistentRewardPayload3D
    {
        [Header("Weapon Damage")]
        [Tooltip("Adds this much percentage damage to projectile, missile, and beam primary weapons. 0.1 = +10% damage. CRAIZAN CONTRACTS may use negative values.")]
        public float allWeaponDamagePercent;
        [Tooltip("Adds this much percentage damage to beam-family weapons only. 0.1 = +10% damage.")]
        public float beamDamagePercent;

        [Header("Projectile Weapons")]
        [Tooltip("Reduces projectile-family cooldown by this percentage. 0.08 = 8% faster fire rate.")]
        public float projectileCooldownReductionPercent;
        [Tooltip("Reduces projectile-family energy/overheat cost by this percentage. 0.2 = 20% lower cost.")]
        public float projectileEnergyCostReductionPercent;
        [Tooltip("Adds this much percentage speed to projectile-family weapons.")]
        public float projectileSpeedPercent;
        [Tooltip("Adds this much percentage lifetime to projectile-family weapons.")]
        public float projectileLifetimePercent;
        [Tooltip("Adds this much world-space radius to player projectile hit checks.")]
        public float projectileHitRadiusBonus;

        [Header("Beam Weapons")]
        [Tooltip("Adds this much percentage max energy capacity to beam-family weapons.")]
        public float beamCapacityPercent;
        [Tooltip("Adds this much percentage passive regeneration to beam-family weapons.")]
        public float beamRegenPercent;

        [Header("Abilities")]
        [Tooltip("Reduces player ability cooldowns by this percentage. 0.1 = 10% shorter cooldown.")]
        public float abilityCooldownReductionPercent;

        [Header("Durability")]
        [Tooltip("Adds this much percentage max hull. CRAIZAN CONTRACTS may use negative values.")]
        public float maxHealthPercent;
        [Tooltip("Adds this much percentage max shield. CRAIZAN CONTRACTS may use negative values.")]
        public float maxShieldPercent;
        [Tooltip("Legacy/additive flat max hull. Prefer Max Health Percent for new rewards.")]
        public float flatMaxHealth;
        [Tooltip("Legacy/additive flat max shield. Prefer Max Shield Percent for new rewards.")]
        public float flatMaxShield;
        [Tooltip("Reduces shield regeneration delay by this percentage. 0.25 = 25% sooner. Negative values increase the delay.")]
        public float shieldRegenDelayReductionPercent;
        [Tooltip("Adds this much percentage shield regeneration rate.")]
        public float shieldRegenRatePercent;
        [Tooltip("Reduces incoming damage by this percentage. 0.1 = 10% less damage.")]
        public float incomingDamageReductionPercent;
        [Tooltip("Increases incoming damage taken by this percentage. 0.25 = 25% more incoming damage.")]
        public float incomingDamageTakenPercent;

        [Header("Flight")]
        [Tooltip("Adds this much percentage thrust acceleration. CRAIZAN CONTRACTS may use negative values.")]
        public float thrustAccelerationPercent;
        [Tooltip("Adds this much percentage top speed. CRAIZAN CONTRACTS may use negative values.")]
        public float maxSpeedPercent;
        [Tooltip("Adds this much percentage turn speed and turn accel/decel. CRAIZAN CONTRACTS may use negative values.")]
        public float turnResponsePercent;
        [Tooltip("Adds this much percentage drift damping to the assist model for tighter control.")]
        public float flightAssistDampingPercent;
        [Tooltip("Adds this much percentage velocity-alignment strength to the assist model.")]
        public float flightAssistAlignmentPercent;

        [Header("Permanent Upgrade Hooks")]
        [Tooltip("Additional dodge invulnerability seconds added to every generic dodge.")]
        public float extraDodgeInvulnerabilitySeconds;
        [Tooltip("Additional aim assist cone angle in degrees.")]
        public float aimAssistConeAngleBonus;
        [Tooltip("Additional aim assist maximum angular correction in degrees.")]
        public float aimAssistMaxCorrectionBonus;
        [Tooltip("Additional aim assist range in world units.")]
        public float aimAssistRangeBonus;
        [Tooltip("If enabled, weapon slot 0 projectiles pierce additional enemies.")]
        public bool primaryWeaponPierces;
        [Tooltip("How many extra enemies weapon slot 0 projectiles can pierce.")]
        public int primaryWeaponPierceCount;
        [Tooltip("Damage multiplier applied after each pierce. 0.5 = 50% damage after first hit.")]
        public float primaryWeaponPierceDamageMultiplier;
        [Tooltip("If enabled, the player restores full shields once per wave when shields break.")]
        public bool restoreFullShieldOnBreakOncePerWave;
        [Tooltip("Additional shield capacity above max that regeneration can fill. 0.5 = regenerate up to 150% shield.")]
        public float shieldOverchargePercent;
        [Tooltip("Seconds without damage before the no-hit damage ramp starts.")]
        public float noDamageRampDelaySeconds;
        [Tooltip("Damage percent gained per second after the no-hit ramp delay.")]
        public float noDamageRampPercentPerSecond;
        [Tooltip("Maximum damage percent from the no-hit ramp.")]
        public float noDamageRampMaxPercent;
        [Tooltip("If enabled, all player damage can instantly defeat non-boss enemies.")]
        public bool executionLotteryEnabled;
        [Tooltip("Chance per allowed damage roll to instantly defeat a non-boss enemy.")]
        public float executionLotteryChance;
        [Tooltip("Minimum seconds between execution-lottery rolls against the same target.")]
        public float executionLotteryPerTargetCooldown;
        [Tooltip("Multiplies future normal stat boost payloads only. 0.25 = future stat boosts are 25% stronger.")]
        public float futureStatBoostMultiplierBonus;
    }

    [System.Serializable]
    public struct InstantRewardPayload3D
    {
        [Tooltip("If greater than 0, restore this fraction of the player's missing hull when chosen. 1 = full missing hull restore.")]
        [Range(0f, 1f)]
        public float repairMissingHullFraction;
        [Tooltip("If enabled, refill the player's shield to max when chosen.")]
        public bool refillShieldToFull;
        [Tooltip("If enabled, grant one additional Invasion life immediately when chosen.")]
        public bool grantExtraLife;
    }

    [Header("Identity")]
    [Tooltip("Stable reward ID used for history/debugging. Keep this unique across the reward pool.")]
    [SerializeField] private string rewardId = "reward_id";
    [Tooltip("High-level reward category. CRAIZAN CONTRACTS use special offer rules and tier 4 card styling.")]
    [SerializeField] private InvasionRewardCategory3D category = InvasionRewardCategory3D.StatBoost;

    [Header("Display")]
    [Tooltip("Title shown on the reused augment-card reward UI.")]
    [SerializeField] private string displayName = "Reward";
    [Tooltip("Fallback description. When Generate Description From Payload is enabled, the runtime card uses generated numeric text instead.")]
    [TextArea(3, 5)]
    [SerializeField] private string description = "";
    [Tooltip("If enabled, the reward card description is generated from the resolved tier payload and current run modifiers.")]
    [SerializeField] private bool generateDescriptionFromPayload = true;
    [Tooltip("Icon shown on the reused augment-card reward UI.")]
    [SerializeField] private Sprite icon;

    [Header("Offer Rules")]
    [Tooltip("Reward tiers this asset can appear in. CRAIZAN CONTRACTS can usually leave this as All.")]
    [SerializeField] private InvasionRewardTierMask3D eligibleTiers = InvasionRewardTierMask3D.All;
    [Tooltip("Visual card style override. Set CRAIZAN CONTRACTS to Tier4 so they use the tier 4 contract visuals inside any offer.")]
    [SerializeField] private InvasionRewardTier3D visualStyleOverride = InvasionRewardTier3D.Common;
    [Tooltip("If enabled, Visual Style Override is used instead of the current reward tier for card presentation.")]
    [SerializeField] private bool useVisualStyleOverride;
    [Tooltip("Relative weight used when this reward is eligible for a wave offer roll.")]
    [Min(0f)]
    [SerializeField] private float offerWeight = 1f;
    [Tooltip("If disabled, the reward can only appear once per run even if it does not use an explicit one-time flag.")]
    [SerializeField] private bool repeatable = true;
    [Tooltip("Eligibility requirements used to filter which ships and run states may see this reward.")]
    [SerializeField] private RewardEligibility3D eligibility = RewardEligibility3D.None;

    [Header("Common Tier")]
    [SerializeField] private PersistentRewardPayload3D commonPersistent;
    [SerializeField] private InstantRewardPayload3D commonInstant;

    [Header("Epic Tier")]
    [SerializeField] private PersistentRewardPayload3D epicPersistent;
    [SerializeField] private InstantRewardPayload3D epicInstant;

    [Header("High Tier")]
    [SerializeField] private PersistentRewardPayload3D highPersistent;
    [SerializeField] private InstantRewardPayload3D highInstant;

    [Header("CRAIZAN CONTRACT / Tier 4 Payload")]
    [Tooltip("Fixed payload used by CRAIZAN CONTRACTS regardless of the current Common/Epic/High reward grouping.")]
    [SerializeField] private PersistentRewardPayload3D tier4Persistent;
    [SerializeField] private InstantRewardPayload3D tier4Instant;

    public string RewardId => string.IsNullOrWhiteSpace(rewardId) ? name : rewardId;
    public InvasionRewardCategory3D Category => category;
    public bool IsStatBoost => category == InvasionRewardCategory3D.StatBoost;
    public bool IsCraizanContract => category == InvasionRewardCategory3D.CraizanContract;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public bool GenerateDescriptionFromPayload => generateDescriptionFromPayload;
    public Sprite Icon => icon;
    public float OfferWeight => Mathf.Max(0f, offerWeight);
    public bool Repeatable => repeatable;
    public RewardEligibility3D Eligibility => eligibility;

    public bool RequiresProjectileWeapon => (eligibility & RewardEligibility3D.RequiresProjectileWeapon) != 0;
    public bool RequiresBeamWeapon => (eligibility & RewardEligibility3D.RequiresBeamWeapon) != 0;
    public bool IsOneTimePerRun => !repeatable || (eligibility & RewardEligibility3D.OneTimePerRun) != 0;
    public bool IsEarlyRewardsOnly => (eligibility & RewardEligibility3D.EarlyRewardsOnly) != 0;

    public bool IsEligibleForTier(InvasionRewardTier3D tier)
    {
        InvasionRewardTierMask3D mask = tier switch
        {
            InvasionRewardTier3D.Epic => InvasionRewardTierMask3D.Epic,
            InvasionRewardTier3D.High => InvasionRewardTierMask3D.High,
            _ => InvasionRewardTierMask3D.Common
        };
        return (eligibleTiers & mask) != 0;
    }

    public InvasionRewardTier3D ResolveVisualTier(InvasionRewardTier3D offerTier)
    {
        if (IsCraizanContract)
        {
            return InvasionRewardTier3D.Tier4;
        }

        return useVisualStyleOverride ? visualStyleOverride : offerTier;
    }

    public PersistentRewardPayload3D GetPersistentPayload(InvasionRewardTier3D tier)
    {
        if (IsCraizanContract)
        {
            return tier4Persistent;
        }

        return tier switch
        {
            InvasionRewardTier3D.Epic => epicPersistent,
            InvasionRewardTier3D.High => highPersistent,
            InvasionRewardTier3D.Tier4 => tier4Persistent,
            _ => commonPersistent
        };
    }

    public InstantRewardPayload3D GetInstantPayload(InvasionRewardTier3D tier)
    {
        if (IsCraizanContract)
        {
            return tier4Instant;
        }

        return tier switch
        {
            InvasionRewardTier3D.Epic => epicInstant,
            InvasionRewardTier3D.High => highInstant,
            InvasionRewardTier3D.Tier4 => tier4Instant,
            _ => commonInstant
        };
    }

    public string BuildDescription(InvasionRewardTier3D tier, float statBoostMultiplier)
    {
        if (!generateDescriptionFromPayload)
        {
            return description;
        }

        PersistentRewardPayload3D payload = GetPersistentPayload(tier);
        if (IsStatBoost && statBoostMultiplier > 0f)
        {
            ScaleStatPayload(ref payload, statBoostMultiplier);
        }

        InstantRewardPayload3D instant = GetInstantPayload(tier);
        StringBuilder builder = new StringBuilder(160);
        AppendPayloadDescription(builder, payload, instant);
        return builder.Length > 0 ? builder.ToString() : description;
    }

    public bool HasInstantRepair(InvasionRewardTier3D tier)
    {
        InstantRewardPayload3D instant = GetInstantPayload(tier);
        return instant.repairMissingHullFraction > 0f || instant.refillShieldToFull;
    }

    public bool GrantsExtraLife(InvasionRewardTier3D tier)
    {
        return GetInstantPayload(tier).grantExtraLife;
    }

    private void OnValidate()
    {
        offerWeight = Mathf.Max(0f, offerWeight);
        ClampPersistentPayload(ref commonPersistent);
        ClampPersistentPayload(ref epicPersistent);
        ClampPersistentPayload(ref highPersistent);
        ClampPersistentPayload(ref tier4Persistent);
        ClampInstantPayload(ref commonInstant);
        ClampInstantPayload(ref epicInstant);
        ClampInstantPayload(ref highInstant);
        ClampInstantPayload(ref tier4Instant);

        if (category == InvasionRewardCategory3D.CraizanContract)
        {
            useVisualStyleOverride = true;
            visualStyleOverride = InvasionRewardTier3D.Tier4;
            repeatable = true;
        }
    }

    public static void ScaleStatPayload(ref PersistentRewardPayload3D payload, float multiplier)
    {
        multiplier = Mathf.Max(0f, multiplier);
        payload.allWeaponDamagePercent *= multiplier;
        payload.projectileCooldownReductionPercent *= multiplier;
        payload.projectileEnergyCostReductionPercent *= multiplier;
        payload.projectileSpeedPercent *= multiplier;
        payload.projectileLifetimePercent *= multiplier;
        payload.beamDamagePercent *= multiplier;
        payload.beamCapacityPercent *= multiplier;
        payload.beamRegenPercent *= multiplier;
        payload.abilityCooldownReductionPercent *= multiplier;
        payload.maxHealthPercent *= multiplier;
        payload.maxShieldPercent *= multiplier;
        payload.flatMaxHealth *= multiplier;
        payload.flatMaxShield *= multiplier;
        payload.shieldRegenDelayReductionPercent *= multiplier;
        payload.shieldRegenRatePercent *= multiplier;
        payload.thrustAccelerationPercent *= multiplier;
        payload.maxSpeedPercent *= multiplier;
        payload.turnResponsePercent *= multiplier;
        payload.flightAssistDampingPercent *= multiplier;
        payload.flightAssistAlignmentPercent *= multiplier;
    }

    private static void ClampPersistentPayload(ref PersistentRewardPayload3D payload)
    {
        payload.primaryWeaponPierceCount = Mathf.Max(0, payload.primaryWeaponPierceCount);
        payload.primaryWeaponPierceDamageMultiplier = Mathf.Max(0f, payload.primaryWeaponPierceDamageMultiplier);
        payload.extraDodgeInvulnerabilitySeconds = Mathf.Max(0f, payload.extraDodgeInvulnerabilitySeconds);
        payload.aimAssistConeAngleBonus = Mathf.Max(0f, payload.aimAssistConeAngleBonus);
        payload.aimAssistMaxCorrectionBonus = Mathf.Max(0f, payload.aimAssistMaxCorrectionBonus);
        payload.aimAssistRangeBonus = Mathf.Max(0f, payload.aimAssistRangeBonus);
        payload.projectileHitRadiusBonus = Mathf.Max(0f, payload.projectileHitRadiusBonus);
        payload.shieldOverchargePercent = Mathf.Max(0f, payload.shieldOverchargePercent);
        payload.noDamageRampDelaySeconds = Mathf.Max(0f, payload.noDamageRampDelaySeconds);
        payload.noDamageRampPercentPerSecond = Mathf.Max(0f, payload.noDamageRampPercentPerSecond);
        payload.noDamageRampMaxPercent = Mathf.Max(0f, payload.noDamageRampMaxPercent);
        payload.executionLotteryChance = Mathf.Clamp01(payload.executionLotteryChance);
        payload.executionLotteryPerTargetCooldown = Mathf.Max(0f, payload.executionLotteryPerTargetCooldown);
        payload.futureStatBoostMultiplierBonus = Mathf.Max(0f, payload.futureStatBoostMultiplierBonus);
    }

    private static void ClampInstantPayload(ref InstantRewardPayload3D payload)
    {
        payload.repairMissingHullFraction = Mathf.Clamp01(payload.repairMissingHullFraction);
    }

    private static void AppendPayloadDescription(StringBuilder builder, PersistentRewardPayload3D payload, InstantRewardPayload3D instant)
    {
        AppendPercent(builder, "Damage", payload.allWeaponDamagePercent);
        AppendPercent(builder, "Beam damage", payload.beamDamagePercent);
        AppendReduction(builder, "Projectile cooldown", payload.projectileCooldownReductionPercent);
        AppendReduction(builder, "Ability cooldown", payload.abilityCooldownReductionPercent);
        AppendReduction(builder, "Projectile energy cost", payload.projectileEnergyCostReductionPercent);
        AppendPercent(builder, "Projectile speed", payload.projectileSpeedPercent);
        AppendPercent(builder, "Projectile lifetime", payload.projectileLifetimePercent);
        AppendPercent(builder, "Beam capacity", payload.beamCapacityPercent);
        AppendPercent(builder, "Beam regen", payload.beamRegenPercent);
        AppendPercent(builder, "Max hull", payload.maxHealthPercent);
        AppendPercent(builder, "Max shield", payload.maxShieldPercent);
        AppendReduction(builder, "Shield regen delay", payload.shieldRegenDelayReductionPercent);
        AppendPercent(builder, "Shield regen rate", payload.shieldRegenRatePercent);
        AppendPercent(builder, "Speed", payload.maxSpeedPercent);
        AppendPercent(builder, "Acceleration", payload.thrustAccelerationPercent);
        AppendPercent(builder, "Turn response", payload.turnResponsePercent);
        AppendReduction(builder, "Incoming damage", payload.incomingDamageReductionPercent);
        AppendPercent(builder, "Incoming damage taken", payload.incomingDamageTakenPercent);

        if (payload.extraDodgeInvulnerabilitySeconds > 0f) AppendLine(builder, $"+{payload.extraDodgeInvulnerabilitySeconds:0.##}s dodge invulnerability");
        if (payload.aimAssistConeAngleBonus > 0f) AppendLine(builder, $"+{payload.aimAssistConeAngleBonus:0.#}° aim assist cone");
        if (payload.aimAssistMaxCorrectionBonus > 0f) AppendLine(builder, $"+{payload.aimAssistMaxCorrectionBonus:0.#}° aim correction");
        if (payload.aimAssistRangeBonus > 0f) AppendLine(builder, $"+{payload.aimAssistRangeBonus:0.#} aim assist range");
        if (payload.projectileHitRadiusBonus > 0f) AppendLine(builder, $"+{payload.projectileHitRadiusBonus:0.##} projectile hit radius");
        if (payload.primaryWeaponPierces) AppendLine(builder, $"First weapon pierces {payload.primaryWeaponPierceCount} extra target; {FormatPercent(payload.primaryWeaponPierceDamageMultiplier)} damage after pierce");
        if (payload.restoreFullShieldOnBreakOncePerWave) AppendLine(builder, "Once per wave, restore full shields on shield break");
        if (payload.shieldOverchargePercent > 0f) AppendLine(builder, $"Shield regen can overcharge to {FormatPercent(1f + payload.shieldOverchargePercent)}");
        if (payload.noDamageRampMaxPercent > 0f) AppendLine(builder, $"After {payload.noDamageRampDelaySeconds:0.#}s without damage, gain {FormatPercent(payload.noDamageRampPercentPerSecond)} damage per second, up to {FormatPercent(payload.noDamageRampMaxPercent)}");
        if (payload.executionLotteryEnabled) AppendLine(builder, $"{FormatPercent(payload.executionLotteryChance)} chance for damage to one-shot non-boss enemies");
        if (payload.futureStatBoostMultiplierBonus > 0f) AppendLine(builder, $"Future stat boosts are {FormatPercent(payload.futureStatBoostMultiplierBonus)} stronger");
        if (instant.repairMissingHullFraction > 0f) AppendLine(builder, instant.repairMissingHullFraction >= 0.999f ? "Restore full hull" : $"Restore {FormatPercent(instant.repairMissingHullFraction)} missing hull");
        if (instant.refillShieldToFull) AppendLine(builder, "Refill shields");
        if (instant.grantExtraLife) AppendLine(builder, "Gain one extra life");
    }

    private static void AppendPercent(StringBuilder builder, string label, float value)
    {
        if (Mathf.Abs(value) > 0.0001f)
        {
            AppendLine(builder, $"{label} {(value >= 0f ? "+" : "")}{FormatPercent(value)}");
        }
    }

    private static void AppendReduction(StringBuilder builder, string label, float value)
    {
        if (Mathf.Abs(value) > 0.0001f)
        {
            string sign = value >= 0f ? "-" : "+";
            AppendLine(builder, $"{label} {sign}{FormatPercent(Mathf.Abs(value))}");
        }
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(line);
    }

    private static string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
