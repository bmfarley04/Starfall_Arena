using StarfallArena.UI;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "ReinforcedHull", menuName = "Starfall Arena/Augments/Reinforced Hull", order = 2)]
public class ReinforcedHull : Augment
{
    [Tooltip("Multiplier applied to max health while augment is active")]
    public float healthMultiplier = 1.5f; // 50% more health

    // Duration in rounds
    private void Reset()
    {
        rounds = 3;
    }

    private float _appliedAmount = 0f;
    private bool _isApplied = false;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Reinforced Hull";
        description = "Start with 50% more health for the next three Rounds.";

        // Default duration
        rounds = 3;
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (player == null) return;

        if (IsAugmentActive())
        {
            if (!_isApplied)
            {
                ApplyHealthBonus();
            }
        }
        else
        {
            if (_isApplied)
            {
                RemoveHealthBonus();
            }
        }
    }

    private void ApplyHealthBonus()
    {
        if (player == null) return;

        // Calculate added amount based on current maxHealth
        float originalMax = player.maxHealth;
        _appliedAmount = originalMax * (healthMultiplier - 1f);

        player.maxHealth = originalMax + _appliedAmount;

        // Increase current health by the same amount using reflection (currentHealth is protected)
        var currentField = player.GetType().BaseType.GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentField != null)
        {
            float current = (float)currentField.GetValue(player);
            currentField.SetValue(player, current + _appliedAmount);
        }

        // Invoke protected OnHealthChanged to update HUD
        var onHealthChanged = player.GetType().BaseType.GetMethod("OnHealthChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        onHealthChanged?.Invoke(player, null);

        _isApplied = true;
        Debug.Log($"{augmentName} applied: +{_appliedAmount} max health");
    }

    private void RemoveHealthBonus()
    {
        if (player == null) return;

        // Revert max health
        player.maxHealth = player.maxHealth - _appliedAmount;

        // Clamp currentHealth to new max and update using reflection
        var currentField = player.GetType().BaseType.GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentField != null)
        {
            float current = (float)currentField.GetValue(player);
            float clamped = Mathf.Min(current, player.maxHealth);
            currentField.SetValue(player, clamped);
        }

        // Invoke protected OnHealthChanged to update HUD
        var onHealthChanged = player.GetType().BaseType.GetMethod("OnHealthChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        onHealthChanged?.Invoke(player, null);

        Debug.Log($"{augmentName} removed: -{_appliedAmount} max health");

        _appliedAmount = 0f;
        _isApplied = false;
    }
}
