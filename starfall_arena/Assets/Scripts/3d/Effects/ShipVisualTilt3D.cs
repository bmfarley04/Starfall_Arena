using UnityEngine;

public class ShipVisualTilt3D : MonoBehaviour
{
    [System.Serializable]
    private struct BarrelRollConfig3D
    {
        [Tooltip("Degrees the visual model rolls during a dodge. 360 is a full barrel roll.")]
        public float degrees;
        [Tooltip("Multiplies the dodge slide duration to determine visual roll duration.")]
        public float durationMultiplier;
        [Tooltip("Minimum seconds for the visual roll.")]
        public float minDuration;
        [Tooltip("Extra bank applied into the dodge direction while the roll starts.")]
        public float entryBankAngle;
    }

    [SerializeField] private Entity3D entity;
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private VisualEffects3DConfig visualEffects;
    [Header("Dodge Barrel Roll")]
    [SerializeField] private BarrelRollConfig3D barrelRoll = new BarrelRollConfig3D
    {
        degrees = 360f,
        durationMultiplier = 1.1f,
        minDuration = 0.18f,
        entryBankAngle = 28f
    };

    private float _currentBankAngle;
    private float _currentPitchLeanAngle;
    private float _barrelRollStartTime = float.NegativeInfinity;
    private float _barrelRollDuration = 0.18f;
    private int _barrelRollDirection = 1;
    private Quaternion _visualBaseLocalRotation = Quaternion.identity;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        if (entity == null)
        {
            entity = GetComponent<Entity3D>();
        }

