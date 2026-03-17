using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Partial class handling projectile spawning, combat state broadcasting,
/// death effects, reflected projectiles, and fire sound resolution.
/// </summary>
public partial class NetMovement
{
    // ===== PROJECTILE VISUAL & AUDIO RESOLUTION =====

    public GameObject ResolveProjectileVisualPrefab(NetProjectileVisualType visualType)
    {
        switch (visualType)
        {
            case NetProjectileVisualType.Primary:
                return _player != null ? _player.projectileWeapon.prefab : null;
            case NetProjectileVisualType.GigaBlastTier1:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier1ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier2:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier2ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier3:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier3ProjectilePrefab;
            case NetProjectileVisualType.GigaBlastTier4:
                return GetComponent<GigaBlast>()?.gigaBlast.visual.tier4ProjectilePrefab;
            case NetProjectileVisualType.Class2EmpoweredShot:
                return GetComponent<EmpoweredShot>()?.empoweredShot.projectilePrefab;
            case NetProjectileVisualType.Class2PhysicalProjectile:
                return GetComponent<PhysicalProjectileAbility>()?.physicalProjectile.projectilePrefab;
            default:
                return null;
        }
    }

    private SoundEffect ResolveFireSound(NetProjectileVisualType visualType)
    {
        switch (visualType)
        {
            case NetProjectileVisualType.GigaBlastTier1:
                return GetComponent<GigaBlast>()?.gigaBlast.tier1FireSound;
            case NetProjectileVisualType.GigaBlastTier2:
                return GetComponent<GigaBlast>()?.gigaBlast.tier2FireSound;
            case NetProjectileVisualType.GigaBlastTier3:
                return GetComponent<GigaBlast>()?.gigaBlast.tier3FireSound;
            case NetProjectileVisualType.GigaBlastTier4:
                return GetComponent<GigaBlast>()?.gigaBlast.tier4FireSound;
            case NetProjectileVisualType.Class2EmpoweredShot:
                return GetComponent<EmpoweredShot>()?.empoweredShot.fireSound;
            case NetProjectileVisualType.Class2PhysicalProjectile:
                return GetComponent<PhysicalProjectileAbility>()?.physicalProjectile.fireSound;
            default:
                return _primaryFireSound;
        }
    }

    // ===== PRIMARY FIRE =====

    public void RequestPrimaryFire(NetFireRequest request)
    {
        if (!NetTickUtil.IsActive || !IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            HandlePrimaryFireServer(request);
            return;
        }

        SubmitPrimaryFireServerRpc(request);
    }

    [ServerRpc]
    private void SubmitPrimaryFireServerRpc(NetFireRequest request, ServerRpcParams rpcParams = default)
    {
        HandlePrimaryFireServer(request);
    }

