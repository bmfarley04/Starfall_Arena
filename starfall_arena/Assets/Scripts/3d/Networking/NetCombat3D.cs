using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetMovement3D))]
[RequireComponent(typeof(Entity3D))]
public class NetCombat3D : NetworkBehaviour
{
    private const int ProjectileVisualTypeCount = 9;

    private readonly List<NetProjectileFireRequest3D> _projectileRequests = new List<NetProjectileFireRequest3D>(8);
    private readonly int[] _lastAcceptedProjectileTick = new int[ProjectileVisualTypeCount];

    private Entity3D _entity;
    private NetMovement3D _movement;
    private bool _loggedFireBeforeSpawn;
    private bool _loggedFireWithoutOwnership;
    private bool _loggedFireMissingSourceWeapon;
    private bool _loggedFireMissingProjectilePrefab;
    private bool _loggedFireNoRequests;
    private bool _loggedServerMissingProjectileBinding;
    private bool _loggedClientMissingProjectileBinding;
    private bool _loggedReflectedMissingProjectileBinding;

    private void Awake()
    {
        CacheReferences();
        for (int i = 0; i < _lastAcceptedProjectileTick.Length; i++)
        {
            _lastAcceptedProjectileTick[i] = -1;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CacheReferences();
        if (IsOwner)
        {
            _movement?.EnsureOwnerLocalControlReady();
        }
    }

    public bool TryFireProjectilePattern(
        Weapon3D sourceWeapon,
        ProjectileFireRequest3D request,
        ProjectileWeaponConfig3D fallbackConfig,
        SoundEffect fireSound)
    {
        if (!NetTickUtil.IsActive || !IsSpawned)
        {
            LogWarningOnce(ref _loggedFireBeforeSpawn, "[NetCombat3D] Owner projectile fire was ignored because networking is not active or this combat broker has not spawned yet.");
            return false;
        }

        if (!IsOwner)
        {
            LogWarningOnce(ref _loggedFireWithoutOwnership, "[NetCombat3D] Projectile fire was ignored on a non-owner player object. Only the owning client should drive local combat input.");
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedFireMissingSourceWeapon, "[NetCombat3D] Owner projectile fire was ignored because the selected weapon did not resolve to a Weapon3D source.");
            return false;
        }

        if (request.projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedFireMissingProjectilePrefab, $"[NetCombat3D] Owner projectile fire from {sourceWeapon.GetType().Name} was ignored because the request has no projectile prefab.");
            return false;
        }

        ApplyProjectileTargeting(ref request);
        NetProjectileVisualType3D visualType = ResolveProjectileVisualType(sourceWeapon, request.projectilePrefab);
        int tick = NetTickUtil.CurrentTick;
        _projectileRequests.Clear();
        sourceWeapon.BuildNetworkProjectileRequests(request, fallbackConfig, visualType, tick, _projectileRequests);
        if (_projectileRequests.Count == 0)
        {
            LogWarningOnce(ref _loggedFireNoRequests, $"[NetCombat3D] Owner projectile fire from {sourceWeapon.GetType().Name} produced no network fire requests. Check muzzle/spawn configuration.");
            return false;
        }

        if (!IsServer)
        {
            sourceWeapon.FireProjectilePatternLocal(request, fallbackConfig, fireSound, cosmeticOnly: true, networkAuthority: null, visualType);
            for (int i = 0; i < _projectileRequests.Count; i++)
            {
                SubmitProjectileFireServerRpc(_projectileRequests[i]);
            }
            return true;
        }

        for (int i = 0; i < _projectileRequests.Count; i++)
        {
            HandleProjectileFireServer(_projectileRequests[i]);
        }

