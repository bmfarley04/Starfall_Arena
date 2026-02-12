using StarfallArena.UI;
using System;
using System.Reflection;
using UnityEngine;

[CreateAssetMenu(fileName = "Auto-Shields", menuName = "Starfall Arena/Augments/Auto-Shields", order = 2)]
public class AutoShields : Augment
{
    [Tooltip("Once per round/combat, when your shields reach zero, regain all shields instantly.")]
    public bool enabled = true;

    // Track when this augment was last used so it only triggers once per round
    private int lastUsedRound = int.MinValue;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Auto-Shields";
        description = "Once per combat, when your shields reach zero, regain all shields instantly.";
    }

    public override void SetUpAugment(int roundAcquired)
    {
        base.SetUpAugment(roundAcquired);
        // reset usage tracker when augment is set up
        lastUsedRound = int.MinValue;
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (!IsAugmentActive() || !enabled) return;
        if (player == null || playerReference == null) return;

        // Ignore entities without shields
        if (player.maxShield <= 0f) return;

        // Only trigger when shields have just reached zero and we haven't used this round yet
        if (player.currentShield <= 0f && lastUsedRound != player.currentRound)
        {
            // Restore shields instantly
            player.currentShield = player.maxShield;

            // Call protected OnShieldChanged() to update HUD/visuals via reflection
            MethodInfo onShieldChanged = player.GetType().GetMethod("OnShieldChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                if (onShieldChanged != null)
                {
                    onShieldChanged.Invoke(player, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{augmentName}: failed to invoke OnShieldChanged via reflection: {ex.Message}");
            }

            // Ensure shield visuals are in correct state
            if (player.shieldController != null)
            {
                player.shieldController.SetRegeneration(false);
            }

            lastUsedRound = player.currentRound;
            Debug.Log($"{augmentName} restored shields for {player.gameObject.name} (round {player.currentRound})");
        }
    }
}
