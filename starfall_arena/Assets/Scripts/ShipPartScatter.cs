using System.Collections;
using UnityEngine;

public class ShipPartScatter : MonoBehaviour
{
    [Header("Scatter Physics")]
    [Tooltip("Minimum scatter force magnitude")]
    public float minScatterForce = 5f;

    [Tooltip("Maximum scatter force magnitude")]
    public float maxScatterForce = 10f;

    [Tooltip("Cone angle in degrees around damage direction (±30 = 60° cone)")]
    public float scatterConeAngle = 45f;

    [Header("Rotation")]
    [Tooltip("Minimum angular velocity per axis (deg/s)")]
    public float minAngularVelocity = 90f;

    [Tooltip("Maximum angular velocity per axis (deg/s)")]
    public float maxAngularVelocity = 360f;

    private Vector3 _rotationVelocity;
    private Vector3 _initialRotationVelocity;
    private Rigidbody2D _rb;
    private bool _persistAfterScatter;
    private Coroutine _lifecycleCoroutine;
    private Coroutine _regroupCoroutine;
    private bool _isScattered;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;
    private Transform _anchorTransform;
    private Vector3 _anchorLocalPosition;
    private Quaternion _anchorLocalRotation;
    private Collider2D[] _colliders;
    private bool[] _originalColliderStates;

    [Header("Visual Effect")]
    [Tooltip("Part lifetime before despawn (seconds)")]
    public float lifetime = 8f;

    [Tooltip("When to start shrinking (0 = immediately, 1 = at end)")]
    [Range(0f, 1f)]
    public float shrinkStartTime = 0.95f;

    [Header("Physics Setup")]
    [Tooltip("Rigidbody2D mass")]
    public float mass = 0.5f;

    [Tooltip("Linear drag")]
    public float drag = 0.5f;

    [Tooltip("Angular drag")]
    public float angularDrag = 0.3f;

    private void Awake()
    {
        CacheOriginalTransformData();
    }