        return true;
    }

    public void RequestBeamState(bool isFiring, Vector3 aimDirection)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsOwner)
        {
            return;
        }

        NetBeamState3D state = new NetBeamState3D
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring,
            AimDirection = aimDirection
        };

        if (IsServer)
        {
            HandleBeamStateServer(state);
            return;
        }

        SubmitBeamStateServerRpc(state);
    }

    public void UpdateBeamAim(Vector3 aimDirection)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsOwner)
        {
            return;
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        NetAimUpdate3D update = new NetAimUpdate3D
        {
            Tick = NetTickUtil.CurrentTick,
            AimDirection = aimDirection
        };

        if (IsServer)
        {
            HandleBeamAimServer(update);
            return;
        }

        SubmitBeamAimServerRpc(update);
    }

    public void UpdateTractorBeamAim(Vector3 aimDirection)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsOwner)
        {
            return;
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        NetAimUpdate3D update = new NetAimUpdate3D
        {
            Tick = NetTickUtil.CurrentTick,
            AimDirection = aimDirection
        };

        if (IsServer)
        {
            HandleTractorBeamAimServer(update);
            return;
        }

        SubmitTractorBeamAimServerRpc(update);
    }

    public void RequestTeleport(Vector3 targetPosition)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsOwner)
        {
            return;
        }

        NetTeleportState3D state = new NetTeleportState3D { TargetPosition = targetPosition };
        if (IsServer)
        {
            HandleTeleportServer(state);
            return;
        }

        SubmitTeleportServerRpc(state);
    }

    public void RequestReflectActivation()
    {
        RequestAbilityToggle(NetAbilityKind3D.Reflect, true, Vector3.zero);
    }

    public void RequestClass2ShieldActivation()
    {
        RequestAbilityToggle(NetAbilityKind3D.Class2Shield, true, Vector3.zero);
    }

    public void RequestTractorBeamState(bool isActive, Vector3 aimDirection)
    {
        RequestAbilityToggle(NetAbilityKind3D.TractorBeam, isActive, aimDirection);
    }

    public void RequestEmpowerState(bool isActive)
    {
        RequestAbilityToggle(NetAbilityKind3D.Class4Empower, isActive, Vector3.zero, allowServerAuthority: true);
    }

    [System.Obsolete("Class 4 dodge movement is predicted through NetMovement3D input snapshots. This legacy path is presentation-only.")]
    public void RequestClass4Dodge(Vector3 worldDirection)
    {
        RequestAbilityToggle(NetAbilityKind3D.Class4Dodge, true, worldDirection);
    }

    public void RequestGigaBlastChargeState(bool isCharging, int tier)
    {
        if (!NetTickUtil.IsActive || !IsSpawned || !IsOwner)
        {
            return;
        }

        NetGigaBlastChargeState3D state = new NetGigaBlastChargeState3D
        {
            IsCharging = isCharging,
            Tier = tier
        };

        if (IsServer)
        {
            HandleGigaBlastChargeServer(state);
            return;
        }

        SubmitGigaBlastChargeServerRpc(state);
    }

    public void BroadcastCombatState(NetCombatState3D state)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        BroadcastCombatStateClientRpc(state);
    }

    public void BroadcastDeath(Vector3 position, Quaternion rotation, Vector3 lastDamageDirection)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        BroadcastDeathClientRpc(position, rotation, lastDamageDirection);
    }

    public void BroadcastReflectedProjectile(Projectile3D projectile, Color reflectColor, NetProjectileVisualType3D visualType)
    {
        if (!IsServer || projectile == null)
        {
            return;
        }

        BroadcastReflectedProjectileClientRpc(new NetReflectedProjectileData3D
        {
            SpawnPosition = projectile.transform.position,
            Direction = projectile.Direction,
            Speed = projectile.Speed,
            Damage = projectile.Damage,
            Lifetime = projectile.RemainingLifetime,
            ImpactForce = projectile.ImpactForce,
            ProjectileScaleMultiplier = projectile.ProjectileScaleMultiplier,
            ReflectColor = reflectColor,
            TargetFaction = projectile.TargetFaction,
            VisualType = visualType
        });
    }

    public void BroadcastReflectedProjectile(Projectile3D projectile, Color reflectColor)
    {
        if (projectile == null)
        {
            return;
        }

        BroadcastReflectedProjectile(projectile, reflectColor, projectile.VisualType);
    }

    public void ApplyCombatVelocityDelta(Vector3 velocityDelta)
    {
        _movement?.ApplyCombatVelocityDelta(velocityDelta);
    }

    public void ApplyCombatWarp(Vector3 position)
    {
        _movement?.ApplyCombatWarp(position);
    }

    [ServerRpc]
    private void SubmitProjectileFireServerRpc(NetProjectileFireRequest3D fireRequest, ServerRpcParams rpcParams = default)
    {
        HandleProjectileFireServer(fireRequest);
    }

    private void HandleProjectileFireServer(NetProjectileFireRequest3D fireRequest)
    {
        if (!IsServer || !AcceptProjectileTick(fireRequest.VisualType, fireRequest.Tick))
        {
            return;
        }

        Weapon3D sourceWeapon = ResolveWeaponForVisualType(fireRequest.VisualType);
        GameObject projectilePrefab = ResolveProjectileVisualPrefab(fireRequest.VisualType);
        if (sourceWeapon == null || projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedServerMissingProjectileBinding, $"[NetCombat3D] Server rejected projectile fire because visual type {fireRequest.VisualType} could not resolve a source weapon or projectile prefab.");
            return;
        }

        sourceWeapon.SpawnNetworkProjectile(
            projectilePrefab,
            fireRequest,
            ResolveProjectileTargetTag(fireRequest.TargetFaction),
            fireRequest.TargetFaction,
            cosmeticOnly: false,
            networkAuthority: this,
            playMuzzleEffect: true);

        if (fireRequest.ApplyRecoil)
        {
            _movement?.ApplyCombatVelocityDelta(-fireRequest.Direction.normalized * fireRequest.RecoilForce);
        }

        BroadcastProjectileSpawnClientRpc(new NetProjectileSpawnData3D
        {
            Fire = fireRequest,
            ServerSpawnTime = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d
        });

        ResolveFireSound(fireRequest.VisualType)?.PlayAtPoint(transform.position);
    }

    [ClientRpc]
    private void BroadcastProjectileSpawnClientRpc(NetProjectileSpawnData3D spawnData)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        Weapon3D sourceWeapon = ResolveWeaponForVisualType(spawnData.Fire.VisualType);
        GameObject projectilePrefab = ResolveProjectileVisualPrefab(spawnData.Fire.VisualType);
        if (sourceWeapon == null || projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedClientMissingProjectileBinding, $"[NetCombat3D] Client received projectile cosmetic RPC for {spawnData.Fire.VisualType}, but this proxy could not resolve a source weapon or projectile prefab.");
            return;
        }

        NetProjectileFireRequest3D fire = spawnData.Fire;
        if (NetworkManager.Singleton != null && spawnData.ServerSpawnTime > 0d)
        {
            float elapsed = (float)(NetworkManager.Singleton.ServerTime.Time - spawnData.ServerSpawnTime);
            if (elapsed > 0f)
            {
                fire.Lifetime = Mathf.Max(0.01f, fire.Lifetime - elapsed);
            }
        }

        sourceWeapon.SpawnNetworkProjectile(
            projectilePrefab,
            fire,
            ResolveProjectileTargetTag(fire.TargetFaction),
            fire.TargetFaction,
            cosmeticOnly: true,
            networkAuthority: null,
            playMuzzleEffect: true);

        ResolveFireSound(fire.VisualType)?.PlayAtPoint(transform.position);
    }

    [ServerRpc]
    private void SubmitBeamStateServerRpc(NetBeamState3D state, ServerRpcParams rpcParams = default)
    {
        HandleBeamStateServer(state);
    }

    private void HandleBeamStateServer(NetBeamState3D state)
    {
        IBeamWeaponNetwork3D beam = GetComponent<IBeamWeaponNetwork3D>();
        if (beam == null)
        {
            return;
        }

        if (state.IsFiring)
        {
            beam.ApplyNetworkBeamAim(state.AimDirection);
        }
        beam.ApplyNetworkBeamState(state.IsFiring, authoritative: true, state.Tick);
        BroadcastBeamStateClientRpc(state);
    }

    [ClientRpc]
    private void BroadcastBeamStateClientRpc(NetBeamState3D state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        IBeamWeaponNetwork3D beam = GetComponent<IBeamWeaponNetwork3D>();
        if (beam == null)
        {
            return;
        }

        if (state.IsFiring)
        {
            beam.ApplyNetworkBeamAim(state.AimDirection);
        }
        beam.ApplyNetworkBeamState(state.IsFiring, authoritative: false, PlayerCombatStats3D.InvalidAttackId);
    }

    [ServerRpc]
    private void SubmitBeamAimServerRpc(NetAimUpdate3D update, ServerRpcParams rpcParams = default)
    {
        HandleBeamAimServer(update);
    }

    private void HandleBeamAimServer(NetAimUpdate3D update)
    {
        GetComponent<IBeamWeaponNetwork3D>()?.ApplyNetworkBeamAim(update.AimDirection);
        BroadcastBeamAimClientRpc(update);
    }

    [ClientRpc]
    private void BroadcastBeamAimClientRpc(NetAimUpdate3D update)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GetComponent<IBeamWeaponNetwork3D>()?.ApplyNetworkBeamAim(update.AimDirection);
    }

    [ServerRpc]
    private void SubmitTractorBeamAimServerRpc(NetAimUpdate3D update, ServerRpcParams rpcParams = default)
    {
        HandleTractorBeamAimServer(update);
    }

    private void HandleTractorBeamAimServer(NetAimUpdate3D update)
    {
        GetComponent<TractorBeam3D>()?.ApplyNetworkTractorBeamAim(update.AimDirection);
        BroadcastTractorBeamAimClientRpc(update);
    }

    [ClientRpc]
    private void BroadcastTractorBeamAimClientRpc(NetAimUpdate3D update)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GetComponent<TractorBeam3D>()?.ApplyNetworkTractorBeamAim(update.AimDirection);
    }

    [ServerRpc]
    private void SubmitTeleportServerRpc(NetTeleportState3D state, ServerRpcParams rpcParams = default)
    {
        HandleTeleportServer(state);
    }

    private void HandleTeleportServer(NetTeleportState3D state)
    {
        Teleport3D teleport = GetComponent<Teleport3D>();
        if (teleport == null)
        {
            return;
        }

        if (ArenaBoundary3D.TryGetActive(out ArenaBoundary3D boundary) && boundary.BlocksMovement)
        {
            float radius = _movement != null ? _movement.GetCollisionRadius() : 0f;
            state.TargetPosition = boundary.ClampPositionInside(state.TargetPosition, radius);
        }

        teleport.ApplyNetworkTeleport(state.TargetPosition, authoritative: true);
        BroadcastTeleportClientRpc(state);
    }

    [ClientRpc]
    private void BroadcastTeleportClientRpc(NetTeleportState3D state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GetComponent<Teleport3D>()?.ApplyNetworkTeleport(state.TargetPosition, authoritative: false);
    }

    private void RequestAbilityToggle(NetAbilityKind3D abilityKind, bool isActive, Vector3 aimDirection, bool allowServerAuthority = false)
    {
        if (!NetTickUtil.IsActive || !IsSpawned)
        {
            return;
        }

        bool canRequest = IsOwner || (allowServerAuthority && IsServer);
        if (!canRequest)
        {
            return;
        }

        NetAbilityToggleState3D state = new NetAbilityToggleState3D
        {
            Tick = NetTickUtil.CurrentTick,
            IsActive = isActive,
            AimDirection = aimDirection
        };

        if (IsServer)
        {
            HandleAbilityToggleServer(abilityKind, state);
            return;
        }

        SubmitAbilityToggleServerRpc(abilityKind, state);
    }

    [ServerRpc]
    private void SubmitAbilityToggleServerRpc(NetAbilityKind3D abilityKind, NetAbilityToggleState3D state, ServerRpcParams rpcParams = default)
    {
        HandleAbilityToggleServer(abilityKind, state);
    }

    private void HandleAbilityToggleServer(NetAbilityKind3D abilityKind, NetAbilityToggleState3D state)
    {
        switch (abilityKind)
        {
            case NetAbilityKind3D.Reflect:
                GetComponent<Reflector3D>()?.ApplyNetworkReflectActivation(authoritative: true);
                break;
            case NetAbilityKind3D.Class2Shield:
                GetComponent<Class2Shield3D>()?.ApplyNetworkShieldActivation(authoritative: true);
                break;
            case NetAbilityKind3D.TractorBeam:
            {
                TractorBeam3D tractorBeam = GetComponent<TractorBeam3D>();
                if (tractorBeam != null)
                {
                    if (state.IsActive)
                    {
                        tractorBeam.ApplyNetworkTractorBeamAim(state.AimDirection);
                    }
                    tractorBeam.ApplyNetworkTractorBeamState(state.IsActive, authoritative: true);
                }
                break;
            }
            case NetAbilityKind3D.Class4Empower:
                GetComponent<Empower3D>()?.ApplyNetworkEmpowerState(state.IsActive, authoritative: true);
                break;
            case NetAbilityKind3D.Class4Dodge:
                GetComponent<Dodge3D>()?.PlayNetworkDodgePresentation(state.AimDirection);
                break;
        }

        BroadcastAbilityToggleClientRpc(abilityKind, state);
    }

    [ClientRpc]
    private void BroadcastAbilityToggleClientRpc(NetAbilityKind3D abilityKind, NetAbilityToggleState3D state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        switch (abilityKind)
        {
            case NetAbilityKind3D.Reflect:
                GetComponent<Reflector3D>()?.ApplyNetworkReflectActivation(authoritative: false);
                break;
            case NetAbilityKind3D.Class2Shield:
                GetComponent<Class2Shield3D>()?.ApplyNetworkShieldActivation(authoritative: false);
                break;
            case NetAbilityKind3D.TractorBeam:
            {
                TractorBeam3D tractorBeam = GetComponent<TractorBeam3D>();
                if (tractorBeam != null)
                {
                    if (state.IsActive)
                    {
                        tractorBeam.ApplyNetworkTractorBeamAim(state.AimDirection);
                    }
                    tractorBeam.ApplyNetworkTractorBeamState(state.IsActive, authoritative: false);
                }
                break;
            }
            case NetAbilityKind3D.Class4Empower:
                GetComponent<Empower3D>()?.ApplyNetworkEmpowerState(state.IsActive, authoritative: false);
                break;
            case NetAbilityKind3D.Class4Dodge:
                GetComponent<Dodge3D>()?.PlayNetworkDodgePresentation(state.AimDirection);
                break;
        }
    }

    [ServerRpc]
    private void SubmitGigaBlastChargeServerRpc(NetGigaBlastChargeState3D state, ServerRpcParams rpcParams = default)
    {
        HandleGigaBlastChargeServer(state);
    }

    private void HandleGigaBlastChargeServer(NetGigaBlastChargeState3D state)
    {
        if (!IsOwner)
        {
            GetComponent<GigaBlastWeapon3D>()?.ApplyNetworkChargeState(state.IsCharging, state.Tier);
        }

        BroadcastGigaBlastChargeClientRpc(state);
    }

    [ClientRpc]
    private void BroadcastGigaBlastChargeClientRpc(NetGigaBlastChargeState3D state)
    {
        if (IsServer || IsOwner)
        {
            return;
        }

        GetComponent<GigaBlastWeapon3D>()?.ApplyNetworkChargeState(state.IsCharging, state.Tier);
    }

    [ClientRpc]
    private void BroadcastCombatStateClientRpc(NetCombatState3D state)
    {
        if (_entity == null)
        {
            CacheReferences();
        }

        _entity?.ApplyNetworkCombatState(state);
    }

    [ClientRpc]
    private void BroadcastDeathClientRpc(Vector3 position, Quaternion rotation, Vector3 lastDamageDirection)
    {
        if (IsServer)
        {
            return;
        }

        _entity?.PlayNetworkDeath(position, rotation, lastDamageDirection);
    }

    [ClientRpc]
    private void BroadcastReflectedProjectileClientRpc(NetReflectedProjectileData3D data)
    {
        if (IsServer)
        {
            return;
        }

        Weapon3D sourceWeapon = ResolveWeaponForVisualType(data.VisualType);
        GameObject projectilePrefab = ResolveProjectileVisualPrefab(data.VisualType);
        if (sourceWeapon == null || projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedReflectedMissingProjectileBinding, $"[NetCombat3D] Client received reflected projectile RPC for {data.VisualType}, but this proxy could not resolve a source weapon or projectile prefab.");
            return;
        }

        NetProjectileFireRequest3D fire = new NetProjectileFireRequest3D
        {
            Tick = NetTickUtil.CurrentTick,
            SpawnPosition = data.SpawnPosition,
            SpawnRotation = Quaternion.LookRotation(data.Direction, Vector3.up),
            Direction = data.Direction,
            Speed = data.Speed,
            Damage = data.Damage,
            Lifetime = data.Lifetime,
            ImpactForce = data.ImpactForce,
            TargetFaction = data.TargetFaction,
            VisualType = data.VisualType,
            ProjectileScaleMultiplier = data.ProjectileScaleMultiplier,
            AccuracyAttackId = PlayerCombatStats3D.InvalidAttackId
        };

        sourceWeapon.SpawnNetworkProjectile(projectilePrefab, fire, ResolveProjectileTargetTag(data.TargetFaction), data.TargetFaction, cosmeticOnly: true, networkAuthority: null, playMuzzleEffect: false);
    }

    private bool AcceptProjectileTick(NetProjectileVisualType3D visualType, int tick)
    {
        int index = Mathf.Clamp((int)visualType, 0, _lastAcceptedProjectileTick.Length - 1);
        if (tick < _lastAcceptedProjectileTick[index])
        {
            return false;
        }

        if (tick > _lastAcceptedProjectileTick[index])
        {
            _lastAcceptedProjectileTick[index] = tick;
        }

        return true;
    }

    private NetProjectileVisualType3D ResolveProjectileVisualType(Weapon3D sourceWeapon, GameObject projectilePrefab)
    {
        if (projectilePrefab == null)
        {
            return NetProjectileVisualType3D.Primary;
        }

        if (sourceWeapon is GuidedMissileWeapon3D guidedMissile)
        {
            return guidedMissile.ResolveVisualTypeForProjectile(projectilePrefab);
        }

        ProjectileWeapon3D primary = _entity != null ? _entity.PrimaryWeapon : null;
        if (primary != null && primary.WeaponConfig.projectilePrefab == projectilePrefab)
        {
            return NetProjectileVisualType3D.Primary;
        }

        GigaBlastWeapon3D gigaBlast = GetComponent<GigaBlastWeapon3D>();
        if (gigaBlast != null)
        {
            for (int tier = 1; tier <= 4; tier++)
            {
                if (gigaBlast.GetNetworkProjectilePrefab(tier) == projectilePrefab)
                {
                    return (NetProjectileVisualType3D)tier;
                }
            }
        }

        EmpoweredShot3D empoweredShot = GetComponent<EmpoweredShot3D>();
        if (empoweredShot != null && empoweredShot.NetworkProjectilePrefab == projectilePrefab)
        {
            return NetProjectileVisualType3D.Class2EmpoweredShot;
        }

        PhysicalProjectileAbility3D physicalProjectile = GetComponent<PhysicalProjectileAbility3D>();
        if (physicalProjectile != null && physicalProjectile.NetworkProjectilePrefab == projectilePrefab)
        {
            return NetProjectileVisualType3D.Class2PhysicalProjectile;
        }

        return NetProjectileVisualType3D.Primary;
    }

    private GameObject ResolveProjectileVisualPrefab(NetProjectileVisualType3D visualType)
    {
        return visualType switch
        {
            NetProjectileVisualType3D.Primary => ResolvePrimaryWeapon()?.WeaponConfig.projectilePrefab,
            NetProjectileVisualType3D.GigaBlastTier1 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkProjectilePrefab(1),
            NetProjectileVisualType3D.GigaBlastTier2 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkProjectilePrefab(2),
            NetProjectileVisualType3D.GigaBlastTier3 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkProjectilePrefab(3),
            NetProjectileVisualType3D.GigaBlastTier4 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkProjectilePrefab(4),
            NetProjectileVisualType3D.Class2EmpoweredShot => GetComponent<EmpoweredShot3D>()?.NetworkProjectilePrefab,
            NetProjectileVisualType3D.Class2PhysicalProjectile => GetComponent<PhysicalProjectileAbility3D>()?.NetworkProjectilePrefab,
            NetProjectileVisualType3D.Class4GuidedMissile => GetComponent<GuidedMissileWeapon3D>()?.RegularProjectilePrefab,
            NetProjectileVisualType3D.Class4GuidedMissileEmpowered => GetComponent<GuidedMissileWeapon3D>()?.EmpoweredProjectilePrefab,
            _ => null
        };
    }

    private Weapon3D ResolveWeaponForVisualType(NetProjectileVisualType3D visualType)
    {
        return visualType switch
        {
            NetProjectileVisualType3D.Class2EmpoweredShot => GetComponent<EmpoweredShot3D>(),
            NetProjectileVisualType3D.Class2PhysicalProjectile => GetComponent<PhysicalProjectileAbility3D>(),
            NetProjectileVisualType3D.Class4GuidedMissile => GetComponent<GuidedMissileWeapon3D>(),
            NetProjectileVisualType3D.Class4GuidedMissileEmpowered => GetComponent<GuidedMissileWeapon3D>(),
            _ => ResolvePrimaryWeapon()
        };
    }

    private SoundEffect ResolveFireSound(NetProjectileVisualType3D visualType)
    {
        return visualType switch
        {
            NetProjectileVisualType3D.Primary => ResolvePrimaryWeapon()?.NetworkFireSound,
            NetProjectileVisualType3D.GigaBlastTier1 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkFireSound(1),
            NetProjectileVisualType3D.GigaBlastTier2 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkFireSound(2),
            NetProjectileVisualType3D.GigaBlastTier3 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkFireSound(3),
            NetProjectileVisualType3D.GigaBlastTier4 => GetComponent<GigaBlastWeapon3D>()?.GetNetworkFireSound(4),
            NetProjectileVisualType3D.Class2EmpoweredShot => GetComponent<EmpoweredShot3D>()?.NetworkFireSound,
            NetProjectileVisualType3D.Class2PhysicalProjectile => GetComponent<PhysicalProjectileAbility3D>()?.NetworkFireSound,
            NetProjectileVisualType3D.Class4GuidedMissile => GetComponent<GuidedMissileWeapon3D>()?.NetworkFireSound,
            NetProjectileVisualType3D.Class4GuidedMissileEmpowered => GetComponent<GuidedMissileWeapon3D>()?.NetworkFireSound,
            _ => null
        };
    }

    public string GetEnemyTag()
    {
        return ResolveEnemyTag();
    }

    private string ResolveEnemyTag()
    {
        byte slot = _movement != null ? _movement.PlayerSlot : (byte)0;
        return slot switch
        {
            1 => "Player2",
            2 => "Player1",
            _ => _entity != null && _entity.CompareTag("Player1") ? "Player2" : "Player1"
        };
    }

    private void ApplyProjectileTargeting(ref ProjectileFireRequest3D request)
    {
        if (request.targetFaction != Faction3D.Neutral)
        {
            request.targetTag = ResolveProjectileTargetTag(request.targetFaction);
            return;
        }

        request.targetTag = ResolveEnemyTag();
    }

    private string ResolveProjectileTargetTag(Faction3D targetFaction)
    {
        return targetFaction == Faction3D.EnemyTeam ? "Enemy" : ResolveEnemyTag();
    }

    private void CacheReferences()
    {
        _entity ??= GetComponent<Entity3D>();
        _movement ??= GetComponent<NetMovement3D>();
    }

    private void LogWarningOnce(ref bool flag, string message)
    {
        if (flag)
        {
            return;
        }

        Debug.LogWarning(message, this);
        flag = true;
    }

    private ProjectileWeapon3D ResolvePrimaryWeapon()
    {
        if (_entity != null && _entity.PrimaryWeapon != null)
        {
            return _entity.PrimaryWeapon;
        }

        return GetComponent<ProjectileWeapon3D>();
    }
}

public enum NetAbilityKind3D : byte
{
    Reflect = 0,
    Class2Shield = 1,
    TractorBeam = 2,
    Class4Empower = 3,
    Class4Dodge = 4
}
