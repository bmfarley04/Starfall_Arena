using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipFlight3D : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] private ShipFlightConfig3D flight = new ShipFlightConfig3D
    {
        thrustAcceleration = 50f,
        maxSpeed = 100f,
        pitchSpeed = 2.5f,
        yawSpeed = 2.5f,
        invertY = true,
        minRotationMultiplierAtMaxSpeed = 0.1f
    };

    [Header("Flight Assist")]
    [SerializeField] private ShipFlightAssistConfig3D flightAssist = new ShipFlightAssistConfig3D
    {
        frictionDeceleration = 20f,
        activeAngularDamping = 2f
    };

    [SerializeField] private MonoBehaviour inputSourceBehaviour;
    [SerializeField] private bool frictionEnabled;

    private Rigidbody _rb;
    private IShipFlightInputSource _inputSource;
    private Vector2 _lookInput;
    private float _thrustInput;
    private Vector3 _previousVelocity;
    private Vector3 _linearAcceleration;

    public Rigidbody Rigidbody => _rb;
    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;
    public bool IsFrictionEnabled => frictionEnabled;
    public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public Vector3 LinearAcceleration => _linearAcceleration;
    public float ForwardSpeed => Vector3.Dot(LinearVelocity, transform.forward);
    public float ForwardSpeedNormalized => flight.maxSpeed > 0f ? Mathf.Clamp01(Mathf.Clamp(ForwardSpeed, 0f, flight.maxSpeed) / flight.maxSpeed) : 0f;
    public bool IsApplyingThrust => _thrustInput > 0.05f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        ConfigureRigidbody();
        SetInputSource(inputSourceBehaviour);
        _previousVelocity = _rb.linearVelocity;
    }

    private void OnValidate()
    {
        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody>();
        }

        if (_rb != null && Application.isPlaying)
        {
            ConfigureRigidbody();
        }
    }

    private void FixedUpdate()
    {
        PullInputFromSource();
        HandleRotation();
        HandleThrust();

        _linearAcceleration = (_rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = _rb.linearVelocity;
    }

    public void SetFlightConfig(ShipFlightConfig3D config)
    {
        flight = config;
    }

    public void SetFlightAssistConfig(ShipFlightAssistConfig3D config)
    {
        flightAssist = config;
        if (_rb != null)
        {
            _rb.angularDamping = frictionEnabled ? flightAssist.activeAngularDamping : 0f;
        }
    }

    public void SetInputSource(MonoBehaviour sourceBehaviour)
    {
        inputSourceBehaviour = sourceBehaviour;
        _inputSource = sourceBehaviour as IShipFlightInputSource;
    }

    public void SetLookInput(Vector2 lookInput)
    {
        _lookInput = lookInput;
    }

    public void SetThrustInput(float thrustInput)
    {
        _thrustInput = Mathf.Clamp(thrustInput, -1f, 1f);
    }

    public void ToggleFriction()
    {
        SetFrictionEnabled(!frictionEnabled);
    }

    public void SetFrictionEnabled(bool enabled)
    {
        frictionEnabled = enabled;
        if (_rb != null)
        {
            _rb.angularDamping = frictionEnabled ? flightAssist.activeAngularDamping : 0f;
        }
    }

    public void ApplyRecoil(float recoilForce)
    {
        if (_rb == null || Mathf.Approximately(recoilForce, 0f))
        {
            return;
        }

        _rb.linearVelocity -= transform.forward * recoilForce;
    }

    private void ConfigureRigidbody()
    {
        _rb.useGravity = false;
        _rb.linearDamping = 0f;
        _rb.angularDamping = frictionEnabled ? flightAssist.activeAngularDamping : 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void PullInputFromSource()
    {
        if (_inputSource == null)
        {
            return;
        }

        _lookInput = _inputSource.LookInput;
        _thrustInput = Mathf.Clamp(_inputSource.ThrustInput, -1f, 1f);

        if (_inputSource.ConsumeToggleFrictionPressed())
        {
            ToggleFriction();
        }
    }

    private void HandleRotation()
    {
        float speedPercent = flight.maxSpeed > 0f ? _rb.linearVelocity.magnitude / flight.maxSpeed : 0f;
        float rotMult = Mathf.Lerp(1f, flight.minRotationMultiplierAtMaxSpeed, Mathf.Clamp01(speedPercent));

        float pitch = _lookInput.y * flight.pitchSpeed * rotMult * (flight.invertY ? -1f : 1f);
        float yaw = _lookInput.x * flight.yawSpeed * rotMult;

        Vector3 localAngularVelocity = new Vector3(pitch, yaw, 0f);
        _rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void HandleThrust()
    {
        if (_thrustInput > 0.05f)
        {
            _rb.linearVelocity += transform.forward * (_thrustInput * flight.thrustAcceleration * Time.fixedDeltaTime);
        }
        else if (frictionEnabled)
        {
            _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, Vector3.zero, flightAssist.frictionDeceleration * Time.fixedDeltaTime);
        }

        if (_rb.linearVelocity.magnitude > flight.maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * flight.maxSpeed;
        }
    }
}
