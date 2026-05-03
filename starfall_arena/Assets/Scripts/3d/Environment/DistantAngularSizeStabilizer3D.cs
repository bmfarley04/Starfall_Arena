using UnityEngine;

/// <summary>
/// Counter-scales distant background set pieces so perspective camera yaw does not make them swell at screen edges.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class DistantAngularSizeStabilizer3D : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera used to compensate perspective edge growth. Leave empty to use Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("When enabled and Target Camera is empty, the component resolves Camera.main each frame.")]
    [SerializeField] private bool useMainCameraWhenTargetMissing = true;

    [Header("Base Scale")]
    [Tooltip("When enabled, the component captures the object's current local scale at runtime and uses that as the centered baseline. Keep this on for scene instances with scale overrides.")]
    [SerializeField] private bool captureCurrentScaleOnAwake = true;

    [Tooltip("Fallback local scale used when Capture Current Scale On Awake is disabled. Keep this equal to the authored prefab scale.")]
    [SerializeField] private Vector3 centeredLocalScale = Vector3.one;

    [Header("Perspective Compensation")]
    [Tooltip("How strongly the object counter-scales as it moves off the camera forward axis. 1 fully cancels perspective edge growth; 0 leaves normal perspective unchanged.")]
    [Range(0f, 1f)]
    [SerializeField] private float compensationStrength = 1f;

    [Tooltip("Smallest scale multiplier allowed when the object is far off-axis. Prevents the background object from collapsing near the edge of view.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float minimumScaleMultiplier = 0.35f;

    [Tooltip("Largest scale multiplier allowed. Values slightly above 1 allow a tiny amount of normal perspective variation if desired.")]
    [Range(1f, 2f)]
    [SerializeField] private float maximumScaleMultiplier = 1f;

    private Vector3 runtimeCenteredLocalScale;
    private bool hasRuntimeCenteredLocalScale;

    private void Reset()
    {
        centeredLocalScale = transform.localScale;
    }

    private void Awake()
    {
        if (captureCurrentScaleOnAwake)
        {
            CaptureRuntimeCenteredScale(transform.localScale);
        }
        else
        {
            CaptureRuntimeCenteredScale(centeredLocalScale);
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureRuntimeCenteredScale();
        ApplyScaleCompensation();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureRuntimeCenteredScale();
        ApplyScaleCompensation();
    }

    private void OnValidate()
    {
        minimumScaleMultiplier = Mathf.Clamp(minimumScaleMultiplier, 0.05f, 1f);
        maximumScaleMultiplier = Mathf.Max(maximumScaleMultiplier, 1f);
        if (maximumScaleMultiplier < minimumScaleMultiplier)
        {
            maximumScaleMultiplier = minimumScaleMultiplier;
        }
    }

    [ContextMenu("Capture Current Local Scale As Centered Scale")]
    private void CaptureCurrentLocalScaleAsCenteredScale()
    {
        centeredLocalScale = transform.localScale;
        CaptureRuntimeCenteredScale(centeredLocalScale);
    }

    private void ApplyScaleCompensation()
    {
        Vector3 baseScale = runtimeCenteredLocalScale;
        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null || cameraToUse.orthographic)
        {
            transform.localScale = baseScale;
            return;
        }

        Vector3 toObject = transform.position - cameraToUse.transform.position;
        float distance = toObject.magnitude;
        if (distance <= 0.0001f)
        {
            transform.localScale = baseScale;
            return;
        }

        float forwardDepth = Vector3.Dot(cameraToUse.transform.forward, toObject);
        if (forwardDepth <= 0.0001f)
        {
            transform.localScale = baseScale * minimumScaleMultiplier;
            return;
        }

        float forwardAlignment = Mathf.Clamp01(forwardDepth / distance);
        float scaleMultiplier = Mathf.Lerp(1f, forwardAlignment, compensationStrength);
        scaleMultiplier = Mathf.Clamp(scaleMultiplier, minimumScaleMultiplier, maximumScaleMultiplier);
        transform.localScale = baseScale * scaleMultiplier;
    }

    private void EnsureRuntimeCenteredScale()
    {
        if (!hasRuntimeCenteredLocalScale)
        {
            CaptureRuntimeCenteredScale(captureCurrentScaleOnAwake ? transform.localScale : centeredLocalScale);
        }
    }

    private void CaptureRuntimeCenteredScale(Vector3 scale)
    {
        runtimeCenteredLocalScale = scale;
        hasRuntimeCenteredLocalScale = true;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        if (useMainCameraWhenTargetMissing && Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            return Camera.main;
        }

        return null;
    }
}
