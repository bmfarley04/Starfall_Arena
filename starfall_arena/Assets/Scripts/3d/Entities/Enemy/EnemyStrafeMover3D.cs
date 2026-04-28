using Unity.Netcode;
using UnityEngine;

// Runs after the flight controller so its velocity write happens last and can
// either replace or supplement the flight controller's forward thrust.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class EnemyStrafeMover3D : MonoBehaviour
{
    [Header("Strafe Limits")]
    [Tooltip("Hard upper bound on the world-space strafe speed (m/s) this mover will apply, regardless of what the brain requests. Acts as a per-prefab safety cap so brain bugs cannot launch the enemy at absurd speeds.")]
    [SerializeField] private float maxStrafeSpeed = 50f;

    [Header("Composition")]
    [Tooltip("If true, the strafe velocity is added on top of whatever EnemyAIFlightController3D already wrote to the rigidbody this physics step (slide while still thrusting forward). If false, the strafe velocity replaces the flight controller's velocity entirely.")]
    [SerializeField] private bool combineWithFlightThrust = true;

    [Header("Plane Constraint")]
    [Tooltip("If true, vertical (world Y) component of the strafe velocity is forced to zero. Match this to EnemyAIFlightController3D.lockToWorldYPlane on the same prefab if that flag is on.")]
    [SerializeField] private bool lockToWorldYPlane;

    private Rigidbody _rb;
    private NetworkObject _networkObject;

    private Vector3 _strafeVelocity;
    private float _strafeEndsAt;
    private bool _isStrafing;

    public bool IsStrafing => _isStrafing;
    public float StrafeEndsAt => _strafeEndsAt;
    public Vector3 CurrentStrafeVelocity => _isStrafing ? _strafeVelocity : Vector3.zero;
    public float MaxStrafeSpeed => maxStrafeSpeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnValidate()
    {
        maxStrafeSpeed = Mathf.Max(0f, maxStrafeSpeed);
    }

    private void OnDisable()
    {
        StopStrafe();
    }

    public void BeginStrafe(Vector3 worldVelocity, float durationSeconds)
    {
        if (durationSeconds <= 0f || worldVelocity.sqrMagnitude <= 0.0001f)
        {
            StopStrafe();
            return;
        }

        Vector3 clamped = Vector3.ClampMagnitude(worldVelocity, Mathf.Max(0f, maxStrafeSpeed));
        if (lockToWorldYPlane)
        {
            clamped.y = 0f;
        }

        if (clamped.sqrMagnitude <= 0.0001f)
        {
            StopStrafe();
            return;
        }

        _strafeVelocity = clamped;
        _strafeEndsAt = Time.time + durationSeconds;
        _isStrafing = true;
    }

    public void StopStrafe()
    {
        _strafeVelocity = Vector3.zero;
        _strafeEndsAt = 0f;
        _isStrafing = false;
    }

    private void FixedUpdate()
    {
        if (!_isStrafing)
        {
            return;
        }

        if (Time.time >= _strafeEndsAt)
        {
            StopStrafe();
            return;
        }

        if (!HasMovementAuthority() || _rb == null)
        {
            return;
        }

        Vector3 nextVelocity = combineWithFlightThrust
            ? _rb.linearVelocity + _strafeVelocity
            : _strafeVelocity;

        if (lockToWorldYPlane)
        {
            nextVelocity.y = 0f;
        }

        _rb.linearVelocity = nextVelocity;
        _rb.angularVelocity = Vector3.zero;
    }

    private bool HasMovementAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null || !_networkObject.IsSpawned)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
    }
}
