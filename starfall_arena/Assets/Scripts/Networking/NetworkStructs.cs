using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-tick input snapshot sent from owning client to server.
/// Captures everything MovementSimulation.SimulateTick() needs to reproduce a tick.
/// </summary>
public struct NetInputSnapshot : INetworkSerializable
{
    /// <summary>Network tick this input was generated on.</summary>
    public int Tick;

    /// <summary>True when the player is holding thrust.</summary>
    public bool Thrust;

    /// <summary>Stick / mouse look direction (raw, pre-deadzone).</summary>
    public Vector2 LookInput;

    /// <summary>True while the anchor ability is held.</summary>
    public bool Anchor;

    /// <summary>True when space-friction mode is toggled on.</summary>
    public bool FrictionEnabled;

    /// <summary>Current owner-computed visual roll/bank angle for the ship model.</summary>
    public float VisualBankAngle;

    /// <summary>Current owner-computed visual pitch angle for the ship model.</summary>
    public float VisualPitchAngle;

    /// <summary>Rotation speed multiplier from active ability (1 = no modifier).</summary>
    public float AbilityRotationMultiplier;

    /// <summary>Thrust force multiplier from active ability (1 = no modifier).</summary>
    public float AbilityThrustMultiplier;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Thrust);
        serializer.SerializeValue(ref LookInput);
        serializer.SerializeValue(ref Anchor);
        serializer.SerializeValue(ref FrictionEnabled);
        serializer.SerializeValue(ref VisualBankAngle);
        serializer.SerializeValue(ref VisualPitchAngle);
        serializer.SerializeValue(ref AbilityRotationMultiplier);
        serializer.SerializeValue(ref AbilityThrustMultiplier);
    }
}

/// <summary>
/// Authoritative state snapshot broadcast from server to all clients each tick.
/// Used by the owning client for reconciliation and by remote clients for interpolation.
/// </summary>
public struct NetStateSnapshot : INetworkSerializable
{
    /// <summary>Server tick this state was produced on.</summary>
    public int Tick;

    /// <summary>Authoritative world position.</summary>
    public Vector2 Position;

    /// <summary>Authoritative Z-rotation in degrees.</summary>
    public float Rotation;

    /// <summary>Authoritative linear velocity.</summary>
    public Vector2 Velocity;

    /// <summary>Authoritative visual roll/bank angle for the 3D ship model.</summary>
    public float VisualBankAngle;

    /// <summary>Authoritative visual pitch angle for the 3D ship model.</summary>
    public float VisualPitchAngle;

    /// <summary>Authoritative anchor state so anchor-gated augments evaluate consistently.</summary>
    public bool Anchor;

    /// <summary>Anchor drag accumulator (needed for reconciliation replay).</summary>
    public float AnchorDragAccumulator;

    /// <summary>Friction timer (needed for reconciliation replay).</summary>
    public float FrictionTimer;

    /// <summary>Authoritative friction toggle state for debugging and replay context.</summary>
    public bool FrictionEnabled;

    /// <summary>Whether the player is currently thrusting (for remote thruster visuals).</summary>
    public bool Thrust;

    /// <summary>Current shield value (for remote shield regen visuals).</summary>
    public float Shield;

    /// <summary>Current health value (for augment-driven healing and HUD sync).</summary>
    public float Health;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref VisualBankAngle);
        serializer.SerializeValue(ref VisualPitchAngle);
        serializer.SerializeValue(ref Anchor);
        serializer.SerializeValue(ref AnchorDragAccumulator);
        serializer.SerializeValue(ref FrictionTimer);
        serializer.SerializeValue(ref FrictionEnabled);
        serializer.SerializeValue(ref Thrust);
        serializer.SerializeValue(ref Shield);
        serializer.SerializeValue(ref Health);
    }
}

public enum NetProjectileVisualType : byte
{
    Primary = 0,
    GigaBlastTier1 = 1,
    GigaBlastTier2 = 2,
    GigaBlastTier3 = 3,
    GigaBlastTier4 = 4,
    Class2EmpoweredShot = 5,
    Class2PhysicalProjectile = 6,
}

public struct NetFireRequest : INetworkSerializable
{
    public int Tick;
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public Vector2 InheritedVelocity;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public float RecoilForce;
    public bool ApplyRecoil;
    public float PierceMultiplier;
    public float SlowMultiplier;
    public float SlowDuration;
    public bool CanPierce;
    public bool AppliesSlow;
    public NetProjectileVisualType VisualType;
    public bool IgnoreCooldown;
    public bool OwnerPredicted;
    public byte FireSource;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref InheritedVelocity);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref RecoilForce);
        serializer.SerializeValue(ref ApplyRecoil);
        serializer.SerializeValue(ref PierceMultiplier);
        serializer.SerializeValue(ref SlowMultiplier);
        serializer.SerializeValue(ref SlowDuration);
        serializer.SerializeValue(ref CanPierce);
        serializer.SerializeValue(ref AppliesSlow);
        serializer.SerializeValue(ref VisualType);
        serializer.SerializeValue(ref IgnoreCooldown);
        serializer.SerializeValue(ref OwnerPredicted);
        serializer.SerializeValue(ref FireSource);
    }
}

