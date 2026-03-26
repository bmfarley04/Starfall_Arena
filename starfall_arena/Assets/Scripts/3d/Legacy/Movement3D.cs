using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ShipFlight3D))]
public class Movement3D : Player3D
{
    [Header("Legacy Flight Settings")]
    [SerializeField] private ShipFlightConfig3D flightConfig = new ShipFlightConfig3D
    {
        thrustAcceleration = 50f,
        maxSpeed = 100f,
        pitchSpeed = 2.5f,
        yawSpeed = 2.5f,
        invertY = true,
        minRotationMultiplierAtMaxSpeed = 0.1f
    };

    [Header("Legacy Flight Assist")]
    [SerializeField] private ShipFlightAssistConfig3D flightAssistConfig = new ShipFlightAssistConfig3D
    {
        frictionDeceleration = 20f,
        activeAngularDamping = 2f
    };

    [Header("Legacy Camera")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private PlayerCameraRigConfig3D cameraConfig = new PlayerCameraRigConfig3D
    {
        minZOffset = -10f,
        maxZOffset = -16f,
        minFOV = 40f,
        maxFOV = 70f,
        cameraLerpSpeed = 5f
    };

    [Header("Legacy Speed VFX")]
    [SerializeField] private ShipSpeedEffects3DConfig speedEffects = new ShipSpeedEffects3DConfig
    {
        maxDustEmissionRate = 200f,
        dustSpeedThreshold = 0.5f
    };

    [Header("Legacy Visual Effects")]
    [SerializeField] private VisualEffects3DConfig visualEffects;

    [Header("Legacy Thruster Effects")]
    [SerializeField] private ThrusterEffects3DConfig thrusterEffects;

    protected override void Awake()
    {
        EnsureArchitectureComponents();
        base.Awake();
        ApplyLegacyConfiguration();
    }

    private void Reset()
    {
        EnsureArchitectureComponents();
        ApplyLegacyConfiguration();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureArchitectureComponents();
            ApplyLegacyConfiguration();
        }
    }

    public void OnFreeLook(InputValue value)
    {
        if (playerInput3D != null)
        {
            playerInput3D.OnFreeLook(value);
        }
        else if (shipFlight != null)
        {
            shipFlight.SetLookInput(value.Get<Vector2>());
        }
    }

    public void OnThrust(InputValue value)
    {
        if (playerInput3D != null)
        {
            playerInput3D.OnThrust(value);
        }
        else if (shipFlight != null)
        {
            shipFlight.SetThrustInput(value.Get<float>());
        }
    }

    public void OnToggleFriction(InputValue value)
    {
        if (playerInput3D != null)
        {
            playerInput3D.OnToggleFriction(value);
        }
        else if (shipFlight != null && value.isPressed)
        {
            shipFlight.ToggleFriction();
        }
    }

    public void OnFire(InputValue value)
    {
        if (playerInput3D != null)
        {
            playerInput3D.OnFire(value);
        }
    }

    private void EnsureArchitectureComponents()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        if (shipFlight == null)
        {
            shipFlight = gameObject.AddComponent<ShipFlight3D>();
        }

        shipVisualTilt ??= GetComponent<ShipVisualTilt3D>();
        if (shipVisualTilt == null)
        {
            shipVisualTilt = gameObject.AddComponent<ShipVisualTilt3D>();
        }

        shipThrusterVfx ??= GetComponent<ShipThrusterVfx3D>();
        if (shipThrusterVfx == null)
        {
            shipThrusterVfx = gameObject.AddComponent<ShipThrusterVfx3D>();
        }

        shipSpeedFx ??= GetComponent<ShipSpeedFx3D>();
        if (shipSpeedFx == null)
        {
            shipSpeedFx = gameObject.AddComponent<ShipSpeedFx3D>();
        }

        playerInput3D ??= GetComponent<PlayerInput3D>();
        if (playerInput3D == null)
        {
            playerInput3D = gameObject.AddComponent<PlayerInput3D>();
        }

        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();
        if (playerCameraRig3D == null)
        {
            playerCameraRig3D = gameObject.AddComponent<PlayerCameraRig3D>();
        }
    }

    private void ApplyLegacyConfiguration()
    {
        if (shipFlight != null)
        {
            shipFlight.SetFlightConfig(flightConfig);
            shipFlight.SetFlightAssistConfig(flightAssistConfig);
            shipFlight.SetInputSource(playerInput3D);
        }

        if (shipVisualTilt != null)
        {
            shipVisualTilt.SetShipFlight(shipFlight);
            shipVisualTilt.SetVisualEffects(visualEffects);
        }

        if (shipThrusterVfx != null)
        {
            shipThrusterVfx.SetShipFlight(shipFlight);
            shipThrusterVfx.SetThrusterEffects(thrusterEffects);
        }

        if (shipSpeedFx != null)
        {
            shipSpeedFx.SetShipFlight(shipFlight);
            shipSpeedFx.SetSpeedEffects(speedEffects);
        }

        if (playerCameraRig3D != null)
        {
            playerCameraRig3D.SetShipFlight(shipFlight);
            playerCameraRig3D.SetCamera(virtualCamera);
            playerCameraRig3D.SetCameraConfig(cameraConfig);
        }
    }
}
