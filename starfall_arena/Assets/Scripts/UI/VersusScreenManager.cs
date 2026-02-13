using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Manages the VS screen shown at the start of a duel.
/// Retrieves ship selections from GameDataManager, spawns 3D ship previews,
/// and plays a slam-in/slide-out animation sequence.
/// </summary>
public class VersusScreenManager : MonoBehaviour
{
    [System.Serializable]
    public struct DefaultShipsConfig
    {
        [Tooltip("Default ship for Player 1 when no selection exists")]
        public ShipData defaultPlayer1Ship;

        [Tooltip("Default ship for Player 2 when no selection exists")]
        public ShipData defaultPlayer2Ship;
    }

    [System.Serializable]
    public struct ShipDisplayConfig
    {
        [Tooltip("World position to spawn the ship model")]
        public Vector3 position;

        [Tooltip("Euler rotation for the ship model")]
        public Vector3 rotation;

        [Tooltip("Scale multiplier for the ship model")]
        public float scale;
    }

    [System.Serializable]
    public struct AnimationConfig
    {
        [Header("Entrance")]
        [Tooltip("Pause before cards begin entering")]
        public float initialDelay;

        [Tooltip("Duration for cards to slide in from off-screen")]
        public float cardSlideDuration;

        [Tooltip("How far off-screen cards start (in canvas units)")]
        public float cardSlideOffset;

        [Tooltip("Overshoot amount for the bounce effect (0 = no overshoot)")]
        [Range(0f, 1f)]
        public float cardSlideOvershoot;

        [Tooltip("Delay before Player 2 card starts sliding (stagger)")]
        public float cardStaggerDelay;

        [Header("VS Stamp")]
        [Tooltip("Delay after cards land before VS text punches in")]
        public float vsStampDelay;

        [Tooltip("Duration of VS text scale punch")]
        public float vsStampDuration;

        [Tooltip("Overshoot scale for VS punch (e.g. 1.3 = 130% before settling to 100%)")]
        public float vsStampOvershoot;

        [Header("Ship Models")]
        [Tooltip("Delay after VS stamp before ships appear")]
        public float shipAppearDelay;

        [Tooltip("Duration for ships to scale in")]
        public float shipAppearDuration;

        [Header("Hold & Exit")]
        [Tooltip("How long to hold the full VS screen before exiting")]
        public float holdDuration;

        [Tooltip("Duration for cards to slide out")]
        public float exitDuration;

        [Tooltip("Delay after exit before firing the complete event")]
        public float postExitDelay;
    }

    [System.Serializable]
    public struct SoundEffectsConfig
    {
        [Tooltip("Sound when a card slams into position")]
        public SoundEffect cardSlam;

        [Tooltip("Sound when VS text stamps in")]
        public SoundEffect vsStamp;

        [Tooltip("Sound when cards slide out")]
        public SoundEffect exitWhoosh;
    }

    [Header("Default Ship Selections")]
    [SerializeField] private DefaultShipsConfig defaultShips;

    [Header("Ship Display")]
    [Tooltip("Position/rotation/scale for Player 1's ship model")]
    [SerializeField] private ShipDisplayConfig player1ShipDisplay;

    [Tooltip("Position/rotation/scale for Player 2's ship model")]
    [SerializeField] private ShipDisplayConfig player2ShipDisplay;

    [Header("Canvas References")]
    [Tooltip("The root CanvasGroup for the entire VS screen")]
    [SerializeField] private CanvasGroup vsScreenCanvas;

    [Tooltip("Player 1 card panel (slides from left)")]
    [SerializeField] private RectTransform player1Card;

    [Tooltip("Player 2 card panel (slides from right)")]
    [SerializeField] private RectTransform player2Card;

    [Tooltip("Center 'V' text element")]
    [SerializeField] private RectTransform vText;

    [Tooltip("Center 'S' text element")]
    [SerializeField] private RectTransform sText;

    [Header("Ship Name Text")]
    [Tooltip("TextMeshPro field for Player 1's ship name")]
    [SerializeField] private TextMeshProUGUI player1ShipNameText;

    [Tooltip("TextMeshPro field for Player 2's ship name")]
    [SerializeField] private TextMeshProUGUI player2ShipNameText;

    [Header("Animation")]
    [SerializeField] private AnimationConfig animation;

    [Header("Sound Effects")]
    [SerializeField] private SoundEffectsConfig sounds;

    [Tooltip("AudioSource for playing VS screen sounds")]
    [SerializeField] private AudioSource audioSource;

    [Header("Events")]
    [Tooltip("Fired when the VS screen sequence is fully complete")]
    public UnityEvent onVersusScreenComplete;

    private GameObject _player1ShipInstance;
    private GameObject _player2ShipInstance;
    private Vector3 _player1TargetScale;
    private Vector3 _player2TargetScale;
    private Vector2 _player1CardRestPos;
    private Vector2 _player2CardRestPos;

