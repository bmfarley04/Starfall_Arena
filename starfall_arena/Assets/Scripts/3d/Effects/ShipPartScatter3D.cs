using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ShipPartScatter3D : MonoBehaviour
{
    [Header("Scatter Physics")]
    [Tooltip("Minimum scatter force magnitude.")]
    [SerializeField] private float minScatterForce = 5f;
    [Tooltip("Maximum scatter force magnitude.")]
    [SerializeField] private float maxScatterForce = 10f;
    [Tooltip("Horizontal cone angle in degrees around the incoming damage direction.")]
    [SerializeField] private float horizontalScatterAngle = 45f;
    [Tooltip("Vertical cone angle in degrees around the incoming damage direction.")]
    [SerializeField] private float verticalScatterAngle = 20f;
    [Tooltip("Extra upward velocity added after the cone direction is chosen.")]
    [SerializeField] private float upwardVelocityBias = 1f;

    [Header("Rotation")]
    [Tooltip("Minimum angular velocity in degrees per second.")]
    [SerializeField] private float minAngularVelocity = 90f;
    [Tooltip("Maximum angular velocity in degrees per second.")]
    [SerializeField] private float maxAngularVelocity = 360f;

    [Header("Visual Lifetime")]
    [Tooltip("Part lifetime before despawn.")]
    [SerializeField] private float lifetime = 8f;
    [Tooltip("When to start shrinking the detached part.")]
    [SerializeField] [Range(0f, 1f)] private float shrinkStartTime = 0.95f;

    [Header("Physics Setup")]
    [Tooltip("Rigidbody mass.")]
    [SerializeField] private float mass = 0.5f;
    [Tooltip("Linear damping after the part detaches.")]
    [SerializeField] private float linearDamping = 0.5f;
    [Tooltip("Angular damping after the part detaches.")]
    [SerializeField] private float angularDamping = 0.3f;

    private Rigidbody _rb;
    private bool _persistAfterScatter;
    private bool _hasScattered;
    private Coroutine _lifecycleCoroutine;

    public void SetPersistAfterScatter(bool persistAfterScatter)
    {
        _persistAfterScatter = persistAfterScatter;
    }

    public void Scatter(Vector3 damageDirection, Vector3 inheritedVelocity)
    {
        if (_hasScattered)
        {
            return;
        }

        _hasScattered = true;
        transform.SetParent(null, true);

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        ConfigureRigidbody();
        DisableColliders();

        Vector3 scatterDirection = ResolveScatterDirection(damageDirection);
        float scatterForce = Random.Range(minScatterForce, maxScatterForce);
        _rb.linearVelocity = inheritedVelocity + scatterDirection * scatterForce;
        _rb.angularVelocity = Random.onUnitSphere * Random.Range(minAngularVelocity, maxAngularVelocity) * Mathf.Deg2Rad;

        if (_lifecycleCoroutine != null)
        {
            StopCoroutine(_lifecycleCoroutine);
        }

        _lifecycleCoroutine = StartCoroutine(PartLifecycle());
    }

    private void ConfigureRigidbody()
    {
        _rb.mass = mass;
        _rb.linearDamping = linearDamping;
        _rb.angularDamping = angularDamping;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void DisableColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    private Vector3 ResolveScatterDirection(Vector3 damageDirection)
    {
        Vector3 baseDirection = damageDirection.sqrMagnitude > 0.0001f ? damageDirection.normalized : Random.onUnitSphere;
        Vector3 upAxis = Mathf.Abs(Vector3.Dot(baseDirection, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        Quaternion basis = Quaternion.LookRotation(baseDirection, upAxis);
        Quaternion scatterOffset = Quaternion.Euler(
            Random.Range(-verticalScatterAngle, verticalScatterAngle),
            Random.Range(-horizontalScatterAngle, horizontalScatterAngle),
            0f);

        Vector3 scatterDirection = basis * scatterOffset * Vector3.forward;
        scatterDirection += Vector3.up * upwardVelocityBias;

        if (scatterDirection.sqrMagnitude <= 0.0001f)
        {
            scatterDirection = Random.onUnitSphere;
        }

        return scatterDirection.normalized;
    }

    private IEnumerator PartLifecycle()
    {
        Vector3 initialScale = transform.localScale;
        float elapsed = 0f;
        float shrinkStartDelay = lifetime * shrinkStartTime;
        float shrinkDuration = Mathf.Max(0.0001f, lifetime - shrinkStartDelay);

        while (_persistAfterScatter || elapsed < lifetime)
        {
            elapsed += Time.deltaTime;

            if (_persistAfterScatter)
            {
                yield return null;
                continue;
            }

            if (elapsed >= shrinkStartDelay)
            {
                float shrinkProgress = Mathf.Clamp01((elapsed - shrinkStartDelay) / shrinkDuration);
                transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, shrinkProgress);
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
