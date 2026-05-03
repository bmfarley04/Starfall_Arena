using Unity.Netcode;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BlackHoleMenaceVisual3D : NetworkBehaviour
{
    private static readonly int InnerColorId = Shader.PropertyToID("_InnerColor");
    private static readonly int MidColorId = Shader.PropertyToID("_MidColor");
    private static readonly int OuterColorId = Shader.PropertyToID("_OuterColor");
    private static readonly int HotStreakColorId = Shader.PropertyToID("_HotStreakColor");

    [Header("References")]
    [Tooltip("Renderer using Starfall/3D/BlackHole/AccretionDisk. Only this renderer's accretion disk material is tinted by menace progress.")]
    [SerializeField] private Renderer accretionDiskRenderer;

    [Tooltip("Material slot on Accretion Disk Renderer that uses Starfall/3D/BlackHole/AccretionDisk.")]
    [SerializeField] [Min(0)] private int accretionDiskMaterialIndex;

    [Tooltip("Invasion wave manager that owns authored enemy totals and defeated-enemy progress. Required on the server/non-networked authority.")]
    [SerializeField] private InvasionWaveManager3D invasionWaveManager;

    [Header("Preview")]
    [Tooltip("Editor preview value for the black hole menace look. 0 uses the start palette; 1 uses the final palette.")]
    [SerializeField] [Range(0f, 1f)] private float previewMenacePercent;

    [Tooltip("If enabled, Preview Menace Percent is applied in edit mode through a renderer property block without changing the shared material asset.")]
    [SerializeField] private bool applyPreviewInEditMode = true;

    [Header("Progress Response")]
    [Tooltip("Maps defeated-enemy progress to visual menace. Use this to delay, accelerate, or ease the blue-to-red transition without changing kill counting.")]
    [SerializeField] private AnimationCurve menaceResponseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Start Palette")]
    [Tooltip("Start _MidColor copied from the authored blue accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color startMidColor = new Color(0f, 2.0155764f, 10.680627f, 1f);

    [Tooltip("Start _OuterColor copied from the authored blue accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color startOuterColor = new Color(0f, 0.5728299f, 1.8268172f, 1f);

    [Tooltip("Start _HotStreakColor copied from the authored blue accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color startHotStreakColor = new Color(0f, 1.0611426f, 3.3899353f, 1f);

    [Header("Final Palette")]
    [Tooltip("Final _MidColor copied from the authored red accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color finalMidColor = new Color(12.374387f, 0f, 0.040595904f, 1f);

    [Tooltip("Final _OuterColor copied from the authored red accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color finalOuterColor = new Color(1.826817f, 0f, 0.005250956f, 1f);

    [Tooltip("Final _HotStreakColor copied from the authored red accretion material.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color finalHotStreakColor = new Color(6.7798715f, 0f, 0.10718339f, 1f);

    [Header("Inner Disk")]
    [Tooltip("HDR intensity for the disk _InnerColor. The inner disk always stays pure white and only this intensity is applied.")]
    [SerializeField] [Min(0f)] private float innerWhiteHdrIntensity = 1.0694609f;

    private readonly NetworkVariable<float> _syncedMenaceProgress = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private MaterialPropertyBlock _previewPropertyBlock;
    private Material _runtimeDiskMaterial;
    private Material _originalDiskMaterial;
    private bool _runtimeMaterialAssigned;
    private bool _subscribedToWaveManager;
    private bool _loggedMissingRenderer;
    private bool _loggedMissingWaveManager;
    private bool _loggedMissingMaterial;
    private bool _loggedMissingShaderProperties;

    private void Reset()
    {
        AutoAssignReferences();
        ApplyPreviewMenace();
    }

    private void Awake()
    {
        AutoAssignReferences();
        if (Application.isPlaying)
        {
            EnsureRuntimeMaterialInstance();
        }
    }

    private void OnEnable()
    {
        AutoAssignReferences();

        if (Application.isPlaying)
        {
            EnsureRuntimeMaterialInstance();
            TrySubscribeToWaveManager();
            ApplyMenaceProgress(ResolveInitialRuntimeProgress());
        }
        else
        {
            ApplyPreviewMenace();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromWaveManager();

        if (!Application.isPlaying)
        {
            ClearPreviewPropertyBlock();
        }
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterial();
    }

    private void OnValidate()
    {
        accretionDiskMaterialIndex = Mathf.Max(0, accretionDiskMaterialIndex);
        previewMenacePercent = Mathf.Clamp01(previewMenacePercent);
        innerWhiteHdrIntensity = Mathf.Max(0f, innerWhiteHdrIntensity);
        EnsureResponseCurve();
        AutoAssignReferences();

        if (!Application.isPlaying)
        {
            ApplyPreviewMenace();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _syncedMenaceProgress.OnValueChanged += HandleSyncedMenaceProgressChanged;
        EnsureRuntimeMaterialInstance();

        if (IsServer)
        {
            TrySubscribeToWaveManager();
            ApplyAuthoritativeProgress(ResolveInitialRuntimeProgress());
        }
        else
        {
            ApplyMenaceProgress(_syncedMenaceProgress.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _syncedMenaceProgress.OnValueChanged -= HandleSyncedMenaceProgressChanged;
        UnsubscribeFromWaveManager();
        base.OnNetworkDespawn();
    }

    [ContextMenu("Apply Preview Menace")]
    private void ApplyPreviewMenace()
    {
        if (Application.isPlaying || !applyPreviewInEditMode)
        {
            return;
        }

        ApplyColors(previewMenacePercent, usePropertyBlock: true);
    }

    private void AutoAssignReferences()
    {
        if (accretionDiskRenderer == null)
        {
            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer candidate = childRenderers[i];
                if (RendererHasAccretionDiskMaterial(candidate))
                {
                    accretionDiskRenderer = candidate;
                    break;
                }
            }
        }

        if (invasionWaveManager == null)
        {
            invasionWaveManager = FindFirstObjectByType<InvasionWaveManager3D>();
#if UNITY_2022_3_OR_NEWER
#else
            if (invasionWaveManager == null)
            {
                invasionWaveManager = FindObjectOfType<InvasionWaveManager3D>();
            }
#endif
        }
    }

    private bool RendererHasAccretionDiskMaterial(Renderer candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Material[] materials = candidate.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || material.shader == null)
            {
                continue;
            }

            if (material.shader.name == "Starfall/3D/BlackHole/AccretionDisk")
            {
                accretionDiskMaterialIndex = i;
                return true;
            }
        }

        return false;
    }

    private void EnsureRuntimeMaterialInstance()
    {
        if (!Application.isPlaying || _runtimeMaterialAssigned)
        {
            return;
        }

        if (accretionDiskRenderer == null)
        {
            LogMissingRenderer();
            return;
        }

        Material[] sharedMaterials = accretionDiskRenderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            LogMissingMaterial();
            return;
        }

        int materialIndex = Mathf.Clamp(accretionDiskMaterialIndex, 0, sharedMaterials.Length - 1);
        Material sourceMaterial = sharedMaterials[materialIndex];
        if (sourceMaterial == null)
        {
            LogMissingMaterial();
            return;
        }

        _originalDiskMaterial = sourceMaterial;
        _runtimeDiskMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name} (Menace Runtime)"
        };

        sharedMaterials[materialIndex] = _runtimeDiskMaterial;
        accretionDiskRenderer.sharedMaterials = sharedMaterials;
        accretionDiskMaterialIndex = materialIndex;
        _runtimeMaterialAssigned = true;
        ClearPreviewPropertyBlock();
    }

    private void RestoreOriginalMaterial()
    {
        if (!_runtimeMaterialAssigned)
        {
            return;
        }

        if (accretionDiskRenderer != null && _originalDiskMaterial != null)
        {
            Material[] sharedMaterials = accretionDiskRenderer.sharedMaterials;
            if (sharedMaterials != null && accretionDiskMaterialIndex >= 0 && accretionDiskMaterialIndex < sharedMaterials.Length)
            {
                sharedMaterials[accretionDiskMaterialIndex] = _originalDiskMaterial;
                accretionDiskRenderer.sharedMaterials = sharedMaterials;
            }
        }

        if (_runtimeDiskMaterial != null)
        {
            Destroy(_runtimeDiskMaterial);
        }

        _runtimeDiskMaterial = null;
        _originalDiskMaterial = null;
        _runtimeMaterialAssigned = false;
    }

    private void TrySubscribeToWaveManager()
    {
        if (_subscribedToWaveManager || !HasProgressAuthority())
        {
            return;
        }

        if (invasionWaveManager == null)
        {
            LogMissingWaveManager();
            return;
        }

        invasionWaveManager.EnemyDefeatProgressChanged -= HandleEnemyDefeatProgressChanged;
        invasionWaveManager.EnemyDefeatProgressChanged += HandleEnemyDefeatProgressChanged;
        _subscribedToWaveManager = true;
    }

    private void UnsubscribeFromWaveManager()
    {
        if (!_subscribedToWaveManager || invasionWaveManager == null)
        {
            _subscribedToWaveManager = false;
            return;
        }

        invasionWaveManager.EnemyDefeatProgressChanged -= HandleEnemyDefeatProgressChanged;
        _subscribedToWaveManager = false;
    }

    private bool HasProgressAuthority()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return IsSpawned && IsServer;
    }

    private float ResolveInitialRuntimeProgress()
    {
        if (NetTickUtil.IsActive && IsSpawned)
        {
            return _syncedMenaceProgress.Value;
        }

        return invasionWaveManager != null ? invasionWaveManager.EnemyDefeatProgress01 : 0f;
    }

    private void HandleEnemyDefeatProgressChanged(int defeated, int total, float progress01)
    {
        ApplyAuthoritativeProgress(progress01);
    }

    private void ApplyAuthoritativeProgress(float progress01)
    {
        float clampedProgress = Mathf.Clamp01(progress01);

        if (NetTickUtil.IsActive && IsSpawned && IsServer && Mathf.Abs(_syncedMenaceProgress.Value - clampedProgress) > 0.0001f)
        {
            _syncedMenaceProgress.Value = clampedProgress;
        }

        ApplyMenaceProgress(clampedProgress);
    }

    private void HandleSyncedMenaceProgressChanged(float previousValue, float newValue)
    {
        ApplyMenaceProgress(newValue);
    }

    private void ApplyMenaceProgress(float progress01)
    {
        EnsureRuntimeMaterialInstance();
        ApplyColors(progress01, usePropertyBlock: false);
    }

    private void ApplyColors(float progress01, bool usePropertyBlock)
    {
        if (accretionDiskRenderer == null)
        {
            LogMissingRenderer();
            return;
        }

        float visualProgress = EvaluateVisualProgress(progress01);
        Color innerColor = new Color(innerWhiteHdrIntensity, innerWhiteHdrIntensity, innerWhiteHdrIntensity, 1f);
        Color midColor = Color.LerpUnclamped(startMidColor, finalMidColor, visualProgress);
        Color outerColor = Color.LerpUnclamped(startOuterColor, finalOuterColor, visualProgress);
        Color hotStreakColor = Color.LerpUnclamped(startHotStreakColor, finalHotStreakColor, visualProgress);

        if (usePropertyBlock)
        {
            ApplyColorsToPropertyBlock(innerColor, midColor, outerColor, hotStreakColor);
            return;
        }

        Material material = _runtimeDiskMaterial != null ? _runtimeDiskMaterial : ResolveCurrentSharedMaterial();
        if (material == null)
        {
            LogMissingMaterial();
            return;
        }

        if (!HasRequiredShaderProperties(material))
        {
            LogMissingShaderProperties();
            return;
        }

        material.SetColor(InnerColorId, innerColor);
        material.SetColor(MidColorId, midColor);
        material.SetColor(OuterColorId, outerColor);
        material.SetColor(HotStreakColorId, hotStreakColor);
    }

    private void ApplyColorsToPropertyBlock(Color innerColor, Color midColor, Color outerColor, Color hotStreakColor)
    {
        _previewPropertyBlock ??= new MaterialPropertyBlock();
        accretionDiskRenderer.GetPropertyBlock(_previewPropertyBlock, accretionDiskMaterialIndex);
        _previewPropertyBlock.SetColor(InnerColorId, innerColor);
        _previewPropertyBlock.SetColor(MidColorId, midColor);
        _previewPropertyBlock.SetColor(OuterColorId, outerColor);
        _previewPropertyBlock.SetColor(HotStreakColorId, hotStreakColor);
        accretionDiskRenderer.SetPropertyBlock(_previewPropertyBlock, accretionDiskMaterialIndex);
    }

    private void ClearPreviewPropertyBlock()
    {
        if (accretionDiskRenderer == null)
        {
            return;
        }

        accretionDiskRenderer.SetPropertyBlock(null, accretionDiskMaterialIndex);
    }

    private Material ResolveCurrentSharedMaterial()
    {
        if (accretionDiskRenderer == null)
        {
            return null;
        }

        Material[] sharedMaterials = accretionDiskRenderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            return null;
        }

        int materialIndex = Mathf.Clamp(accretionDiskMaterialIndex, 0, sharedMaterials.Length - 1);
        accretionDiskMaterialIndex = materialIndex;
        return sharedMaterials[materialIndex];
    }

    private bool HasRequiredShaderProperties(Material material)
    {
        return material.HasProperty(InnerColorId)
            && material.HasProperty(MidColorId)
            && material.HasProperty(OuterColorId)
            && material.HasProperty(HotStreakColorId);
    }

    private float EvaluateVisualProgress(float progress01)
    {
        float clampedProgress = Mathf.Clamp01(progress01);
        if (menaceResponseCurve == null || menaceResponseCurve.length == 0)
        {
            return clampedProgress;
        }

        return Mathf.Clamp01(menaceResponseCurve.Evaluate(clampedProgress));
    }

    private void EnsureResponseCurve()
    {
        if (menaceResponseCurve == null || menaceResponseCurve.length == 0)
        {
            menaceResponseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
    }

    private void LogMissingRenderer()
    {
        if (_loggedMissingRenderer)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(BlackHoleMenaceVisual3D)}] Accretion Disk Renderer is missing, so menace colors cannot be applied.", this);
        _loggedMissingRenderer = true;
    }

    private void LogMissingWaveManager()
    {
        if (_loggedMissingWaveManager)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(BlackHoleMenaceVisual3D)}] Invasion Wave Manager is missing on the authority, so runtime menace progress will stay at the start color.", this);
        _loggedMissingWaveManager = true;
    }

    private void LogMissingMaterial()
    {
        if (_loggedMissingMaterial)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(BlackHoleMenaceVisual3D)}] Accretion Disk Renderer has no material at index {accretionDiskMaterialIndex}.", this);
        _loggedMissingMaterial = true;
    }

    private void LogMissingShaderProperties()
    {
        if (_loggedMissingShaderProperties)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(BlackHoleMenaceVisual3D)}] Assigned accretion disk material is missing one or more black hole color properties.", this);
        _loggedMissingShaderProperties = true;
    }
}
