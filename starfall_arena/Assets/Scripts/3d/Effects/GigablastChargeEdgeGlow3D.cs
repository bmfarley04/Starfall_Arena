using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCameraRig3D))]
public class GigablastChargeEdgeGlow3D : MonoBehaviour
{
    [System.Serializable]
    public struct TierColorConfig
    {
        [ColorUsage(true, true)] public Color tier1Color;
        [ColorUsage(true, true)] public Color tier2Color;
        [ColorUsage(true, true)] public Color tier3Color;
        [ColorUsage(true, true)] public Color tier4Color;
    }

    private static readonly int EdgeColorId = Shader.PropertyToID("_GigablastEdgeGlow_EdgeColor");
    private static readonly int Params1Id = Shader.PropertyToID("_GigablastEdgeGlow_Params1");
    private static readonly int Params2Id = Shader.PropertyToID("_GigablastEdgeGlow_Params2");
    private static readonly int Params3Id = Shader.PropertyToID("_GigablastEdgeGlow_Params3");

    private static bool s_HasOwner;
    private static int s_OwnerId;

    [Header("References")]
    [SerializeField] private Player3D player;
    [SerializeField] private GigaBlastWeapon3D gigaBlast;

    [Header("Glow")]
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private bool useTierColors = true;
    [SerializeField] private TierColorConfig tierColors = new TierColorConfig
    {
        tier1Color = new Color(0.2f, 0.6f, 1f, 1f),
        tier2Color = new Color(0.35f, 0.75f, 1f, 1f),
        tier3Color = new Color(0.85f, 0.65f, 1f, 1f),
        tier4Color = new Color(1f, 0.82f, 0.45f, 1f)
    };

    [Header("Core Border")]
    [SerializeField, Range(0f, 0.25f)] private float coreThicknessMin = 0.01f;
    [SerializeField, Range(0f, 0.25f)] private float coreThicknessMax = 0.03f;
    [SerializeField, Min(0.001f)] private float coreSoftness = 0.012f;
    [SerializeField, Min(0f)] private float coreIntensity = 2.6f;

    [Header("Halo")]
    [SerializeField, Range(0f, 0.3f)] private float haloThicknessMin = 0.045f;
    [SerializeField, Range(0f, 0.3f)] private float haloThicknessMax = 0.12f;
    [SerializeField, Min(0.001f)] private float haloSoftness = 0.08f;
    [SerializeField, Min(0f)] private float haloIntensity = 1.1f;

    [Header("Shape")]
    [SerializeField, Range(0f, 1f)] private float cornerBoost = 0.22f;
    [SerializeField, Min(0.01f)] private float edgeBiasHorizontal = 1f;
    [SerializeField, Min(0.01f)] private float edgeBiasVertical = 0.85f;

    [Header("Charge Response")]
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 3f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 4f;
    [SerializeField] private AnimationCurve chargeRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Pulse")]
    [SerializeField] private bool pulseWhileCharging = false;
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.1f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 6f;

    private float _currentCharge;
    private bool _warnedMissingGigablast;
    private Color _currentEdgeColor;

    public static bool IsEffectVisible { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_HasOwner = false;
        s_OwnerId = 0;
        IsEffectVisible = false;
        ResetShaderGlobals();
    }

