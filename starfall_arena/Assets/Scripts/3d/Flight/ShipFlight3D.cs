using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipFlight3D : MonoBehaviour
{
    private const float MinSpeedSqrMagnitude = 0.0001f;

    [Header("Flight")]
    [SerializeField] private ShipFlightConfig3D flight = new ShipFlightConfig3D
    {
        thrustAcceleration = 50f,
        maxSpeed = 100f,
        lookInputResponse = 8f,
        pitchSpeed = 2.5f,
        yawSpeed = 2.5f,
        pitchAcceleration = 12f,
        pitchDeceleration = 16f,
        yawAcceleration = 12f,
        yawDeceleration = 16f,
        invertY = true,
        minRotationMultiplierAtMaxSpeed = 0.1f
    };

    [Header("Flight Assist")]
    [SerializeField] private ShipFlightAssistConfig3D flightAssist = new ShipFlightAssistConfig3D
    {
        frictionDeceleration = 20f,
        activeAngularDamping = 2f,
        lateralDriftDamping = 18f,
        verticalDriftDamping = 16f,
        velocityAlignmentStrength = 3f
    };

    [SerializeField] private MonoBehaviour inputSourceBehaviour;
    [SerializeField] private Entity3D entity;
    [SerializeField] private bool frictionEnabled;

    [Header("Flight Plane")]
    [SerializeField] private bool lockToWorldYPlane;
    [SerializeField] private bool captureInitialWorldY = true;
    [SerializeField] private float lockedWorldY;

    private Rigidbody _rb;
    private IShipFlightInputSource _inputSource;
    private Vector2 _lookInput;
    private Vector2 _filteredLookInput;
    private Vector2 _currentTurnRates;
    private Vector2 _normalizedTurnRates;
    private float _thrustInput;
    private Vector3 _previousVelocity;
    private Vector3 _linearAcceleration;
    private Vector3 _localVelocity;
    private Vector3 _localLinearAcceleration;
    private Vector3 _recentRecoilVelocityDelta;
    private Vector3 _recoilVelocityDeltaThisStep;
    private Vector3 _lastFixedStepRecoilVelocityDelta;
    private float _effectiveThrustInput;
    private bool _externalSimulationEnabled;

    public Rigidbody Rigidbody => _rb;
    public Vector2 LookInput => _lookInput;
    public Vector2 FilteredLookInput => GetEffectiveSteeringInput();
    public Vector2 CurrentTurnRates => _currentTurnRates;
    public Vector2 NormalizedTurnRates => _normalizedTurnRates;
    public float ThrustInput => _thrustInput;
    public bool IsFrictionEnabled => frictionEnabled;
    public bool IsExternalSimulationEnabled => _externalSimulationEnabled;
    public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public Vector3 LocalVelocity => _localVelocity;
    public Vector3 LinearAcceleration => _linearAcceleration;
    public Vector3 LocalLinearAcceleration => _localLinearAcceleration;
    public Vector3 RecentRecoilVelocityDelta => _recentRecoilVelocityDelta;
    public Vector3 LastFixedStepRecoilAcceleration => Time.fixedDeltaTime > 0f ? _lastFixedStepRecoilVelocityDelta / Time.fixedDeltaTime : Vector3.zero;
    public float ForwardSpeed => Vector3.Dot(LinearVelocity, transform.forward);
    public float ForwardSpeedNormalized => flight.maxSpeed > 0f ? Mathf.Clamp01(Mathf.Clamp(ForwardSpeed, 0f, flight.maxSpeed) / flight.maxSpeed) : 0f;
    public float LateralSpeed => _localVelocity.x;
    public float VerticalSpeed => _localVelocity.y;
    public bool IsApplyingThrust => _effectiveThrustInput > 0.05f;
    public ShipFlightConfig3D FlightConfig => flight;
    public ShipFlightAssistConfig3D FlightAssistConfig => flightAssist;
    public bool LockToWorldYPlane => lockToWorldYPlane;
    public float LockedWorldY => lockedWorldY;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        entity ??= GetComponent<Entity3D>();
        ValidateConfigValues();
        ConfigureRigidbody();
        SetInputSource(inputSourceBehaviour);
        CacheLockedWorldYIfNeeded();
        _previousVelocity = _rb.linearVelocity;
        _localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
    }

    private void OnValidate()
    {
        ValidateConfigValues();

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
        if (Time.fixedDeltaTime <= 0f)
        {
            return;
        }

        if (_externalSimulationEnabled)
        {
            _previousVelocity = _rb.linearVelocity;
            _lastFixedStepRecoilVelocityDelta = _recoilVelocityDeltaThisStep;
            _recoilVelocityDeltaThisStep = Vector3.zero;
            return;
        }

        PullInputFromSource();
        FilterLookInput();
        HandleRotation();
        HandleThrustAndAssist();
        EnforceFlightPlane();
        UpdateTelemetry();

        _previousVelocity = _rb.linearVelocity;
        _lastFixedStepRecoilVelocityDelta = _recoilVelocityDeltaThisStep;
        _recoilVelocityDeltaThisStep = Vector3.zero;
    }

    public void SetFlightConfig(ShipFlightConfig3D config)
    {
        flight = config;
        ValidateConfigValues();
    }

    public void SetFlightAssistConfig(ShipFlightAssistConfig3D config)
    {
        flightAssist = config;
        ValidateConfigValues();
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

    public void SetExternalSimulationEnabled(bool enabled)
    {
        _externalSimulationEnabled = enabled;

        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody>();
        }

        if (!enabled || _rb == null)
        {
            return;
        }

        _rb.angularVelocity = Vector3.zero;
        _previousVelocity = _rb.linearVelocity;
        _linearAcceleration = Vector3.zero;
        _localLinearAcceleration = Vector3.zero;
        _recentRecoilVelocityDelta = Vector3.zero;
        _recoilVelocityDeltaThisStep = Vector3.zero;
        _lastFixedStepRecoilVelocityDelta = Vector3.zero;
    }

    public void SetLookInput(Vector2 lookInput)
    {
        _lookInput = Vector2.ClampMagnitude(lookInput, 1f);
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

        Vector3 recoilVelocityDelta = -transform.forward * recoilForce;
        _rb.linearVelocity += recoilVelocityDelta;
        _recentRecoilVelocityDelta += recoilVelocityDelta;
        _recoilVelocityDeltaThisStep += recoilVelocityDelta;
        EnforceFlightPlane();
        _localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
    }

    public void ApplyExternalSimulationState(
        Vector2 rawLookInput,
        Vector2 filteredLookInput,
        Vector2 currentTurnRates,
        float thrustInput,
        bool frictionActive,
        Vector3 linearVelocity,
        Vector3 linearAcceleration,
        Vector3 recoilVelocityDelta)
    {
        _lookInput = Vector2.ClampMagnitude(rawLookInput, 1f);
        _filteredLookInput = Vector2.ClampMagnitude(filteredLookInput, 1f);
        _currentTurnRates = currentTurnRates;
        _normalizedTurnRates = new Vector2(
            NormalizeTurnRate(_currentTurnRates.x, flight.pitchSpeed),
            NormalizeTurnRate(_currentTurnRates.y, flight.yawSpeed));
        _thrustInput = Mathf.Clamp(thrustInput, -1f, 1f);
        frictionEnabled = frictionActive;
        _effectiveThrustInput = Mathf.Max(0f, _thrustInput);
        _linearAcceleration = linearAcceleration;
        _localLinearAcceleration = transform.InverseTransformDirection(linearAcceleration);
        _localVelocity = transform.InverseTransformDirection(linearVelocity);
        _recentRecoilVelocityDelta = recoilVelocityDelta;
        _lastFixedStepRecoilVelocityDelta = recoilVelocityDelta;
        _previousVelocity = linearVelocity;

        if (_rb != null)
        {
            _rb.linearVelocity = linearVelocity;
            _rb.angularDamping = frictionEnabled ? flightAssist.activeAngularDamping : 0f;
        }
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

        _lookInput = Vector2.ClampMagnitude(_inputSource.LookInput, 1f);
        _thrustInput = Mathf.Clamp(_inputSource.ThrustInput, -1f, 1f);

        if (_inputSource.ConsumeToggleFrictionPressed())
        {
            ToggleFriction();
        }
    }

    private void FilterLookInput()
    {
        float response = Mathf.Max(0.01f, flight.lookInputResponse);
        float lerpFactor = 1f - Mathf.Exp(-response * Time.fixedDeltaTime);
        _filteredLookInput = Vector2.Lerp(_filteredLookInput, _lookInput, lerpFactor);
    }

    private void HandleRotation()
    {
        float speedPercent = flight.maxSpeed > 0f ? Mathf.Clamp01(_rb.linearVelocity.magnitude / flight.maxSpeed) : 0f;
        float speedRotationMultiplier = Mathf.Lerp(1f, flight.minRotationMultiplierAtMaxSpeed, speedPercent);
        float baseRotationMultiplier = entity != null ? entity.GetBaseRotationMultiplier() : 1f;
        float abilityRotationMultiplier = entity != null ? entity.GetAbilityRotationMultiplier() : 1f;

        Vector2 steeringInput = GetEffectiveSteeringInput();
        Vector2 targetTurnRates = new Vector2(
            steeringInput.y * flight.pitchSpeed * baseRotationMultiplier * speedRotationMultiplier * abilityRotationMultiplier,
            steeringInput.x * flight.yawSpeed * baseRotationMultiplier * speedRotationMultiplier * abilityRotationMultiplier
        );

        _currentTurnRates.x = MoveTurnRate(_currentTurnRates.x, targetTurnRates.x, flight.pitchAcceleration, flight.pitchDeceleration);
        _currentTurnRates.y = MoveTurnRate(_currentTurnRates.y, targetTurnRates.y, flight.yawAcceleration, flight.yawDeceleration);
        _normalizedTurnRates = new Vector2(
            NormalizeTurnRate(_currentTurnRates.x, flight.pitchSpeed),
            NormalizeTurnRate(_currentTurnRates.y, flight.yawSpeed)
        );

        Vector3 localAngularVelocity = new Vector3(_currentTurnRates.x, _currentTurnRates.y, 0f);
        _rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void HandleThrustAndAssist()
    {
        float thrustMultiplier = entity != null ? entity.GetCombinedThrustMultiplier() : 1f;
        float slowMultiplier = entity != null ? entity.GetSlowMultiplier() : 1f;
        _effectiveThrustInput = thrustMultiplier > 0f ? Mathf.Max(0f, _thrustInput) : 0f;
        bool passiveLinearAssistEnabled = frictionEnabled && flightAssist.frictionDeceleration > 0f;
        bool isApplyingThrust = _effectiveThrustInput > 0.05f;

        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);

        if (isApplyingThrust)
        {
            localVelocity.z += _effectiveThrustInput * flight.thrustAcceleration * thrustMultiplier * slowMultiplier * Time.fixedDeltaTime;
        }
        else if (passiveLinearAssistEnabled)
        {
            localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0f, flightAssist.frictionDeceleration * Time.fixedDeltaTime);
        }

        if (passiveLinearAssistEnabled)
        {
            localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, flightAssist.lateralDriftDamping * Time.fixedDeltaTime);
            localVelocity.y = Mathf.MoveTowards(localVelocity.y, 0f, flightAssist.verticalDriftDamping * Time.fixedDeltaTime);
        }

        Vector3 worldVelocity = transform.TransformDirection(localVelocity);
        worldVelocity = ApplyVelocityAlignment(worldVelocity, passiveLinearAssistEnabled);

        float effectiveMaxSpeed = Mathf.Max(0f, flight.maxSpeed * slowMultiplier);
        if (effectiveMaxSpeed > 0f && worldVelocity.magnitude > effectiveMaxSpeed)
        {
            worldVelocity = worldVelocity.normalized * effectiveMaxSpeed;
        }

        _rb.linearVelocity = worldVelocity;
        _localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
    }

    private Vector3 ApplyVelocityAlignment(Vector3 worldVelocity, bool passiveLinearAssistEnabled)
    {
        if (!passiveLinearAssistEnabled || flightAssist.velocityAlignmentStrength <= 0f || _effectiveThrustInput <= 0.05f || worldVelocity.sqrMagnitude <= MinSpeedSqrMagnitude)
        {
            return worldVelocity;
        }

        float turnInfluence = Mathf.Clamp01(Mathf.Max(Mathf.Abs(_normalizedTurnRates.x), Mathf.Abs(_normalizedTurnRates.y)));
        float alignmentStrength = flightAssist.velocityAlignmentStrength * _effectiveThrustInput * (0.5f + (0.5f * turnInfluence));
        float lerpFactor = 1f - Mathf.Exp(-alignmentStrength * Time.fixedDeltaTime);
        Vector3 alignedDirection = Vector3.Slerp(worldVelocity.normalized, transform.forward, lerpFactor).normalized;
        return alignedDirection * worldVelocity.magnitude;
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

    private void UpdateTelemetry()
    {
        _linearAcceleration = (_rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        _localLinearAcceleration = transform.InverseTransformDirection(_linearAcceleration);
    }

    private Vector2 GetEffectiveSteeringInput()
    {
        return new Vector2(
            _filteredLookInput.x,
            _filteredLookInput.y * (flight.invertY ? -1f : 1f)
        );
    }

    private static float MoveTurnRate(float current, float target, float acceleration, float deceleration)
    {
        float step = DetermineTurnRateStep(current, target, acceleration, deceleration) * Time.fixedDeltaTime;
        return Mathf.MoveTowards(current, target, step);
    }

    private static float DetermineTurnRateStep(float current, float target, float acceleration, float deceleration)
    {
        bool acceleratingIntoSameDirection = Mathf.Abs(target) > Mathf.Abs(current) && Mathf.Sign(target) == Mathf.Sign(current);
        return Mathf.Max(0.01f, acceleratingIntoSameDirection ? acceleration : deceleration);
    }

    private static float NormalizeTurnRate(float turnRate, float maxTurnRate)
    {
        if (maxTurnRate <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp(turnRate / maxTurnRate, -1f, 1f);
    }

    private void ValidateConfigValues()
    {
        flight.thrustAcceleration = Mathf.Max(0f, flight.thrustAcceleration);
        if (flight.maxSpeed <= 0f)
        {
            flight.maxSpeed = 100f;
        }

        if (flight.lookInputResponse <= 0f)
        {
            flight.lookInputResponse = 8f;
        }

        if (flight.pitchSpeed <= 0f)
        {
            flight.pitchSpeed = 2.5f;
        }

        if (flight.yawSpeed <= 0f)
        {
            flight.yawSpeed = 2.5f;
        }

        if (flight.pitchAcceleration <= 0f)
        {
            flight.pitchAcceleration = 12f;
        }

        if (flight.pitchDeceleration <= 0f)
        {
            flight.pitchDeceleration = 16f;
        }

        if (flight.yawAcceleration <= 0f)
        {
            flight.yawAcceleration = 12f;
        }

        if (flight.yawDeceleration <= 0f)
        {
            flight.yawDeceleration = 16f;
        }

        flight.minRotationMultiplierAtMaxSpeed = Mathf.Clamp01(flight.minRotationMultiplierAtMaxSpeed);

        if (flightAssist.frictionDeceleration < 0f)
        {
            flightAssist.frictionDeceleration = 0f;
        }

        if (flightAssist.activeAngularDamping < 0f)
        {
            flightAssist.activeAngularDamping = 0f;
        }

        if (flightAssist.lateralDriftDamping <= 0f)
        {
            flightAssist.lateralDriftDamping = 18f;
        }

        if (flightAssist.verticalDriftDamping <= 0f)
        {
            flightAssist.verticalDriftDamping = 16f;
        }

        if (flightAssist.velocityAlignmentStrength < 0f)
        {
            flightAssist.velocityAlignmentStrength = 0f;
        }
    }
}
