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

    public void Scatter(Vector2 damageDirection)
    {
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
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
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
        StartCoroutine(PartLifecycle());
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

        while (elapsed < lifetime)
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

            // Scale shrinking phase
            if (elapsed >= shrinkStartDelay)
            {
                float shrinkProgress = (elapsed - shrinkStartDelay) / shrinkDuration;
                float scale = Mathf.Lerp(1f, 0f, shrinkProgress);
                transform.localScale = initialScale * scale;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
