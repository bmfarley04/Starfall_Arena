using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Title screen button with fade-in hover effects and configurable click actions.
/// Keeps hover elements active but transparent, fading alpha on pointer enter/exit.
/// Requires a Graphic component (e.g., Image) on this GameObject for raycast detection.
/// </summary>
public class TitleScreenButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerDownHandler, IPointerUpHandler
{
    [System.Serializable]
    public struct HoverEffectsConfig
    {
        [Tooltip("Circle images that flank the button")]
        public Image[] flankingCircles;

        [Tooltip("Overlay highlight image")]
        public Image overlayHighlight;

        [Header("Fade Timing")]
        [Tooltip("Fade-in duration for flanking circles in seconds")]
        public float circleFadeInDuration;

        [Tooltip("Fade-in duration for overlay highlight in seconds")]
        public float overlayFadeInDuration;

        [Tooltip("Fade-out duration for all hover elements in seconds")]
        public float fadeOutDuration;

        [Header("Alpha Values")]
        [Tooltip("Target alpha for hover elements when visible (0-1)")]
        [Range(0f, 1f)]
        public float targetHoverAlpha;
    }

    [System.Serializable]
    public struct ClickConfig
    {
        [Header("Action Type")]
        [Tooltip("If true, uses menu transition instead of scene load")]
        public bool useMenuTransition;

        [Tooltip("TitleScreenManager reference (required if using menu transition)")]
        public TitleScreenManager titleScreenManager;

        [Header("Menu Transition (if useMenuTransition = true)")]
        [Tooltip("Which menu to transition to: ShipSelect, Controls, MainMenu")]
        public MenuTransitionType menuTransitionType;

        [Header("Scene Load (if useMenuTransition = false)")]
        [Tooltip("Scene to load on click (leave empty for no scene transition)")]
        public string sceneName;

        [Tooltip("Delay before scene load so the click sound can play")]
        public float sceneLoadDelay;

        [Header("Other Actions")]
        [Tooltip("If true, this button quits the application on click")]
        public bool quitsGame;

        [Tooltip("Additional effects triggered on click (e.g., enable a panel, play animation)")]
        public UnityEvent onClickEffects;
    }

    public enum MenuTransitionType
    {
        ShipSelect,
        Controls,
        MainMenu,
        MainMenuFromShipSelect
    }

    [System.Serializable]
    public struct SoundConfig
    {
        public SoundEffect hoverSound;
        public SoundEffect clickSound;
    }

    [System.Serializable]
    public struct HoldToSubmitConfig
    {
        [Tooltip("If true, submit actions require the button to be held instead of tapped.")]
        public bool enabled;

        [Tooltip("How long the button must be held before the action fires.")]
        [Range(0.2f, 3f)]
        public float holdDuration;

        [Tooltip("Optional radial or bar fill image. Starts full and drains to empty while holding.")]
        public Image fillImage;
    }

    [Header("Hover Effects")]
    [SerializeField] private HoverEffectsConfig hoverEffects;

    [Header("Click Action")]
    [SerializeField] private ClickConfig click;

    [Header("Sound Effects")]
    [SerializeField] private SoundConfig sounds;

    [Header("Hold To Submit")]
    [SerializeField] private HoldToSubmitConfig holdToSubmit;

    private AudioSource _audioSource;
    private CanvasGroup _parentCanvasGroup;
    private Coroutine[] _circleCoroutines;
    private Coroutine _overlayCoroutine;
    private bool _programmaticSelection = false;
    private bool _executedByHold = false;
    private float _holdTimer = 0f;
    private bool _pointerHeld;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _parentCanvasGroup = GetComponentInParent<CanvasGroup>();

