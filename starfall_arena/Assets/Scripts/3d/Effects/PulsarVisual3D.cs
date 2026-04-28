using UnityEngine;

public class PulsarVisual3D : MonoBehaviour
{
    private const string ExternalPulseIntensityName = "_ExternalPulseIntensity";
    [Header("Rotation")]
    [Tooltip("Optional transform to rotate. Leave empty to rotate this GameObject, including the core, jets, and trigger volumes together.")]
    [SerializeField] private Transform rotationRoot;

    [Tooltip("Local-space axis used for the pulsar spin. The default Y axis matches the prefab's polar jet direction.")]
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;

    [Tooltip("Degrees per second applied to Rotation Root so visuals and trigger volumes share the same beam direction.")]
    [SerializeField] [Min(0f)] private float rotationDegreesPerSecond = 18f;

    [Header("Renderers")]
    [Tooltip("Renderer using Starfall/3D/Pulsar/CoreSurface. Property blocks keep the shared material asset reusable.")]
    [SerializeField] private Renderer coreRenderer;

    [Tooltip("Renderer for the positive-pole jet using Starfall/3D/Pulsar/Jet.")]
    [SerializeField] private Renderer northJetRenderer;

    [Tooltip("Renderer for the negative-pole jet using Starfall/3D/Pulsar/Jet.")]
    [SerializeField] private Renderer southJetRenderer;

    [Header("Pulse")]
    [Tooltip("Base multiplier sent to pulsar materials through _ExternalPulseIntensity.")]
    [SerializeField] [Min(0f)] private float basePulseIntensity = 1f;

    [Tooltip("Additional script-driven pulse amplitude layered on top of the material's own pulse.")]
    [SerializeField] [Min(0f)] private float helperPulseAmplitude = 0.12f;

    [Tooltip("Cycles per second for the script-driven pulse multiplier.")]
    [SerializeField] [Min(0f)] private float helperPulseFrequency = 0.22f;

    [Header("Jet Gameplay Query")]
    [Tooltip("Positive-pole jet transform. Used only by IsPointInsideJets and gizmo previews; trigger colliders still own physics callbacks.")]
    [SerializeField] private Transform northJetTransform;

    [Tooltip("Negative-pole jet transform. Used only by IsPointInsideJets and gizmo previews; trigger colliders still own physics callbacks.")]
    [SerializeField] private Transform southJetTransform;

    [Tooltip("Approximate radius of the bright core in world units, used as the starting offset for dot/radius jet queries.")]
    [SerializeField] [Min(0f)] private float gameplayCoreRadius = 12f;

    [Tooltip("Approximate usable beam length beyond the core in world units for lightweight dot/radius hit checks.")]
    [SerializeField] [Min(0f)] private float gameplayJetLength = 120f;

    [Tooltip("Approximate beam radius in world units for lightweight dot/radius hit checks.")]
    [SerializeField] [Min(0f)] private float gameplayJetRadius = 8f;

    public float RotationDegreesPerSecond => rotationDegreesPerSecond;
    public float GameplayJetLength => gameplayJetLength;
    public float GameplayJetRadius => gameplayJetRadius;

    private void Reset()
    {
        rotationRoot = transform;
        AutoAssignReferences();
    }

    private void Awake()
    {
        if (rotationRoot == null)
        {
            rotationRoot = transform;
        }

        AutoAssignReferences();
    }

    private void OnEnable()
    {
        ApplyMaterialProperties();
    }

    private void Update()
    {
        ApplyRotation();
        ApplyMaterialProperties();
    }

    private void OnValidate()
    {
        rotationDegreesPerSecond = Mathf.Max(0f, rotationDegreesPerSecond);
        basePulseIntensity = Mathf.Max(0f, basePulseIntensity);
        helperPulseAmplitude = Mathf.Max(0f, helperPulseAmplitude);
        helperPulseFrequency = Mathf.Max(0f, helperPulseFrequency);
        gameplayCoreRadius = Mathf.Max(0f, gameplayCoreRadius);
        gameplayJetLength = Mathf.Max(0f, gameplayJetLength);
        gameplayJetRadius = Mathf.Max(0f, gameplayJetRadius);

        if (rotationRoot == null)
        {
            rotationRoot = transform;
        }

        AutoAssignReferences();
    }

