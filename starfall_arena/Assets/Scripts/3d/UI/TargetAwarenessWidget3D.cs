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
    public Vector2 CanvasPosition;
    public Vector2 IndicatorDirection;
    public float IndicatorScale;
    public float BracketScale;
    public float BarScale;
    public float Health01;
    public float Shield01;
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
    }

    [Header("Groups")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform indicatorGroup;
    [SerializeField] private RectTransform bracketGroup;
    [SerializeField] private RectTransform healthBarGroup;
    [SerializeField] private RectTransform shieldBarGroup;

    [Header("Bars")]
    [SerializeField] private BarBinding3D healthBar;
    [SerializeField] private BarBinding3D shieldBar;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothing = 18f;
    [SerializeField] private float scaleSmoothing = 14f;
    [SerializeField] private float fadeSmoothing = 12f;
    [Tooltip("Degrees added to the indicator direction. Use this when the authored arrow sprite points up/right/etc.")]
    [SerializeField] private float indicatorRotationOffset;

    private CanvasGroup _indicatorCanvasGroup;
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
        rootGroup ??= GetComponent<CanvasGroup>();
        if (rootGroup == null)
        {
            rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _indicatorCanvasGroup = EnsureCanvasGroup(indicatorGroup);
        _bracketCanvasGroup = EnsureCanvasGroup(bracketGroup);
        _healthCanvasGroup = EnsureCanvasGroup(healthBarGroup);
        _shieldCanvasGroup = EnsureCanvasGroup(shieldBarGroup);

        InitializeBar(healthBar);
        InitializeBar(shieldBar);
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
            if (presentation.SnapPosition || !_hasPosition)
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
        bool showBracket = presentation.State == TargetAwarenessVisibility3D.Bracket;

        SetGroupAlpha(_indicatorCanvasGroup, showIndicator ? 1f : 0f, deltaTime);
        SetGroupAlpha(_bracketCanvasGroup, showBracket ? 1f : 0f, deltaTime);
        SetGroupAlpha(_healthCanvasGroup, showBracket ? 1f : 0f, deltaTime);
        SetGroupAlpha(_shieldCanvasGroup, showBracket ? 1f : 0f, deltaTime);

        _indicatorScale = Mathf.Lerp(_indicatorScale, Mathf.Max(0.01f, presentation.IndicatorScale), ExponentialLerp(scaleSmoothing, deltaTime));
        _bracketScale = Mathf.Lerp(_bracketScale, Mathf.Max(0.01f, presentation.BracketScale), ExponentialLerp(scaleSmoothing, deltaTime));
        _barScale = Mathf.Lerp(_barScale, Mathf.Max(0.01f, presentation.BarScale), ExponentialLerp(scaleSmoothing, deltaTime));

        ApplyScale(indicatorGroup, _indicatorScale);
        ApplyScale(bracketGroup, _bracketScale);
        ApplyScale(healthBarGroup, _barScale);
        ApplyScale(shieldBarGroup, _barScale);
        RotateIndicator(presentation.IndicatorDirection);
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
        SetGroupAlphaImmediate(_bracketCanvasGroup, 0f);
        SetGroupAlphaImmediate(_healthCanvasGroup, 0f);
        SetGroupAlphaImmediate(_shieldCanvasGroup, 0f);
        _hasPosition = false;
    }

    private void RotateIndicator(Vector2 direction)
    {
        if (indicatorGroup == null || direction.sqrMagnitude <= 0.0001f)
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

    private static void ApplyBar(BarBinding3D binding, float value01)
    {
        float clamped = Mathf.Clamp01(value01);
        if (binding.fillImage != null)
        {
            binding.fillImage.fillAmount = clamped;
        }

        if (binding.segmentedBar != null)
        {
            binding.segmentedBar.UpdateBar(clamped, 1f);
        }
    }

    private static void InitializeBar(BarBinding3D binding)
    {
        if (binding.segmentedBar != null)
        {
            binding.segmentedBar.InitializeBar(1f, 1f);
        }
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
