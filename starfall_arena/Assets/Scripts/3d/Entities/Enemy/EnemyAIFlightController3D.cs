using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class EnemyAIFlightController3D : MonoBehaviour
{
    [Header("Simple Enemy Flight")]
    [SerializeField] private float moveSpeed = 35f;
    [SerializeField] private float rotationDegreesPerSecond = 180f;
    [SerializeField] private float moveWhenFacingAngle = 12f;
    [SerializeField] private bool useGravity;

    [Header("Plane Constraint")]
    [SerializeField] private bool lockToWorldYPlane;
    [SerializeField] private bool captureInitialWorldY = true;
    [SerializeField] private float lockedWorldY;

    private Rigidbody _rb;
    private NetworkObject _networkObject;
    private Vector3 _moveDirection;
    private float _speedScale;
    private bool _hasMoveIntent;

    public Vector3 MoveDirection => _hasMoveIntent ? _moveDirection : Vector3.zero;
    public float MoveSpeed => moveSpeed;
    public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _networkObject = GetComponent<NetworkObject>();
        ConfigureRigidbody();
        CacheLockedWorldYIfNeeded();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        rotationDegreesPerSecond = Mathf.Max(0f, rotationDegreesPerSecond);
        moveWhenFacingAngle = Mathf.Clamp(moveWhenFacingAngle, 0f, 180f);

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
        if (!HasMovementAuthority() || _rb == null || Time.fixedDeltaTime <= 0f)
        {
            return;
        }

        if (_hasMoveIntent)
        {
            RotateTowardMoveDirection();
            ApplyDeclaredVelocity();
        }
        else
        {
            StopMovement();
        }

        EnforceFlightPlane();
    }

    public void SetMoveDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            ClearFlightIntent();
            return;
        }

        SetMoveDirection(worldDirection, 1f);
    }

    public void SetMoveDirection(Vector3 worldDirection, float speedScale)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            ClearFlightIntent();
            return;
        }

        _moveDirection = worldDirection.normalized;
        _speedScale = Mathf.Clamp01(speedScale);
        _hasMoveIntent = true;
    }

    public void ClearFlightIntent()
    {
        _moveDirection = Vector3.zero;
        _speedScale = 0f;
        _hasMoveIntent = false;
    }

    private void RotateTowardMoveDirection()
    {
        Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            _rb.rotation,
            targetRotation,
            rotationDegreesPerSecond * Time.fixedDeltaTime);

        _rb.MoveRotation(nextRotation);
        _rb.angularVelocity = Vector3.zero;
    }

    private void ApplyDeclaredVelocity()
    {
        Vector3 forward = (_rb.rotation * Vector3.forward).normalized;
        float facingAngle = Vector3.Angle(forward, _moveDirection);
        _rb.linearVelocity = facingAngle <= moveWhenFacingAngle
            ? forward * (moveSpeed * _speedScale)
            : Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void StopMovement()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void ConfigureRigidbody()
    {
        _rb.useGravity = useGravity;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
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
        if (!lockToWorldYPlane)
        {
            return;
        }

        Vector3 velocity = _rb.linearVelocity;
        velocity.y = 0f;
        _rb.linearVelocity = velocity;

        Vector3 position = _rb.position;
        position.y = lockedWorldY;
        _rb.position = position;
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