    private void HandlePrimaryFireServer(NetFireRequest request)
    {
        GameObject projectilePrefab = ResolveProjectileVisualPrefab(request.VisualType);
        if (_player == null || projectilePrefab == null)
        {
            return;
        }

        float tickInterval = NetTickUtil.TickInterval > 0f ? NetTickUtil.TickInterval : Time.fixedDeltaTime;
        int cooldownTicks = Mathf.Max(1, Mathf.CeilToInt(_player.PrimaryFireCooldown / Mathf.Max(0.0001f, tickInterval)));
        bool isNewVolleyTick = request.Tick != _lastServerPrimaryFireTick;
        if (isNewVolleyTick && request.Tick < _lastServerPrimaryFireTick + cooldownTicks)
        {
            return;
        }

        if (isNewVolleyTick)
        {
            _lastServerPrimaryFireTime = Time.time;
            _lastServerPrimaryFireTick = request.Tick;
        }

        int accuracyAttackId = ResolveServerAccuracyAttackId(request.Tick, request.VisualType);

        GameObject projectileObject = Instantiate(projectilePrefab, request.SpawnPosition, Quaternion.identity);
        if (!projectileObject.TryGetComponent(out ProjectileScript projectile))
        {
            Destroy(projectileObject);
            return;
        }

        projectile.targetTag = _player.enemyTag;
        projectile.SetNetworkAuthority(this, request.Tick);
        projectile.Initialize(
            request.Direction,
            request.InheritedVelocity,
            request.Speed,
            request.Damage,
            request.Lifetime,
            request.ImpactForce,
            _player,
            accuracyAttackId);

        if (request.CanPierce)
        {
            projectile.EnablePiercing(request.PierceMultiplier);
        }

        if (request.AppliesSlow)
        {
            projectile.EnableSlow(request.SlowMultiplier, request.SlowDuration);
        }

        // Play fire sound on host for remote player's projectiles
        if (!IsOwner && _player != null)
        {
            SoundEffect fireSound = ResolveFireSound(request.VisualType);
            fireSound?.Play(_player.GetAvailableAudioSource());
        }

        BroadcastProjectileSpawnClientRpc(new NetProjectileSpawnData
        {
            Tick = request.Tick,
            SpawnPosition = request.SpawnPosition,
            Direction = request.Direction,
            InheritedVelocity = request.InheritedVelocity,
            Speed = request.Speed,
            Damage = request.Damage,
            Lifetime = request.Lifetime,
            ImpactForce = request.ImpactForce,
            RecoilForce = request.RecoilForce,
            ApplyRecoil = request.ApplyRecoil,
            PierceMultiplier = request.PierceMultiplier,
            SlowMultiplier = request.SlowMultiplier,
            SlowDuration = request.SlowDuration,
            CanPierce = request.CanPierce,
            AppliesSlow = request.AppliesSlow,
            VisualType = request.VisualType,
        });

        if (request.ApplyRecoil)
        {
            _player.ApplyRecoil(request.RecoilForce);
        }
    }

    private int ResolveServerAccuracyAttackId(int requestTick, NetProjectileVisualType visualType)
    {
        if (_player == null || !CountsTowardAccuracy(visualType))
        {
            return Player.InvalidAttackId;
        }

        if (_lastServerAccuracyVolleyTick == requestTick && _lastServerAccuracyVolleyType == visualType)
        {
            return _lastServerAccuracyAttackId;
        }

        _lastServerAccuracyVolleyTick = requestTick;
        _lastServerAccuracyVolleyType = visualType;
        _lastServerAccuracyAttackId = _player.BeginTrackedAttack();
        return _lastServerAccuracyAttackId;
    }

    private static bool CountsTowardAccuracy(NetProjectileVisualType visualType)
    {
        switch (visualType)
        {
            case NetProjectileVisualType.Primary:
            case NetProjectileVisualType.GigaBlastTier1:
            case NetProjectileVisualType.GigaBlastTier2:
            case NetProjectileVisualType.GigaBlastTier3:
            case NetProjectileVisualType.GigaBlastTier4:
            case NetProjectileVisualType.Class2EmpoweredShot:
            case NetProjectileVisualType.Class2PhysicalProjectile:
                return true;
            default:
                return false;
        }
    }

    [ClientRpc]
    private void BroadcastProjectileSpawnClientRpc(NetProjectileSpawnData spawnData)
    {
        if (IsServer || (IsOwner && !IsServer))
        {
            return;
        }

        GameObject prefab = ResolveProjectileVisualPrefab(spawnData.VisualType);
        if (prefab == null)
        {
            return;
        }

        GameObject projectileObject = Instantiate(prefab, spawnData.SpawnPosition, Quaternion.identity);
        if (!projectileObject.TryGetComponent(out ProjectileScript projectile))
        {
            Destroy(projectileObject);
            return;
        }

        projectile.targetTag = _player != null ? _player.enemyTag : string.Empty;
        projectile.SetCosmeticOnly(true);
        projectile.Initialize(
            spawnData.Direction,
            spawnData.InheritedVelocity,
            spawnData.Speed,
            spawnData.Damage,
            spawnData.Lifetime,
            spawnData.ImpactForce,
            _player);

        if (spawnData.CanPierce)
        {
            projectile.EnablePiercing(spawnData.PierceMultiplier);
        }

        if (spawnData.AppliesSlow)
        {
            projectile.EnableSlow(spawnData.SlowMultiplier, spawnData.SlowDuration);
        }

        // Play fire sound on remote client
        if (_player != null)
        {
            SoundEffect fireSound = ResolveFireSound(spawnData.VisualType);
            fireSound?.Play(_player.GetAvailableAudioSource());
        }
    }

