using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Orchestrates the title screen intro: fade from black, then fade UI in.
/// Uses OnGUI to draw a full-screen black overlay (no Canvas needed for the fade).
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    [System.Serializable]
    public struct SceneFadeConfig
    {
        [Tooltip("Delay before the scene starts fading in (screen stays black)")]
        public float delay;

        [Tooltip("Duration to fade from black to the full scene")]
        public float fadeDuration;
    }

    [System.Serializable]
    public struct UIFadeConfig
    {
        [Tooltip("CanvasGroup on the UI elements root (buttons, title text, etc.)")]
        public CanvasGroup canvasGroup;

        [Tooltip("Delay after scene is visible before UI starts fading in")]
        public float delay;

        [Tooltip("Duration to fade the UI in")]
        public float fadeDuration;
    }

    [Header("Phase 1: Scene Fade In")]
    [SerializeField] private SceneFadeConfig sceneFade;

    [Header("Phase 2: UI Fade In")]
    [SerializeField] private UIFadeConfig uiFade;

    private float _overlayAlpha = 1f;

    private IEnumerator Start()
    {
        _overlayAlpha = 1f;

        if (uiFade.canvasGroup != null)
        {
            uiFade.canvasGroup.alpha = 0f;
            uiFade.canvasGroup.interactable = false;
            uiFade.canvasGroup.blocksRaycasts = false;
        }

        // Phase 1: Fade from black
        yield return new WaitForSecondsRealtime(sceneFade.delay);
        yield return RunSceneFade();

        // Phase 2: UI fades in
        if (uiFade.canvasGroup != null)
        {
            yield return new WaitForSecondsRealtime(uiFade.delay);
            yield return RunUIFade();
        }
    }

    private IEnumerator RunSceneFade()
    {
        float elapsed = 0f;

        while (elapsed < sceneFade.fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / sceneFade.fadeDuration);
            _overlayAlpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        _overlayAlpha = 0f;
    }

    private IEnumerator RunUIFade()
    {
        float elapsed = 0f;

        while (elapsed < uiFade.fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / uiFade.fadeDuration);
            uiFade.canvasGroup.alpha = t;
            yield return null;
        }

        uiFade.canvasGroup.alpha = 1f;
        uiFade.canvasGroup.interactable = true;
        uiFade.canvasGroup.blocksRaycasts = true;

        // Re-trigger the current selection so the default button shows its hover
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selected);
        }
    }

    private void OnGUI()
    {
        if (_overlayAlpha <= 0f) return;

        GUI.color = new Color(0f, 0f, 0f, _overlayAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void Reset()
    {
        sceneFade.delay = 0.5f;
        sceneFade.fadeDuration = 3f;
        uiFade.delay = 0.5f;
        uiFade.fadeDuration = 1.5f;
    }
}
