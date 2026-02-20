using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; 

[RequireComponent(typeof(Rigidbody))]
public class ShipController : MonoBehaviour
{
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

    private Rigidbody rb;
    private CinemachineFollow followComponent;
    private Vector2 lookInput;
    private float thrustInput;

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
    }

    private void Update()
    {
        HandleVisuals();
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
}