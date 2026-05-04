using UnityEngine;

public class WhiteDwarfStarVisual3D : MonoBehaviour
{
    private const string ExternalPulseIntensityName = "_ExternalPulseIntensity";

    [Header("Renderers")]
    [Tooltip("Renderer using Starfall/3D/WhiteDwarf/CoreSurface. Property blocks are used so the shared material is not duplicated at runtime.")]
    [SerializeField] private Renderer coreRenderer;

    [Tooltip("Renderer using Starfall/3D/WhiteDwarf/CoronaShell. Leave empty if the prefab uses only the core surface.")]
    [SerializeField] private Renderer coronaRenderer;

    [Tooltip("Optional renderer using Starfall/3D/WhiteDwarf/CompactLensing. This shell is disabled by distance unless the camera is near enough.")]
    [SerializeField] private Renderer lensingRenderer;

    [Header("Pulse")]
    [Tooltip("Base multiplier sent to all white dwarf materials through _ExternalPulseIntensity.")]
    [SerializeField] [Min(0f)] private float basePulseIntensity = 1f;

    [Tooltip("Additional pulse amplitude driven by this helper. Set to zero when the material-only shader pulse is enough.")]
    [SerializeField] [Min(0f)] private float helperPulseAmplitude = 0.15f;

    [Tooltip("Cycles per second for the helper-driven pulse multiplier.")]
    [SerializeField] [Min(0f)] private float helperPulseFrequency = 0.18f;

    [Header("Optional Lensing")]
    [Tooltip("If enabled, the lensing shell is shown only when the active camera is within Lensing Enable Distance.")]
    [SerializeField] private bool enableDistanceBasedLensing = true;

    [Tooltip("Camera distance, in world units, at which the optional lensing shell becomes visible.")]
    [SerializeField] [Min(0f)] private float lensingEnableDistance = 180f;

    [Tooltip("Extra distance beyond Lensing Enable Distance before the shell is hidden again, preventing visible flicker near the threshold.")]
    [SerializeField] [Min(0f)] private float lensingDisableHysteresis = 20f;

    private Camera _cachedCamera;
    private bool _isLensingVisible;

    private void Awake()
    {
        AutoAssignRenderers();
        _isLensingVisible = lensingRenderer != null && lensingRenderer.enabled;
    }

    private void OnEnable()
    {
        ApplyPulse();
        UpdateLensingVisibility();
    }

    private void Update()
    {
        ApplyPulse();
        UpdateLensingVisibility();
    }

    private void OnValidate()
    {
        basePulseIntensity = Mathf.Max(0f, basePulseIntensity);
        helperPulseAmplitude = Mathf.Max(0f, helperPulseAmplitude);
        helperPulseFrequency = Mathf.Max(0f, helperPulseFrequency);
        lensingEnableDistance = Mathf.Max(0f, lensingEnableDistance);
        lensingDisableHysteresis = Mathf.Max(0f, lensingDisableHysteresis);
    }

    private void AutoAssignRenderers()
    {
        if (coreRenderer != null && coronaRenderer != null && lensingRenderer != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null)
            {
                continue;
            }

            string objectName = candidate.gameObject.name;
            if (coreRenderer == null && objectName.Contains("Core"))
            {
                coreRenderer = candidate;
            }
            else if (coronaRenderer == null && objectName.Contains("Corona"))
            {
                coronaRenderer = candidate;
            }
            else if (lensingRenderer == null && objectName.Contains("Lensing"))
            {
                lensingRenderer = candidate;
            }
        }
    }

    private void ApplyPulse()
    {
        float pulse = basePulseIntensity;
        if (helperPulseAmplitude > 0f && helperPulseFrequency > 0f)
        {
            pulse += (0.5f + 0.5f * Mathf.Sin(Time.time * helperPulseFrequency * Mathf.PI * 2f)) * helperPulseAmplitude;
        }

        ApplyPulseToRenderer(coreRenderer, pulse);
        ApplyPulseToRenderer(coronaRenderer, pulse);
        ApplyPulseToRenderer(lensingRenderer, pulse);
    }

    private void ApplyPulseToRenderer(Renderer targetRenderer, float pulse)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            return;
        }

        targetRenderer.sharedMaterial.SetFloat(ExternalPulseIntensityName, pulse);
    }

    private void UpdateLensingVisibility()
    {
        if (lensingRenderer == null)
        {
            return;
        }

        if (!enableDistanceBasedLensing)
        {
            lensingRenderer.enabled = _isLensingVisible;
            return;
        }

        Camera targetCamera = ResolveCamera();
        if (targetCamera == null)
        {
            SetLensingVisible(false);
            return;
        }

        float distance = Vector3.Distance(targetCamera.transform.position, transform.position);
        float disableDistance = lensingEnableDistance + lensingDisableHysteresis;

        if (_isLensingVisible)
        {
            SetLensingVisible(distance <= disableDistance);
        }
        else
        {
            SetLensingVisible(distance <= lensingEnableDistance);
        }
    }

    private void SetLensingVisible(bool visible)
    {
        _isLensingVisible = visible;
        if (lensingRenderer != null)
        {
            lensingRenderer.enabled = visible;
        }
    }

    private Camera ResolveCamera()
    {
        if (_cachedCamera != null && _cachedCamera.isActiveAndEnabled)
        {
            return _cachedCamera;
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                _cachedCamera = candidate;
                return _cachedCamera;
            }
        }

        return null;
    }
}
