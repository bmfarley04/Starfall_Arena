using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileChargeTelegraph3D : MonoBehaviour
{
    private const string DefaultEmissionColorProperty = "_EmissionColor";

    [Header("Renderers")]
    [Tooltip("Renderers that should glow during this projectile or missile charge tell. If empty and Auto Collect Child Renderers is enabled, all child renderers are used.")]
    [SerializeField] private Renderer[] chargeRenderers;

    [Tooltip("If true, the component fills Charge Renderers from child renderers when no renderers are explicitly assigned.")]
    [SerializeField] private bool autoCollectChildRenderers = true;

    [Tooltip("Shader color property used for emission. URP/Lit and Standard materials normally use _EmissionColor.")]
    [SerializeField] private string emissionColorProperty = DefaultEmissionColorProperty;

    [Header("Emission")]
    [Tooltip("Optional warning color used only when Use Charge Color Override is enabled.")]
    [SerializeField] private Color chargeEmissionColor = new Color(1f, 0.35f, 0.08f, 1f);

    [Tooltip("If true, the charge ramp uses Charge Emission Color. If false, the ramp preserves each renderer's shared-material emission color and only changes intensity.")]
    [SerializeField] private bool useChargeColorOverride;

    [Tooltip("If true, charge emission is added on top of the shared material's authored emission. If false, this renderer gets its own local emission range, which can idle lower than the shared material without duplicating it.")]
    [SerializeField] private bool addToSharedMaterialEmission;

    [Tooltip("If true, renderers with no authored emission color are ignored. Keep this enabled when auto-collecting child renderers so shields/transparent effects are not forced to black.")]
    [SerializeField] private bool onlyAffectAuthoredEmissionRenderers = true;

    [Tooltip("Local emission intensity when the weapon is not charging. Set very low (for example 0-0.1) when the shared material is normally bright but this enemy should start dim.")]
    [SerializeField] private float idleEmissionIntensity = 0f;

    [Tooltip("Local emission intensity at full charge. Use values like 4-5 for a strong cannon windup tell.")]
    [SerializeField] private float maxChargeEmissionIntensity = 3f;

    [Tooltip("Maps normalized charge progress (0-1) to emission intensity. Use this to make the tell ease in or spike near firing.")]
    [SerializeField] private AnimationCurve chargeIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional VFX")]
    [Tooltip("Optional authored child VFX root, such as particles, beam buildup, or a glow mesh. Enabled while charging and disabled after fade-out.")]
    [SerializeField] private GameObject chargeVfxRoot;

    [Tooltip("Optional charge light. Its starting intensity is preserved, then Charge Light Max Added Intensity is layered on top while charging.")]
    [SerializeField] private Light chargeLight;

    [Tooltip("Maximum light intensity added at full charge.")]
    [SerializeField] private float chargeLightMaxAddedIntensity = 5f;

    [Tooltip("Seconds used to fade the emission/light back to idle after charging stops. Set to 0 for an immediate snap-off.")]
    [SerializeField] private float fadeOutDuration = 0.12f;

    private struct RendererState
    {
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public MaterialPropertyBlock OriginalPropertyBlock;
        public bool HadOriginalPropertyBlock;
        public Color BaseEmissionColor;
        public bool CanApplyEmission;
    }

    private RendererState[] _rendererStates;
    private int _emissionColorPropertyId;
    private float _chargeStartedAt;
    private float _chargeDuration = 1f;
    private float _fadeStartedAt;
    private float _fadeStartNormalizedIntensity;
    private float _currentNormalizedIntensity;
    private float _baseLightIntensity;
    private bool _isCharging;
    private bool _isFadingOut;
    private bool _cachedLightIntensity;

    private void Reset()
    {
        AutoAssignRenderersIfNeeded();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        RestoreBaselineVisuals();
        SetChargeVfxActive(false);
    }

    private void OnDisable()
    {
        StopCharge(immediate: true);
    }

    private void Update()
    {
        if (_isCharging)
        {
            float normalizedTime = _chargeDuration <= 0.0001f
                ? 1f
                : Mathf.Clamp01((Time.time - _chargeStartedAt) / _chargeDuration);
            ApplyNormalizedIntensity(EvaluateChargeCurve(normalizedTime));
            return;
        }

        if (_isFadingOut)
        {
            float fadeDuration = Mathf.Max(0f, fadeOutDuration);
            float fadeProgress = fadeDuration <= 0.0001f
                ? 1f
                : Mathf.Clamp01((Time.time - _fadeStartedAt) / fadeDuration);
            float intensity = Mathf.Lerp(_fadeStartNormalizedIntensity, 0f, fadeProgress);
            ApplyNormalizedIntensity(intensity);

            if (fadeProgress >= 1f)
            {
                _isFadingOut = false;
                RestoreBaselineVisuals();
                SetChargeVfxActive(false);
            }
        }
    }

    private void OnValidate()
    {
        idleEmissionIntensity = Mathf.Max(0f, idleEmissionIntensity);
        maxChargeEmissionIntensity = Mathf.Max(0f, maxChargeEmissionIntensity);
        chargeLightMaxAddedIntensity = Mathf.Max(0f, chargeLightMaxAddedIntensity);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);

        if (string.IsNullOrWhiteSpace(emissionColorProperty))
        {
            emissionColorProperty = DefaultEmissionColorProperty;
        }

        AutoAssignRenderersIfNeeded();
        _emissionColorPropertyId = Shader.PropertyToID(emissionColorProperty);
    }

    public void PlayCharge(float duration)
    {
        PlayCharge(duration, 0f);
    }

    public void PlayCharge(float duration, float elapsedSeconds)
    {
        CacheReferences();

        _chargeDuration = Mathf.Max(0f, duration);
        _chargeStartedAt = Time.time - Mathf.Max(0f, elapsedSeconds);
        _isCharging = true;
        _isFadingOut = false;
        SetChargeVfxActive(true);

        float normalizedTime = _chargeDuration <= 0.0001f
            ? 1f
            : Mathf.Clamp01((Time.time - _chargeStartedAt) / _chargeDuration);
        ApplyNormalizedIntensity(EvaluateChargeCurve(normalizedTime));
    }

    public void StopCharge(bool immediate = false)
    {
        _isCharging = false;

        if (immediate || fadeOutDuration <= 0.0001f)
        {
            _isFadingOut = false;
            RestoreBaselineVisuals();
            SetChargeVfxActive(false);
            return;
        }

        _isFadingOut = true;
        _fadeStartedAt = Time.time;
        _fadeStartNormalizedIntensity = _currentNormalizedIntensity;
    }

    private void CacheReferences()
    {
        AutoAssignRenderersIfNeeded();
        _emissionColorPropertyId = Shader.PropertyToID(string.IsNullOrWhiteSpace(emissionColorProperty)
            ? DefaultEmissionColorProperty
            : emissionColorProperty);

        if (!_cachedLightIntensity && chargeLight != null)
        {
            _baseLightIntensity = chargeLight.intensity;
            _cachedLightIntensity = true;
        }

        int rendererCount = chargeRenderers != null ? chargeRenderers.Length : 0;
        if (_rendererStates != null && _rendererStates.Length == rendererCount)
        {
            return;
        }

        _rendererStates = new RendererState[rendererCount];
        for (int i = 0; i < _rendererStates.Length; i++)
        {
            Renderer renderer = chargeRenderers[i];
            MaterialPropertyBlock originalPropertyBlock = new MaterialPropertyBlock();
            bool hadOriginalPropertyBlock = false;
            if (renderer != null)
            {
                renderer.GetPropertyBlock(originalPropertyBlock);
                hadOriginalPropertyBlock = !originalPropertyBlock.isEmpty;
            }

            _rendererStates[i] = new RendererState
            {
                Renderer = renderer,
                PropertyBlock = new MaterialPropertyBlock(),
                OriginalPropertyBlock = originalPropertyBlock,
                HadOriginalPropertyBlock = hadOriginalPropertyBlock,
                BaseEmissionColor = ResolveBaseEmissionColor(renderer),
                CanApplyEmission = CanApplyEmissionToRenderer(renderer)
            };
        }
    }

    private void AutoAssignRenderersIfNeeded()
    {
        if (!autoCollectChildRenderers || chargeRenderers != null && chargeRenderers.Length > 0)
        {
            return;
        }

        chargeRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private Color ResolveBaseEmissionColor(Renderer targetRenderer)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            return Color.black;
        }

        Material sharedMaterial = targetRenderer.sharedMaterial;
        return sharedMaterial.HasProperty(_emissionColorPropertyId)
            ? sharedMaterial.GetColor(_emissionColorPropertyId)
            : Color.black;
    }

    private bool CanApplyEmissionToRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            return false;
        }

        Material sharedMaterial = targetRenderer.sharedMaterial;
        if (!sharedMaterial.HasProperty(_emissionColorPropertyId))
        {
            return false;
        }

        if (!onlyAffectAuthoredEmissionRenderers)
        {
            return true;
        }

        Color emissionColor = sharedMaterial.GetColor(_emissionColorPropertyId);
        return HasVisibleColor(emissionColor);
    }

    private float EvaluateChargeCurve(float normalizedTime)
    {
        if (chargeIntensityCurve == null || chargeIntensityCurve.length == 0)
        {
            return normalizedTime;
        }

        return Mathf.Max(0f, chargeIntensityCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
    }

    private void ApplyNormalizedIntensity(float normalizedIntensity)
    {
        _currentNormalizedIntensity = Mathf.Clamp01(normalizedIntensity);
        float emissionIntensity = Mathf.Lerp(
            idleEmissionIntensity,
            maxChargeEmissionIntensity,
            _currentNormalizedIntensity);
        if (_rendererStates != null)
        {
            for (int i = 0; i < _rendererStates.Length; i++)
            {
                ApplyEmissionToRenderer(_rendererStates[i], emissionIntensity);
            }
        }

        if (chargeLight != null)
        {
            float baseIntensity = _cachedLightIntensity ? _baseLightIntensity : 0f;
            chargeLight.intensity = baseIntensity + (chargeLightMaxAddedIntensity * _currentNormalizedIntensity);
            chargeLight.enabled = chargeLight.intensity > 0.0001f;
        }
    }

    private void ApplyEmissionToRenderer(RendererState state, float emissionIntensity)
    {
        if (state.Renderer == null || state.PropertyBlock == null)
        {
            return;
        }

        if (!state.CanApplyEmission)
        {
            return;
        }

        state.Renderer.GetPropertyBlock(state.PropertyBlock);
        Color localEmission = ResolveEmissionColor(state) * Mathf.Max(0f, emissionIntensity);
        Color targetEmission = addToSharedMaterialEmission
            ? state.BaseEmissionColor + localEmission
            : localEmission;
        state.PropertyBlock.SetColor(_emissionColorPropertyId, targetEmission);
        state.Renderer.SetPropertyBlock(state.PropertyBlock);
    }

    private Color ResolveEmissionColor(RendererState state)
    {
        if (useChargeColorOverride)
        {
            return NormalizeEmissionColor(chargeEmissionColor);
        }

        return NormalizeEmissionColor(state.BaseEmissionColor);
    }

    private static bool HasVisibleColor(Color color)
    {
        return Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 0.0001f;
    }

    private static Color NormalizeEmissionColor(Color color)
    {
        float maxComponent = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        if (maxComponent <= 0.0001f)
        {
            return Color.white;
        }

        return new Color(
            color.r / maxComponent,
            color.g / maxComponent,
            color.b / maxComponent,
            color.a);
    }

    private void SetChargeVfxActive(bool active)
    {
        if (chargeVfxRoot != null && chargeVfxRoot.activeSelf != active)
        {
            chargeVfxRoot.SetActive(active);
        }

        if (chargeLight != null && active)
        {
            chargeLight.enabled = true;
        }
    }

    private void RestoreBaselineVisuals()
    {
        _currentNormalizedIntensity = 0f;
        if (_rendererStates != null)
        {
            for (int i = 0; i < _rendererStates.Length; i++)
            {
                RestoreRendererBaseline(_rendererStates[i]);
            }
        }

        if (chargeLight != null)
        {
            float baseIntensity = _cachedLightIntensity ? _baseLightIntensity : chargeLight.intensity;
            chargeLight.intensity = baseIntensity;
            chargeLight.enabled = chargeLight.intensity > 0.0001f;
        }
    }

    private static void RestoreRendererBaseline(RendererState state)
    {
        if (state.Renderer == null)
        {
            return;
        }

        state.Renderer.SetPropertyBlock(state.HadOriginalPropertyBlock ? state.OriginalPropertyBlock : null);
    }
}
