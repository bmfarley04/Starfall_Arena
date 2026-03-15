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

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Thrust);
        serializer.SerializeValue(ref LookInput);
        serializer.SerializeValue(ref Anchor);
        serializer.SerializeValue(ref FrictionEnabled);
        serializer.SerializeValue(ref VisualBankAngle);
        serializer.SerializeValue(ref VisualPitchAngle);
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

    /// <summary>Anchor drag accumulator (needed for reconciliation replay).</summary>
    public float AnchorDragAccumulator;

    /// <summary>Friction timer (needed for reconciliation replay).</summary>
    public float FrictionTimer;

    /// <summary>Authoritative friction toggle state for debugging and replay context.</summary>
    public bool FrictionEnabled;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref VisualBankAngle);
        serializer.SerializeValue(ref VisualPitchAngle);
        serializer.SerializeValue(ref AnchorDragAccumulator);
        serializer.SerializeValue(ref FrictionTimer);
        serializer.SerializeValue(ref FrictionEnabled);
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

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref DamagePerSecond);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
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

public struct NetAbilityToggleState : INetworkSerializable
{
    public bool IsActive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsActive);
    }
}
