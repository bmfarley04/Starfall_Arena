using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using StarfallArena.UI;

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
    [Tooltip("Canvas containing Player 1 health/shield bars")]
    [SerializeField] private Canvas player1HUDCanvas;
    [Tooltip("Canvas containing Player 2 health/shield bars")]
    [SerializeField] private Canvas player2HUDCanvas;
    [Tooltip("Canvas containing Player 1 ability icons")]
    [SerializeField] private Canvas player1AbilityCanvas;
    [Tooltip("Canvas containing Player 2 ability icons")]
    [SerializeField] private Canvas player2AbilityCanvas;

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

    // Augment persistence between rounds
    private List<Augment> player1Augments = new List<Augment>();
    private List<Augment> player2Augments = new List<Augment>();

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

    // ===== INITIALIZATION =====
    void Start()
    {
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

        // Start in whole-screen mode for the VS screen
        if (splitScreenManager != null)
        {
            splitScreenManager.ActivateWholeScreen();
        }

        // Subscribe to VS screen completion
        if (versusScreenManager != null)
        {
            versusScreenManager.onVersusScreenComplete.AddListener(OnVersusScreenComplete);
        }

        // Subscribe to augment selection
        if (augmentSelectManager != null)
        {
            augmentSelectManager.onAugmentChosen += OnAugmentChosen;
        }

        // Start the game loop
        StartCoroutine(GameLoop());
    }

    private void ResolveShipData()
    {
        if (GameDataManager.Instance != null &&
            GameDataManager.Instance.selectedShipClasses != null &&
            GameDataManager.Instance.selectedShipClasses.Count >= 2)
        {
            player1Data = GameDataManager.Instance.selectedShipClasses[0];
            player2Data = GameDataManager.Instance.selectedShipClasses[1];
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
            Debug.Log($"[DEBUG] Skipping VS screen, starting at round {debugStartAtRound}");

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

            // Go straight to split-screen
            if (splitScreenManager != null)
            {
                splitScreenManager.ActivateSplitScreen();
            }
        }
        else
        {
            // --- VS SCREEN (whole-screen mode, already active from Start) ---
            // Wait for VS screen to complete (it runs automatically via its own Start())
            yield return new WaitUntil(() => versusScreenDone);

            // VS screen is done — switch to split-screen for gameplay
            if (splitScreenManager != null)
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
            if (splitScreenManager != null)
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
                player1.LockAbility4();
                player2.LockAbility4();
            }
            else
            {
                player1.UnlockAbility4();
                player2.UnlockAbility4();
            }

            // --- Map selection ---
            yield return ActivateRandomMap();

            // --- Round text ---
            yield return ShowRoundText(currentRound);

            // --- Countdown ---
            yield return ShowCountdown();

            // --- Unlock players and start round ---
            player1.isMovementLocked = false;
            player2.isMovementLocked = false;
            roundStartTime = Time.time;
            roundOver = false;
            roundWinner = 0;

            // --- Wait for someone to die ---
            yield return new WaitUntil(() => roundOver);

            // Lock surviving player
            if (player1 != null) player1.isMovementLocked = true;
            if (player2 != null) player2.isMovementLocked = true;

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
            if (roundEndScreenManager != null)
            {
                roundEndScreenManager.ShowRoundEndScreen(
                    roundWinner, roundDuration,
                    p1RoundDmg, p2RoundDmg,
                    p1RoundAcc, p2RoundAcc
                );
            }

            yield return new WaitForSecondsRealtime(roundEndScreenDuration);

            if (roundEndScreenManager != null)
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

            // Capture gamepad references before players are destroyed
            // (PlayerInput components hold the device assignments)
            CapturePlayerGamepads();

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
        // Deactivate ship visuals (hide models but keep references alive for stat capture)
        if (player1 != null) player1.gameObject.SetActive(false);
        if (player2 != null) player2.gameObject.SetActive(false);

        // Lerp both cameras back to the spawn point positions
        if (splitScreenManager != null)
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
        if (splitScreenManager != null)
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
        if (player1HUDCanvas != null) player1HUDCanvas.gameObject.SetActive(active);
        if (player2HUDCanvas != null) player2HUDCanvas.gameObject.SetActive(active);
        if (player1AbilityCanvas != null) player1AbilityCanvas.gameObject.SetActive(active);
        if (player2AbilityCanvas != null) player2AbilityCanvas.gameObject.SetActive(active);
    }

    // ===== PLAYER SPAWNING =====
    private IEnumerator SpawnPlayers()
    {
        player1 = SpawnPlayer(player1Data, player1SpawnPoint, "Player1", player1Augments);
        player2 = SpawnPlayer(player2Data, player2SpawnPoint, "Player2", player2Augments);

        // Wire up split screen
        if (splitScreenManager != null)
        {
            splitScreenManager.AssignPlayers(player1.gameObject, player2.gameObject);
        }

        yield return null;
    }

    private Player SpawnPlayer(ShipData data, Transform spawnPoint, string tag, List<Augment> existingAugments)
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

        // Transfer augments from previous round
        if (existingAugments != null && existingAugments.Count > 0)
        {
            player.augments = new List<Augment>(existingAugments);
        }

        player.currentRound = currentRound;
        player.isMovementLocked = true;

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
            GameObject hudObj = Instantiate(data.abilityHUDPrefab);
            var panel = hudObj.GetComponent<AbilityHUDPanel>();
            if (panel != null)
            {
                player.BindAbilityHUD(panel);
            }
        }

        // Subscribe to death
        player.onDeath += OnPlayerDeath;

        return player;
    }

    private void SavePlayerAugments()
    {
        if (player1 != null)
            player1Augments = new List<Augment>(player1.augments);
        if (player2 != null)
            player2Augments = new List<Augment>(player2.augments);
    }

    private void DestroyPlayers()
    {
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
            roundWinner = 2;
            player1 = null; // Already destroyed by Die()
        }
        else if (deadEntity.gameObject.CompareTag("Player2"))
        {
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
        GameObject newMapObj;
        if (maps.Length == 1)
        {
            newMapObj = maps[0];
        }
        else
        {
            do
            {
                newMapObj = maps[Random.Range(0, maps.Length)];
            }
            while (newMapObj == activeMapObject);
        }

        activeMapObject = newMapObj;
        activeMapScript = activeMapObject.GetComponent<MapManagerScript>();

        activeMapObject.SetActive(true);
        if (activeMapScript != null)
        {
            activeMapScript.SpawnAsteroids();
        }

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

    // ===== AUGMENT SELECTION =====
    private IEnumerator DoAugmentSelection()
    {
        if (augmentSelectManager == null) yield break;

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

        // Brief pause then hide
        yield return new WaitForSecondsRealtime(0.3f);
        augmentSelectManager.HideAugmentSelect();
    }

    private void OnAugmentChosen(Augment augment, int index)
    {
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
            player1Augments.Add(augment);
            // If player is currently alive, apply immediately
            if (player1 != null) player1.AcquireAugment(augment, currentRound);
        }
        else
        {
            player2Augments.Add(augment);
            if (player2 != null) player2.AcquireAugment(augment, currentRound);
        }
    }

    // ===== GAME END =====
    private IEnumerator ShowGameEnd()
    {
        if (gameEndScreenManager == null) yield break;

        int winner = player1Wins >= winsRequired ? 1 : 2;
        int loser = winner == 1 ? 2 : 1;

        ShipData winnerShipData = winner == 1 ? player1Data : player2Data;
        int winnerWins = winner == 1 ? player1Wins : player2Wins;
        int winnerLosses = winner == 1 ? player2Wins : player1Wins;

        float winnerDamageDealt = winner == 1 ? p1TotalDamageDealt : p2TotalDamageDealt;
        float winnerDamageTaken = winner == 1 ? p1TotalDamageTaken : p2TotalDamageTaken;
        int winnerShotsFired = winner == 1 ? p1TotalShotsFired : p2TotalShotsFired;
        int winnerShotsHit = winner == 1 ? p1TotalShotsHit : p2TotalShotsHit;
        float winnerAccuracy = winnerShotsFired > 0 ? (float)winnerShotsHit / winnerShotsFired * 100f : 0f;

        gameEndScreenManager.ShowGameEndScreen(
            winner,
            winnerShipData,
            totalGameDuration,
            winnerWins,
            winnerLosses,
            winnerDamageDealt,
            winnerDamageTaken,
            winnerAccuracy
        );
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
    }
}
