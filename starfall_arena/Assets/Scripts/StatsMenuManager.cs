using UnityEngine;

public class StatsMenuManager : MonoBehaviour
{
    [Header("GUI Settings")]
    public GUISkin guiSkin;

    [Header("Animation Settings")]
    [Tooltip("How long the pop-in animation takes")]
    [SerializeField] public float popInDuration = 0.4f;
    [Tooltip("The starting scale (0.0 to 1.0)")]
    [SerializeField] private float startScale = 0.7f;

    [Header("Stats Box Settings")]
    [Tooltip("Width of the stats container box (base for 1080p)")]
    [SerializeField] private float statsBoxWidth = 800f;

    [Tooltip("Height of the stats container box (base for 1080p)")]
    [SerializeField] private float statsBoxHeight = 400f;

    [Tooltip("Horizontal offset for the stats table. Positive moves right, Negative moves left.")]
    [SerializeField] private float statsBoxXOffset = 0f;

    [Tooltip("Vertical offset for the stats table. Positive moves down, Negative moves up.")]
    [SerializeField] private float statsBoxYOffset = 0f;

    [Header("Text Settings")]
    [Tooltip("Font size for stat labels (base for 1080p)")]
    [SerializeField] private int statLabelFontSize = 40;

    [Tooltip("Font size for stat values (base for 1080p)")]
    [SerializeField] private int statValueFontSize = 40;

    [Tooltip("Color for the stat labels")]
    [SerializeField] private Color statLabelColor = Color.white;

    [Tooltip("Color for the stat values")]
    [SerializeField] private Color statValueColor = Color.yellow;

    [Header("Layout Settings")]
    [Tooltip("Height of each row (base for 1080p)")]
    [SerializeField] private float rowHeight = 60f;

    [Tooltip("Padding inside the box (base for 1080p)")]
    [SerializeField] private float boxPadding = 60f;

    [Tooltip("Extra spacing between rows")]
    [SerializeField] private float rowSpacing = 0f;

    // Stats data
    private int missionsCompleted;
    private int highestWave;
    private int totalEnemiesDefeated;

    // Animation State
    private float openTime;
    private float closeTime = -1f;

    void OnEnable()
    {
        openTime = Time.time;
        closeTime = -1f;
        RefreshStats();
    }

    public void StartClosing()
    {
        closeTime = Time.time;
    }

    private void RefreshStats()
    {
        missionsCompleted = PlayerPrefs.GetInt("MissionsCompleted", 0);
        highestWave = PlayerPrefs.GetInt("HighestWaveReached", 0);
        totalEnemiesDefeated = PlayerPrefs.GetInt("TotalEnemiesDefeated", 0);
    }

    void OnGUI()
    {
        if (guiSkin != null) GUI.skin = guiSkin;
        float scaleFactor = Screen.height / 1080f;

        DrawStatsMenu(scaleFactor);
    }

    private void DrawStatsMenu(float scaleFactor)
    {
        float screenW = Screen.width;
        float screenH = Screen.height;
        float centerX = screenW / 2f;
        float centerY = screenH / 2f;

        // --- ANIMATION CALCULATION ---
        float animProgress;

        if (closeTime >= 0f)
        {
            // Closing animation - reverse
            float timeClosing = Time.time - closeTime;
            animProgress = 1f - Mathf.Clamp01(timeClosing / popInDuration);
        }
        else
        {
            // Opening animation
            float timeOpen = Time.time - openTime;
            animProgress = Mathf.Clamp01(timeOpen / popInDuration);
        }

        float currentAnimScale = Mathf.Lerp(startScale, 1.0f, animProgress);
        float alpha = animProgress;

        // Combine resolution scaling with animation scaling
        float combinedScale = scaleFactor * currentAnimScale;

        // Apply Alpha
        Color originalColor = GUI.color;
        GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

        // 1. Stats Table (Box)
        float boxW = statsBoxWidth * combinedScale;
        float boxH = statsBoxHeight * combinedScale;

        float boxX = centerX - (boxW / 2f) + (statsBoxXOffset * combinedScale);
        float boxY = centerY - (boxH / 2f) + (statsBoxYOffset * combinedScale);

        // Background for the stats
        GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

        // 2. Stats Inside Box
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.fontSize = Mathf.RoundToInt(statLabelFontSize * combinedScale);
        labelStyle.normal.textColor = statLabelColor;
        labelStyle.hover.textColor = statLabelColor;

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.alignment = TextAnchor.MiddleRight;
        valueStyle.fontSize = Mathf.RoundToInt(statValueFontSize * combinedScale);
        valueStyle.normal.textColor = statValueColor;
        valueStyle.hover.textColor = statValueColor;

        // Define row height and padding
        float scaledRowHeight = rowHeight * combinedScale;
        float scaledRowSpacing = rowSpacing * combinedScale;
        float padding = boxPadding * combinedScale;
        float currentY = boxY + padding;
        float contentWidth = boxW - (padding * 2);
        float labelX = boxX + padding;

        // -- Row 1: Missions Completed --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Missions Completed:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), missionsCompleted.ToString(), valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 2: Highest Wave Reached --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Highest Wave Reached:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), highestWave.ToString(), valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 3: Enemies Defeated --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Enemies Defeated:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), totalEnemiesDefeated.ToString(), valueStyle);

        // Restore Color
        GUI.color = originalColor;
    }

    public bool IsAnimationComplete()
    {
        if (closeTime >= 0f)
        {
            float timeClosing = Time.time - closeTime;
            return timeClosing >= popInDuration;
        }
        return true;
    }
}
