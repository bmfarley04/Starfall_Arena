using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // For TextMeshPro support

public class GameOverSceneManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI gameOverText; // Main "Game Over" title
    public TextMeshProUGUI scoreText; // Optional: Display final score
    public Button restartButton;
    public Button mainMenuButton;

    [Header("Scene Names")]
    public string mainMenuScene = "MainMenu";
    public string gameplayScene = "AllWaves"; // The main gameplay scene name

    [Header("Button Styling")]
    [SerializeField] private Color normalButtonColor = new Color(0.2f, 0.4f, 0.8f); // Blue
    [SerializeField] private Color hoverButtonColor = new Color(0.3f, 0.6f, 1f); // Light Blue
    [SerializeField] private Color pressedButtonColor = new Color(0.1f, 0.2f, 0.5f); // Dark Blue
    [SerializeField] private float buttonGlowIntensity = 1.5f;
    [SerializeField] private float buttonTextSize = 32f;

    [Header("Text Styling")]
    [SerializeField] private Color gameOverTextColor = new Color(1f, 0.3f, 0.3f); // Red
    [SerializeField] private float gameOverGlowIntensity = 2f;
    [SerializeField] private float gameOverFontSize = 72f;
    [SerializeField] private Color scoreTextColor = Color.white;
    [SerializeField] private float scoreFontSize = 36f;

    [Header("Animation")]
    [SerializeField] private bool enableGameOverPulse = true;
    [SerializeField] private float gameOverPulseSpeed = 1.5f;
    [SerializeField] private float gameOverPulseAmount = 0.1f;

    private Vector3 originalGameOverScale;

    void Start()
    {
        SetupButtons();
        SetupText();
        UpdateScore();
    }

    void SetupButtons()
    {
        // Setup restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
            StyleButton(restartButton);
        }
        else
        {
            Debug.LogWarning("Restart button reference missing!");
        }

        // Setup main menu button
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            StyleButton(mainMenuButton);
        }
        else
        {
            Debug.LogWarning("Main menu button reference missing!");
        }
    }

    void StyleButton(Button button)
    {
        // Get or create ColorBlock
        ColorBlock colors = button.colors;
        colors.normalColor = normalButtonColor * buttonGlowIntensity;
        colors.highlightedColor = hoverButtonColor * buttonGlowIntensity;
        colors.pressedColor = pressedButtonColor * buttonGlowIntensity;
        colors.selectedColor = normalButtonColor * buttonGlowIntensity;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        // Style button text (if it has a child Text component)
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.color = Color.white;
            buttonText.fontSize = buttonTextSize;
            buttonText.fontStyle = FontStyles.Bold;
        }
        else
        {
            // Fallback to regular Text component
            Text regularText = button.GetComponentInChildren<Text>();
            if (regularText != null)
            {
                regularText.color = Color.white;
                regularText.fontSize = (int)buttonTextSize;
                regularText.fontStyle = FontStyle.Bold;
            }
        }
    }

    void SetupText()
    {
        // Setup Game Over title
        if (gameOverText != null)
        {
            gameOverText.text = "GAME OVER";
            gameOverText.fontSize = gameOverFontSize;
            gameOverText.fontStyle = FontStyles.Bold;
            gameOverText.color = gameOverTextColor * gameOverGlowIntensity;
            gameOverText.alignment = TextAlignmentOptions.Center;

            // Store original scale for pulse animation
            originalGameOverScale = gameOverText.transform.localScale;
        }
        else
        {
            Debug.LogWarning("Game Over Text reference is missing!");
        }

        // Setup score text styling
        if (scoreText != null)
        {
            scoreText.fontSize = scoreFontSize;
            scoreText.fontStyle = FontStyles.Bold;
            scoreText.color = scoreTextColor;
            scoreText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void UpdateScore()
    {
        if (scoreText != null)
        {
            // You can pass score data between scenes using PlayerPrefs
            int finalScore = PlayerPrefs.GetInt("FinalScore", 0);
            scoreText.text = $"Final Score: {finalScore}";
        }
    }

    void Update()
    {
        // Animate game over text pulse
        if (enableGameOverPulse && gameOverText != null)
        {
            float pulse = Mathf.Sin(Time.time * gameOverPulseSpeed) * gameOverPulseAmount;
            gameOverText.transform.localScale = originalGameOverScale * (1f + pulse);
        }
    }

    public void RestartGame()
    {
        // Load the main gameplay scene
        SceneManager.LoadScene(gameplayScene);
    }

    public void ReturnToMainMenu()
    {
        // Load the main menu scene
        SceneManager.LoadScene(mainMenuScene);
    }
}