    // ===== COMBAT STATE =====

    public void BroadcastCombatState(float health, float shield, Vector3 hitPoint, DamageSource source, bool shieldHit, bool shieldBreak, float impactForce)
    {
        if (!IsServer)
        {
            return;
        }

        // Include slow state so clients can show/hide the slow particle effect.
        bool isSlowed = _player != null && _player.IsSlowed();
        float slowMultiplier = isSlowed ? _player.GetSlowMultiplier() : 1f;
        float slowRemainingTime = isSlowed ? _player.GetSlowRemainingTime() : 0f;

        BroadcastCombatStateClientRpc(health, shield, hitPoint, (int)source, shieldHit, shieldBreak, impactForce, slowMultiplier, slowRemainingTime);
    }

    [ClientRpc]
    private void BroadcastCombatStateClientRpc(float health, float shield, Vector2 hitPoint, int source, bool shieldHit, bool shieldBreak, float impactForce, float slowMultiplier, float slowRemainingTime)
    {
        if (_player == null)
        {
            return;
        }

        DamageSource damageSource = (DamageSource)source;
        _player.ApplyAuthoritativeCombatState(health, shield, hitPoint, damageSource, shieldHit, shieldBreak);
        if (!IsServer)
        {
            _player.NotifyAuthoritativeDamageReceived(damageSource);
        }

        // Reset the shield regen delay timer so the owner's local regen stays
        // in sync with the server (TakeDamage only runs on the server, so the
        // client's _lastShieldHitTime would never update otherwise).
        _player.ResetShieldRegenTimer();

        // Apply slow effect so clients show the particle system
        if (slowRemainingTime > 0f)
        {
            _player.ApplySlow(slowMultiplier, slowRemainingTime);
        }

        // Play damage sounds on remote clients. Skip only for the host's own player,
        // which already plays audio in TakeDamage via shouldPlayLocalAudio.
        if (!(IsServer && IsOwner))
        {
            _player.PlayNetworkDamageSounds(damageSource, shieldHit);
        }
    }

    // ===== REFLECTED PROJECTILES =====

    public void BroadcastReflectedProjectile(NetReflectedProjectileData data)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastReflectedProjectileClientRpc(data);
    }

    [ClientRpc]
    private void BroadcastReflectedProjectileClientRpc(NetReflectedProjectileData data)
    {
        // Server already has the authoritative reflected projectile
        if (IsServer)
        {
            return;
        }

        GameObject prefab = ResolveProjectileVisualPrefab(data.VisualType);
        if (prefab == null)
        {
            return;
        }

        GameObject projectileObject = Instantiate(prefab, data.SpawnPosition, Quaternion.identity);
        if (!projectileObject.TryGetComponent(out ProjectileScript projectile))
        {
            Destroy(projectileObject);
            return;
        }

        projectile.targetTag = _player != null ? _player.enemyTag : string.Empty;
        projectile.SetCosmeticOnly(true);
        projectile.Initialize(
            data.Direction,
            Vector2.zero,
            data.Speed,
            data.Damage,
            data.Lifetime,
            data.ImpactForce,
            _player);

        // Apply reflected visual state
        ProjectileVisualController visualController = projectileObject.GetComponent<ProjectileVisualController>();
        if (visualController != null)
        {
            visualController.ResetVisualState();
            visualController.SetProjectileColor(data.ReflectColor);
        }
    }

    // ===== DEATH =====

    public void BroadcastDeath(Vector2 position, float rotation, Vector2 lastDamageDirection)
    {
        if (!IsServer)
        {
            return;
        }

        BroadcastDeathClientRpc(position, rotation, lastDamageDirection);
    }

    [ClientRpc]
    private void BroadcastDeathClientRpc(Vector2 position, float rotation, Vector2 lastDamageDirection)
    {
        // Server already ran Die() locally; only non-server clients need effects
        if (IsServer)
        {
            return;
        }

        if (_player != null)
        {
            _player.PlayNetworkDeathEffects(position, rotation, lastDamageDirection);
        }
    }
}
