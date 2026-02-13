using System.Collections;
using UnityEngine;
using TMPro;

public class RoundEndScreenManager : MonoBehaviour
{
    [Header("Canvas References")]
    [Tooltip("Player 1 win screen canvas group (blue)")]
    [SerializeField] private CanvasGroup player1Canvas;

    [Tooltip("Player 2 win screen canvas group (orange)")]
    [SerializeField] private CanvasGroup player2Canvas;

    [Header("Player 1 Canvas Text Fields")]
    [Tooltip("Duration text in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_durationText;

    [Tooltip("Player 1 damage dealt text (left column) in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_player1DamageText;

    [Tooltip("Player 2 damage dealt text (right column) in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_player2DamageText;

    [Tooltip("Player 1 accuracy text (left column) in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_player1AccuracyText;

    [Tooltip("Player 2 accuracy text (right column) in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_player2AccuracyText;

    [Header("Player 2 Canvas Text Fields")]
    [Tooltip("Duration text in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_durationText;

    [Tooltip("Player 1 damage dealt text (left column) in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_player1DamageText;

    [Tooltip("Player 2 damage dealt text (right column) in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_player2DamageText;

    [Tooltip("Player 1 accuracy text (left column) in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_player1AccuracyText;

    [Tooltip("Player 2 accuracy text (right column) in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_player2AccuracyText;

    [Header("Animation Settings")]
    [SerializeField] private float spawnDuration = 0.6f;
    [SerializeField] private float despawnDuration = 0.4f;
    [SerializeField] private float statDelayBetween = 0.15f;
    [SerializeField] private float scaleOvershoot = 1.15f;

    [Header("Player 1 Canvas Stat Sections")]
    [SerializeField] private CanvasGroup p1_durationSection;
    [SerializeField] private CanvasGroup p1_damageSection;
    [SerializeField] private CanvasGroup p1_accuracySection;

    [Header("Player 2 Canvas Stat Sections")]
    [SerializeField] private CanvasGroup p2_durationSection;
    [SerializeField] private CanvasGroup p2_damageSection;
    [SerializeField] private CanvasGroup p2_accuracySection;

    private CanvasGroup currentActiveCanvas;
    private Coroutine currentAnimation;
    private CanvasGroup currentDurationSection;
    private CanvasGroup currentDamageSection;
    private CanvasGroup currentAccuracySection;

    private void Awake()
    {
        // Ensure both canvases start disabled
        if (player1Canvas != null)
        {
            player1Canvas.alpha = 0f;
            player1Canvas.gameObject.SetActive(false);
        }

        if (player2Canvas != null)
        {
            player2Canvas.alpha = 0f;
            player2Canvas.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Displays the round end screen with the provided stats
    /// </summary>
    /// <param name="winningPlayer">1 or 2</param>
    /// <param name="roundDuration">Duration in seconds</param>
    /// <param name="player1Damage">Total damage dealt by player 1</param>
    /// <param name="player2Damage">Total damage dealt by player 2</param>
    /// <param name="player1Accuracy">Accuracy percentage (0-100)</param>
    /// <param name="player2Accuracy">Accuracy percentage (0-100)</param>
    public void ShowRoundEndScreen(int winningPlayer, float roundDuration, float player1Damage, float player2Damage, float player1Accuracy, float player2Accuracy)
    {
        // Stop any existing animation
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        // Determine which canvas to show and which stat sections to animate
        if (winningPlayer == 1)
        {
            currentActiveCanvas = player1Canvas;
            currentDurationSection = p1_durationSection;
            currentDamageSection = p1_damageSection;
            currentAccuracySection = p1_accuracySection;
        }
        else
        {
            currentActiveCanvas = player2Canvas;
            currentDurationSection = p2_durationSection;
            currentDamageSection = p2_damageSection;
            currentAccuracySection = p2_accuracySection;
        }

        if (currentActiveCanvas == null)
        {
            Debug.LogError($"Player {winningPlayer} canvas is not assigned!");
            return;
        }

        // Populate text fields
        PopulateStats(winningPlayer, roundDuration, player1Damage, player2Damage, player1Accuracy, player2Accuracy);

        // Start spawn animation
        currentAnimation = StartCoroutine(SpawnAnimation());
    }

    /// <summary>
    /// Hides the round end screen with animation
    /// </summary>
    public void HideRoundEndScreen()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        if (currentActiveCanvas != null)
        {
            currentAnimation = StartCoroutine(DespawnAnimation());
        }
    }

    private void PopulateStats(int winningPlayer, float roundDuration, float player1Damage, float player2Damage, float player1Accuracy, float player2Accuracy)
    {
        // Format duration as MM:SS
        int minutes = Mathf.FloorToInt(roundDuration / 60f);
        int seconds = Mathf.FloorToInt(roundDuration % 60f);
        string durationStr = $"{minutes}:{seconds:D2}";

        // Format damage (whole numbers)
        string p1DamageStr = Mathf.RoundToInt(player1Damage).ToString();
        string p2DamageStr = Mathf.RoundToInt(player2Damage).ToString();

        // Format accuracy (1 decimal place)
        string p1AccuracyStr = $"{player1Accuracy:F1}%";
        string p2AccuracyStr = $"{player2Accuracy:F1}%";

        // Populate the appropriate canvas's text fields
        if (winningPlayer == 1)
        {
            if (p1_durationText != null) p1_durationText.text = durationStr;
            if (p1_player1DamageText != null) p1_player1DamageText.text = p1DamageStr;
            if (p1_player2DamageText != null) p1_player2DamageText.text = p2DamageStr;
            if (p1_player1AccuracyText != null) p1_player1AccuracyText.text = p1AccuracyStr;
            if (p1_player2AccuracyText != null) p1_player2AccuracyText.text = p2AccuracyStr;
        }
        else
        {
            if (p2_durationText != null) p2_durationText.text = durationStr;
            if (p2_player1DamageText != null) p2_player1DamageText.text = p1DamageStr;
            if (p2_player2DamageText != null) p2_player2DamageText.text = p2DamageStr;
            if (p2_player1AccuracyText != null) p2_player1AccuracyText.text = p1AccuracyStr;
            if (p2_player2AccuracyText != null) p2_player2AccuracyText.text = p2AccuracyStr;
        }
    }

    private IEnumerator SpawnAnimation()
    {
        // Activate canvas but keep it invisible
        currentActiveCanvas.gameObject.SetActive(true);
        currentActiveCanvas.alpha = 0f;

        // Reset stat sections to invisible
        if (currentDurationSection != null) currentDurationSection.alpha = 0f;
        if (currentDamageSection != null) currentDamageSection.alpha = 0f;
        if (currentAccuracySection != null) currentAccuracySection.alpha = 0f;

        // Get the root transform for scale animation
        Transform canvasTransform = currentActiveCanvas.transform;
        Vector3 originalScale = canvasTransform.localScale;

        // Phase 1: Explosive entrance - scale up from small with overshoot + fade in
        float elapsed = 0f;
        float halfDuration = spawnDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;

            // Ease out elastic for overshoot effect
            float scaleT = EaseOutBack(t);
            canvasTransform.localScale = originalScale * Mathf.Lerp(0.3f, scaleOvershoot, scaleT);

            // Fade in
            currentActiveCanvas.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // Phase 2: Settle to normal scale
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / halfDuration;

            canvasTransform.localScale = originalScale * Mathf.Lerp(scaleOvershoot, 1f, EaseOutQuad(t));

            yield return null;
        }

        canvasTransform.localScale = originalScale;

        // Phase 3: Sequential stat appearance with punch effect
        yield return new WaitForSecondsRealtime(0.1f);

        // Duration section
        if (currentDurationSection != null)
        {
            yield return StartCoroutine(PunchInSection(currentDurationSection));
            yield return new WaitForSecondsRealtime(statDelayBetween);
        }

        // Damage section
        if (currentDamageSection != null)
        {
            yield return StartCoroutine(PunchInSection(currentDamageSection));
            yield return new WaitForSecondsRealtime(statDelayBetween);
        }

        // Accuracy section
        if (currentAccuracySection != null)
        {
            yield return StartCoroutine(PunchInSection(currentAccuracySection));
        }

        currentAnimation = null;
    }

    private IEnumerator PunchInSection(CanvasGroup section)
    {
        Transform sectionTransform = section.transform;
        Vector3 originalScale = sectionTransform.localScale;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // Scale punch with overshoot
            float scaleT = EaseOutBack(t);
            sectionTransform.localScale = originalScale * Mathf.Lerp(0.5f, 1f, scaleT);

            // Fade in
            section.alpha = Mathf.Lerp(0f, 1f, EaseOutQuad(t));

            yield return null;
        }

        sectionTransform.localScale = originalScale;
        section.alpha = 1f;
    }

    private IEnumerator DespawnAnimation()
    {
        Transform canvasTransform = currentActiveCanvas.transform;
        Vector3 originalScale = canvasTransform.localScale;
        float startAlpha = currentActiveCanvas.alpha;

        // Quick scale down + fade out + slight rotation
        float elapsed = 0f;
        while (elapsed < despawnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / despawnDuration;

            // Scale down with ease in
            canvasTransform.localScale = originalScale * Mathf.Lerp(1f, 0.7f, EaseInQuad(t));

            // Fade out
            currentActiveCanvas.alpha = Mathf.Lerp(startAlpha, 0f, EaseInQuad(t));

            // Subtle rotation for dynamic feel
            canvasTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 5f, EaseInQuad(t)));

            yield return null;
        }

        // Reset and deactivate
        canvasTransform.localScale = originalScale;
        canvasTransform.rotation = Quaternion.identity;
        currentActiveCanvas.alpha = 0f;
        currentActiveCanvas.gameObject.SetActive(false);

        currentActiveCanvas = null;
        currentAnimation = null;
    }

    // Easing functions
    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private float EaseInQuad(float t)
    {
        return t * t;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
