using System.Collections.Generic;
using UnityEngine;

public interface IEnemyProjectileWeapon3D
{
    ProjectileWeaponConfig3D WeaponConfig { get; }
    SoundEffect NetworkFireSound { get; }
    NetProjectileVisualType3D NetworkVisualType { get; }

    bool TryFireAtFaction(Faction3D targetFaction);
    bool TryConsumeFireGate();
    void BuildNetworkProjectileRequests(Faction3D targetFaction, int tick, List<NetProjectileFireRequest3D> output);
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
