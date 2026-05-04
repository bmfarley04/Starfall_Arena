using System;
using UnityEngine;

public abstract class PlayerBalanceProfile3D : ScriptableObject
{
    [Serializable]
    public struct CoreStats
    {
        [Min(1f)] public float maxHealth;
        [Min(0f)] public float maxShield;
        [Min(0f)] public float shieldRegenDelay;
        [Min(0f)] public float shieldRegenRate;
        public bool anchorEnabled;
        [Min(0f)] public float anchorRotationMultiplier;
        [Min(0f)] public float anchorThrustMultiplier;
    }

    [Serializable]
    public struct FlightStats
    {
        [Min(0f)] public float thrustAcceleration;
        [Min(0.01f)] public float maxSpeed;
        [Min(0.01f)] public float lookInputResponse;
        [Min(0.01f)] public float pitchSpeed;
        [Min(0.01f)] public float yawSpeed;
        [Min(0.01f)] public float pitchAcceleration;
        [Min(0.01f)] public float pitchDeceleration;
        [Min(0.01f)] public float yawAcceleration;
        [Min(0.01f)] public float yawDeceleration;
        [Range(0f, 1f)] public float minRotationMultiplierAtMaxSpeed;
    }

    [Serializable]
    public struct FlightAssistStats
    {
        [Min(0f)] public float frictionDeceleration;
        [Min(0f)] public float activeAngularDamping;
        [Min(0.01f)] public float lateralDriftDamping;
        [Min(0.01f)] public float verticalDriftDamping;
        [Min(0f)] public float velocityAlignmentStrength;
    }

    [Serializable]
    public struct ProjectileWeaponStats
    {
        [Min(0f)] public float cooldown;
        [Min(0f)] public float speed;
        [Min(0f)] public float damage;
        [Min(0f)] public float lifetime;
        [Min(0f)] public float energyCost;
    }

    [Serializable]
    public struct BeamWeaponStats
    {
        [Min(0f)] public float damagePerSecond;
        [Min(0f)] public float maxDistance;
        [Min(0f)] public float capacity;
        [Min(0f)] public float drainRate;
        [Min(0f)] public float regenRate;
        [Min(0f)] public float minimumStartEnergy;
        [Min(0f)] public float rotationMultiplier;
        [Min(0f)] public float postFireRotationPenaltyDuration;
    }

    [Header("Shared Core")]
    [Tooltip("Player health, shield, shield regeneration, and Anchor handling numbers. Visuals, sounds, input, UI, and network references stay on the prefab.")]
    public CoreStats core;

    [Header("Flight")]
    [Tooltip("Main flight speed and handling numbers from ShipFlight3D. Plane locks, input source, Rigidbody setup, and camera references stay on the prefab.")]
    public FlightStats flight;

    [Tooltip("Flight-assist damping and velocity alignment numbers from ShipFlight3D. This does not include the current friction toggle state.")]
    public FlightAssistStats flightAssist;

    [Header("Weapons")]
    [Tooltip("Projectile weapon balance stats, applied by component order to ProjectileWeapon3D components under the player prefab. Projectile prefabs, muzzles, targeting, recoil, impact force, audio, and pooling stay on the prefab.")]
    public ProjectileWeaponStats[] projectileWeapons = Array.Empty<ProjectileWeaponStats>();

    [Tooltip("Beam weapon balance stats, applied by component order to BeamWeapon3D components under the player prefab. Beam prefabs, muzzles, targeting, offsets, recoil, impact force, audio, and pooling stay on the prefab.")]
    public BeamWeaponStats[] beamWeapons = Array.Empty<BeamWeaponStats>();

    public abstract void ApplyClassStats(GameObject prefabRoot);
}
