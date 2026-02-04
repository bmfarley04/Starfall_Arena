using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Title screen button with fade-in hover effects and configurable click actions.
/// Keeps hover elements active but transparent, fading alpha on pointer enter/exit.
/// Requires a Graphic component (e.g., Image) on this GameObject for raycast detection.
/// </summary>
public class TitleScreenButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
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
    }

    [System.Serializable]
    public struct ClickConfig
    {
        [Tooltip("Scene to load on click (leave empty for no scene transition)")]
        public string sceneName;

        [Tooltip("If true, this button quits the application on click")]
        public bool quitsGame;

        [Tooltip("Delay before scene load so the click sound can play")]
        public float sceneLoadDelay;

        [Tooltip("Additional effects triggered on click (e.g., enable a panel, play animation)")]
        public UnityEvent onClickEffects;
    }

    [System.Serializable]
    public struct SoundConfig
    {
        public SoundEffect hoverSound;
        public SoundEffect clickSound;
    }

    [Header("Hover Effects")]
    [SerializeField] private HoverEffectsConfig hoverEffects;

    [Header("Click Action")]
    [SerializeField] private ClickConfig click;

    [Header("Sound Effects")]
    [SerializeField] private SoundConfig sounds;

    private AudioSource _audioSource;
    private CanvasGroup _parentCanvasGroup;
    private Coroutine[] _circleCoroutines;
    private Coroutine _overlayCoroutine;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _parentCanvasGroup = GetComponentInParent<CanvasGroup>();

        InitializeHoverElements();
    }

    private bool IsInteractable()
    {
        return _parentCanvasGroup == null || _parentCanvasGroup.interactable;
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
        ExecuteClick();
    }

    // Controller submit (A button / Enter key)
    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsInteractable()) return;
        ExecuteClick();
    }

    private void ShowHover()
    {
        if (sounds.hoverSound != null)
            sounds.hoverSound.Play(_audioSource);

        FadeHoverElements(1f, hoverEffects.circleFadeInDuration, hoverEffects.overlayFadeInDuration);
    }

    private void HideHover()
    {
        FadeHoverElements(0f, hoverEffects.fadeOutDuration, hoverEffects.fadeOutDuration);
    }

    private void ExecuteClick()
    {
        if (sounds.clickSound != null)
            sounds.clickSound.Play(_audioSource);

        click.onClickEffects?.Invoke();

        if (!string.IsNullOrEmpty(click.sceneName))
            StartCoroutine(LoadSceneDelayed(click.sceneName, click.sceneLoadDelay));

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

    private void Reset()
    {
        hoverEffects.circleFadeInDuration = 0.2f;
        hoverEffects.overlayFadeInDuration = 0.1f;
        hoverEffects.fadeOutDuration = 0.15f;
        click.sceneLoadDelay = 0.15f;
    }
}
