using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
// Removed TMPro usage

public class SceneManagerScript : MonoBehaviour
{
    // ==================== SERIALIZED FIELDS ====================

    [Header("GUI Settings")]
    public GUISkin guiSkin;

    [Header("Warning Text Settings")]
    [Tooltip("Base font size for 1080p screens")]
    [SerializeField] private int warningFontSize = 80;
    [Tooltip("Width of the text area. Increase this if text is wrapping to too many lines.")]
    [SerializeField] private float warningBoxWidthUI = 1600f;
    [Tooltip("Height of the text area.")]
    [SerializeField] private float warningBoxHeightUI = 400f;
    [Tooltip("Offset from the center of the screen. Positive = Down, Negative = Up")]
    [SerializeField] private float warningYOffset = 0f;

    [Header("Game Over Menu Settings")]
    [Tooltip("Font size for the Game Over title")]
    [SerializeField] private int gameOverTitleFontSize = 100;
    [Tooltip("Font size for the stats text")]
    [SerializeField] private int statLabelFontSize = 40;
    [Tooltip("Width of the stats container box")]
    [SerializeField] private float statsBoxWidth = 800f;
    [Tooltip("Height of the stats container box")]
    [SerializeField] private float statsBoxHeight = 500f;
    [Tooltip("Vertical offset for the stats table. Positive moves it down, Negative moves it up.")]
    [SerializeField] private float statsBoxYOffset = 0f;

    [Space(10)]
    [Tooltip("Vertical offset for the Buttons. Positive moves it down, Negative moves it up.")]
    [SerializeField] private float menuButtonYOffset = 300f;
    [Tooltip("Width of the buttons")]
    [SerializeField] private float menuButtonWidth = 350f;
    [Tooltip("Height of the buttons")]
    [SerializeField] private float menuButtonHeight = 80f;
    [Tooltip("Space between the Main Menu and Continue buttons")]
    [SerializeField] private float menuButtonSpacing = 40f;
    [Tooltip("Font size for menu buttons (base for 1080p)")]
    [SerializeField] private int menuButtonFontSize = 30;

    [Header("Game Over Animation Delays")]
    [Tooltip("Delay in seconds before the title appears")]
    [SerializeField] private float titleAppearDelay = 0.5f;
    [Tooltip("Delay in seconds before the stats table appears (relative to game over start)")]
    [SerializeField] private float statsTableAppearDelay = 1.0f;
    [Tooltip("Delay in seconds before the buttons appear (relative to game over start)")]
    [SerializeField] private float buttonsAppearDelay = 1.5f;

    [Header("Game Over Animation Settings")]
    [Tooltip("Duration of title fade-in animation")]
    [SerializeField] private float titleFadeDuration = 0.5f;
    [Tooltip("Duration of stats table fade-in and scale animation")]
    [SerializeField] private float statsTableAnimDuration = 0.4f;
    [Tooltip("Duration of buttons fade-in animation")]
    [SerializeField] private float buttonsFadeDuration = 0.3f;
    [Tooltip("Enable scale animation for stats table")]
    [SerializeField] private bool enableStatsTableScale = true;
    [Tooltip("Starting scale for stats table (0.0 to 1.0)")]
    [SerializeField] private float statsTableStartScale = 0.8f;

    [Header("Victory Animation Delays")]
    [Tooltip("Delay in seconds before the victory title appears")]
    [SerializeField] private float victoryTitleAppearDelay = 0.5f;
    [Tooltip("Delay in seconds before the victory stats table appears")]
    [SerializeField] private float victoryStatsTableAppearDelay = 1.0f;
    [Tooltip("Delay in seconds before the victory buttons appear")]
    [SerializeField] private float victoryButtonsAppearDelay = 1.5f;

    [Header("Victory Animation Settings")]
    [Tooltip("Duration of victory title fade-in animation")]
    [SerializeField] private float victoryTitleFadeDuration = 0.5f;
    [Tooltip("Duration of victory stats table fade-in and scale animation")]
    [SerializeField] private float victoryStatsTableAnimDuration = 0.4f;
    [Tooltip("Duration of victory buttons fade-in animation")]
    [SerializeField] private float victoryButtonsFadeDuration = 0.3f;
    [Tooltip("Enable scale animation for victory stats table")]
    [SerializeField] private bool enableVictoryStatsTableScale = true;
    [Tooltip("Starting scale for victory stats table (0.0 to 1.0)")]
    [SerializeField] private float victoryStatsTableStartScale = 0.8f;

    [Header("Map Managers")]
    [Tooltip("Ordered list of MapManagers. The first one will be activated on scene start.")]
    [SerializeField] private List<MapManagerScript> mapManagers = new List<MapManagerScript>();

    [Header("Transition Settings")]
    [Tooltip("Time in seconds to wait before the next map activates.")]
    [SerializeField] private float mapTransitionDelay = 2.0f;
    [Tooltip("Time in seconds to wait after completing the last map before showing victory screen.")]
    [SerializeField] private float victoryScreenDelay = 2.0f;

    [Header("Boss Fight Settings")]
    [Tooltip("The boss prefab to spawn after the fake victory glitch")]
    [SerializeField] private GameObject bossPrefab;
    [Tooltip("Position to spawn the boss")]
    [SerializeField] private Vector2 bossSpawnPosition = new Vector2(0f, 30f);
    [Tooltip("Enable the fake victory glitch effect before boss fight")]
    [SerializeField] private bool enableBossFight = true;

