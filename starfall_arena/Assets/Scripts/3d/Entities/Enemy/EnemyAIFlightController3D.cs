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
    private Entity3D _entity;
    private Vector3 _moveDirection;
    private Vector3 _facingDirection;
    private float _speedScale;
    private bool _hasMoveIntent;
    private bool _hasFacingIntent;
    private bool _moveBackward;
    private bool _isMovingForward;
    private bool _isMovingBackward;

    public Vector3 MoveDirection => _hasMoveIntent ? _moveDirection : Vector3.zero;
    public float MoveSpeed => moveSpeed;
    public Vector3 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    public bool IsMovingForward => _isMovingForward;
    public bool IsMovingBackward => _isMovingBackward;
    public bool IsApplyingThrust => _isMovingForward || _isMovingBackward;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _networkObject = GetComponent<NetworkObject>();
        _entity = GetComponent<Entity3D>();
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

        if (_hasMoveIntent || _hasFacingIntent)
        {
            Quaternion nextRotation = RotateTowardDesiredFacing();
            if (_hasMoveIntent)
            {
                ApplyDeclaredVelocity(nextRotation);
            }
            else
            {
                StopMovement();
            }
        }
        else
        {
            StopMovement();
        }

        ApplyUprightRecovery();
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
        SetFlightIntent(worldDirection, worldDirection, speedScale, moveBackward: false);
    }

    public void SetFlightIntent(Vector3 moveDirection, Vector3 facingDirection, float speedScale, bool moveBackward)
    {
        bool hasMoveDirection = moveDirection.sqrMagnitude > 0.0001f;
        bool hasFacingDirection = facingDirection.sqrMagnitude > 0.0001f;
        if (!hasMoveDirection && !hasFacingDirection)
        {
            ClearFlightIntent();
            return;
        }

        _moveDirection = hasMoveDirection ? moveDirection.normalized : Vector3.zero;
        _facingDirection = hasFacingDirection ? facingDirection.normalized : _moveDirection;
        _speedScale = Mathf.Clamp01(speedScale);
        _hasMoveIntent = hasMoveDirection && _speedScale > 0f;
        _hasFacingIntent = hasFacingDirection || _hasMoveIntent;
        _moveBackward = _hasMoveIntent && moveBackward;
    }

    public void SetFacingDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            ClearFlightIntent();
            return;
        }

        _moveDirection = Vector3.zero;
        _facingDirection = worldDirection.normalized;
        _speedScale = 0f;
        _hasMoveIntent = false;
        _hasFacingIntent = true;
        _moveBackward = false;
    }

    public void ClearFlightIntent()
    {
        _moveDirection = Vector3.zero;
        _facingDirection = Vector3.zero;
        _speedScale = 0f;
        _hasMoveIntent = false;
        _hasFacingIntent = false;
        _moveBackward = false;
    }

    public void OverrideMoveSpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0f, newMoveSpeed);
    }

    public void ApplyProfile(EnemyBalanceProfile3D.CoreStats core)
    {
        moveSpeed = Mathf.Max(0f, core.moveSpeed);
        rotationDegreesPerSecond = Mathf.Max(0f, core.rotationDegreesPerSecond);
    }

    private Quaternion RotateTowardDesiredFacing()
    {
        Vector3 desiredFacing = _hasFacingIntent
            ? _facingDirection
            : _moveDirection;
        Quaternion targetRotation = ResolveTargetRotation(desiredFacing);
        Quaternion nextRotation = Quaternion.RotateTowards(
            _rb.rotation,
            targetRotation,
            GetEffectiveRotationDegreesPerSecond() * Time.fixedDeltaTime);

        _rb.MoveRotation(nextRotation);
        _rb.angularVelocity = Vector3.zero;
        return nextRotation;
    }

    private void ApplyDeclaredVelocity(Quaternion facingRotation)
    {
        Vector3 movementFacing = ((facingRotation * Vector3.forward) * (_moveBackward ? -1f : 1f)).normalized;
        float facingAngle = Vector3.Angle(movementFacing, _moveDirection);
        _rb.linearVelocity = facingAngle <= moveWhenFacingAngle
            ? movementFacing * (moveSpeed * _speedScale)
            : Vector3.zero;
        bool isMoving = _rb.linearVelocity.sqrMagnitude > 0.0001f;
        _isMovingForward = isMoving && !_moveBackward;
        _isMovingBackward = isMoving && _moveBackward;
        _rb.angularVelocity = Vector3.zero;
    }

    private void StopMovement()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _isMovingForward = false;
        _isMovingBackward = false;
    }

    private void ApplyUprightRecovery()
    {
        if (_entity == null || _rb == null)
        {
            return;
        }

        bool hasRotationIntent = _hasFacingIntent;
        if (!_entity.ShouldApplyUprightRecovery(hasRotationIntent))
        {
            return;
        }

        _rb.MoveRotation(_entity.ApplyUprightRecovery(_rb.rotation, Time.fixedDeltaTime, hasRotationIntent));
        _rb.angularVelocity = Vector3.zero;
    }

    private Quaternion ResolveTargetRotation(Vector3 desiredDirection)
    {
        Vector3 forward = desiredDirection.normalized;
        Vector3 upReference = Vector3.up;
        float worldUpAlignment = Mathf.Abs(Vector3.Dot(forward, upReference));

        if (worldUpAlignment >= 0.98f)
        {
            Vector3 currentUp = (_rb.rotation * Vector3.up).normalized;
            if (Mathf.Abs(Vector3.Dot(forward, currentUp)) < 0.98f)
            {
                upReference = currentUp;
            }
            else
            {
                Vector3 currentRight = (_rb.rotation * Vector3.right).normalized;
                upReference = Vector3.Cross(currentRight, forward);
                if (upReference.sqrMagnitude <= 0.0001f)
                {
                    upReference = Vector3.Cross(Vector3.forward, forward);
                }

                upReference = upReference.sqrMagnitude > 0.0001f
                    ? upReference.normalized
                    : Vector3.up;
            }
        }

        return Quaternion.LookRotation(forward, upReference);
    }

    private void ConfigureRigidbody()
    {
        _rb.useGravity = useGravity;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private float GetEffectiveRotationDegreesPerSecond()
    {
        float multiplier = _entity != null ? _entity.GetCombinedRotationMultiplier() : 1f;
        return Mathf.Max(0f, rotationDegreesPerSecond * Mathf.Max(0f, multiplier));
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
