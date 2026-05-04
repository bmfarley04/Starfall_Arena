using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
using System.Security.Cryptography;

namespace StarfallArena.UI
{
    /// <summary>
    /// Manages the augment selection screen with tier-based random selection,
    /// animated UI transitions, and controller navigation support.
    /// Supports sequential two-player picking from the same pool.
    /// Only the current picking player's gamepad can navigate/select.
    /// </summary>
    public class AugmentSelectManager : MonoBehaviour
    {
        [Header("Augment Pools")]
        [Tooltip("Tier 1 augments (common)")]
        [SerializeField] private List<Augment> tier1Augments = new List<Augment>();

        [Tooltip("Tier 2 augments (rare)")]
        [SerializeField] private List<Augment> tier2Augments = new List<Augment>();

        [Tooltip("Tier 3 augments (legendary)")]
        [SerializeField] private List<Augment> tier3Augments = new List<Augment>();

        [Tooltip("Optional tier 4 augments. This Invasion-only tier is shown as a single card and is not included in the normal 2D random tier order.")]
        [SerializeField] private List<Augment> tier4Augments = new List<Augment>();

        [Header("Tier Selection Probabilities")]
        [Tooltip("Probability of tier 1 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier1Probability = 0.6f;

        [Tooltip("Probability of tier 2 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier2Probability = 0.3f;

        [Tooltip("Probability of tier 3 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier3Probability = 0.1f;

        [Header("Tier 1 UI References - Choice 1")]
        [SerializeField] private Image tier1Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice1Name;
        [SerializeField] private TextMeshProUGUI tier1Choice1Description;

        [Header("Tier 1 UI References - Choice 2")]
        [SerializeField] private Image tier1Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice2Name;
        [SerializeField] private TextMeshProUGUI tier1Choice2Description;

        [Header("Tier 1 UI References - Choice 3")]
        [SerializeField] private Image tier1Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice3Name;
        [SerializeField] private TextMeshProUGUI tier1Choice3Description;

        [Header("Tier 2 UI References - Choice 1")]
        [SerializeField] private Image tier2Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice1Name;
        [SerializeField] private TextMeshProUGUI tier2Choice1Description;

        [Header("Tier 2 UI References - Choice 2")]
        [SerializeField] private Image tier2Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice2Name;
        [SerializeField] private TextMeshProUGUI tier2Choice2Description;

        [Header("Tier 2 UI References - Choice 3")]
        [SerializeField] private Image tier2Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice3Name;
        [SerializeField] private TextMeshProUGUI tier2Choice3Description;

        [Header("Tier 3 UI References - Choice 1")]
        [SerializeField] private Image tier3Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice1Name;
        [SerializeField] private TextMeshProUGUI tier3Choice1Description;

        [Header("Tier 3 UI References - Choice 2")]
        [SerializeField] private Image tier3Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice2Name;
        [SerializeField] private TextMeshProUGUI tier3Choice2Description;

        [Header("Tier 3 UI References - Choice 3")]
        [SerializeField] private Image tier3Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice3Name;
        [SerializeField] private TextMeshProUGUI tier3Choice3Description;

        [Header("Tier 4 UI References - Choice 1")]
        [Tooltip("Invasion-only tier 4 choice 1 icon. Leave unassigned in 2D scenes.")]
        [SerializeField] private Image tier4Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier4Choice1Name;
        [SerializeField] private TextMeshProUGUI tier4Choice1Description;

        [Header("Canvas Groups")]
        [Tooltip("Canvas group containing tier 1 UI")]
        [SerializeField] private CanvasGroup tier1CanvasGroup;

        [Tooltip("Canvas group containing tier 2 UI")]
        [SerializeField] private CanvasGroup tier2CanvasGroup;

        [Tooltip("Canvas group containing tier 3 UI")]
        [SerializeField] private CanvasGroup tier3CanvasGroup;

        [Tooltip("Optional canvas group containing tier 4 UI. Used by 3D Invasion reward visuals when assigned.")]
        [SerializeField] private CanvasGroup tier4CanvasGroup;

        [Header("Card Buttons")]
        [SerializeField] private Button[] tier1Buttons = new Button[3];
        [SerializeField] private Button[] tier2Buttons = new Button[3];
        [SerializeField] private Button[] tier3Buttons = new Button[3];
        [Tooltip("Single Invasion-only tier 4 button. Leave unassigned in 2D scenes.")]
        [SerializeField] private Button[] tier4Buttons = new Button[1];

        [Header("Card Containers (border Image per card)")]
        [Tooltip("The 'container' Image child of each tier 1 card (border element)")]
        [SerializeField] private Image[] tier1Containers = new Image[3];
        [Tooltip("The 'container' Image child of each tier 2 card (border element)")]
        [SerializeField] private Image[] tier2Containers = new Image[3];
        [Tooltip("The 'container' Image child of each tier 3 card (border element)")]
        [SerializeField] private Image[] tier3Containers = new Image[3];
        [Tooltip("The 'container' Image child of the single tier 4 card. Used only when the Invasion-only tier 4 UI is assigned.")]
        [SerializeField] private Image[] tier4Containers = new Image[1];

        [Header("Inner Card Containers (inner border Image per card)")]
        [Tooltip("The 'inner container' Image child of each tier 1 card (inner border element)")]
        [SerializeField] private Image[] tier1InnerContainers = new Image[3];
        [Tooltip("The 'inner container' Image child of each tier 2 card (inner border element)")]
        [SerializeField] private Image[] tier2InnerContainers = new Image[3];
        [Tooltip("The 'inner container' Image child of each tier 3 card (inner border element)")]
        [SerializeField] private Image[] tier3InnerContainers = new Image[3];
        [Tooltip("The inner border Image child of the single tier 4 card. Used only when the Invasion-only tier 4 UI is assigned.")]
        [SerializeField] private Image[] tier4InnerContainers = new Image[1];

        [Header("Hover / Selection Scale")]
        [Tooltip("X scale multiplier when a card is hovered/selected")]
        [SerializeField] private float hoverScaleX = 1.1f;
        [Tooltip("Y scale multiplier when a card is hovered/selected")]
        [SerializeField] private float hoverScaleY = 1.1f;
        [Tooltip("Duration of the hover scale transition")]
        [SerializeField] private float hoverScaleDuration = 0.12f;

        [Header("Player Choice UI")]
        [Tooltip("Text field showing which player is choosing (e.g. 'PLAYER 1 CHOICE')")]
        [SerializeField] private TextMeshProUGUI playerChoiceText;
        [Tooltip("Countdown timer text field")]
        [SerializeField] private TextMeshProUGUI countdownTimerText;
        [Tooltip("Time in seconds for each player to pick an augment")]
        [SerializeField] private float selectionTimeLimit = 10f;
        [Tooltip("Color for Player 1 choice text and timer")]
        [SerializeField] private Color player1Color = Color.cyan;
        [Tooltip("Color for Player 2 choice text and timer")]
        [SerializeField] private Color player2Color = Color.red;

        [Header("Player Choice Fade")]
        [Tooltip("Duration of player choice / timer text fade in/out")]
        [SerializeField] private float playerChoiceFadeDuration = 0.3f;

        [Header("Card Disable Animation")]
        [Tooltip("Duration of the shrink animation when a chosen card is disabled")]
        [SerializeField] private float cardDisableShrinkDuration = 0.3f;

        [Header("Selection Effect")]
        [Tooltip("Material applied to the container border and icon when a card is selected")]
        [SerializeField] private Material selectedMaterial;
        [Tooltip("Duration of the white text flash on Title and Description")]
        [SerializeField] private float textFlashDuration = 0.4f;

        [Header("Selection Flow")]
        [Tooltip("Extra time to keep the final selected card visible before leaving augment select")]
        [Min(0f)]
        [SerializeField] private float finalSelectionHoldDuration = 0.2f;

        [Header("Animation Settings")]
        [Tooltip("Duration of the entrance animation")]
        [SerializeField] private float animationDuration = 0.6f;

        [Tooltip("Animation curve for entrance (bounce/overshoot recommended)")]
        [SerializeField] private AnimationCurve entranceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Starting scale for cards (0 = from center point)")]
        [SerializeField] private float startScale = 0f;

        [Tooltip("Delay between each card animation")]
        [SerializeField] private float cardAnimationDelay = 0.1f;

        [Header("Controller Navigation")]
        [Tooltip("Dead-zone for stick navigation (below this = no input)")]
        [SerializeField] private float stickDeadZone = 0.5f;

        [Header("Audio")]
        [Tooltip("Sound played when augment screen appears")]
        [SerializeField] private SoundEffect augmentAppearSound;

        [Tooltip("AudioSource for playing UI sounds (2D)")]
        [SerializeField] private AudioSource audioSource;

        [Header("Debug")]
        [Tooltip("Automatically show augment select on Start (for testing)")]
        [SerializeField] private bool debugShowOnStart = false;

        [Tooltip("Enable F1 key to trigger augment select during play (for testing)")]
        [SerializeField] private bool debugEnableKeyTrigger = true;

        [Tooltip("Logs detailed augment-card focus changes, navigation input, and selection fallback decisions for debugging tier-specific UI issues.")]
        [SerializeField] private bool debugLogSelectionFocus = false;

        // ===== EVENTS =====
        /// <summary>
        /// Fired when a player selects an augment. Parameters: chosen augment, choice index (0-2).
        /// </summary>
        public System.Action<Augment, int> onAugmentChosen;

        // Internal state
        private int currentTier;
        private int currentPickingPlayer; // 1 or 2
        private List<Augment> selectedAugments = new List<Augment>(3);
        private List<int> selectedAugmentVisualTiers;
        private bool isShowing = false;
        private Coroutine _countdownCoroutine;

        // Per-game tier sequence: one of each tier in randomized order
        private List<int> _gameTierOrder = new List<int>(3);
        private int _gameTierOrderIndex = 0;
        private System.Random _tierOrderRng;

        // CanvasGroups for player choice / timer text (auto-created in Start)
        private CanvasGroup _playerChoiceCG;
        private CanvasGroup _countdownTimerCG;

        // Transition state
        private Coroutine _transitionCoroutine;

        // Hover state — tracks original scales per card button so we restore correctly
        private Dictionary<Button, Vector3> _cardOriginalScales = new Dictionary<Button, Vector3>();
        private Dictionary<Button, Coroutine> _hoverCoroutines = new Dictionary<Button, Coroutine>();

        // Per-player gamepad lock
        private Gamepad _player1Gamepad;
        private Gamepad _player2Gamepad;
        private Gamepad _activeGamepad; // the gamepad allowed to navigate/select right now
        private InputSystemUIInputModule _uiInputModule;
        private InputActionReference _cachedMoveAction;
        private InputActionReference _cachedSubmitAction;

        // Stick navigation cooldown (prevents rapid-fire from held stick)
        private bool _stickNavigated = false;
        private bool _useExternalTimer = false;
        private int _selectedCardIndex = -1;
        private CardVisualSnapshot[][] _originalCardVisuals;
        private struct ImageVisualSnapshot
        {
            public bool captured;
            public Color color;
            public Material material;
            public Image.Type type;
            public bool preserveAspect;
        }

        private struct TextVisualSnapshot
        {
            public bool captured;
            public Color color;
            public Material material;
            public TMP_FontAsset font;
            public FontStyles fontStyle;
            public TextAlignmentOptions alignment;
        }

        private struct CardVisualSnapshot
        {
            public ImageVisualSnapshot container;
            public ImageVisualSnapshot innerContainer;
            public ImageVisualSnapshot icon;
            public TextVisualSnapshot name;
            public TextVisualSnapshot description;
        }

        // Selection effect: cached original materials per card so we can restore
        private Dictionary<Image, Material> _originalContainerMaterials = new Dictionary<Image, Material>();
        private Dictionary<Image, Material> _originalIconMaterials = new Dictionary<Image, Material>();

        public bool IsShowing => isShowing;
        public float SelectionTimeLimit => selectionTimeLimit;

        private void OnValidate()
        {
            // Warn if probabilities don't sum to 1
            float sum = tier1Probability + tier2Probability + tier3Probability;
            if (Mathf.Abs(sum - 1f) > 0.001f)
            {
                Debug.LogWarning($"Tier probabilities sum to {sum:F3}, but should sum to 1.0");
            }
        }

        private void Start()
        {
            // Hide all tiers at start
            HideAllTiers();

            // Ensure CanvasGroups exist on player choice / timer text
            _playerChoiceCG = EnsureCanvasGroup(playerChoiceText);
            _countdownTimerCG = EnsureCanvasGroup(countdownTimerText);

            // Start with both hidden
            if (_playerChoiceCG != null) _playerChoiceCG.alpha = 0f;
            if (_countdownTimerCG != null) _countdownTimerCG.alpha = 0f;

            // Cache the InputSystemUIInputModule from EventSystem
            if (EventSystem.current != null)
                _uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();

            // Wire button onClick events + hover listeners for all tiers
            WireButtonEvents(tier1Buttons);
            WireButtonEvents(tier2Buttons);
            WireButtonEvents(tier3Buttons);
            WireButtonEvents(tier4Buttons);

            // Create isolated RNG so tier shuffling is not affected by UnityEngine.Random.InitState calls in other systems
            InitializeTierOrderRng();

            // Build randomized tier order for this game (one of each tier)
            GenerateRandomizedGameTierOrder();

            // Debug: Auto-show if enabled
            if (debugShowOnStart)
            {
                ShowAugmentSelect(1);
            }
        }

        /// <summary>
        /// Initializes an isolated RNG with a non-deterministic seed.
        /// This avoids deterministic tier order when Unity's global Random state is reset elsewhere.
        /// </summary>
        private void InitializeTierOrderRng()
        {
            byte[] seedBytes = new byte[4];
            RandomNumberGenerator.Fill(seedBytes);
            int seed = System.BitConverter.ToInt32(seedBytes, 0);
            _tierOrderRng = new System.Random(seed);
        }

        /// <summary>
        /// Generates a randomized order containing exactly one of each normal duel tier.
        /// Tier 4 is Invasion-only and must be requested explicitly by the reward presenter.
        /// </summary>
        private void GenerateRandomizedGameTierOrder()
        {
            _gameTierOrder.Clear();
            _gameTierOrder.Add(1);
            _gameTierOrder.Add(2);
            _gameTierOrder.Add(3);

            if (_tierOrderRng == null)
                InitializeTierOrderRng();

            for (int i = _gameTierOrder.Count - 1; i > 0; i--)
            {
                int j = _tierOrderRng.Next(i + 1);
                (_gameTierOrder[i], _gameTierOrder[j]) = (_gameTierOrder[j], _gameTierOrder[i]);
            }

            _gameTierOrderIndex = 0;
            Debug.Log($"[AugmentSelect] Game tier order: {string.Join(" -> ", _gameTierOrder)}");
        }

        private void Update()
        {
            // Debug: Trigger with F1 key press (new Input System)
            if (debugEnableKeyTrigger && !isShowing)
            {
                if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                {
                    ShowAugmentSelect(1);
                }
            }

            // Manual gamepad polling — only the active picker's gamepad can navigate/select
            if (isShowing && _activeGamepad != null)
            {
                PollGamepadNavigation();
            }

            // Manual keyboard polling for networked mode.
            // We disable the UIInputModule's move/submit during selection so the
            // gamepad doesn't drive both manual polling and the EventSystem at once
            // (which caused the middle card to be skipped). Poll the keyboard here
            // so KB/M users can still navigate and confirm.
            if (isShowing)
            {
                PollKeyboardNavigation();
            }
        }

        // ===== GAMEPAD ASSIGNMENT =====

        /// <summary>
        /// Call from SceneManager to tell the augment screen which gamepad belongs to which player.
        /// Must be called before players are destroyed (capture from PlayerInput.devices).
        /// </summary>
        public void AssignGamepads(Gamepad player1Pad, Gamepad player2Pad)
        {
            _player1Gamepad = player1Pad;
            _player2Gamepad = player2Pad;
        }

        /// <summary>
        /// Sets the active gamepad based on which player is currently picking.
        /// </summary>
        private void SetActiveGamepad(int playerNumber)
        {
            _activeGamepad = (playerNumber == 1) ? _player1Gamepad : _player2Gamepad;
        }

        // ===== MANUAL GAMEPAD POLLING =====

        /// <summary>
        /// Polls only the active picker's gamepad for navigation (dpad/left stick) and submit (south/A button).
        /// EventSystem's built-in InputSystemUIInputModule is disabled during augment selection
        /// so both gamepads don't drive UI simultaneously.
        /// </summary>
        private void PollGamepadNavigation()
        {
            Gamepad pad = _activeGamepad;
            if (pad == null) return;

            // --- Submit (A / South button) ---
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                if (TryGetButtonForSelectedCard(out Button selectedButton))
                {
                    selectedButton.onClick.Invoke();
                    return;
                }
            }

            // --- D-Pad navigation ---
            if (pad.dpad.left.wasPressedThisFrame)
            {
                NavigateCards(-1);
                return;
            }
            if (pad.dpad.right.wasPressedThisFrame)
            {
                NavigateCards(1);
                return;
            }

            // --- Left stick navigation (with dead-zone + cooldown) ---
            float stickX = pad.leftStick.ReadValue().x;
            if (Mathf.Abs(stickX) > stickDeadZone)
            {
                if (!_stickNavigated)
                {
                    NavigateCards(stickX > 0 ? 1 : -1);
                    _stickNavigated = true;
                }
            }
            else
            {
                _stickNavigated = false;
            }
        }

        /// <summary>
        /// Moves the EventSystem selection left (-1) or right (+1) among active card buttons.
        /// </summary>
        private void NavigateCards(int direction)
        {
            Button[] buttons = GetButtonsForTier(currentTier);
            if (buttons == null) return;

            // Build list of active buttons
            List<int> activeIndices = new List<int>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.activeSelf && buttons[i].interactable)
                    activeIndices.Add(i);
            }
            if (activeIndices.Count == 0) return;

            int currentIndex = activeIndices.IndexOf(_selectedCardIndex);

            // Compute next index
            int nextIndex;
            if (currentIndex < 0)
            {
                nextIndex = 0; // nothing selected, go to first
            }
            else
            {
                nextIndex = currentIndex + direction;
                nextIndex = Mathf.Clamp(nextIndex, 0, activeIndices.Count - 1);
            }

            int btnIndex = activeIndices[nextIndex];
            LogSelectionDebug($"NavigateCards direction={direction} active=[{string.Join(",", activeIndices)}] currentIndex={currentIndex} nextButtonIndex={btnIndex}");
            SetSelectedCardIndex(btnIndex);
        }

        private bool TryGetButtonForSelectedCard(out Button button)
        {
            button = null;
            Button[] buttons = GetButtonsForTier(currentTier);
            if (buttons == null)
            {
                return false;
            }

            if (_selectedCardIndex >= 0
                && _selectedCardIndex < buttons.Length
                && buttons[_selectedCardIndex] != null
                && buttons[_selectedCardIndex].gameObject.activeSelf
                && buttons[_selectedCardIndex].interactable)
            {
                button = buttons[_selectedCardIndex];
                LogSelectionDebug($"TryGetButtonForSelectedCard reused selected index {_selectedCardIndex}", button, _selectedCardIndex);
                return true;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].gameObject.activeSelf || !buttons[i].interactable)
                {
                    continue;
                }

                SetSelectedCardIndex(i);
                button = buttons[i];
                LogSelectionDebug($"TryGetButtonForSelectedCard fell back to first active index {i}", button, i);
                return true;
            }

            LogSelectionDebug("TryGetButtonForSelectedCard found no active button.");
            return false;
        }

        private void SetSelectedCardIndex(int cardIndex)
        {
            Button[] buttons = GetButtonsForTier(currentTier);
            if (buttons == null
                || cardIndex < 0
                || cardIndex >= buttons.Length
                || buttons[cardIndex] == null
                || !buttons[cardIndex].gameObject.activeSelf
                || !buttons[cardIndex].interactable)
            {
                LogSelectionDebug($"SetSelectedCardIndex ignored invalid index {cardIndex}");
                return;
            }

            _selectedCardIndex = cardIndex;
            LogSelectionDebug("SetSelectedCardIndex applied", buttons[cardIndex], cardIndex);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(buttons[cardIndex].gameObject);
            }
        }