    public bool IsPointInsideJets(Vector3 worldPoint, out float bestAlignment, out float normalizedDistance)
    {
        bestAlignment = 0f;
        normalizedDistance = 0f;

        bool insideNorth = IsPointInsideJet(worldPoint, northJetTransform, 1f, out float northAlignment, out float northDistance);
        bool insideSouth = IsPointInsideJet(worldPoint, southJetTransform, -1f, out float southAlignment, out float southDistance);

        if (insideNorth && (!insideSouth || northAlignment >= southAlignment))
        {
            bestAlignment = northAlignment;
            normalizedDistance = northDistance;
            return true;
        }

        if (insideSouth)
        {
            bestAlignment = southAlignment;
            normalizedDistance = southDistance;
            return true;
        }

        bestAlignment = Mathf.Max(northAlignment, southAlignment);
        normalizedDistance = northAlignment >= southAlignment ? northDistance : southDistance;
        return false;
    }

    private void ApplyRotation()
    {
        if (rotationRoot == null || rotationDegreesPerSecond <= 0f)
        {
            return;
        }

        Vector3 axis = localRotationAxis.sqrMagnitude > 0.0001f ? localRotationAxis.normalized : Vector3.up;
        rotationRoot.Rotate(axis, rotationDegreesPerSecond * Time.deltaTime, Space.Self);
    }

    private void ApplyMaterialProperties()
    {
        float pulse = basePulseIntensity;
        if (helperPulseAmplitude > 0f && helperPulseFrequency > 0f)
        {
            pulse += (0.5f + 0.5f * Mathf.Sin(Time.time * helperPulseFrequency * Mathf.PI * 2f)) * helperPulseAmplitude;
        }

        ApplyPulseToRenderer(coreRenderer, pulse);
        ApplyPulseToRenderer(northJetRenderer, pulse);
        ApplyPulseToRenderer(southJetRenderer, pulse);
    }

    private void ApplyPulseToRenderer(Renderer targetRenderer, float pulse)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            return;
        }

        targetRenderer.sharedMaterial.SetFloat(ExternalPulseIntensityName, pulse);
    }

    private bool IsPointInsideJet(Vector3 worldPoint, Transform jetTransform, float outwardSign, out float alignment, out float normalizedDistance)
    {
        alignment = 0f;
        normalizedDistance = 0f;

        if (jetTransform == null || gameplayJetLength <= 0f || gameplayJetRadius <= 0f)
        {
            return false;
        }

        Vector3 direction = jetTransform.up * Mathf.Sign(outwardSign);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        Vector3 basePoint = transform.position + direction * gameplayCoreRadius;
        Vector3 toPoint = worldPoint - basePoint;
        float distanceToPoint = toPoint.magnitude;
        if (distanceToPoint <= 0.0001f)
        {
            alignment = 1f;
            return true;
        }

        float axialDistance = Vector3.Dot(toPoint, direction);
        normalizedDistance = Mathf.Clamp01(axialDistance / gameplayJetLength);
        alignment = Mathf.Clamp01(axialDistance / distanceToPoint);

        if (axialDistance < 0f || axialDistance > gameplayJetLength)
        {
            return false;
        }

        Vector3 radialOffset = toPoint - direction * axialDistance;
        return radialOffset.sqrMagnitude <= gameplayJetRadius * gameplayJetRadius;
    }

    private void AutoAssignReferences()
    {
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
            else if (northJetRenderer == null && objectName.Contains("NorthJet"))
            {
                northJetRenderer = candidate;
            }
            else if (southJetRenderer == null && objectName.Contains("SouthJet"))
            {
                southJetRenderer = candidate;
            }
        }

        if (northJetTransform == null && northJetRenderer != null)
        {
            northJetTransform = northJetRenderer.transform;
        }

        if (southJetTransform == null && southJetRenderer != null)
        {
            southJetTransform = southJetRenderer.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        DrawJetGizmo(northJetTransform, 1f);
        DrawJetGizmo(southJetTransform, -1f);
    }

    private void DrawJetGizmo(Transform jetTransform, float outwardSign)
    {
        if (jetTransform == null || gameplayJetLength <= 0f)
        {
            return;
        }

        Vector3 direction = jetTransform.up * Mathf.Sign(outwardSign);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        Vector3 basePoint = transform.position + direction * gameplayCoreRadius;
        Vector3 tipPoint = basePoint + direction * gameplayJetLength;

        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.65f);
        Gizmos.DrawLine(basePoint, tipPoint);
        Gizmos.DrawWireSphere(basePoint, gameplayJetRadius);
        Gizmos.DrawWireSphere(tipPoint, gameplayJetRadius * 0.65f);
    }
}
