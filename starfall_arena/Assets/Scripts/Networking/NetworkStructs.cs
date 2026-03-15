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

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Thrust);
        serializer.SerializeValue(ref LookInput);
        serializer.SerializeValue(ref Anchor);
        serializer.SerializeValue(ref FrictionEnabled);
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
