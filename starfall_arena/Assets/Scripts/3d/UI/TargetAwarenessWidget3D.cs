using StarfallArena.UI;
using UnityEngine;
using UnityEngine.UI;

public enum TargetAwarenessVisibility3D
{
    Hidden,
    Bracket,
    FloatingIndicator,
    EdgeIndicator
}

public struct TargetAwarenessPresentation3D
{
    public TargetAwarenessVisibility3D State;
    public bool IsBossTarget;
    public Vector2 CanvasPosition;
    public Vector2 IndicatorDirection;
    public Vector2 BracketSize;
    public bool RotateIndicator;
    public float IndicatorScale;
    public float BracketScale;
    public float BarScale;
    public float Health01;
    public float Shield01;
    public float AttackPulse01;
    public bool SnapPosition;
}

public class TargetAwarenessWidget3D : MonoBehaviour
{
    [System.Serializable]
    private struct BarBinding3D
    {
        [Tooltip("Simple fill image. Configure the Image as Filled in the editor.")]
        public Image fillImage;
        [Tooltip("Optional segmented bar for authored segmented HUD visuals.")]
        public SegmentedBar segmentedBar;
        [Tooltip("If enabled, this widget configures the assigned Image as a left-to-right filled bar at runtime so fillAmount visibly updates.")]
        public bool autoConfigureFillImage;
    }