    private void Reset()
    {
        chargeRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!TryClaimOwnership())
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        _currentCharge = 0f;
        _currentEdgeColor = ResolveTierColor(0);
        ApplyShaderGlobals(0f);
    }

    private void Update()
    {
        if (!OwnsEffect())
        {
            return;
        }

        if (gigaBlast == null)
        {
            ResolveReferences();
            if (gigaBlast == null)
            {
                WarnMissingGigablast();
                ApplyShaderGlobals(0f);
                return;
            }
        }

        float targetCharge = gigaBlast.IsCharging ? EvaluateCharge(gigaBlast.NormalizedChargeProgress) : 0f;
        float speed = gigaBlast.IsCharging ? fadeInSpeed : fadeOutSpeed;
        _currentCharge = Mathf.MoveTowards(_currentCharge, targetCharge, speed * Time.deltaTime);
        Color targetColor = ResolveTierColor(gigaBlast.IsCharging ? gigaBlast.CurrentChargeTier : 0);
        _currentEdgeColor = Color.Lerp(_currentEdgeColor, targetColor, 1f - Mathf.Exp(-speed * Time.deltaTime));
        ApplyShaderGlobals(_currentCharge);
    }

    private void OnDisable()
    {
        if (OwnsEffect())
        {
            ReleaseOwnership();
        }
    }

    private void OnDestroy()
    {
        if (OwnsEffect())
        {
            ReleaseOwnership();
        }
    }

    private void ResolveReferences()
    {
        player ??= GetComponent<Player3D>();

        if (gigaBlast != null)
        {
            return;
        }

        if (player != null)
        {
            Weapon3D[] weapons = player.Weapons;
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] is GigaBlastWeapon3D gigaBlastWeapon)
                {
                    gigaBlast = gigaBlastWeapon;
                    return;
                }
            }
        }

        gigaBlast = GetComponent<GigaBlastWeapon3D>();
    }

    private float EvaluateCharge(float normalizedCharge)
    {
        normalizedCharge = Mathf.Clamp01(normalizedCharge);
        if (chargeRemap == null || chargeRemap.length == 0)
        {
            return normalizedCharge;
        }

        return Mathf.Clamp01(chargeRemap.Evaluate(normalizedCharge));
    }

    private void ApplyShaderGlobals(float charge)
    {
        charge = Mathf.Clamp01(charge);
        float pulse = 1f;
        if (pulseWhileCharging && charge > 0f)
        {
            float pulseWeight = Mathf.Clamp01(charge);
            pulse += Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude * pulseWeight;
        }

        Shader.SetGlobalColor(EdgeColorId, _currentEdgeColor);
        Shader.SetGlobalVector(Params1Id, new Vector4(
            charge,
            coreThicknessMin,
            coreThicknessMax,
            Mathf.Max(0.001f, coreSoftness)));
        Shader.SetGlobalVector(Params2Id, new Vector4(
            haloThicknessMin,
            haloThicknessMax,
            Mathf.Max(0.001f, haloSoftness),
            Mathf.Max(0f, coreIntensity) * pulse));
        Shader.SetGlobalVector(Params3Id, new Vector4(
            Mathf.Max(0f, haloIntensity) * pulse,
            Mathf.Max(0f, cornerBoost),
            Mathf.Max(0.01f, edgeBiasHorizontal),
            Mathf.Max(0.01f, edgeBiasVertical)));
        IsEffectVisible = charge > 0.0001f && (coreIntensity > 0.0001f || haloIntensity > 0.0001f);
    }

    private bool TryClaimOwnership()
    {
        int ownerId = GetInstanceID();
        if (s_HasOwner && s_OwnerId != ownerId)
        {
            Debug.LogWarning(
                "GigablastChargeEdgeGlow3D disabled itself because another instance already owns the fullscreen edge glow. " +
                "This effect must stay on the local 3D camera path only.",
                this);
            return false;
        }

        s_HasOwner = true;
        s_OwnerId = ownerId;
        return true;
    }

    private bool OwnsEffect()
    {
        return s_HasOwner && s_OwnerId == GetInstanceID();
    }

    private void ReleaseOwnership()
    {
        ResetShaderGlobals();
        s_HasOwner = false;
        s_OwnerId = 0;
        IsEffectVisible = false;
    }

    private static void ResetShaderGlobals()
    {
        Shader.SetGlobalColor(EdgeColorId, Color.black);
        Shader.SetGlobalVector(Params1Id, Vector4.zero);
        Shader.SetGlobalVector(Params2Id, Vector4.zero);
        Shader.SetGlobalVector(Params3Id, Vector4.zero);
    }

    private void WarnMissingGigablast()
    {
        if (_warnedMissingGigablast)
        {
            return;
        }

        _warnedMissingGigablast = true;
        Debug.LogWarning(
            "GigablastChargeEdgeGlow3D could not find a GigaBlastWeapon3D source. Assign the weapon directly or keep the component on the same player object as Player3D.",
            this);
    }

    private Color ResolveTierColor(int tier)
    {
        if (!useTierColors)
        {
            return edgeColor;
        }

        return tier switch
        {
            1 => tierColors.tier1Color,
            2 => tierColors.tier2Color,
            3 => tierColors.tier3Color,
            4 => tierColors.tier4Color,
            _ => edgeColor
        };
    }
}
