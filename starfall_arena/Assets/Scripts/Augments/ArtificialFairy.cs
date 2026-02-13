using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Artificial Fairy", menuName = "Starfall Arena/Augments/Artificial Fairy", order = 2)]
public class ArtificialFairy : Augment
{
    [Tooltip("Fraction of max health to set when the augment triggers (0-1)")]
    [Range(0f, 1f)]
    public float healFraction = 0.75f;

    // One-shot state
    private bool triggered = false;

    // Temporary shield backup so we can restore post-hit
    private float _originalShield = 0f;
    private bool _restoreShieldNextFixedUpdate = false;
    private float _shieldAdded = 0f;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Artificial Fairy";
        description = "The next time you would die, instantly heal to 75% health. This augment breaks.";
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        // After the damage application occurs we restore the player's shield to the original value
        if (_restoreShieldNextFixedUpdate && player != null)
        {
            player.currentShield = _originalShield;
            _restoreShieldNextFixedUpdate = false;
            _shieldAdded = 0f;
        }
    }

    public override void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        // Only trigger once and only while augment is active
        if (!IsAugmentActive() || triggered) return;
        if (player == null) return;

        // Simulate how much damage would get through after shields (same logic as Entity.TakeDamage)
        float availableShield = player.currentShield;
        float shieldAbsorb = Mathf.Min(availableShield, damage);
        float damageToHealth = damage - shieldAbsorb;

        // If this damage would reduce health to zero or less, trigger the fairy
        if (damageToHealth >= player.CurrentHealth)
        {
            triggered = true; // break the augment

            // Backup shield so we can restore it after the damage is processed
            _originalShield = player.currentShield;

            // Add enough shield to fully absorb this incoming hit so health does not actually drop to 0
            _shieldAdded = damage + 1f;
            player.currentShield += _shieldAdded;

            // Heal to the configured fraction of max health
            float target = player.maxHealth * healFraction;
            float amountToHeal = target - player.CurrentHealth;
            if (amountToHeal > 0f)
            {
                player.Heal(amountToHeal);
            }

            // Schedule shield restore on next FixedUpdate/ExecuteEffects
            _restoreShieldNextFixedUpdate = true;

            Debug.Log($"{augmentName} triggered: prevented death and healed to {Mathf.CeilToInt(target)} HP");
        }
    }

    public override bool IsAugmentActive()
    {
        // Only active while base lifetime is valid and not yet used
        return base.IsAugmentActive() && !triggered;
    }
}