    [Header("Groups")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform indicatorGroup;
    [Tooltip("Normal offscreen indicator visuals. Hide this for boss targets so the boss icon can replace the standard tracker art.")]
    [SerializeField] private RectTransform normalIndicatorVisualGroup;
    [Tooltip("Boss-only offscreen indicator visuals. Show this only when the presentation is a boss target edge indicator.")]
    [SerializeField] private RectTransform bossIndicatorVisualGroup;
    [SerializeField] private RectTransform bracketGroup;
    [Tooltip("Optional RectTransform that should receive the computed target bracket size. Falls back to Bracket Group when left empty.")]
    [SerializeField] private RectTransform bracketFrame;
    [SerializeField] private RectTransform healthBarGroup;
    [SerializeField] private RectTransform shieldBarGroup;

    [Header("Bars")]
    [SerializeField] private BarBinding3D healthBar;
    [SerializeField] private BarBinding3D shieldBar;

    [Header("Attack Flash")]
    [Tooltip("Specific red outer bracket Images that should pulse when this offscreen enemy is attacking the local player.")]
    [SerializeField] private Image[] attackFlashBracketImages;
    [Tooltip("Resting alpha for red offscreen attack brackets. 18/255 matches the current intended base transparency.")]
    [SerializeField, Range(0f, 1f)] private float attackFlashBaseAlpha = 18f / 255f;
    [Tooltip("Peak alpha for red offscreen attack brackets while an enemy attack warning is active.")]
    [SerializeField, Range(0f, 1f)] private float attackFlashPeakAlpha = 1f;
    [Tooltip("Boss icon Images that should pulse alongside the red offscreen brackets when a boss is threatening the local player.")]
    [SerializeField] private Image[] bossAttackPulseImages;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothing = 18f;
    [SerializeField] private float scaleSmoothing = 14f;
    [SerializeField] private float fadeSmoothing = 12f;
    [SerializeField] private float bracketSizeSmoothing = 18f;
    [SerializeField] private bool snapPositionToTarget = true;
    [Tooltip("Horizontal gap from the right edge of the computed bracket to the health and shield bars.")]
    [SerializeField] private float bracketBarRightGap = 12f;
    [Tooltip("Additional canvas-space offset applied to the health bar after it is placed against the bracket's right edge.")]
    [SerializeField] private Vector2 healthBarBracketOffset = Vector2.zero;
    [Tooltip("Additional canvas-space offset applied to the shield bar after it is placed against the bracket's right edge.")]
    [SerializeField] private Vector2 shieldBarBracketOffset = Vector2.zero;
    [Tooltip("Degrees added to the indicator direction. Use this when the authored arrow sprite points up/right/etc.")]
    [SerializeField] private float indicatorRotationOffset;

    private Vector2 _healthBarBasePosition;
    private Vector2 _shieldBarBasePosition;
    private Vector2 _healthBarSize;
    private Vector2 _shieldBarSize;
    private Vector2 _currentBracketSize;
    private Color[] _attackFlashBaseColors;
    private Color[] _bossAttackPulseBaseColors;
    private CanvasGroup _indicatorCanvasGroup;
    private CanvasGroup _normalIndicatorVisualCanvasGroup;
    private CanvasGroup _bossIndicatorVisualCanvasGroup;
    private CanvasGroup _bracketCanvasGroup;
    private CanvasGroup _healthCanvasGroup;
    private CanvasGroup _shieldCanvasGroup;
    private Vector2 _currentPosition;
    private float _indicatorScale = 1f;
    private float _bracketScale = 1f;
    private float _barScale = 1f;
    private bool _hasPosition;

    public void Initialize()
    {
        root ??= transform as RectTransform;
        bracketFrame ??= bracketGroup;
        rootGroup ??= GetComponent<CanvasGroup>();
        if (rootGroup == null)
        {
            rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _indicatorCanvasGroup = EnsureCanvasGroup(indicatorGroup);
        _normalIndicatorVisualCanvasGroup = EnsureCanvasGroup(normalIndicatorVisualGroup);
        _bossIndicatorVisualCanvasGroup = EnsureCanvasGroup(bossIndicatorVisualGroup);
        _bracketCanvasGroup = EnsureCanvasGroup(bracketGroup);
        _healthCanvasGroup = EnsureCanvasGroup(healthBarGroup);
        _shieldCanvasGroup = EnsureCanvasGroup(shieldBarGroup);
        CacheBarBasePositions();
        CacheAttackFlashColors();
        CacheBossAttackPulseColors();

        InitializeBar(ref healthBar);
        InitializeBar(ref shieldBar);
        ApplyHiddenImmediate();
    }

    public void ApplyPresentation(TargetAwarenessPresentation3D presentation, float deltaTime)
    {
        if (root == null)
        {
            return;
        }

        bool visible = presentation.State != TargetAwarenessVisibility3D.Hidden;
        rootGroup.alpha = MoveTowards(rootGroup.alpha, visible ? 1f : 0f, fadeSmoothing, deltaTime);
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        if (visible)
        {
            if (snapPositionToTarget || presentation.SnapPosition || !_hasPosition)
            {
                _currentPosition = presentation.CanvasPosition;
                _hasPosition = true;
            }
            else
            {
                _currentPosition = Vector2.Lerp(_currentPosition, presentation.CanvasPosition, ExponentialLerp(positionSmoothing, deltaTime));
            }

            root.anchoredPosition = _currentPosition;
        }

        bool showIndicator = presentation.State == TargetAwarenessVisibility3D.FloatingIndicator
            || presentation.State == TargetAwarenessVisibility3D.EdgeIndicator;
        bool showBossIndicator = presentation.IsBossTarget && presentation.State == TargetAwarenessVisibility3D.EdgeIndicator;
        bool showNormalIndicatorVisual = showIndicator && !presentation.IsBossTarget;
        bool showBracket = presentation.State == TargetAwarenessVisibility3D.Bracket && !presentation.IsBossTarget;

        SetGroupAlpha(_indicatorCanvasGroup, showIndicator ? 1f : 0f, deltaTime);
        SetGroupAlpha(_normalIndicatorVisualCanvasGroup, showNormalIndicatorVisual ? 1f : 0f, deltaTime);
        SetGroupAlpha(_bossIndicatorVisualCanvasGroup, showBossIndicator ? 1f : 0f, deltaTime);
        SetGroupAlpha(_bracketCanvasGroup, showBracket ? 1f : 0f, deltaTime);
        SetGroupAlpha(_healthCanvasGroup, showBracket ? 1f : 0f, deltaTime);
        SetGroupAlpha(_shieldCanvasGroup, showBracket ? 1f : 0f, deltaTime);

        _indicatorScale = Mathf.Lerp(_indicatorScale, Mathf.Max(0.01f, presentation.IndicatorScale), ExponentialLerp(scaleSmoothing, deltaTime));
        _bracketScale = Mathf.Lerp(_bracketScale, Mathf.Max(0.01f, presentation.BracketScale), ExponentialLerp(scaleSmoothing, deltaTime));
        _barScale = Mathf.Lerp(_barScale, Mathf.Max(0.01f, presentation.BarScale), ExponentialLerp(scaleSmoothing, deltaTime));
        Vector2 targetBracketSize = new Vector2(Mathf.Max(1f, presentation.BracketSize.x), Mathf.Max(1f, presentation.BracketSize.y));
        if (presentation.SnapPosition || _currentBracketSize.sqrMagnitude <= 0.0001f)
        {
            _currentBracketSize = targetBracketSize;
        }
        else
        {
            _currentBracketSize = Vector2.Lerp(_currentBracketSize, targetBracketSize, ExponentialLerp(bracketSizeSmoothing, deltaTime));
        }

        ApplyScale(indicatorGroup, _indicatorScale);
        ApplyScale(bracketGroup, _bracketScale);
        ApplyScale(healthBarGroup, _barScale);
        ApplyScale(shieldBarGroup, _barScale);
        ApplyBracketSize(_currentBracketSize);
        ApplyBracketBarOffsets(showBracket);
        ApplyAttackFlash(presentation.AttackPulse01);
        ApplyBossAttackPulse(presentation.IsBossTarget ? presentation.AttackPulse01 : 0f);
        RotateIndicator(presentation.IndicatorDirection, presentation.RotateIndicator);
        RefreshBars(presentation.Health01, presentation.Shield01);
    }

    public void HideImmediate()
    {
        ApplyHiddenImmediate();
    }

    private void ApplyHiddenImmediate()
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        SetGroupAlphaImmediate(_indicatorCanvasGroup, 0f);
        SetGroupAlphaImmediate(_normalIndicatorVisualCanvasGroup, 0f);
        SetGroupAlphaImmediate(_bossIndicatorVisualCanvasGroup, 0f);
        SetGroupAlphaImmediate(_bracketCanvasGroup, 0f);
        SetGroupAlphaImmediate(_healthCanvasGroup, 0f);
        SetGroupAlphaImmediate(_shieldCanvasGroup, 0f);
        ApplyAttackFlash(0f);
        ApplyBossAttackPulse(0f);
        _hasPosition = false;
        _currentBracketSize = Vector2.zero;
    }

    private void RotateIndicator(Vector2 direction, bool shouldRotate)
    {
        if (indicatorGroup == null)
        {
            return;
        }

        if (!shouldRotate)
        {
            indicatorGroup.localRotation = Quaternion.Euler(0f, 0f, indicatorRotationOffset);
            return;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        indicatorGroup.localRotation = Quaternion.Euler(0f, 0f, angle + indicatorRotationOffset);
    }

    private void RefreshBars(float health01, float shield01)
    {
        ApplyBar(healthBar, health01);
        ApplyBar(shieldBar, shield01);
    }

    private void CacheBarBasePositions()
    {
        if (healthBarGroup != null)
        {
            _healthBarBasePosition = healthBarGroup.anchoredPosition;
            _healthBarSize = healthBarGroup.rect.size;
        }

        if (shieldBarGroup != null)
        {
            _shieldBarBasePosition = shieldBarGroup.anchoredPosition;
            _shieldBarSize = shieldBarGroup.rect.size;
        }
    }

    private void ApplyBracketBarOffsets(bool showBracket)
    {
        float bracketHalfWidth = Mathf.Max(0f, _currentBracketSize.x * 0.5f * Mathf.Max(0.01f, _bracketScale));
        if (healthBarGroup != null)
        {
            float healthHalfWidth = Mathf.Max(0f, _healthBarSize.x * 0.5f * Mathf.Max(0.01f, _barScale));
            Vector2 bracketEdgePosition = new Vector2(bracketHalfWidth + bracketBarRightGap + healthHalfWidth, _healthBarBasePosition.y);
            healthBarGroup.anchoredPosition = showBracket ? bracketEdgePosition + healthBarBracketOffset : _healthBarBasePosition;
        }

        if (shieldBarGroup != null)
        {
            float shieldHalfWidth = Mathf.Max(0f, _shieldBarSize.x * 0.5f * Mathf.Max(0.01f, _barScale));
            Vector2 bracketEdgePosition = new Vector2(bracketHalfWidth + bracketBarRightGap + shieldHalfWidth, _shieldBarBasePosition.y);
            shieldBarGroup.anchoredPosition = showBracket ? bracketEdgePosition + shieldBarBracketOffset : _shieldBarBasePosition;
        }
    }

    private void ApplyBracketSize(Vector2 bracketSize)
    {
        if (bracketFrame == null)
        {
            return;
        }

        bracketFrame.sizeDelta = new Vector2(Mathf.Max(1f, bracketSize.x), Mathf.Max(1f, bracketSize.y));
    }

    private void CacheAttackFlashColors()
    {
        CachePulseColors(attackFlashBracketImages, ref _attackFlashBaseColors);
    }

    private void CacheBossAttackPulseColors()
    {
        CachePulseColors(bossAttackPulseImages, ref _bossAttackPulseBaseColors);
    }

    private static void CachePulseColors(Image[] images, ref Color[] cache)
    {
        if (images == null)
        {
            cache = null;
            return;
        }

        cache = new Color[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            cache[i] = image != null ? image.color : Color.white;
        }
    }

    private void ApplyAttackFlash(float pulse01)
    {
        ApplyPulseImages(attackFlashBracketImages, ref _attackFlashBaseColors, pulse01);
    }

    private void ApplyBossAttackPulse(float pulse01)
    {
        ApplyPulseImages(bossAttackPulseImages, ref _bossAttackPulseBaseColors, pulse01);
    }

    private void ApplyPulseImages(Image[] images, ref Color[] cache, float pulse01)
    {
        if (images == null || images.Length == 0)
        {
            return;
        }

        if (cache == null || cache.Length != images.Length)
        {
            CachePulseColors(images, ref cache);
        }

        float alpha = Mathf.Lerp(attackFlashBaseAlpha, attackFlashPeakAlpha, Mathf.Clamp01(pulse01));
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            Color color = i < cache.Length ? cache[i] : image.color;
            color.a = alpha;
            image.color = color;
        }
    }

    private static void ApplyBar(BarBinding3D binding, float value01)
    {
        float clamped = Mathf.Clamp01(value01);
        if (binding.fillImage != null)
        {
            ConfigureFillImageIfNeeded(binding);
            binding.fillImage.fillAmount = clamped;
        }

        if (binding.segmentedBar != null)
        {
            binding.segmentedBar.UpdateBar(clamped, 1f);
        }
    }

    private static void InitializeBar(ref BarBinding3D binding)
    {
        if (binding.fillImage != null)
        {
            if (!binding.autoConfigureFillImage && binding.fillImage.type != Image.Type.Filled)
            {
                binding.autoConfigureFillImage = true;
            }

            ConfigureFillImageIfNeeded(binding);
            binding.fillImage.fillAmount = 1f;
        }

        if (binding.segmentedBar != null)
        {
            binding.segmentedBar.InitializeBar(1f, 1f);
        }
    }

    private static void ConfigureFillImageIfNeeded(BarBinding3D binding)
    {
        if (!binding.autoConfigureFillImage || binding.fillImage == null)
        {
            return;
        }

        binding.fillImage.type = Image.Type.Filled;
        binding.fillImage.fillMethod = Image.FillMethod.Horizontal;
        binding.fillImage.fillOrigin = 0;
        binding.fillImage.fillClockwise = true;
    }

    private void SetGroupAlpha(CanvasGroup group, float targetAlpha, float deltaTime)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = MoveTowards(group.alpha, targetAlpha, fadeSmoothing, deltaTime);
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private static void SetGroupAlphaImmediate(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private static CanvasGroup EnsureCanvasGroup(RectTransform target)
    {
        if (target == null)
        {
            return null;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }

        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private static void ApplyScale(RectTransform target, float scale)
    {
        if (target != null)
        {
            target.localScale = Vector3.one * scale;
        }
    }

    private static float ExponentialLerp(float smoothing, float deltaTime)
    {
        return 1f - Mathf.Exp(-Mathf.Max(0.01f, smoothing) * Mathf.Max(0f, deltaTime));
    }

    private static float MoveTowards(float current, float target, float smoothing, float deltaTime)
    {
        return Mathf.Lerp(current, target, ExponentialLerp(smoothing, deltaTime));
    }
}