        /// <summary>
        /// Polls the keyboard for navigation (arrow keys / A,D) and submit (Enter / Space).
        /// Used so KB/M players can drive augment select while the UIInputModule's
        /// move/submit are disabled to prevent gamepad double-input.
        /// </summary>
        private void PollKeyboardNavigation()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // Submit (Enter / Space)
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                if (TryGetButtonForSelectedCard(out Button selectedButton))
                {
                    selectedButton.onClick.Invoke();
                    return;
                }
            }

            // Navigation (arrows / A,D)
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            {
                NavigateCards(-1);
                return;
            }
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            {
                NavigateCards(1);
                return;
            }
        }

        // ===== CANVAS GROUP HELPER =====

        /// <summary>
        /// Ensures a CanvasGroup component exists on the TMP text's GameObject.
        /// Returns null if the text reference itself is null.
        /// </summary>
        private CanvasGroup EnsureCanvasGroup(TextMeshProUGUI textField)
        {
            if (textField == null) return null;
            CanvasGroup cg = textField.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = textField.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        // ===== BUTTON WIRING =====

        /// <summary>
        /// Wires onClick and select/deselect listeners for a set of card buttons.
        /// onClick is still wired so both manual polling (A button → onClick.Invoke)
        /// and EventSystem fallback path work.
        /// </summary>
        private void WireButtonEvents(Button[] buttons)
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                int index = i; // capture for closure
                Button btn = buttons[i];

                // onClick → select this augment
                btn.onClick.AddListener(() => OnAugmentSelected(index));

                // Add EventTrigger for Select / Deselect (controller navigation)
                EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = btn.gameObject.AddComponent<EventTrigger>();

                // On Select (hovered via controller)
                EventTrigger.Entry selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
                selectEntry.callback.AddListener(_ =>
                {
                    _selectedCardIndex = index;
                    LogSelectionDebug("EventTrigger Select", btn, index);
                    OnCardHoverEnter(btn);
                });
                trigger.triggers.Add(selectEntry);

                // On Deselect (moved away via controller)
                EventTrigger.Entry deselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
                deselectEntry.callback.AddListener(_ =>
                {
                    LogSelectionDebug("EventTrigger Deselect", btn, index);
                    OnCardHoverExit(btn);
                });
                trigger.triggers.Add(deselectEntry);

                // On Pointer Enter (mouse fallback)
                EventTrigger.Entry pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                pointerEnter.callback.AddListener(_ => LogSelectionDebug("EventTrigger PointerEnter", btn, index));
                trigger.triggers.Add(pointerEnter);

                EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                pointerDown.callback.AddListener(_ =>
                {
                    // Mouse can still click a card directly, but it never becomes
                    // the owner of selection state for controller/keyboard flow.
                    _selectedCardIndex = index;
                    LogSelectionDebug("EventTrigger PointerDown", btn, index);
                });
                trigger.triggers.Add(pointerDown);
            }
        }

        // ===== HOVER SCALE =====

        private void OnCardHoverEnter(Button btn)
        {
            if (btn == null) return;

            if (!_cardOriginalScales.ContainsKey(btn))
                _cardOriginalScales[btn] = btn.transform.localScale;

            Vector3 target = new Vector3(
                _cardOriginalScales[btn].x * hoverScaleX,
                _cardOriginalScales[btn].y * hoverScaleY,
                _cardOriginalScales[btn].z
            );

            if (_hoverCoroutines.ContainsKey(btn) && _hoverCoroutines[btn] != null)
                StopCoroutine(_hoverCoroutines[btn]);

            _hoverCoroutines[btn] = StartCoroutine(LerpScale(btn.transform, target, hoverScaleDuration));
        }

        private void OnCardHoverExit(Button btn)
        {
            if (btn == null) return;

            if (!_cardOriginalScales.ContainsKey(btn)) return;

            if (_hoverCoroutines.ContainsKey(btn) && _hoverCoroutines[btn] != null)
                StopCoroutine(_hoverCoroutines[btn]);

            _hoverCoroutines[btn] = StartCoroutine(LerpScale(btn.transform, _cardOriginalScales[btn], hoverScaleDuration));
        }

        private IEnumerator LerpScale(Transform t, Vector3 target, float duration)
        {
            Vector3 start = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                t.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            t.localScale = target;
        }

        // ===== SHOW / HIDE =====

        /// <summary>
        /// MAIN FUNCTION: Call this from your scene manager to show the augment select screen.
        /// </summary>
        /// <param name="pickingPlayer">Which player is picking (1 or 2). Used for UI text + color.</param>
        [ContextMenu("Debug: Show Augment Select")]
        public void ShowAugmentSelect(int pickingPlayer = 1)
        {
            if (isShowing)
            {
                Debug.LogWarning("Augment select is already showing!");
                return;
            }

            isShowing = true;
            currentPickingPlayer = pickingPlayer;
            _useExternalTimer = false;
            _selectedCardIndex = -1;
            _stickNavigated = false;

            // Lock input to the picking player's gamepad
            SetActiveGamepad(pickingPlayer);
            DisableUIModuleNavigation();

            // Select tier based on probabilities
            currentTier = SelectRandomTier();
            Debug.Log($"Selected augment tier: {currentTier}");

            // Pick randomly from tier
            selectedAugments = SelectRandomAugments(currentTier);
            selectedAugmentVisualTiers = null;

            // Populate UI with selected augments
            PopulateUI(currentTier, selectedAugments);

            // Update player choice text and colors
            UpdatePlayerChoiceUI(pickingPlayer);

            // Show only the selected tier's canvas group (activates GameObject)
            // Cards start invisible — the animation reveals them
            ShowTierCanvas(currentTier);

            // Play entrance animation (also fades in player choice / timer text)
            StartCoroutine(AnimateEntrance(currentTier));

            // Start countdown timer
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(RunCountdownTimer());

            // Play sound effect
            if (augmentAppearSound != null && audioSource != null)
            {
                augmentAppearSound.Play(audioSource);
            }
        }

        public void ShowNetworkAugmentSelect(int pickingPlayer, int tier, List<Augment> augments)
        {
            ShowNetworkAugmentSelect(pickingPlayer, tier, augments, null);
        }

        public void ShowNetworkAugmentSelect(int pickingPlayer, int tier, List<Augment> augments, IReadOnlyList<int> perCardVisualTiers)
        {
            if (augments == null || augments.Count == 0)
            {
                Debug.LogWarning("[AugmentSelect] No augments supplied for network selection.");
                return;
            }

            if (isShowing)
            {
                HideAugmentSelect();
            }

            isShowing = true;
            currentPickingPlayer = pickingPlayer;
            currentTier = ResolveDisplayedTier(tier, perCardVisualTiers);
            _useExternalTimer = true;
            _selectedCardIndex = -1;
            _stickNavigated = false;

            // Networked play: each client has only one local gamepad, so always use it
            // regardless of slot. We disable the UIInputModule (same as local split-screen)
            // so the gamepad isn't driving both the EventSystem and manual polling — that
            // double-fires navigation and skips the middle card. KB/M is handled by
            // PollKeyboardNavigation while selection is showing.
            _activeGamepad = Gamepad.current ?? (Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
            DisableUIModuleNavigation();

            selectedAugments = new List<Augment>(augments);
            selectedAugmentVisualTiers = perCardVisualTiers != null ? new List<int>(perCardVisualTiers) : null;
            PopulateUI(currentTier, selectedAugments);
            UpdateDisplayedOptionCount(currentTier, selectedAugments.Count);
            UpdatePlayerChoiceUI(pickingPlayer);
            ShowTierCanvas(currentTier);
            StartCoroutine(AnimateEntrance(currentTier));

            if (augmentAppearSound != null && audioSource != null)
            {
                augmentAppearSound.Play(audioSource);
            }
        }

        /// <summary>
        /// Returns the list of currently selected augments (for removing chosen augment from pool).
        /// </summary>
        public List<Augment> GetSelectedAugments()
        {
            return selectedAugments;
        }

        public int DrawNextTierForRound()
        {
            return SelectRandomTier();
        }

        public List<Augment> DrawRandomAugmentsForTier(int tier, int count)
        {
            return SelectRandomAugments(tier, count);
        }

        public void SetCountdownValue(float timeRemaining)
        {
            if (countdownTimerText != null)
            {
                countdownTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining)).ToString();
            }
        }

        /// <summary>
        /// Transitions from the first picker to the second picker on the same screen.
        /// Plays selection effect, shrinks and deactivates the chosen card so the
        /// HorizontalLayoutGroup reflows, fades out/in the player choice text, resets the timer.
        /// </summary>
        /// <param name="chosenCardIndex">Index (0-2) of the card the first picker chose.</param>
        /// <param name="secondPickerPlayer">Player number (1 or 2) for the second picker.</param>
        public void TransitionToSecondPicker(int chosenCardIndex, int secondPickerPlayer)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(DoSecondPickerTransition(chosenCardIndex, secondPickerPlayer));
        }

        private IEnumerator DoSecondPickerTransition(int chosenCardIndex, int secondPickerPlayer)
        {
            Button[] buttons = GetButtonsForTier(currentTier);
            if (buttons == null) yield break;

            // --- 1. Play selection effect on chosen card (material swap + text flash) ---
            yield return StartCoroutine(PlaySelectionEffect(chosenCardIndex));

            // --- 2. Shrink the chosen card to zero scale, then deactivate it ---
            Button chosenBtn = (chosenCardIndex >= 0 && chosenCardIndex < buttons.Length)
                ? buttons[chosenCardIndex]
                : null;

            if (chosenBtn != null)
            {
                // Cancel any hover coroutine on this card
                if (_hoverCoroutines.ContainsKey(chosenBtn) && _hoverCoroutines[chosenBtn] != null)
                    StopCoroutine(_hoverCoroutines[chosenBtn]);

                yield return StartCoroutine(LerpScale(chosenBtn.transform, Vector3.zero, cardDisableShrinkDuration));

                // Deactivate so HorizontalLayoutGroup reflows remaining cards
                chosenBtn.gameObject.SetActive(false);

                // Remove from hover tracking
                _cardOriginalScales.Remove(chosenBtn);
                _hoverCoroutines.Remove(chosenBtn);
            }

            // --- 3. Fade out player choice + timer text ---
            yield return StartCoroutine(FadePlayerChoiceUI(0f, playerChoiceFadeDuration));

            // --- 4. Switch active gamepad to second picker ---
            currentPickingPlayer = secondPickerPlayer;
            SetActiveGamepad(secondPickerPlayer);

            // --- 5. Update text for second picker ---
            UpdatePlayerChoiceUI(secondPickerPlayer);

            // --- 6. Fade in player choice + timer text ---
            yield return StartCoroutine(FadePlayerChoiceUI(1f, playerChoiceFadeDuration));

            // --- 7. Restart countdown timer ---
            if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(RunCountdownTimer());

            // --- 8. Re-enable interactability on the tier canvas group ---
            CanvasGroup tierCG = GetCanvasGroupForTier(currentTier);
            if (tierCG != null)
            {
                tierCG.interactable = true;
                tierCG.blocksRaycasts = true;
            }
            SetTierButtonsInteractable(currentTier, true);

            // --- 9. Set default selection to first remaining active button ---
            SetDefaultSelectionFirstActive(currentTier);

            // --- 10. Re-cache original scales for remaining cards (they may have shifted) ---
            _cardOriginalScales.Clear();
            foreach (var btn in buttons)
            {
                if (btn != null && btn.gameObject.activeSelf)
                    _cardOriginalScales[btn] = btn.transform.localScale;
            }

            _transitionCoroutine = null;
        }

        /// <summary>
        /// Plays the selection effect for the final picker, then hides the augment UI.
        /// This ensures the second player's choice is visible before transitioning out.
        /// </summary>
        public IEnumerator PlayFinalSelectionThenHide(int finalChoiceIndex)
        {
            if (!isShowing) yield break;

            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            SetTierButtonsInteractable(currentTier, false);

            CanvasGroup tierCG = GetCanvasGroupForTier(currentTier);
            if (tierCG != null)
            {
                tierCG.interactable = false;
                tierCG.blocksRaycasts = false;
            }

            yield return StartCoroutine(PlaySelectionEffect(finalChoiceIndex));

            if (finalSelectionHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(finalSelectionHoldDuration);

            HideAugmentSelect();
        }

        /// <summary>
        /// Hides the augment select screen. Call after the second player has picked.
        /// Reactivates all cards so they are ready for the next augment phase.
        /// </summary>
        public void HideAugmentSelect()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            // Reset any hover scales
            foreach (var kvp in _cardOriginalScales)
            {
                if (kvp.Key != null)
                    kvp.Key.transform.localScale = kvp.Value;
            }
            _cardOriginalScales.Clear();

            // Reactivate all cards (some may have been deactivated during sequential pick)
            ReactivateAllCards();

            // Restore all card materials to originals
            RestoreAllCardMaterials();

            // Hide player choice / timer text
            if (_playerChoiceCG != null) _playerChoiceCG.alpha = 0f;
            if (_countdownTimerCG != null) _countdownTimerCG.alpha = 0f;

            HideAllTiers();
            isShowing = false;
            _activeGamepad = null;
            _useExternalTimer = false;
            _selectedCardIndex = -1;
            _stickNavigated = false;
            selectedAugmentVisualTiers = null;

            // Re-enable the UI module navigation for other screens
            EnableUIModuleNavigation();
        }

        // ===== INPUT MODULE CONTROL =====

        /// <summary>
        /// Disables the InputSystemUIInputModule's move and submit actions
        /// so that both gamepads don't simultaneously drive the EventSystem.
        /// We manually poll only the active picker's gamepad instead.
        /// </summary>
        private void DisableUIModuleNavigation()
        {
            // Disable the UI module's move/submit so both gamepads don't drive EventSystem
            // navigation simultaneously. Manual polling in PollGamepadNavigation handles
            // input for only the active picker's gamepad. The module stays enabled so
            // mouse pointer events (hover, click) continue to work.
            if (_uiInputModule == null) return;
            _cachedMoveAction = _uiInputModule.move;
            _cachedSubmitAction = _uiInputModule.submit;
            _uiInputModule.move = null;
            _uiInputModule.submit = null;
        }

        private void EnableUIModuleNavigation()
        {
            if (_uiInputModule == null) return;
            _uiInputModule.move = _cachedMoveAction;
            _uiInputModule.submit = _cachedSubmitAction;
        }

        // ===== SELECTION EFFECT =====

        /// <summary>
        /// Plays a visual effect on the chosen card: swaps container + icon material,
        /// flashes title and description text white.
        /// </summary>
        private IEnumerator PlaySelectionEffect(int choiceIndex)
        {
            if (selectedMaterial == null) yield break;

            // Get references for this card
            Image outerContainer = GetContainerForCard(currentTier, choiceIndex);
            Image innerContainer = GetInnerContainerForCard(currentTier, choiceIndex);
            Image icon = GetIconForCard(currentTier, choiceIndex);
            TextMeshProUGUI titleText = GetNameForCard(currentTier, choiceIndex);
            TextMeshProUGUI descText = GetDescriptionForCard(currentTier, choiceIndex);

            // Cache originals and swap all visual targets.
            ApplySelectedMaterial(outerContainer, _originalContainerMaterials);
            ApplySelectedMaterial(innerContainer, _originalContainerMaterials);
            ApplySelectedMaterial(icon, _originalIconMaterials);

            // Flash text white
            Color titleOriginal = titleText != null ? titleText.color : Color.white;
            Color descOriginal = descText != null ? descText.color : Color.white;

            if (titleText != null) titleText.color = Color.white;
            if (descText != null) descText.color = Color.white;

            yield return new WaitForSecondsRealtime(textFlashDuration);

            // Restore text colors
            if (titleText != null) titleText.color = titleOriginal;
            if (descText != null) descText.color = descOriginal;
        }

        /// <summary>
        /// Restores all cached original materials on containers and icons.
        /// Called on HideAugmentSelect so cards are clean for the next phase.
        /// </summary>
        private void RestoreAllCardMaterials()
        {
            foreach (var kvp in _originalContainerMaterials)
            {
                if (kvp.Key != null)
                    kvp.Key.material = kvp.Value;
            }
            _originalContainerMaterials.Clear();

            foreach (var kvp in _originalIconMaterials)
            {
                if (kvp.Key != null)
                    kvp.Key.material = kvp.Value;
            }
            _originalIconMaterials.Clear();
        }

        // ===== CARD REFERENCE GETTERS =====

        private Image GetContainerForCard(int tier, int index)
        {
            Image[] containers = tier switch
            {
                1 => tier1Containers,
                2 => tier2Containers,
                3 => tier3Containers,
                4 => tier4Containers,
                _ => tier1Containers
            };
            return (containers != null && index >= 0 && index < containers.Length) ? containers[index] : null;
        }

        private Image GetInnerContainerForCard(int tier, int index)
        {
            Image[] containers = tier switch
            {
                1 => tier1InnerContainers,
                2 => tier2InnerContainers,
                3 => tier3InnerContainers,
                4 => tier4InnerContainers,
                _ => tier1InnerContainers
            };
            return (containers != null && index >= 0 && index < containers.Length) ? containers[index] : null;
        }

        private Image GetIconForCard(int tier, int index)
        {
            return (tier, index) switch
            {
                (1, 0) => tier1Choice1Icon,
                (1, 1) => tier1Choice2Icon,
                (1, 2) => tier1Choice3Icon,
                (2, 0) => tier2Choice1Icon,
                (2, 1) => tier2Choice2Icon,
                (2, 2) => tier2Choice3Icon,
                (3, 0) => tier3Choice1Icon,
                (3, 1) => tier3Choice2Icon,
                (3, 2) => tier3Choice3Icon,
                (4, 0) => tier4Choice1Icon,
                _ => null
            };
        }

        private TextMeshProUGUI GetNameForCard(int tier, int index)
        {
            return (tier, index) switch
            {
                (1, 0) => tier1Choice1Name,
                (1, 1) => tier1Choice2Name,
                (1, 2) => tier1Choice3Name,
                (2, 0) => tier2Choice1Name,
                (2, 1) => tier2Choice2Name,
                (2, 2) => tier2Choice3Name,
                (3, 0) => tier3Choice1Name,
                (3, 1) => tier3Choice2Name,
                (3, 2) => tier3Choice3Name,
                (4, 0) => tier4Choice1Name,
                _ => null
            };
        }

        private TextMeshProUGUI GetDescriptionForCard(int tier, int index)
        {
            return (tier, index) switch
            {
                (1, 0) => tier1Choice1Description,
                (1, 1) => tier1Choice2Description,
                (1, 2) => tier1Choice3Description,
                (2, 0) => tier2Choice1Description,
                (2, 1) => tier2Choice2Description,
                (2, 2) => tier2Choice3Description,
                (3, 0) => tier3Choice1Description,
                (3, 1) => tier3Choice2Description,
                (3, 2) => tier3Choice3Description,
                (4, 0) => tier4Choice1Description,
                _ => null
            };
        }

        // ===== PLAYER CHOICE UI =====

        private void UpdatePlayerChoiceUI(int playerNumber)
        {
            Color c = playerNumber == 1 ? player1Color : player2Color;

            if (playerChoiceText != null)
            {
                playerChoiceText.text = $"PLAYER {playerNumber} CHOICE";
                playerChoiceText.color = c;
            }

            if (countdownTimerText != null)
            {
                countdownTimerText.color = c;
                if (!_useExternalTimer)
                {
                    countdownTimerText.text = Mathf.CeilToInt(selectionTimeLimit).ToString();
                }
            }
        }

        /// <summary>
        /// Fades the player choice text and countdown timer CanvasGroups to the target alpha.
        /// </summary>
        private IEnumerator FadePlayerChoiceUI(float targetAlpha, float duration)
        {
            float startAlphaChoice = _playerChoiceCG != null ? _playerChoiceCG.alpha : targetAlpha;
            float startAlphaTimer = _countdownTimerCG != null ? _countdownTimerCG.alpha : targetAlpha;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (_playerChoiceCG != null)
                    _playerChoiceCG.alpha = Mathf.Lerp(startAlphaChoice, targetAlpha, t);
                if (_countdownTimerCG != null)
                    _countdownTimerCG.alpha = Mathf.Lerp(startAlphaTimer, targetAlpha, t);

                yield return null;
            }

            if (_playerChoiceCG != null) _playerChoiceCG.alpha = targetAlpha;
            if (_countdownTimerCG != null) _countdownTimerCG.alpha = targetAlpha;
        }

        private IEnumerator RunCountdownTimer()
        {
            if (_useExternalTimer)
            {
                yield break;
            }

            float remaining = selectionTimeLimit;

            while (remaining > 0f)
            {
                if (countdownTimerText != null)
                    countdownTimerText.text = Mathf.CeilToInt(remaining).ToString();

                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (countdownTimerText != null)
                countdownTimerText.text = "0";

            // Time expired — auto-select the first available active card
            if (isShowing)
            {
                int autoIndex = GetFirstActiveCardIndex();
                Debug.Log($"[AugmentSelect] Time expired — auto-selecting augment index {autoIndex}");
                OnAugmentSelected(autoIndex);
            }

            _countdownCoroutine = null;
        }

        /// <summary>
        /// Returns the index of the first active (non-deactivated) card button in the current tier.
        /// Falls back to 0 if none found.
        /// </summary>
        private int GetFirstActiveCardIndex()
        {
            Button[] buttons = GetButtonsForTier(currentTier);
            if (buttons == null) return 0;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.activeSelf && buttons[i].interactable)
                    return i;
            }
            return 0;
        }

        // ===== CARD REACTIVATION =====

        /// <summary>
        /// Reactivates all card buttons across all tiers. Called on HideAugmentSelect
        /// so cards are ready for the next augment phase.
        /// </summary>
        private void ReactivateAllCards()
        {
            ReactivateButtons(tier1Buttons);
            ReactivateButtons(tier2Buttons);
            ReactivateButtons(tier3Buttons);
            ReactivateButtons(tier4Buttons);
        }

        private void ReactivateButtons(Button[] buttons)
        {
            if (buttons == null) return;
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.gameObject.SetActive(true);
                btn.interactable = true;
                btn.transform.localScale = Vector3.one;

                CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        // ===== INTERNAL HELPERS =====

        /// <summary>
        /// Hides all tier canvas groups.
        /// </summary>
        private void HideAllTiers()
        {
            SetCanvasGroupVisibility(tier1CanvasGroup, false);
            SetCanvasGroupVisibility(tier2CanvasGroup, false);
            SetCanvasGroupVisibility(tier3CanvasGroup, false);
            SetCanvasGroupVisibility(tier4CanvasGroup, false);
        }

        /// <summary>
        /// Selects a random tier based on the configured probabilities.
        /// </summary>
        private int SelectRandomTier()
        {
            if (_gameTierOrder.Count < 3)
                GenerateRandomizedGameTierOrder();

            if (_gameTierOrderIndex >= _gameTierOrder.Count)
            {
                Debug.LogWarning("[AugmentSelect] Tier order exhausted; generating a new randomized order.");
                GenerateRandomizedGameTierOrder();
            }

            int tier = _gameTierOrder[_gameTierOrderIndex];
            _gameTierOrderIndex++;
            return tier;

            // Old probability-based tier selection (kept for reference):
            // float roll = Random.Range(0f, 1f);
            //
            // if (roll < tier1Probability)
            // {
            //     return 1;
            // }
            // else if (roll < tier1Probability + tier2Probability)
            // {
            //     return 2;
            // }
            // else
            // {
            //     return 3;
            // }
        }

        /// <summary>
        /// Selects random augments from the specified tier without duplicates.
        /// Tier 4 is intentionally capped to a single Invasion-only card.
        /// </summary>
        private List<Augment> SelectRandomAugments(int tier, int count = 3)
        {
            if (tier == 4)
            {
                count = Mathf.Min(count, 1);
            }

            List<Augment> sourceList = tier switch
            {
                1 => tier1Augments,
                2 => tier2Augments,
                3 => tier3Augments,
                4 => tier4Augments,
                _ => tier1Augments
            };

            if (count <= 0)
            {
                return new List<Augment>();
            }

            if (sourceList.Count < count)
            {
                Debug.LogError($"Tier {tier} has fewer than {count} augments! Cannot populate augment select.");
                return new List<Augment>();
            }

            // Create copy to avoid modifying original list
            List<Augment> availableAugments = new List<Augment>(sourceList);
            List<Augment> selected = new List<Augment>(count);

            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, availableAugments.Count);
                selected.Add(availableAugments[randomIndex]);
                availableAugments.RemoveAt(randomIndex);
            }

            return selected;
        }

        /// <summary>
        /// Populates the UI elements with the selected augments.
        /// </summary>
        private void PopulateUI(int tier, List<Augment> augments)
        {
            if (augments == null || augments.Count == 0)
            {
                Debug.LogError("Not enough augments selected to populate UI!");
                return;
            }

            UpdateDisplayedOptionCount(tier, augments.Count);

            switch (tier)
            {
                case 1:
                    SetUIElements(tier1Choice1Icon, tier1Choice1Name, tier1Choice1Description, augments[0]);
                    SetOptionalUIElements(1, 1, augments, tier1Choice2Icon, tier1Choice2Name, tier1Choice2Description);
                    SetOptionalUIElements(1, 2, augments, tier1Choice3Icon, tier1Choice3Name, tier1Choice3Description);
                    break;

                case 2:
                    SetUIElements(tier2Choice1Icon, tier2Choice1Name, tier2Choice1Description, augments[0]);
                    SetOptionalUIElements(2, 1, augments, tier2Choice2Icon, tier2Choice2Name, tier2Choice2Description);
                    SetOptionalUIElements(2, 2, augments, tier2Choice3Icon, tier2Choice3Name, tier2Choice3Description);
                    break;

                case 3:
                    SetUIElements(tier3Choice1Icon, tier3Choice1Name, tier3Choice1Description, augments[0]);
                    SetOptionalUIElements(3, 1, augments, tier3Choice2Icon, tier3Choice2Name, tier3Choice2Description);
                    SetOptionalUIElements(3, 2, augments, tier3Choice3Icon, tier3Choice3Name, tier3Choice3Description);
                    break;

                case 4:
                    SetUIElements(tier4Choice1Icon, tier4Choice1Name, tier4Choice1Description, augments[0]);
                    break;
            }

            ApplyPerCardVisualStyles(tier, augments.Count);

            // Re-enable all card buttons (may have been disabled from previous pick)
            ReEnableCardButtons(tier);
        }

        private void ApplyPerCardVisualStyles(int baseTier, int cardCount)
        {
            EnsureOriginalCardVisualsCaptured();
            for (int i = 0; i < cardCount; i++)
            {
                int visualTier = selectedAugmentVisualTiers != null && i < selectedAugmentVisualTiers.Count
                    ? Mathf.Clamp(selectedAugmentVisualTiers[i], 1, 4)
                    : baseTier;
                int sourceIndex = visualTier == 4 ? 0 : i;
                ApplyCardVisualSnapshot(baseTier, i, GetOriginalCardVisualSnapshot(visualTier, sourceIndex));
            }
        }

        private int ResolveDisplayedTier(int requestedTier, IReadOnlyList<int> perCardVisualTiers)
        {
            int clampedTier = Mathf.Clamp(requestedTier, 1, 4);
            bool usesPerCardVisuals = perCardVisualTiers != null && perCardVisualTiers.Count > 0;
            if (clampedTier == 4 && usesPerCardVisuals)
            {
                // Tier 4 is a visual template for mixed Invasion offers. Use the Epic row
                // as the host so the tier 4 art replaces a card slot instead of overlaying the row.
                return 2;
            }

            return clampedTier;
        }

        private void EnsureOriginalCardVisualsCaptured()
        {
            if (_originalCardVisuals != null)
            {
                return;
            }

            _originalCardVisuals = new CardVisualSnapshot[5][];
            for (int tier = 1; tier <= 4; tier++)
            {
                int count = tier == 4 ? 1 : 3;
                _originalCardVisuals[tier] = new CardVisualSnapshot[count];
                for (int i = 0; i < count; i++)
                {
                    _originalCardVisuals[tier][i] = CaptureCardVisualSnapshot(tier, i);
                }
            }
        }

        private CardVisualSnapshot GetOriginalCardVisualSnapshot(int tier, int index)
        {
            EnsureOriginalCardVisualsCaptured();
            if (tier < 1 || tier >= _originalCardVisuals.Length || _originalCardVisuals[tier] == null || _originalCardVisuals[tier].Length == 0)
            {
                tier = 1;
                index = 0;
            }

            index = Mathf.Clamp(index, 0, _originalCardVisuals[tier].Length - 1);
            return _originalCardVisuals[tier][index];
        }

        private CardVisualSnapshot CaptureCardVisualSnapshot(int tier, int index)
        {
            return new CardVisualSnapshot
            {
                container = CaptureImageVisual(GetContainerForCard(tier, index)),
                innerContainer = CaptureImageVisual(GetInnerContainerForCard(tier, index)),
                icon = CaptureImageVisual(GetIconForCard(tier, index)),
                name = CaptureTextVisual(GetNameForCard(tier, index)),
                description = CaptureTextVisual(GetDescriptionForCard(tier, index))
            };
        }

        private void ApplyCardVisualSnapshot(int tier, int index, CardVisualSnapshot snapshot)
        {
            ApplyImageVisual(GetContainerForCard(tier, index), snapshot.container);
            ApplyImageVisual(GetInnerContainerForCard(tier, index), snapshot.innerContainer);
            ApplyImageVisual(GetIconForCard(tier, index), snapshot.icon);
            ApplyTextVisual(GetNameForCard(tier, index), snapshot.name);
            ApplyTextVisual(GetDescriptionForCard(tier, index), snapshot.description);
        }

        private static ImageVisualSnapshot CaptureImageVisual(Image image)
        {
            if (image == null)
            {
                return default;
            }

            return new ImageVisualSnapshot
            {
                captured = true,
                color = image.color,
                material = image.material,
                type = image.type,
                preserveAspect = image.preserveAspect
            };
        }

        private static void ApplyImageVisual(Image image, ImageVisualSnapshot snapshot)
        {
            if (image == null || !snapshot.captured)
            {
                return;
            }

            image.color = snapshot.color;
            image.material = snapshot.material;
            image.type = snapshot.type;
            image.preserveAspect = snapshot.preserveAspect;
        }

        private static TextVisualSnapshot CaptureTextVisual(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return default;
            }

            return new TextVisualSnapshot
            {
                captured = true,
                color = text.color,
                material = text.fontSharedMaterial,
                font = text.font,
                fontStyle = text.fontStyle,
                alignment = text.alignment
            };
        }

        private static void ApplyTextVisual(TextMeshProUGUI text, TextVisualSnapshot snapshot)
        {
            if (text == null || !snapshot.captured)
            {
                return;
            }

            text.color = snapshot.color;
            text.fontSharedMaterial = snapshot.material;
            text.font = snapshot.font;
            text.fontStyle = snapshot.fontStyle;
            text.alignment = snapshot.alignment;
        }

        private void SetOptionalUIElements(int tier, int choiceIndex, List<Augment> augments, Image icon, TextMeshProUGUI nameText, TextMeshProUGUI descriptionText)
        {
            bool isActive = augments.Count > choiceIndex;
            SetCardActive(tier, choiceIndex, isActive);

            if (isActive)
            {
                SetUIElements(icon, nameText, descriptionText, augments[choiceIndex]);
                return;
            }

            if (icon != null)
            {
                icon.sprite = null;
            }

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }
        }

        private void UpdateDisplayedOptionCount(int tier, int optionCount)
        {
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                SetCardActive(tier, i, i < optionCount);
            }
        }

        private void SetCardActive(int tier, int index, bool isActive)
        {
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons == null || index < 0 || index >= buttons.Length || buttons[index] == null)
            {
                return;
            }

            buttons[index].gameObject.SetActive(isActive);
        }

        /// <summary>
        /// Re-enables all card buttons for the given tier.
        /// </summary>
        private void ReEnableCardButtons(int tier)
        {
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons == null) return;

            foreach (var btn in buttons)
            {
                if (btn != null)
                {
                    btn.interactable = true;
                    // Reset alpha in case it was dimmed
                    CanvasGroup cg = btn.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
            }
        }

        /// <summary>
        /// Sets the UI elements (icon, name, description) for a single augment choice.
        /// </summary>
        private void SetUIElements(Image icon, TextMeshProUGUI nameText, TextMeshProUGUI descriptionText, Augment augment)
        {
            if (icon != null)
            {
                icon.sprite = augment.icon;
            }

            if (nameText != null)
            {
                nameText.text = augment.augmentName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = augment.description;
            }
        }

        /// <summary>
        /// Shows the canvas group for the selected tier, hides others.
        /// Cards start at scale 0 and alpha 0 — the entrance animation is the first time they appear.
        /// </summary>
        private void ShowTierCanvas(int tier)
        {
            // Hide all canvas groups first
            HideAllTiers();

            // Show selected tier
            CanvasGroup targetGroup = GetCanvasGroupForTier(tier);

            if (targetGroup != null)
            {
                // Pre-hide all card children so they don't flash before animation
                int childCount = targetGroup.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    Transform child = targetGroup.transform.GetChild(i);
                    child.localScale = Vector3.one * startScale;

                    CanvasGroup childCG = child.GetComponent<CanvasGroup>();
                    if (childCG == null)
                        childCG = child.gameObject.AddComponent<CanvasGroup>();
                    childCG.alpha = 0f;
                }

                // Now activate the parent — cards are invisible (scale 0, alpha 0)
                targetGroup.gameObject.SetActive(true);
                targetGroup.alpha = 1f;
                targetGroup.interactable = false;
                targetGroup.blocksRaycasts = false;
            }
            else
            {
                Debug.LogError($"Tier {tier} canvas group is not assigned!");
            }
        }

        /// <summary>
        /// Returns the CanvasGroup for the given tier.
        /// </summary>
        private CanvasGroup GetCanvasGroupForTier(int tier)
        {
            return tier switch
            {
                1 => tier1CanvasGroup,
                2 => tier2CanvasGroup,
                3 => tier3CanvasGroup,
                4 => tier4CanvasGroup,
                _ => tier1CanvasGroup
            };
        }

        /// <summary>
        /// Sets a canvas group's visibility, interactability, and raycast blocking.
        /// </summary>
        private void SetCanvasGroupVisibility(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (!visible)
            {
                group.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Animates the entrance of augment cards with staggered timing,
        /// and fades in the player choice / timer text.
        /// </summary>
        private IEnumerator AnimateEntrance(int tier)
        {
            CanvasGroup targetGroup = GetCanvasGroupForTier(tier);

            if (targetGroup == null)
            {
                Debug.LogError("Target canvas group is null!");
                yield break;
            }

            targetGroup.alpha = 1f;
            targetGroup.interactable = false;
            targetGroup.blocksRaycasts = false;

            // Fade in player choice + timer text alongside card animation
            StartCoroutine(FadePlayerChoiceUI(1f, playerChoiceFadeDuration));

            // Get the three card transforms (first 3 children)
            Transform[] cardTransforms = new Transform[3];
            int childCount = Mathf.Min(3, targetGroup.transform.childCount);

            for (int i = 0; i < childCount; i++)
            {
                cardTransforms[i] = targetGroup.transform.GetChild(i);
            }

            // Animate each card with staggered timing
            for (int i = 0; i < cardTransforms.Length; i++)
            {
                if (cardTransforms[i] != null)
                {
                    StartCoroutine(AnimateCard(cardTransforms[i]));
                }
                yield return new WaitForSecondsRealtime(cardAnimationDelay);
            }

            // Wait for last animation to finish
            yield return new WaitForSecondsRealtime(animationDuration);

            // Enable interactability
            targetGroup.interactable = true;
            targetGroup.blocksRaycasts = true;

            // Cache original scales for hover system
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons != null)
            {
                foreach (var btn in buttons)
                {
                    if (btn != null)
                        _cardOriginalScales[btn] = btn.transform.localScale;
                }
            }

            // Set default selection to center button (index 1)
            SetDefaultSelection(tier);
        }

        /// <summary>
        /// Animates a single card using scale and fade.
        /// </summary>
        private IEnumerator AnimateCard(Transform card)
        {
            CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
            if (cardGroup == null)
            {
                cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            }

            // Target scale is 1 (the card's normal size)
            Vector3 targetScale = Vector3.one;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                float curveValue = entranceCurve.Evaluate(t);

                // Scale animation
                card.localScale = Vector3.Lerp(Vector3.one * startScale, targetScale, curveValue);

                // Fade animation
                cardGroup.alpha = curveValue;

                yield return null;
            }

            // Ensure final state
            card.localScale = targetScale;
            cardGroup.alpha = 1f;
        }

        /// <summary>
        /// Sets the default EventSystem selection to the center button (index 1).
        /// </summary>
        private void SetDefaultSelection(int tier)
        {
            Button[] buttons = GetButtonsForTier(tier);
            int defaultIndex = -1;
            if (buttons != null && buttons.Length > 1 && buttons[1] != null && buttons[1].gameObject.activeSelf && buttons[1].interactable)
            {
                defaultIndex = 1;
            }

            if (defaultIndex < 0 && buttons != null)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button != null && button.gameObject.activeSelf && button.interactable)
                    {
                        defaultIndex = i;
                        break;
                    }
                }
            }

            if (defaultIndex >= 0)
            {
                LogSelectionDebug($"SetDefaultSelection chose index {defaultIndex}");
                SetSelectedCardIndex(defaultIndex);
            }
        }

        /// <summary>
        /// Sets the default selection to the first remaining active button.
        /// Used after the first picker's card has been deactivated.
        /// </summary>
        private void SetDefaultSelectionFirstActive(int tier)
        {
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn != null && btn.gameObject.activeSelf && btn.interactable)
                {
                    LogSelectionDebug($"SetDefaultSelectionFirstActive chose index {i}", btn, i);
                    SetSelectedCardIndex(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns the button array for the given tier.
        /// </summary>
        private Button[] GetButtonsForTier(int tier)
        {
            return tier switch
            {
                1 => tier1Buttons,
                2 => tier2Buttons,
                3 => tier3Buttons,
                4 => tier4Buttons,
                _ => tier1Buttons
            };
        }

        /// <summary>
        /// Called when an augment is selected (via button click or controller confirm).
        /// Plays selection effect on the chosen card, then notifies SceneManager.
        /// Does NOT hide the screen — SceneManager controls flow.
        /// </summary>
        public void OnAugmentSelected(int choiceIndex)
        {
            if (!isShowing) return;

            if (choiceIndex < 0 || choiceIndex >= selectedAugments.Count)
            {
                Debug.LogError($"Invalid augment choice index: {choiceIndex}");
                return;
            }

            Augment selectedAugment = selectedAugments[choiceIndex];
            _selectedCardIndex = choiceIndex;
            LogSelectionDebug($"OnAugmentSelected choiceIndex={choiceIndex} augment={selectedAugment.augmentName}");
            Debug.Log($"Player {currentPickingPlayer} selected augment: {selectedAugment.augmentName}");

            // Stop countdown
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            // Disable interactivity so no double-picks during transition
            CanvasGroup tierCG = GetCanvasGroupForTier(currentTier);
            if (tierCG != null)
            {
                tierCG.interactable = false;
                tierCG.blocksRaycasts = false;
            }
            SetTierButtonsInteractable(currentTier, false);

            // Notify listeners (SceneManager) of the selection
            // SceneManager will call TransitionToSecondPicker() or HideAugmentSelect()
            onAugmentChosen?.Invoke(selectedAugment, choiceIndex);
        }

        private void SetTierButtonsInteractable(int tier, bool interactable)
        {
            Button[] buttons = GetButtonsForTier(tier);
            if (buttons == null) return;

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeSelf) continue;
                btn.interactable = interactable;
            }
        }

        private void ApplySelectedMaterial(Image image, Dictionary<Image, Material> originalLookup)
        {
            if (image == null) return;

            if (!originalLookup.ContainsKey(image))
                originalLookup[image] = image.material;

            image.material = selectedMaterial;
        }

        private void LogSelectionDebug(string message, Button button = null, int? buttonIndex = null)
        {
            if (!debugLogSelectionFocus)
            {
                return;
            }

            string buttonName = button != null ? button.gameObject.name : "null";
            string selectedObjectName = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "null";
            Debug.Log($"[AugmentSelect][Focus] tier={currentTier} picker={currentPickingPlayer} selectedIndex={_selectedCardIndex} buttonIndex={(buttonIndex.HasValue ? buttonIndex.Value.ToString() : "n/a")} button={buttonName} eventSelected={selectedObjectName} :: {message}", this);
        }
    }
}
