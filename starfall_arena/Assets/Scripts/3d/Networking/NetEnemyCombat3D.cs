using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Enemy3D))]
public class NetEnemyCombat3D : NetworkBehaviour
{
    private readonly List<NetProjectileFireRequest3D> _projectileRequests = new List<NetProjectileFireRequest3D>(8);

    private Enemy3D _enemy;
    private NetEnemyMovement3D _movement;
    private bool _loggedMissingWeapon;
    private bool _loggedMissingProjectile;
    private bool _loggedMissingBeamWeapon;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryFireProjectilePattern(ProjectileWeapon3D sourceWeapon, Faction3D targetFaction)
    {
        if (!IsServer || !IsSpawned)
        {
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingWeapon, "[NetEnemyCombat3D] Enemy projectile fire was ignored because no ProjectileWeapon3D source was supplied.");
            return false;
        }

        ProjectileWeaponConfig3D config = sourceWeapon.WeaponConfig;
        if (config.projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedMissingProjectile, $"[NetEnemyCombat3D] Enemy projectile fire from {sourceWeapon.name} was ignored because its projectile prefab is missing.");
            return false;
        }

        if (!sourceWeapon.TryConsumeFireGate())
        {
            return false;
        }

        ProjectileFireRequest3D request = new ProjectileFireRequest3D
        {
            projectilePrefab = config.projectilePrefab,
            muzzles = config.muzzles,
            targetTag = string.Empty,
            targetFaction = targetFaction,
            speed = config.speed,
            damage = config.damage,
            lifetime = config.lifetime,
            impactForce = config.impactForce,
            recoilForce = config.recoilForce
        };

        _projectileRequests.Clear();
        sourceWeapon.BuildNetworkProjectileRequests(request, config, NetProjectileVisualType3D.Primary, NetTickUtil.CurrentTick, _projectileRequests);
        if (_projectileRequests.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _projectileRequests.Count; i++)
        {
            SpawnAuthoritativeProjectile(sourceWeapon, config.projectilePrefab, _projectileRequests[i], targetFaction);
        }

        sourceWeapon.NetworkFireSound?.PlayAtPoint(transform.position);
        return true;
    }

    public bool SetBeamState(BeamWeapon3D sourceWeapon, bool isFiring, Vector3 aimDirection)
    {
        if (!IsServer || !IsSpawned)
        {
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Enemy beam update was ignored because no BeamWeapon3D source was supplied.");
            return false;
        }

        if (isFiring && aimDirection.sqrMagnitude > 0.0001f)
        {
            sourceWeapon.ApplyNetworkBeamAim(aimDirection.normalized);
        }

        sourceWeapon.ApplyNetworkBeamState(isFiring, authoritative: true, NetTickUtil.CurrentTick);
        BroadcastEnemyBeamStateClientRpc(new NetBeamState3D
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring,
            AimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector3.zero
        });
        return true;
    }

    public bool UpdateBeamAim(BeamWeapon3D sourceWeapon, Vector3 aimDirection)
    {
        if (!IsServer || !IsSpawned)
        {
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Enemy beam aim update was ignored because no BeamWeapon3D source was supplied.");
            return false;
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 normalizedAim = aimDirection.normalized;
        sourceWeapon.ApplyNetworkBeamAim(normalizedAim);
        BroadcastEnemyBeamAimClientRpc(new NetAimUpdate3D
        {
            Tick = NetTickUtil.CurrentTick,
            AimDirection = normalizedAim
        });
        return true;
    }

    private void SpawnAuthoritativeProjectile(
        ProjectileWeapon3D sourceWeapon,
        GameObject projectilePrefab,
        NetProjectileFireRequest3D fireRequest,
        Faction3D targetFaction)
    {
        sourceWeapon.SpawnNetworkProjectile(
            projectilePrefab,
            fireRequest,
            string.Empty,
            targetFaction,
            cosmeticOnly: false,
            networkAuthority: null,
            playMuzzleEffect: true,
            serverAuthoritativeGameplay: true);

        if (fireRequest.ApplyRecoil)
        {
            _movement?.ApplyCombatVelocityDelta(-fireRequest.Direction.normalized * fireRequest.RecoilForce);
        }

        BroadcastEnemyProjectileClientRpc(new NetProjectileSpawnData3D
        {
            Fire = fireRequest,
            ServerSpawnTime = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d
        });
    }

    [ClientRpc]
    private void BroadcastEnemyProjectileClientRpc(NetProjectileSpawnData3D spawnData)
    {
        if (IsServer)
        {
            return;
        }

        CacheReferences();
        ProjectileWeapon3D sourceWeapon = _enemy != null ? _enemy.PrimaryWeapon : GetComponent<ProjectileWeapon3D>();
        GameObject projectilePrefab = sourceWeapon != null ? sourceWeapon.WeaponConfig.projectilePrefab : null;
        if (sourceWeapon == null || projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedMissingProjectile, "[NetEnemyCombat3D] Client received an enemy projectile cosmetic RPC, but the enemy proxy could not resolve a primary projectile weapon/prefab.");
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
            string.Empty,
            fire.TargetFaction,
            cosmeticOnly: true,
            networkAuthority: null,
            playMuzzleEffect: true);

        sourceWeapon.NetworkFireSound?.PlayAtPoint(transform.position);
    }

    [ClientRpc]
    private void BroadcastEnemyBeamStateClientRpc(NetBeamState3D state)
    {
        if (IsServer)
        {
            return;
        }

        if (!TryResolveBeamWeapon(out BeamWeapon3D beamWeapon))
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Client received an enemy beam RPC, but the enemy proxy could not resolve a BeamWeapon3D.");
            return;
        }

        if (state.IsFiring && state.AimDirection.sqrMagnitude > 0.0001f)
        {
            beamWeapon.ApplyNetworkBeamAim(state.AimDirection);
        }

        beamWeapon.ApplyNetworkBeamState(state.IsFiring, authoritative: false, PlayerCombatStats3D.InvalidAttackId);
    }

    [ClientRpc]
    private void BroadcastEnemyBeamAimClientRpc(NetAimUpdate3D update)
    {
        if (IsServer)
        {
            return;
        }

        if (!TryResolveBeamWeapon(out BeamWeapon3D beamWeapon))
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Client received an enemy beam aim RPC, but the enemy proxy could not resolve a BeamWeapon3D.");
            return;
        }

        beamWeapon.ApplyNetworkBeamAim(update.AimDirection);
    }

    private void CacheReferences()
    {
        _enemy ??= GetComponent<Enemy3D>();
        _movement ??= GetComponent<NetEnemyMovement3D>();
    }

    private bool TryResolveBeamWeapon(out BeamWeapon3D beamWeapon)
    {
        beamWeapon = GetComponent<BeamWeapon3D>();
        return beamWeapon != null;
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
}
