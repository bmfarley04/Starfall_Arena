using System.Collections.Generic;
using UnityEngine;

public interface IEnemyProjectileWeapon3D
{
    ProjectileWeaponConfig3D WeaponConfig { get; }
    SoundEffect NetworkFireSound { get; }
    NetProjectileVisualType3D NetworkVisualType { get; }

    bool TryFireAtFaction(Faction3D targetFaction);
    bool TryFireAtFaction(Faction3D targetFaction, Vector3 fireDirectionOverride);
    bool TryFireAtFactionConverged(Faction3D targetFaction, Vector3 convergencePoint);
    bool TryConsumeFireGate();
    bool IsFireGateReady { get; }
    void BuildNetworkProjectileRequests(Faction3D targetFaction, int tick, List<NetProjectileFireRequest3D> output);
    void BuildNetworkProjectileRequests(Faction3D targetFaction, int tick, List<NetProjectileFireRequest3D> output, Vector3 fireDirectionOverride);
    void BuildNetworkProjectileRequestsConverged(Faction3D targetFaction, int tick, List<NetProjectileFireRequest3D> output, Vector3 convergencePoint);
    void SpawnNetworkProjectile(
        NetProjectileFireRequest3D fire,
        string targetTag,
        Faction3D targetFaction,
        bool cosmeticOnly,
        bool playMuzzleEffect,
        bool serverAuthoritativeGameplay);
    bool UsesVisualType(NetProjectileVisualType3D visualType);
    GameObject GetProjectilePrefab();
}
