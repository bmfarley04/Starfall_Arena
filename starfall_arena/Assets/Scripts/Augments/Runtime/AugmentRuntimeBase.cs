using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;

public abstract class AugmentRuntimeBase : IAugmentRuntime
{
    public Augment Definition { get; }
    public int RoundAcquired { get; private set; }

    protected Player player;
    protected int currentRound;

    protected AugmentRuntimeBase(Augment definition)
    {
        Definition = definition;
    }

    public virtual void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        this.player = player;
        RoundAcquired = roundAcquired;
        LoadPersistentState(persistentState);
    }

    public virtual void OnRoundSet(int currentRound)
    {
        this.currentRound = currentRound;
    }

    public virtual void ExecuteEffects() { }

    public virtual void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source) { }

    public virtual void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source) { }

    public virtual void OnTakeDirectDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source) { }

    public virtual void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source) { }

    public virtual void OnContact(Collision2D collision) { }

    public virtual object CapturePersistentState()
    {
        return null;
    }

    protected virtual void LoadPersistentState(object persistentState) { }

    protected bool IsActiveByRounds()
    {
        return IsActiveByRounds(currentRound, RoundAcquired, Definition.rounds);
    }

    protected static bool IsActiveByRounds(int currentRound, int roundAcquired, int rounds)
    {
        if (rounds == -1)
        {
            return true;
        }

        return currentRound - roundAcquired < rounds;
    }

    protected void AddMultiplier(float mult, Dictionary<string, float> typeMultiplier)
    {
        if (player == null || typeMultiplier == null) return;

        if (IsActiveByRounds() && !typeMultiplier.ContainsKey(Definition.augmentID))
        {
            typeMultiplier.Add(Definition.augmentID, mult);
            player.SetAugmentVariables();
            Debug.Log($"{Definition.augmentName} activated: x{mult}");
        }
    }

    protected void AddOrRefreshMultiplier(float mult, Dictionary<string, float> typeMultiplier)
    {
        if (player == null || typeMultiplier == null) return;

        if (!typeMultiplier.ContainsKey(Definition.augmentID))
        {
            typeMultiplier.Add(Definition.augmentID, mult);
            Debug.Log($"{Definition.augmentName} activated: x{mult}");
        }
        else
        {
            typeMultiplier[Definition.augmentID] = mult;
            Debug.Log($"{Definition.augmentName} refreshed: x{mult}");
        }

        player.SetAugmentVariables();
    }

    protected void RemoveMultiplier(Dictionary<string, float> typeMultiplier)
    {
        if (player == null || typeMultiplier == null) return;

        if (typeMultiplier.ContainsKey(Definition.augmentID))
        {
            typeMultiplier.Remove(Definition.augmentID);
            player.SetAugmentVariables();
            Debug.Log($"{Definition.augmentName} deactivated: returned to normal");
        }
    }
}