    private IEnumerator Start()
    {
        // Resolve ship data from GameDataManager or fall back to defaults
        ShipData player1Data = null;
        ShipData player2Data = null;

        if (GameDataManager.Instance != null &&
            GameDataManager.Instance.selectedShipClasses != null &&
            GameDataManager.Instance.selectedShipClasses.Count >= 2)
        {
            player1Data = GameDataManager.Instance.selectedShipClasses[0];
            player2Data = GameDataManager.Instance.selectedShipClasses[1];
        }

        if (player1Data == null) player1Data = defaultShips.defaultPlayer1Ship;
        if (player2Data == null) player2Data = defaultShips.defaultPlayer2Ship;

        // Update ship name text
        if (player1ShipNameText != null && player1Data != null)
            player1ShipNameText.text = player1Data.shipName;
        if (player2ShipNameText != null && player2Data != null)
            player2ShipNameText.text = player2Data.shipName;

        // Spawn ship models (inactive until reveal phase)
        _player1ShipInstance = SpawnShipModel(player1Data, player1ShipDisplay, true);
        _player2ShipInstance = SpawnShipModel(player2Data, player2ShipDisplay, false);

        // Store rest positions and set initial state
        _player1CardRestPos = player1Card.anchoredPosition;
        _player2CardRestPos = player2Card.anchoredPosition;

        // Hide everything initially
        player1Card.anchoredPosition = new Vector2(
            _player1CardRestPos.x - animation.cardSlideOffset, _player1CardRestPos.y);
        player2Card.anchoredPosition = new Vector2(
            _player2CardRestPos.x + animation.cardSlideOffset, _player2CardRestPos.y);
        vText.localScale = Vector3.zero;
        sText.localScale = Vector3.zero;
        vsScreenCanvas.alpha = 1f;

        // Run the full animation sequence
        yield return RunVersusSequence();
    }