        ValidateVisualEffects();
        CacheBaseRotation();
    }

    private void OnValidate()
    {
        ValidateVisualEffects();
    }

    private void LateUpdate()
    {
        UpdateVisualRotation();
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetVisualEffects(VisualEffects3DConfig config)
    {
        visualEffects = config;
        ValidateVisualEffects();
        CacheBaseRotation();
    }

    public void BeginBarrelRoll(int horizontalDirection, float dodgeDuration)
    {
        _barrelRollDirection = horizontalDirection >= 0 ? -1 : 1;
        _barrelRollDuration = Mathf.Max(Mathf.Max(0.01f, dodgeDuration) * Mathf.Max(0.01f, barrelRoll.durationMultiplier), Mathf.Max(0.01f, barrelRoll.minDuration));
        _barrelRollStartTime = Time.time;
    }

    private void CacheBaseRotation()
    {
        if (visualEffects.visualModel != null)
        {
            _visualBaseLocalRotation = visualEffects.visualModel.localRotation;
        }
        else
        {
            _visualBaseLocalRotation = Quaternion.identity;
        }
    }

    private void UpdateVisualRotation()
    {
        if (shipFlight == null || visualEffects.visualModel == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector2 steeringInput = shipFlight.FilteredLookInput;
        Vector2 turnRatesNormalized = shipFlight.NormalizedTurnRates;
        Vector3 localAcceleration = shipFlight.LocalLinearAcceleration;
        Vector3 localVelocity = shipFlight.LocalVelocity;

        float recoilImpulse = Vector3.Dot(shipFlight.RecentRecoilVelocityDelta, transform.forward);

        float targetBankAngle = Mathf.Clamp(
            (-steeringInput.x * visualEffects.steeringInputBankSensitivity)
            + (-turnRatesNormalized.y * visualEffects.bankSensitivity)
            + (-localAcceleration.x * visualEffects.lateralAccelBankSensitivity)
            + (-localVelocity.x * visualEffects.lateralDriftBankSensitivity),
            -visualEffects.maxBankAngle,
            visualEffects.maxBankAngle
        );

        float targetPitchLeanAngle = Mathf.Clamp(
            (steeringInput.y * visualEffects.steeringInputPitchSensitivity)
            + (turnRatesNormalized.x * visualEffects.pitchLeanSensitivity)
            + (localAcceleration.z * visualEffects.forwardAccelPitchSensitivity)
            + (-localVelocity.y * visualEffects.verticalDriftPitchSensitivity)
            + (recoilImpulse * GetImpulseRecoilPitchSensitivity()),
            -visualEffects.maxPitchLeanAngle,
            visualEffects.maxPitchLeanAngle
        );

        _currentBankAngle = DampAngle(_currentBankAngle, targetBankAngle, visualEffects.bankSmoothing, visualEffects.bankReturnSmoothing);
        _currentPitchLeanAngle = DampAngle(_currentPitchLeanAngle, targetPitchLeanAngle, visualEffects.pitchLeanSmoothing, visualEffects.pitchLeanReturnSmoothing);

        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchLeanAngle, Vector3.right);
        Quaternion bankQuat = Quaternion.AngleAxis(_currentBankAngle + GetBarrelRollEntryBankAngle(), Vector3.forward);
        Quaternion barrelRollQuat = Quaternion.AngleAxis(GetBarrelRollAngle(), Vector3.forward);
        visualEffects.visualModel.localRotation = _visualBaseLocalRotation * pitchQuat * bankQuat * barrelRollQuat;
    }

    private float GetBarrelRollAngle()
    {
        if (Time.time >= _barrelRollStartTime + _barrelRollDuration)
        {
            return 0f;
        }

        float t = Mathf.Clamp01((Time.time - _barrelRollStartTime) / Mathf.Max(0.01f, _barrelRollDuration));
        float eased = SmoothStep01(t);
        return _barrelRollDirection * Mathf.Max(0f, barrelRoll.degrees) * eased;
    }

    private float GetBarrelRollEntryBankAngle()
    {
        if (Time.time >= _barrelRollStartTime + _barrelRollDuration)
        {
            return 0f;
        }

        float t = Mathf.Clamp01((Time.time - _barrelRollStartTime) / Mathf.Max(0.01f, _barrelRollDuration));
        float pulse = Mathf.Sin(t * Mathf.PI);
        return _barrelRollDirection * Mathf.Max(0f, barrelRoll.entryBankAngle) * pulse;
    }

    private float GetImpulseRecoilPitchSensitivity()
    {
        if (entity != null)
        {
            return entity.ImpulseRecoilPitchSensitivity;
        }

        return 1f;
    }

    private float DampAngle(float current, float target, float activeSmoothing, float returnSmoothing)
    {
        float smoothing = Mathf.Abs(target) > Mathf.Abs(current) ? activeSmoothing : returnSmoothing;
        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothing) * Time.deltaTime);
        return Mathf.Lerp(current, target, lerpFactor);
    }

    private void ValidateVisualEffects()
    {
        if (barrelRoll.degrees <= 0f)
        {
            barrelRoll.degrees = 360f;
        }

        if (barrelRoll.durationMultiplier <= 0f)
        {
            barrelRoll.durationMultiplier = 1.1f;
        }

        if (barrelRoll.minDuration <= 0f)
        {
            barrelRoll.minDuration = 0.18f;
        }

        if (barrelRoll.entryBankAngle < 0f)
        {
            barrelRoll.entryBankAngle = 0f;
        }

        if (visualEffects.maxBankAngle <= 0f)
        {
            visualEffects.maxBankAngle = 75f;
        }
        else
        {
            visualEffects.maxBankAngle = Mathf.Max(0f, visualEffects.maxBankAngle);
        }

        if (visualEffects.bankSensitivity == 0f)
        {
            visualEffects.bankSensitivity = 20f;
        }

        if (Mathf.Approximately(visualEffects.steeringInputBankSensitivity, 0f))
        {
            visualEffects.steeringInputBankSensitivity = 24f;
        }

        if (visualEffects.bankSmoothing <= 0f)
        {
            visualEffects.bankSmoothing = 8f;
        }

        if (visualEffects.bankReturnSmoothing <= 0f)
        {
            visualEffects.bankReturnSmoothing = 5f;
        }

        if (visualEffects.maxPitchLeanAngle <= 0f)
        {
            visualEffects.maxPitchLeanAngle = 40f;
        }
        else
        {
            visualEffects.maxPitchLeanAngle = Mathf.Max(0f, visualEffects.maxPitchLeanAngle);
        }

        if (visualEffects.pitchLeanSensitivity == 0f)
        {
            visualEffects.pitchLeanSensitivity = 12f;
        }

        if (Mathf.Approximately(visualEffects.steeringInputPitchSensitivity, 0f))
        {
            visualEffects.steeringInputPitchSensitivity = 14f;
        }

        if (visualEffects.pitchLeanSmoothing <= 0f)
        {
            visualEffects.pitchLeanSmoothing = 8f;
        }

        if (visualEffects.pitchLeanReturnSmoothing <= 0f)
        {
            visualEffects.pitchLeanReturnSmoothing = 5f;
        }

        if (Mathf.Approximately(visualEffects.forwardAccelPitchSensitivity, 0f))
        {
            visualEffects.forwardAccelPitchSensitivity = 0.08f;
        }

        if (Mathf.Approximately(visualEffects.lateralAccelBankSensitivity, 0f))
        {
            visualEffects.lateralAccelBankSensitivity = 0.1f;
        }

        if (Mathf.Approximately(visualEffects.lateralDriftBankSensitivity, 0f))
        {
            visualEffects.lateralDriftBankSensitivity = 0.5f;
        }

        if (Mathf.Approximately(visualEffects.verticalDriftPitchSensitivity, 0f))
        {
            visualEffects.verticalDriftPitchSensitivity = 0.35f;
        }
    }

    private static float SmoothStep01(float t)
    {
        return t * t * (3f - (2f * t));
    }
}
