using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    public enum MenuLayoutMode
    {
        Stacked, 
        Spread   
    }

    public GUISkin guiSkin;

    [Header("Scene Configuration")]
    public string wavesScene = "AllWaves";

    [Header("UI References")]
    [Tooltip("Drag the 'Title' object from the Hierarchy here")]
    public RectTransform titleRectTransform;

    [Tooltip("The GameObject containing your ControlMenuManager script")]
    public GameObject controlsPanel;

    [Tooltip("The GameObject containing your StatsMenuManager script")]
    public GameObject statsPanel;

    [Header("Button Sounds")]
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip buttonClickSound;
    private AudioSource audioSource; 

    [Header("Menu Layout")]
    [SerializeField] private float baseButtonWidth = 400f;
    [SerializeField] private float baseButtonHeight = 100f;
    [SerializeField] private int baseFontSize = 30;

    [Header("Layout Settings")]
    [SerializeField] private MenuLayoutMode layoutMode = MenuLayoutMode.Stacked;
    [SerializeField] private float buttonSpacing = 20f;
    [SerializeField] private float screenEdgeMargin = 20f;

    [Header("Positioning (Offsets from Center)")]
    [SerializeField] private float buttonXOffset = 0f;
    [SerializeField] private float buttonGroupYOffset = 50f;
    private bool showStats = false;

    private Vector2 defaultTitleSize;
    private Vector2 defaultTitlePos;

    private bool showControls = false;

    private int lastHoveredButtonId = -1;

    void Start()
    {
        if (titleRectTransform != null)
        {
            defaultTitleSize = titleRectTransform.sizeDelta;
            defaultTitlePos = titleRectTransform.anchoredPosition;
        }

        // Ensure panels are hidden at start
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (statsPanel != null) statsPanel.SetActive(false);

        // Get or create AudioSource for button sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (titleRectTransform != null)
        {
            float scaleFactor = Screen.height / 1080f;
            titleRectTransform.sizeDelta = defaultTitleSize * scaleFactor;
            titleRectTransform.anchoredPosition = defaultTitlePos * scaleFactor;
        }

        // Reset hover state when no buttons are hovered
        if (!GUI.tooltip.Contains(""))
        {
            lastHoveredButtonId = -1;
        }
    }

    void OnGUI()
    {
        if (guiSkin != null) GUI.skin = guiSkin;

        float scaleFactor = Screen.height / 1080f;
        float scaledWidth = baseButtonWidth * scaleFactor;
        float scaledHeight = baseButtonHeight * scaleFactor;

        if (guiSkin != null)
        {
            GUI.skin.button.fontSize = Mathf.RoundToInt(baseFontSize * scaleFactor);
            GUI.skin.label.fontSize = Mathf.RoundToInt(baseFontSize * scaleFactor);
            GUI.skin.button.normal.textColor = Color.white;
        }

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        // --- 1. DRAW MAIN MENU BUTTONS ---
        if (layoutMode == MenuLayoutMode.Stacked)
        {
            DrawStackedLayout(centerX, centerY, scaledWidth, scaledHeight, scaleFactor);
        }
        else
        {
            DrawSpreadLayout(centerX, centerY, scaledWidth, scaledHeight, scaleFactor);
        }

        // --- 2. SYNC CONTROL PANEL STATE ---
        if (controlsPanel != null)
        {
            bool panelActive = controlsPanel.activeSelf;

            if (showControls && !panelActive)
            {
                // Opening the panel
                controlsPanel.SetActive(true);
            }
            else if (!showControls && panelActive)
            {
                // Closing the panel - check if animation is complete
                ControlMenuManager controlMenu = controlsPanel.GetComponent<ControlMenuManager>();
                if (controlMenu != null && controlMenu.IsAnimationComplete())
                {
                    controlsPanel.SetActive(false);
                }
            }
        }

        // --- 3. SYNC STATS PANEL STATE ---
        if (statsPanel != null)
        {
            bool panelActive = statsPanel.activeSelf;

            if (showStats && !panelActive)
            {
                // Opening the panel
                statsPanel.SetActive(true);
            }
            else if (!showStats && panelActive)
            {
                // Closing the panel - check if animation is complete
                StatsMenuManager statsMenu = statsPanel.GetComponent<StatsMenuManager>();
                if (statsMenu != null && statsMenu.IsAnimationComplete())
                {
                    statsPanel.SetActive(false);
                }
            }
        }
    }

    private void DrawStackedLayout(float centerX, float centerY, float w, float h, float scale)
    {
        float spacing = buttonSpacing * scale;
        float xPos = centerX + (buttonXOffset * scale) - (w / 2);

        float startY = centerY + (buttonGroupYOffset * scale);

        // If controls are open, we can optionally disable buttons (make them unclickable)
        // by wrapping them in GUI.enabled = !showControls;
        // But to keep them visible as requested:

        Rect startGameRect = new Rect(xPos, startY, w, h);
        if (startGameRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(0);
        }
        if (GUI.Button(startGameRect, "START GAME"))
        {
            StartGame();
        }

        float statsY = startY + h + spacing;
        Rect statsRect = new Rect(xPos, statsY, w, h);
        if (statsRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(1);
        }
        if (GUI.Button(statsRect, "STATS"))
        {
            OpenStats();
        }

        float controlsY = statsY + h + spacing;
        Rect controlsRect = new Rect(xPos, controlsY, w, h);
        if (controlsRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(2);
        }
        if (GUI.Button(controlsRect, "CONTROLS"))
        {
            OpenControls();
        }

        float quitY = controlsY + h + spacing;
        Rect quitRect = new Rect(xPos, quitY, w, h);
        if (quitRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(3);
        }
        if (GUI.Button(quitRect, "QUIT"))
        {
            QuitGame();
        }
    }

    private void DrawSpreadLayout(float centerX, float centerY, float w, float h, float scale)
    {
        float margin = screenEdgeMargin * scale;
        float xPos = centerX + (buttonXOffset * scale) - (w / 2);

        float startY = centerY + (buttonGroupYOffset * scale);
        Rect startGameRect = new Rect(xPos, startY, w, h);
        if (startGameRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(0);
        }
        if (GUI.Button(startGameRect, "START GAME"))
        {
            StartGame();
        }

        // Stats button - left side, above controls
        float statsX = margin;
        float statsY = Screen.height - (h * 2) - (margin * 2);
        Rect statsRect = new Rect(statsX, statsY, w, h);
        if (statsRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(1);
        }
        if (GUI.Button(statsRect, "STATS"))
        {
            OpenStats();
        }

        float controlsX = margin;
        float controlsY = Screen.height - h - margin;
        Rect controlsRect = new Rect(controlsX, controlsY, w, h);
        if (controlsRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(2);
        }
        if (GUI.Button(controlsRect, "CONTROLS"))
        {
            OpenControls();
        }

        float quitX = Screen.width - w - margin;
        float quitY = Screen.height - h - margin;
        Rect quitRect = new Rect(quitX, quitY, w, h);
        if (quitRect.Contains(Event.current.mousePosition))
        {
            HandleButtonHover(3);
        }
        if (GUI.Button(quitRect, "QUIT"))
        {
            QuitGame();
        }
    }

    private void PlayButtonSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
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

    public void StartGame()
    {
        PlayButtonSound(buttonClickSound);
        StartCoroutine(StartGameAfterSound());
    }

    private IEnumerator StartGameAfterSound()
    {
        float soundDuration = buttonClickSound != null ? buttonClickSound.length : 0f;
        yield return new WaitForSeconds(soundDuration);
        SceneManager.LoadScene(wavesScene);
    }

    public void OpenControls()
    {
        PlayButtonSound(buttonClickSound);
        if (showControls)
        {
            // Close with animation
            showControls = false;
            ControlMenuManager controlMenu = controlsPanel.GetComponent<ControlMenuManager>();
            if (controlMenu != null)
            {
                controlMenu.StartClosing();
            }
        }
        else
        {
            // Close stats if open
            if (showStats)
            {
                showStats = false;
                StatsMenuManager statsMenu = statsPanel.GetComponent<StatsMenuManager>();
                if (statsMenu != null)
                {
                    statsMenu.StartClosing();
                }
            }
            // Open controls
            showControls = true;
        }
    }

    public void OpenStats()
    {
        PlayButtonSound(buttonClickSound);
        if (showStats)
        {
            // Close with animation
            showStats = false;
            StatsMenuManager statsMenu = statsPanel.GetComponent<StatsMenuManager>();
            if (statsMenu != null)
            {
                statsMenu.StartClosing();
            }
        }
        else
        {
            // Close controls if open
            if (showControls)
            {
                showControls = false;
                ControlMenuManager controlMenu = controlsPanel.GetComponent<ControlMenuManager>();
                if (controlMenu != null)
                {
                    controlMenu.StartClosing();
                }
            }
            // Open stats
            showStats = true;
        }
    }

    public void QuitGame()
    {
        PlayButtonSound(buttonClickSound);
        Debug.Log("Quit Game Requested");
        Application.Quit();
    }
}