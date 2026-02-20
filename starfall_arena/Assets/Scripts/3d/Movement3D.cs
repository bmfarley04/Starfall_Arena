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

    private Rigidbody rb;
    private CinemachineFollow followComponent;
    private Vector2 lookInput;
    private float thrustInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.useGravity = false;
        
        // Initialize with friction off
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate; 

        if (virtualCamera != null)
        {
            followComponent = virtualCamera.GetComponent<CinemachineFollow>();
        }
    }

    // --- Input System Callbacks ---

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
        // value.isPressed ensures this only fires on button down, ignoring the button release
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

    // --- Physics Application ---

    private void FixedUpdate()
    {
        HandleRotation();
        HandleThrust();
    }

    private void Update()
    {
        HandleDynamicCamera();
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

    private void HandleDynamicCamera()
    {
        if (virtualCamera == null || followComponent == null) return;

        float speedPercent = rb.linearVelocity.magnitude / maxSpeed;

        float targetZ = Mathf.Lerp(minZOffset, maxZOffset, speedPercent);
        float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedPercent);

        Vector3 currentOffset = followComponent.FollowOffset;
        currentOffset.z = Mathf.Lerp(currentOffset.z, targetZ, Time.deltaTime * cameraLerpSpeed);
        followComponent.FollowOffset = currentOffset;

        virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, targetFOV, Time.deltaTime * cameraLerpSpeed);
    }
}