using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using TMPro;
using StarfallArena.UI;
using Unity.Netcode;

public class GameSceneManager : MonoBehaviour
{
    [Header("Split Screen")]
    [SerializeField] private SplitScreenManager splitScreenManager;

    [Header("Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Default Ships")]
    [Tooltip("Fallback ship for Player 1 if no selection exists")]
    [SerializeField] private ShipData defaultPlayer1Ship;
    [Tooltip("Fallback ship for Player 2 if no selection exists")]
    [SerializeField] private ShipData defaultPlayer2Ship;

    [Header("UI Managers")]
    [SerializeField] private VersusScreenManager versusScreenManager;
    [SerializeField] private RoundEndScreenManager roundEndScreenManager;
    [SerializeField] private GameEndScreenManager gameEndScreenManager;
    [SerializeField] private AugmentSelectManager augmentSelectManager;

    [Header("Round UI")]
    [SerializeField] private CanvasGroup roundTextCanvasGroup;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private CanvasGroup countdownCanvasGroup;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Maps")]
    [Tooltip("Map root GameObjects — each must have a MapManagerScript component")]
    [SerializeField] private GameObject[] maps;

    [Header("Timing")]
    [SerializeField] private float deathToRoundEndDelay = 1.5f;
    [SerializeField] private float roundTextDisplayDuration = 1.5f;
    [SerializeField] private float countdownInterval = 1.0f;
    [SerializeField] private float roundEndScreenDuration = 4.0f;
    [SerializeField] private float textFadeDuration = 0.3f;

    [Header("Camera Transition")]
    [Tooltip("Duration for both cameras to lerp back to spawn points before switching to whole-screen")]
    [SerializeField] private float cameraLerpDuration = 0.8f;

    [Tooltip("Pause after cameras arrive at spawn points before switching to whole-screen")]
    [SerializeField] private float cameraLerpSettleDelay = 0.2f;

    [Header("Wins Required")]
    [SerializeField] private int winsRequired = 3;

    [Header("Ability 4 Unlock")]
    [Tooltip("Round number at which ability 4 becomes available (1-indexed)")]
    [SerializeField] private int ability4UnlockRound = 3;

    [Header("Player HUD Canvases")]
    [Tooltip("Canvas containing the local player's health/shield bars in network mode")]
    [SerializeField] private Canvas player1HUDCanvas;
    [Tooltip("Unused in the active network scene. Can remain assigned for legacy/local setups.")]
    [SerializeField] private Canvas player2HUDCanvas;
    [Tooltip("Canvas containing the local player's ability icons in network mode")]
    [SerializeField] private Canvas player1AbilityCanvas;
    [Tooltip("Unused in the active network scene. Can remain assigned for legacy/local setups.")]
    [SerializeField] private Canvas player2AbilityCanvas;

    [Header("Performance UI")]
    [Tooltip("Text field formatted as '[number] FPS'")]
    [SerializeField] private TextMeshProUGUI fpsText;
    [Tooltip("Text field formatted as '[number] ms'")]
    [SerializeField] private TextMeshProUGUI pingText;
    [Tooltip("How often the rolling-average FPS/ping labels are refreshed")]
    [Min(0.05f)]
    [SerializeField] private float performanceDisplayRefreshInterval = 0.25f;
    [Tooltip("How many frame samples are used in the FPS rolling average")]
    [Min(1)]
    [SerializeField] private int fpsRollingSampleCount = 30;
    [Tooltip("How many transport RTT samples are used in the ping rolling average")]
    [Min(1)]
    [SerializeField] private int pingRollingSampleCount = 12;

    [Header("Win Trackers")]
    [Tooltip("WinTracker component on Player 1's win indicator rectangles")]
    [SerializeField] private StarfallArena.UI.WinTracker player1WinTracker;
    [Tooltip("WinTracker component on Player 2's win indicator rectangles")]
    [SerializeField] private StarfallArena.UI.WinTracker player2WinTracker;

    [Header("Debug — Start At Round")]
    [Tooltip("Skip VS screen and start directly at this round. 0 = normal flow (VS screen first).")]
    [Range(0, 5)]
    [SerializeField] private int debugStartAtRound = 0;

    // ===== INTERNAL STATE =====
    private int currentRound = 0;
    private int player1Wins = 0;
    private int player2Wins = 0;
    private int lastRoundLoser = 0; // 1 or 2, for augment pick order
    private float roundStartTime;

    private Player player1;
    private Player player2;
    private ShipData player1Data;
    private ShipData player2Data;
    private GameObject activeMapObject;
    private MapManagerScript activeMapScript;

    // Runtime-instantiated ability HUD instances (one per player)
    private GameObject player1AbilityHUDInstance;
    private GameObject player2AbilityHUDInstance;
    private ulong _boundNetworkHudObjectId = ulong.MaxValue;

    // Augment persistence between rounds
    private List<AugmentLoadoutEntry> player1Augments = new List<AugmentLoadoutEntry>();
    private List<AugmentLoadoutEntry> player2Augments = new List<AugmentLoadoutEntry>();

    // Cumulative stats across all rounds
    private float totalGameDuration;
    private int p1TotalShotsFired, p1TotalShotsHit;
    private float p1TotalDamageDealt, p1TotalDamageTaken;
    private int p2TotalShotsFired, p2TotalShotsHit;
    private float p2TotalDamageDealt, p2TotalDamageTaken;

    // Death tracking for round resolution
    private bool roundOver = false;
    private int roundWinner = 0;

    // Dead player stats (captured in OnPlayerDeath before reference is lost)
    private int deadPlayerShotsFired, deadPlayerShotsHit;
    private float deadPlayerDamageDealt, deadPlayerDamageTaken;

    // Augment selection synchronization
    private bool augmentChosen = false;
    private Augment chosenAugment = null;
    private int chosenAugmentIndex = -1;

    // VS screen completion tracking
    private bool versusScreenDone = false;
    private bool useNetworkSession = false;
    private bool isAuthoritativeNetworkController = true;
    private Camera networkPresentationCamera;
    private InputDevice _localPlayer1PrimaryDevice;
    private InputDevice _localPlayer1SecondaryDevice;
    private InputDevice _localPlayer2PrimaryDevice;
    private InputDevice _localPlayer2SecondaryDevice;
    private Coroutine activeRoundIntroCoroutine;
    private int lastRoundIntroSequenceId = -1;
    private readonly Queue<float> _fpsSamples = new Queue<float>();
    private readonly Queue<float> _pingSamples = new Queue<float>();
    private float _fpsSampleSum;
    private float _pingSampleSum;
    private float _performanceDisplayTimer;
    private bool _hidePerformanceStats;

    // ===== INITIALIZATION =====
    void Start()
    {
        useNetworkSession = NetMgr.IsNetworked && NetworkManager.Singleton != null;
        isAuthoritativeNetworkController = !useNetworkSession || NetworkManager.Singleton.IsServer;

        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            session.OnSelectedMapIndexChanged += HandleSelectedMapIndexChanged;
            session.OnRoundStartPresentationChanged += HandleRoundStartPresentationChanged;
            session.OnRoundEndPresentationChanged += HandleRoundEndPresentationChanged;
            session.OnWinStateChanged += HandleWinStateChanged;
            session.OnAugmentSelectionPresentationChanged += HandleAugmentSelectionPresentationChanged;
            session.OnGameEndPresentationChanged += HandleGameEndPresentationChanged;
            session.OnSelectionTimerChanged += HandleSessionTimerChanged;
        }

        // Resolve ship data
        ResolveShipData();

        // Hide round/countdown UI initially
        if (roundTextCanvasGroup != null) roundTextCanvasGroup.alpha = 0f;
        if (countdownCanvasGroup != null) countdownCanvasGroup.alpha = 0f;

        // Deactivate all maps initially
        if (maps != null)
        {
            foreach (var mapObj in maps)
            {
                if (mapObj != null) mapObj.SetActive(false);
            }
        }

