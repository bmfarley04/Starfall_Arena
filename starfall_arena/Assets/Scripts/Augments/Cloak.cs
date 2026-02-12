using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Cloak", menuName = "Starfall Arena/Augments/Cloak", order = 2)]
public class Cloak : Augment
{
    [Tooltip("Multiplier applied to the player's movement max speed after taking damage")]
    public float speedMultiplier = 1.5f;

    [Tooltip("How long (seconds) the speed boost lasts after taking damage")]
    public float boostDuration = 5f;

    // runtime end time for the temporary speed boost
    private float _speedBoostEndTime = 0f;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Cloak";
        description = "After taking damage, move 50% faster for the next 5 seconds.";
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (player == null) return;

        // If the boost expired, remove it
        if (player.speedMultipliers.ContainsKey(augmentID) && Time.time >= _speedBoostEndTime)
        {
            RemoveSpeedMultiplier();
        }
    }

    public override void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.OnTakeDamage(damage, impactForce, hitPoint, source);

        if (player == null) return;

        // Only apply if augment is currently active (respect rounds duration)
        if (!IsAugmentActive()) return;

        // Refresh end time
        _speedBoostEndTime = Time.time + boostDuration;

        // Add or refresh the multiplier on the player
        if (!player.speedMultipliers.ContainsKey(augmentID))
        {
            player.speedMultipliers.Add(augmentID, speedMultiplier);
            player.SetAugmentVariables();
            Debug.Log($"{augmentName} activated: speed x{speedMultiplier}");
        }
        else
        {
            // ensure stored value matches desired multiplier (in case it was changed)
            player.speedMultipliers[augmentID] = speedMultiplier;
            player.SetAugmentVariables();
            Debug.Log($"{augmentName} refreshed: speed x{speedMultiplier} for {boostDuration}s");
        }
    }

    public void RemoveSpeedMultiplier()
    {
        if (player == null) return;
        if (player.speedMultipliers.ContainsKey(augmentID))
        {
            player.speedMultipliers.Remove(augmentID);
            player.SetAugmentVariables();
            Debug.Log($"{augmentName} deactivated: speed returned to normal");
        }
    }

    public override bool IsAugmentActive()
    {
        // Default behavior: use rounds lifetime from base class
        return base.IsAugmentActive();
    }
}
