using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVisualTilt3D : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const float DefaultMaxBankAngle = 45f;
    private const float DefaultMaxPitchLeanAngle = 24f;

    [Header("References")]
    [Tooltip("Enemy flight controller that owns root movement intent. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Optional enemy strafe overlay used by duelists, flamethrowers, and other sliding/orbiting enemies. Auto-assigned from this GameObject when present.")]
    [SerializeField] private EnemyStrafeMover3D strafeMover;

    [Tooltip("Rigidbody used only for cosmetic velocity and acceleration sampling. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private Rigidbody body;

    [Tooltip("Owning entity used for shared visual-response values such as recoil pitch sensitivity. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private Entity3D entity;

    [Header("Visual Effects")]
    [Tooltip("Visual model and bank/pitch tuning. Only Visual Model is rotated; this never rotates the enemy root Rigidbody.")]
    [SerializeField] private VisualEffects3DConfig visualEffects = new VisualEffects3DConfig
    {
        maxBankAngle = DefaultMaxBankAngle,
        bankSensitivity = 16f,
        steeringInputBankSensitivity = 18f,
        bankSmoothing = 8f,
        bankReturnSmoothing = 5f,
        maxPitchLeanAngle = DefaultMaxPitchLeanAngle,
        pitchLeanSensitivity = 10f,
        steeringInputPitchSensitivity = 10f,
        pitchLeanSmoothing = 8f,
        pitchLeanReturnSmoothing = 5f,
        forwardAccelPitchSensitivity = 0.04f,
        lateralAccelBankSensitivity = 0.06f,
        lateralDriftBankSensitivity = 0.25f,
        verticalDriftPitchSensitivity = 0.2f
    };

    [Header("Enemy Tilt Inputs")]
    [Tooltip("How strongly movement/facing intent banks the visual model before the Rigidbody has visibly turned.")]
    [SerializeField] private float bankFromMoveIntent = 18f;

    [Tooltip("How strongly EnemyStrafeMover3D orbit/dodge/slide velocity banks the visual model.")]
    [SerializeField] private float bankFromStrafeVelocity = 0.45f;

    [Tooltip("How strongly climb/dive movement intent pitches the visual model.")]
    [SerializeField] private float pitchFromMoveIntent = 10f;

    [Tooltip("If enabled, local Rigidbody velocity and acceleration add cosmetic bank/pitch. Keep enabled so remote enemy proxies can tilt from observed motion without networking extra state.")]
    [SerializeField] private bool useRigidbodyVelocity = true;

    [Header("Debug")]
    [Tooltip("Draws enemy move/facing intent and current tilt axes when this object is selected.")]
    [SerializeField] private bool drawGizmos = true;

    private Quaternion _visualBaseLocalRotation = Quaternion.identity;
    private Vector3 _previousVelocity;
    private Vector3 _previousForward;
    private Vector3 _localVelocity;
    private Vector3 _localAcceleration;
    private Vector3 _localFacingChange;
    private float _currentBankAngle;
    private float _currentPitchLeanAngle;
    private float _lastSampleTime = -1f;
    private bool _hasVelocitySample;

    private void Awake()
    {
        AutoAssignReferences();
        ValidateVisualEffects();
        CacheBaseRotation();
        CacheInitialVelocity();
    }

    private void OnEnable()
    {
        CacheBaseRotation();
        CacheInitialVelocity();
    }

    private void OnDisable()
    {
        _currentBankAngle = 0f;
        _currentPitchLeanAngle = 0f;
        if (visualEffects.visualModel != null)
        {
            visualEffects.visualModel.localRotation = _visualBaseLocalRotation;
        }
    }

    private void OnValidate()
    {
        ValidateVisualEffects();
    }

    private void LateUpdate()
    {
        UpdateVisualRotation();
    }

    private void AutoAssignReferences()
    {
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        strafeMover ??= GetComponent<EnemyStrafeMover3D>();
        body ??= GetComponent<Rigidbody>();
        entity ??= GetComponent<Entity3D>();

        if (visualEffects.visualModel == null)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (candidate == null
                    || candidate is ParticleSystemRenderer
                    || candidate is TrailRenderer
                    || candidate is LineRenderer
                    || candidate.transform == transform)
                {
                    continue;
                }

                visualEffects.visualModel = candidate.transform;
                break;
            }
        }

        if (visualEffects.visualModel == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                {
                    visualEffects.visualModel = child;
                    break;
                }
            }
        }
    }

    private void CacheBaseRotation()
    {
        _visualBaseLocalRotation = visualEffects.visualModel != null
            ? visualEffects.visualModel.localRotation
            : Quaternion.identity;
    }

    private void CacheInitialVelocity()
    {
        _previousVelocity = body != null ? body.linearVelocity : Vector3.zero;
        _previousForward = transform.forward;
        _localVelocity = Vector3.zero;
        _localAcceleration = Vector3.zero;
        _localFacingChange = Vector3.zero;
        _lastSampleTime = Time.time;
        _hasVelocitySample = body != null;
    }

    private void UpdateVisualRotation()
    {
        if (visualEffects.visualModel == null || Time.deltaTime <= 0f)
        {
            return;
        }

        SampleVelocityTelemetry();
        ResolveIntentAxes(out float lateralIntent, out float verticalIntent);

        float strafeBank = 0f;
        if (strafeMover != null && strafeMover.IsStrafing)
        {
            strafeBank = -transform.InverseTransformDirection(strafeMover.CurrentStrafeVelocity).x * bankFromStrafeVelocity;
        }

        float targetBankAngle = Mathf.Clamp(
            (-lateralIntent * bankFromMoveIntent)
            + (-lateralIntent * visualEffects.steeringInputBankSensitivity)
            + (-_localFacingChange.x * visualEffects.bankSensitivity)
            + (-_localAcceleration.x * visualEffects.lateralAccelBankSensitivity)
            + (-_localVelocity.x * visualEffects.lateralDriftBankSensitivity)
            + strafeBank,
            -visualEffects.maxBankAngle,
            visualEffects.maxBankAngle);

        float targetPitchLeanAngle = Mathf.Clamp(
            (verticalIntent * pitchFromMoveIntent)
            + (verticalIntent * visualEffects.steeringInputPitchSensitivity)
            + (_localFacingChange.y * visualEffects.pitchLeanSensitivity)
            + (_localAcceleration.z * visualEffects.forwardAccelPitchSensitivity)
            + (-_localVelocity.y * visualEffects.verticalDriftPitchSensitivity),
            -visualEffects.maxPitchLeanAngle,
            visualEffects.maxPitchLeanAngle);

        _currentBankAngle = DampAngle(_currentBankAngle, targetBankAngle, visualEffects.bankSmoothing, visualEffects.bankReturnSmoothing);
        _currentPitchLeanAngle = DampAngle(_currentPitchLeanAngle, targetPitchLeanAngle, visualEffects.pitchLeanSmoothing, visualEffects.pitchLeanReturnSmoothing);

        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchLeanAngle, Vector3.right);
        Quaternion bankQuat = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        visualEffects.visualModel.localRotation = _visualBaseLocalRotation * pitchQuat * bankQuat;
    }

    private void SampleVelocityTelemetry()
    {
        Vector3 forward = transform.forward;
        float now = Time.time;
        float deltaTime = _hasVelocitySample ? now - _lastSampleTime : 0f;
        _localFacingChange = deltaTime > 0.0001f
            ? transform.InverseTransformDirection((forward - _previousForward) / deltaTime)
            : Vector3.zero;
        _previousForward = forward;

        if (!useRigidbodyVelocity || body == null)
        {
            _localVelocity = Vector3.zero;
            _localAcceleration = Vector3.zero;
            _lastSampleTime = now;
            _hasVelocitySample = true;
            return;
        }

        Vector3 velocity = body.linearVelocity;
        Vector3 acceleration = deltaTime > 0.0001f
            ? (velocity - _previousVelocity) / deltaTime
            : Vector3.zero;

        _localVelocity = transform.InverseTransformDirection(velocity);
        _localAcceleration = transform.InverseTransformDirection(acceleration);
        _previousVelocity = velocity;
        _lastSampleTime = now;
        _hasVelocitySample = true;
    }

    private void ResolveIntentAxes(out float lateralIntent, out float verticalIntent)
    {
        lateralIntent = 0f;
        verticalIntent = 0f;

        Vector3 intent = ResolveBestIntentDirection();
        if (intent.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            return;
        }

        Vector3 localIntent = transform.InverseTransformDirection(intent.normalized);
        lateralIntent = Mathf.Clamp(localIntent.x, -1f, 1f);
        verticalIntent = Mathf.Clamp(localIntent.y, -1f, 1f);
    }

    private Vector3 ResolveBestIntentDirection()
    {
        if (flightController == null)
        {
            return Vector3.zero;
        }

        if (flightController.HasMoveIntent && flightController.MoveDirection.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return flightController.MoveDirection;
        }

        if (flightController.HasFacingIntent && flightController.FacingDirection.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return flightController.FacingDirection;
        }

        return Vector3.zero;
    }

    private float DampAngle(float current, float target, float activeSmoothing, float returnSmoothing)
    {
        float smoothing = Mathf.Abs(target) > Mathf.Abs(current) ? activeSmoothing : returnSmoothing;
        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothing) * Time.deltaTime);
        return Mathf.Lerp(current, target, lerpFactor);
    }

    private void ValidateVisualEffects()
    {
        bankFromMoveIntent = Mathf.Max(0f, bankFromMoveIntent);
        bankFromStrafeVelocity = Mathf.Max(0f, bankFromStrafeVelocity);
        pitchFromMoveIntent = Mathf.Max(0f, pitchFromMoveIntent);

        if (visualEffects.maxBankAngle <= 0f)
        {
            visualEffects.maxBankAngle = DefaultMaxBankAngle;
        }

        if (visualEffects.bankSensitivity == 0f)
        {
            visualEffects.bankSensitivity = 16f;
        }

        if (Mathf.Approximately(visualEffects.steeringInputBankSensitivity, 0f))
        {
            visualEffects.steeringInputBankSensitivity = 18f;
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
            visualEffects.maxPitchLeanAngle = DefaultMaxPitchLeanAngle;
        }

        if (visualEffects.pitchLeanSensitivity == 0f)
        {
            visualEffects.pitchLeanSensitivity = 10f;
        }

        if (Mathf.Approximately(visualEffects.steeringInputPitchSensitivity, 0f))
        {
            visualEffects.steeringInputPitchSensitivity = 10f;
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
            visualEffects.forwardAccelPitchSensitivity = 0.04f;
        }

        if (Mathf.Approximately(visualEffects.lateralAccelBankSensitivity, 0f))
        {
            visualEffects.lateralAccelBankSensitivity = 0.06f;
        }

        if (Mathf.Approximately(visualEffects.lateralDriftBankSensitivity, 0f))
        {
            visualEffects.lateralDriftBankSensitivity = 0.25f;
        }

        if (Mathf.Approximately(visualEffects.verticalDriftPitchSensitivity, 0f))
        {
            visualEffects.verticalDriftPitchSensitivity = 0.2f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 origin = transform.position;
        if (flightController != null)
        {
            if (flightController.HasMoveIntent && flightController.MoveDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(origin, flightController.MoveDirection.normalized * 8f);
            }

            if (flightController.HasFacingIntent && flightController.FacingDirection.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(origin, flightController.FacingDirection.normalized * 6f);
            }
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(origin, transform.forward * 5f);
        Gizmos.DrawRay(origin, transform.right * (_currentBankAngle / Mathf.Max(1f, visualEffects.maxBankAngle) * 4f));
        Gizmos.DrawRay(origin, transform.up * (_currentPitchLeanAngle / Mathf.Max(1f, visualEffects.maxPitchLeanAngle) * 4f));
    }
}