        InitializeHoverElements();
        ResetHoldVisual();
    }

    private bool IsInteractable()
    {
        return _parentCanvasGroup == null || _parentCanvasGroup.interactable;
    }

    /// <summary>
    /// Call this before programmatic selection to prevent hover sound.
    /// Used for initial auto-selection and menu transitions.
    /// </summary>
    public void MarkAsProgrammaticSelection()
    {
        _programmaticSelection = true;
    }

    private void InitializeHoverElements()
    {
        if (hoverEffects.flankingCircles != null)
        {
            _circleCoroutines = new Coroutine[hoverEffects.flankingCircles.Length];
            foreach (var circle in hoverEffects.flankingCircles)
                SetAlpha(circle, 0f);
        }
        else
        {
            _circleCoroutines = new Coroutine[0];
        }

        SetAlpha(hoverEffects.overlayHighlight, 0f);
    }

    // Mouse hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        ShowHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        HideHover();
    }

    // Controller/keyboard navigation
    public void OnSelect(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        ShowHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        HideHover();
    }

    // Mouse click
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        if (holdToSubmit.enabled) return;
        ExecuteClick();
    }

    // Controller submit (A button / Enter key)
    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        if (holdToSubmit.enabled) return;
        ExecuteClick();
    }

    private void Update()
    {
        if (!holdToSubmit.enabled)
        {
            return;
        }

        if (!IsInteractable())
        {
            ResetHoldState();
            return;
        }

        bool isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;

        bool pointerPressed = _pointerHeld && Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool submitPressed = isSelected && (
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) ||
            (Keyboard.current != null && Keyboard.current.enterKey.isPressed));

        bool isHolding = pointerPressed || submitPressed;
        if (!isHolding)
        {
            ResetHoldState();
            return;
        }

        _holdTimer += Time.unscaledDeltaTime;
        float remainingRatio = 1f - Mathf.Clamp01(_holdTimer / Mathf.Max(0.001f, holdToSubmit.holdDuration));
        if (holdToSubmit.fillImage != null)
        {
            holdToSubmit.fillImage.fillAmount = remainingRatio;
        }

        if (_executedByHold || _holdTimer < holdToSubmit.holdDuration)
        {
            return;
        }

        _executedByHold = true;
        ExecuteClick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!holdToSubmit.enabled || !IsInteractable()) return;
        _pointerHeld = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!holdToSubmit.enabled) return;
        _pointerHeld = false;
        ResetHoldState();
    }

    private void ShowHover()
    {
        // Only play sound if this is actual user input (navigation, mouse hover)
        // NOT on programmatic selection (RefreshSelection, initial auto-select)
        bool shouldPlaySound = !_programmaticSelection && sounds.hoverSound != null;

        if (shouldPlaySound)
            sounds.hoverSound.Play(_audioSource);

        // Clear programmatic flag after processing
        _programmaticSelection = false;

        FadeHoverElements(hoverEffects.targetHoverAlpha, hoverEffects.circleFadeInDuration, hoverEffects.overlayFadeInDuration);
    }

    private void HideHover()
    {
        FadeHoverElements(0f, hoverEffects.fadeOutDuration, hoverEffects.fadeOutDuration);
        if (!holdToSubmit.enabled)
        {
            return;
        }

        _pointerHeld = false;
        ResetHoldState();
    }

    private void ExecuteClick()
    {
        if (sounds.clickSound != null)
            sounds.clickSound.Play(_audioSource);

        click.onClickEffects?.Invoke();

        // Menu transition (stays in same scene, transitions between canvases)
        if (click.useMenuTransition && click.titleScreenManager != null)
        {
            switch (click.menuTransitionType)
            {
                case MenuTransitionType.ShipSelect:
                    click.titleScreenManager.TransitionToShipSelect();
                    break;
                case MenuTransitionType.Controls:
                    click.titleScreenManager.TransitionToControls();
                    break;
                case MenuTransitionType.MainMenu:
                    click.titleScreenManager.TransitionToMainMenu();
                    break;
                case MenuTransitionType.MainMenuFromShipSelect:
                    click.titleScreenManager.TransitionToMainMenuFromShipSelect();
                    break;
            }
        }
        // Scene load (loads a new scene)
        else if (!string.IsNullOrEmpty(click.sceneName))
        {
            StartCoroutine(LoadSceneDelayed(click.sceneName, click.sceneLoadDelay));
        }

        if (click.quitsGame)
            Application.Quit();
    }

    private IEnumerator LoadSceneDelayed(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void FadeHoverElements(float targetAlpha, float circleDuration, float overlayDuration)
    {
        for (int i = 0; i < _circleCoroutines.Length; i++)
        {
            if (_circleCoroutines[i] != null)
                StopCoroutine(_circleCoroutines[i]);

            _circleCoroutines[i] = StartCoroutine(
                FadeImage(hoverEffects.flankingCircles[i], targetAlpha, circleDuration));
        }

        if (_overlayCoroutine != null)
            StopCoroutine(_overlayCoroutine);

        _overlayCoroutine = StartCoroutine(
            FadeImage(hoverEffects.overlayHighlight, targetAlpha, overlayDuration));
    }

    private IEnumerator FadeImage(Image image, float targetAlpha, float duration)
    {
        if (image == null) yield break;

        Color color = image.color;
        float startAlpha = color.a;

        if (Mathf.Approximately(startAlpha, targetAlpha)) yield break;

        if (duration <= 0f)
        {
            color.a = targetAlpha;
            image.color = color;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            image.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        image.color = color;
    }

    private void SetAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void ResetHoldState()
    {
        _holdTimer = 0f;
        _executedByHold = false;
        ResetHoldVisual();
    }

    private void ResetHoldVisual()
    {
        if (holdToSubmit.fillImage != null)
        {
            holdToSubmit.fillImage.fillAmount = 1f;
        }
    }

    private void Reset()
    {
        hoverEffects.circleFadeInDuration = 0.2f;
        hoverEffects.overlayFadeInDuration = 0.1f;
        hoverEffects.fadeOutDuration = 0.15f;
        hoverEffects.targetHoverAlpha = 1f;
        click.sceneLoadDelay = 0.15f;
        holdToSubmit.holdDuration = 1f;
    }

}