    [Header("Glitch Effect Settings")]
    [Tooltip("Time to wait after victory appears before glitch starts")]
    [SerializeField] private float glitchStartDelay = 2.0f;
    [Tooltip("Total duration of the glitch effect sequence")]
    [SerializeField] private float glitchTotalDuration = 3.0f;
    [Tooltip("How chaotic the character corruption is (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float glitchCorruptionIntensity = 0.5f;
    [Tooltip("Audio clip for glitch sound effect")]
    [SerializeField] private AudioClip glitchSound;
    [Tooltip("Time between glitch sound plays")]
    [SerializeField] private float glitchSoundInterval = 0.15f;
    [Tooltip("Maximum shake offset in pixels")]
    [SerializeField] private float glitchShakeIntensity = 15f;
    [Tooltip("Maximum chromatic aberration during glitch")]
    [SerializeField] private float glitchChromaticIntensity = 1.5f;

    [Header("Boss Title Screen Settings")]
    [Tooltip("Duration to show the boss title screen")]
    [SerializeField] private float bossTitleDuration = 3.0f;
    [Tooltip("Title text for the boss")]
    [SerializeField] private string bossTitleText = "THE CRAIZAN STAR";
    [Tooltip("Font size for boss title")]
    [SerializeField] private int bossTitleFontSize = 120;
    [Tooltip("Color of the boss title text")]
    [SerializeField] private Color bossTitleTextColor = new Color(1f, 0.15f, 0.15f, 1f); // Red by default
    [Tooltip("Audio clip for boss title reveal")]
    [SerializeField] private AudioClip bossTitleSound;
    [Tooltip("How many seconds before the title appears to play the title sound (for syncing)")]
    [SerializeField] private float bossTitleSoundOffset = 0f;
    [Tooltip("Riser audio clip that builds up before the boss reveal")]
    [SerializeField] private AudioClip bossRiserSound;
    [Tooltip("How many seconds before the title appears to start the riser")]
    [SerializeField] private float bossRiserOffset = 2.0f;

    [Header("Boundary System")]
    // [SerializeField] private TMP_Text warningText; // Removed in favor of OnGUI
    [SerializeField] private List<GameObject> borderGuardians = new List<GameObject>();

    [Header("Boundary Zones")]
    [Tooltip("Width of the warning zone box (Gameplay Logic)")]
    [SerializeField] private float warningBoxWidth = 240f;
    [Tooltip("Height of the warning zone box (Gameplay Logic)")]
    [SerializeField] private float warningBoxHeight = 144f;
    [Tooltip("Width of the firing zone box (should be larger than warning box)")]
    [SerializeField] private float firingBoxWidth = 260f;
    [Tooltip("Height of the firing zone box (should be larger than warning box)")]
    [SerializeField] private float firingBoxHeight = 164f;
    [Tooltip("Center position of the boundary boxes")]
    [SerializeField] private Vector2 boxCenter = Vector2.zero;

    [Header("Audio")]
    [Tooltip("Reference to the background music AudioSource. If not assigned, will try to find one on this GameObject.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Key to toggle music on/off")]
    [SerializeField] private KeyCode toggleMusicKey = KeyCode.M;

    [Header("Button Sounds")]
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip buttonClickSound;
    private AudioSource buttonAudioSource;

    [Header("Explosion Sound")]
    [SerializeField] private AudioClip playerExplosionSound;
    [Range(0f, 3f)]
    [SerializeField] private float playerExplosionVolume = 0.7f;

    [Header("Pause Menu Settings")]
    [Tooltip("Font size for the Paused title")]
    [SerializeField] private int pauseTitleFontSize = 100;
    [Tooltip("Vertical offset for the pause title. Positive moves it down, Negative moves it up.")]
    [SerializeField] private float pauseTitleYOffset = -150f;
    [Tooltip("Width of the pause menu buttons")]
    [SerializeField] private float pauseButtonWidth = 350f;
    [Tooltip("Height of the pause menu buttons")]
    [SerializeField] private float pauseButtonHeight = 80f;
    [Tooltip("Space between pause menu buttons")]
    [SerializeField] private float pauseButtonSpacing = 20f;
    [Tooltip("Font size for pause menu buttons (base for 1080p)")]
    [SerializeField] private int pauseButtonFontSize = 30;
    [Tooltip("Vertical offset for the pause buttons. Positive moves it down, Negative moves it up.")]
    [SerializeField] private float pauseButtonYOffset = 50f;

    [Header("Pause Menu Animation")]
    [Tooltip("Duration of pause menu fade-in animation")]
    [SerializeField] private float pauseFadeInDuration = 0.2f;
    [Tooltip("Enable darkened background overlay when paused")]
    [SerializeField] private bool enablePauseOverlay = true;
    [Tooltip("Alpha value for the pause background overlay (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseOverlayAlpha = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [Tooltip("Enable map skipping with keyboard shortcut")]
    [SerializeField] private bool enableMapSkipping = false;
    [Tooltip("Key to press to skip to next map")]
    [SerializeField] private KeyCode skipMapKey = KeyCode.N;
    [Tooltip("Enable boss fight skip with keyboard shortcut")]
    [SerializeField] private bool enableBossSkip = true;
    [Tooltip("Key to press to skip to boss fight (wave 6 completion)")]
    [SerializeField] private KeyCode skipToBossKey = KeyCode.B;

    // ==================== PRIVATE FIELDS ====================

    private int currentMapIndex = -1;
    private GameObject player;
    private bool isWarningActive = false;
    private bool isFiringActive = false;

    // New flag to control Game Over state
    private bool showGameOverMenu = false;
    private float gameOverStartTime = 0f;

    // Victory menu state
    private bool showVictoryMenu = false;
    private float victoryStartTime = 0f;

    // Glitch effect state
    private bool isGlitching = false;
    private float glitchStartTime = 0f;
    private bool isFakeVictory = false;
    private string glitchedVictoryText = "VICTORY";
    private Color glitchedTextColor = Color.green;
    private bool glitchVisible = true;
    private float lastGlitchUpdateTime = 0f;
    private float lastGlitchSoundTime = 0f;
    private Vector2 glitchShakeOffset = Vector2.zero;
    private PlayerScript playerScript;

    // Boss title screen state
    private bool showBossTitle = false;
    private float bossTitleStartTime = 0f;
    private bool hasPlayedRiser = false;
    private bool hasPlayedTitleSound = false;
    private float glitchEndScheduledTime = 0f; // When the glitch will end and title appears

    // Characters used for text corruption
    private static readonly char[] glitchChars = new char[] {
        '!', '@', '#', '$', '%', '^', '&', '*', '?', '/', '\\', '|',
        '█', '▓', '▒', '░', '■', '□', '▪', '▫', '▲', '▼', '◄', '►',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'Ξ', 'Ψ', 'Ω', 'Σ', 'Δ', 'Φ', 'Γ', 'Λ', 'Π', 'Θ'
    };

    // Track enemies defeated across all levels
    private int totalEnemiesDefeated = 0;

    // Pause menu state
    private bool isPaused = false;
    private float pauseStartTime = 0f;

    // Music state
    private bool isMusicEnabled = true;
    private bool isMusicFadingOut = false;
    private float musicFadeStartTime = 0f;
    private float musicFadeDuration = 2.0f;
    private float originalMusicVolume = 1f;

    // Button hover tracking
    private int lastHoveredButtonId = -1;

    // ==================== UNITY LIFECYCLE ====================

    void Start()
    {
        // Reset game statistics
        totalEnemiesDefeated = 0;

        // Auto-find AudioSource if not assigned
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
        }

        // Load music preference from PlayerPrefs
        isMusicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        if (musicSource != null)
        {
            musicSource.mute = !isMusicEnabled;
        }

        // Get or create AudioSource for button sounds
        buttonAudioSource = gameObject.AddComponent<AudioSource>();

        InitializeMapManagers();
        player = GameObject.FindGameObjectWithTag("Player");

        // Cache PlayerScript reference for chromatic aberration control
        if (player != null)
        {
            playerScript = player.GetComponent<PlayerScript>();
        }
    }

    void Update()
    {
        // Only check boundaries if the game is still running (not game over)
        if (!showGameOverMenu)
        {
            CheckBoundaries();
        }

        // Debug: Skip to next map
        if (enableMapSkipping && Input.GetKeyDown(skipMapKey))
        {
            if (showDebugLogs)
            {
                Debug.Log($"Debug: Skipping to next map (pressed {skipMapKey})");
            }

            // Check if there's a next map to skip to
            if (HasNextMap())
            {
                ActivateNextMap();
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.Log("Debug: No more maps to skip to. Already on last map.");
                }
            }
        }

        // Debug: Skip to boss fight
        if (enableBossSkip && Input.GetKeyDown(skipToBossKey) && !showVictoryMenu && !showGameOverMenu)
        {
            if (showDebugLogs)
            {
                Debug.Log($"Debug: Skipping to boss fight (pressed {skipToBossKey})");
            }
            StartCoroutine(SkipToBossFight());
        }

        // Toggle music
        if (Input.GetKeyDown(toggleMusicKey))
        {
            ToggleMusic();
        }
    }

    void OnGUI()
    {
        if (guiSkin != null) GUI.skin = guiSkin;
        float scaleFactor = Screen.height / 1080f;

        // --- 0. BOSS TITLE SCREEN (takes priority over everything except game over) ---
        if (showBossTitle)
        {
            DrawBossTitleScreen(scaleFactor);
        }
        // --- 1. GAME OVER MENU ---
        else if (showGameOverMenu)
        {
            DrawGameOverMenu(scaleFactor);
        }
        // --- 2. VICTORY MENU ---
        else if (showVictoryMenu)
        {
            DrawVictoryMenu(scaleFactor);
        }
        // --- 3. PAUSE MENU ---
        else if (isPaused)
        {
            DrawPauseMenu(scaleFactor);
        }
        // --- 4. WARNING TEXT (Only show if game is NOT over, NOT victory, and NOT paused) ---
        else if (isWarningActive)
        {
            DrawWarningText(scaleFactor);
        }
    }

    // ==================== GUI DRAWING HELPERS ====================

    private void DrawGameOverMenu(float scaleFactor)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = screenW / 2f;
        float centerY = screenH / 2f;

        // Calculate elapsed time since game over started
        float elapsedTime = Time.time - gameOverStartTime;

        // 1. Darken Background - REMOVED as per request
        // GUI.Box(new Rect(0, 0, screenW, screenH), "");

        // 2. "GAME OVER" Title - Only show after titleAppearDelay
        if (elapsedTime >= titleAppearDelay)
        {
            // Calculate fade-in progress (0 to 1)
            float titleTimeSinceStart = elapsedTime - titleAppearDelay;
            float titleAlpha = Mathf.Clamp01(titleTimeSinceStart / titleFadeDuration);

            // Save original GUI color and apply fade
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, titleAlpha);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = Mathf.RoundToInt(gameOverTitleFontSize * scaleFactor);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.red;
            // Explicitly set hover color to the same as normal to remove hover effect
            titleStyle.hover.textColor = Color.red;

            float titleHeight = 150f * scaleFactor;
            // Position title above the stats box
            float titleY = centerY - (statsBoxHeight * scaleFactor / 2f) - titleHeight;

            GUI.Label(new Rect(centerX - (500 * scaleFactor), titleY, 1000 * scaleFactor, titleHeight), "GAME OVER", titleStyle);

            // Restore original GUI color
            GUI.color = originalColor;
        }

        // 3. Stats Table (Box) - Only show after statsTableAppearDelay
        if (elapsedTime >= statsTableAppearDelay)
        {
            // Calculate fade-in and scale progress (0 to 1)
            float statsTimeSinceStart = elapsedTime - statsTableAppearDelay;
            float statsAlpha = Mathf.Clamp01(statsTimeSinceStart / statsTableAnimDuration);
            float statsScale = enableStatsTableScale
                ? Mathf.Lerp(statsTableStartScale, 1f, statsAlpha)
                : 1f;

            float boxW = statsBoxWidth * scaleFactor * statsScale;
            float boxH = statsBoxHeight * scaleFactor * statsScale;
            float boxX = centerX - (boxW / 2f);

            // Calculate Y with the stats offset
            float boxY = centerY - (boxH / 2f) + (statsBoxYOffset * scaleFactor);

            // Save original GUI color and apply fade
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, statsAlpha);

            // Background for the stats
            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

            // 4. Placeholder Stats Inside Box
            GUIStyle statStyle = new GUIStyle(GUI.skin.label);
            statStyle.alignment = TextAnchor.MiddleLeft;
            statStyle.fontSize = Mathf.RoundToInt(statLabelFontSize * scaleFactor * statsScale);
            statStyle.normal.textColor = Color.white;
            statStyle.hover.textColor = Color.white; // Disable Hover

            GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
            valueStyle.alignment = TextAnchor.MiddleRight;
            valueStyle.fontSize = Mathf.RoundToInt(statLabelFontSize * scaleFactor * statsScale);
            valueStyle.normal.textColor = Color.yellow;
            valueStyle.hover.textColor = Color.yellow; // Disable Hover

            // Define row height and padding
            float rowHeight = 60f * scaleFactor * statsScale;
            float padding = 60f * scaleFactor * statsScale;
            float currentY = boxY + padding;
            float contentWidth = boxW - (padding * 2);
            float labelX = boxX + padding;

            // -- Row 1: Wave --
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), "Wave Reached:", statStyle);
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), (currentMapIndex + 1).ToString(), valueStyle);
            currentY += rowHeight;

            // -- Row 2: Enemies Defeated --
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), "Enemies Defeated:", statStyle);
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), totalEnemiesDefeated.ToString(), valueStyle);

            // Restore original GUI color
            GUI.color = originalColor;
        }

        // 5. Buttons (Main Menu & Continue) - Only show after buttonsAppearDelay
        if (elapsedTime >= buttonsAppearDelay)
        {
            // Calculate fade-in progress (0 to 1)
            float buttonsTimeSinceStart = elapsedTime - buttonsAppearDelay;
            float buttonsAlpha = Mathf.Clamp01(buttonsTimeSinceStart / buttonsFadeDuration);

            // Save original GUI color and apply fade
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, buttonsAlpha);

            float btnWidth = menuButtonWidth * scaleFactor;
            float btnHeight = menuButtonHeight * scaleFactor;
            float spacing = menuButtonSpacing * scaleFactor;

            float btnY = centerY + (menuButtonYOffset * scaleFactor);

            // Calculate width of the whole button row to center it
            float totalButtonRowWidth = (btnWidth * 2) + spacing;
            float startX = centerX - (totalButtonRowWidth / 2f);

            // Create scaled button style
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = Mathf.RoundToInt(menuButtonFontSize * scaleFactor);

            // -- BUTTON 1: MAIN MENU --
            Rect mainMenuRect = new Rect(startX, btnY, btnWidth, btnHeight);
            if (mainMenuRect.Contains(Event.current.mousePosition))
            {
                HandleButtonHover(0);
            }
            if (GUI.Button(mainMenuRect, "MAIN MENU", buttonStyle))
            {
                PlayButtonSound(buttonClickSound);
                SceneManager.LoadScene("MainMenu");
            }

            // -- BUTTON 2: CONTINUE --
            Rect continueRect = new Rect(startX + btnWidth + spacing, btnY, btnWidth, btnHeight);
            if (continueRect.Contains(Event.current.mousePosition))
            {
                HandleButtonHover(1);
            }
            if (GUI.Button(continueRect, "CONTINUE", buttonStyle))
            {
                PlayButtonSound(buttonClickSound);
                // Reload the current scene
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            // Restore original GUI color
            GUI.color = originalColor;
        }
    }

    private void DrawVictoryMenu(float scaleFactor)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = screenW / 2f;
        float centerY = screenH / 2f;

        // Calculate elapsed time since victory started
        float elapsedTime = Time.time - victoryStartTime;

        // Start music fade 2 seconds before glitch starts (for fake victory)
        float glitchStartTime_expected = glitchStartDelay + victoryButtonsAppearDelay;
        float musicFadeStartPoint = glitchStartTime_expected - musicFadeDuration;
        if (isFakeVictory && !isMusicFadingOut && elapsedTime >= musicFadeStartPoint)
        {
            StartMusicFadeOut();
        }

        // Update music fade
        if (isMusicFadingOut)
        {
            UpdateMusicFade();
        }

        // Check if we should start glitching (for fake victory before boss)
        if (isFakeVictory && !isGlitching && elapsedTime >= glitchStartTime_expected)
        {
            StartGlitchEffect();
        }

        // Update glitch effect if active
        if (isGlitching)
        {
            UpdateGlitchEffect();
        }

        // Calculate glitch-adjusted alpha (for fade out during glitch)
        float glitchFadeMultiplier = 1f;
        if (isGlitching)
        {
            float glitchElapsed = Time.time - glitchStartTime;
            float glitchProgress = glitchElapsed / glitchTotalDuration;

            // During the last 30% of glitch, fade everything out
            if (glitchProgress > 0.7f)
            {
                glitchFadeMultiplier = 1f - ((glitchProgress - 0.7f) / 0.3f);
            }

            // If glitch is not visible this frame, hide everything
            if (!glitchVisible)
            {
                glitchFadeMultiplier *= 0.1f; // Nearly invisible during flicker-off
            }
        }

        // 1. "VICTORY" Title - Only show after victoryTitleAppearDelay
        if (elapsedTime >= victoryTitleAppearDelay)
        {
            // Calculate fade-in progress (0 to 1)
            float titleTimeSinceStart = elapsedTime - victoryTitleAppearDelay;
            float titleAlpha = Mathf.Clamp01(titleTimeSinceStart / victoryTitleFadeDuration) * glitchFadeMultiplier;

            // Save original GUI color and apply fade
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, titleAlpha);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = Mathf.RoundToInt(gameOverTitleFontSize * scaleFactor);
            titleStyle.fontStyle = FontStyle.Bold;

            // Use glitched color if glitching, otherwise green
            Color textColor = isGlitching ? glitchedTextColor : Color.green;
            titleStyle.normal.textColor = textColor;
            titleStyle.hover.textColor = textColor;

            float titleHeight = 150f * scaleFactor;
            float titleY = centerY - (statsBoxHeight * scaleFactor / 2f) - titleHeight;

            // Apply shake offset during glitch
            float shakeX = isGlitching ? glitchShakeOffset.x : 0f;
            float shakeY = isGlitching ? glitchShakeOffset.y : 0f;

            // Use glitched text if glitching, otherwise "VICTORY"
            string displayText = isGlitching ? glitchedVictoryText : "VICTORY";
            GUI.Label(new Rect(centerX - (500 * scaleFactor) + shakeX, titleY + shakeY, 1000 * scaleFactor, titleHeight), displayText, titleStyle);

            GUI.color = originalColor;
        }

        // 2. Stats Table (Box) - Only show after victoryStatsTableAppearDelay
        if (elapsedTime >= victoryStatsTableAppearDelay)
        {
            // Calculate fade-in and scale progress (0 to 1)
            float statsTimeSinceStart = elapsedTime - victoryStatsTableAppearDelay;
            float statsAlpha = Mathf.Clamp01(statsTimeSinceStart / victoryStatsTableAnimDuration) * glitchFadeMultiplier;
            float statsScale = enableVictoryStatsTableScale
                ? Mathf.Lerp(victoryStatsTableStartScale, 1f, Mathf.Clamp01(statsTimeSinceStart / victoryStatsTableAnimDuration))
                : 1f;

            // Apply shake offset during glitch
            float shakeX = isGlitching ? glitchShakeOffset.x * 0.7f : 0f; // Slightly less shake than title
            float shakeY = isGlitching ? glitchShakeOffset.y * 0.7f : 0f;

            float boxW = statsBoxWidth * scaleFactor * statsScale;
            float boxH = statsBoxHeight * scaleFactor * statsScale;
            float boxX = centerX - (boxW / 2f) + shakeX;
            float boxY = centerY - (boxH / 2f) + (statsBoxYOffset * scaleFactor) + shakeY;

            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, statsAlpha);

            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

            GUIStyle statStyle = new GUIStyle(GUI.skin.label);
            statStyle.alignment = TextAnchor.MiddleLeft;
            statStyle.fontSize = Mathf.RoundToInt(statLabelFontSize * scaleFactor * statsScale);
            statStyle.normal.textColor = isGlitching ? glitchedTextColor : Color.white;
            statStyle.hover.textColor = isGlitching ? glitchedTextColor : Color.white;

            GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
            valueStyle.alignment = TextAnchor.MiddleRight;
            valueStyle.fontSize = Mathf.RoundToInt(statLabelFontSize * scaleFactor * statsScale);
            valueStyle.normal.textColor = isGlitching ? glitchedTextColor : Color.yellow;
            valueStyle.hover.textColor = isGlitching ? glitchedTextColor : Color.yellow;

            float rowHeight = 60f * scaleFactor * statsScale;
            float padding = 60f * scaleFactor * statsScale;
            float currentY = boxY + padding;
            float contentWidth = boxW - (padding * 2);
            float labelX = boxX + padding;

            // -- Row 1: Waves Completed -- (corrupt text during glitch)
            string wavesLabel = isGlitching ? CorruptText("Waves Completed:", Mathf.Clamp01((Time.time - glitchStartTime) / glitchTotalDuration) * 0.5f) : "Waves Completed:";
            string wavesValue = isGlitching ? CorruptText(mapManagers.Count.ToString(), Mathf.Clamp01((Time.time - glitchStartTime) / glitchTotalDuration) * 0.3f) : mapManagers.Count.ToString();
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), wavesLabel, statStyle);
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), wavesValue, valueStyle);
            currentY += rowHeight;

            // -- Row 2: Enemies Defeated -- (corrupt text during glitch)
            string enemiesLabel = isGlitching ? CorruptText("Enemies Defeated:", Mathf.Clamp01((Time.time - glitchStartTime) / glitchTotalDuration) * 0.5f) : "Enemies Defeated:";
            string enemiesValue = isGlitching ? CorruptText(totalEnemiesDefeated.ToString(), Mathf.Clamp01((Time.time - glitchStartTime) / glitchTotalDuration) * 0.3f) : totalEnemiesDefeated.ToString();
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), enemiesLabel, statStyle);
            GUI.Label(new Rect(labelX, currentY, contentWidth, rowHeight), enemiesValue, valueStyle);

            GUI.color = originalColor;
        }

        // 3. Buttons - Only show after victoryButtonsAppearDelay and NOT during fake victory (no buttons for fake victory)
        if (elapsedTime >= victoryButtonsAppearDelay && !isFakeVictory)
        {
            float buttonsTimeSinceStart = elapsedTime - victoryButtonsAppearDelay;
            float buttonsAlpha = Mathf.Clamp01(buttonsTimeSinceStart / victoryButtonsFadeDuration) * glitchFadeMultiplier;

            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, buttonsAlpha);

            float btnWidth = menuButtonWidth * scaleFactor;
            float btnHeight = menuButtonHeight * scaleFactor;
            float spacing = menuButtonSpacing * scaleFactor;
            float btnY = centerY + (menuButtonYOffset * scaleFactor);

            float totalButtonRowWidth = (btnWidth * 2) + spacing;
            float startX = centerX - (totalButtonRowWidth / 2f);

            // Create scaled button style
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = Mathf.RoundToInt(menuButtonFontSize * scaleFactor);

            // -- BUTTON 1: MAIN MENU --
            Rect mainMenuRect = new Rect(startX, btnY, btnWidth, btnHeight);
            if (mainMenuRect.Contains(Event.current.mousePosition))
            {
                HandleButtonHover(2);
            }
            if (GUI.Button(mainMenuRect, "MAIN MENU", buttonStyle))
            {
                PlayButtonSound(buttonClickSound);
                SceneManager.LoadScene("MainMenu");
            }

            // -- BUTTON 2: PLAY AGAIN --
            Rect playAgainRect = new Rect(startX + btnWidth + spacing, btnY, btnWidth, btnHeight);
            if (playAgainRect.Contains(Event.current.mousePosition))
            {
                HandleButtonHover(3);
            }
            if (GUI.Button(playAgainRect, "PLAY AGAIN", buttonStyle))
            {
                PlayButtonSound(buttonClickSound);
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            GUI.color = originalColor;
        }
    }

    private void DrawPauseMenu(float scaleFactor)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = screenW / 2f;
        float centerY = screenH / 2f;

        // Calculate elapsed time since pause started (use unscaled time since game is paused)
        float elapsedTime = Time.unscaledTime - pauseStartTime;
        float fadeAlpha = Mathf.Clamp01(elapsedTime / pauseFadeInDuration);

        // 1. Darkened Background Overlay
        if (enablePauseOverlay)
        {
            Color originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, pauseOverlayAlpha * fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);
            GUI.color = originalColor;
        }

        // Save original GUI color and apply fade
        Color savedColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha);

        // 2. "PAUSED" Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = Mathf.RoundToInt(pauseTitleFontSize * scaleFactor);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.hover.textColor = Color.white;

        float titleHeight = 150f * scaleFactor;
        float titleY = centerY + (pauseTitleYOffset * scaleFactor);

        GUI.Label(new Rect(centerX - (500 * scaleFactor), titleY, 1000 * scaleFactor, titleHeight), "PAUSED", titleStyle);

        // 3. Buttons (Resume, Main Menu, Quit) - stacked vertically
        float btnWidth = pauseButtonWidth * scaleFactor;
        float btnHeight = pauseButtonHeight * scaleFactor;
        float spacing = pauseButtonSpacing * scaleFactor;

        float btnX = centerX - (btnWidth / 2f);
        float btnStartY = centerY + (pauseButtonYOffset * scaleFactor);

        // Create scaled button style
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Mathf.RoundToInt(pauseButtonFontSize * scaleFactor);

        // -- BUTTON 1: RESUME --
        Rect resumeRect = new Rect(btnX, btnStartY, btnWidth, btnHeight);
        if (resumeRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(10); // Use unique IDs for pause menu buttons
        }
        if (GUI.Button(resumeRect, "RESUME", buttonStyle))
        {
            PlayButtonSound(buttonClickSound);
            ResumeGame();
        }

        // -- BUTTON 2: MAIN MENU --
        float mainMenuY = btnStartY + btnHeight + spacing;
        Rect mainMenuRect = new Rect(btnX, mainMenuY, btnWidth, btnHeight);
        if (mainMenuRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(11);
        }
        if (GUI.Button(mainMenuRect, "MAIN MENU", buttonStyle))
        {
            PlayButtonSound(buttonClickSound);
            // Restore time scale before loading scene
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        // -- BUTTON 3: QUIT --
        float quitY = mainMenuY + btnHeight + spacing;
        Rect quitRect = new Rect(btnX, quitY, btnWidth, btnHeight);
        if (quitRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(12);
        }
        if (GUI.Button(quitRect, "QUIT", buttonStyle))
        {
            PlayButtonSound(buttonClickSound);
            QuitGame();
        }

        // Restore original GUI color
        GUI.color = savedColor;
    }

    private void DrawWarningText(float scaleFactor)
    {
        // Define the style for the warning
        GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.alignment = TextAnchor.MiddleCenter;
        warningStyle.fontSize = Mathf.RoundToInt(warningFontSize * scaleFactor);
        warningStyle.fontStyle = FontStyle.Bold;

        // Force the color to Red in all states (Normal and Hover)
        warningStyle.normal.textColor = Color.red;
        warningStyle.hover.textColor = Color.red;

        // Define the text
        string text = "WARNING\nAPPROACHING DREADNOUGHT";

        // Define the position
        float width = warningBoxWidthUI * scaleFactor;
        float height = warningBoxHeightUI * scaleFactor;

        // Center X, and Center Y + Offset
        float x = (Screen.width / 2f) - (width / 2f);
        float y = (Screen.height / 2f) - (height / 2f) + (warningYOffset * scaleFactor);

        GUI.Label(new Rect(x, y, width, height), text, warningStyle);
    }

    private void DrawBossTitleScreen(float scaleFactor)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = screenW / 2f;
        float centerY = screenH / 2f;

        float elapsedTime = Time.time - bossTitleStartTime;
        float progress = elapsedTime / bossTitleDuration;

        // Check if title sequence is complete
        if (progress >= 1f)
        {
            EndBossTitleAndSpawnBoss();
            return;
        }

        // ===== FADE OUT ONLY (instant reveal, fade out at end) =====
        float fadeOutDuration = 0.8f;
        float fadeOutStart = bossTitleDuration - fadeOutDuration;
        
        // Calculate master alpha - instant reveal, fade out at end
        float masterAlpha = 1f;
        if (elapsedTime > fadeOutStart)
        {
            // Fade out
            masterAlpha = 1f - ((elapsedTime - fadeOutStart) / fadeOutDuration);
        }
        masterAlpha = Mathf.Clamp01(masterAlpha);

        // ===== CHROMATIC ABERRATION ON REVEAL =====
        if (playerScript != null)
        {
            // Chromatic aberration peaks at start, then fades down over 1 second
            float chromaticFadeDuration = 1.0f;
            float chromaticIntensity = 0f;
            if (elapsedTime < chromaticFadeDuration)
            {
                // Peak at start of reveal, then fade down
                chromaticIntensity = glitchChromaticIntensity * (1f - (elapsedTime / chromaticFadeDuration));
            }
            playerScript.SetChromaticAberrationIntensity(chromaticIntensity);
        }

        // ===== BACKGROUND: Pure black =====
        Color originalColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);
        
        // Reset GUI.color so text and particles render correctly
        GUI.color = Color.white;

        // ===== TITLE TEXT - Clean, thin style =====
        float titleAlpha = masterAlpha;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = Mathf.RoundToInt(bossTitleFontSize * scaleFactor);
        titleStyle.fontStyle = FontStyle.Normal; // Normal weight for thinner text

        // Main title color - use the tunable parameter
        Color titleColor = bossTitleTextColor;
        titleColor.a = titleAlpha;
        
        // Shadow color - darker version of the main color
        Color shadowColor = new Color(bossTitleTextColor.r * 0.2f, bossTitleTextColor.g * 0.2f, bossTitleTextColor.b * 0.2f, titleAlpha * 0.6f);
        
        // Just a subtle shadow for depth, no thick glow
        float shadowOffset = 3f * scaleFactor;
        
        // Set ALL text color states to ensure correct color regardless of GUISkin
        titleStyle.normal.textColor = shadowColor;
        titleStyle.hover.textColor = shadowColor;
        titleStyle.active.textColor = shadowColor;
        titleStyle.focused.textColor = shadowColor;
        
        GUI.Label(new Rect(centerX - (1000 * scaleFactor) + shadowOffset, 
            centerY - (150 * scaleFactor) + shadowOffset, 2000 * scaleFactor, 300 * scaleFactor), 
            bossTitleText, titleStyle);

        // Main text - clean and readable
        // Set ALL text color states to ensure correct color regardless of GUISkin
        titleStyle.normal.textColor = titleColor;
        titleStyle.hover.textColor = titleColor;
        titleStyle.active.textColor = titleColor;
        titleStyle.focused.textColor = titleColor;
        
        GUI.Label(new Rect(centerX - (1000 * scaleFactor), 
            centerY - (150 * scaleFactor), 2000 * scaleFactor, 300 * scaleFactor), 
            bossTitleText, titleStyle);

        // ===== SCANLINES EFFECT (very subtle) =====
        GUI.color = new Color(0f, 0f, 0f, 0.08f * masterAlpha);
        float scanlineHeight = 2f;
        for (float y = 0; y < screenH; y += scanlineHeight * 3f)
        {
            GUI.DrawTexture(new Rect(0, y, screenW, scanlineHeight), Texture2D.whiteTexture);
        }

        GUI.color = originalColor;
    }

    // ==================== BUTTON SOUND HELPERS ====================

    private void PlayButtonSound(AudioClip clip)
    {
        if (buttonAudioSource != null && clip != null)
        {
            buttonAudioSource.PlayOneShot(clip);
        }
    }

    public void PlayPlayerExplosionSound()
    {
        if (buttonAudioSource != null && playerExplosionSound != null)
        {
            buttonAudioSource.PlayOneShot(playerExplosionSound, playerExplosionVolume);
        }
    }

    private void HandleButtonHover(int buttonId)
    {
        if (lastHoveredButtonId != buttonId)
        {
            lastHoveredButtonId = buttonId;
            PlayButtonSound(buttonHoverSound);
        }
    }

    private void ResetHoverState()
    {
        lastHoveredButtonId = -1;
    }

    // ==================== GLITCH EFFECT SYSTEM ====================

    private void StartGlitchEffect()
    {
        isGlitching = true;
        glitchStartTime = Time.time;
        lastGlitchUpdateTime = Time.time;
        lastGlitchSoundTime = Time.time;
        glitchShakeOffset = Vector2.zero;
        
        // Reset audio state for boss title
        hasPlayedRiser = false;
        hasPlayedTitleSound = false;
        glitchEndScheduledTime = Time.time + glitchTotalDuration;

        // Play initial glitch sound
        PlayGlitchSound();

        if (showDebugLogs)
        {
            Debug.Log("Glitch effect started!");
        }
    }

    private void PlayGlitchSound()
    {
        if (glitchSound != null && buttonAudioSource != null)
        {
            // Vary the pitch slightly for variety
            buttonAudioSource.pitch = Random.Range(0.9f, 1.1f);
            buttonAudioSource.PlayOneShot(glitchSound, Random.Range(0.7f, 1f));
        }
    }

    private void UpdateGlitchEffect()
    {
        float glitchElapsed = Time.time - glitchStartTime;
        float glitchProgress = glitchElapsed / glitchTotalDuration;

        // Calculate time remaining until title appears
        float timeUntilTitle = glitchEndScheduledTime - Time.time;

        // Trigger riser sound at the right time
        if (!hasPlayedRiser && bossRiserSound != null && buttonAudioSource != null)
        {
            if (timeUntilTitle <= bossRiserOffset)
            {
                hasPlayedRiser = true;
                buttonAudioSource.PlayOneShot(bossRiserSound);
                if (showDebugLogs)
                {
                    Debug.Log($"Playing riser sound {bossRiserOffset}s before title");
                }
            }
        }

        // Trigger title sound at the right time (before title appears)
        if (!hasPlayedTitleSound && bossTitleSound != null && buttonAudioSource != null)
        {
            if (timeUntilTitle <= bossTitleSoundOffset)
            {
                hasPlayedTitleSound = true;
                buttonAudioSource.PlayOneShot(bossTitleSound);
                if (showDebugLogs)
                {
                    Debug.Log($"Playing title sound {bossTitleSoundOffset}s before title");
                }
            }
        }

        // Glitch is complete - show boss title screen
        if (glitchProgress >= 1f)
        {
            EndGlitchAndShowBossTitle();
            return;
        }

        // Play repeating glitch sounds
        float soundInterval = Mathf.Lerp(glitchSoundInterval * 2f, glitchSoundInterval * 0.5f, glitchProgress);
        if (Time.time - lastGlitchSoundTime >= soundInterval)
        {
            lastGlitchSoundTime = Time.time;
            PlayGlitchSound();
        }

        // Update chromatic aberration - increases with glitch progress
        if (playerScript != null)
        {
            float chromaticValue = Mathf.Lerp(0f, glitchChromaticIntensity, glitchProgress);
            // Add some random spikes
            if (Random.value < 0.3f)
            {
                chromaticValue *= Random.Range(1f, 1.5f);
            }
            playerScript.SetChromaticAberrationIntensity(chromaticValue);
        }

        // Update glitch visuals at varying intervals (faster as glitch progresses)
        float updateInterval = Mathf.Lerp(0.12f, 0.02f, glitchProgress);
        if (Time.time - lastGlitchUpdateTime >= updateInterval)
        {
            lastGlitchUpdateTime = Time.time;

            // Visibility: mostly visible with brief flashes off
            // Early: 95% visible, Late: 70% visible with more frequent flashes
            float visibleChance = Mathf.Lerp(0.95f, 0.6f, glitchProgress);
            glitchVisible = Random.value < visibleChance;

            // Shake offset - increases with progress
            float shakeAmount = glitchShakeIntensity * glitchProgress;
            glitchShakeOffset = new Vector2(
                Random.Range(-shakeAmount, shakeAmount),
                Random.Range(-shakeAmount, shakeAmount)
            );

            // Corrupt the victory text - more corruption over time
            glitchedVictoryText = CorruptText("VICTORY", glitchProgress);

            // Randomize color - more chaotic over time
            glitchedTextColor = GetGlitchColor(glitchProgress);
        }
    }

    private string CorruptText(string originalText, float intensity)
    {
        char[] chars = originalText.ToCharArray();
        float corruptionChance = glitchCorruptionIntensity * intensity;

        for (int i = 0; i < chars.Length; i++)
        {
            if (Random.value < corruptionChance)
            {
                // Replace with a random glitch character
                chars[i] = glitchChars[Random.Range(0, glitchChars.Length)];
            }
            else if (Random.value < corruptionChance * 0.5f)
            {
                // Occasionally swap case or shift character
                if (char.IsLetter(chars[i]))
                {
                    chars[i] = char.IsUpper(chars[i]) ? char.ToLower(chars[i]) : char.ToUpper(chars[i]);
                }
            }
        }

        // Occasionally add extra glitch characters
        if (Random.value < intensity * 0.3f)
        {
            int insertPos = Random.Range(0, chars.Length);
            string result = new string(chars);
            char extraChar = glitchChars[Random.Range(0, glitchChars.Length)];
            return result.Insert(insertPos, extraChar.ToString());
        }

        return new string(chars);
    }

    private Color GetGlitchColor(float progress)
    {
        // Array of glitch colors - starts with green variants, gets more chaotic
        Color[] earlyColors = new Color[]
        {
            Color.green,
            new Color(0f, 1f, 0.5f),  // Cyan-green
            new Color(0.5f, 1f, 0f),  // Yellow-green
        };

        Color[] lateColors = new Color[]
        {
            Color.red,
            Color.magenta,
            Color.cyan,
            new Color(1f, 0f, 0.5f),  // Pink
            new Color(1f, 0.5f, 0f),  // Orange
            Color.white,
            Color.black,
        };

        // Early in glitch, use mostly green variants
        if (progress < 0.4f)
        {
            return earlyColors[Random.Range(0, earlyColors.Length)];
        }
        // Late in glitch, use chaotic colors
        else if (progress > 0.7f)
        {
            return lateColors[Random.Range(0, lateColors.Length)];
        }
        // Middle - mix
        else
        {
            if (Random.value < 0.5f)
                return earlyColors[Random.Range(0, earlyColors.Length)];
            else
                return lateColors[Random.Range(0, lateColors.Length)];
        }
    }

    private void EndGlitchAndShowBossTitle()
    {
        // Reset glitch state
        isGlitching = false;
        isFakeVictory = false;
        showVictoryMenu = false;
        glitchedVictoryText = "VICTORY";
        glitchedTextColor = Color.green;
        glitchVisible = true;
        glitchShakeOffset = Vector2.zero;

        // Reset chromatic aberration (will be set again in DrawBossTitleScreen)
        if (playerScript != null)
        {
            playerScript.SetChromaticAberrationIntensity(glitchChromaticIntensity);
        }

        // Reset audio pitch
        if (buttonAudioSource != null)
        {
            buttonAudioSource.pitch = 1f;
        }

        if (showDebugLogs)
        {
            Debug.Log("Glitch effect ended. Showing boss title...");
        }

        // Show boss title screen
        showBossTitle = true;
        bossTitleStartTime = Time.time;
        
        // Note: Title sound is now triggered earlier in UpdateGlitchEffect with offset timing
    }

    private void EndBossTitleAndSpawnBoss()
    {
        showBossTitle = false;

        // Reset chromatic aberration
        if (playerScript != null)
        {
            playerScript.SetChromaticAberrationIntensity(0f);
        }

        if (showDebugLogs)
        {
            Debug.Log("Boss title ended. Spawning boss...");
        }

        // Spawn the boss
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("Boss prefab not assigned! Cannot spawn boss.");
            return;
        }

        // Instantiate the boss at the specified position
        Vector3 spawnPos = new Vector3(bossSpawnPosition.x, bossSpawnPosition.y, 0f);
        GameObject boss = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        boss.name = "Boss";

        if (showDebugLogs)
        {
            Debug.Log($"Boss spawned at {spawnPos}");
        }

        // Add boss tracker and link to last map manager for victory tracking
        // Use the last map manager since boss spawns after all waves are complete
        if (mapManagers.Count > 0)
        {
            // Get the last map manager (the one that was active when all waves completed)
            int lastMapIndex = mapManagers.Count - 1;
            var lastMap = mapManagers[lastMapIndex];
            
            // Use BossDestructionTracker for boss-specific behavior
            var tracker = boss.AddComponent<BossDestructionTracker>();
            tracker.mapManager = lastMap;

            // Increment the active enemy count on the last map
            lastMap.OnBossSpawned();
        }
    }

    /// <summary>
    /// Triggers a fake victory screen that will glitch and spawn the boss.
    /// Call this instead of the real victory when it's time for the boss fight.
    /// </summary>
    public void TriggerFakeVictory()
    {
        if (!enableBossFight || bossPrefab == null)
        {
            if (showDebugLogs)
            {
                Debug.Log("Boss fight disabled or no boss prefab. Showing real victory.");
            }
            // Fall back to real victory
            TriggerRealVictory();
            return;
        }

        isFakeVictory = true;
        showVictoryMenu = true;
        victoryStartTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log("Fake victory triggered! Glitch will start soon...");
        }
    }

    /// <summary>
    /// Triggers the real victory screen (after boss is defeated).
    /// </summary>
    public void TriggerRealVictory()
    {
        if (showDebugLogs)
        {
            Debug.Log("Showing real Victory menu.");
        }

        // Update missions completed stat
        int currentMissions = PlayerPrefs.GetInt("MissionsCompleted", 0);
        PlayerPrefs.SetInt("MissionsCompleted", currentMissions + 1);

        // Update total enemies defeated in PlayerPrefs
        int currentTotal = PlayerPrefs.GetInt("TotalEnemiesDefeated", 0);
        PlayerPrefs.SetInt("TotalEnemiesDefeated", currentTotal + totalEnemiesDefeated);

        PlayerPrefs.Save();

        // Show victory menu
        isFakeVictory = false;
        showVictoryMenu = true;
        victoryStartTime = Time.time;
    }

    /// <summary>
    /// Called when the boss is defeated. Triggers the real victory.
    /// </summary>
    public void OnBossDefeated()
    {
        if (showDebugLogs)
        {
            Debug.Log("Boss defeated! Triggering real victory...");
        }

        // Restart the music if it was stopped/faded
        RestartMusic();

        // Small delay before victory screen
        StartCoroutine(DelayedRealVictory());
    }

    /// <summary>
    /// Restarts the background music from the beginning.
    /// </summary>
    public void RestartMusic()
    {
        if (musicSource != null && isMusicEnabled)
        {
            // Reset fade state
            isMusicFadingOut = false;
            musicSource.volume = originalMusicVolume > 0 ? originalMusicVolume : 1f;
            
            // Restart from beginning
            musicSource.Stop();
            musicSource.Play();
            
            if (showDebugLogs)
            {
                Debug.Log("Music restarted.");
            }
        }
    }

    private IEnumerator DelayedRealVictory()
    {
        yield return new WaitForSeconds(victoryScreenDelay);
        TriggerRealVictory();
    }

    // ==================== PUBLIC API ====================

    public void OnPlayerDestroyed()
    {
        if (showDebugLogs)
        {
            Debug.Log("Player destroyed. Opening Game Over Menu.");
        }

        // Disable the warning systems so they don't overlap the menu
        isWarningActive = false;
        SetWarning(false);
        SetFiring(false);

        // Update total enemies defeated in PlayerPrefs
        int currentTotal = PlayerPrefs.GetInt("TotalEnemiesDefeated", 0);
        PlayerPrefs.SetInt("TotalEnemiesDefeated", currentTotal + totalEnemiesDefeated);
        PlayerPrefs.Save();

        // Trigger the Game Over Menu in OnGUI and record the start time
        showGameOverMenu = true;
        gameOverStartTime = Time.time;
    }

    /// <summary>
    /// Called by MapManagerScript when an enemy is destroyed.
    /// Tracks total enemies defeated across all levels.
    /// </summary>
    public void OnEnemyDefeated()
    {
        totalEnemiesDefeated++;
    }

    /// <summary>
    /// Toggles the background music on/off and saves the preference.
    /// </summary>
    public void ToggleMusic()
    {
        isMusicEnabled = !isMusicEnabled;

        if (musicSource != null)
        {
            musicSource.mute = !isMusicEnabled;
        }

        // Save preference
        PlayerPrefs.SetInt("MusicEnabled", isMusicEnabled ? 1 : 0);
        PlayerPrefs.Save();

        if (showDebugLogs)
        {
            Debug.Log($"Music toggled: {(isMusicEnabled ? "ON" : "OFF")}");
        }
    }

    /// <summary>
    /// Starts fading out the background music.
    /// </summary>
    private void StartMusicFadeOut()
    {
        if (musicSource == null || isMusicFadingOut) return;

        isMusicFadingOut = true;
        musicFadeStartTime = Time.time;
        originalMusicVolume = musicSource.volume;

        if (showDebugLogs)
        {
            Debug.Log("Starting music fade out...");
        }
    }

    /// <summary>
    /// Updates the music fade progress each frame.
    /// </summary>
    private void UpdateMusicFade()
    {
        if (musicSource == null || !isMusicFadingOut) return;

        float fadeElapsed = Time.time - musicFadeStartTime;
        float fadeProgress = fadeElapsed / musicFadeDuration;

        if (fadeProgress >= 1f)
        {
            // Fade complete
            musicSource.volume = 0f;
            musicSource.Stop();
            isMusicFadingOut = false;

            if (showDebugLogs)
            {
                Debug.Log("Music fade out complete.");
            }
        }
        else
        {
            // Interpolate volume
            musicSource.volume = Mathf.Lerp(originalMusicVolume, 0f, fadeProgress);
        }
    }

    // ==================== MAP MANAGEMENT - PUBLIC ====================

    /// <summary>
    /// Activates the next MapManager in the list.
    /// </summary>
    public void ActivateNextMap()
    {
        StartCoroutine(ActivateNextMapCoroutine());
    }

    /// <summary>
    /// Activates a specific MapManager by index.
    /// </summary>
    public void ActivateMap(int index)
    {
        if (index < 0 || index >= mapManagers.Count)
        {
            Debug.LogWarning($"Invalid map index: {index}. Valid range: 0-{mapManagers.Count - 1}");
            return;
        }

        // Deactivate current map
        if (currentMapIndex >= 0 && currentMapIndex < mapManagers.Count)
        {
            var currentMap = mapManagers[currentMapIndex];
            if (currentMap != null)
            {
                DeactivateMapManager(currentMap);
            }
        }

        // Activate specified map
        currentMapIndex = index;
        var targetMap = mapManagers[currentMapIndex];
        if (targetMap != null)
        {
            targetMap.gameObject.SetActive(true);

            if (showDebugLogs)
            {
                Debug.Log($"Activated MapManager: {targetMap.name} ({currentMapIndex + 1}/{mapManagers.Count})");
            }
        }
    }

    /// <summary>
    /// Gets the currently active MapManager.
    /// </summary>
    public MapManagerScript GetCurrentMap()
    {
        if (currentMapIndex >= 0 && currentMapIndex < mapManagers.Count)
        {
            return mapManagers[currentMapIndex];
        }
        return null;
    }

    /// <summary>
    /// Gets the current map index.
    /// </summary>
    public int GetCurrentMapIndex()
    {
        return currentMapIndex;
    }

    /// <summary>
    /// Gets the total number of maps.
    /// </summary>
    public int GetMapCount()
    {
        return mapManagers.Count;
    }

    /// <summary>
    /// Debug method: Skip to the last map (wave 6) and complete all waves to trigger boss fight.
    /// </summary>
    private IEnumerator SkipToBossFight()
    {
        // Stop any ongoing glitch effects
        isGlitching = false;
        isFakeVictory = false;
        showVictoryMenu = false;

        // Calculate the last map index (wave 6 would be index 5)
        int lastMapIndex = mapManagers.Count - 1;

        if (lastMapIndex < 0)
        {
            Debug.LogWarning("No maps available to skip to!");
            yield break;
        }

        // Deactivate current map if one is active
        if (currentMapIndex >= 0 && currentMapIndex < mapManagers.Count)
        {
            var currentMap = mapManagers[currentMapIndex];
            if (currentMap != null)
            {
                DeactivateMapManager(currentMap);
            }
        }

        // Activate the last map
        currentMapIndex = lastMapIndex;
        var lastMap = mapManagers[lastMapIndex];
        
        if (lastMap == null)
        {
            Debug.LogWarning($"MapManager at index {lastMapIndex} is null!");
            yield break;
        }

        lastMap.gameObject.SetActive(true);
        
        if (showDebugLogs)
        {
            Debug.Log($"Activated last map: {lastMap.name} (index {lastMapIndex})");
        }

        // Wait a frame for the map to initialize
        yield return null;

        // Force complete all waves on this map by setting wave count and clearing enemies
        // This simulates completing wave 6
        var enemyWaves = lastMap.transform.Find("EnemyWaves");
        if (enemyWaves != null)
        {
            // Destroy all existing enemies
            foreach (Transform wave in enemyWaves)
            {
                Destroy(wave.gameObject);
            }
        }

        // Use reflection to access private fields and force wave completion
        var mapType = lastMap.GetType();
        var currentWaveField = mapType.GetField("currentWave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeEnemyCountField = mapType.GetField("activeEnemyCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var isSpawningWavesField = mapType.GetField("isSpawningWaves", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (currentWaveField != null && activeEnemyCountField != null && isSpawningWavesField != null)
        {
            // Set current wave to total waves (completion)
            currentWaveField.SetValue(lastMap, lastMap.totalWaves);
            // Set active enemy count to 0
            activeEnemyCountField.SetValue(lastMap, 0);
            // Stop spawning waves
            isSpawningWavesField.SetValue(lastMap, false);

            if (showDebugLogs)
            {
                Debug.Log("Forced wave completion on last map");
            }

            // Wait a brief moment
            yield return new WaitForSeconds(0.5f);

            // Manually trigger the cleanup and next map activation
            // This will trigger the fake victory -> boss fight sequence
            var cleanupMethod = mapType.GetMethod("CleanupAndActivateNextMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cleanupMethod != null)
            {
                cleanupMethod.Invoke(lastMap, null);
                if (showDebugLogs)
                {
                    Debug.Log("Triggered map cleanup and boss fight sequence");
                }
            }
        }
        else
        {
            Debug.LogWarning("Could not access private fields for wave completion. Using alternate method.");
            
            // Alternate method: directly trigger fake victory
            yield return new WaitForSeconds(victoryScreenDelay);
            TriggerFakeVictory();
        }
    }

    /// <summary>
    /// Checks if there are more maps after the current one.
    /// </summary>
    public bool HasNextMap()
    {
        return currentMapIndex < mapManagers.Count - 1;
    }

    // ==================== MAP MANAGEMENT - PRIVATE ====================

    private void InitializeMapManagers()
    {
        // Disable all MapManagers initially
        foreach (var mapManager in mapManagers)
        {
            if (mapManager != null)
            {
                mapManager.gameObject.SetActive(false);
            }
        }

        // Activate the first MapManager
        ActivateNextMap();
    }

    /// <summary>
    /// Helper method to properly deactivate a MapManager and its UI elements.
    /// </summary>
    private void DeactivateMapManager(MapManagerScript mapManager)
    {
        if (mapManager == null) return;

        // Explicitly disable the wave text UI element
        if (mapManager.waveText != null)
        {
            mapManager.waveText.gameObject.SetActive(false);
        }

        // Deactivate the map manager itself
        mapManager.gameObject.SetActive(false);
    }

    private IEnumerator ActivateNextMapCoroutine()
    {
        // 1. Deactivate current map if one is active
        if (currentMapIndex >= 0 && currentMapIndex < mapManagers.Count)
        {
            var currentMap = mapManagers[currentMapIndex];
            if (currentMap != null)
            {
                DeactivateMapManager(currentMap);

                if (showDebugLogs)
                {
                    Debug.Log($"Deactivated MapManager: {currentMap.name}");
                }
            }
        }

        // 2. Wait for the delay (This creates the pause)
        // We only wait if we just finished a map (index >= 0) to avoid delay on very first start
        if (currentMapIndex >= 0)
        {
            yield return new WaitForSeconds(mapTransitionDelay);
        }

        // 3. Move to next map index
        currentMapIndex++;

        // 4. Check if there are more maps (Victory check)
        if (currentMapIndex >= mapManagers.Count)
        {
            if (showDebugLogs)
            {
                Debug.Log("All maps completed! Waiting for victory screen delay...");
            }

            // Wait before showing victory screen
            yield return new WaitForSeconds(victoryScreenDelay);

            // Check if we should trigger fake victory for boss fight
            if (enableBossFight && bossPrefab != null)
            {
                if (showDebugLogs)
                {
                    Debug.Log("Triggering fake victory for boss fight...");
                }

                // Trigger fake victory (will glitch and spawn boss)
                TriggerFakeVictory();

                // Don't save stats yet - wait until actual victory after boss
                yield break;
            }

            if (showDebugLogs)
            {
                Debug.Log("Showing Victory menu.");
            }

            // Update missions completed stat
            int currentMissions = PlayerPrefs.GetInt("MissionsCompleted", 0);
            PlayerPrefs.SetInt("MissionsCompleted", currentMissions + 1);

            // Update total enemies defeated in PlayerPrefs
            int currentTotal = PlayerPrefs.GetInt("TotalEnemiesDefeated", 0);
            PlayerPrefs.SetInt("TotalEnemiesDefeated", currentTotal + totalEnemiesDefeated);

            PlayerPrefs.Save();

            // Show victory menu instead of immediately going to main menu
            showVictoryMenu = true;
            victoryStartTime = Time.time;

            yield break; // Stop the coroutine here
        }

        // 5. Activate the next map
        var nextMap = mapManagers[currentMapIndex];
        if (nextMap != null)
        {
            nextMap.gameObject.SetActive(true);

            if (showDebugLogs)
            {
                Debug.Log($"Activated MapManager: {nextMap.name} ({currentMapIndex + 1}/{mapManagers.Count})");
            }

            // update player prefs for highest wave reached
            int highestWave = PlayerPrefs.GetInt("HighestWaveReached", 0);
            if (currentMapIndex + 1 > highestWave)
            {
                PlayerPrefs.SetInt("HighestWaveReached", currentMapIndex + 1);
                PlayerPrefs.Save();
            }
        }
        else
        {
            Debug.LogWarning($"MapManager at index {currentMapIndex} is null!");
        }
    }

    // ==================== BOUNDARY SYSTEM - PUBLIC ====================

    /// <summary>
    /// Sets the warning text visibility and border guardians.
    /// </summary>
    public void SetWarning(bool active)
    {
        // NOTE: Text is now handled in OnGUI via isWarningActive flag

        // Set all border guardians with the same state
        foreach (GameObject guardian in borderGuardians)
        {
            if (guardian != null)
            {
                guardian.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Sets border guardian firing. If true, finds closest guardian to player and activates firing.
    /// If false, deactivates firing for all guardians.
    /// </summary>
    public void SetFiring(bool active)
    {
        if (active)
        {
            // Find the player
            GameObject playerTarget = GameObject.FindGameObjectWithTag("Player");
            if (playerTarget == null) return;

            // Find the closest border guardian to the player
            GameObject closestGuardian = null;
            float closestDistance = float.MaxValue;

            foreach (GameObject guardian in borderGuardians)
            {
                if (guardian != null)
                {
                    float distance = Vector3.Distance(playerTarget.transform.position, guardian.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestGuardian = guardian;
                    }
                }
            }

            // Set the closest guardian's IsFiring to true
            if (closestGuardian != null)
            {
                BoundaryGuardianScript guardianScript = closestGuardian.GetComponent<BoundaryGuardianScript>();
                if (guardianScript != null)
                {
                    guardianScript.IsFiring = true;
                }
            }
        }
        else
        {
            // Set all guardians' IsFiring to false
            foreach (GameObject guardian in borderGuardians)
            {
                if (guardian != null)
                {
                    BoundaryGuardianScript guardianScript = guardian.GetComponent<BoundaryGuardianScript>();
                    if (guardianScript != null)
                    {
                        guardianScript.IsFiring = false;
                    }
                }
            }
        }
    }

    // ==================== BOUNDARY SYSTEM - PRIVATE ====================

    private void CheckBoundaries()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
        }

        Vector2 playerPos = player.transform.position;

        // Check if player is outside the warning box
        bool outsideWarningBox = IsOutsideBox(playerPos, warningBoxWidth, warningBoxHeight);

        // Check if player is outside the firing box
        bool outsideFiringBox = IsOutsideBox(playerPos, firingBoxWidth, firingBoxHeight);

        // Update warning state
        if (outsideWarningBox && !isWarningActive)
        {
            isWarningActive = true;
            SetWarning(true);
        }
        else if (!outsideWarningBox && isWarningActive)
        {
            isWarningActive = false;
            SetWarning(false);
        }

        // Update firing state
        if (outsideFiringBox && !isFiringActive)
        {
            isFiringActive = true;
            SetFiring(true);
        }
        else if (!outsideFiringBox && isFiringActive)
        {
            isFiringActive = false;
            SetFiring(false);
        }
    }

    private bool IsOutsideBox(Vector2 position, float boxWidth, float boxHeight)
    {
        float halfWidth = boxWidth / 2f;
        float halfHeight = boxHeight / 2f;

        bool outsideX = position.x < (boxCenter.x - halfWidth) || position.x > (boxCenter.x + halfWidth);
        bool outsideY = position.y < (boxCenter.y - halfHeight) || position.y > (boxCenter.y + halfHeight);

        return outsideX || outsideY;
    }

    // ==================== PUBLIC GETTERS FOR RADAR ====================

    public Vector2 GetWarningBoxSize()
    {
        return new Vector2(warningBoxWidth, warningBoxHeight);
    }

    public Vector2 GetBoxCenter()
    {
        return boxCenter;
    }

    public void TogglePause()
    {
        // Don't allow pause during game over or victory
        if (showGameOverMenu || showVictoryMenu)
        {
            return;
        }

        if (!isPaused)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }
    }

    private void PauseGame()
    {
        isPaused = true;
        pauseStartTime = Time.unscaledTime;
        Time.timeScale = 0f;
        ResetHoverState();

        // Play the click sound when bringing up the pause menu
        PlayButtonSound(buttonClickSound);

        // Pause the music
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
        }

        if (showDebugLogs)
        {
            Debug.Log("Game paused.");
        }
    }

    private void ResumeGame()
    {
        // Play the click sound when closing the pause menu
        PlayButtonSound(buttonClickSound);

        isPaused = false;
        Time.timeScale = 1f;
        ResetHoverState();

        // Resume the music if it was enabled
        if (musicSource != null && isMusicEnabled)
        {
            musicSource.UnPause();
        }

        if (showDebugLogs)
        {
            Debug.Log("Game resumed.");
        }
    }

    private void QuitGame()
    {
        if (showDebugLogs)
        {
            Debug.Log("Quitting game...");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Returns true if the game is currently paused.
    /// </summary>
    public bool IsPaused()
    {
        return isPaused;
    }
}