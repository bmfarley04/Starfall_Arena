using NUnit.Framework;
using StarfallArena.UI;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Thorns", menuName = "Starfall Arena/Augments/Thorns", order = 2)]

public class Thorns : Augment
{
    [Tooltip("Damage dealt to other entities on contact")]
    public float contactDamage = 10f;

    [Tooltip("Impact force applied to target on contact")]
    public float contactImpactForce = 0f;

    [Tooltip("Seconds to wait before damaging the same object again")]
    public float hitCooldown = 0.5f;

    // Stores the time when an object can be hit again
    private Dictionary<GameObject, float> hitTimers = new Dictionary<GameObject, float>();

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Thorns";
        description = "Deal contact damage to entities that touch you.";
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
    }
}