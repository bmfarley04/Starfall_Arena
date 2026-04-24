using UnityEngine;
using UnityEngine.UI;

public class PlayerLowHealthVignetteHUD3D : PlayerHUDBindingTarget3D
{
    [Header("References")]
    [SerializeField] private Image vignetteImage;

    [Header("Vignette")]
    [SerializeField] private Color vignetteColor = new Color(1f, 0.08f, 0.08f, 1f);
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.5f;
    [SerializeField, Min(0f)] private float shieldActiveThreshold = 0.001f;
    [SerializeField, Min(0.01f)] private float fadeInSpeed = 3f;
    [SerializeField, Min(0.01f)] private float fadeOutSpeed = 4f;
    [SerializeField] private AnimationCurve healthRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Preview")]
    [SerializeField] private bool alwaysOnPreview;
    [SerializeField, Range(0f, 1f)] private float previewIntensity = 1f;

    private float _currentAlpha;
    private float _targetAlpha;

    protected override void Awake()
    {
        base.Awake();
        vignetteImage ??= GetComponent<Image>();
        ApplyAlphaImmediate(0f);
    }

    private void Reset()
    {
        healthRemap = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Update()
    {
        float speed = _targetAlpha > _currentAlpha ? fadeInSpeed : fadeOutSpeed;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, speed * Time.unscaledDeltaTime);
        ApplyAlphaImmediate(_currentAlpha);
    }

    protected override void BindPlayer(Player3D player)
    {
        player.HealthChanged += HandleHealthChanged;
        player.ShieldChanged += HandleShieldChanged;
        RefreshTargetAlpha(player);
    }

    protected override void UnbindPlayer(Player3D player)
    {
        player.HealthChanged -= HandleHealthChanged;
        player.ShieldChanged -= HandleShieldChanged;
    }

    protected override void RefreshBoundPlayer(Player3D player)
    {
        RefreshTargetAlpha(player);
    }

    protected override void ClearBinding()
    {
        _targetAlpha = alwaysOnPreview ? Mathf.Clamp01(previewIntensity) * Mathf.Clamp01(maxAlpha) : 0f;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        RefreshTargetAlpha(BoundPlayer);
    }

    private void HandleShieldChanged(float currentShield, float maxShield)
    {
        RefreshTargetAlpha(BoundPlayer);
    }

    private void RefreshTargetAlpha(Player3D player)
    {
        if (alwaysOnPreview)
        {
            _targetAlpha = Mathf.Clamp01(previewIntensity) * Mathf.Clamp01(maxAlpha);
            return;
        }

        if (player == null)
        {
            _targetAlpha = 0f;
            return;
        }

        if (player.CurrentShield > Mathf.Max(0f, shieldActiveThreshold))
        {
            _targetAlpha = 0f;
            return;
        }

        float maxHealthSafe = Mathf.Max(0.0001f, player.MaxHealth);
        float healthRatio = Mathf.Clamp01(player.CurrentHealth / maxHealthSafe);
        if (healthRatio >= lowHealthThreshold)
        {
            _targetAlpha = 0f;
            return;
        }

        float threshold = Mathf.Max(0.0001f, lowHealthThreshold);
        float normalizedLowHealth = Mathf.Clamp01((threshold - healthRatio) / threshold);
        float remapped = healthRemap == null || healthRemap.length == 0
            ? normalizedLowHealth
            : Mathf.Clamp01(healthRemap.Evaluate(normalizedLowHealth));

        _targetAlpha = remapped * Mathf.Clamp01(maxAlpha);
    }

    private void ApplyAlphaImmediate(float alpha)
    {
        if (vignetteImage == null)
        {
            return;
        }

        Color color = vignetteColor;
        color.a = Mathf.Clamp01(alpha);
        vignetteImage.color = color;
    }
}