public struct NetProjectileSpawnData : INetworkSerializable
{
    public int Tick;
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public Vector2 InheritedVelocity;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public float RecoilForce;
    public bool ApplyRecoil;
    public float PierceMultiplier;
    public float SlowMultiplier;
    public float SlowDuration;
    public bool CanPierce;
    public bool AppliesSlow;
    public NetProjectileVisualType VisualType;
    public bool OwnerPredicted;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref InheritedVelocity);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref RecoilForce);
        serializer.SerializeValue(ref ApplyRecoil);
        serializer.SerializeValue(ref PierceMultiplier);
        serializer.SerializeValue(ref SlowMultiplier);
        serializer.SerializeValue(ref SlowDuration);
        serializer.SerializeValue(ref CanPierce);
        serializer.SerializeValue(ref AppliesSlow);
        serializer.SerializeValue(ref VisualType);
        serializer.SerializeValue(ref OwnerPredicted);
    }
}

public struct NetBeamState : INetworkSerializable
{
    public int Tick;
    public bool IsFiring;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsFiring);
    }
}

public struct NetConvergeBeamState : INetworkSerializable
{
    public int Tick;
    public bool IsFiring;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsFiring);
    }
}

public struct NetFireTrailState : INetworkSerializable
{
    public int Tick;
    public bool IsActive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsActive);
    }
}

public struct NetFireHazardSpawnData : INetworkSerializable
{
    public Vector2 SpawnPosition;
    public float DamagePerSecond;
    public float Lifetime;
    public float ImpactForce;
    /// <summary>
    /// Server network time when the hazard was spawned, used by clients to
    /// subtract transit latency from the visual lifetime so hazards expire
    /// at the same moment on all machines.
    /// </summary>
    public double ServerSpawnTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref DamagePerSecond);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref ServerSpawnTime);
    }
}

public struct NetTeleportState : INetworkSerializable
{
    public Vector2 TargetPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TargetPosition);
    }
}

public struct NetGuidedMissileState : INetworkSerializable
{
    public int Tick;
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public Vector2 InheritedVelocity;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public float RecoilForce;
    public float SizeMultiplier;
    public bool Empowered;
    public bool ApplyRecoil;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref InheritedVelocity);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref RecoilForce);
        serializer.SerializeValue(ref SizeMultiplier);
        serializer.SerializeValue(ref Empowered);
        serializer.SerializeValue(ref ApplyRecoil);
    }
}

public struct NetDodgeState : INetworkSerializable
{
    public Vector2 Direction;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Direction);
    }
}

public enum NetChronoStepAction : byte
{
    Plant = 0,
    Teleport = 1,
}

public struct NetChronoStepState : INetworkSerializable
{
    public NetChronoStepAction Action;
    public Vector2 Waypoint;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Action);
        serializer.SerializeValue(ref Waypoint);
    }
}

public struct NetClass2ShieldState : INetworkSerializable
{
    public bool IsActive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsActive);
    }
}

public struct NetTractorBeamState : INetworkSerializable
{
    public bool IsActive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsActive);
    }
}

public struct NetTriggerBombLaunchState : INetworkSerializable
{
    public Vector2 SpawnPosition;
    public Vector2 Velocity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Velocity);
    }
}

public struct NetTriggerBombDetonateState : INetworkSerializable
{
    public Vector2 Position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
    }
}

public struct NetDarkMatterCastState : INetworkSerializable
{
    public int ChargesSpent;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ChargesSpent);
    }
}

public struct NetFlameWaveCastState : INetworkSerializable
{
    public int ChargesSpent;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ChargesSpent);
    }
}

public struct NetFlameWaveHazardSpawnData : INetworkSerializable
{
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public float DamagePerSecond;
    public float Lifetime;
    public float ImpactForce;
    public float SlowRate;
    public float LaunchSpeed;
    public bool DisableDampening;
    public double ServerSpawnTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref DamagePerSecond);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref SlowRate);
        serializer.SerializeValue(ref LaunchSpeed);
        serializer.SerializeValue(ref DisableDampening);
        serializer.SerializeValue(ref ServerSpawnTime);
    }
}

public struct NetDarkMatterHazardSpawnData : INetworkSerializable
{
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public float DamagePerSecond;
    public float Lifetime;
    public float ImpactForce;
    public float SlowRate;
    public float LaunchSpeed;
    public bool DisableDampening;
    public double ServerSpawnTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref DamagePerSecond);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref SlowRate);
        serializer.SerializeValue(ref LaunchSpeed);
        serializer.SerializeValue(ref DisableDampening);
        serializer.SerializeValue(ref ServerSpawnTime);
    }
}

public struct NetAbilityToggleState : INetworkSerializable
{
    public bool IsActive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsActive);
    }
}

public struct NetGigaBlastChargeState : INetworkSerializable
{
    public bool IsCharging;
    public int Tier;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsCharging);
        serializer.SerializeValue(ref Tier);
    }
}

public struct NetBatteryRamState : INetworkSerializable
{
    public int Tick;
    public bool IsActive;
    public bool Broken;
    public bool GrantedCharge;
    /// <summary>
    /// When true, the owning client skips this broadcast (used when the owner already predicted locally).
    /// </summary>
    public bool SkipOwner;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsActive);
        serializer.SerializeValue(ref Broken);
        serializer.SerializeValue(ref GrantedCharge);
        serializer.SerializeValue(ref SkipOwner);
    }
}

public struct NetReflectedProjectileData : INetworkSerializable
{
    public Vector2 SpawnPosition;
    public Vector2 Direction;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public Color ReflectColor;
    public NetProjectileVisualType VisualType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref ReflectColor);
        serializer.SerializeValue(ref VisualType);
    }
}
