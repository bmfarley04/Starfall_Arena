using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;

public abstract class AugmentRuntimeBase : IAugmentRuntime
{
    private const float DefaultTransientEffectLifetime = 2f;

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
        }
    }

    protected void AddOrRefreshMultiplier(float mult, Dictionary<string, float> typeMultiplier)
    {
        if (player == null || typeMultiplier == null) return;

        if (!typeMultiplier.ContainsKey(Definition.augmentID))
        {
            typeMultiplier.Add(Definition.augmentID, mult);
        }
        else
        {
            typeMultiplier[Definition.augmentID] = mult;
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
        }
    }

    protected void SetAttachedEffectActive(ref GameObject runtimeInstance, GameObject prefab, bool isActive, string effectName)
    {
        if (player == null || prefab == null)
        {
            return;
        }

        if (runtimeInstance == null)
        {
            runtimeInstance = Object.Instantiate(prefab, player.transform);
            runtimeInstance.name = $"{effectName}_{Definition.augmentID}";
            runtimeInstance.transform.localPosition = Vector3.zero;
            runtimeInstance.transform.localRotation = Quaternion.identity;
            runtimeInstance.SetActive(false);
        }

        if (runtimeInstance.activeSelf != isActive)
        {
            runtimeInstance.SetActive(isActive);
        }
    }

    protected void SpawnTransientEffect(GameObject prefab, float fallbackLifetime = DefaultTransientEffectLifetime)
    {
        if (player == null || prefab == null)
        {
            return;
        }

        GameObject effect = Object.Instantiate(prefab, player.transform.position, player.transform.rotation);
        ParticleSystem particle = effect.GetComponent<ParticleSystem>();

        float lifetime = fallbackLifetime;
        if (particle != null)
        {
            lifetime = Mathf.Max(0.1f, particle.main.duration + particle.main.startLifetime.constantMax);
        }

        Object.Destroy(effect, lifetime);
    }

    protected void PlaySoundEffect(SoundEffect soundEffect)
    {
        if (player == null || soundEffect == null)
        {
            return;
        }

        AudioSource source = player.GetAvailableAudioSource();
        if (source != null)
        {
            soundEffect.Play(source);
            return;
        }

        soundEffect.PlayAtPoint(player.transform.position);
    }
}