    private GameObject SpawnShipModel(ShipData data, ShipDisplayConfig config, bool isPlayer1)
    {
        GameObject prefab = isPlayer1 ? data?.player1VSPrefab : data?.player2VSPrefab;
        if (data == null || prefab == null) return null;

        GameObject ship = Instantiate(prefab);
        Vector3 shipOffset = isPlayer1 ? data.player1VSPositionOffset : data.player2VSPositionOffset;
        ship.transform.position = config.position + shipOffset;
        ship.transform.rotation = Quaternion.Euler(config.rotation);

        // Multiply prefab's original scale by config scale multiplier
        Vector3 prefabScale = prefab.transform.localScale;
        float multiplier = config.scale > 0f ? config.scale : 1f;
        Vector3 finalScale = prefabScale * multiplier;

        // Store target scale for animations, start at zero for reveal
        if (isPlayer1) _player1TargetScale = finalScale;
        else _player2TargetScale = finalScale;

        ship.transform.localScale = Vector3.zero;

        // Disable all scripts except ShieldController (visual preview only)
        MonoBehaviour[] scripts = ship.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script is ShieldController) continue;
            script.enabled = false;
        }

        ship.SetActive(false);
        return ship;
    }

    private IEnumerator RunVersusSequence()
    {
        // --- Initial delay ---
        if (animation.initialDelay > 0f)
            yield return new WaitForSecondsRealtime(animation.initialDelay);

        // --- Cards slam in from sides ---
        Coroutine p1Slide = StartCoroutine(SlideCardIn(
            player1Card, _player1CardRestPos, true));

        if (animation.cardStaggerDelay > 0f)
            yield return new WaitForSecondsRealtime(animation.cardStaggerDelay);

        Coroutine p2Slide = StartCoroutine(SlideCardIn(
            player2Card, _player2CardRestPos, false));

        // Wait for both cards to finish
        yield return p1Slide;
        yield return p2Slide;

        // --- VS text stamps in ---
        if (animation.vsStampDelay > 0f)
            yield return new WaitForSecondsRealtime(animation.vsStampDelay);

        yield return StampVsText();

        // --- Ship models appear ---
        if (animation.shipAppearDelay > 0f)
            yield return new WaitForSecondsRealtime(animation.shipAppearDelay);

        Coroutine s1 = null;
        Coroutine s2 = null;

        if (_player1ShipInstance != null)
            s1 = StartCoroutine(RevealShipModel(_player1ShipInstance, _player1TargetScale));
        if (_player2ShipInstance != null)
            s2 = StartCoroutine(RevealShipModel(_player2ShipInstance, _player2TargetScale));

        if (s1 != null) yield return s1;
        if (s2 != null) yield return s2;

        // --- Hold ---
        if (animation.holdDuration > 0f)
            yield return new WaitForSecondsRealtime(animation.holdDuration);

        // --- Exit ---
        yield return RunExitAnimation();

        // --- Post-exit delay ---
        if (animation.postExitDelay > 0f)
            yield return new WaitForSecondsRealtime(animation.postExitDelay);

        // Cleanup
        if (_player1ShipInstance != null) Destroy(_player1ShipInstance);
        if (_player2ShipInstance != null) Destroy(_player2ShipInstance);

        vsScreenCanvas.gameObject.SetActive(false);

        onVersusScreenComplete?.Invoke();
    }

    private IEnumerator SlideCardIn(RectTransform card, Vector2 targetPos, bool isLeft)
    {
        Vector2 startPos = card.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < animation.cardSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animation.cardSlideDuration);
            float eased = EaseOutBack(t, animation.cardSlideOvershoot);

            card.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
            yield return null;
        }

        card.anchoredPosition = targetPos;

        // Play slam sound
        if (sounds.cardSlam != null && audioSource != null)
            sounds.cardSlam.Play(audioSource);
    }

    private IEnumerator StampVsText()
    {
        if (sounds.vsStamp != null && audioSource != null)
            sounds.vsStamp.Play(audioSource);

        float elapsed = 0f;

        while (elapsed < animation.vsStampDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animation.vsStampDuration);
            float eased = EaseOutBack(t, animation.vsStampOvershoot);

            float scale = eased; // 0 → overshoot → 1
            vText.localScale = new Vector3(scale, scale, 1f);
            sText.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        vText.localScale = Vector3.one;
        sText.localScale = Vector3.one;
    }

    private IEnumerator RevealShipModel(GameObject ship, Vector3 targetScale)
    {
        ship.SetActive(true);
        float elapsed = 0f;

        while (elapsed < animation.shipAppearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animation.shipAppearDuration);
            float eased = EaseOutBack(t, 0.3f);

            ship.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, eased);
            yield return null;
        }

        ship.transform.localScale = targetScale;
    }

    private IEnumerator RunExitAnimation()
    {
        if (sounds.exitWhoosh != null && audioSource != null)
            sounds.exitWhoosh.Play(audioSource);

        // Stop and clear all particle systems on ships so world-space particles
        // don't linger when the ship scales down / deactivates
        StopShipParticles(_player1ShipInstance);
        StopShipParticles(_player2ShipInstance);

        float elapsed = 0f;
        Vector2 p1Start = player1Card.anchoredPosition;
        Vector2 p2Start = player2Card.anchoredPosition;
        Vector2 p1Exit = new Vector2(p1Start.x - animation.cardSlideOffset, p1Start.y);
        Vector2 p2Exit = new Vector2(p2Start.x + animation.cardSlideOffset, p2Start.y);

        while (elapsed < animation.exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animation.exitDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            // Cards slide out to sides
            player1Card.anchoredPosition = Vector2.Lerp(p1Start, p1Exit, eased);
            player2Card.anchoredPosition = Vector2.Lerp(p2Start, p2Exit, eased);

            // V and S text scale down
            float vsScale = Mathf.Lerp(1f, 0f, eased);
            vText.localScale = new Vector3(vsScale, vsScale, 1f);
            sText.localScale = new Vector3(vsScale, vsScale, 1f);

            // Ships scale down
            if (_player1ShipInstance != null)
                _player1ShipInstance.transform.localScale = Vector3.Lerp(_player1TargetScale, Vector3.zero, eased);
            if (_player2ShipInstance != null)
                _player2ShipInstance.transform.localScale = Vector3.Lerp(_player2TargetScale, Vector3.zero, eased);

            // Overall canvas fades in the last 30% of the exit
            float fadeT = Mathf.InverseLerp(0.7f, 1f, t);
            vsScreenCanvas.alpha = 1f - fadeT;

            yield return null;
        }

        vsScreenCanvas.alpha = 0f;

        if (_player1ShipInstance != null) _player1ShipInstance.SetActive(false);
        if (_player2ShipInstance != null) _player2ShipInstance.SetActive(false);
    }

    private static void StopShipParticles(GameObject ship)
    {
        if (ship == null) return;
        ParticleSystem[] particles = ship.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// Attempt an ease-out-back curve. Goes past 1.0 then settles back.
    /// overshoot controls how far past 1.0 it goes (0 = no overshoot, 1 = strong).
    /// </summary>
    private static float EaseOutBack(float t, float overshoot)
    {
        float c = overshoot * 1.70158f;
        float t1 = t - 1f;
        return 1f + t1 * t1 * ((c + 1f) * t1 + c);
    }

    private void Reset()
    {
        // Sensible defaults
        player1ShipDisplay.scale = 1f;
        player2ShipDisplay.scale = 1f;

        animation.initialDelay = 0.3f;
        animation.cardSlideDuration = 0.5f;
        animation.cardSlideOffset = 2000f;
        animation.cardSlideOvershoot = 0.4f;
        animation.cardStaggerDelay = 0.1f;
        animation.vsStampDelay = 0.15f;
        animation.vsStampDuration = 0.35f;
        animation.vsStampOvershoot = 0.5f;
        animation.shipAppearDelay = 0.2f;
        animation.shipAppearDuration = 0.4f;
        animation.holdDuration = 2.0f;
        animation.exitDuration = 0.4f;
        animation.postExitDelay = 0.3f;
    }
}