        // Hide player HUD canvases initially (will be shown when players spawn)
        SetPlayerHUDsActive(false);
        ConfigureNetworkHudCanvases();
        SetPerformanceStatsActive(true);
        UpdatePerformanceStatsText(forceRefresh: true);

        // Start in whole-screen mode for the VS screen
        if (!useNetworkSession && splitScreenManager != null)
        {
            splitScreenManager.ActivateWholeScreen();
        }

        // Subscribe to VS screen completion
        if (versusScreenManager != null)
        {
            if (!versusScreenManager.gameObject.activeSelf)
            {
                versusScreenManager.gameObject.SetActive(true);
            }

            versusScreenManager.onVersusScreenComplete.AddListener(OnVersusScreenComplete);
        }

        // Subscribe to augment selection
        if (augmentSelectManager != null)
        {
            augmentSelectManager.onAugmentChosen += OnAugmentChosen;
        }

        if (useNetworkSession)
        {
            StartCoroutine(ClientNetworkPresentationLoop());
        }

        if (isAuthoritativeNetworkController)
        {
            StartCoroutine(GameLoop());
        }
        else if (!useNetworkSession)
        {
            StartCoroutine(ClientNetworkPresentationLoop());
        }
    }

    private void Update()
    {
        UpdatePerformanceStats();
    }

    private void ResolveShipData()
    {
        if (useNetworkSession && NetworkSessionData.Instance != null)
        {
            ShipData sessionPlayer1Ship = NetworkSessionData.Instance.Player1Selection != null
                ? NetworkSessionData.Instance.Player1Selection.ShipData
                : null;
            ShipData sessionPlayer2Ship = NetworkSessionData.Instance.Player2Selection != null
                ? NetworkSessionData.Instance.Player2Selection.ShipData
                : null;

            if (sessionPlayer1Ship != null)
            {
                player1Data = sessionPlayer1Ship;
            }

            if (sessionPlayer2Ship != null)
            {
                player2Data = sessionPlayer2Ship;
            }

        }

        if (GameDataManager.Instance != null &&
            GameDataManager.Instance.selectedShipClasses != null &&
            GameDataManager.Instance.selectedShipClasses.Count >= 2)
        {
            if (player1Data == null)
            {
                player1Data = GameDataManager.Instance.selectedShipClasses[0];
            }

            if (player2Data == null)
            {
                player2Data = GameDataManager.Instance.selectedShipClasses[1];
            }
        }

        if (player1Data == null) player1Data = defaultPlayer1Ship;
        if (player2Data == null) player2Data = defaultPlayer2Ship;

    }

    // ===== GAME LOOP =====
    private IEnumerator GameLoop()
    {
        // --- DEBUG: Skip VS screen and jump to a specific round ---
        if (debugStartAtRound >= 1)
        {
            // Disable VS screen if it auto-started
            if (versusScreenManager != null)
            {
                versusScreenManager.gameObject.SetActive(false);
            }

            // Set round counter so the while-loop starts at the right round
            currentRound = debugStartAtRound - 1;

            // Simulate prior wins based on round number
            // e.g. round 3 = 1 win each, round 4 = 2-1, round 5 = 2-2
            switch (debugStartAtRound)
            {
                case 1: player1Wins = 0; player2Wins = 0; lastRoundLoser = 0; break;
                case 2: player1Wins = 1; player2Wins = 0; lastRoundLoser = 2; break;
                case 3: player1Wins = 1; player2Wins = 1; lastRoundLoser = 1; break;
                case 4: player1Wins = 2; player2Wins = 1; lastRoundLoser = 2; break;
                case 5: player1Wins = 2; player2Wins = 2; lastRoundLoser = 1; break;
            }

            // Sync win trackers with simulated wins
            UpdateWinTrackers();
            NetworkSessionData.Instance?.BroadcastWinStateServer(player1Wins, player2Wins);

            // Go straight to split-screen
            if (!useNetworkSession && splitScreenManager != null)
            {
                splitScreenManager.ActivateSplitScreen();
            }
        }
        else
        {
            // --- VS SCREEN (whole-screen mode, already active from Start) ---
            // Wait for VS screen to complete only when a live manager is active.
            if (versusScreenManager != null && versusScreenManager.gameObject.activeInHierarchy)
            {
                yield return new WaitUntil(() => versusScreenDone);
            }
            else
            {
                versusScreenDone = true;
            }

            // VS screen is done — switch to split-screen for gameplay
            if (!useNetworkSession && splitScreenManager != null)
            {
                splitScreenManager.ActivateSplitScreen();
            }
        }

        // Round loop
        while (player1Wins < winsRequired && player2Wins < winsRequired)
        {
            currentRound++;

            // --- Pre-round events that need full-screen ---
            // Augment selection (rounds 2, 4, 5 — i.e. after first round, loser picks first)
            // At this point we're already in whole-screen mode if coming from a round that
            // needed augment select, or in split-screen mode if this is round 1 or round 3.
            if (currentRound >= 2 && lastRoundLoser != 0)
            {
                // Determine if augment selection happens this round
                // (rounds 2, 4, 5 — you can customize this logic)
                bool hasAugmentPhase = (currentRound == 2 || currentRound == 4 || currentRound == 5);
                if (hasAugmentPhase)
                {
                    // We should already be in whole-screen mode (transitioned at end of previous round)
                    yield return DoAugmentSelection();
                }
            }

            // --- Transition from whole-screen back to split-screen for gameplay ---
            if (!useNetworkSession && splitScreenManager != null)
            {
                splitScreenManager.ActivateSplitScreen();
            }

            // --- Spawn fresh players ---
            yield return SpawnPlayers();

            // Show player HUD canvases
            SetPlayerHUDsActive(true);

            // Ability 4 lock/unlock
            if (currentRound < ability4UnlockRound)
            {
                SetAbility4Locked(true);
            }
            else
            {
                SetAbility4Locked(false);
            }

            // --- Map selection ---
            yield return ActivateRandomMap();

            // --- Round intro presentation ---
            if (useNetworkSession && NetworkSessionData.Instance != null)
            {
                SetPlayersMovementLocked(true);
                NetworkSessionData.Instance.BroadcastRoundStartServer(currentRound);
                yield return new WaitForSecondsRealtime(GetRoundIntroDuration());
                NetworkSessionData.Instance.MarkMatchStarted();
            }
            else
            {
                yield return ShowRoundText(currentRound);
                yield return ShowCountdown();
            }

            // --- Unlock players and start round ---
            SetPlayersMovementLocked(false);
            roundStartTime = Time.time;
            roundOver = false;
            roundWinner = 0;

            // --- Wait for someone to die ---
            yield return new WaitUntil(() => roundOver);

            // Lock surviving player
            if (player1 != null)
            {
                player1.PrepareForRoundEndFreeze();
                SetPlayerMovementLocked(player1, true);
            }

            if (player2 != null)
            {
                player2.PrepareForRoundEndFreeze();
                SetPlayerMovementLocked(player2, true);
            }

            // Brief delay for death effects
            yield return new WaitForSeconds(deathToRoundEndDelay);

            // --- Capture stats before destroying players ---
            float roundDuration = Time.time - roundStartTime;
            totalGameDuration += roundDuration;

            CaptureRoundStats();

            // Snapshot per-round stats — use saved dead player stats for the killed player
            float p1RoundDmg, p2RoundDmg;
            float p1RoundAcc, p2RoundAcc;

            if (roundWinner == 2) // Player 1 died
            {
                p1RoundDmg = deadPlayerDamageDealt;
                p1RoundAcc = deadPlayerShotsFired > 0
                    ? (float)deadPlayerShotsHit / deadPlayerShotsFired * 100f : 0f;
                p2RoundDmg = player2 != null ? player2.damageDealt : 0f;
                p2RoundAcc = player2 != null && player2.shotsFired > 0
                    ? (float)player2.shotsHit / player2.shotsFired * 100f : 0f;
            }
            else // Player 2 died
            {
                p1RoundDmg = player1 != null ? player1.damageDealt : 0f;
                p1RoundAcc = player1 != null && player1.shotsFired > 0
                    ? (float)player1.shotsHit / player1.shotsFired * 100f : 0f;
                p2RoundDmg = deadPlayerDamageDealt;
                p2RoundAcc = deadPlayerShotsFired > 0
                    ? (float)deadPlayerShotsHit / deadPlayerShotsFired * 100f : 0f;
            }

            // Save augments before destroying players
            SavePlayerAugments();

            // --- Hide HUDs and transition cameras to whole-screen for round end ---
            SetPlayerHUDsActive(false);
            yield return TransitionToWholeScreen();

            // --- Show round end screen (now in whole-screen mode) ---
            if (useNetworkSession && NetworkSessionData.Instance != null)
            {
                NetworkSessionData.Instance.ShowRoundEndServer(
                    roundWinner, roundDuration,
                    p1RoundDmg, p2RoundDmg,
                    p1RoundAcc, p2RoundAcc
                );
            }
            else if (roundEndScreenManager != null)
            {
                roundEndScreenManager.ShowRoundEndScreen(
                    roundWinner, roundDuration,
                    p1RoundDmg, p2RoundDmg,
                    p1RoundAcc, p2RoundAcc
                );
            }

            yield return new WaitForSecondsRealtime(roundEndScreenDuration);

            if (useNetworkSession && NetworkSessionData.Instance != null)
            {
                NetworkSessionData.Instance.HideRoundEndServer();
            }
            else if (roundEndScreenManager != null)
            {
                roundEndScreenManager.HideRoundEndScreen();
            }

            yield return new WaitForSecondsRealtime(0.5f);

            // --- Update win counters ---
            if (roundWinner == 1)
            {
                player1Wins++;
                lastRoundLoser = 2;
            }
            else
            {
                player2Wins++;
                lastRoundLoser = 1;
            }

            UpdateWinTrackers();
            NetworkSessionData.Instance?.BroadcastWinStateServer(player1Wins, player2Wins);

            // Capture gamepad references before players are destroyed
            // (PlayerInput components hold the device assignments)
            if (!useNetworkSession)
            {
                CapturePlayerGamepads();
            }

            // Destroy current players (already hidden since TransitionToWholeScreen)
            DestroyPlayers();

            // Shrink map
            if (activeMapScript != null)
            {
                activeMapScript.ShrinkAndDestroy();
                yield return new WaitForSeconds(activeMapScript.animation.shrinkDuration + 0.1f);
                if (activeMapObject != null) activeMapObject.SetActive(false);
                activeMapObject = null;
                activeMapScript = null;
            }
        }

        // ===== GAME END (already in whole-screen mode from the final round end) =====
        yield return ShowGameEnd();
    }

    /// <summary>
    /// Transitions from split-screen gameplay to whole-screen mode.
    /// Deactivates ship visuals, lerps cameras back to spawn points, then swaps cameras.
    /// </summary>
    private IEnumerator TransitionToWholeScreen()
    {
        // Local-only flow can safely deactivate whole player objects here.
        // In network play this is a host-local scene change, not replicated state:
        // disabling spawned NetworkObjects can leave frozen ghost ships on clients
        // and stale NetMovement entries on the server during the round transition.
        if (!useNetworkSession)
        {
            if (player1 != null) player1.gameObject.SetActive(false);
            if (player2 != null) player2.gameObject.SetActive(false);
        }

        // Lerp both cameras back to the spawn point positions
        if (!useNetworkSession && splitScreenManager != null)
        {
            yield return splitScreenManager.LerpCamerasToPositions(
                player1SpawnPoint.position,
                player2SpawnPoint.position,
                cameraLerpDuration
            );
        }

        // Brief settle before camera swap
        yield return new WaitForSecondsRealtime(cameraLerpSettleDelay);

        // Swap to whole-screen + UI overlay cameras
        if (!useNetworkSession && splitScreenManager != null)
        {
            splitScreenManager.ActivateWholeScreen();
        }
    }

    // ===== PLAYER HUD MANAGEMENT =====

    /// <summary>
    /// Shows or hides all player HUD canvases (health/shield bars and ability icons).
    /// Called when switching between split-screen gameplay and whole-screen UI.
    /// </summary>
    private void SetPlayerHUDsActive(bool active)
    {
        if (useNetworkSession)
        {
            if (player1HUDCanvas != null) player1HUDCanvas.gameObject.SetActive(active);
            if (player2HUDCanvas != null) player2HUDCanvas.gameObject.SetActive(false);
            if (player1AbilityCanvas != null) player1AbilityCanvas.gameObject.SetActive(active);
            if (player2AbilityCanvas != null) player2AbilityCanvas.gameObject.SetActive(false);
            if (player1AbilityHUDInstance != null) player1AbilityHUDInstance.SetActive(active);
            if (player2AbilityHUDInstance != null) player2AbilityHUDInstance.SetActive(false);
            return;
        }

        if (player1HUDCanvas != null) player1HUDCanvas.gameObject.SetActive(active);
        if (player2HUDCanvas != null) player2HUDCanvas.gameObject.SetActive(active);
        if (player1AbilityCanvas != null) player1AbilityCanvas.gameObject.SetActive(active);
        if (player2AbilityCanvas != null) player2AbilityCanvas.gameObject.SetActive(active);
        if (player1AbilityHUDInstance != null) player1AbilityHUDInstance.SetActive(active);
        if (player2AbilityHUDInstance != null) player2AbilityHUDInstance.SetActive(active);
    }

    private void SetPerformanceStatsActive(bool active)
    {
        bool shouldShow = active && !_hidePerformanceStats;

        if (fpsText != null)
        {
            fpsText.gameObject.SetActive(shouldShow);
        }

        if (pingText != null)
        {
            pingText.gameObject.SetActive(shouldShow);
        }
    }

    private void UpdatePerformanceStats()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime > Mathf.Epsilon)
        {
            AddRollingSample(_fpsSamples, ref _fpsSampleSum, 1f / deltaTime, Mathf.Max(1, fpsRollingSampleCount));
        }

        AddRollingSample(_pingSamples, ref _pingSampleSum, GetCurrentPingMilliseconds(), Mathf.Max(1, pingRollingSampleCount));

        _performanceDisplayTimer += deltaTime;
        if (_performanceDisplayTimer < performanceDisplayRefreshInterval)
        {
            return;
        }

        _performanceDisplayTimer = 0f;
        UpdatePerformanceStatsText(forceRefresh: false);
    }

    private void UpdatePerformanceStatsText(bool forceRefresh)
    {
        bool shouldShow = !_hidePerformanceStats;
        if (!shouldShow)
        {
            SetPerformanceStatsActive(false);
            return;
        }

        if (!forceRefresh && fpsText == null && pingText == null)
        {
            return;
        }

        SetPerformanceStatsActive(true);

        if (fpsText != null)
        {
            float averageFps = _fpsSamples.Count > 0 ? _fpsSampleSum / _fpsSamples.Count : 0f;
            fpsText.text = $"{Mathf.RoundToInt(averageFps)} FPS";
        }

        if (pingText != null)
        {
            float averagePing = _pingSamples.Count > 0 ? _pingSampleSum / _pingSamples.Count : 0f;
            pingText.text = $"{Mathf.RoundToInt(averagePing)} ms";
        }
    }

    private float GetCurrentPingMilliseconds()
    {
        if (!useNetworkSession || NetworkManager.Singleton == null)
        {
            return 0f;
        }

        Unity.Netcode.Transports.UTP.UnityTransport transport =
            NetworkManager.Singleton.NetworkConfig.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport;
        if (transport == null)
        {
            return 0f;
        }

        ulong currentRttMs = transport.GetCurrentRtt(NetworkManager.ServerClientId);

        // This project wanted the displayed "ping" to be a one-way estimate rather than full RTT.
        return currentRttMs * 0.5f;
    }

    private static void AddRollingSample(Queue<float> samples, ref float sum, float value, int maxSamples)
    {
        if (samples == null)
        {
            return;
        }

        samples.Enqueue(value);
        sum += value;

        while (samples.Count > maxSamples)
        {
            sum -= samples.Dequeue();
        }
    }

    /// <summary>
    /// Syncs both win tracker UIs with the current win counts.
    /// </summary>
    private void UpdateWinTrackers()
    {
        if (player1WinTracker != null) player1WinTracker.SetWins(player1Wins);
        if (player2WinTracker != null) player2WinTracker.SetWins(player2Wins);
    }

    private void ConfigureNetworkHudCanvases()
    {
        if (!useNetworkSession)
        {
            return;
        }

        Camera uiCamera = ResolveNetworkPresentationCamera();
        ConfigureNetworkCameraCanvas(player1HUDCanvas, uiCamera, 200);
        ConfigureNetworkCameraCanvas(player2HUDCanvas, uiCamera, 210);
    }

    private Camera ResolveNetworkPresentationCamera()
    {
        if (networkPresentationCamera != null)
        {
            return networkPresentationCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera candidate in cameras)
        {
            if (candidate != null && candidate.name == "UICamera")
            {
                networkPresentationCamera = candidate;
                return networkPresentationCamera;
            }
        }

        if (player1HUDCanvas != null && player1HUDCanvas.worldCamera != null)
        {
            networkPresentationCamera = player1HUDCanvas.worldCamera;
            return networkPresentationCamera;
        }

        networkPresentationCamera = Camera.main;
        return networkPresentationCamera;
    }

    private static void ConfigureNetworkCameraCanvas(Canvas canvas, Camera uiCamera, int sortingOrder)
    {
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 100f;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    private void ConfigureNetworkCameraCanvasHierarchy(GameObject root, int baseSortingOrder)
    {
        if (root == null)
        {
            return;
        }

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        if (canvases == null || canvases.Length == 0)
        {
            return;
        }

        Camera uiCamera = ResolveNetworkPresentationCamera();
        for (int i = 0; i < canvases.Length; i++)
        {
            ConfigureNetworkCameraCanvas(canvases[i], uiCamera, baseSortingOrder + i);
        }
    }

    // ===== PLAYER SPAWNING =====
    private IEnumerator SpawnPlayers()
    {
        if (useNetworkSession)
        {
            yield return SpawnPlayersNetworked();
            yield break;
        }

        ResolveLocalGameplayDevices();

        player1 = SpawnPlayer(player1Data, player1SpawnPoint, "Player1", player1Augments);
        player2 = SpawnPlayer(player2Data, player2SpawnPoint, "Player2", player2Augments);

        // Wire up split screen
        if (splitScreenManager != null)
        {
            splitScreenManager.AssignPlayers(player1.gameObject, player2.gameObject);
        }

        yield return null;
    }

    private IEnumerator SpawnPlayersNetworked()
    {
        NetworkSessionData session = NetworkSessionData.Instance;
        if (!isAuthoritativeNetworkController || session == null)
        {
            yield break;
        }

        ulong player1Owner = session.Player1Selection.ClientId;
        ulong player2Owner = session.Player2Selection.ClientId;

        player1 = SpawnNetworkPlayer(player1Data, player1SpawnPoint, "Player1", player1Owner, player1Augments);
        player2 = SpawnNetworkPlayer(player2Data, player2SpawnPoint, "Player2", player2Owner, player2Augments);

        yield return null;
        yield return WaitForLocalNetworkHudTarget();
        BindNetworkPresentation();
    }

    private IEnumerator WaitForLocalNetworkHudTarget()
    {
        if (!useNetworkSession)
        {
            yield break;
        }

        const float timeoutSeconds = 1.5f;
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (TryFindLocalOwnedNetworkPlayer(out _))
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[GameSceneManager] Timed out waiting for the local network player before HUD bind. HUD binding will still be attempted with current scene state.");
    }

    private Player SpawnPlayer(ShipData data, Transform spawnPoint, string tag, List<AugmentLoadoutEntry> existingAugments)
    {
        if (data == null || data.shipPrefab == null)
        {
            Debug.LogError($"Cannot spawn player: ShipData or shipPrefab is null for {tag}!");
            return null;
        }

        GameObject ship = Instantiate(data.shipPrefab, spawnPoint.position, spawnPoint.rotation);
        ship.tag = tag;

        Player player = ship.GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError($"Spawned ship prefab for {tag} has no Player component!");
            return null;
        }

        // Instantiate triggers Awake before this method continues, so refresh now using runtime tag.
        player.RefreshCombatTags();

        // Transfer augment loadout from previous rounds if one exists.
        if (existingAugments != null && existingAugments.Count > 0)
        {
            player.ImportAugmentLoadout(existingAugments, currentRound);
        }
        player.SetCurrentRound(currentRound);
        player.isMovementLocked = true;

        ConfigureLocalPlayerInput(player, tag);

        // Bind HUD directly from canvas reference (avoids inactive-object discovery issues)
        Canvas hudCanvas = (tag == "Player1") ? player1HUDCanvas : player2HUDCanvas;
        if (hudCanvas != null)
        {
            PlayerHUD ph = hudCanvas.GetComponent<PlayerHUD>();
            if (ph != null)
                player.BindHUD(ph);
            else
                Debug.LogWarning($"No PlayerHUD component found on {hudCanvas.name} for {tag}");
        }
        else
        {
            // Fallback to auto-discovery
            player.BindHUD();
        }

        // Bind ability HUD
        if (data.abilityHUDPrefab != null)
        {
            // Ensure there is only one runtime HUD instance per player.
            DestroyPlayerAbilityHUD(tag);

            GameObject hudObj = Instantiate(data.abilityHUDPrefab);
            var panel = hudObj.GetComponent<AbilityHUDPanel>();
            if (panel != null)
            {
                player.BindAbilityHUD(panel);
            }

            if (tag == "Player1") player1AbilityHUDInstance = hudObj;
            else if (tag == "Player2") player2AbilityHUDInstance = hudObj;

            // Assign the correct split-screen camera as the render camera
            // so the ability canvas renders on the right player's viewport
            Canvas abilityCanvas = hudObj.GetComponent<Canvas>();
            if (abilityCanvas == null)
                abilityCanvas = hudObj.GetComponentInChildren<Canvas>();

            if (abilityCanvas != null && splitScreenManager != null)
            {
                Camera cam = (tag == "Player1")
                    ? splitScreenManager.player1Camera
                    : splitScreenManager.player2Camera;
                abilityCanvas.worldCamera = cam;
            }
        }

        // Subscribe to death
        player.onDeath += OnPlayerDeath;

        return player;
    }

    private void ResolveLocalGameplayDevices()
    {
        _localPlayer1PrimaryDevice = null;
        _localPlayer1SecondaryDevice = null;
        _localPlayer2PrimaryDevice = null;
        _localPlayer2SecondaryDevice = null;

        if (Gamepad.all.Count >= 2)
        {
            _localPlayer1PrimaryDevice = Gamepad.all[0];
            _localPlayer2PrimaryDevice = Gamepad.all[1];
            return;
        }

        if (Keyboard.current != null)
        {
            _localPlayer1PrimaryDevice = Keyboard.current;
            if (Mouse.current != null)
            {
                _localPlayer1SecondaryDevice = Mouse.current;
            }
        }
        else if (Gamepad.all.Count >= 1)
        {
            _localPlayer1PrimaryDevice = Gamepad.all[0];
        }

        if (Gamepad.all.Count >= 1)
        {
            _localPlayer2PrimaryDevice = Gamepad.all[0];
        }
    }

    private void ConfigureLocalPlayerInput(Player player, string tag)
    {
        if (player == null)
        {
            return;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            return;
        }

        playerInput.enabled = true;

        InputDevice primaryDevice = tag == "Player1" ? _localPlayer1PrimaryDevice : _localPlayer2PrimaryDevice;
        InputDevice secondaryDevice = tag == "Player1" ? _localPlayer1SecondaryDevice : _localPlayer2SecondaryDevice;
        bool useKeyboardMouseScheme = primaryDevice is Keyboard;

        if (playerInput.user.valid)
        {
            playerInput.user.UnpairDevices();
        }

        if (useKeyboardMouseScheme)
        {
            if (secondaryDevice != null)
            {
                playerInput.SwitchCurrentControlScheme("key+mouse", primaryDevice, secondaryDevice);
            }
            else if (primaryDevice != null)
            {
                playerInput.SwitchCurrentControlScheme("key+mouse", primaryDevice);
            }
        }
        else if (primaryDevice != null)
        {
            playerInput.SwitchCurrentControlScheme("controller", primaryDevice);
        }

        if (primaryDevice != null)
        {
            InputUser.PerformPairingWithDevice(primaryDevice, playerInput.user);
        }

        if (secondaryDevice != null)
        {
            InputUser.PerformPairingWithDevice(secondaryDevice, playerInput.user);
        }

        playerInput.ActivateInput();
    }

    private Player SpawnNetworkPlayer(ShipData data, Transform spawnPoint, string tag, ulong ownerClientId, List<AugmentLoadoutEntry> existingAugments)
    {
        if (data == null || data.shipPrefab == null)
        {
            Debug.LogError($"Cannot network-spawn player: ShipData or shipPrefab is null for {tag}!");
            return null;
        }

        GameObject ship = NetMgr.SpawnPlayerNetworked(
            data.shipPrefab,
            spawnPoint != null ? spawnPoint.position : Vector3.zero,
            spawnPoint != null ? spawnPoint.rotation : Quaternion.identity,
            ownerClientId);

        if (ship == null)
        {
            return null;
        }

        ship.tag = tag;

        Player player = ship.GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError($"Spawned network ship prefab for {tag} has no Player component!");
            return null;
        }

        player.RefreshCombatTags();

        // Replicate the player index to clients so remote proxies get the correct
        // gameObject.tag and enemyTag (Unity tags are not replicated by NGO).
        NetMovement netMovement = ship.GetComponent<NetMovement>();
        if (netMovement != null)
        {
            byte playerIndex = (byte)(tag == "Player1" ? 1 : 2);
            netMovement.SetNetworkPlayerIndex(playerIndex);
        }

        if (existingAugments != null && existingAugments.Count > 0)
        {
            player.ImportAugmentLoadout(existingAugments, currentRound);
        }

        player.SetCurrentRound(currentRound);
        SetPlayerMovementLocked(player, true);
        player.onDeath += OnPlayerDeath;
        return player;
    }

    private void SavePlayerAugments()
    {
        if (player1 != null)
            player1Augments = player1.ExportAugmentLoadout();
        if (player2 != null)
            player2Augments = player2.ExportAugmentLoadout();
    }

    private void DestroyPlayers()
    {
        DestroyPlayerAbilityHUD("Player1");
        DestroyPlayerAbilityHUD("Player2");

        if (useNetworkSession)
        {
            DespawnPlayerNetworkObject(player1);
            DespawnPlayerNetworkObject(player2);
            player1 = null;
            player2 = null;
            return;
        }

        if (player1 != null)
        {
            player1.onDeath -= OnPlayerDeath;
            Destroy(player1.gameObject);
            player1 = null;
        }
        if (player2 != null)
        {
            player2.onDeath -= OnPlayerDeath;
            Destroy(player2.gameObject);
            player2 = null;
        }
    }

    private void DestroyPlayerAbilityHUD(string tag)
    {
        if (tag == "Player1")
        {
            if (player1AbilityHUDInstance != null)
            {
                Destroy(player1AbilityHUDInstance);
                player1AbilityHUDInstance = null;
            }
        }
        else if (tag == "Player2")
        {
            if (player2AbilityHUDInstance != null)
            {
                Destroy(player2AbilityHUDInstance);
                player2AbilityHUDInstance = null;
            }
        }
    }

    /// <summary>
    /// Captures each player's assigned Gamepad from their PlayerInput component
    /// and passes them to the AugmentSelectManager so only the active picker's
    /// controller can navigate during augment selection.
    /// Must be called BEFORE DestroyPlayers().
    /// </summary>
    private void CapturePlayerGamepads()
    {
        Gamepad p1Pad = null;
        Gamepad p2Pad = null;

        if (player1 != null)
        {
            var pi = player1.GetComponent<PlayerInput>();
            if (pi != null)
            {
                foreach (var device in pi.devices)
                {
                    if (device is Gamepad gp) { p1Pad = gp; break; }
                }
            }
        }

        if (player2 != null)
        {
            var pi = player2.GetComponent<PlayerInput>();
            if (pi != null)
            {
                foreach (var device in pi.devices)
                {
                    if (device is Gamepad gp) { p2Pad = gp; break; }
                }
            }
        }

        // Fallback: if we couldn't get from PlayerInput, use Gamepad.all ordering
        if (p1Pad == null && Gamepad.all.Count > 0) p1Pad = Gamepad.all[0];
        if (p2Pad == null && Gamepad.all.Count > 1) p2Pad = Gamepad.all[1];

        if (augmentSelectManager != null)
            augmentSelectManager.AssignGamepads(p1Pad, p2Pad);
    }

    // ===== DEATH HANDLING =====
    private void OnPlayerDeath(Entity deadEntity)
    {
        if (roundOver) return;
        roundOver = true;

        // Capture dead player stats NOW, before Die() destroys the gameObject
        Player deadPlayer = deadEntity as Player;
        if (deadPlayer != null)
        {
            deadPlayerShotsFired = deadPlayer.shotsFired;
            deadPlayerShotsHit = deadPlayer.shotsHit;
            deadPlayerDamageDealt = deadPlayer.damageDealt;
            deadPlayerDamageTaken = deadPlayer.damageTaken;
        }

        // Determine winner (the one who is still alive)
        if (deadEntity.gameObject.CompareTag("Player1"))
        {
            if (deadPlayer != null)
            {
                player1Augments = deadPlayer.ExportAugmentLoadout();
            }
            roundWinner = 2;
            player1 = null; // Already destroyed by Die()
        }
        else if (deadEntity.gameObject.CompareTag("Player2"))
        {
            if (deadPlayer != null)
            {
                player2Augments = deadPlayer.ExportAugmentLoadout();
            }
            roundWinner = 1;
            player2 = null; // Already destroyed by Die()
        }
    }

    // ===== STAT TRACKING =====
    private void CaptureRoundStats()
    {
        // Use saved dead player stats for the killed player, live stats for survivor
        if (roundWinner == 2) // Player 1 died
        {
            p1TotalShotsFired += deadPlayerShotsFired;
            p1TotalShotsHit += deadPlayerShotsHit;
            p1TotalDamageDealt += deadPlayerDamageDealt;
            p1TotalDamageTaken += deadPlayerDamageTaken;

            if (player2 != null)
            {
                p2TotalShotsFired += player2.shotsFired;
                p2TotalShotsHit += player2.shotsHit;
                p2TotalDamageDealt += player2.damageDealt;
                p2TotalDamageTaken += player2.damageTaken;
            }
        }
        else // Player 2 died
        {
            if (player1 != null)
            {
                p1TotalShotsFired += player1.shotsFired;
                p1TotalShotsHit += player1.shotsHit;
                p1TotalDamageDealt += player1.damageDealt;
                p1TotalDamageTaken += player1.damageTaken;
            }

            p2TotalShotsFired += deadPlayerShotsFired;
            p2TotalShotsHit += deadPlayerShotsHit;
            p2TotalDamageDealt += deadPlayerDamageDealt;
            p2TotalDamageTaken += deadPlayerDamageTaken;
        }
    }

    // ===== VS SCREEN =====
    private void OnVersusScreenComplete()
    {
        versusScreenDone = true;
    }

    // ===== MAP SELECTION =====
    private IEnumerator ActivateRandomMap()
    {
        if (maps == null || maps.Length == 0) yield break;

        // Pick a random map different from the current one if possible
        int newMapIndex;
        if (maps.Length == 1)
        {
            newMapIndex = 0;
        }
        else
        {
            do
            {
                newMapIndex = Random.Range(0, maps.Length);
            }
            while (maps[newMapIndex] == activeMapObject);
        }

        if (useNetworkSession)
        {
            NetworkSessionData.Instance?.SetSelectedMapIndexServer(newMapIndex);
            yield return null;
            yield break;
        }

        ApplySelectedMap(newMapIndex);
        yield return null;
    }

    // ===== ROUND TEXT & COUNTDOWN =====
    private IEnumerator ShowRoundText(int roundNumber)
    {
        if (roundText == null || roundTextCanvasGroup == null) yield break;

        roundText.text = $"ROUND {roundNumber}";
        yield return FadeCanvasGroup(roundTextCanvasGroup, 0f, 1f, textFadeDuration);
        yield return new WaitForSecondsRealtime(roundTextDisplayDuration);
        yield return FadeCanvasGroup(roundTextCanvasGroup, 1f, 0f, textFadeDuration);
    }

    private IEnumerator ShowCountdown()
    {
        if (countdownText == null || countdownCanvasGroup == null) yield break;

        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            yield return FadeCanvasGroup(countdownCanvasGroup, 0f, 1f, 0.15f);
            yield return new WaitForSecondsRealtime(countdownInterval - 0.3f);
            yield return FadeCanvasGroup(countdownCanvasGroup, 1f, 0f, 0.15f);
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        group.alpha = to;
    }

    private float GetRoundIntroDuration()
    {
        float singleCountdownStep = Mathf.Max(0f, countdownInterval);
        return (textFadeDuration * 2f) + roundTextDisplayDuration + (singleCountdownStep * 3f);
    }

    // ===== AUGMENT SELECTION =====
    private IEnumerator DoAugmentSelection()
    {
        if (augmentSelectManager == null) yield break;

        if (useNetworkSession)
        {
            NetworkSessionData session = NetworkSessionData.Instance;
            if (session == null || !isAuthoritativeNetworkController)
            {
                yield break;
            }

            int losingPlayer = lastRoundLoser;
            int winningPlayer = losingPlayer == 1 ? 2 : 1;
            int tier = augmentSelectManager.DrawNextTierForRound();

            List<Augment> loserPool = augmentSelectManager.DrawRandomAugmentsForTier(tier, 3);
            List<Augment> winnerPool = augmentSelectManager.DrawRandomAugmentsForTier(tier, 2);

            string[] player1Options = losingPlayer == 1
                ? loserPool.ConvertAll(augment => augment != null ? augment.augmentID : string.Empty).ToArray()
                : winnerPool.ConvertAll(augment => augment != null ? augment.augmentID : string.Empty).ToArray();
            string[] player2Options = losingPlayer == 2
                ? loserPool.ConvertAll(augment => augment != null ? augment.augmentID : string.Empty).ToArray()
                : winnerPool.ConvertAll(augment => augment != null ? augment.augmentID : string.Empty).ToArray();

            session.StartAugmentSelectionServer(tier, player1Options, player2Options, augmentSelectManager.SelectionTimeLimit);
            yield return new WaitUntil(() => session.AreAugmentSelectionsResolved);

            ApplyAugmentToPlayer(1, GameDataManager.Instance != null ? GameDataManager.Instance.GetAugmentById(session.GetResolvedAugmentIdForSlot(0)) : null);
            ApplyAugmentToPlayer(2, GameDataManager.Instance != null ? GameDataManager.Instance.GetAugmentById(session.GetResolvedAugmentIdForSlot(1)) : null);
            session.ClearResolvedAugmentSelectionsServer();
            yield return new WaitForSecondsRealtime(0.2f);
            yield break;
        }

        // Determine pick order: loser picks first
        int firstPicker = lastRoundLoser;
        int secondPicker = (firstPicker == 1) ? 2 : 1;

        // --- First picker ---
        augmentChosen = false;
        chosenAugment = null;
        chosenAugmentIndex = -1;

        augmentSelectManager.ShowAugmentSelect(firstPicker);

        // Wait for first pick
        yield return new WaitUntil(() => augmentChosen);

        Augment firstChoice = chosenAugment;
        int firstChoiceIndex = chosenAugmentIndex;

        // Apply augment to first picker
        ApplyAugmentToPlayer(firstPicker, firstChoice);

        // --- Transition to second picker (same screen, chosen card shrinks away) ---
        augmentChosen = false;
        chosenAugment = null;
        chosenAugmentIndex = -1;

        augmentSelectManager.TransitionToSecondPicker(firstChoiceIndex, secondPicker);

        // Wait for second pick
        yield return new WaitUntil(() => augmentChosen);

        // Apply augment to second picker
        ApplyAugmentToPlayer(secondPicker, chosenAugment);

        // Show final pick effect, then exit augment select.
        yield return augmentSelectManager.PlayFinalSelectionThenHide(chosenAugmentIndex);
    }

    private void OnAugmentChosen(Augment augment, int index)
    {
        if (useNetworkSession && NetworkSessionData.Instance != null && augment != null)
        {
            NetworkSessionData.Instance.RequestAugmentChoice(augment.augmentID);
            return;
        }

        chosenAugment = augment;
        chosenAugmentIndex = index;
        augmentChosen = true;
    }

    private void ApplyAugmentToPlayer(int playerNumber, Augment augment)
    {
        if (augment == null) return;

        // Add to persistent augment list (will be transferred on next spawn)
        if (playerNumber == 1)
        {
            player1Augments.Add(new AugmentLoadoutEntry
            {
                definition = augment,
                roundAcquired = currentRound,
                persistentState = null
            });
            // If player is currently alive, apply immediately
            if (player1 != null) player1.AcquireAugment(augment, currentRound);
        }
        else
        {
            player2Augments.Add(new AugmentLoadoutEntry
            {
                definition = augment,
                roundAcquired = currentRound,
                persistentState = null
            });
            if (player2 != null) player2.AcquireAugment(augment, currentRound);
        }
    }

    // ===== GAME END =====
    private IEnumerator ShowGameEnd()
    {
        if (gameEndScreenManager == null) yield break;

        _hidePerformanceStats = true;
        SetPlayerHUDsActive(false);
        SetPerformanceStatsActive(false);

        if (useNetworkSession)
        {
            NetworkSessionData.Instance?.SetLocalState(NetworkMatchState.MatchComplete, "Match complete.");
        }

        int winner = player1Wins >= winsRequired ? 1 : 2;
        float player1Accuracy = p1TotalShotsFired > 0 ? (float)p1TotalShotsHit / p1TotalShotsFired * 100f : 0f;
        float player2Accuracy = p2TotalShotsFired > 0 ? (float)p2TotalShotsHit / p2TotalShotsFired * 100f : 0f;

        if (useNetworkSession && NetworkSessionData.Instance != null)
        {
            NetworkSessionData.Instance.ShowGameEndServer(
                winner,
                player1Data,
                player2Data,
                totalGameDuration,
                player1Wins,
                player2Wins,
                p1TotalDamageDealt,
                p1TotalDamageTaken,
                player1Accuracy,
                player2Wins,
                player1Wins,
                p2TotalDamageDealt,
                p2TotalDamageTaken,
                player2Accuracy);
            yield break;
        }

        int winnerWins = winner == 1 ? player1Wins : player2Wins;
        int winnerLosses = winner == 1 ? player2Wins : player1Wins;
        float winnerDamageDealt = winner == 1 ? p1TotalDamageDealt : p2TotalDamageDealt;
        float winnerDamageTaken = winner == 1 ? p1TotalDamageTaken : p2TotalDamageTaken;
        float winnerAccuracy = winner == 1 ? player1Accuracy : player2Accuracy;

        gameEndScreenManager.ShowGameEndScreen(
            winner,
            winner,
            winner == 1 ? player1Data : player2Data,
            totalGameDuration,
            winnerWins,
            winnerLosses,
            winnerDamageDealt,
            winnerDamageTaken,
            winnerAccuracy);
    }

    // ===== CLEANUP =====
    private void OnDestroy()
    {
        if (versusScreenManager != null)
        {
            versusScreenManager.onVersusScreenComplete.RemoveListener(OnVersusScreenComplete);
        }
        if (augmentSelectManager != null)
        {
            augmentSelectManager.onAugmentChosen -= OnAugmentChosen;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            session.OnSelectedMapIndexChanged -= HandleSelectedMapIndexChanged;
            session.OnRoundStartPresentationChanged -= HandleRoundStartPresentationChanged;
            session.OnRoundEndPresentationChanged -= HandleRoundEndPresentationChanged;
            session.OnWinStateChanged -= HandleWinStateChanged;
            session.OnAugmentSelectionPresentationChanged -= HandleAugmentSelectionPresentationChanged;
            session.OnGameEndPresentationChanged -= HandleGameEndPresentationChanged;
            session.OnSelectionTimerChanged -= HandleSessionTimerChanged;
        }
    }

    private void HandleSelectedMapIndexChanged(int mapIndex)
    {
        if (!useNetworkSession)
        {
            return;
        }

        ApplySelectedMap(mapIndex);
    }

    private void ApplySelectedMap(int mapIndex)
    {
        if (maps == null || maps.Length == 0)
        {
            return;
        }

        if (mapIndex < 0 || mapIndex >= maps.Length)
        {
            return;
        }

        GameObject newMapObject = maps[mapIndex];
        if (newMapObject == activeMapObject)
        {
            return;
        }

        if (activeMapObject != null)
        {
            activeMapObject.SetActive(false);
        }

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i] != newMapObject)
            {
                maps[i].SetActive(false);
            }
        }

        activeMapObject = newMapObject;
        activeMapScript = activeMapObject != null ? activeMapObject.GetComponent<MapManagerScript>() : null;

        if (activeMapObject != null)
        {
            activeMapObject.SetActive(true);
        }
    }

    private void HandleRoundEndPresentationChanged(NetworkRoundEndStatePayload payload)
    {
        if (!useNetworkSession || roundEndScreenManager == null)
        {
            return;
        }

        if (payload.IsVisible)
        {
            SetPlayerHUDsActive(false);
            roundEndScreenManager.ShowRoundEndScreen(
                payload.WinningPlayer,
                payload.RoundDuration,
                payload.Player1Damage,
                payload.Player2Damage,
                payload.Player1Accuracy,
                payload.Player2Accuracy);
            return;
        }

        roundEndScreenManager.HideRoundEndScreen();
    }

    private void HandleWinStateChanged(NetworkWinStatePayload payload)
    {
        if (!useNetworkSession)
        {
            return;
        }

        player1Wins = payload.Player1Wins;
        player2Wins = payload.Player2Wins;
        UpdateWinTrackers();
    }

    private void HandleRoundStartPresentationChanged(NetworkRoundStartStatePayload payload)
    {
        if (!useNetworkSession)
        {
            return;
        }

        if (payload.SequenceId <= lastRoundIntroSequenceId)
        {
            return;
        }

        lastRoundIntroSequenceId = payload.SequenceId;

        if (activeRoundIntroCoroutine != null)
        {
            StopCoroutine(activeRoundIntroCoroutine);
        }

        activeRoundIntroCoroutine = StartCoroutine(PlayNetworkRoundIntro(payload.RoundNumber));
    }

    private IEnumerator PlayNetworkRoundIntro(int roundNumber)
    {
        if (roundTextCanvasGroup != null)
        {
            roundTextCanvasGroup.alpha = 0f;
        }

        if (countdownCanvasGroup != null)
        {
            countdownCanvasGroup.alpha = 0f;
        }

        yield return ShowRoundText(roundNumber);
        yield return ShowCountdown();
        activeRoundIntroCoroutine = null;
    }

    private void HandleAugmentSelectionPresentationChanged(NetworkAugmentSelectionStatePayload payload)
    {
        if (!useNetworkSession || augmentSelectManager == null)
        {
            return;
        }

        if (!payload.IsVisible)
        {
            augmentSelectManager.HideAugmentSelect();
            return;
        }

        if (augmentSelectManager.IsShowing)
        {
            return;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        if (session == null)
        {
            return;
        }

        int localSlot = session.GetLocalSlotIndex();
        if (localSlot < 0)
        {
            return;
        }

        string[] optionIds = localSlot == 0
            ? new[] { payload.Player1Option1.ToString(), payload.Player1Option2.ToString(), payload.Player1Option3.ToString() }
            : new[] { payload.Player2Option1.ToString(), payload.Player2Option2.ToString(), payload.Player2Option3.ToString() };
        int optionCount = localSlot == 0 ? payload.Player1OptionCount : payload.Player2OptionCount;

        List<Augment> augments = new List<Augment>(optionCount);
        for (int i = 0; i < optionCount; i++)
        {
            Augment augment = GameDataManager.Instance != null ? GameDataManager.Instance.GetAugmentById(optionIds[i]) : null;
            if (augment != null)
            {
                augments.Add(augment);
            }
        }

        if (augments.Count == 0)
        {
            return;
        }

        augmentSelectManager.ShowNetworkAugmentSelect(localSlot + 1, payload.Tier, augments);
        augmentSelectManager.SetCountdownValue(session.SelectionTimeRemaining);
    }

    private void HandleSessionTimerChanged(float timeRemaining)
    {
        if (!useNetworkSession || augmentSelectManager == null || !augmentSelectManager.IsShowing)
        {
            return;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        if (session == null || session.CurrentState != NetworkMatchState.AugmentPhase)
        {
            return;
        }

        augmentSelectManager.SetCountdownValue(timeRemaining);
    }

    private void HandleGameEndPresentationChanged(NetworkGameEndStatePayload payload)
    {
        if (!useNetworkSession || gameEndScreenManager == null)
        {
            return;
        }

        if (!payload.IsVisible)
        {
            _hidePerformanceStats = false;
            SetPerformanceStatsActive(true);
            gameEndScreenManager.HideGameEndScreen();
            return;
        }

        _hidePerformanceStats = true;
        SetPlayerHUDsActive(false);
        SetPerformanceStatsActive(false);

        NetworkSessionData session = NetworkSessionData.Instance;
        int localSlot = session != null ? session.GetLocalSlotIndex() : 0;
        bool localIsPlayer1 = localSlot != 1;

        ShipData localShip = GameDataManager.Instance != null
            ? GameDataManager.Instance.GetShipById(localIsPlayer1 ? payload.Player1ShipId.ToString() : payload.Player2ShipId.ToString())
            : null;
        int localPlayerNumber = localIsPlayer1 ? 1 : 2;
        int localWins = localIsPlayer1 ? payload.Player1Wins : payload.Player2Wins;
        int localLosses = localIsPlayer1 ? payload.Player1Losses : payload.Player2Losses;
        float localDamageDealt = localIsPlayer1 ? payload.Player1DamageDealt : payload.Player2DamageDealt;
        float localDamageTaken = localIsPlayer1 ? payload.Player1DamageTaken : payload.Player2DamageTaken;
        float localAccuracy = localIsPlayer1 ? payload.Player1Accuracy : payload.Player2Accuracy;

        gameEndScreenManager.ShowGameEndScreen(
            payload.WinningPlayer,
            localPlayerNumber,
            localShip,
            payload.GameDuration,
            localWins,
            localLosses,
            localDamageDealt,
            localDamageTaken,
            localAccuracy);
    }

    private IEnumerator ClientNetworkPresentationLoop()
    {
        while (true)
        {
            BindNetworkPresentation();
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void BindNetworkPresentation()
    {
        if (!useNetworkSession)
        {
            return;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null &&
            (session.CurrentState == NetworkMatchState.MatchComplete || session.CurrentState == NetworkMatchState.AugmentPhase))
        {
            ClearLocalNetworkHudBinding(destroyAbilityHudInstance: false);
            SetPlayerHUDsActive(false);
            return;
        }

        ConfigureNetworkHudCanvases();
        TryFindLocalOwnedNetworkPlayer(out Player localPlayer);

        if (localPlayer == null)
        {
            // The losing client has a gap between death/despawn and the next owner
            // spawn. If we keep the old bindings alive through that gap, the HUD can
            // stay attached to the dead player object's last replicated stats and the
            // old ability panel can remain hidden for the entire next round.
            ClearLocalNetworkHudBinding(destroyAbilityHudInstance: true);
            SetPlayerHUDsActive(false);
            return;
        }

        NetworkObject localPlayerNetworkObject = localPlayer.GetComponent<NetworkObject>();
        ulong localPlayerObjectId = localPlayerNetworkObject != null ? localPlayerNetworkObject.NetworkObjectId : ulong.MaxValue;
        bool localPlayerChanged = _boundNetworkHudObjectId != localPlayerObjectId;

        if (localPlayerChanged)
        {
            ClearLocalNetworkHudBinding(destroyAbilityHudInstance: true);
        }

        player1 = localPlayer;
        NetMovement localNetMovement = localPlayer.GetComponent<NetMovement>();
        localNetMovement?.EnsureOwnerLocalControlReady();
        BindPlayerToHud(localPlayer, player1HUDCanvas, "Player1", true);
        _boundNetworkHudObjectId = localPlayerObjectId;
        SetPlayerHUDsActive(true);
    }

    private bool TryFindLocalOwnedNetworkPlayer(out Player localPlayer)
    {
        localPlayer = null;

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player candidate in players)
        {
            NetMovement netMovement = candidate != null ? candidate.GetComponent<NetMovement>() : null;
            if (netMovement == null || !netMovement.IsSpawned)
            {
                continue;
            }

            if (!netMovement.IsOwner)
            {
                continue;
            }

            localPlayer = candidate;
            return true;
        }

        return false;
    }

    private void ClearLocalNetworkHudBinding(bool destroyAbilityHudInstance)
    {
        if (!useNetworkSession)
        {
            return;
        }

        player1 = null;
        _boundNetworkHudObjectId = ulong.MaxValue;

        if (destroyAbilityHudInstance)
        {
            DestroyPlayerAbilityHUD("Player1");
        }
    }

    private void BindPlayerToHud(Player player, Canvas hudCanvas, string tag, bool usePrimaryAbilityCanvas)
    {
        if (player == null)
        {
            return;
        }

        if (hudCanvas != null)
        {
            PlayerHUD ph = hudCanvas.GetComponent<PlayerHUD>();
            if (ph != null)
            {
                player.BindHUD(ph);
            }
        }

        if (player == null || player.GetComponent<NetMovement>() == null)
        {
            return;
        }

        ShipData data = ResolveAbilityHudShipData(player, tag);
        if (data == null || data.abilityHUDPrefab == null)
        {
            return;
        }

        GameObject currentHudInstance = usePrimaryAbilityCanvas ? player1AbilityHUDInstance : player2AbilityHUDInstance;
        if (currentHudInstance != null)
        {
            AbilityHUDPanel currentPanel = currentHudInstance.GetComponent<AbilityHUDPanel>();
            if (currentHudInstance.name.StartsWith(data.abilityHUDPrefab.name) && currentPanel != null)
            {
                if (currentPanel.BoundPlayer != player)
                {
                    player.BindAbilityHUD(currentPanel);
                }
                return;
            }

            Destroy(currentHudInstance);

            if (usePrimaryAbilityCanvas)
            {
                player1AbilityHUDInstance = null;
            }
            else
            {
                player2AbilityHUDInstance = null;
            }
        }

        GameObject hudObj = Instantiate(data.abilityHUDPrefab);
        AbilityHUDPanel panel = hudObj.GetComponent<AbilityHUDPanel>();
        if (panel != null)
        {
            player.BindAbilityHUD(panel);
        }

        Canvas abilityCanvas = hudObj.GetComponent<Canvas>();
        if (abilityCanvas == null)
        {
            abilityCanvas = hudObj.GetComponentInChildren<Canvas>();
        }

        if (abilityCanvas != null)
        {
            abilityCanvas.worldCamera = ResolveNetworkPresentationCamera();
        }

        if (useNetworkSession)
        {
            ConfigureNetworkCameraCanvasHierarchy(hudObj, 300);
        }

        if (usePrimaryAbilityCanvas)
        {
            player1AbilityHUDInstance = hudObj;
        }
        else
        {
            player2AbilityHUDInstance = hudObj;
        }
    }

    private ShipData ResolveAbilityHudShipData(Player player, string fallbackTag)
    {
        if (!useNetworkSession)
        {
            return fallbackTag == "Player1" ? player1Data : player2Data;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        NetworkObject playerNetworkObject = player != null ? player.GetComponent<NetworkObject>() : null;
        if (session == null || playerNetworkObject == null)
        {
            return fallbackTag == "Player1" ? player1Data : player2Data;
        }

        ulong ownerClientId = playerNetworkObject.OwnerClientId;
        if (session.Player1Selection != null && session.Player1Selection.ClientId == ownerClientId)
        {
            return session.Player1Selection.ShipData ?? player1Data;
        }

        if (session.Player2Selection != null && session.Player2Selection.ClientId == ownerClientId)
        {
            return session.Player2Selection.ShipData ?? player2Data;
        }

        return fallbackTag == "Player1" ? player1Data : player2Data;
    }

    private void SetPlayersMovementLocked(bool isLocked)
    {
        SetPlayerMovementLocked(player1, isLocked);
        SetPlayerMovementLocked(player2, isLocked);
    }

    private void SetPlayerMovementLocked(Player player, bool isLocked)
    {
        if (player == null)
        {
            return;
        }

        if (useNetworkSession)
        {
            NetMovement netMovement = player.GetComponent<NetMovement>();
            if (netMovement != null)
            {
                netMovement.SetMovementLockedAuthoritative(isLocked);
                return;
            }
        }

        player.isMovementLocked = isLocked;
    }

    private void SetAbility4Locked(bool isLocked)
    {
        ApplyAbility4Lock(player1, isLocked);
        ApplyAbility4Lock(player2, isLocked);
    }

    private void ApplyAbility4Lock(Player player, bool isLocked)
    {
        if (player == null)
        {
            return;
        }

        if (useNetworkSession)
        {
            NetMovement netMovement = player.GetComponent<NetMovement>();
            if (netMovement != null)
            {
                netMovement.SetAbility4LockedAuthoritative(isLocked);
                return;
            }
        }

        if (isLocked)
        {
            player.LockAbility4();
        }
        else
        {
            player.UnlockAbility4();
        }
    }

    private static void DespawnPlayerNetworkObject(Player player)
    {
        if (player == null)
        {
            return;
        }

        NetworkObject netObject = player.GetComponent<NetworkObject>();
        if (netObject != null && netObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            netObject.Despawn(true);
        }
    }
}
