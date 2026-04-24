using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCameraRig3D))]
public class PlayerLowHealthEdgeGlow3D : MonoBehaviour
{
    private static readonly int EdgeColorId = Shader.PropertyToID("_GigablastEdgeGlow_EdgeColor");
    private static readonly int Params1Id = Shader.PropertyToID("_GigablastEdgeGlow_Params1");
    private static readonly int Params2Id = Shader.PropertyToID("_GigablastEdgeGlow_Params2");
    private static readonly int Params3Id = Shader.PropertyToID("_GigablastEdgeGlow_Params3");

    private static bool s_HasOwner;
    private static int s_OwnerId;

    [Header("References")]
    [SerializeField] private Player3D player;
    [SerializeField] private PlayerCameraRig3D playerCameraRig3D;
    [SerializeField] private NetMovement3D netMovement;

    [Header("Glow")]
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeColor = new Color(1f, 0.08f, 0.08f, 1f);

    [Header("Core Border")]
    [SerializeField, Range(0f, 0.25f)] private float coreThicknessMin = 0.01f;
    [SerializeField, Range(0f, 0.25f)] private float coreThicknessMax = 0.03f;
    [SerializeField, Min(0.001f)] private float coreSoftness = 1f;
    [SerializeField, Min(0f)] private float coreIntensity = 0.25f;

    [Header("Halo")]
    [SerializeField, Range(0f, 0.3f)] private float haloThicknessMin = 0.045f;
    [SerializeField, Range(0f, 0.3f)] private float haloThicknessMax = 0.12f;
    [SerializeField, Min(0.001f)] private float haloSoftness = 1f;
    [SerializeField, Min(0f)] private float haloIntensity = 0.9f;

    [Header("Shape")]
    [SerializeField, Range(0f, 1f)] private float cornerBoost = 0.22f;
    [SerializeField, Min(0.01f)] private float edgeBiasHorizontal = 1f;
    [SerializeField, Min(0.01f)] private float edgeBiasVertical = 0.85f;

    [Header("Health Response")]
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.5f;
    [SerializeField, Min(0f)] private float shieldActiveThreshold = 0.001f;
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 3f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 4f;
    [SerializeField] private AnimationCurve healthRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Pulse")]
    [SerializeField] private bool pulseWhileLowHealth = true;
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.1f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 6f;

    private float _currentIntensity;
    private float _targetIntensity;
    private bool _warnedMissingPlayer;

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
        healthRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        _currentIntensity = 0f;
        _targetIntensity = 0f;
        ApplyShaderGlobals(0f);
    }

    private void Update()
    {
        ResolveReferences();
        if (!CanDriveEffect())
        {
            if (OwnsEffect())
            {
                ReleaseOwnership();
            }

            return;
        }

        if (!OwnsEffect() && !TryClaimOwnership())
        {
            return;
        }

        if (player == null)
        {
            WarnMissingPlayer();
            _targetIntensity = 0f;
            _currentIntensity = Mathf.MoveTowards(_currentIntensity, 0f, fadeOutSpeed * Time.deltaTime);
            ApplyShaderGlobals(_currentIntensity);
            return;
        }

        _targetIntensity = EvaluateLowHealthIntensity();
        float speed = _targetIntensity > _currentIntensity ? fadeInSpeed : fadeOutSpeed;
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, speed * Time.deltaTime);
        ApplyShaderGlobals(_currentIntensity);
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
        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();
        netMovement ??= GetComponent<NetMovement3D>();
    }

    private bool CanDriveEffect()
    {
        if (playerCameraRig3D == null || !playerCameraRig3D.isActiveAndEnabled)
        {
            return false;
        }

        if (NetTickUtil.IsActive && netMovement != null && netMovement.IsSpawned && !netMovement.IsOwner)
        {
            return false;
        }

        return true;
    }

    private float EvaluateLowHealthIntensity()
    {
        if (GigablastChargeEdgeGlow3D.IsEffectVisible)
        {
            return 0f;
        }

        float maxHealthSafe = Mathf.Max(0.0001f, player.MaxHealth);
        float healthRatio = Mathf.Clamp01(player.CurrentHealth / maxHealthSafe);
        if (healthRatio >= lowHealthThreshold)
        {
            return 0f;
        }

        if (player.CurrentShield > Mathf.Max(0f, shieldActiveThreshold))
        {
            return 0f;
        }

        float threshold = Mathf.Max(0.0001f, lowHealthThreshold);
        float normalizedLowHealth = Mathf.Clamp01((threshold - healthRatio) / threshold);
        if (healthRemap == null || healthRemap.length == 0)
        {
            return normalizedLowHealth;
        }

        return Mathf.Clamp01(healthRemap.Evaluate(normalizedLowHealth));
    }

    private void ApplyShaderGlobals(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        float pulse = 1f;
        if (pulseWhileLowHealth && intensity > 0f)
        {
            pulse += Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude * intensity;
        }

        Shader.SetGlobalColor(EdgeColorId, edgeColor);
        Shader.SetGlobalVector(Params1Id, new Vector4(
            intensity,
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
        IsEffectVisible = intensity > 0.0001f && (coreIntensity > 0.0001f || haloIntensity > 0.0001f);
    }

    private bool TryClaimOwnership()
    {
        int ownerId = GetInstanceID();
        if (s_HasOwner && s_OwnerId != ownerId)
        {
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
        IsEffectVisible = false;
    }

    private void WarnMissingPlayer()
    {
        if (_warnedMissingPlayer)
        {
            return;
        }

        _warnedMissingPlayer = true;
        Debug.LogWarning(
            "PlayerLowHealthEdgeGlow3D could not find Player3D. Keep this component on the same object as Player3D or assign the player reference directly.",
            this);
    }
}
