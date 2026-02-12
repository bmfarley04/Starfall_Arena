using NUnit.Framework;
using StarfallArena.UI;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Thorns", menuName = "Starfall Arena/Augments/Thorns", order = 2)]

public class Regenerator : Augment
{
    [Tooltip("Damage dealt to other entities on contact")]
    public float contactDamage = 10f;

    [Tooltip("Impact force applied to target on contact")]
    public float contactImpactForce = 0f;

    [Tooltip("Seconds to wait before damaging the same object again")]
    public float hitCooldown = 0.5f;

    [Header("Regeneration")]
    [Tooltip("Amount of health restored per second while anchored")]
    public float healRate = 5f;

    [Tooltip("Seconds the player must remain anchored before healing starts")]
    public float healDelay = 1.0f;

    [Tooltip("Seconds after taking damage before healing can resume")]
    public float damageInterruptCooldown = 1.5f;

    // Stores the time when an object can be hit again
    private Dictionary<GameObject, float> hitTimers = new Dictionary<GameObject, float>();

    // Tracks last time this player took damage (used to interrupt healing)
    private float lastDamageTime = -999f;

    // Tracks when the player started anchoring (so we can wait healDelay before starting heal)
    private float anchorStartTime = -999f;

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Thorns";
        description = "While anchored, slowly regenerate health after a short delay. Taking damage interrupts healing.";
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
            }
        }
        else
        {
            anchorStartTime = -999f;
        }
    }

    public override void OnContact(Collision2D collision)
    {
        if (!IsAugmentActive()) return;
        if (player == null || playerReference == null) return;

        var otherCollider = collision.collider;
        if (otherCollider == null) return;

        GameObject other = otherCollider.gameObject;

        // --- COOLDOWN CHECK ---
        // If the object is in our dictionary and the current time hasn't reached the "next allowed hit" time, stop.
        if (hitTimers.ContainsKey(other) && Time.time < hitTimers[other])
        {
            return;
        }

        Vector3 hitPoint = otherCollider.ClosestPoint(player.transform.position);
        HandleContact(other, hitPoint);

        // --- UPDATE COOLDOWN ---
        // Set the next time this specific object is allowed to take damage
        hitTimers[other] = Time.time + hitCooldown;
    }

    private void HandleContact(GameObject other, Vector3 hitPoint)
    {
        if (other == playerReference) return;

        var target = other.GetComponent<Entity>();
        if (target == null) return;

        target.TakeDamage(contactDamage, contactImpactForce, hitPoint, DamageSource.Other);

        // If we hit another entity while anchored, consider that as taking damage? No.
        // Nothing to do here for regenerator on dealing contact damage.
    }

    public override void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.OnTakeDamage(damage, impactForce, hitPoint, source);
        // record the time of last damage so regen is interrupted
        lastDamageTime = Time.time;
    }
}