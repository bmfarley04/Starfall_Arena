using UnityEngine;

[DisallowMultipleComponent]
public class TriumvirateLightningLinkVisual3D : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("World-space transform where the cosmetic link begins.")]
    [SerializeField] private Transform startAnchor;
    [Tooltip("World-space transform where the cosmetic link ends.")]
    [SerializeField] private Transform endAnchor;

    [Header("Lightning Shape")]
    [Tooltip("Total line points including start and end. Higher values make the link more jagged.")]
    [SerializeField] private int pointCount = 4;
    [Tooltip("Maximum sideways offset for intermediate points in world units.")]
    [SerializeField] private float amplitude = 0.45f;
    [Tooltip("Seconds between link randomization steps.")]
    [SerializeField] private float jitterInterval = 0.05f;

    private LineRenderer _lineRenderer;
    private ParticleSystem[] _particles;
    private float _nextJitterTime;

    private void Awake()
    {
        _lineRenderer = GetComponentInChildren<LineRenderer>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
        DisablePrefabBeamRuntime();
    }

    private void OnEnable()
    {
        PlayParticles();
        _nextJitterTime = 0f;
    }

    private void OnDisable()
    {
        StopParticles();
    }

    private void LateUpdate()
    {
        if (startAnchor == null || endAnchor == null || _lineRenderer == null)
        {
            return;
        }

        Vector3 start = startAnchor.position;
        Vector3 end = endAnchor.position;
        Vector3 span = end - start;
        float distance = span.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 forward = span / distance;
        transform.position = start;
        transform.rotation = Quaternion.LookRotation(forward, ResolveUpVector(forward));

        int resolvedPointCount = Mathf.Max(2, pointCount);
        if (_lineRenderer.positionCount != resolvedPointCount)
        {
            _lineRenderer.positionCount = resolvedPointCount;
            _nextJitterTime = 0f;
        }

        _lineRenderer.useWorldSpace = false;
        _lineRenderer.SetPosition(0, Vector3.zero);
        _lineRenderer.SetPosition(resolvedPointCount - 1, new Vector3(0f, 0f, distance));

        if (Time.time < _nextJitterTime)
        {
            return;
        }

        _nextJitterTime = Time.time + Mathf.Max(0.01f, jitterInterval);
        float lastPointIndex = Mathf.Max(1f, resolvedPointCount - 1f);
        for (int i = 1; i < resolvedPointCount - 1; i++)
        {
            float z = distance * (i / lastPointIndex);
            float x = Random.Range(-amplitude, amplitude);
            float y = Random.Range(-amplitude, amplitude);
            _lineRenderer.SetPosition(i, new Vector3(x, y, z));
        }
    }

    public void Initialize(Transform start, Transform end, int linePointCount, float lineAmplitude, float lineJitterInterval)
    {
        startAnchor = start;
        endAnchor = end;
        pointCount = Mathf.Max(2, linePointCount);
        amplitude = Mathf.Max(0f, lineAmplitude);
        jitterInterval = Mathf.Max(0.01f, lineJitterInterval);
        _nextJitterTime = 0f;
    }

    private void DisablePrefabBeamRuntime()
    {
        ForgeEnemyBeam3D forgeBeam = GetComponent<ForgeEnemyBeam3D>();
        if (forgeBeam != null)
        {
            forgeBeam.enabled = false;
        }

        FORGE3D.F3DLightning forgeLightning = GetComponent<FORGE3D.F3DLightning>();
        if (forgeLightning != null)
        {
            forgeLightning.enabled = false;
        }
    }

    private void PlayParticles()
    {
        if (_particles == null)
        {
            return;
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null)
            {
                continue;
            }

            _particles[i].Clear(true);
            _particles[i].Play(true);
        }
    }

    private void StopParticles()
    {
        if (_particles == null)
        {
            return;
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] != null)
            {
                _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private static Vector3 ResolveUpVector(Vector3 forward)
    {
        Vector3 normalizedForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) > 0.98f)
        {
            return Vector3.right;
        }

        return Vector3.up;
    }
}
