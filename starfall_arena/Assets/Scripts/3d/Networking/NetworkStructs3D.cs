using Unity.Netcode;
using UnityEngine;

public struct NetInputSnapshot3D : INetworkSerializable
{
    public int Tick;
    public Vector2 LookInput;
    public float ThrustInput;
    public bool FrictionEnabled;
    public float BaseRotationMultiplier;
    public float AbilityRotationMultiplier;
    public float ThrustMultiplier;
    public float SlowMultiplier;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref LookInput);
        serializer.SerializeValue(ref ThrustInput);
        serializer.SerializeValue(ref FrictionEnabled);
        serializer.SerializeValue(ref BaseRotationMultiplier);
        serializer.SerializeValue(ref AbilityRotationMultiplier);
        serializer.SerializeValue(ref ThrustMultiplier);
        serializer.SerializeValue(ref SlowMultiplier);
    }
}

public struct NetStateSnapshot3D : INetworkSerializable
{
    public int Tick;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector2 FilteredLookInput;
    public Vector2 TurnRates;
    public float ThrustInput;
    public bool FrictionEnabled;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Velocity);
        serializer.SerializeValue(ref FilteredLookInput);
        serializer.SerializeValue(ref TurnRates);
        serializer.SerializeValue(ref ThrustInput);
        serializer.SerializeValue(ref FrictionEnabled);
    }
}

public struct MovementState3D
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector2 FilteredLookInput;
    public Vector2 TurnRates;
}
