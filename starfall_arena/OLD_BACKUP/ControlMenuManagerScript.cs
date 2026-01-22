using UnityEngine;
using UnityEngine.SceneManagement;

public class ControlMenuManager : MonoBehaviour
{
    [Header("GUI Settings")]
    public GUISkin guiSkin;

    [Header("Scene Configuration")]
    public string mainMenuScene = "MainMenu";

    [Header("Animation Settings")] // <--- NEW ANIMATION SETTINGS
    [Tooltip("How long the pop-in animation takes")]
    [SerializeField] public float popInDuration = 0.4f;
    [Tooltip("The starting scale (0.0 to 1.0)")]
    [SerializeField] private float startScale = 0.7f;

    [Header("Controls Box Settings")]
    [Tooltip("Width of the controls container box (base for 1080p)")]
    [SerializeField] private float controlsBoxWidth = 800f;

    [Tooltip("Height of the controls container box (base for 1080p)")]
    [SerializeField] private float controlsBoxHeight = 500f;

    [Tooltip("Horizontal offset for the controls table. Positive moves right, Negative moves left.")]
    [SerializeField] private float controlsBoxXOffset = 0f;

    [Tooltip("Vertical offset for the controls table. Positive moves down, Negative moves up.")]
    [SerializeField] private float controlsBoxYOffset = 0f;

    [Header("Text Settings")]
    [Tooltip("Font size for control labels (base for 1080p)")]
    [SerializeField] private int controlLabelFontSize = 40;

    [Tooltip("Font size for control values (base for 1080p)")]
    [SerializeField] private int controlValueFontSize = 40;

    [Tooltip("Color for the control labels")]
    [SerializeField] private Color controlLabelColor = Color.white;

    [Tooltip("Color for the control values")]
    [SerializeField] private Color controlValueColor = Color.yellow;

    [Header("Layout Settings")]
    [Tooltip("Height of each row (base for 1080p)")]
    [SerializeField] private float rowHeight = 60f;

    [Tooltip("Padding inside the box (base for 1080p)")]
    [SerializeField] private float boxPadding = 60f;

    [Tooltip("Extra spacing between rows")]
    [SerializeField] private float rowSpacing = 0f;

    // [Header("Button Settings")]
    // [Tooltip("Vertical offset for the Back Button. Positive moves down, Negative moves up.")]
    // [SerializeField] private float backButtonYOffset = 300f;
    
    // [Tooltip("Width of the back button")]
    // [SerializeField] private float backButtonWidth = 350f;
    
    // [Tooltip("Height of the back button")]
    // [SerializeField] private float backButtonHeight = 80f;

    // [Tooltip("Scale multiplier for the back button size")]
    // [SerializeField] private float backButtonScale = 1.0f;

    // Animation State
    private float openTime;
    private float closeTime = -1f;

    // This runs every time GameObject.SetActive(true) is called
    void OnEnable()
    {
        openTime = Time.time;
        closeTime = -1f;
    }

    public void StartClosing()
    {
        closeTime = Time.time;
    }

    void OnGUI()
    {
        if (guiSkin != null) GUI.skin = guiSkin;
        float scaleFactor = Screen.height / 1080f;

        DrawControlsMenu(scaleFactor);
    }

    private void DrawControlsMenu(float scaleFactor)
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
        float alpha = animProgress; // Fade in/out opacity

        // Combine resolution scaling with animation scaling
        float combinedScale = scaleFactor * currentAnimScale;

        // Apply Alpha
        Color originalColor = GUI.color;
        GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);

        // -----------------------------

        // 1. Controls Table (Box) - use combined scale for dimensions
        float boxW = controlsBoxWidth * combinedScale;
        float boxH = controlsBoxHeight * combinedScale;

        float boxX = centerX - (boxW / 2f) + (controlsBoxXOffset * combinedScale);
        float boxY = centerY - (boxH / 2f) + (controlsBoxYOffset * combinedScale);

        // Background for the controls
        GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

        // 2. Controls Inside Box
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleLeft;
        labelStyle.fontSize = Mathf.RoundToInt(controlLabelFontSize * combinedScale);
        labelStyle.normal.textColor = controlLabelColor;
        labelStyle.hover.textColor = controlLabelColor;

        GUIStyle valueStyle = new GUIStyle(GUI.skin.label);
        valueStyle.alignment = TextAnchor.MiddleRight;
        valueStyle.fontSize = Mathf.RoundToInt(controlValueFontSize * combinedScale);
        valueStyle.normal.textColor = controlValueColor;
        valueStyle.hover.textColor = controlValueColor;

        // Define row height and padding - use combined scale
        float scaledRowHeight = rowHeight * combinedScale;
        float scaledRowSpacing = rowSpacing * combinedScale;
        float padding = boxPadding * combinedScale;
        float currentY = boxY + padding;
        float contentWidth = boxW - (padding * 2);
        float labelX = boxX + padding;

        // -- Row 1: Steer --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Steer:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Mouse Move", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 2: Shoot --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Shoot:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Left Click", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 3: Thrust --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Thrust:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Spacebar", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 4: Toggle Friction --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Toggle Friction:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Tab", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 5: Energy Beam --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Energy Beam:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Q", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 6: Reflect --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Reflect:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "W", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 7: Teleport --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Teleport:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "E", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 8: Charge Shot --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Charge Shot:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "R", valueStyle);
        currentY += scaledRowHeight + scaledRowSpacing;

        // -- Row 9: Pause --
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Pause:", labelStyle);
        GUI.Label(new Rect(labelX, currentY, contentWidth, scaledRowHeight), "Escape", valueStyle);

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