using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Orchestrates the title screen intro and menu transitions.
/// Intro: fade from black → fade UI in. Transitions: scale+fade between canvases.
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    public enum Test3DFlowDefaultRole
    {
        Host = 0,
        Client = 1
    }

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

    [System.Serializable]
    public struct HoldActionButton
    {
        public GameObject target;
        public Image fillImage;
    }

    [System.Serializable]
    public struct NavigationGroup
    {
        public GameObject[] targets;
    }

    [Header("Menu Canvases")]
    [Tooltip("Main menu canvas (buttons, title). Also used for intro fade-in.")]
    [SerializeField] private CanvasGroup mainMenuCanvas;

    [Tooltip("First selected button on the main menu (for controller navigation)")]
    [SerializeField] private GameObject mainMenuFirstSelected;

    [Tooltip("2D controls screen canvas")]
    [SerializeField] private CanvasGroup controlsCanvas;

    [Tooltip("3D controls screen canvas")]
    [SerializeField] private CanvasGroup controls3DCanvas;

    [Tooltip("First selected button on the 2D controls screen (for controller navigation)")]
    [SerializeField] private GameObject controlsFirstSelected;

    [Tooltip("Ship select screen canvas")]
    [SerializeField] private CanvasGroup shipSelectCanvas;

    [Tooltip("Ship select manager (controls ship selection logic)")]
    [SerializeField] private ShipSelectManager shipSelectManager;

    [Tooltip("First selected button on the ship select screen (for controller navigation)")]
    [SerializeField] private GameObject shipSelectFirstSelected;

    [Tooltip("Join-game canvas that contains the IP field and connect button")]
    [SerializeField] private CanvasGroup joinGameCanvas;

    [Tooltip("First selected button or input field on the join-game canvas")]
    [SerializeField] private GameObject joinGameFirstSelected;

    [Tooltip("Waiting-room canvas shown to the host after starting a LAN duel")]
    [SerializeField] private CanvasGroup hostWaitingCanvas;

    [Tooltip("First selected button on the host waiting canvas")]
    [SerializeField] private GameObject hostWaitingFirstSelected;

    [Tooltip("Canvas shown after Host Game where the player chooses 2D or 3D")]
    [SerializeField] private CanvasGroup hostModeSelectCanvas;

    [Tooltip("First selected button on the host mode select canvas")]
    [SerializeField] private GameObject hostModeSelectFirstSelected;

    [Tooltip("Canvas shown after choosing 3D where the player selects duel or invasion")]
    [SerializeField] private CanvasGroup host3DModeSelectCanvas;

    [Tooltip("First selected button on the 3D mode select canvas")]
    [SerializeField] private GameObject host3DModeSelectFirstSelected;

    [Header("3D Menu Options")]
    [Tooltip("If true, the 3D host flow shows the duel/invasion submenu. If false, 3D goes straight to the duel waiting screen.")]
    [SerializeField] private bool show3DInvasionModeInMenu = true;

    [Header("Networking UI")]
    [SerializeField] private TMP_InputField ipAddressInputField;
    [SerializeField] private TextMeshProUGUI networkStatusText;
    [SerializeField] private TextMeshProUGUI hostModeStatusText;
    [SerializeField] private TextMeshProUGUI hostWaitingStatusText;

    [Header("Host Scene Routing")]
    [Tooltip("2D gameplay scene loaded after both players lock in when hosting from title")]
    [SerializeField] private string network2DGameplaySceneName = "SampleScene";
    [Tooltip("3D duel gameplay scene loaded after both players lock in when hosting from title")]
    [SerializeField] private string network3DGameplaySceneName = "3d";
    [Tooltip("3D invasion gameplay scene loaded after both players lock in when hosting from title")]
    [SerializeField] private string network3DInvasionGameplaySceneName = "3d_invasion";
    [SerializeField] private string host2DStatusLabel = "2D - DUEL";
    [SerializeField] private string host3DStatusLabel = "3D - DUEL";
    [SerializeField] private string host3DInvasionStatusLabel = "3D - INVASION";
    [SerializeField] private string hostWaitingStatusLabel = "WAITING ON OPPONENT...";

    [Header("Host Mode Preview Models")]
    [Tooltip("3D preview model roots shown on the host-mode select canvas. They stay hidden until the canvas transition is fully visible.")]
    [SerializeField] private GameObject[] hostModePreviewModels;

    [Header("3D Test Flow")]
    [Tooltip("Gameplay scene used by the test-only title shortcuts. Set this to 3d_invasion for the co-op invasion test flow.")]
    [SerializeField] private string test3DGameplaySceneName = "3d_invasion";
    [Tooltip("Direct-connect address used by the client-side 3D test shortcut.")]
    [SerializeField] private string test3DClientAddress = "10.33.102.140";
    [Tooltip("If enabled, the title scene skips the normal intro and immediately starts the configured 3D invasion test flow on scene load.")]
    [SerializeField] private bool autoStart3DTestFlowOnSceneStart;
    [Tooltip("Which side the auto-start 3D test flow should use when the scene opens.")]
    [SerializeField] private Test3DFlowDefaultRole autoStart3DTestRole = Test3DFlowDefaultRole.Host;
    [Tooltip("Optional override for the host's auto-selected 3D test ship. Falls back to 3d_class1 by ship ID.")]
    [SerializeField] private ShipData test3DHostShip;
    [Tooltip("Optional override for the client's auto-selected 3D test ship. Falls back to 3d_class2 by ship ID.")]
    [SerializeField] private ShipData test3DClientShip;

    [Header("Hold Actions")]
    [SerializeField] private float submitHoldDuration = 1f;
    [SerializeField] private float backHoldDuration = 1f;
    [SerializeField] private HoldActionButton joinConfirmButton;
    [SerializeField] private HoldActionButton joinBackButton;
    [SerializeField] private HoldActionButton waitingBackButton;
    [SerializeField] private HoldActionButton hostModeBackButton;
    [SerializeField] private HoldActionButton host3DModeBackButton;

    [Header("Manual Navigation")]
    [SerializeField] private NavigationGroup joinGameNavigation;
    [SerializeField] private NavigationGroup hostWaitingNavigation;
    [SerializeField] private NavigationGroup hostModeSelectNavigation;
    [SerializeField] private NavigationGroup host3DModeSelectNavigation;

    [Header("Intro: Scene Fade In")]
    [SerializeField] private SceneFadeConfig sceneFade;

    [Header("Intro: UI Fade In")]
    [SerializeField] private UIFadeConfig uiFade;

    [Header("Menu Transitions")]
    [SerializeField] private MenuTransitionConfig menuTransition;

    private float _overlayAlpha = 1f;
    private Coroutine _activeTransition;
    private CanvasGroup _activeCanvas;
    private NetMgr _netMgr;
    private NetworkSessionData _sessionData;
    private float _submitHoldTime;
    private float _backHoldTime;
    private bool _navigationLatch;
    private bool _controlsSchemeNavigationLatch;
    private bool _submitTriggeredWhileHeld;
    private HoldActionButton _resolvedControlsBackButton;
    private HoldActionButton _resolvedControls3DBackButton;
    private string _resolved2DGameplaySceneName = "SampleScene";
    private string _pendingHostModeLabel = string.Empty;
    private bool _isRunning3DTestFlow;
    private bool _is3DTestHostFlow;
    private bool _hasSubmitted3DTestShipSelection;
    private bool _pendingShipSelectTransition;
    private Coroutine _autoLock3DTestShipCoroutine;

    private const string Default3DTestHostShipId = "9219fbe4c7a848e095b199627d5ab9f4";
    private const string Default3DTestClientShipId = "bb7f2f8d9d60405a998a935672e855dd";

    private IEnumerator Start()
    {
        _netMgr = NetMgr.Instance;
        _sessionData = NetworkSessionData.Instance;
        _resolved2DGameplaySceneName = string.IsNullOrWhiteSpace(network2DGameplaySceneName)
            ? (_sessionData != null ? _sessionData.GameplaySceneName : "SampleScene")
            : network2DGameplaySceneName;

        if (_netMgr != null)
        {
            _netMgr.OnConnectionFailed += HandleConnectionFailed;
        }

        if (_sessionData != null)
        {
            _sessionData.OnSessionStateChanged += HandleSessionStateChanged;
            _sessionData.OnStatusMessageChanged += HandleStatusMessageChanged;

            // Always reset session data when loading the title screen.
            // If a prior networked game didn't fully shut down, force cleanup now
            // so the player can host or join a fresh session.
            if (NetMgr.IsNetworked && _netMgr != null)
            {
                _netMgr.ShutdownToTitle();
            }

            _sessionData.ResetToTitleLocal();
        }

        ResetHoldVisuals();

        _overlayAlpha = 1f;

        // CRITICAL: Deactivate canvas GameObjects to prevent ANY events during intro
        // This prevents EventSystem auto-selection and mouse hover events
        mainMenuCanvas.gameObject.SetActive(false);
        controlsCanvas.gameObject.SetActive(false);
        if (controls3DCanvas != null) controls3DCanvas.gameObject.SetActive(false);
        shipSelectCanvas.gameObject.SetActive(false);
        if (joinGameCanvas != null) joinGameCanvas.gameObject.SetActive(false);
        if (hostWaitingCanvas != null) hostWaitingCanvas.gameObject.SetActive(false);
        if (hostModeSelectCanvas != null) hostModeSelectCanvas.gameObject.SetActive(false);
        if (host3DModeSelectCanvas != null) host3DModeSelectCanvas.gameObject.SetActive(false);

        // Hide all canvases at start (when we activate them later)
        SetCanvasHidden(mainMenuCanvas);
        SetCanvasHidden(controlsCanvas);
        SetCanvasHidden(controls3DCanvas);
        SetCanvasHidden(shipSelectCanvas);
        SetCanvasHidden(joinGameCanvas);
        SetCanvasHidden(hostWaitingCanvas);
        SetCanvasHidden(hostModeSelectCanvas);
        SetCanvasHidden(host3DModeSelectCanvas);
        SetHostModePreviewModelsActive(false);

        if (autoStart3DTestFlowOnSceneStart)
        {
            PrepareImmediateTestFlowStart();
            StartConfigured3DTestFlow();
            yield break;
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
        if (_activeTransition != null || mainMenuCanvas == null || controlsCanvas == null) return;
        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, controlsCanvas, controlsFirstSelected));
    }

    public void TransitionToMainMenu()
    {
        if (_activeTransition != null || mainMenuCanvas == null) return;
        CanvasGroup source = GetActiveControlsCanvas();
        if (source == null)
        {
            source = controlsCanvas;
        }

        if (source == null)
        {
            return;
        }

        _activeTransition = StartCoroutine(
            RunTransition(source, mainMenuCanvas, mainMenuFirstSelected));
    }

    public void TransitionToShipSelect()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, shipSelectCanvas, shipSelectFirstSelected));
    }

    public void TransitionToLocalShipSelect()
    {
        if (_activeTransition != null) return;

        _netMgr = NetMgr.Instance;
        _netMgr?.CancelCurrentAttempt();

        _sessionData = NetworkSessionData.Instance;
        if (_sessionData != null)
        {
            _sessionData.ResetToTitleLocal();
        }

        HandleStatusMessageChanged(string.Empty);
        ApplyShipRosterForGameplayScene(_resolved2DGameplaySceneName);

        shipSelectManager?.BeginGameplayScenePreload();

        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, shipSelectCanvas, shipSelectFirstSelected));
    }

    public void TransitionToJoinGame()
    {
        if (_activeTransition != null || joinGameCanvas == null) return;
        _activeTransition = StartCoroutine(
            RunTransition(mainMenuCanvas, joinGameCanvas, joinGameFirstSelected));
    }

    public void TransitionToOnlineMenuFromJoin()
    {
        if (_activeTransition != null || joinGameCanvas == null) return;
        _activeTransition = StartCoroutine(
            RunTransition(joinGameCanvas, mainMenuCanvas, mainMenuFirstSelected));
    }

    public void StartHostingFlow()
    {
        if (hostModeSelectCanvas == null)
        {
            StartHosting2DFlow();
            return;
        }

        if (_activeTransition != null)
        {
            return;
        }

        CanvasGroup source = _activeCanvas ?? mainMenuCanvas;
        _activeTransition = StartCoroutine(
            RunTransition(source, hostModeSelectCanvas, hostModeSelectFirstSelected));
    }

    public void StartHosting2DFlow()
    {
        _pendingHostModeLabel = host2DStatusLabel;
        StartHostingForScene(_resolved2DGameplaySceneName);
    }

    public void StartHosting3DFlow()
    {
        if (!show3DInvasionModeInMenu || host3DModeSelectCanvas == null)
        {
            StartHosting3DDuelFlow();
            return;
        }

        if (_activeTransition != null)
        {
            return;
        }

        CanvasGroup source = _activeCanvas ?? hostModeSelectCanvas ?? mainMenuCanvas;
        _activeTransition = StartCoroutine(
            RunTransition(source, host3DModeSelectCanvas, host3DModeSelectFirstSelected));
    }

    public void StartHosting3DDuelFlow()
    {
        _pendingHostModeLabel = host3DStatusLabel;
        StartHostingForScene(network3DGameplaySceneName);
    }

    public void StartHosting3DInvasionFlow()
    {
        _pendingHostModeLabel = host3DInvasionStatusLabel;
        StartHostingForScene(network3DInvasionGameplaySceneName, network3DGameplaySceneName);
    }

    public void start3dhostflow()
    {
        Start3DTestFlowAsRole(Test3DFlowDefaultRole.Host);
    }

    public void start3dclientflow()
    {
        Start3DTestFlowAsRole(Test3DFlowDefaultRole.Client);
    }

    public void StartConfigured3DTestFlow()
    {
        Start3DTestFlowAsRole(autoStart3DTestRole);
    }

    public void TransitionToOnlineMenuFromHostMode()
    {
        if (_activeTransition != null || hostModeSelectCanvas == null)
        {
            return;
        }

        _activeTransition = StartCoroutine(
            RunTransition(hostModeSelectCanvas, mainMenuCanvas, mainMenuFirstSelected));
    }

    public void TransitionToHostModeSelectFrom3DMode()
    {
        if (_activeTransition != null || host3DModeSelectCanvas == null || hostModeSelectCanvas == null)
        {
            return;
        }

        _activeTransition = StartCoroutine(
            RunTransition(host3DModeSelectCanvas, hostModeSelectCanvas, hostModeSelectFirstSelected));
    }

    public void StartJoinFlow()
    {
        string address = ipAddressInputField != null ? ipAddressInputField.text : string.Empty;
        _netMgr = NetMgr.Instance;
        _sessionData = NetworkSessionData.Instance;
        bool started = _netMgr != null && _netMgr.StartClientForMenu(address);
        if (started)
        {
            HandleStatusMessageChanged("Connecting to host...");
        }
    }

    public void CancelNetworkFlow()
    {
        Reset3DTestFlowState();
        _netMgr = NetMgr.Instance;
        _netMgr?.CancelCurrentAttempt();
        HandleStatusMessageChanged(string.Empty);

        if (_activeCanvas == hostWaitingCanvas)
        {
            TransitionCanvas(hostWaitingCanvas, mainMenuCanvas, mainMenuFirstSelected);
        }
        else if (_activeCanvas == joinGameCanvas)
        {
            TransitionCanvas(joinGameCanvas, mainMenuCanvas, mainMenuFirstSelected);
        }
        else if (_activeCanvas == hostModeSelectCanvas)
        {
            TransitionCanvas(hostModeSelectCanvas, mainMenuCanvas, mainMenuFirstSelected);
        }
        else if (_activeCanvas == host3DModeSelectCanvas)
        {
            TransitionToHostModeSelectFrom3DMode();
        }
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

        if (from == hostModeSelectCanvas)
        {
            SetHostModePreviewModelsActive(false);
        }

        // Activate target canvas NOW (before transition) but keep it non-interactable
        to.gameObject.SetActive(true);
        to.interactable = false;
        to.blocksRaycasts = false;
        SetButtonsEnabled(to, false); // Keep buttons disabled during transition

        if (to == hostModeSelectCanvas)
        {
            SetHostModePreviewModelsActive(false);
        }

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
            shipSelectManager.ResetToPlayer1(); // Reset to Player 1 state when entering ship select
            shipSelectManager.PreloadShipData();
            shipSelectManager.BeginGameplayScenePreload();
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

        if (to == hostModeSelectCanvas)
        {
            SetHostModePreviewModelsActive(true);
        }

        // Enable buttons NOW (right before selection) to prevent premature auto-selection
        SetButtonsEnabled(to, true);

        RefreshSelection(selectAfter);
        PrimeControlsSchemeNavigationLatch();
        _activeTransition = null;
        TryRunPendingShipSelectTransition();
    }

    private void Update()
    {
        if (_activeTransition != null)
        {
            ResetHoldVisuals();
            return;
        }

        TryRunPendingShipSelectTransition();
        Ensure3DTestFlowAdvancesPastJoinScreen();

        if (_activeCanvas == null || _activeCanvas == shipSelectCanvas)
        {
            ResetHoldVisuals();
            return;
        }

        if (IsControlsCanvasActive())
        {
            HandleControlsSchemeNavigation();
        }
        else if (_activeCanvas != mainMenuCanvas)
        {
            HandleManualNavigation();
        }

        bool submitHeld =
            IsAnyGamepadButtonHeld(gamepad => gamepad.buttonSouth.isPressed) ||
            (Keyboard.current != null && Keyboard.current.xKey.isPressed);

        bool backHeld =
            IsAnyGamepadButtonHeld(gamepad => gamepad.buttonEast.isPressed) ||
            (Keyboard.current != null && Keyboard.current.bKey.isPressed);

        HandleSubmitHold(submitHeld);
        HandleBackHold(backHeld);
    }

    private void HandleSubmitHold(bool submitHeld)
    {
        HoldActionButton activeButton = GetActiveSubmitButton();
        ResetSubmitFillVisuals(activeButton.fillImage);
        if (activeButton.target == null || !submitHeld)
        {
            _submitHoldTime = 0f;
            _submitTriggeredWhileHeld = false;
            UpdateFill(activeButton.fillImage, 1f);
            return;
        }

        if (_submitTriggeredWhileHeld)
        {
            UpdateFill(activeButton.fillImage, 0f);
            return;
        }

        _submitHoldTime += Time.unscaledDeltaTime;
        UpdateFill(activeButton.fillImage, 1f - Mathf.Clamp01(_submitHoldTime / Mathf.Max(0.001f, submitHoldDuration)));

        if (_submitHoldTime < submitHoldDuration)
        {
            return;
        }

        _submitHoldTime = 0f;
        _submitTriggeredWhileHeld = true;
        UpdateFill(activeButton.fillImage, 0f);

        if (activeButton.target == joinConfirmButton.target)
        {
            StartJoinFlow();
        }
    }

    private void HandleBackHold(bool backHeld)
    {
        HoldActionButton activeButton = GetActiveBackButton();
        ResetBackFillVisuals(activeButton.fillImage);
        if (activeButton.target == null || !backHeld)
        {
            _backHoldTime = 0f;
            UpdateFill(activeButton.fillImage, 1f);
            return;
        }

        _backHoldTime += Time.unscaledDeltaTime;
        UpdateFill(activeButton.fillImage, 1f - Mathf.Clamp01(_backHoldTime / Mathf.Max(0.001f, backHoldDuration)));

        if (_backHoldTime < backHoldDuration)
        {
            return;
        }

        _backHoldTime = 0f;
        UpdateFill(activeButton.fillImage, 1f);

        if (_activeCanvas == controlsCanvas)
        {
            TransitionToMainMenu();
        }
        else if (_activeCanvas == controls3DCanvas)
        {
            TransitionToMainMenu();
        }
        else if (_activeCanvas == hostModeSelectCanvas)
        {
            TransitionToOnlineMenuFromHostMode();
        }
        else
        {
            CancelNetworkFlow();
        }
    }

    private void RefreshSelection(GameObject target)
    {
        if (target != null)
        {
            // Mark button as programmatic selection to prevent hover sound
            TitleScreenButton button = target.GetComponent<TitleScreenButton>();
            if (button != null)
                button.MarkAsProgrammaticSelection();

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

    private void HandleConnectionFailed(string message)
    {
        HandleStatusMessageChanged(message);
    }

    private void HandleSessionStateChanged(NetworkMatchState state)
    {
        switch (state)
        {
            case NetworkMatchState.ShipSelect:
                if (!NetMgr.IsNetworked || _sessionData == null || !_sessionData.HasBothPlayersConnected)
                {
                    return;
                }

                ApplyShipRosterForGameplayScene(_sessionData.GameplaySceneName);
                if (_isRunning3DTestFlow)
                {
                    QueueShipSelectTransitionFromCurrent();
                    HandleStatusMessageChanged("Connected. Auto-selecting 3D test ships...");
                    Begin3DTestAutoLock();
                    return;
                }

                QueueShipSelectTransitionFromCurrent();
                break;
            case NetworkMatchState.LoadingGameplay:
                if (_isRunning3DTestFlow)
                {
                    HandleStatusMessageChanged("Loading 3D test gameplay...");
                }
                break;
            case NetworkMatchState.Disconnected:
            case NetworkMatchState.Error:
                Reset3DTestFlowState();
                if (_activeCanvas == hostWaitingCanvas || _activeCanvas == joinGameCanvas)
                {
                    TransitionCanvas(_activeCanvas, mainMenuCanvas, mainMenuFirstSelected);
                }
                break;
        }
    }

    private void HandleStatusMessageChanged(string message)
    {
        if (networkStatusText != null)
        {
            networkStatusText.text = message ?? string.Empty;
        }
    }

    private void SetHostModeStatus(string modeLabel)
    {
        if (hostModeStatusText != null)
        {
            hostModeStatusText.text = modeLabel ?? string.Empty;
        }
    }

    private void SetHostWaitingStatus(string statusLabel)
    {
        if (hostWaitingStatusText != null)
        {
            hostWaitingStatusText.text = statusLabel ?? string.Empty;
        }
    }

    private void TransitionToShipSelectFromCurrent()
    {
        if (_activeTransition != null || shipSelectCanvas == null) return;

        CanvasGroup source = _activeCanvas ?? mainMenuCanvas;
        if (source == shipSelectCanvas) return;

        _activeTransition = StartCoroutine(
            RunTransition(source, shipSelectCanvas, shipSelectFirstSelected));
    }

    private void QueueShipSelectTransitionFromCurrent()
    {
        _pendingShipSelectTransition = true;
        TryRunPendingShipSelectTransition();
    }

    private void TryRunPendingShipSelectTransition()
    {
        if (!_pendingShipSelectTransition)
        {
            return;
        }

        if (_activeTransition != null || shipSelectCanvas == null)
        {
            return;
        }

        CanvasGroup source = _activeCanvas ?? mainMenuCanvas;
        if (source == shipSelectCanvas)
        {
            _pendingShipSelectTransition = false;
            return;
        }

        _pendingShipSelectTransition = false;
        _activeTransition = StartCoroutine(
            RunTransition(source, shipSelectCanvas, shipSelectFirstSelected));
    }

    private void Ensure3DTestFlowAdvancesPastJoinScreen()
    {
        if (!_isRunning3DTestFlow || _is3DTestHostFlow || _sessionData == null)
        {
            return;
        }

        if (_sessionData.CurrentState != NetworkMatchState.ShipSelect &&
            _sessionData.CurrentState != NetworkMatchState.LoadingGameplay)
        {
            return;
        }

        if (_activeCanvas == joinGameCanvas || _activeCanvas == hostWaitingCanvas || _activeCanvas == mainMenuCanvas)
        {
            QueueShipSelectTransitionFromCurrent();
        }
    }

    private void TransitionCanvas(CanvasGroup from, CanvasGroup to, GameObject firstSelected)
    {
        if (from == null || to == null || _activeTransition != null)
        {
            return;
        }

        _activeTransition = StartCoroutine(RunTransition(from, to, firstSelected));
    }

    private HoldActionButton GetActiveSubmitButton()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (_activeCanvas == joinGameCanvas)
        {
            if (selected == null)
            {
                return joinConfirmButton;
            }

            if (selected == joinConfirmButton.target || selected == ipAddressInputField?.gameObject)
            {
                return joinConfirmButton;
            }
        }

        return default;
    }

    private HoldActionButton GetActiveBackButton()
    {
        if (_activeCanvas == controlsCanvas || _activeCanvas == controls3DCanvas)
        {
            return GetControlsBackButton(_activeCanvas);
        }

        if (_activeCanvas == joinGameCanvas)
        {
            return joinBackButton;
        }

        if (_activeCanvas == hostModeSelectCanvas)
        {
            return hostModeBackButton;
        }

        if (_activeCanvas == host3DModeSelectCanvas)
        {
            return host3DModeBackButton;
        }

        if (_activeCanvas == hostWaitingCanvas)
        {
            return waitingBackButton;
        }

        return default;
    }

    private HoldActionButton GetControlsBackButton(CanvasGroup canvas)
    {
        if (canvas == controlsCanvas && _resolvedControlsBackButton.target != null)
        {
            return _resolvedControlsBackButton;
        }

        if (canvas == controls3DCanvas && _resolvedControls3DBackButton.target != null)
        {
            return _resolvedControls3DBackButton;
        }

        if (canvas == null)
        {
            return default;
        }

        RectTransform[] controlsChildren = canvas.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in controlsChildren)
        {
            if (child == null || !child.name.Equals("Back", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Image fillImage = child.GetComponent<Image>();
            if (fillImage == null)
            {
                fillImage = child.GetComponentInChildren<Image>(true);
            }

            HoldActionButton resolvedButton = new HoldActionButton
            {
                target = child.gameObject,
                fillImage = fillImage
            };

            if (canvas == controls3DCanvas)
            {
                _resolvedControls3DBackButton = resolvedButton;
                return _resolvedControls3DBackButton;
            }

            _resolvedControlsBackButton = resolvedButton;
            return _resolvedControlsBackButton;
        }

        return default;
    }

    private void ResetHoldVisuals()
    {
        _submitHoldTime = 0f;
        _backHoldTime = 0f;
        _submitTriggeredWhileHeld = false;
        ResetSubmitFillVisuals(null);
        ResetBackFillVisuals(null);
    }

    private void HandleManualNavigation()
    {
        NavigationGroup group = GetActiveNavigationGroup();
        if (group.targets == null || group.targets.Length == 0)
        {
            return;
        }

        Vector2 navigationInput = Vector2.zero;
        if (Gamepad.current != null)
        {
            navigationInput = Gamepad.current.dpad.ReadValue();
            if (navigationInput == Vector2.zero)
            {
                navigationInput = Gamepad.current.leftStick.ReadValue();
            }
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
            {
                navigationInput.x = -1f;
            }
            else if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
            {
                navigationInput.x = 1f;
            }

            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
            {
                navigationInput.y = 1f;
            }
            else if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
            {
                navigationInput.y = -1f;
            }
        }

        bool hasNavigation = Mathf.Abs(navigationInput.x) > 0.5f || Mathf.Abs(navigationInput.y) > 0.5f;
        if (!hasNavigation)
        {
            _navigationLatch = false;
            return;
        }

        if (_navigationLatch)
        {
            return;
        }

        _navigationLatch = true;

        int direction = ResolveNavigationDirection(navigationInput);
        if (direction == 0)
        {
            return;
        }

        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int currentIndex = System.Array.IndexOf(group.targets, current);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }
        else
        {
            currentIndex = (currentIndex + direction + group.targets.Length) % group.targets.Length;
        }

        RefreshSelection(group.targets[currentIndex]);
    }

    private bool HandleControlsSchemeNavigation()
    {
        CanvasGroup activeControlsCanvas = GetActiveControlsCanvas();
        if (activeControlsCanvas == null)
        {
            _controlsSchemeNavigationLatch = false;
            return false;
        }

        int direction = ResolveControlsSchemeNavigationDirection();
        if (direction == 0)
        {
            _controlsSchemeNavigationLatch = false;
            return false;
        }

        if (_controlsSchemeNavigationLatch)
        {
            return true;
        }

        _controlsSchemeNavigationLatch = true;

        if (direction > 0 && activeControlsCanvas == controlsCanvas && controls3DCanvas != null)
        {
            TransitionToControls3D();
            return true;
        }

        if (direction < 0 && activeControlsCanvas == controls3DCanvas)
        {
            TransitionToControls2D();
            return true;
        }

        return true;
    }

    private NavigationGroup GetActiveNavigationGroup()
    {
        if (_activeCanvas == joinGameCanvas)
        {
            return joinGameNavigation;
        }

        if (_activeCanvas == hostWaitingCanvas)
        {
            return hostWaitingNavigation;
        }

        if (_activeCanvas == hostModeSelectCanvas)
        {
            return hostModeSelectNavigation;
        }

        if (_activeCanvas == host3DModeSelectCanvas)
        {
            return host3DModeSelectNavigation;
        }

        return default;
    }

    private CanvasGroup GetActiveControlsCanvas()
    {
        if (_activeCanvas == controlsCanvas)
        {
            return controlsCanvas;
        }

        if (_activeCanvas == controls3DCanvas)
        {
            return controls3DCanvas;
        }

        return null;
    }

    private void TransitionToControls3D()
    {
        if (_activeTransition != null || controlsCanvas == null || controls3DCanvas == null)
        {
            return;
        }

        _activeTransition = StartCoroutine(
            RunControlsSchemeTransition(controlsCanvas, controls3DCanvas));
    }

    private void TransitionToControls2D()
    {
        if (_activeTransition != null || controlsCanvas == null || controls3DCanvas == null)
        {
            return;
        }

        _activeTransition = StartCoroutine(
            RunControlsSchemeTransition(controls3DCanvas, controlsCanvas));
    }

    private IEnumerator RunControlsSchemeTransition(CanvasGroup from, CanvasGroup to)
    {
        if (from == null || to == null)
        {
            yield break;
        }

        EventSystem.current.SetSelectedGameObject(null);

        from.interactable = false;
        from.blocksRaycasts = false;
        SetButtonsEnabled(from, false);

        to.gameObject.SetActive(true);
        to.alpha = 0f;
        to.interactable = false;
        to.blocksRaycasts = false;
        SetButtonsEnabled(to, false);

        RectTransform fromRect = (RectTransform)from.transform;
        RectTransform toRect = (RectTransform)to.transform;
        Vector2 fromStartPosition = fromRect.anchoredPosition;
        Vector2 toRestPosition = toRect.anchoredPosition;
        float slideDistance = ResolveControlsSlideDistance(fromRect, toRect);
        float slideDirection = from == controlsCanvas ? 1f : -1f;
        Vector2 fromTargetPosition = fromStartPosition + Vector2.left * slideDistance * slideDirection;
        Vector2 toStartPosition = toRestPosition + Vector2.right * slideDistance * slideDirection;

        toRect.anchoredPosition = toStartPosition;

        float duration = Mathf.Max(0.01f, menuTransition.enterDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            from.alpha = 1f - t;
            to.alpha = t;
            fromRect.anchoredPosition = Vector2.Lerp(fromStartPosition, fromTargetPosition, t);
            toRect.anchoredPosition = Vector2.Lerp(toStartPosition, toRestPosition, t);

            yield return null;
        }

        from.alpha = 0f;
        to.alpha = 1f;
        fromRect.anchoredPosition = fromStartPosition;
        toRect.anchoredPosition = toRestPosition;
        from.gameObject.SetActive(false);
        to.interactable = true;
        to.blocksRaycasts = true;

        _activeCanvas = to;
        SetButtonsEnabled(to, true);
        RefreshSelection(GetControlsDefaultSelection(to));
        PrimeControlsSchemeNavigationLatch();
        _activeTransition = null;
    }

    private void PrimeControlsSchemeNavigationLatch()
    {
        _controlsSchemeNavigationLatch = ResolveControlsSchemeNavigationDirection() != 0;
    }

    private static float ResolveControlsSlideDistance(RectTransform fromRect, RectTransform toRect)
    {
        float fromWidth = fromRect != null ? fromRect.rect.width : 0f;
        float toWidth = toRect != null ? toRect.rect.width : 0f;
        float distance = Mathf.Max(Screen.width, fromWidth, toWidth);
        return Mathf.Max(1f, distance);
    }

    private int ResolveControlsSchemeNavigationDirection()
    {
        bool leftPressed = false;
        bool rightPressed = false;

        if (Gamepad.current != null)
        {
            leftPressed = Gamepad.current.leftShoulder.isPressed;
            rightPressed = Gamepad.current.rightShoulder.isPressed;
        }

        if (Keyboard.current != null)
        {
            leftPressed |= Keyboard.current.qKey.isPressed;
            rightPressed |= Keyboard.current.eKey.isPressed;
        }

        if (leftPressed == rightPressed)
        {
            return 0;
        }

        return rightPressed ? 1 : -1;
    }

    private GameObject GetControlsDefaultSelection(CanvasGroup canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        if (canvas == controlsCanvas && controlsFirstSelected != null)
        {
            return controlsFirstSelected;
        }

        TitleScreenButton[] buttons = canvas.GetComponentsInChildren<TitleScreenButton>(true);
        foreach (TitleScreenButton button in buttons)
        {
            if (button != null && button.gameObject.activeInHierarchy)
            {
                return button.gameObject;
            }
        }

        HoldActionButton backButton = GetControlsBackButton(canvas);
        return backButton.target;
    }

    private bool IsControlsCanvasActive()
    {
        return _activeCanvas == controlsCanvas || _activeCanvas == controls3DCanvas;
    }

    private static int ResolveNavigationDirection(Vector2 navigationInput)
    {
        if (Mathf.Abs(navigationInput.x) >= Mathf.Abs(navigationInput.y))
        {
            if (navigationInput.x > 0.5f) return 1;
            if (navigationInput.x < -0.5f) return -1;
        }
        else
        {
            if (navigationInput.y < -0.5f) return 1;
            if (navigationInput.y > 0.5f) return -1;
        }

        return 0;
    }

    private void ResetSubmitFillVisuals(Image activeImage)
    {
        ResetFillIfInactive(joinConfirmButton.fillImage, activeImage);
    }

    private void ResetBackFillVisuals(Image activeImage)
    {
        ResetFillIfInactive(joinBackButton.fillImage, activeImage);
        ResetFillIfInactive(hostModeBackButton.fillImage, activeImage);
        ResetFillIfInactive(host3DModeBackButton.fillImage, activeImage);
        ResetFillIfInactive(waitingBackButton.fillImage, activeImage);
        ResetFillIfInactive(_resolvedControlsBackButton.fillImage, activeImage);
        ResetFillIfInactive(_resolvedControls3DBackButton.fillImage, activeImage);
    }

    private void StartHostingForScene(string sceneName)
    {
        StartHostingForScene(sceneName, sceneName);
    }

    private void StartHostingForScene(string sceneName, string rosterSceneName)
    {
        if (hostWaitingCanvas == null || _activeTransition != null)
        {
            if (_isRunning3DTestFlow)
            {
                Reset3DTestFlowState();
            }
            return;
        }

        string resolvedScene = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        if (string.IsNullOrEmpty(resolvedScene))
        {
            HandleStatusMessageChanged("Host scene is not configured.");
            if (_isRunning3DTestFlow)
            {
                Reset3DTestFlowState();
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(_pendingHostModeLabel))
        {
            _pendingHostModeLabel = host2DStatusLabel;
        }

        SetHostModeStatus(_pendingHostModeLabel);
        SetHostWaitingStatus(
            string.Equals(resolvedScene, network3DInvasionGameplaySceneName, System.StringComparison.OrdinalIgnoreCase)
                ? "WAITING ON TEAMMATE..."
                : hostWaitingStatusLabel);

        _sessionData = NetworkSessionData.Instance;
        _sessionData?.SetGameplaySceneName(resolvedScene);
        string resolvedRosterScene = string.IsNullOrWhiteSpace(rosterSceneName) ? resolvedScene : rosterSceneName.Trim();
        ApplyShipRosterForGameplayScene(resolvedRosterScene);

        _netMgr = NetMgr.Instance;
        if (_netMgr == null || !_netMgr.StartHostForMenu())
        {
            if (_isRunning3DTestFlow)
            {
                Reset3DTestFlowState();
            }
            return;
        }

        CanvasGroup source = _activeCanvas ?? mainMenuCanvas;
        _activeTransition = StartCoroutine(
            RunTransition(source, hostWaitingCanvas, hostWaitingFirstSelected));
    }

    private static void UpdateFill(Image image, float amount)
    {
        if (image != null)
        {
            image.fillAmount = amount;
        }
    }

    private static void ResetFillIfInactive(Image image, Image activeImage)
    {
        if (image != null && image != activeImage)
        {
            image.fillAmount = 1f;
        }
    }

    private static bool IsAnyGamepadButtonHeld(System.Func<Gamepad, bool> buttonPredicate)
    {
        if (buttonPredicate == null)
        {
            return false;
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad != null && gamepad.added && buttonPredicate(gamepad))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyShipRosterForGameplayScene(string sceneName)
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        GameDataManager.Instance.SetShipRosterForGameplayScene(sceneName);
    }

    private void PrepareImmediateTestFlowStart()
    {
        _overlayAlpha = 0f;

        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.gameObject.SetActive(true);
            mainMenuCanvas.alpha = 1f;
            mainMenuCanvas.interactable = false;
            mainMenuCanvas.blocksRaycasts = false;
            SetButtonsEnabled(mainMenuCanvas, false);
            _activeCanvas = mainMenuCanvas;
        }
    }

    private void SetHostModePreviewModelsActive(bool active)
    {
        if (hostModePreviewModels == null)
        {
            return;
        }

        foreach (GameObject previewModel in hostModePreviewModels)
        {
            if (previewModel != null)
            {
                previewModel.SetActive(active);
            }
        }
    }

    private bool Prepare3DTestShipSelections()
    {
        ShipData hostShip = Resolve3DTestShip(true);
        ShipData clientShip = Resolve3DTestShip(false);

        if (hostShip == null || clientShip == null)
        {
            HandleStatusMessageChanged("3D test flow is missing its ship data.");
            Reset3DTestFlowState();
            return false;
        }

        ApplyShipRosterForGameplayScene(test3DGameplaySceneName);
        GameDataManager.Instance?.SetSelectedShips(hostShip, clientShip);
        return true;
    }

    private void Start3DTestFlowAsRole(Test3DFlowDefaultRole role)
    {
        Reset3DTestFlowState();
        _isRunning3DTestFlow = true;
        _is3DTestHostFlow = role == Test3DFlowDefaultRole.Host;

        if (!Prepare3DTestShipSelections())
        {
            return;
        }

        if (_is3DTestHostFlow)
        {
            _pendingHostModeLabel = Resolve3DTestHostModeLabel();
            StartHostingForScene(test3DGameplaySceneName, network3DGameplaySceneName);
            return;
        }

        Start3DTestClientFlow();
    }

    private void Start3DTestClientFlow()
    {
        if (ipAddressInputField != null)
        {
            ipAddressInputField.text = test3DClientAddress;
        }

        _netMgr = NetMgr.Instance;
        _sessionData = NetworkSessionData.Instance;
        bool started = _netMgr != null && _netMgr.StartClientForMenu(test3DClientAddress);
        if (!started)
        {
            Reset3DTestFlowState();
            return;
        }

        HandleStatusMessageChanged(Resolve3DTestClientConnectStatus());

        if (joinGameCanvas != null && _activeTransition == null)
        {
            CanvasGroup source = _activeCanvas ?? mainMenuCanvas;
            if (source != joinGameCanvas)
            {
                _activeTransition = StartCoroutine(
                    RunTransition(source, joinGameCanvas, joinGameFirstSelected));
            }
        }
    }

    private string Resolve3DTestHostModeLabel()
    {
        return Is3DInvasionSceneName(test3DGameplaySceneName) ? host3DInvasionStatusLabel : host3DStatusLabel;
    }

    private string Resolve3DTestClientConnectStatus()
    {
        return Is3DInvasionSceneName(test3DGameplaySceneName)
            ? "Connecting to 3D invasion test host..."
            : "Connecting to 3D test host...";
    }

    private bool Is3DInvasionSceneName(string sceneName)
    {
        return string.Equals(
            sceneName?.Trim(),
            network3DInvasionGameplaySceneName?.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    private ShipData Resolve3DTestShip(bool hostShip)
    {
        ShipData configuredShip = hostShip ? test3DHostShip : test3DClientShip;
        if (configuredShip != null)
        {
            return configuredShip;
        }

        if (GameDataManager.Instance == null)
        {
            return null;
        }

        string fallbackShipId = hostShip ? Default3DTestHostShipId : Default3DTestClientShipId;
        return GameDataManager.Instance.GetShipById(fallbackShipId);
    }

    private void Begin3DTestAutoLock()
    {
        if (_autoLock3DTestShipCoroutine != null)
        {
            StopCoroutine(_autoLock3DTestShipCoroutine);
        }

        _autoLock3DTestShipCoroutine = StartCoroutine(AutoLock3DTestShipWhenReady());
    }

    private IEnumerator AutoLock3DTestShipWhenReady()
    {
        while (_isRunning3DTestFlow && !_hasSubmitted3DTestShipSelection)
        {
            _sessionData = NetworkSessionData.Instance;
            if (!NetMgr.IsNetworked || _sessionData == null || _sessionData.CurrentState != NetworkMatchState.ShipSelect)
            {
                _autoLock3DTestShipCoroutine = null;
                yield break;
            }

            ShipData selectedShip = Resolve3DTestShip(_is3DTestHostFlow);
            if (selectedShip != null && _sessionData.GetLocalSlotIndex() >= 0)
            {
                Prepare3DTestShipSelections();
                _sessionData.RequestShipSelection(selectedShip.ShipId, true);
                _hasSubmitted3DTestShipSelection = true;
                HandleStatusMessageChanged("3D test ship locked. Waiting for scene load...");
                _autoLock3DTestShipCoroutine = null;
                yield break;
            }

            yield return null;
        }

        _autoLock3DTestShipCoroutine = null;
    }

    private void Reset3DTestFlowState()
    {
        _isRunning3DTestFlow = false;
        _is3DTestHostFlow = false;
        _hasSubmitted3DTestShipSelection = false;
        _pendingShipSelectTransition = false;

        if (_autoLock3DTestShipCoroutine != null)
        {
            StopCoroutine(_autoLock3DTestShipCoroutine);
            _autoLock3DTestShipCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        Reset3DTestFlowState();

        if (_netMgr != null)
        {
            _netMgr.OnConnectionFailed -= HandleConnectionFailed;
        }

        if (_sessionData != null)
        {
            _sessionData.OnSessionStateChanged -= HandleSessionStateChanged;
            _sessionData.OnStatusMessageChanged -= HandleStatusMessageChanged;
        }
    }
}
