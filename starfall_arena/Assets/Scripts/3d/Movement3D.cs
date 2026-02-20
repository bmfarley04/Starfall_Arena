using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
    [System.Serializable]
    public struct VisualEffects3DConfig
    {
        [Header("Visual Model")]
        [Tooltip("Child transform containing the ship mesh. Banking and pitch lean are applied here.")]
        public Transform visualModel;

        [Header("Banking (Roll)")]
        [Tooltip("Maximum roll angle applied to the visual model when yawing")]
        public float maxBankAngle;
        [Tooltip("How strongly yaw angular velocity drives the bank. Negative values invert the direction.")]
        public float bankSensitivity;
        [Tooltip("Smoothing speed for bank interpolation")]
        public float bankSmoothing;

        [Header("Pitch Lean")]
        [Tooltip("Maximum additional pitch lean applied to the visual model when pitching")]
        public float maxPitchLeanAngle;
        [Tooltip("How strongly pitch angular velocity drives the lean. Negative values invert the direction.")]
        public float pitchLeanSensitivity;
        [Tooltip("Smoothing speed for pitch lean interpolation")]
        public float pitchLeanSmoothing;

        [Header("Acceleration Response")]
        [Tooltip("How strongly forward/backward linear acceleration drives pitch lean (thrust start/stop, braking)")]
        public float forwardAccelPitchSensitivity;
        [Tooltip("How strongly lateral linear acceleration drives banking (centripetal force from turning at speed)")]
        public float lateralAccelBankSensitivity;
    }

    [Header("Engine Parameters")]
    [SerializeField] private float thrustAcceleration = 50f;
    [SerializeField] private float maxSpeed = 100f;

    [Header("Handling Parameters")]
    [SerializeField] private float pitchSpeed = 2.5f;
    [SerializeField] private float yawSpeed = 2.5f;
    [SerializeField] private bool invertY = true;

    [Header("Flight Assist (Friction)")]
    [SerializeField] private float activeLinearDamping = 1.5f;
    [SerializeField] private float activeAngularDamping = 2.0f;
    private bool isFrictionEnabled = false;

    [Header("Dynamic Camera Settings")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private float minZOffset = -10f;
    [SerializeField] private float maxZOffset = -16f;
    [SerializeField] private float minFOV = 40f;
    [SerializeField] private float maxFOV = 70f;
    [SerializeField] private float cameraLerpSpeed = 5f;

    [Header("VFX Settings")]
    [SerializeField] private ParticleSystem speedDustParticles;
    [SerializeField] private float maxDustEmissionRate = 200f;
    [SerializeField, Range(0f, 1f)] private float dustSpeedThreshold = 0.5f; // Sets the activation floor

    [Header("Visual Effects")]
    [SerializeField] private VisualEffects3DConfig visualEffects;

    private Rigidbody rb;
    private CinemachineFollow followComponent;
    private Vector2 lookInput;
    private float thrustInput;

    // Visual state
    private float _currentBankAngle;
    private float _currentPitchLeanAngle;
    private Quaternion _visualBaseLocalRotation;
    private Vector3 _previousVelocity;
    private Vector3 _linearAcceleration;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (virtualCamera != null)
        {
            followComponent = virtualCamera.GetComponent<CinemachineFollow>();
        }

        if (speedDustParticles != null)
        {
            var emission = speedDustParticles.emission;
            emission.rateOverTime = 0f;
        }

        if (visualEffects.visualModel != null)
        {
            _visualBaseLocalRotation = visualEffects.visualModel.localRotation;
        }
        else
        {
            _visualBaseLocalRotation = Quaternion.identity;
            Debug.LogWarning($"Visual model not assigned on {gameObject.name}. Banking/pitch lean will not work.", this);
        }
    }

    public void OnFreeLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnThrust(InputValue value)
    {
        thrustInput = value.Get<float>();
    }

    public void OnToggleFriction(InputValue value)
    {
        if (value.isPressed)
        {
            isFrictionEnabled = !isFrictionEnabled;

            if (isFrictionEnabled)
            {
                rb.linearDamping = activeLinearDamping;
                rb.angularDamping = activeAngularDamping;
            }
            else
            {
                rb.linearDamping = 0f;
                rb.angularDamping = 0f;
            }
        }
    }

    private void FixedUpdate()
    {
        HandleRotation();
        HandleThrust();

        _linearAcceleration = (rb.linearVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = rb.linearVelocity;
    }

    private void Update()
    {
        HandleVisuals();
    }

    private void LateUpdate()
    {
        UpdateVisualRotation();
    }

    private void HandleRotation()
    {
        float pitch = lookInput.y * pitchSpeed * (invertY ? -1f : 1f);
        float yaw = lookInput.x * yawSpeed;

        Vector3 localAngularVelocity = new Vector3(pitch, yaw, 0f);
        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
    }

    private void HandleThrust()
    {
        if (thrustInput > 0.05f)
        {
            rb.linearVelocity += transform.forward * (thrustInput * thrustAcceleration * Time.fixedDeltaTime);
        }

        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private void HandleVisuals()
    {
        // 1. Calculate ONLY the forward component of our velocity
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        // 2. Clamp it between 0 and maxSpeed so moving backward doesn't cause negative visual math
        float clampedForwardSpeed = Mathf.Clamp(forwardSpeed, 0f, maxSpeed);

        // 3. Get the percentage based purely on forward momentum
        float forwardSpeedPercent = clampedForwardSpeed / maxSpeed;

        // 4. Update Camera
        if (virtualCamera != null && followComponent != null)
        {
            float targetZ = Mathf.Lerp(minZOffset, maxZOffset, forwardSpeedPercent);
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, forwardSpeedPercent);

            Vector3 currentOffset = followComponent.FollowOffset;
            currentOffset.z = Mathf.Lerp(currentOffset.z, targetZ, Time.deltaTime * cameraLerpSpeed);
            followComponent.FollowOffset = currentOffset;

            virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, targetFOV, Time.deltaTime * cameraLerpSpeed);
        }

        // 5. Update Dust Particles
        if (speedDustParticles != null)
        {
            float normalizedDustEmission = Mathf.InverseLerp(dustSpeedThreshold, 1f, forwardSpeedPercent);

            var emission = speedDustParticles.emission;
            emission.rateOverTime = normalizedDustEmission * maxDustEmissionRate;
        }
    }

    private void UpdateVisualRotation()
    {
        if (visualEffects.visualModel == null) return;
        if (Time.deltaTime <= 0f) return;

        // Extract yaw and pitch components from world-space angular velocity
        // by projecting onto the ship's local axes.
        float yawAngVel   = Vector3.Dot(rb.angularVelocity, transform.up);
        float pitchAngVel = Vector3.Dot(rb.angularVelocity, transform.right);

        // Decompose linear acceleration into local axes.
        float forwardAccel = Vector3.Dot(_linearAcceleration, transform.forward);
        float lateralAccel = Vector3.Dot(_linearAcceleration, transform.right);

        // Banking: yaw angular velocity (spinning in place) + lateral linear acceleration (turning at speed).
        float targetBankAngle = Mathf.Clamp(
            (-yawAngVel * visualEffects.bankSensitivity) + (-lateralAccel * visualEffects.lateralAccelBankSensitivity),
            -visualEffects.maxBankAngle,
            visualEffects.maxBankAngle
        );

        // Pitch lean: pitch angular velocity + forward linear acceleration (thrust start/stop, braking).
        float targetPitchLeanAngle = Mathf.Clamp(
            (pitchAngVel * visualEffects.pitchLeanSensitivity) + (-forwardAccel * visualEffects.forwardAccelPitchSensitivity),
            -visualEffects.maxPitchLeanAngle,
            visualEffects.maxPitchLeanAngle
        );

        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBankAngle, Time.deltaTime * visualEffects.bankSmoothing);
        _currentPitchLeanAngle = Mathf.Lerp(_currentPitchLeanAngle, targetPitchLeanAngle, Time.deltaTime * visualEffects.pitchLeanSmoothing);

        // Combine: pitch lean around local X, bank around local Z, applied on top of the base local rotation.
        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchLeanAngle, Vector3.right);
        Quaternion bankQuat  = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        visualEffects.visualModel.localRotation = _visualBaseLocalRotation * pitchQuat * bankQuat;
    }
}
