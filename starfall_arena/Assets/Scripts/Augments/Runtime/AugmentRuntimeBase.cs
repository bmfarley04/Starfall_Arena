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

    public virtual void OnNetworkDamageTaken(float damage, DamageSource source) { }

    public virtual void OnNetworkStateUpdated(float anchoredDamageTaken, bool isStunned, bool isAnchored) { }

    public virtual void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source) { }

    public virtual void OnTakeDirectDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source) { }

    public virtual void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source) { }

    public virtual void OnContact(Collision2D collision) { }

    public virtual void OnPrimaryProjectileHit(Entity target, Vector2 hitPoint, float damage) { }

    public virtual void OnRemoved() { }

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

        ApplyShipSizeScale(runtimeInstance.transform, prefab.transform.localScale, isWorldSpace: false);

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
        ApplyShipSizeScale(effect.transform, prefab.transform.localScale, isWorldSpace: true);
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

    private void ApplyShipSizeScale(Transform effectTransform, Vector3 baseScale, bool isWorldSpace)
    {
        if (player == null || effectTransform == null)
        {
            return;
        }

        float size = Mathf.Max(0.01f, player.ShipSize);
        Vector3 scaled = baseScale * size;

        if (isWorldSpace)
        {
            effectTransform.localScale = scaled;
            return;
        }

        effectTransform.localScale = scaled;
    }

    protected bool TryFirePrimaryVolleyFromAugment(float damageMultiplier, bool ignoreCooldown, PrimaryFireExecutionSource source, bool playSound = true)
    {
        if (player == null || player.projectileWeapon.prefab == null || player.turrets == null || player.turrets.Length == 0)
        {
            return false;
        }

        float safeDamageMultiplier = Mathf.Max(0f, damageMultiplier);
        NetMovement netMovement = player.GetComponent<NetMovement>();
        bool useNetworkPath = NetTickUtil.IsActive && netMovement != null && netMovement.IsSpawned;

        if (useNetworkPath)
        {
            if (!netMovement.IsServer)
            {
                return false;
            }

            int tick = NetTickUtil.CurrentTick;
            for (int turretIndex = 0; turretIndex < player.turrets.Length; turretIndex++)
            {
                Transform turret = player.turrets[turretIndex];
                if (turret == null)
                {
                    continue;
                }

                Vector2 direction = player.transform.up;
                netMovement.RequestPrimaryFire(new NetFireRequest
                {
                    Tick = tick,
                    SpawnPosition = turret.position,
                    Direction = direction.normalized,
                    InheritedVelocity = Vector2.zero,
                    Speed = player.projectileWeapon.speed,
                    Damage = player.projectileWeapon.damage * safeDamageMultiplier,
                    Lifetime = player.projectileWeapon.lifetime,
                    ImpactForce = player.projectileWeapon.impactForce,
                    RecoilForce = player.projectileWeapon.recoilForce,
                    ApplyRecoil = turretIndex == 0,
                    PierceMultiplier = 1f,
                    SlowMultiplier = 1f,
                    SlowDuration = 0f,
                    CanPierce = false,
                    AppliesSlow = false,
                    VisualType = NetProjectileVisualType.Primary,
                    IgnoreCooldown = ignoreCooldown,
                    OwnerPredicted = false,
                    FireSource = (byte)source,
                });
            }

            return true;
        }

        int attackId = player.BeginTrackedAttack();
        for (int i = 0; i < player.turrets.Length; i++)
        {
            Transform turret = player.turrets[i];
            if (turret == null)
            {
                continue;
            }

            GameObject projectile = Object.Instantiate(player.projectileWeapon.prefab, turret.position, player.transform.rotation);
            if (projectile.TryGetComponent<ProjectileScript>(out ProjectileScript projectileScript))
            {
                projectileScript.targetTag = player.enemyTag;
                projectileScript.Initialize(
                    player.transform.up,
                    Vector2.zero,
                    player.projectileWeapon.speed,
                    player.projectileWeapon.damage * safeDamageMultiplier,
                    player.projectileWeapon.lifetime,
                    player.projectileWeapon.impactForce,
                    player,
                    attackId);
            }
        }

        if (playSound && player.projectileFireSound != null)
        {
            player.projectileFireSound.Play(player.GetAvailableAudioSource());
        }

        PrimaryFireExecutionBus.Raise(player, source);
        return true;
    }
}
