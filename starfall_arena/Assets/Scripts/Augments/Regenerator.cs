using StarfallArena.UI;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "Regenerator", menuName = "Starfall Arena/Augments/Regenerator", order = 2)]

public class Regenerator : Augment
{
    [Header("Regeneration")]

    [Tooltip("Amount of health restored per second while anchored")]
    public float healRate = 5f;

    [Tooltip("Seconds the player must remain anchored before healing starts")]
    public float healDelay = 1.0f;

    [Tooltip("Seconds after taking damage before healing can resume")]
    public float damageInterruptCooldown = 1.5f;

    // Tracks last time this player took damage (used to interrupt healing)
    private float lastDamageTime = -999f;

    // Tracks when the player started anchoring (so we can wait healDelay before starting heal)
    private float anchorStartTime = -999f;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Regenerator";
        description = "While anchoring, slowly regenerate health after a short delay. Taking damage interrupts healing.";
    }

    public override void SetUpAugment(int roundAcquired)
    {
        base.SetUpAugment(roundAcquired);
        lastDamageTime = -999f;
        anchorStartTime = -999f;
    }

    public override void ExecuteEffects()
    {
        base.ExecuteEffects();

        if (!IsAugmentActive()) return;
        if (player == null || playerReference == null) return;

        // If player is anchored, start or continue timer. If not anchored, reset anchor start time.
        if (player.IsAnchored)
        {
            if (anchorStartTime < 0f) anchorStartTime = Time.time;

            // Only start healing after healDelay and if enough time passed since last damage
            if (Time.time >= anchorStartTime + healDelay && Time.time >= lastDamageTime + damageInterruptCooldown)
            {
                float amount = healRate * Time.deltaTime;
                player.Heal(amount);
                Debug.Log($"[Regenerator] Healing player for {amount:F1} HP (current: {player.CurrentHealth}/{player.maxHealth})");
            }
        }
        else
        {
            anchorStartTime = -999f;
        }
    }

    // Regenerator does not use contact damage. OnContact intentionally left unimplemented.

    public override void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.OnTakeDamage(damage, impactForce, hitPoint, source);
        // record the time of last damage so regen is interrupted
        lastDamageTime = Time.time;
        Debug.Log($"[Regenerator] Player took damage, interrupting healing until {lastDamageTime + damageInterruptCooldown}");
    }
}