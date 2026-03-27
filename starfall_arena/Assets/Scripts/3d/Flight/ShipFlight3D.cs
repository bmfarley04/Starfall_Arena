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
    [SerializeField] private Entity3D entity;
    [SerializeField] private bool frictionEnabled;

    [Header("Flight Plane")]
    [SerializeField] private bool lockToWorldYPlane = true;
    [SerializeField] private bool captureInitialWorldY = true;
    [SerializeField] private float lockedWorldY;

    private Rigidbody _rb;
    private IShipFlightInputSource _inputSource;
    private Vector2 _lookInput;
    private float _thrustInput;
    private Vector3 _previousVelocity;
    private Vector3 _linearAcceleration;
    private Vector3 _recentRecoilVelocityDelta;
    private Vector3 _recoilVelocityDeltaThisStep;
    private Vector3 _lastFixedStepRecoilVelocityDelta;
    private float _effectiveThrustInput;

    public Rigidbody Rigidbody => _rb;
    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;
    public bool IsFrictionEnabled => frictionEnabled;
    public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public Vector3 LinearAcceleration => _linearAcceleration;
    public Vector3 RecentRecoilVelocityDelta => _recentRecoilVelocityDelta;
    public Vector3 LastFixedStepRecoilAcceleration => Time.fixedDeltaTime > 0f ? _lastFixedStepRecoilVelocityDelta / Time.fixedDeltaTime : Vector3.zero;
    public float ForwardSpeed => Vector3.Dot(LinearVelocity, GetPlanarForward());
    public float ForwardSpeedNormalized => flight.maxSpeed > 0f ? Mathf.Clamp01(Mathf.Clamp(ForwardSpeed, 0f, flight.maxSpeed) / flight.maxSpeed) : 0f;
    public bool IsApplyingThrust => _effectiveThrustInput > 0.05f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        entity ??= GetComponent<Entity3D>();
        ConfigureRigidbody();
        SetInputSource(inputSourceBehaviour);
        CacheLockedWorldYIfNeeded();
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

    private void Update()
    {
        if (Time.deltaTime <= 0f)
        {
            return;
        }

        _recentRecoilVelocityDelta = Vector3.Lerp(_recentRecoilVelocityDelta, Vector3.zero, Time.deltaTime * 5f);
    }

    private void FixedUpdate()
    {
        PullInputFromSource();
        HandleRotation();
        HandleThrust();
        EnforceFlightPlane();

        _linearAcceleration = (_rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = _rb.linearVelocity;
        _lastFixedStepRecoilVelocityDelta = _recoilVelocityDeltaThisStep;
        _recoilVelocityDeltaThisStep = Vector3.zero;
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

        Vector3 recoilVelocityDelta = -GetPlanarForward() * recoilForce;
        _rb.linearVelocity += recoilVelocityDelta;
        _recentRecoilVelocityDelta += recoilVelocityDelta;
        _recoilVelocityDeltaThisStep += recoilVelocityDelta;
        EnforceFlightPlane();
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
        if (entity != null)
        {
            rotMult *= entity.GetCombinedRotationMultiplier();
        }

        float pitch = _lookInput.y * flight.pitchSpeed * rotMult * (flight.invertY ? -1f : 1f);
        float yaw = _lookInput.x * flight.yawSpeed * rotMult;

        Vector3 localAngularVelocity = new Vector3(pitch, yaw, 0f);
        _rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void HandleThrust()
    {
        float thrustMultiplier = entity != null ? entity.GetCombinedThrustMultiplier() : 1f;
        float slowMultiplier = entity != null ? entity.GetSlowMultiplier() : 1f;
        _effectiveThrustInput = thrustMultiplier > 0f ? _thrustInput : 0f;

        if (_effectiveThrustInput > 0.05f)
        {
            _rb.linearVelocity += GetPlanarForward() * (_effectiveThrustInput * flight.thrustAcceleration * thrustMultiplier * slowMultiplier * Time.fixedDeltaTime);
        }
        else if (frictionEnabled)
        {
            _rb.linearVelocity = Vector3.MoveTowards(_rb.linearVelocity, Vector3.zero, flightAssist.frictionDeceleration * Time.fixedDeltaTime);
        }

        float effectiveMaxSpeed = flight.maxSpeed * slowMultiplier;
        if (_rb.linearVelocity.magnitude > effectiveMaxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * effectiveMaxSpeed;
        }
    }

    private void CacheLockedWorldYIfNeeded()
    {
        if (captureInitialWorldY)
        {
            lockedWorldY = transform.position.y;
        }
    }

    private void EnforceFlightPlane()
    {
        if (_rb == null || !lockToWorldYPlane)
        {
            return;
        }

        Vector3 linearVelocity = _rb.linearVelocity;
        if (!Mathf.Approximately(linearVelocity.y, 0f))
        {
            linearVelocity.y = 0f;
            _rb.linearVelocity = linearVelocity;
        }

        Vector3 position = _rb.position;
        if (!Mathf.Approximately(position.y, lockedWorldY))
        {
            position.y = lockedWorldY;
            _rb.position = position;
        }
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return planarForward.normalized;
    }
}
