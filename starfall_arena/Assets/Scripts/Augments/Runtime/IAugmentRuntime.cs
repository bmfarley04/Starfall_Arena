using UnityEngine;
using StarfallArena.UI;

public interface IAugmentRuntime
{
    Augment Definition { get; }
    int RoundAcquired { get; }

    void Initialize(Player player, int roundAcquired, object persistentState = null);
    void OnRoundSet(int currentRound);
    void ExecuteEffects();
    void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source);
    void OnNetworkDamageTaken(float damage, DamageSource source);
    void OnNetworkStateUpdated(float anchoredDamageTaken, bool isStunned, bool isAnchored);
    void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source);
    void OnTakeDirectDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source);
    void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source);
    void OnContact(Collision2D collision);
    void OnPrimaryProjectileHit(Entity target, Vector2 hitPoint, float damage);
    void OnRemoved();
    object CapturePersistentState();
}
