using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Evasion", menuName = "Starfall Arena/Augments/Evasion", order = 2)]

public class Evasion : Augment
{
    [Tooltip("Chance (0-1) to ignore incoming shield damage portion")]
    [Range(0f, 1f)]
    public float shieldIgnoreChance = 0.05f; // 5%

    [Tooltip("Chance (0-1) to ignore incoming health damage portion")]
    [Range(0f, 1f)]
    public float healthIgnoreChance = 0.10f; // 10%

    protected override void Awake()
    {
        augmentID = this.name;
        augmentName = "Evasion";
        description = "Small chance to ignore shield or health damage when hit.";
    }

    public override void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source = DamageSource.Projectile)
    {
        base.OnBeforeTakeDamage(ref damage, ref shieldIgnored, ref healthIgnored, source);

        if (!IsAugmentActive()) return;
        if (player == null) return;

        // Random roll for ignoring shield portion
        if (!shieldIgnored && Random.value < shieldIgnoreChance)
        {
            shieldIgnored = true;
            Debug.Log($"{player.name} evaded shield damage!");
        }

        // Random roll for ignoring health portion
        if (!healthIgnored && Random.value < healthIgnoreChance)
        {
            healthIgnored = true;
            Debug.Log($"{player.name} evaded damage!");
        }
    }

    public override void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source = DamageSource.Projectile)
    {
        base.OnBeforeTakeDirectDamage(ref damage, ref healthIgnored, source);

        if (!IsAugmentActive()) return;
        if (player == null) return;

        if (!healthIgnored && Random.value < healthIgnoreChance)
        {
            healthIgnored = true;
            Debug.Log($"{player.name} evaded direct damage!");
        }
    }
}