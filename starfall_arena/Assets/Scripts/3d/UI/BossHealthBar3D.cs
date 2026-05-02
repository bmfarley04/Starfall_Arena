using TMPro;
using System.Collections;
using UnityEngine;
using StarfallArena.UI;

[DisallowMultipleComponent]
public class BossHealthBar3D : MonoBehaviour
{
    public bool IsBossTarget => true;
    public bool IsTrackerRevealReady => _isRevealReady;

    [Header("References")]
    [Tooltip("Enemy root that owns the actual boss health values. Defaults to the Enemy3D on this GameObject when left empty.")]
    [SerializeField] private Enemy3D bossEnemy;
    [Tooltip("CanvasGroup used to show or hide the boss health bar presentation.")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [Tooltip("Screen-space camera canvas used by this boss bar. Defaults to a parent/child Canvas when left empty.")]
    [SerializeField] private Canvas targetCanvas;
    [Tooltip("Segmented boss durability bar. This uses the same SegmentedBar damage-flash behavior as the player HUD.")]
    [SerializeField] private SegmentedBar healthBar;
    [Tooltip("Optional text readout showing the boss's current combined shield-plus-health durability.")]
    [SerializeField] private TextMeshProUGUI healthText;
    [Tooltip("Optional explicit UI camera for screen-space camera canvases. Leave empty to auto-resolve the shared UICamera.")]
    [SerializeField] private Camera uiCameraOverride;

    [Header("Spawn Presentation")]
    [Tooltip("Optional spawn-arrival component whose reveal event should gate when the boss bar becomes visible.")]
    [SerializeField] private SpawnArrivalEffect3D spawnArrivalEffect;
    [Tooltip("Optional portal boss spawn component whose completion event should gate when the boss bar becomes visible.")]
    [SerializeField] private PortalBossSpawn3D portalBossSpawn;
    [Tooltip("Seconds to wait after the reveal/portal intro completes before the boss bar starts its reveal animation.")]
    [SerializeField] private float revealDelaySeconds = 0f;
    [Tooltip("Seconds the boss bar spends fading in once its spawn intro is complete.")]
    [SerializeField] private float revealFadeDuration = 0.3f;
    [Tooltip("If enabled, the boss bar also eases upward from the hidden offset while fading in.")]
    [SerializeField] private bool animateRevealOffset = true;
    [Tooltip("Local anchored Y offset applied while hidden so the boss bar can drift into place during reveal.")]
    [SerializeField] private float hiddenRevealOffsetY = -20f;

    [Header("Visibility")]
    [Tooltip("If enabled, the CanvasGroup hides automatically when the boss has no health remaining.")]
    [SerializeField] private bool hideWhenDead = true;
    [Tooltip("Alpha applied to the CanvasGroup while the boss is alive and the health bar should be visible.")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [Tooltip("Alpha applied to the CanvasGroup when the boss bar is hidden.")]
    [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0f;

    private bool _isSubscribed;
    private bool _hasInitializedBar;
    private bool _isRevealReady;
    private bool _hasRevealAnimationPlayed;
    private Coroutine _revealCoroutine;
    private RectTransform _rootRectTransform;
    private Vector2 _shownAnchoredPosition;

    private void Awake()
    {
        bossEnemy ??= GetComponent<Enemy3D>();
        rootCanvasGroup ??= GetComponentInChildren<CanvasGroup>(true);
        targetCanvas ??= GetComponentInChildren<Canvas>(true);
        spawnArrivalEffect ??= GetComponent<SpawnArrivalEffect3D>();
        portalBossSpawn ??= GetComponent<PortalBossSpawn3D>();
        _rootRectTransform = rootCanvasGroup != null ? rootCanvasGroup.GetComponent<RectTransform>() : null;
        if (_rootRectTransform != null)
        {
            _shownAnchoredPosition = _rootRectTransform.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        BindCanvasCamera();
        SubscribeRevealSources();
        Subscribe();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        UnsubscribeRevealSources();
        Unsubscribe();
        _hasInitializedBar = false;
        _isRevealReady = false;
        _hasRevealAnimationPlayed = false;
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }
    }

    private void OnValidate()
    {
        revealDelaySeconds = Mathf.Max(0f, revealDelaySeconds);
        revealFadeDuration = Mathf.Max(0f, revealFadeDuration);
        visibleAlpha = Mathf.Clamp01(visibleAlpha);
        hiddenAlpha = Mathf.Clamp01(hiddenAlpha);
    }

    private void Subscribe()
    {
        if (_isSubscribed || bossEnemy == null)
        {
            return;
        }

        bossEnemy.HealthChanged += HandleHealthChanged;
        bossEnemy.ShieldChanged += HandleShieldChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || bossEnemy == null)
        {
            _isSubscribed = false;
            return;
        }

        bossEnemy.HealthChanged -= HandleHealthChanged;
        bossEnemy.ShieldChanged -= HandleShieldChanged;
        _isSubscribed = false;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        RefreshImmediate(forceInitializeBar: false);
    }

    private void HandleShieldChanged(float currentShield, float maxShield)
    {
        RefreshImmediate(forceInitializeBar: false);
    }

    private void RefreshImmediate()
    {
        RefreshImmediate(forceInitializeBar: !_hasInitializedBar);
    }

    private void RefreshImmediate(bool forceInitializeBar)
    {
        BindCanvasCamera();

        if (bossEnemy == null)
        {
            RefreshEmptyState();
            return;
        }

        RefreshDurability(
            bossEnemy.CurrentHealth,
            bossEnemy.MaxHealth,
            bossEnemy.CurrentShield,
            bossEnemy.MaxShield,
            initializeBar: forceInitializeBar);
    }

    private void RefreshEmptyState()
    {
        if (healthBar != null)
        {
            healthBar.InitializeBar(0f, 1f);
        }

        _hasInitializedBar = true;

        if (healthText != null)
        {
            healthText.text = "0";
        }

        ApplyVisibility(isVisible: !hideWhenDead, immediate: true);
    }

    private void RefreshDurability(float currentHealth, float maxHealth, float currentShield, float maxShield, bool initializeBar)
    {
        float safeMaxDurability = Mathf.Max(1f, maxHealth + maxShield);
        float clampedDurability = Mathf.Clamp(currentHealth, 0f, Mathf.Max(0f, maxHealth))
            + Mathf.Clamp(currentShield, 0f, Mathf.Max(0f, maxShield));

        if (healthBar != null)
        {
            if (initializeBar)
            {
                healthBar.InitializeBar(clampedDurability, safeMaxDurability);
                _hasInitializedBar = true;
            }
            else
            {
                healthBar.UpdateBar(clampedDurability, safeMaxDurability);
            }
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(clampedDurability).ToString();
        }

        ApplyVisibility(!hideWhenDead || clampedDurability > 0f);
    }

    private void ApplyVisibility(bool isVisible, bool immediate = false)
    {
        if (rootCanvasGroup == null)
        {
            return;
        }

        bool shouldBlockForSpawnIntro = isVisible && !_isRevealReady;
        if (shouldBlockForSpawnIntro)
        {
            SetImmediatePresentation(hiddenAlpha, hiddenRevealOffsetY);
            return;
        }

        if (isVisible)
        {
            if (!_hasRevealAnimationPlayed)
            {
                StartRevealAnimation(immediate);
                return;
            }

            SetImmediatePresentation(visibleAlpha, 0f);
        }
        else
        {
            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            SetImmediatePresentation(hiddenAlpha, hiddenRevealOffsetY);
        }

        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
    }

    private void BindCanvasCamera()
    {
        if (targetCanvas == null)
        {
            return;
        }

        HUDCanvasCameraResolver3D.BindCanvasToBestCamera(targetCanvas, uiCameraOverride);
    }

    private void SubscribeRevealSources()
    {
        if (spawnArrivalEffect != null)
        {
            spawnArrivalEffect.Revealed -= HandleSpawnArrivalRevealed;
            spawnArrivalEffect.Revealed += HandleSpawnArrivalRevealed;
            _isRevealReady = spawnArrivalEffect.HasRevealed;
            return;
        }

        if (portalBossSpawn != null)
        {
            portalBossSpawn.SequenceCompleted -= HandlePortalSequenceCompleted;
            portalBossSpawn.SequenceCompleted += HandlePortalSequenceCompleted;
            _isRevealReady = false;
            return;
        }

        _isRevealReady = true;
    }

    private void UnsubscribeRevealSources()
    {
        if (spawnArrivalEffect != null)
        {
            spawnArrivalEffect.Revealed -= HandleSpawnArrivalRevealed;
        }

        if (portalBossSpawn != null)
        {
            portalBossSpawn.SequenceCompleted -= HandlePortalSequenceCompleted;
        }
    }

    private void HandleSpawnArrivalRevealed(SpawnArrivalEffect3D effect)
    {
        BeginRevealReadyState();
    }

    private void HandlePortalSequenceCompleted(PortalBossSpawn3D portalSpawn)
    {
        BeginRevealReadyState();
    }

    private void BeginRevealReadyState()
    {
        _isRevealReady = true;

        if (bossEnemy == null)
        {
            return;
        }

        bool isVisible = !hideWhenDead || (bossEnemy.CurrentHealth + bossEnemy.CurrentShield) > 0f;
        if (!isVisible)
        {
            return;
        }

        ApplyVisibility(isVisible: true);
    }

    private void StartRevealAnimation(bool immediate)
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }

        if (immediate || (!animateRevealOffset && revealFadeDuration <= 0f && revealDelaySeconds <= 0f))
        {
            _hasRevealAnimationPlayed = true;
            SetImmediatePresentation(visibleAlpha, 0f);
            return;
        }

        _revealCoroutine = StartCoroutine(PlayRevealAnimation());
    }

    private IEnumerator PlayRevealAnimation()
    {
        SetImmediatePresentation(hiddenAlpha, hiddenRevealOffsetY);

        if (revealDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(revealDelaySeconds);
        }

        float duration = Mathf.Max(0.0001f, revealFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            SetImmediatePresentation(
                Mathf.Lerp(hiddenAlpha, visibleAlpha, eased),
                animateRevealOffset ? Mathf.Lerp(hiddenRevealOffsetY, 0f, eased) : 0f);
            yield return null;
        }

        _hasRevealAnimationPlayed = true;
        _revealCoroutine = null;
        SetImmediatePresentation(visibleAlpha, 0f);
    }

    private void SetImmediatePresentation(float alpha, float offsetY)
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = alpha;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (_rootRectTransform != null)
        {
            Vector2 position = _shownAnchoredPosition;
            position.y += offsetY;
            _rootRectTransform.anchoredPosition = position;
        }
    }

    public bool ShouldUseBossTracker()
    {
        return IsBossTarget;
    }
}
