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
    private IEnemyProjectileWeapon3D[] _projectileWeapons;
    private BeamWeapon3D[] _beamWeapons;
    private TargetAwarenessAttackReporter3D _attackReporter;
    private bool _loggedMissingWeapon;
    private bool _loggedMissingProjectile;
    private bool _loggedMissingBeamWeapon;
    private bool _loggedMissingFlamethrowerWeapon;

    private void Awake()
    {
        CacheReferences();
    }

    public bool TryFireProjectilePattern(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction)
    {
        return TryFireProjectilePattern(sourceWeapon, targetFaction, Vector3.zero, null);
    }

    public bool TryFireProjectilePattern(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 fireDirectionOverride)
    {
        return TryFireProjectilePattern(sourceWeapon, targetFaction, fireDirectionOverride, null);
    }

    public bool TryFireProjectilePattern(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 fireDirectionOverride, Entity3D intendedTarget)
    {
        return TryFireProjectilePatternInternal(sourceWeapon, targetFaction, fireDirectionOverride, useConvergencePoint: false, convergencePoint: Vector3.zero, intendedTarget);
    }

    public bool TryFireProjectilePatternConverged(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 convergencePoint)
    {
        return TryFireProjectilePatternConverged(sourceWeapon, targetFaction, convergencePoint, null);
    }

    public bool TryFireProjectilePatternConverged(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 convergencePoint, Entity3D intendedTarget)
    {
        return TryFireProjectilePatternInternal(sourceWeapon, targetFaction, Vector3.zero, useConvergencePoint: true, convergencePoint, intendedTarget);
    }

    private bool TryFireProjectilePatternInternal(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 fireDirectionOverride, bool useConvergencePoint, Vector3 convergencePoint)
    {
        return TryFireProjectilePatternInternal(sourceWeapon, targetFaction, fireDirectionOverride, useConvergencePoint, convergencePoint, null);
    }

    private bool TryFireProjectilePatternInternal(IEnemyProjectileWeapon3D sourceWeapon, Faction3D targetFaction, Vector3 fireDirectionOverride, bool useConvergencePoint, Vector3 convergencePoint, Entity3D intendedTarget)
    {
        if (!IsServer || !IsSpawned)
        {
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingWeapon, "[NetEnemyCombat3D] Enemy projectile fire was ignored because no enemy projectile weapon source was supplied.");
            return false;
        }

        ProjectileWeaponConfig3D config = sourceWeapon.WeaponConfig;
        if (config.projectilePrefab == null)
        {
            LogWarningOnce(ref _loggedMissingProjectile, $"[NetEnemyCombat3D] Enemy projectile fire from {sourceWeapon.GetType().Name} was ignored because its projectile prefab is missing.");
            return false;
        }

        if (!sourceWeapon.TryConsumeFireGate())
        {
            return false;
        }

        _projectileRequests.Clear();
        if (useConvergencePoint)
        {
            sourceWeapon.BuildNetworkProjectileRequestsConverged(targetFaction, NetTickUtil.CurrentTick, _projectileRequests, convergencePoint);
        }
        else
        {
            sourceWeapon.BuildNetworkProjectileRequests(targetFaction, NetTickUtil.CurrentTick, _projectileRequests, fireDirectionOverride);
        }

        if (_projectileRequests.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < _projectileRequests.Count; i++)
        {
            SpawnAuthoritativeProjectile(sourceWeapon, _projectileRequests[i], targetFaction, intendedTarget);
        }

        _enemy?.RecordCombatActivity();
        ReportAttack(intendedTarget);
        sourceWeapon.NetworkFireSound?.PlayAtPoint(transform.position);
        return true;
    }

    public bool SetBeamState(BeamWeapon3D sourceWeapon, bool isFiring, Vector3 aimDirection)
    {
        return SetBeamState(sourceWeapon, isFiring, aimDirection, null);
    }

    public bool SetBeamState(BeamWeapon3D sourceWeapon, bool isFiring, Vector3 aimDirection, Entity3D intendedTarget)
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

        int beamIndex = ResolveBeamWeaponIndex(sourceWeapon);
        if (beamIndex < 0)
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Enemy beam update was ignored because the supplied BeamWeapon3D is not registered on this enemy.");
            return false;
        }

        if (isFiring && aimDirection.sqrMagnitude > 0.0001f)
        {
            sourceWeapon.ApplyNetworkBeamAim(aimDirection.normalized);
        }

        sourceWeapon.ApplyNetworkBeamState(isFiring, authoritative: true, NetTickUtil.CurrentTick);
        _enemy?.RecordCombatActivity();
        ReportSustainedAttack(isFiring, intendedTarget);
        BroadcastEnemyBeamStateClientRpc(new NetBeamState3D
        {
            Tick = NetTickUtil.CurrentTick,
            IsFiring = isFiring,
            AimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector3.zero,
            BeamIndex = beamIndex,
            IntendedTargetNetworkObjectId = ResolveNetworkObjectId(intendedTarget)
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

        int beamIndex = ResolveBeamWeaponIndex(sourceWeapon);
        if (beamIndex < 0)
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Enemy beam aim update was ignored because the supplied BeamWeapon3D is not registered on this enemy.");
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
            AimDirection = normalizedAim,
            BeamIndex = beamIndex
        });
        return true;
    }

    public bool SetFlamethrowerState(EnemyFlamethrowerWeapon3D sourceWeapon, bool isFiring)
    {
        return SetFlamethrowerState(sourceWeapon, isFiring, null);
    }

    public bool SetFlamethrowerState(EnemyFlamethrowerWeapon3D sourceWeapon, bool isFiring, Entity3D intendedTarget)
    {
        if (!IsServer || !IsSpawned)
        {
            return false;
        }

        if (sourceWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingFlamethrowerWeapon, "[NetEnemyCombat3D] Enemy flamethrower update was ignored because no EnemyFlamethrowerWeapon3D source was supplied.");
            return false;
        }

        sourceWeapon.ApplyNetworkFlameState(isFiring, authoritativeDamage: true);
        _enemy?.RecordCombatActivity();
        ReportSustainedAttack(isFiring, intendedTarget);
        BroadcastEnemyFlamethrowerStateClientRpc(isFiring, ResolveNetworkObjectId(intendedTarget));
        return true;
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

    private void SpawnAuthoritativeProjectile(
        IEnemyProjectileWeapon3D sourceWeapon,
        NetProjectileFireRequest3D fireRequest,
        Faction3D targetFaction,
        Entity3D intendedTarget)
    {
        sourceWeapon.SpawnNetworkProjectile(
            fireRequest,
            string.Empty,
            targetFaction,
            cosmeticOnly: false,
            playMuzzleEffect: true,
            serverAuthoritativeGameplay: true);

        if (fireRequest.ApplyRecoil)
        {
            _movement?.ApplyCombatVelocityDelta(-fireRequest.Direction.normalized * fireRequest.RecoilForce);
        }

        BroadcastEnemyProjectileClientRpc(new NetProjectileSpawnData3D
        {
            Fire = fireRequest,
            ServerSpawnTime = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d,
            IntendedTargetNetworkObjectId = ResolveNetworkObjectId(intendedTarget)
        });
    }

    [ClientRpc]
    private void BroadcastCombatStateClientRpc(NetCombatState3D state)
    {
        if (IsServer)
        {
            return;
        }

        CacheReferences();
        _enemy?.ApplyNetworkCombatState(state);
    }

    [ClientRpc]
    private void BroadcastDeathClientRpc(Vector3 position, Quaternion rotation, Vector3 lastDamageDirection)
    {
        if (IsServer)
        {
            return;
        }

        CacheReferences();
        _enemy?.PlayNetworkDeath(position, rotation, lastDamageDirection);
    }

    [ClientRpc]
    private void BroadcastEnemyProjectileClientRpc(NetProjectileSpawnData3D spawnData)
    {
        if (IsServer)
        {
            return;
        }

        CacheReferences();
        IEnemyProjectileWeapon3D sourceWeapon = ResolveProjectileWeapon(spawnData.Fire.VisualType);
        if (sourceWeapon == null || sourceWeapon.GetProjectilePrefab() == null)
        {
            LogWarningOnce(ref _loggedMissingProjectile, $"[NetEnemyCombat3D] Client received an enemy projectile cosmetic RPC for {spawnData.Fire.VisualType}, but the enemy proxy could not resolve the matching enemy weapon/prefab.");
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
            fire,
            string.Empty,
            fire.TargetFaction,
            cosmeticOnly: true,
            playMuzzleEffect: true,
            serverAuthoritativeGameplay: false);

        sourceWeapon.NetworkFireSound?.PlayAtPoint(transform.position);
        ReportAttack(ResolveNetworkTarget(spawnData.IntendedTargetNetworkObjectId));
    }

    [ClientRpc]
    private void BroadcastEnemyBeamStateClientRpc(NetBeamState3D state)
    {
        if (IsServer)
        {
            return;
        }

        if (!TryResolveBeamWeapon(state.BeamIndex, out BeamWeapon3D beamWeapon))
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Client received an enemy beam RPC, but the enemy proxy could not resolve a BeamWeapon3D.");
            return;
        }

        if (state.IsFiring && state.AimDirection.sqrMagnitude > 0.0001f)
        {
            beamWeapon.ApplyNetworkBeamAim(state.AimDirection);
        }

        beamWeapon.ApplyNetworkBeamState(state.IsFiring, authoritative: false, PlayerCombatStats3D.InvalidAttackId);
        ReportSustainedAttack(state.IsFiring, ResolveNetworkTarget(state.IntendedTargetNetworkObjectId));
    }

    [ClientRpc]
    private void BroadcastEnemyBeamAimClientRpc(NetAimUpdate3D update)
    {
        if (IsServer)
        {
            return;
        }

        if (!TryResolveBeamWeapon(update.BeamIndex, out BeamWeapon3D beamWeapon))
        {
            LogWarningOnce(ref _loggedMissingBeamWeapon, "[NetEnemyCombat3D] Client received an enemy beam aim RPC, but the enemy proxy could not resolve a BeamWeapon3D.");
            return;
        }

        beamWeapon.ApplyNetworkBeamAim(update.AimDirection);
    }

    [ClientRpc]
    private void BroadcastEnemyFlamethrowerStateClientRpc(bool isFiring, ulong intendedTargetNetworkObjectId)
    {
        if (IsServer)
        {
            return;
        }

        EnemyFlamethrowerWeapon3D flamethrowerWeapon = GetComponent<EnemyFlamethrowerWeapon3D>();
        if (flamethrowerWeapon == null)
        {
            LogWarningOnce(ref _loggedMissingFlamethrowerWeapon, "[NetEnemyCombat3D] Client received an enemy flamethrower RPC, but the enemy proxy could not resolve an EnemyFlamethrowerWeapon3D.");
            return;
        }

        flamethrowerWeapon.ApplyNetworkFlameState(isFiring, authoritativeDamage: false);
        ReportSustainedAttack(isFiring, ResolveNetworkTarget(intendedTargetNetworkObjectId));
    }

    private void CacheReferences()
    {
        _enemy ??= GetComponent<Enemy3D>();
        _movement ??= GetComponent<NetEnemyMovement3D>();
        _attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>();
        CacheProjectileWeapons();
        CacheBeamWeapons();
    }

    private void ReportAttack(Entity3D intendedTarget)
    {
        if (intendedTarget == null)
        {
            return;
        }

        _attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        _attackReporter.ReportAttack(intendedTarget);
    }

    private void ReportSustainedAttack(bool isFiring, Entity3D intendedTarget)
    {
        _attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        if (isFiring)
        {
            _attackReporter.ReportSustainedAttack(intendedTarget, 0.25f);
        }
        else
        {
            _attackReporter.StopSustainedAttack(intendedTarget);
        }
    }

    private static ulong ResolveNetworkObjectId(Entity3D target)
    {
        if (target == null || !target.TryGetComponent(out NetworkObject networkObject) || !networkObject.IsSpawned)
        {
            return 0UL;
        }

        return networkObject.NetworkObjectId;
    }

    private static Entity3D ResolveNetworkTarget(ulong networkObjectId)
    {
        if (networkObjectId == 0UL || NetworkManager.Singleton == null)
        {
            return null;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject)
            || networkObject == null)
        {
            return null;
        }

        return networkObject.GetComponent<Entity3D>();
    }

    private bool TryResolveBeamWeapon(int beamIndex, out BeamWeapon3D beamWeapon)
    {
        CacheBeamWeapons();
        beamWeapon = null;
        if (_beamWeapons == null || _beamWeapons.Length == 0)
        {
            return false;
        }

        int resolvedIndex = Mathf.Clamp(beamIndex, 0, _beamWeapons.Length - 1);
        beamWeapon = _beamWeapons[resolvedIndex];
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

    private void CacheProjectileWeapons()
    {
        if (_projectileWeapons != null && _projectileWeapons.Length > 0)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        int count = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyProjectileWeapon3D)
            {
                count++;
            }
        }

        _projectileWeapons = new IEnemyProjectileWeapon3D[count];
        int writeIndex = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyProjectileWeapon3D projectileWeapon)
            {
                _projectileWeapons[writeIndex++] = projectileWeapon;
            }
        }
    }

    private void CacheBeamWeapons()
    {
        if (_beamWeapons != null && _beamWeapons.Length > 0)
        {
            return;
        }

        _beamWeapons = GetComponents<BeamWeapon3D>();
    }

    private int ResolveBeamWeaponIndex(BeamWeapon3D sourceWeapon)
    {
        CacheBeamWeapons();
        if (_beamWeapons == null || sourceWeapon == null)
        {
            return -1;
        }

        for (int i = 0; i < _beamWeapons.Length; i++)
        {
            if (_beamWeapons[i] == sourceWeapon)
            {
                return i;
            }
        }

        return -1;
    }

    private IEnemyProjectileWeapon3D ResolveProjectileWeapon(NetProjectileVisualType3D visualType)
    {
        CacheProjectileWeapons();
        if (_projectileWeapons == null)
        {
            return null;
        }

        for (int i = 0; i < _projectileWeapons.Length; i++)
        {
            IEnemyProjectileWeapon3D projectileWeapon = _projectileWeapons[i];
            if (projectileWeapon != null && projectileWeapon.UsesVisualType(visualType))
            {
                return projectileWeapon;
            }
        }

        return null;
    }
}
