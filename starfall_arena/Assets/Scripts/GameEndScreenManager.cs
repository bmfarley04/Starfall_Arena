using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Manages the game end screen, displaying victory/defeat stats and ship model with warp-in animation
/// </summary>
public class GameEndScreenManager : MonoBehaviour
{
    [System.Serializable]
    public struct ReturnHoldUIReferences
    {
        [Tooltip("Player 1 return button radial fill image")]
        public Image player1ReturnFill;

        [Tooltip("Player 2 return button radial fill image")]
        public Image player2ReturnFill;
    }

    [System.Serializable]
    public struct HoldButtonConfig
    {
        [Tooltip("Duration in seconds button must be held to trigger")]
        [Range(0.5f, 3f)]
        public float holdDuration;

        [Tooltip("Sound played when hold completes")]
        public SoundEffect confirmSound;
    }

    [Header("Canvas References")]
    [Tooltip("Player 1 victory screen canvas group")]
    [SerializeField] private CanvasGroup player1Canvas;

    [Tooltip("Player 2 victory screen canvas group")]
    [SerializeField] private CanvasGroup player2Canvas;

    [Header("Player 1 Canvas Text Fields")]
    [Tooltip("Duration text in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_durationText;

    [Tooltip("Final record text (wins-losses) in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_finalRecordText;

    [Tooltip("Damage dealt text in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_damageDealtText;

    [Tooltip("Damage taken text in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_damageTakenText;

    [Tooltip("Accuracy text in Player 1 canvas")]
    [SerializeField] private TextMeshProUGUI p1_accuracyText;

    [Header("Player 2 Canvas Text Fields")]
    [Tooltip("Duration text in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_durationText;

    [Tooltip("Final record text (wins-losses) in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_finalRecordText;

    [Tooltip("Damage dealt text in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_damageDealtText;

    [Tooltip("Damage taken text in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_damageTakenText;

    [Tooltip("Accuracy text in Player 2 canvas")]
    [SerializeField] private TextMeshProUGUI p2_accuracyText;

    [Header("Player 1 Canvas Stat Sections")]
    [SerializeField] private CanvasGroup p1_durationSection;
    [SerializeField] private CanvasGroup p1_recordSection;
    [SerializeField] private CanvasGroup p1_damageDealtSection;
    [SerializeField] private CanvasGroup p1_damageTakenSection;
    [SerializeField] private CanvasGroup p1_accuracySection;

    [Header("Player 2 Canvas Stat Sections")]
    [SerializeField] private CanvasGroup p2_durationSection;
    [SerializeField] private CanvasGroup p2_recordSection;
    [SerializeField] private CanvasGroup p2_damageDealtSection;
    [SerializeField] private CanvasGroup p2_damageTakenSection;
    [SerializeField] private CanvasGroup p2_accuracySection;

    [Header("Ship Model Spawn Points")]
    [Tooltip("Transform where Player 1's ship model will be spawned")]
    [SerializeField] private Transform player1ShipSpawnPoint;

    [Tooltip("Transform where Player 2's ship model will be spawned")]
    [SerializeField] private Transform player2ShipSpawnPoint;

    [Header("Ship Warp Animation Settings")]
    [Tooltip("Position offset from spawn point where ship starts (e.g. -20, -20, 0 for bottom-left)")]
    [SerializeField] private Vector3 warpStartOffset = new Vector3(-20f, -20f, 0f);

    [Tooltip("Duration of the warp-in effect")]
    [SerializeField] private float warpDuration = 0.5f;

    [Tooltip("Delay after stats finish before ship warps in")]
    [SerializeField] private float delayBeforeShipWarp = 0.2f;

    [Tooltip("Maximum Y scale multiplier at start of warp (ship length stretch)")]
    [SerializeField] private float warpMaxStretchY = 5f;

    [Tooltip("Minimum X scale multiplier during warp (ship width compress)")]
    [SerializeField] private float warpMinCompressX = 0.25f;

    [Tooltip("Starting opacity during warp (0-1)")]
    [SerializeField] private float warpStartAlpha = 0.5f;

    [Tooltip("At what point (0-1) the ship snaps to final position")]
    [SerializeField] private float snapThreshold = 0.95f;

    [Header("Canvas Animation Settings")]
    [SerializeField] private float spawnDuration = 0.6f;
    [SerializeField] private float despawnDuration = 0.4f;
    [SerializeField] private float statDelayBetween = 0.15f;
    [SerializeField] private float scaleOvershoot = 1.15f;

    [Header("Return To Title (Hold)")]
    [Tooltip("Return hold button fill references for each player canvas")]
    [SerializeField] private ReturnHoldUIReferences returnHoldUI;

    [Tooltip("Hold button configuration for returning to title")]
    [SerializeField] private HoldButtonConfig holdReturn;

    [Tooltip("Scene name to load when the return button is pressed")]
    [SerializeField] private string titleSceneName = "titleScreenTest";

    [Tooltip("Delay after click sound before loading scene")]
    [SerializeField] private float sceneLoadDelay = 0.15f;

    [Header("Debug Mode")]
    [Tooltip("Enable to test with default parameters (no function call needed)")]
    [SerializeField] private bool debugMode = false;

    [Tooltip("Default ship data to use in debug mode")]
    [SerializeField] private ShipData debugShipData;

    [Tooltip("Which player wins in debug mode (1 or 2)")]
    [SerializeField] private int debugWinningPlayer = 1;

    [Header("Debug Default Stats")]
    [SerializeField] private float debugDuration = 633f; // 10:33
    [SerializeField] private int debugWins = 4;
    [SerializeField] private int debugLosses = 2;
    [SerializeField] private float debugDamageDealt = 19382f;
    [SerializeField] private float debugDamageTaken = 6345f;
    [SerializeField] private float debugAccuracy = 21.5f;

    private CanvasGroup currentActiveCanvas;
    private Coroutine currentAnimation;
    private CanvasGroup[] currentStatSections;
    private GameObject spawnedShipModel;
    private Transform currentShipSpawnPoint;
    private AudioSource _audioSource;
    private float _returnHoldTime = 0f;
    private bool _canReturnToTitle = false;
    private bool _isLoadingTitle = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

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

        ResetReturnFillUI();
    }

    private void Start()
    {
        // Auto-run debug mode if enabled
        if (debugMode)
        {
            ShowGameEndScreen(
                debugWinningPlayer,
                debugShipData,
                debugDuration,
                debugWins,
                debugLosses,
                debugDamageDealt,
                debugDamageTaken,
                debugAccuracy
            );
        }
    }

    private void Update()
    {
        HandleReturnHoldButton();
    }

    /// <summary>
    /// Displays the game end screen with the provided stats and winning ship
    /// </summary>
    /// <param name="winningPlayer">1 or 2</param>
    /// <param name="shipData">Ship data containing the model prefab to display</param>
    /// <param name="gameDuration">Total game duration in seconds</param>
    /// <param name="wins">Number of rounds won</param>
    /// <param name="losses">Number of rounds lost</param>
    /// <param name="damageDealt">Total damage dealt by winner</param>
    /// <param name="damageTaken">Total damage taken by winner</param>
    /// <param name="accuracy">Accuracy percentage (0-100)</param>
    public void ShowGameEndScreen(
        int winningPlayer,
        ShipData shipData,
        float gameDuration,
        int wins,
        int losses,
        float damageDealt,
        float damageTaken,
        float accuracy)
    {
        // Stop any existing animation
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        // Clean up any existing ship model
        if (spawnedShipModel != null)
        {
            Destroy(spawnedShipModel);
        }

        // Determine which canvas to show and which stat sections to animate
        if (winningPlayer == 1)
        {
            currentActiveCanvas = player1Canvas;
            currentShipSpawnPoint = player1ShipSpawnPoint;
            currentStatSections = new CanvasGroup[]
            {
                p1_durationSection,
                p1_recordSection,
                p1_damageDealtSection,
                p1_damageTakenSection,
                p1_accuracySection
            };
        }
        else
        {
            currentActiveCanvas = player2Canvas;
            currentShipSpawnPoint = player2ShipSpawnPoint;
            currentStatSections = new CanvasGroup[]
            {
                p2_durationSection,
                p2_recordSection,
                p2_damageDealtSection,
                p2_damageTakenSection,
                p2_accuracySection
            };
        }

        if (currentActiveCanvas == null)
        {
            Debug.LogError($"Player {winningPlayer} canvas is not assigned!");
            return;
        }

        if (shipData == null || shipData.shipModelPrefab == null)
        {
            Debug.LogError("Ship data or ship model prefab is null!");
            return;
        }

        if (currentShipSpawnPoint == null)
        {
            Debug.LogError($"Player {winningPlayer} ship spawn point is not assigned!");
            return;
        }

        _canReturnToTitle = false;
        _isLoadingTitle = false;
        _returnHoldTime = 0f;
        ResetReturnFillUI();

        // Populate text fields
        PopulateStats(winningPlayer, gameDuration, wins, losses, damageDealt, damageTaken, accuracy);

        // Spawn ship model (but keep it off-screen)
        SpawnShipModel(shipData);

        // Start spawn animation
        currentAnimation = StartCoroutine(SpawnAnimation());
    }

    /// <summary>
    /// Hides the game end screen with animation
    /// </summary>
    public void HideGameEndScreen()
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

    private void PopulateStats(int winningPlayer, float gameDuration, int wins, int losses, float damageDealt, float damageTaken, float accuracy)
    {
        // Format duration as MM:SS
        int minutes = Mathf.FloorToInt(gameDuration / 60f);
        int seconds = Mathf.FloorToInt(gameDuration % 60f);
        string durationStr = $"{minutes}:{seconds:D2}";

        // Format final record as W-L
        string recordStr = $"{wins}-{losses}";

        // Format damage (whole numbers)
        string damageDealtStr = Mathf.RoundToInt(damageDealt).ToString();
        string damageTakenStr = Mathf.RoundToInt(damageTaken).ToString();

        // Format accuracy (1 decimal place)
        string accuracyStr = $"{accuracy:F1}%";

        // Populate the appropriate canvas's text fields
        if (winningPlayer == 1)
        {
            if (p1_durationText != null) p1_durationText.text = durationStr;
            if (p1_finalRecordText != null) p1_finalRecordText.text = recordStr;
            if (p1_damageDealtText != null) p1_damageDealtText.text = damageDealtStr;
            if (p1_damageTakenText != null) p1_damageTakenText.text = damageTakenStr;
            if (p1_accuracyText != null) p1_accuracyText.text = accuracyStr;
        }
        else
        {
            if (p2_durationText != null) p2_durationText.text = durationStr;
            if (p2_finalRecordText != null) p2_finalRecordText.text = recordStr;
            if (p2_damageDealtText != null) p2_damageDealtText.text = damageDealtStr;
            if (p2_damageTakenText != null) p2_damageTakenText.text = damageTakenStr;
            if (p2_accuracyText != null) p2_accuracyText.text = accuracyStr;
        }
    }

    private void SpawnShipModel(ShipData shipData)
    {
        // Instantiate ship model at spawn point with final rotation
        spawnedShipModel = Instantiate(
            shipData.shipModelPrefab,
            currentShipSpawnPoint.position,
            currentShipSpawnPoint.rotation
        );

        // Disable all MonoBehaviours on the ship (visual preview only)
        MonoBehaviour[] components = spawnedShipModel.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            component.enabled = false;
        }

        // Position ship off-screen using fixed offset
        Vector3 warpStartPosition = currentShipSpawnPoint.position + warpStartOffset;
        spawnedShipModel.transform.position = warpStartPosition;

        // Initially hide the ship
        spawnedShipModel.SetActive(false);
    }

    private IEnumerator SpawnAnimation()
    {
        // Activate canvas but keep it invisible
        currentActiveCanvas.gameObject.SetActive(true);
        currentActiveCanvas.alpha = 0f;

        // Reset stat sections to invisible
        foreach (CanvasGroup section in currentStatSections)
        {
            if (section != null) section.alpha = 0f;
        }

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

        foreach (CanvasGroup section in currentStatSections)
        {
            if (section != null)
            {
                yield return StartCoroutine(PunchInSection(section));
                yield return new WaitForSecondsRealtime(statDelayBetween);
            }
        }

        // Phase 4: Wait, then warp in ship
        yield return new WaitForSecondsRealtime(delayBeforeShipWarp);

        if (spawnedShipModel != null)
        {
            yield return StartCoroutine(WarpInShip());
        }

        // Phase 5: Enable hold-to-return input
        _canReturnToTitle = true;

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

    private IEnumerator WarpInShip()
    {
        // Activate ship
        spawnedShipModel.SetActive(true);

        Transform shipTransform = spawnedShipModel.transform;
        Vector3 startPosition = shipTransform.position;
        Vector3 targetPosition = currentShipSpawnPoint.position;
        Vector3 originalScale = shipTransform.localScale;
        Vector3 stretchedScale = originalScale;
        stretchedScale.y *= warpMaxStretchY;
        stretchedScale.x *= warpMinCompressX;

        // Start at full warp stretch and keep that scale during travel
        shipTransform.localScale = stretchedScale;

        // Get all renderers for alpha control
        Renderer[] renderers = spawnedShipModel.GetComponentsInChildren<Renderer>();

        // Store original colors and set to transparent rendering mode
        Color[][] originalColors = new Color[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].materials != null)
            {
                originalColors[i] = new Color[renderers[i].materials.Length];
                for (int j = 0; j < renderers[i].materials.Length; j++)
                {
                    Material mat = renderers[i].materials[j];
                    originalColors[i][j] = mat.color;
                }
            }
        }

        float elapsed = 0f;
        bool hasSnapped = false;

        while (elapsed < warpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / warpDuration;

            // CRISP SNAP at threshold
            if (t >= snapThreshold && !hasSnapped)
            {
                shipTransform.position = targetPosition;
                shipTransform.localScale = originalScale;

                // Restore full opacity
                SetShipAlpha(renderers, originalColors, 1f);

                hasSnapped = true;
                yield return null;
                continue;
            }

            if (!hasSnapped)
            {
                // Fast acceleration toward target
                float posT = EaseInCubic(t);
                shipTransform.position = Vector3.Lerp(startPosition, targetPosition, posT);

                // Keep full warp stretch until snap
                shipTransform.localScale = stretchedScale;

                // Fade in from start alpha to full opacity
                float currentAlpha = Mathf.Lerp(warpStartAlpha, 1f, t);
                SetShipAlpha(renderers, originalColors, currentAlpha);
            }

            yield return null;
        }

        // Ensure final position/scale/alpha is exact
        shipTransform.position = targetPosition;
        shipTransform.localScale = originalScale;
        SetShipAlpha(renderers, originalColors, 1f);
    }

    private void SetShipAlpha(Renderer[] renderers, Color[][] originalColors, float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].materials != null && originalColors[i] != null)
            {
                for (int j = 0; j < renderers[i].materials.Length; j++)
                {
                    if (renderers[i].materials[j] != null && j < originalColors[i].Length)
                    {
                        Color newColor = originalColors[i][j];
                        newColor.a = alpha;
                        renderers[i].materials[j].color = newColor;
                    }
                }
            }
        }
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

        // Clean up ship model
        if (spawnedShipModel != null)
        {
            Destroy(spawnedShipModel);
            spawnedShipModel = null;
        }

        currentActiveCanvas = null;
        currentAnimation = null;
        _canReturnToTitle = false;
        _returnHoldTime = 0f;
        ResetReturnFillUI();
    }

    private void HandleReturnHoldButton()
    {
        if (!_canReturnToTitle || _isLoadingTitle || currentActiveCanvas == null || !currentActiveCanvas.gameObject.activeInHierarchy)
        {
            return;
        }

        bool returnPressed = false;

        foreach (var pad in Gamepad.all)
        {
            if (pad != null && pad.added && pad.aButton.isPressed)
            {
                returnPressed = true;
                break;
            }
        }

        if (Keyboard.current != null)
        {
            returnPressed = returnPressed || Keyboard.current.enterKey.isPressed;
        }

        if (returnPressed)
        {
            _returnHoldTime += Time.unscaledDeltaTime;
            float fillRatio = 1f - Mathf.Clamp01(_returnHoldTime / holdReturn.holdDuration);
            SetActiveReturnFill(fillRatio);

            if (_returnHoldTime >= holdReturn.holdDuration)
            {
                ConfirmReturnToTitle();
            }
        }
        else
        {
            _returnHoldTime = 0f;
            SetActiveReturnFill(1f);
        }
    }

    private void ConfirmReturnToTitle()
    {
        if (_isLoadingTitle) return;

        _isLoadingTitle = true;
        _canReturnToTitle = false;
        _returnHoldTime = 0f;
        SetActiveReturnFill(1f);

        if (holdReturn.confirmSound != null)
        {
            holdReturn.confirmSound.Play(_audioSource);
        }

        StartCoroutine(LoadSceneDelayed(titleSceneName, sceneLoadDelay));
    }

    private void ResetReturnFillUI()
    {
        if (returnHoldUI.player1ReturnFill != null)
            returnHoldUI.player1ReturnFill.fillAmount = 1f;

        if (returnHoldUI.player2ReturnFill != null)
            returnHoldUI.player2ReturnFill.fillAmount = 1f;
    }

    private void SetActiveReturnFill(float fillAmount)
    {
        if (currentActiveCanvas == player1Canvas)
        {
            if (returnHoldUI.player1ReturnFill != null)
                returnHoldUI.player1ReturnFill.fillAmount = fillAmount;
        }
        else if (currentActiveCanvas == player2Canvas)
        {
            if (returnHoldUI.player2ReturnFill != null)
                returnHoldUI.player2ReturnFill.fillAmount = fillAmount;
        }
    }

    private IEnumerator LoadSceneDelayed(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
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

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private void Reset()
    {
        holdReturn.holdDuration = 1.5f;
    }
}
