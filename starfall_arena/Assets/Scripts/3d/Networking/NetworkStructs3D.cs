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
    public bool DodgeRequested;
    public Vector3 DodgeDirection;

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
        serializer.SerializeValue(ref DodgeRequested);
        serializer.SerializeValue(ref DodgeDirection);
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
    public Vector3 DodgeVelocity;
    public Vector3 DodgeExitVelocity;
    public float DodgeRemainingTime;
    public float DodgeDuration;

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
        serializer.SerializeValue(ref DodgeVelocity);
        serializer.SerializeValue(ref DodgeExitVelocity);
        serializer.SerializeValue(ref DodgeRemainingTime);
        serializer.SerializeValue(ref DodgeDuration);
    }
}

public struct MovementState3D
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector2 FilteredLookInput;
    public Vector2 TurnRates;
    public Vector3 DodgeVelocity;
    public Vector3 DodgeExitVelocity;
    public float DodgeRemainingTime;
    public float DodgeDuration;
}

public enum NetProjectileVisualType3D : byte
{
    Primary = 0,
    GigaBlastTier1 = 1,
    GigaBlastTier2 = 2,
    GigaBlastTier3 = 3,
    GigaBlastTier4 = 4,
    Class2EmpoweredShot = 5,
    Class2PhysicalProjectile = 6,
    Class4GuidedMissile = 7,
    Class4GuidedMissileEmpowered = 8,
    EnemyProjectile = 9,
    EnemyMissile = 10,
    EnemySecondaryProjectile = 11,
    EnemyFormationMissile = 12,
}

public struct NetProjectileFireRequest3D : INetworkSerializable
{
    public int Tick;
    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;
    public Vector3 MuzzleEffectPosition;
    public Quaternion MuzzleEffectRotation;
    public Vector3 Direction;
    public Vector3 InheritedVelocity;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public float RecoilForce;
    public bool ApplyRecoil;
    public bool CanPierce;
    public float PierceMultiplier;
    public bool AppliesSlow;
    public float SlowMultiplier;
    public float SlowDuration;
    public float SlowEngineEmissionScale;
    public float ProjectileScaleMultiplier;
    public Faction3D TargetFaction;
    public NetProjectileVisualType3D VisualType;
    public int AccuracyAttackId;
    public bool UsesFormation;
    public int FormationSlotIndex;
    public int FormationSlotCount;
    public float FormationFanArcDegrees;
    public float FormationFanOutDuration;
    public float FormationHoldDuration;
    public float FormationConvergeDuration;
    public float FormationConvergenceRadius;
    public float FormationMaxSpeedMultiplier;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref SpawnRotation);
        serializer.SerializeValue(ref MuzzleEffectPosition);
        serializer.SerializeValue(ref MuzzleEffectRotation);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref InheritedVelocity);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref RecoilForce);
        serializer.SerializeValue(ref ApplyRecoil);
        serializer.SerializeValue(ref CanPierce);
        serializer.SerializeValue(ref PierceMultiplier);
        serializer.SerializeValue(ref AppliesSlow);
        serializer.SerializeValue(ref SlowMultiplier);
        serializer.SerializeValue(ref SlowDuration);
        serializer.SerializeValue(ref SlowEngineEmissionScale);
        serializer.SerializeValue(ref ProjectileScaleMultiplier);
        serializer.SerializeValue(ref TargetFaction);
        serializer.SerializeValue(ref VisualType);
        serializer.SerializeValue(ref AccuracyAttackId);
        serializer.SerializeValue(ref UsesFormation);
        serializer.SerializeValue(ref FormationSlotIndex);
        serializer.SerializeValue(ref FormationSlotCount);
        serializer.SerializeValue(ref FormationFanArcDegrees);
        serializer.SerializeValue(ref FormationFanOutDuration);
        serializer.SerializeValue(ref FormationHoldDuration);
        serializer.SerializeValue(ref FormationConvergeDuration);
        serializer.SerializeValue(ref FormationConvergenceRadius);
        serializer.SerializeValue(ref FormationMaxSpeedMultiplier);
    }
}

public struct NetProjectileSpawnData3D : INetworkSerializable
{
    public NetProjectileFireRequest3D Fire;
    public double ServerSpawnTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Fire);
        serializer.SerializeValue(ref ServerSpawnTime);
    }
}

public struct NetBeamState3D : INetworkSerializable
{
    public int Tick;
    public bool IsFiring;
    public Vector3 AimDirection;
    public int BeamIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsFiring);
        serializer.SerializeValue(ref AimDirection);
        serializer.SerializeValue(ref BeamIndex);
    }
}

public struct NetAimUpdate3D : INetworkSerializable
{
    public int Tick;
    public Vector3 AimDirection;
    public int BeamIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref AimDirection);
        serializer.SerializeValue(ref BeamIndex);
    }
}

public struct NetTeleportState3D : INetworkSerializable
{
    public Vector3 TargetPosition;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TargetPosition);
    }
}

public struct NetAbilityToggleState3D : INetworkSerializable
{
    public int Tick;
    public bool IsActive;
    public Vector3 AimDirection;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Tick);
        serializer.SerializeValue(ref IsActive);
        serializer.SerializeValue(ref AimDirection);
    }
}

public struct NetGigaBlastChargeState3D : INetworkSerializable
{
    public bool IsCharging;
    public int Tier;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsCharging);
        serializer.SerializeValue(ref Tier);
    }
}

public struct NetCombatState3D : INetworkSerializable
{
    public float Health;
    public float Shield;
    public Vector3 HitPoint;
    public int DamageSource;
    public bool ShieldHit;
    public bool ShieldBreak;
    public float SlowMultiplier;
    public float SlowRemainingTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Shield);
        serializer.SerializeValue(ref HitPoint);
        serializer.SerializeValue(ref DamageSource);
        serializer.SerializeValue(ref ShieldHit);
        serializer.SerializeValue(ref ShieldBreak);
        serializer.SerializeValue(ref SlowMultiplier);
        serializer.SerializeValue(ref SlowRemainingTime);
    }
}

public struct NetReflectedProjectileData3D : INetworkSerializable
{
    public Vector3 SpawnPosition;
    public Vector3 Direction;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float ImpactForce;
    public float ProjectileScaleMultiplier;
    public Color ReflectColor;
    public Faction3D TargetFaction;
    public NetProjectileVisualType3D VisualType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpawnPosition);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref ImpactForce);
        serializer.SerializeValue(ref ProjectileScaleMultiplier);
        serializer.SerializeValue(ref ReflectColor);
        serializer.SerializeValue(ref TargetFaction);
        serializer.SerializeValue(ref VisualType);
    }
}