    private void CacheOriginalTransformData()
    {
        _originalParent = transform.parent;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
        _originalLocalScale = transform.localScale;

        Transform playerRoot = GetComponentInParent<Player>()?.transform;
        Transform entityRoot = GetComponentInParent<Entity>()?.transform;
        _anchorTransform = playerRoot != null ? playerRoot : (entityRoot != null ? entityRoot : _originalParent);

        if (_anchorTransform != null)
        {
            _anchorLocalPosition = _anchorTransform.InverseTransformPoint(transform.position);
            _anchorLocalRotation = Quaternion.Inverse(_anchorTransform.rotation) * transform.rotation;
        }
        else
        {
            _anchorLocalPosition = transform.position;
            _anchorLocalRotation = transform.rotation;
        }

        _colliders = GetComponents<Collider2D>();
        _originalColliderStates = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++)
        {
            _originalColliderStates[i] = _colliders[i] != null && _colliders[i].enabled;
        }
    }

    public void SetPersistAfterScatter(bool persistAfterScatter)
    {
        _persistAfterScatter = persistAfterScatter;
    }

    public void Scatter(Vector2 damageDirection)
    {
        if (_regroupCoroutine != null)
        {
            StopCoroutine(_regroupCoroutine);
            _regroupCoroutine = null;
        }

        if (_lifecycleCoroutine != null)
        {
            StopCoroutine(_lifecycleCoroutine);
            _lifecycleCoroutine = null;
        }

        // 1. DETACH from parent hierarchy
        transform.SetParent(null);

        // 1.5 MOVE Z position up to ensure parts are behind the player
        transform.position += Vector3.forward * 10f;

        // 2. ADD Rigidbody2D for physics
        _rb = gameObject.GetComponent<Rigidbody2D>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody2D>();
        }

        _rb.mass = mass;
        _rb.linearDamping = drag;
        _rb.angularDamping = angularDrag;
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.None; // Allow rotation for tumbling

        // 3. DISABLE all colliders (visual only)
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = false;
            }
        }

        // 4. CALCULATE scatter velocity
        float randomAngle = Random.Range(-scatterConeAngle, scatterConeAngle);
        Vector2 scatterDir = Rotate(damageDirection, randomAngle);
        float forceMagnitude = Random.Range(minScatterForce, maxScatterForce);

        _rb.linearVelocity = scatterDir * forceMagnitude;

        // 5. SET random 3D angular velocity (tumble effect on all axes)
        _rotationVelocity = new Vector3(
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1 : 1),
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1 : 1),
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1 : 1)
        );
        _initialRotationVelocity = _rotationVelocity;

        // 6. START lifecycle coroutine
        _isScattered = true;
        _lifecycleCoroutine = StartCoroutine(PartLifecycle());
    }

    public void ScatterForRevive(Vector2 damageDirection)
    {
        SetPersistAfterScatter(true);
        Scatter(damageDirection);
    }

    public void RegroupToOriginal(float duration)
    {
        if (!_isScattered)
        {
            return;
        }

        if (_regroupCoroutine != null)
        {
            StopCoroutine(_regroupCoroutine);
        }

        _regroupCoroutine = StartCoroutine(RegroupRoutine(Mathf.Max(0.01f, duration)));
    }

    private Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    private IEnumerator PartLifecycle()
    {
        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;
        float shrinkStartDelay = lifetime * shrinkStartTime;
        float shrinkDuration = lifetime * (1f - shrinkStartTime);
        float initialSpeed = _rb.linearVelocity.magnitude;

        while (_persistAfterScatter || elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            // Scale rotation velocity based on current speed vs initial speed
            if (_rb != null && initialSpeed > 0)
            {
                float currentSpeed = _rb.linearVelocity.magnitude;
                float speedRatio = currentSpeed / initialSpeed;
                _rotationVelocity = _initialRotationVelocity * speedRatio;
            }

            // Apply 3D rotation manually
            transform.Rotate(_rotationVelocity * Time.deltaTime, Space.World);

            if (_persistAfterScatter)
            {
                yield return null;
                continue;
            }

            // Scale shrinking phase
            if (elapsed >= shrinkStartDelay)
            {
                float shrinkProgress = (elapsed - shrinkStartDelay) / shrinkDuration;
                float scale = Mathf.Lerp(1f, 0f, shrinkProgress);
                transform.localScale = initialScale * scale;
            }

            yield return null;
        }

        _lifecycleCoroutine = null;
        Destroy(gameObject);
    }

    private IEnumerator RegroupRoutine(float duration)
    {
        if (_lifecycleCoroutine != null)
        {
            StopCoroutine(_lifecycleCoroutine);
            _lifecycleCoroutine = null;
        }

        _persistAfterScatter = true;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.simulated = false;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 targetPos;
            Quaternion targetRot;

            if (_originalParent != null)
            {
                targetPos = _originalParent.TransformPoint(_originalLocalPosition);
                targetRot = _originalParent.rotation * _originalLocalRotation;
            }
            else if (_anchorTransform != null)
            {
                targetPos = _anchorTransform.TransformPoint(_anchorLocalPosition);
                targetRot = _anchorTransform.rotation * _anchorLocalRotation;
            }
            else
            {
                targetPos = _originalLocalPosition;
                targetRot = _originalLocalRotation;
            }

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            transform.localScale = Vector3.Lerp(startScale, _originalLocalScale, t);

            yield return null;
        }

        Transform regroupParent = ResolveRegroupParent();
        if (regroupParent != null)
        {
            transform.SetParent(regroupParent, false);

            if (_originalParent != null && regroupParent == _originalParent)
            {
                transform.localPosition = _originalLocalPosition;
                transform.localRotation = _originalLocalRotation;
            }
            else if (_anchorTransform != null)
            {
                transform.position = _anchorTransform.TransformPoint(_anchorLocalPosition);
                transform.rotation = _anchorTransform.rotation * _anchorLocalRotation;
            }
        }
        else
        {
            transform.position = _originalLocalPosition;
            transform.rotation = _originalLocalRotation;
        }

        transform.localScale = _originalLocalScale;

        if (_rb != null)
        {
            Destroy(_rb);
            _rb = null;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = i < _originalColliderStates.Length && _originalColliderStates[i];
            }
        }

        _rotationVelocity = Vector3.zero;
        _initialRotationVelocity = Vector3.zero;
        _persistAfterScatter = false;
        _isScattered = false;
        _regroupCoroutine = null;
    }

    private Transform ResolveRegroupParent()
    {
        if (_originalParent != null)
        {
            return _originalParent;
        }

        return _anchorTransform;
    }

    private static bool IsDescendantOf(Transform child, Transform ancestor)
    {
        if (child == null || ancestor == null)
        {
            return false;
        }

        Transform current = child;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
