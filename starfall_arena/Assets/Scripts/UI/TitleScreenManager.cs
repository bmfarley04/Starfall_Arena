using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Orchestrates the title screen intro and menu transitions.
/// Intro: fade from black → fade UI in. Transitions: scale+fade between canvases.
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
        [Tooltip("Delay after scene is visible before UI starts fading in")]
        public float delay;

        [Tooltip("Duration to fade the UI in")]
        public float fadeDuration;
    }

    [System.Serializable]
    public struct MenuTransitionConfig
    {
        [Header("Exit Animation")]
        [Tooltip("Duration of the exit animation")]
        public float exitDuration;

        [Tooltip("Scale the exiting canvas reaches (>1 = zoom past effect)")]
        public float exitScale;

        [Header("Pause")]
        [Tooltip("Pause between exit and enter animations (background visible)")]
        public float pauseDuration;

        [Header("Enter Animation")]
        [Tooltip("Duration of the enter animation")]
        public float enterDuration;

        [Tooltip("Scale the entering canvas starts at (<1 = zoom in from distance)")]
        public float enterStartScale;
    }

    [Header("Menu Canvases")]
    [Tooltip("Main menu canvas (buttons, title). Also used for intro fade-in.")]
    [SerializeField] private CanvasGroup mainMenuCanvas;

    [Tooltip("First selected button on the main menu (for controller navigation)")]
    [SerializeField] private GameObject mainMenuFirstSelected;

    [Tooltip("Controls screen canvas")]
    [SerializeField] private CanvasGroup controlsCanvas;

    [Tooltip("First selected button on the controls screen (for controller navigation)")]
    [SerializeField] private GameObject controlsFirstSelected;

    [Tooltip("Ship select screen canvas")]
    [SerializeField] private CanvasGroup shipSelectCanvas;

    [Tooltip("Ship select manager (controls ship selection logic)")]
    [SerializeField] private ShipSelectManager shipSelectManager;

    [Tooltip("First selected button on the ship select screen (for controller navigation)")]
    [SerializeField] private GameObject shipSelectFirstSelected;

    [Header("Intro: Scene Fade In")]
    [SerializeField] private SceneFadeConfig sceneFade;

    [Header("Intro: UI Fade In")]
    [SerializeField] private UIFadeConfig uiFade;

    [Header("Menu Transitions")]
    [SerializeField] private MenuTransitionConfig menuTransition;

    private float _overlayAlpha = 1f;
    private Coroutine _activeTransition;
    private CanvasGroup _activeCanvas;

    private IEnumerator Start()
    {
        _overlayAlpha = 1f;

        // CRITICAL: Deactivate canvas GameObjects to prevent ANY events during intro
        // This prevents EventSystem auto-selection and mouse hover events
        mainMenuCanvas.gameObject.SetActive(false);
        controlsCanvas.gameObject.SetActive(false);
        shipSelectCanvas.gameObject.SetActive(false);

        // Hide all canvases at start (when we activate them later)
        SetCanvasHidden(mainMenuCanvas);
        SetCanvasHidden(controlsCanvas);
        SetCanvasHidden(shipSelectCanvas);

        // PRELOAD: Spawn ship models NOW (at scene load) so they're ready instantly
        // This eliminates any loading delay when entering ship select
        if (shipSelectManager != null)
        {
            shipSelectManager.gameObject.SetActive(true); // Activate to allow method call
            shipSelectManager.SpawnShipsAtSceneLoad();
            shipSelectManager.gameObject.SetActive(true); // Keep active but component disabled
        }

        // Phase 1: Scene fades from black
        yield return new WaitForSecondsRealtime(sceneFade.delay);
        yield return RunSceneFade();

        // Phase 2: Main menu fades in
        if (mainMenuCanvas != null)
        {
            yield return new WaitForSecondsRealtime(uiFade.delay);
            yield return RunUIFade();
        }
    }

    private void SetCanvasHidden(CanvasGroup canvas)
    {
        if (canvas == null) return;
        canvas.alpha = 0f;
        canvas.interactable = false;
        canvas.blocksRaycasts = false;
    }

    private void SetButtonsEnabled(CanvasGroup canvas, bool enabled)
    {
        if (canvas == null) return;
        TitleScreenButton[] buttons = canvas.GetComponentsInChildren<TitleScreenButton>(true);
        foreach (var button in buttons)
            button.enabled = enabled;
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
        // Activate canvas NOW (right before fade) so it can be seen
        mainMenuCanvas.gameObject.SetActive(true);

        // Keep buttons disabled during fade to prevent premature EventSystem selection
        SetButtonsEnabled(mainMenuCanvas, false);

        float elapsed = 0f;

        while (elapsed < uiFade.fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / uiFade.fadeDuration);
            mainMenuCanvas.alpha = t;
            yield return null;
        }

        mainMenuCanvas.alpha = 1f;
        mainMenuCanvas.interactable = true;
        mainMenuCanvas.blocksRaycasts = true;

        _activeCanvas = mainMenuCanvas;

        // Enable buttons NOW (right before selection) to prevent premature auto-selection
        SetButtonsEnabled(mainMenuCanvas, true);

        // Re-trigger the current selection so the default button shows its hover
        RefreshSelection(mainMenuFirstSelected);
    }

    // --- Public methods for UnityEvent wiring ---

    public void TransitionToControls()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, controlsCanvas, controlsFirstSelected));
    }

    public void TransitionToMainMenu()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(
            RunTransition(controlsCanvas, mainMenuCanvas, mainMenuFirstSelected));
    }

    public void TransitionToShipSelect()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, shipSelectCanvas, shipSelectFirstSelected));
    }

    public void TransitionToMainMenuFromShipSelect()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(
            RunTransition(shipSelectCanvas, mainMenuCanvas, mainMenuFirstSelected));
    }

    private IEnumerator RunTransition(CanvasGroup from, CanvasGroup to, GameObject selectAfter)
    {
        // Clear selection BEFORE disabling interactable so OnDeselect/HideHover runs
        EventSystem.current.SetSelectedGameObject(null);

        from.interactable = false;
        from.blocksRaycasts = false;
        SetButtonsEnabled(from, false);

        // Activate target canvas NOW (before transition) but keep it non-interactable
        to.gameObject.SetActive(true);
        to.interactable = false;
        to.blocksRaycasts = false;
        SetButtonsEnabled(to, false); // Keep buttons disabled during transition

        // Disable ShipSelectManager when leaving ship select screen
        if (from == shipSelectCanvas && shipSelectManager != null)
        {
            shipSelectManager.enabled = false;
        }

        // PRELOAD: Load ship data EARLY (before transition) for seamless experience
        if (to == shipSelectCanvas && shipSelectManager != null)
        {
            shipSelectManager.gameObject.SetActive(true);
            shipSelectManager.enabled = true;
            shipSelectManager.PreloadShipData();
            // DON'T disable component - keep it enabled so ship stays active
            // The canvas is hidden anyway, so component being enabled doesn't matter
        }

        RectTransform fromRect = (RectTransform)from.transform;
        RectTransform toRect = (RectTransform)to.transform;

        // --- Exit: current canvas scales up and fades out (zoom past) ---
        float elapsed = 0f;
        while (elapsed < menuTransition.exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / menuTransition.exitDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            from.alpha = 1f - eased;
            float scale = Mathf.Lerp(1f, menuTransition.exitScale, eased);
            fromRect.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }

        from.alpha = 0f;
        fromRect.localScale = Vector3.one;

        // Deactivate exited canvas to prevent any events
        from.gameObject.SetActive(false);

        // --- Pause: background visible between menus ---
        yield return new WaitForSecondsRealtime(menuTransition.pauseDuration);

        // --- Enter: new canvas scales up from small and fades in ---
        toRect.localScale = new Vector3(
            menuTransition.enterStartScale, menuTransition.enterStartScale, 1f);

        elapsed = 0f;
        while (elapsed < menuTransition.enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / menuTransition.enterDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            to.alpha = eased;
            float scale = Mathf.Lerp(menuTransition.enterStartScale, 1f, eased);
            toRect.localScale = new Vector3(scale, scale, 1f);

            yield return null;
        }

        to.alpha = 1f;
        toRect.localScale = Vector3.one;
        to.interactable = true;
        to.blocksRaycasts = true;

        _activeCanvas = to;

        // Activate ship NOW (canvas is fully visible)
        if (to == shipSelectCanvas && shipSelectManager != null)
        {
            shipSelectManager.ActivateShipWhenVisible();
        }

        // Enable buttons NOW (right before selection) to prevent premature auto-selection
        SetButtonsEnabled(to, true);

        RefreshSelection(selectAfter);
        _activeTransition = null;
    }

    private void Update()
    {
        if (_activeTransition != null) return;
        if (_activeCanvas == null || _activeCanvas == mainMenuCanvas) return;

        bool cancelPressed = false;

        if (Gamepad.current != null)
            cancelPressed = Gamepad.current.buttonEast.wasPressedThisFrame;

        if (!cancelPressed && Keyboard.current != null)
            cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;

        if (cancelPressed)
        {
            // Return to main menu from different screens
            if (_activeCanvas == shipSelectCanvas)
                TransitionToMainMenuFromShipSelect();
            else
                TransitionToMainMenu();
        }
    }

    private void RefreshSelection(GameObject target)
    {
        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
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
        menuTransition.exitDuration = 0.3f;
        menuTransition.exitScale = 1.1f;
        menuTransition.pauseDuration = 0.15f;
        menuTransition.enterDuration = 0.4f;
        menuTransition.enterStartScale = 0.9f;
    }
}
