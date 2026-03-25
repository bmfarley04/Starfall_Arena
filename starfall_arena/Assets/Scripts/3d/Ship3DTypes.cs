using System.Collections.Generic;
using UnityEngine;

public interface IShipFlightInputSource
{
    Vector2 LookInput { get; }
    float ThrustInput { get; }
    bool ConsumeToggleFrictionPressed();
}

[System.Serializable]
public struct ShipFlightConfig3D
{
    [Header("Engine Parameters")]
    public float thrustAcceleration;
    public float maxSpeed;

    [Header("Handling Parameters")]
    public float pitchSpeed;
    public float yawSpeed;
    public bool invertY;
    [Range(0f, 1f)]
    public float minRotationMultiplierAtMaxSpeed;
}

[System.Serializable]
public struct ShipFlightAssistConfig3D
{
    [Header("Flight Assist (Friction)")]
    [Tooltip("How fast velocity bleeds off when not thrusting (units/s^2). Does not affect max speed while thrusting.")]
    public float frictionDeceleration;
    [Tooltip("Angular damping applied to rotation when friction is active.")]
    public float activeAngularDamping;
}

[System.Serializable]
public struct VisualEffects3DConfig
{
    [Header("Visual Model")]
    [Tooltip("Child transform containing the ship mesh. Banking and pitch lean are applied here.")]
    public Transform visualModel;

    [Header("Banking (Roll)")]
    [Tooltip("Maximum roll angle applied to the visual model when yawing.")]
    public float maxBankAngle;
    [Tooltip("How strongly yaw angular velocity drives the bank. Negative values invert the direction.")]
    public float bankSensitivity;
    [Tooltip("Smoothing speed for bank interpolation.")]
    public float bankSmoothing;

    [Header("Pitch Lean")]
    [Tooltip("Maximum additional pitch lean applied to the visual model when pitching.")]
    public float maxPitchLeanAngle;
    [Tooltip("How strongly pitch angular velocity drives the lean. Negative values invert the direction.")]
    public float pitchLeanSensitivity;
    [Tooltip("Smoothing speed for pitch lean interpolation.")]
    public float pitchLeanSmoothing;

    [Header("Acceleration Response")]
    [Tooltip("How strongly forward/backward linear acceleration drives pitch lean (thrust start/stop, braking).")]
    public float forwardAccelPitchSensitivity;
    [Tooltip("How strongly lateral linear acceleration drives banking (centripetal force from turning at speed).")]
    public float lateralAccelBankSensitivity;
}

[System.Serializable]
public struct ThrusterEffects3DConfig
{
    [Tooltip("Thruster particle systems attached to this ship.")]
    public List<ParticleSystem> thrusters;
    [Tooltip("Time to ramp thruster emission up/down in seconds.")]
    public float rampTime;
    [Tooltip("Invert each thruster's original start color while active.")]
    public bool invertColors;
}

[System.Serializable]
public struct ShipSpeedEffects3DConfig
{
    [Header("Speed VFX")]
    public ParticleSystem speedDustParticles;
    public float maxDustEmissionRate;
    [Range(0f, 1f)]
    public float dustSpeedThreshold;
}

[System.Serializable]
public struct PlayerCameraRigConfig3D
{
    [Header("Dynamic Camera Settings")]
    public float minZOffset;
    public float maxZOffset;
    public float minFOV;
    public float maxFOV;
    public float cameraLerpSpeed;
}

[System.Serializable]
public struct ProjectileWeaponConfig3D
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform[] muzzles;
    public string targetTag;

    [Header("Combat")]
    public float cooldown;
    public float speed;
    public float damage;
    public float lifetime;
    public float impactForce;
    public float recoilForce;
}
