using UnityEngine;

[CreateAssetMenu(fileName = "InvasionStatRewardDefinition3D", menuName = "Starfall Arena/3D/Invasion/Stat Reward", order = 60)]
public class InvasionStatRewardDefinition3D : ScriptableObject
{
    [System.Flags]
    public enum RewardEligibility3D
    {
        None = 0,
        RequiresProjectileWeapon = 1 << 0,
        RequiresBeamWeapon = 1 << 1,
        OneTimePerRun = 1 << 2
    }

    [System.Serializable]
    public struct PersistentRewardPayload3D
    {
        [Tooltip("Adds this much percentage damage to projectile, missile, and beam primary weapons. 0.1 = +10% damage.")]
        public float allWeaponDamagePercent;
        [Tooltip("Reduces projectile-family cooldown by this percentage. 0.08 = 8% faster fire rate.")]
        public float projectileCooldownReductionPercent;
        [Tooltip("Adds this much percentage speed to projectile-family weapons.")]
        public float projectileSpeedPercent;
        [Tooltip("Adds this much percentage lifetime to projectile-family weapons.")]
        public float projectileLifetimePercent;
        [Tooltip("Adds this much percentage damage to beam-family weapons.")]
        public float beamDamagePercent;
        [Tooltip("Adds this much percentage max energy capacity to beam-family weapons.")]
        public float beamCapacityPercent;
        [Tooltip("Adds this much percentage passive regeneration to beam-family weapons.")]
        public float beamRegenPercent;
        [Tooltip("Adds this much flat max hull to the player.")]
        public float flatMaxHealth;
        [Tooltip("Adds this much flat max shield to the player.")]
        public float flatMaxShield;
        [Tooltip("Reduces shield regeneration delay by this percentage. 0.12 = 12% shorter delay.")]
        public float shieldRegenDelayReductionPercent;
        [Tooltip("Adds this much percentage shield regeneration rate.")]
        public float shieldRegenRatePercent;
        [Tooltip("Adds this much percentage thrust acceleration.")]
        public float thrustAccelerationPercent;
        [Tooltip("Adds this much percentage top speed.")]
        public float maxSpeedPercent;
        [Tooltip("Adds this much percentage turn speed and turn accel/decel.")]
        public float turnResponsePercent;
        [Tooltip("Adds this much percentage drift damping to the assist model for tighter control.")]
        public float flightAssistDampingPercent;
        [Tooltip("Adds this much percentage velocity-alignment strength to the assist model.")]
        public float flightAssistAlignmentPercent;
    }

    [System.Serializable]
    public struct InstantRewardPayload3D
    {
        [Tooltip("If greater than 0, restore this fraction of the player's missing hull when chosen. 0.25 = heal 25% of missing hull.")]
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

    [Header("Display")]
    [Tooltip("Title shown on the reused augment-card reward UI.")]
    [SerializeField] private string displayName = "Reward";
    [Tooltip("Description shown on the reused augment-card reward UI.")]
    [TextArea(3, 5)]
    [SerializeField] private string description = "";
    [Tooltip("Icon shown on the reused augment-card reward UI.")]
    [SerializeField] private Sprite icon;

    [Header("Offer Rules")]
    [Tooltip("Relative weight used when this reward is eligible for a wave offer roll.")]
    [Min(0f)]
    [SerializeField] private float offerWeight = 1f;
    [Tooltip("If disabled, the reward can only appear once per run even if it does not use an explicit one-time flag.")]
    [SerializeField] private bool repeatable = true;
    [Tooltip("Eligibility requirements used to filter which ships and run states may see this reward.")]
    [SerializeField] private RewardEligibility3D eligibility = RewardEligibility3D.None;

    [Header("Payload")]
    [Tooltip("Persistent run-long stat changes applied by this reward.")]
    [SerializeField] private PersistentRewardPayload3D persistent;
    [Tooltip("Immediate one-shot effects applied when this reward is chosen.")]
    [SerializeField] private InstantRewardPayload3D instant;

    public string RewardId => string.IsNullOrWhiteSpace(rewardId) ? name : rewardId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public float OfferWeight => Mathf.Max(0f, offerWeight);
    public bool Repeatable => repeatable;
    public RewardEligibility3D Eligibility => eligibility;
    public PersistentRewardPayload3D Persistent => persistent;
    public InstantRewardPayload3D Instant => instant;

    public bool RequiresProjectileWeapon => (eligibility & RewardEligibility3D.RequiresProjectileWeapon) != 0;
    public bool RequiresBeamWeapon => (eligibility & RewardEligibility3D.RequiresBeamWeapon) != 0;
    public bool IsOneTimePerRun => !repeatable || (eligibility & RewardEligibility3D.OneTimePerRun) != 0;
    public bool HasInstantRepair => instant.repairMissingHullFraction > 0f || instant.refillShieldToFull;
    public bool GrantsExtraLife => instant.grantExtraLife;

    public void ConfigureRuntimeDefinition(
        string runtimeRewardId,
        string runtimeDisplayName,
        string runtimeDescription,
        float runtimeOfferWeight,
        bool runtimeRepeatable,
        RewardEligibility3D runtimeEligibility,
        PersistentRewardPayload3D runtimePersistent,
        InstantRewardPayload3D runtimeInstant,
        Sprite runtimeIcon = null)
    {
        rewardId = runtimeRewardId;
        displayName = runtimeDisplayName;
        description = runtimeDescription;
        offerWeight = runtimeOfferWeight;
        repeatable = runtimeRepeatable;
        eligibility = runtimeEligibility;
        persistent = runtimePersistent;
        instant = runtimeInstant;
        icon = runtimeIcon;
    }

    private void OnValidate()
    {
        offerWeight = Mathf.Max(0f, offerWeight);
        persistent.allWeaponDamagePercent = Mathf.Max(0f, persistent.allWeaponDamagePercent);
        persistent.projectileCooldownReductionPercent = Mathf.Max(0f, persistent.projectileCooldownReductionPercent);
        persistent.projectileSpeedPercent = Mathf.Max(0f, persistent.projectileSpeedPercent);
        persistent.projectileLifetimePercent = Mathf.Max(0f, persistent.projectileLifetimePercent);
        persistent.beamDamagePercent = Mathf.Max(0f, persistent.beamDamagePercent);
        persistent.beamCapacityPercent = Mathf.Max(0f, persistent.beamCapacityPercent);
        persistent.beamRegenPercent = Mathf.Max(0f, persistent.beamRegenPercent);
        persistent.flatMaxHealth = Mathf.Max(0f, persistent.flatMaxHealth);
        persistent.flatMaxShield = Mathf.Max(0f, persistent.flatMaxShield);
        persistent.shieldRegenDelayReductionPercent = Mathf.Max(0f, persistent.shieldRegenDelayReductionPercent);
        persistent.shieldRegenRatePercent = Mathf.Max(0f, persistent.shieldRegenRatePercent);
        persistent.thrustAccelerationPercent = Mathf.Max(0f, persistent.thrustAccelerationPercent);
        persistent.maxSpeedPercent = Mathf.Max(0f, persistent.maxSpeedPercent);
        persistent.turnResponsePercent = Mathf.Max(0f, persistent.turnResponsePercent);
        persistent.flightAssistDampingPercent = Mathf.Max(0f, persistent.flightAssistDampingPercent);
        persistent.flightAssistAlignmentPercent = Mathf.Max(0f, persistent.flightAssistAlignmentPercent);
        instant.repairMissingHullFraction = Mathf.Clamp01(instant.repairMissingHullFraction);
    }
}
