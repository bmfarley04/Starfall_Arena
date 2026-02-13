using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Dagger", menuName = "Starfall Arena/Augments/Dagger", order = 2)]
public class Dagger : Augment
{
    [Tooltip("Multiplier applied to the player's damage output after taking damage")]
    public float damageMultiplier = 1.5f;

    [Tooltip("How long (seconds) the damage boost lasts after taking damage")]
    public float boostDuration = 5f;

    // runtime end time for the temporary damage boost
    private float _damageBoostEndTime = 0f;

    protected override void Awake()
    {
        // Initializing IDs and Descriptions
        augmentID = this.name;
        augmentName = "Dagger";
        description = "After taking damage, deal 50% more damage for the next 5 seconds.";
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (player == null) return;

        // If the boost expired, remove it
        if (player.damageMultipliers.ContainsKey(augmentID) && Time.time >= _damageBoostEndTime)
        {
            RemoveMultiplier(player.damageMultipliers);
        }
    }

    public override void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.OnTakeDamage(damage, impactForce, hitPoint, source);
        if (player == null) return;

        // Only apply if augment is currently active (respect rounds duration)
        if (!IsAugmentActive()) return;

        // Refresh end time
        _damageBoostEndTime = Time.time + boostDuration;

        // Add or refresh the damage multiplier on the player
        AddOrRefreshMultiplier(damageMultiplier, player.damageMultipliers);
    }


    public override bool IsAugmentActive()
    {
        return base.IsAugmentActive();
    }
}