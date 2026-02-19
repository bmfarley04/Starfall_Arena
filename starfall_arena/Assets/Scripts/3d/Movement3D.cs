using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Movement3D : MonoBehaviour
{
    [Header("Engine Parameters")]
    [SerializeField] private float thrustAcceleration = 50f;
    [SerializeField] private float maxSpeed = 100f;

    [Header("Handling Parameters")]
    [SerializeField] private float pitchSpeed = 2.5f;
    [SerializeField] private float yawSpeed = 2.5f;
    [SerializeField] private bool invertY = true;

    private Rigidbody rb;
    private Vector2 lookInput;
    private float thrustInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Enforcing a frictionless space environment using the modern API
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        
        rb.interpolation = RigidbodyInterpolation.Interpolate; 
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

    // --- Physics Application ---

    private void FixedUpdate()
    {
        HandleRotation();
        HandleThrust();
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
            // Accumulate linearVelocity manually to simulate thrust over time
            rb.linearVelocity += transform.forward * (thrustInput * thrustAcceleration * Time.fixedDeltaTime);
        }

        // Clamp the absolute velocity vector to enforce the maximum speed limit
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
}