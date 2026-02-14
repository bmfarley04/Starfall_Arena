using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the ship selection screen: navigation between ships, UI population,
/// and ship model spawning. Controller-first design with gamepad navigation.
/// </summary>
public class ShipSelectManager : MonoBehaviour
{
    [System.Serializable]
    public struct StatBarReferences
    {
        [Tooltip("Damage stat fill image (RectTransform 'right' component will be modified)")]
        public Image damageFill;

        [Tooltip("Damage stat value text")]
        public TextMeshProUGUI damageText;

        [Tooltip("Hull stat fill image (RectTransform 'right' component will be modified)")]
        public Image hullFill;

        [Tooltip("Hull stat value text")]
        public TextMeshProUGUI hullText;

        [Tooltip("Shield stat fill image (RectTransform 'right' component will be modified)")]
        public Image shieldFill;

        [Tooltip("Shield stat value text")]
        public TextMeshProUGUI shieldText;

        [Tooltip("Speed stat fill image (RectTransform 'right' component will be modified)")]
        public Image speedFill;

        [Tooltip("Speed stat value text")]
        public TextMeshProUGUI speedText;
    }

    [System.Serializable]
    public struct AbilityButtonReferences
    {
        [Tooltip("Ability button circle (the transparent circle background)")]
        public Image circleButton;

        [Tooltip("Tooltip GameObject (shown on hover)")]
        public GameObject tooltip;

        [Tooltip("Tooltip title text")]
        public TextMeshProUGUI tooltipTitle;

        [Tooltip("Tooltip description text")]
        public TextMeshProUGUI tooltipDescription;
    }

    [System.Serializable]
    public struct AbilityIconReferences
    {
        [Tooltip("Ability 1 icons (one per ship, in order matching availableShips array)")]
        public Image[] ability1Icons;

        [Tooltip("Ability 2 icons (one per ship, in order matching availableShips array)")]
        public Image[] ability2Icons;

        [Tooltip("Ability 3 icons (one per ship, in order matching availableShips array)")]
        public Image[] ability3Icons;

        [Tooltip("Ability 4 icons (one per ship, in order matching availableShips array)")]
        public Image[] ability4Icons;
    }

    [System.Serializable]
    public struct StatMaxValues
    {
        [Tooltip("Maximum damage value for stat bar scaling (typically 50)")]
        public float maxDamage;

        [Tooltip("Maximum hull value for stat bar scaling (typically 500)")]
        public float maxHull;

        [Tooltip("Maximum shield value for stat bar scaling (typically 500)")]
        public float maxShield;

        [Tooltip("Maximum speed value for stat bar scaling (typically 100)")]
        public float maxSpeed;
    }

    [System.Serializable]
    public struct TextSizeConfig
    {
        [Tooltip("Font size for stat values (e.g., 'DAMAGE: 10')")]
        public float statTextSize;

        [Tooltip("Font size for ship name")]
        public float shipNameSize;

        [Tooltip("Font size for tooltip titles")]
        public float tooltipTitleSize;

        [Tooltip("Font size for tooltip descriptions")]
        public float tooltipDescriptionSize;
    }

    [System.Serializable]
    public struct ShipModelConfig
    {
        [Tooltip("Parent transform where ship models are spawned")]
        public Transform shipModelParent;

        [Tooltip("World position where ship model is displayed")]
        public Vector3 displayPosition;

        [Tooltip("Rotation of displayed ship model")]
        public Vector3 displayRotation;

        [Tooltip("Scale of displayed ship model")]
        public float displayScale;
    }

    [System.Serializable]
    public struct NavigationConfig
    {
        [Tooltip("Delay before input is accepted again after navigation (prevents rapid scrolling)")]
        public float navigationCooldown;

        [Tooltip("Sound played when navigating between ships")]
        public SoundEffect navigationSound;

        [Tooltip("Sound played when confirming ship selection")]
        public SoundEffect confirmSound;
    }

    [System.Serializable]
    public struct AbilityHoverConfig
    {
        [Header("Icon Materials")]
        [Tooltip("Normal material for ability icons (when not hovered)")]
        public Material iconNormalMaterial;

        [Tooltip("Hover material for ability icons (when selected/hovered)")]
        public Material iconHoverMaterial;

        [Header("Circle Outline Materials")]
        [Tooltip("Normal material for circle outline (when not hovered)")]
        public Material circleNormalMaterial;

        [Tooltip("Hover material for circle outline (when selected/hovered)")]
        public Material circleHoverMaterial;

        [Header("Sound Effects")]
        [Tooltip("Sound played when hovering over ability buttons (D-pad navigation)")]
        public SoundEffect hoverSound;
    }

    [System.Serializable]
    public struct NavigationButtonEffects
    {
        [Header("Button Materials")]
        [Tooltip("Normal material for navigation buttons")]
        public Material buttonNormalMaterial;

        [Tooltip("Material briefly applied when button is pressed")]
        public Material buttonPressedMaterial;

        [Header("Flash Timing")]
        [Tooltip("Duration in seconds the pressed material is shown")]
        [Range(0.05f, 0.5f)]
        public float flashDuration;
    }

    [System.Serializable]
    public struct SelectionUIReferences
    {
        [Tooltip("Text showing which player is selecting (e.g., 'PLAYER 1' or 'PLAYER 2')")]
        public TextMeshProUGUI playerSelectionText;

        [Tooltip("Back button radial fill image")]
        public Image backButtonFill;

        [Tooltip("Select button radial fill image")]
        public Image selectButtonFill;
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

    [System.Serializable]
    public struct PostSelectionConfig
    {
        [Tooltip("Delay after confirmation before spin animation starts")]
        public float confirmationDelay;

        [Tooltip("Duration of 360° spin animation")]
        public float spinDuration;

        [Tooltip("Delay after spin before transitioning")]
        public float postSpinDelay;

        [Header("Player Transition Slide")]
        [Tooltip("Duration of slide-out animation (P1 exits left)")]
        public float slideOutDuration;

        [Tooltip("Duration of slide-in animation (P2 enters from right)")]
        public float slideInDuration;

        [Tooltip("Slide ship model along with UI")]
        public bool slideShipWithUI;

        [Tooltip("Name of gameplay scene to load after Player 2 selects")]
        public string gameplaySceneName;
    }

    public enum PlayerSelectState
    {
        Player1,
        Player2
    }

    [System.Serializable]
    public struct ShipRotationConfig
    {
        [Header("Rotation Sensitivity")]
        [Tooltip("Sensitivity for Y-axis rotation (left/right spin)")]
        public float yawSensitivity;

        [Tooltip("Sensitivity for X-axis rotation (up/down tilt)")]
        public float pitchSensitivity;

        [Tooltip("Sensitivity for Z-axis rotation (roll)")]
        public float rollSensitivity;

        [Header("Rotation Smoothing")]
        [Tooltip("How smoothly the ship rotates (lower = smoother)")]
        [Range(0.01f, 0.5f)]
        public float rotationSmoothing;

        [Header("Input Deadzone")]
        [Tooltip("Minimum stick input to register (prevents drift)")]
        [Range(0f, 0.3f)]
        public float inputDeadzone;
    }

    [Header("UI Container")]
    [Tooltip("RectTransform container that holds all UI elements (will slide during P1→P2 transition)")]
    [SerializeField] private RectTransform uiContainer;

    [Header("Ship Data")]
    [Tooltip("List of all available ships")]
    [SerializeField] private ShipData[] availableShips;

    [Header("UI References")]
    [Tooltip("Ship name text at top of screen")]
    [SerializeField] private TextMeshProUGUI shipNameText;

    [Tooltip("Stat bar fill images")]
    [SerializeField] private StatBarReferences statBars;

    [Tooltip("Ability button references (in order: 1-4)")]
    [SerializeField] private AbilityButtonReferences ability1;
    [SerializeField] private AbilityButtonReferences ability2;
    [SerializeField] private AbilityButtonReferences ability3;
    [SerializeField] private AbilityButtonReferences ability4;

    [Header("Ability Icons")]
    [Tooltip("Pre-placed ability icon images (one per ship per ability). Arrays should match length of availableShips.")]
    [SerializeField] private AbilityIconReferences abilityIcons;

    [Header("Selection UI")]
    [Tooltip("Player selection UI references (player text, back/select button fills)")]
    [SerializeField] private SelectionUIReferences selectionUI;

    [Header("Navigation Button Images")]
    [Tooltip("Left navigation button image (for material flash effect)")]
    [SerializeField] private Image leftNavigationImage;

    [Tooltip("Right navigation button image (for material flash effect)")]
    [SerializeField] private Image rightNavigationImage;

    [Tooltip("Default selected button for controller navigation (e.g., first ability button)")]
    [SerializeField] private GameObject defaultSelectedButton;

    [Header("Ship Model")]
    [Tooltip("Configuration for spawned ship model")]
    [SerializeField] private ShipModelConfig shipModel;

    [Header("Stat Scaling")]
    [Tooltip("Maximum values for each stat category (for bar fill percentage)")]
    [SerializeField] private StatMaxValues statMaxValues;

    [Header("Navigation")]
    [Tooltip("Navigation settings and audio")]
    [SerializeField] private NavigationConfig navigation;

    [Header("Text Sizes")]
    [Tooltip("Font size configuration for UI text elements")]
    [SerializeField] private TextSizeConfig textSizes;

    [Header("Ship Rotation")]
    [Tooltip("Ship rotation controls and sensitivity")]
    [SerializeField] private ShipRotationConfig shipRotation;

    [Header("Hover & Press Effects")]
    [Tooltip("Ability button hover effects (icon materials and circle colors)")]
    [SerializeField] private AbilityHoverConfig abilityHover;

    [Tooltip("Navigation button press effects (material flash on press)")]
    [SerializeField] private NavigationButtonEffects navigationEffects;

    [Header("Player Selection")]
    [Tooltip("Hold button configuration for back and select buttons")]
    [SerializeField] private HoldButtonConfig holdBack;

    [SerializeField] private HoldButtonConfig holdSelect;

    [Header("Post-Selection Animation")]
    [Tooltip("Animation and transition configuration after ship selection")]
    [SerializeField] private PostSelectionConfig postSelection;

    private int _currentShipIndex = 0;
    private GameObject[] _shipModelInstances;
    private AudioSource _audioSource;
    private float _lastNavigationTime = 0f;
    private Quaternion _targetRotation;
    private Quaternion _currentRotation;
    private bool _isPreloaded = false;
    private InputSystemUIInputModule _uiInputModule;
    private bool _wasNavigationEnabled;
    private int _lastHoveredAbilityIndex = -1; // -1 = none, 0-3 = ability 1-4
    private Class1PreviewController _currentPreviewController;

    // Player selection state
    private PlayerSelectState _currentPlayer = PlayerSelectState.Player1;
    private ShipData _player1Selection;
    private ShipData _player2Selection;

    // Per-player gamepad ownership (only active player's gamepad can control ship select)
    private Gamepad _player1Gamepad;
    private Gamepad _player2Gamepad;
    private Gamepad _activeGamepad;

    // Hold button state
    private float _backHoldTime = 0f;
    private float _selectHoldTime = 0f;
    private bool _isProcessingSelection = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        // Hide all tooltips initially
        HideAllTooltips();

        // Ensure all ability icons start disabled
        DisableAllAbilityIcons();

        // Initialize button fills to 1 (full) - they drain to 0 as you hold
        if (selectionUI.backButtonFill != null)
            selectionUI.backButtonFill.fillAmount = 1f;
        if (selectionUI.selectButtonFill != null)
            selectionUI.selectButtonFill.fillAmount = 1f;

        // Ships are now spawned by TitleScreenManager at scene load
        // This keeps ShipSelectManager completely independent
    }

    /// <summary>
    /// Optional external assignment for deterministic controller ownership.
    /// If not called, gamepads are auto-assigned from connected devices.
    /// </summary>
    public void AssignGamepads(Gamepad player1Pad, Gamepad player2Pad)
    {
        _player1Gamepad = player1Pad;
        _player2Gamepad = player2Pad;

        if (_player2Gamepad == null)
            _player2Gamepad = _player1Gamepad;

        SetActiveGamepadForCurrentPlayer();
    }

    private void ResolveGamepadAssignments()
    {
        if (_player1Gamepad == null || !_player1Gamepad.added)
        {
            _player1Gamepad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        }

        if (_player2Gamepad == null || !_player2Gamepad.added)
        {
            _player2Gamepad = Gamepad.all.Count > 1 ? Gamepad.all[1] : null;
        }

        if (_player2Gamepad == null)
            _player2Gamepad = _player1Gamepad;

        SetActiveGamepadForCurrentPlayer();
    }

    private void SetActiveGamepadForCurrentPlayer()
    {
        _activeGamepad = _currentPlayer == PlayerSelectState.Player1
            ? _player1Gamepad
            : _player2Gamepad;

        if (_activeGamepad == null && Gamepad.all.Count > 0)
            _activeGamepad = Gamepad.all[0];
    }

    private void OnValidate()
    {
        // Validate that icon arrays match ship count
        if (availableShips != null && availableShips.Length > 0)
        {
            int expectedCount = availableShips.Length;

            ValidateIconArray(abilityIcons.ability1Icons, "Ability 1", expectedCount);
            ValidateIconArray(abilityIcons.ability2Icons, "Ability 2", expectedCount);
            ValidateIconArray(abilityIcons.ability3Icons, "Ability 3", expectedCount);
            ValidateIconArray(abilityIcons.ability4Icons, "Ability 4", expectedCount);
        }

        // Validate circle materials
        if (abilityHover.circleNormalMaterial == null)
        {
            Debug.LogWarning("ShipSelectManager: Circle Normal Material is not assigned. Circles may not display correctly.");
        }

        if (abilityHover.circleHoverMaterial == null)
        {
            Debug.LogWarning("ShipSelectManager: Circle Hover Material is not assigned. Hover effect won't work.");
        }

        // Validate selection UI references
        if (selectionUI.backButtonFill == null)
        {
            Debug.LogWarning("ShipSelectManager: Back button fill image is not assigned.");
        }

        if (selectionUI.selectButtonFill == null)
        {
            Debug.LogWarning("ShipSelectManager: Select button fill image is not assigned.");
        }

        if (selectionUI.playerSelectionText == null)
        {
            Debug.LogWarning("ShipSelectManager: Player selection text is not assigned.");
        }

        // Validate navigation button images
        if (leftNavigationImage == null)
        {
            Debug.LogWarning("ShipSelectManager: Left navigation image is not assigned. Material flash won't work.");
        }

        if (rightNavigationImage == null)
        {
            Debug.LogWarning("ShipSelectManager: Right navigation image is not assigned. Material flash won't work.");
        }
    }

    private void ValidateIconArray(Image[] iconArray, string abilityName, int expectedCount)
    {
        if (iconArray == null || iconArray.Length == 0)
        {
            Debug.LogWarning($"ShipSelectManager: {abilityName} icons array is empty! Expected {expectedCount} icons (one per ship).");
            return;
        }

        if (iconArray.Length != expectedCount)
        {
            Debug.LogWarning($"ShipSelectManager: {abilityName} icons array has {iconArray.Length} entries, but there are {expectedCount} ships. These should match!");
        }

        // Check for null entries
        for (int i = 0; i < iconArray.Length; i++)
        {
            if (iconArray[i] == null)
            {
                Debug.LogWarning($"ShipSelectManager: {abilityName} icons array has null entry at index {i}!");
            }
        }
    }

    /// <summary>
    /// Disable all ability icons at startup.
    /// </summary>
    private void DisableAllAbilityIcons()
    {
        DisableIconArray(abilityIcons.ability1Icons);
        DisableIconArray(abilityIcons.ability2Icons);
        DisableIconArray(abilityIcons.ability3Icons);
        DisableIconArray(abilityIcons.ability4Icons);
    }

    private void DisableIconArray(Image[] iconArray)
    {
        if (iconArray == null) return;

        foreach (var icon in iconArray)
        {
            if (icon != null)
                icon.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        Debug.Log("[OnEnable] ShipSelectManager enabled!");

        // Disable EventSystem's automatic navigation (stick input) - we handle navigation manually
        DisableEventSystemNavigation();

        // Ensure gamepads are assigned before input processing starts
        ResolveGamepadAssignments();

        // Update player selection text
        UpdatePlayerSelectionText();

        // If not preloaded, load UI data first
        if (!_isPreloaded)
        {
            Debug.Log("[OnEnable] Not preloaded, loading ship data...");
            LoadShipDataOnly(_currentShipIndex);
            // Will show ship when TitleScreenManager calls ActivateShipWhenVisible
        }
        // If preloaded, ship will be activated by TitleScreenManager when canvas is visible

        HideAllTooltips(); // Ensure tooltips are hidden when screen opens

        // CRITICAL: Start with NO button selected (ship rotation mode)
        // Do this on next frame to ensure EventSystem doesn't auto-select
        StartCoroutine(ClearSelectionNextFrame());

        Debug.Log("[OnEnable] Complete (ship will activate when canvas is visible)!");
    }

    /// <summary>
    /// PUBLIC: Called by TitleScreenManager when canvas is fully visible.
    /// Activates the ship model so it appears at the right time.
    /// </summary>
    public void ActivateShipWhenVisible()
    {
        Debug.Log($"[ActivateShipWhenVisible] Activating ship {_currentShipIndex} NOW!");
        ShowShipModel(_currentShipIndex);
        _isPreloaded = false; // Reset flag
    }

    /// <summary>
    /// PUBLIC: Reset to Player 1 selection state (called when entering ship select from main menu).
    /// </summary>
    public void ResetToPlayer1()
    {
        _currentPlayer = PlayerSelectState.Player1;
        _player1Selection = null;
        _player2Selection = null;
        _currentShipIndex = 0;
        _backHoldTime = 0f;
        _selectHoldTime = 0f;
        _isProcessingSelection = false;

        if (selectionUI.backButtonFill != null)
            selectionUI.backButtonFill.fillAmount = 1f;
        if (selectionUI.selectButtonFill != null)
            selectionUI.selectButtonFill.fillAmount = 1f;

        SetActiveGamepadForCurrentPlayer();
        UpdatePlayerSelectionText();
    }

    private IEnumerator ClearSelectionNextFrame()
    {
        yield return null; // Wait one frame
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDisable()
    {
        Debug.Log("[OnDisable] ShipSelectManager disabled!");

        // Stop active preview before leaving ship select
        if (_currentPreviewController != null)
        {
            _currentPreviewController.StopPreview();
            _currentPreviewController = null;
        }

        // Re-enable EventSystem's automatic navigation
        RestoreEventSystemNavigation();

        HideAllShipModels();
        HideAllTooltips();

        // Clear EventSystem selection when leaving ship select
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Disable EventSystem's automatic navigation so sticks don't control UI.
    /// We handle D-pad navigation manually.
    /// </summary>
    private void DisableEventSystemNavigation()
    {
        if (EventSystem.current == null) return;

        _uiInputModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
        if (_uiInputModule != null)
        {
            Debug.Log("[DisableEventSystemNavigation] Disabling automatic navigation");
            // Store original state
            // Disable move action so EventSystem doesn't respond to stick/D-pad automatically
            if (_uiInputModule.move != null)
            {
                _uiInputModule.move.action.Disable();
            }
        }
    }

    /// <summary>
    /// Re-enable EventSystem's automatic navigation when leaving ship select.
    /// </summary>
    private void RestoreEventSystemNavigation()
    {
        if (_uiInputModule != null && _uiInputModule.move != null)
        {
            Debug.Log("[RestoreEventSystemNavigation] Re-enabling automatic navigation");
            _uiInputModule.move.action.Enable();
        }
    }

    private void Update()
    {
        if (_activeGamepad == null || !_activeGamepad.added)
            ResolveGamepadAssignments();

        // Don't process input during selection animation
        if (_isProcessingSelection)
        {
            UpdateShipRotation();
            return;
        }

        // STICKS ALWAYS rotate ship (separated from D-pad navigation)
        HandleShipRotation();

        // D-pad navigates UI (EventSystem handles automatically)
        HandleDPadNavigation();

        // Shoulder buttons switch ships
        HandleShipNavigation();

        // Hold button logic for back and select
        HandleHoldButtons();

        // Smooth rotate the ship model
        UpdateShipRotation();

        // Update ability hover effects based on selection
        UpdateAbilityHoverEffects();
    }

    /// <summary>
    /// Handle ship rotation with controller sticks.
    /// STICKS ALWAYS rotate ship, regardless of UI selection.
    /// </summary>
    private void HandleShipRotation()
    {
        // Skip stick rotation when preview controller has lock
        if (_currentPreviewController != null && _currentPreviewController.IsRotationLocked)
            return;

        if (_activeGamepad == null) return;

        // Read stick input
        Vector2 leftStick = _activeGamepad.leftStick.ReadValue();
        Vector2 rightStick = _activeGamepad.rightStick.ReadValue();

        // Apply deadzone
        if (leftStick.magnitude < shipRotation.inputDeadzone)
            leftStick = Vector2.zero;
        if (rightStick.magnitude < shipRotation.inputDeadzone)
            rightStick = Vector2.zero;

        // Calculate rotation delta (INTUITIVE: stick right = rotate right)
        float yaw = leftStick.x * shipRotation.yawSensitivity;    // Left stick X = spin left/right
        float pitch = -leftStick.y * shipRotation.pitchSensitivity; // Left stick Y = tilt up/down (inverted)
        float roll = rightStick.x * shipRotation.rollSensitivity;  // Right stick X = roll left/right

        // Apply rotation (Time.unscaledDeltaTime for frame-independent rotation)
        _targetRotation *= Quaternion.Euler(
            pitch * Time.unscaledDeltaTime * 60f,
            yaw * Time.unscaledDeltaTime * 60f,
            roll * Time.unscaledDeltaTime * 60f
        );
    }

    /// <summary>
    /// Smoothly interpolate ship rotation.
    /// </summary>
    private void UpdateShipRotation()
    {
        if (_shipModelInstances == null || _currentShipIndex >= _shipModelInstances.Length)
            return;

        GameObject currentShip = _shipModelInstances[_currentShipIndex];
        if (currentShip != null && currentShip.activeSelf)
        {
            // When preview controller is driving rotation, sync our tracking to prevent snap
            if (_currentPreviewController != null && _currentPreviewController.IsRotationLocked)
            {
                _currentRotation = currentShip.transform.rotation;
                _targetRotation = _currentRotation;
                return;
            }

            _currentRotation = Quaternion.Slerp(_currentRotation, _targetRotation, shipRotation.rotationSmoothing);
            currentShip.transform.rotation = _currentRotation;
        }
    }

    /// <summary>
    /// Update ability hover effects based on currently selected button.
    /// Changes icon materials and circle outline colors.
    /// </summary>
    private void UpdateAbilityHoverEffects()
    {
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        int currentHoveredIndex = GetAbilityIndexFromButton(selected);

        // Only update if hover state changed
        if (currentHoveredIndex != _lastHoveredAbilityIndex)
        {
            // Revert previous ability to normal state
            if (_lastHoveredAbilityIndex >= 0)
                ApplyAbilityNormalState(_lastHoveredAbilityIndex);

            // Apply hover state to newly selected ability
            if (currentHoveredIndex >= 0)
                ApplyAbilityHoverState(currentHoveredIndex);

            _lastHoveredAbilityIndex = currentHoveredIndex;

            // Notify preview controller of hover change
            NotifyPreviewController(currentHoveredIndex);
        }
    }

    /// <summary>
    /// Notify the current ship's preview controller of ability hover changes.
    /// </summary>
    private void NotifyPreviewController(int abilityIndex)
    {
        // Get/cache preview controller from current ship
        if (_shipModelInstances != null && _currentShipIndex >= 0 && _currentShipIndex < _shipModelInstances.Length)
        {
            GameObject currentShip = _shipModelInstances[_currentShipIndex];
            if (currentShip != null)
                _currentPreviewController = currentShip.GetComponent<Class1PreviewController>();
            else
                _currentPreviewController = null;
        }

        if (_currentPreviewController == null)
            return;

        if (abilityIndex >= 0)
            _currentPreviewController.StartPreview(abilityIndex);
        else
            _currentPreviewController.StopPreview();
    }

    /// <summary>
    /// Get ability index (0-3) from a GameObject, or -1 if not an ability button.
    /// </summary>
    private int GetAbilityIndexFromButton(GameObject button)
    {
        if (button == null) return -1;

        if (ability1.circleButton != null && button == ability1.circleButton.gameObject) return 0;
        if (ability2.circleButton != null && button == ability2.circleButton.gameObject) return 1;
        if (ability3.circleButton != null && button == ability3.circleButton.gameObject) return 2;
        if (ability4.circleButton != null && button == ability4.circleButton.gameObject) return 3;

        return -1;
    }

    /// <summary>
    /// Apply hover state to an ability: hover material on icon and circle.
    /// </summary>
    private void ApplyAbilityHoverState(int abilityIndex)
    {
        AbilityButtonReferences abilityRef = GetAbilityReference(abilityIndex);
        Image[] iconArray = GetAbilityIconArray(abilityIndex);

        // Update icon material (only the currently active icon for this ship)
        if (iconArray != null && _currentShipIndex >= 0 && _currentShipIndex < iconArray.Length)
        {
            Image currentIcon = iconArray[_currentShipIndex];
            if (currentIcon != null && abilityHover.iconHoverMaterial != null)
                currentIcon.material = abilityHover.iconHoverMaterial;
        }

        // Update circle outline material
        if (abilityRef.circleButton != null && abilityHover.circleHoverMaterial != null)
        {
            abilityRef.circleButton.material = abilityHover.circleHoverMaterial;
        }
    }

    /// <summary>
    /// Apply normal state to an ability: normal material on icon and circle.
    /// </summary>
    private void ApplyAbilityNormalState(int abilityIndex)
    {
        AbilityButtonReferences abilityRef = GetAbilityReference(abilityIndex);
        Image[] iconArray = GetAbilityIconArray(abilityIndex);

        // Update icon material (only the currently active icon for this ship)
        if (iconArray != null && _currentShipIndex >= 0 && _currentShipIndex < iconArray.Length)
        {
            Image currentIcon = iconArray[_currentShipIndex];
            if (currentIcon != null && abilityHover.iconNormalMaterial != null)
                currentIcon.material = abilityHover.iconNormalMaterial;
        }

        // Update circle outline material
        if (abilityRef.circleButton != null && abilityHover.circleNormalMaterial != null)
        {
            abilityRef.circleButton.material = abilityHover.circleNormalMaterial;
        }
    }

    /// <summary>
    /// Get AbilityButtonReferences by index (0-3).
    /// </summary>
    private AbilityButtonReferences GetAbilityReference(int index)
    {
        switch (index)
        {
            case 0: return ability1;
            case 1: return ability2;
            case 2: return ability3;
            case 3: return ability4;
            default: return default;
        }
    }

    /// <summary>
    /// Get icon array for a specific ability by index (0-3).
    /// </summary>
    private Image[] GetAbilityIconArray(int index)
    {
        switch (index)
        {
            case 0: return abilityIcons.ability1Icons;
            case 1: return abilityIcons.ability2Icons;
            case 2: return abilityIcons.ability3Icons;
            case 3: return abilityIcons.ability4Icons;
            default: return null;
        }
    }

    /// <summary>
    /// Handle D-pad navigation for UI.
    /// Manually handles ALL D-pad directions to prevent stick input from controlling UI.
    /// STICKS ONLY rotate ship, D-PAD ONLY navigates UI.
    /// </summary>
    private void HandleDPadNavigation()
    {
        if (_activeGamepad == null || EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        // D-pad DOWN
        if (_activeGamepad.dpad.down.wasPressedThisFrame)
        {
            if (selected == null)
            {
                // Nothing selected - select first ability (no sound on initial selection)
                if (defaultSelectedButton != null)
                    EventSystem.current.SetSelectedGameObject(defaultSelectedButton);
            }
            else
            {
                // Navigate down using explicit navigation
                Selectable selectable = selected.GetComponent<Selectable>();
                if (selectable != null)
                {
                    Selectable downNeighbor = selectable.FindSelectableOnDown();
                    if (downNeighbor != null)
                    {
                        EventSystem.current.SetSelectedGameObject(downNeighbor.gameObject);
                        // Play hover sound on successful navigation
                        if (abilityHover.hoverSound != null)
                            abilityHover.hoverSound.Play(_audioSource);
                    }
                }
            }
        }

        // D-pad UP
        if (_activeGamepad.dpad.up.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable upNeighbor = selectable.FindSelectableOnUp();
                if (upNeighbor != null)
                {
                    EventSystem.current.SetSelectedGameObject(upNeighbor.gameObject);
                    // Play hover sound on successful navigation
                    if (abilityHover.hoverSound != null)
                        abilityHover.hoverSound.Play(_audioSource);
                }
                else
                {
                    // No neighbor above - deselect to return to ship rotation mode (no sound)
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        // D-pad LEFT
        if (_activeGamepad.dpad.left.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable leftNeighbor = selectable.FindSelectableOnLeft();
                if (leftNeighbor != null)
                {
                    EventSystem.current.SetSelectedGameObject(leftNeighbor.gameObject);
                    // Play hover sound on successful navigation
                    if (abilityHover.hoverSound != null)
                        abilityHover.hoverSound.Play(_audioSource);
                }
            }
        }

        // D-pad RIGHT
        if (_activeGamepad.dpad.right.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable rightNeighbor = selectable.FindSelectableOnRight();
                if (rightNeighbor != null)
                {
                    EventSystem.current.SetSelectedGameObject(rightNeighbor.gameObject);
                    // Play hover sound on successful navigation
                    if (abilityHover.hoverSound != null)
                        abilityHover.hoverSound.Play(_audioSource);
                }
            }
        }
    }

    /// <summary>
    /// Handle ship navigation (shoulder buttons to switch between ships).
    /// </summary>
    private void HandleShipNavigation()
    {
        if (Time.unscaledTime - _lastNavigationTime < navigation.navigationCooldown)
            return;

        bool navigateLeft = false;
        bool navigateRight = false;

        // Active gamepad shoulder buttons for ship navigation
        if (_activeGamepad != null)
        {
            navigateLeft = _activeGamepad.leftShoulder.wasPressedThisFrame;
            navigateRight = _activeGamepad.rightShoulder.wasPressedThisFrame;
        }

        // Keyboard fallback (Q/E for shoulders)
        if (!navigateLeft && !navigateRight && Keyboard.current != null)
        {
            navigateLeft = Keyboard.current.qKey.wasPressedThisFrame;
            navigateRight = Keyboard.current.eKey.wasPressedThisFrame;
        }

        if (navigateLeft)
            NavigatePrevious();
        else if (navigateRight)
            NavigateNext();
    }

    /// <summary>
    /// Navigate to the previous ship in the list (wraps around).
    /// </summary>
    public void NavigatePrevious()
    {
        if (availableShips == null || availableShips.Length == 0) return;

        _currentShipIndex--;
        if (_currentShipIndex < 0)
            _currentShipIndex = availableShips.Length - 1;

        LoadShip(_currentShipIndex);
        PlayNavigationSound();
        FlashNavigationButton(leftNavigationImage);
        _lastNavigationTime = Time.unscaledTime;
    }

    /// <summary>
    /// Navigate to the next ship in the list (wraps around).
    /// </summary>
    public void NavigateNext()
    {
        if (availableShips == null || availableShips.Length == 0) return;

        _currentShipIndex++;
        if (_currentShipIndex >= availableShips.Length)
            _currentShipIndex = 0;

        LoadShip(_currentShipIndex);
        PlayNavigationSound();
        FlashNavigationButton(rightNavigationImage);
        _lastNavigationTime = Time.unscaledTime;
    }

    /// <summary>
    /// Flash a navigation button with the pressed material briefly.
    /// </summary>
    private void FlashNavigationButton(Image buttonImage)
    {
        if (buttonImage != null)
            StartCoroutine(FlashNavigationButtonCoroutine(buttonImage));
    }

    /// <summary>
    /// Coroutine to flash a navigation button material.
    /// </summary>
    private IEnumerator FlashNavigationButtonCoroutine(Image buttonImage)
    {
        if (buttonImage == null)
        {
            Debug.LogWarning("ShipSelectManager: FlashNavigationButton called with null Image");
            yield break;
        }

        if (navigationEffects.buttonPressedMaterial == null)
        {
            Debug.LogWarning("ShipSelectManager: No pressed material assigned for navigation button flash");
            yield break;
        }

        // Store original material
        Material originalMaterial = buttonImage.material;

        // Apply pressed material (instantiate to avoid modifying the asset)
        buttonImage.material = new Material(navigationEffects.buttonPressedMaterial);

        // Wait for flash duration
        yield return new WaitForSecondsRealtime(navigationEffects.flashDuration);

        // Revert to normal material
        if (navigationEffects.buttonNormalMaterial != null)
        {
            buttonImage.material = new Material(navigationEffects.buttonNormalMaterial);
        }
        else
        {
            // Fallback: restore original material
            buttonImage.material = originalMaterial;
        }
    }


    /// <summary>
    /// Preload ship data before the screen is visible (called early in transition).
    /// Ships are already spawned, so this just loads UI data.
    /// Ship will be activated when canvas is fully visible (in OnEnable).
    /// </summary>
    public void PreloadShipData()
    {
        Debug.Log("[PreloadShipData] Loading ship UI data (ship will activate when canvas is visible)");

        // Load UI data (text, stats, abilities)
        LoadShipDataOnly(_currentShipIndex);

        // Prepare ship position/rotation but DON'T activate yet
        PrepareShipTransform(_currentShipIndex);

        _isPreloaded = true;
    }

    /// <summary>
    /// Prepare ship transform (position, rotation, scale) without activating it.
    /// This sets everything up so activation is instant later.
    /// </summary>
    private void PrepareShipTransform(int index)
    {
        if (_shipModelInstances == null || index < 0 || index >= _shipModelInstances.Length)
            return;

        GameObject selectedShip = _shipModelInstances[index];
        if (selectedShip != null)
        {
            Debug.Log($"[PrepareShipTransform] Preparing ship {index} transform (still inactive)");

            // Set position, scale, rotation while ship is inactive
            selectedShip.transform.position = shipModel.displayPosition;
            selectedShip.transform.localScale = Vector3.one * shipModel.displayScale;

            _targetRotation = Quaternion.Euler(shipModel.displayRotation);
            _currentRotation = _targetRotation;
            selectedShip.transform.rotation = _currentRotation;

            // Ship stays inactive - will be activated in OnEnable
        }
    }

    /// <summary>
    /// Load ship data without showing the model (for preloading).
    /// </summary>
    private void LoadShipDataOnly(int index)
    {
        if (availableShips == null || index < 0 || index >= availableShips.Length)
        {
            Debug.LogWarning($"ShipSelectManager: Invalid ship index {index}");
            return;
        }

        ShipData ship = availableShips[index];

        // Update ship name
        if (shipNameText != null)
        {
            shipNameText.text = ship.shipName;
            shipNameText.enableAutoSizing = false;
            shipNameText.fontSize = textSizes.shipNameSize;
        }

        // Update stat bars and text
        UpdateStatBar(statBars.damageFill, statBars.damageText, ship.stats.damage, statMaxValues.maxDamage);
        UpdateStatBar(statBars.hullFill, statBars.hullText, ship.stats.hull, statMaxValues.maxHull);
        UpdateStatBar(statBars.shieldFill, statBars.shieldText, ship.stats.shield, statMaxValues.maxShield);
        UpdateStatBar(statBars.speedFill, statBars.speedText, ship.stats.speed, statMaxValues.maxSpeed);

        // Update abilities (pass icon arrays and current ship index)
        UpdateAbility(ability1, ship.ability1, abilityIcons.ability1Icons, index);
        UpdateAbility(ability2, ship.ability2, abilityIcons.ability2Icons, index);
        UpdateAbility(ability3, ship.ability3, abilityIcons.ability3Icons, index);
        UpdateAbility(ability4, ship.ability4, abilityIcons.ability4Icons, index);

        // Initialize all abilities to normal state (no hover)
        InitializeAbilityStates();
    }

    /// <summary>
    /// Initialize all abilities to their normal (non-hovered) state.
    /// Sets normal materials and colors on all abilities.
    /// </summary>
    private void InitializeAbilityStates()
    {
        for (int i = 0; i < 4; i++)
        {
            ApplyAbilityNormalState(i);
        }

        // Reset hover tracking
        _lastHoveredAbilityIndex = -1;
    }

    /// <summary>
    /// Load and display the specified ship's data.
    /// </summary>
    private void LoadShip(int index)
    {
        LoadShipDataOnly(index);
        ShowShipModel(index);
    }

    /// <summary>
    /// Update a stat bar's fill using RectTransform offset and update text value.
    /// 0 = full bar, 200 = empty bar (modifies RectTransform 'right' component).
    /// </summary>
    private void UpdateStatBar(Image fillImage, TextMeshProUGUI valueText, float value, float maxValue)
    {
        // Update bar fill (modify RectTransform right offset)
        if (fillImage != null)
        {
            RectTransform rectTransform = fillImage.rectTransform;
            float fillPercentage = maxValue > 0f ? Mathf.Clamp01(value / maxValue) : 0f;
            float offsetRight = (1f - fillPercentage) * 200f;

            Vector2 offsetMax = rectTransform.offsetMax;
            offsetMax.x = -offsetRight;
            rectTransform.offsetMax = offsetMax;
        }

        // Update text value (format: "LABEL: VALUE")
        if (valueText != null)
        {
            string currentText = valueText.text;
            int colonIndex = currentText.IndexOf(':');

            if (colonIndex >= 0)
            {
                // Preserve the label, only update the number
                string label = currentText.Substring(0, colonIndex + 1);
                valueText.text = $"{label} {value.ToString("F0")}";
            }
            else
            {
                // No colon found, just set the value
                valueText.text = value.ToString("F0");
            }

            // Set fixed font size
            valueText.enableAutoSizing = false;
            valueText.fontSize = textSizes.statTextSize;
        }
    }

    /// <summary>
    /// Update an ability button's icon and tooltip data.
    /// Enables the correct pre-placed icon for the current ship, disables others.
    /// </summary>
    private void UpdateAbility(AbilityButtonReferences abilityRef, ShipData.AbilityData abilityData, Image[] iconArray, int shipIndex)
    {
        // Enable/disable the correct icon from the pre-placed array
        if (iconArray != null && iconArray.Length > 0)
        {
            // Disable all icons for this ability
            for (int i = 0; i < iconArray.Length; i++)
            {
                if (iconArray[i] != null)
                    iconArray[i].gameObject.SetActive(false);
            }

            // Enable only the icon for the current ship
            if (shipIndex >= 0 && shipIndex < iconArray.Length && iconArray[shipIndex] != null)
            {
                iconArray[shipIndex].gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"ShipSelectManager: Icon array index {shipIndex} out of bounds (array length: {iconArray.Length})");
            }
        }

        // Update tooltip text
        if (abilityRef.tooltipTitle != null)
        {
            abilityRef.tooltipTitle.text = abilityData.abilityName;
            abilityRef.tooltipTitle.enableAutoSizing = false;
            abilityRef.tooltipTitle.fontSize = textSizes.tooltipTitleSize;
        }

        if (abilityRef.tooltipDescription != null)
        {
            abilityRef.tooltipDescription.text = abilityData.abilityDescription;
            abilityRef.tooltipDescription.enableAutoSizing = false;
            abilityRef.tooltipDescription.fontSize = textSizes.tooltipDescriptionSize;
        }

        // Ensure tooltip starts hidden
        if (abilityRef.tooltip != null)
            abilityRef.tooltip.SetActive(false);
    }

    /// <summary>
    /// PUBLIC: Called by TitleScreenManager at scene load to spawn ships early.
    /// This eliminates any loading delay when entering ship select.
    /// </summary>
    public void SpawnShipsAtSceneLoad()
    {
        Debug.Log("[SpawnShipsAtSceneLoad] Called by TitleScreenManager at scene start");
        SpawnAllShipModels();
    }

    /// <summary>
    /// Spawn all ship models (deactivated).
    /// Only spawns once - subsequent calls are ignored.
    /// </summary>
    private void SpawnAllShipModels()
    {
        // ONLY spawn once - prevent duplicate spawning
        if (_shipModelInstances != null && _shipModelInstances.Length > 0)
            return;

        if (availableShips == null || availableShips.Length == 0)
        {
            Debug.LogWarning("ShipSelectManager: No ships available to spawn");
            return;
        }

        _shipModelInstances = new GameObject[availableShips.Length];

        for (int i = 0; i < availableShips.Length; i++)
        {
            if (availableShips[i].shipModelPrefab == null)
            {
                Debug.LogWarning($"ShipSelectManager: Ship {i} has no model prefab assigned");
                continue;
            }

            GameObject instance = Instantiate(
                availableShips[i].shipModelPrefab,
                shipModel.shipModelParent != null ? shipModel.shipModelParent : transform
            );

            instance.name = $"ShipModel_{availableShips[i].shipName}";
            instance.SetActive(false);

            // Disable all scripts on the ship model (visual preview only)
            MonoBehaviour[] scripts = instance.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in scripts)
                script.enabled = false;

            // Re-enable preview controller (it's designed to work independently)
            Class1PreviewController previewController = instance.GetComponent<Class1PreviewController>();
            if (previewController != null)
                previewController.enabled = true;

            _shipModelInstances[i] = instance;
        }
    }

    /// <summary>
    /// Show the ship model for the specified index, hide all others.
    /// </summary>
    private void ShowShipModel(int index)
    {
        Debug.Log($"[ShowShipModel] Called with index {index}. Current ship index: {_currentShipIndex}");
        Debug.Log($"[ShowShipModel] Stack trace: {System.Environment.StackTrace}");

        if (_shipModelInstances == null || index < 0 || index >= _shipModelInstances.Length)
        {
            Debug.LogWarning($"[ShowShipModel] Cannot show ship {index} - instances null: {_shipModelInstances == null}, length: {_shipModelInstances?.Length ?? 0}");
            return;
        }

        Debug.Log($"[ShowShipModel] Total ship instances: {_shipModelInstances.Length}");

        // Stop active preview before switching ships
        if (_currentPreviewController != null)
        {
            _currentPreviewController.StopPreview();
            _currentPreviewController = null;
        }

        // Hide all ship models
        for (int i = 0; i < _shipModelInstances.Length; i++)
        {
            if (_shipModelInstances[i] != null)
            {
                bool wasActive = _shipModelInstances[i].activeSelf;
                _shipModelInstances[i].SetActive(false);
                Debug.Log($"[ShowShipModel] Ship {i} ({_shipModelInstances[i].name}) - was active: {wasActive}, now deactivated");
            }
        }

        // Show and position the selected ship
        GameObject selectedShip = _shipModelInstances[index];
        if (selectedShip != null)
        {
            Debug.Log($"[ShowShipModel] BEFORE SetActive - Ship {index} active state: {selectedShip.activeSelf}, activeInHierarchy: {selectedShip.activeInHierarchy}");

            // CRITICAL: Activate the ship GameObject
            selectedShip.SetActive(true);

            Debug.Log($"[ShowShipModel] AFTER SetActive - Ship {index} active state: {selectedShip.activeSelf}, activeInHierarchy: {selectedShip.activeInHierarchy}");

            // Ensure parent is also active (in case ship is child of inactive parent)
            if (selectedShip.transform.parent != null)
            {
                Debug.Log($"[ShowShipModel] Parent: {selectedShip.transform.parent.name}, parent active: {selectedShip.transform.parent.gameObject.activeSelf}");
                selectedShip.transform.parent.gameObject.SetActive(true);
            }

            // Set position, scale, and rotation
            selectedShip.transform.position = shipModel.displayPosition;
            selectedShip.transform.localScale = Vector3.one * shipModel.displayScale;

            // Initialize rotation from config
            _targetRotation = Quaternion.Euler(shipModel.displayRotation);
            _currentRotation = _targetRotation;
            selectedShip.transform.rotation = _currentRotation;

            Debug.Log($"[ShowShipModel] Ship {index} ({selectedShip.name}) FINAL STATE: activeSelf={selectedShip.activeSelf}, position={selectedShip.transform.position}, scale={selectedShip.transform.localScale}");
        }
        else
        {
            Debug.LogError($"[ShowShipModel] Ship {index} is null!");
        }
    }

    /// <summary>
    /// Hide all ship models.
    /// </summary>
    private void HideAllShipModels()
    {
        Debug.Log($"[HideAllShipModels] Called! Stack trace: {System.Environment.StackTrace}");

        if (_shipModelInstances == null)
        {
            Debug.Log("[HideAllShipModels] Ship instances array is null");
            return;
        }

        Debug.Log($"[HideAllShipModels] Hiding {_shipModelInstances.Length} ships");

        foreach (var model in _shipModelInstances)
        {
            if (model != null)
            {
                Debug.Log($"[HideAllShipModels] Deactivating {model.name}, was active: {model.activeSelf}");
                model.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Hide all ability tooltips.
    /// </summary>
    private void HideAllTooltips()
    {
        if (ability1.tooltip != null) ability1.tooltip.SetActive(false);
        if (ability2.tooltip != null) ability2.tooltip.SetActive(false);
        if (ability3.tooltip != null) ability3.tooltip.SetActive(false);
        if (ability4.tooltip != null) ability4.tooltip.SetActive(false);
    }

    private void PlayNavigationSound()
    {
        if (navigation.navigationSound != null)
            navigation.navigationSound.Play(_audioSource);
    }


    /// <summary>
    /// Public method to show a specific ability tooltip (for testing or external calls).
    /// </summary>
    public void ShowAbilityTooltip(int abilityIndex)
    {
        GameObject tooltip = null;

        switch (abilityIndex)
        {
            case 1: tooltip = ability1.tooltip; break;
            case 2: tooltip = ability2.tooltip; break;
            case 3: tooltip = ability3.tooltip; break;
            case 4: tooltip = ability4.tooltip; break;
        }

        if (tooltip != null)
            tooltip.SetActive(true);
    }

    /// <summary>
    /// Public method to hide a specific ability tooltip.
    /// </summary>
    public void HideAbilityTooltip(int abilityIndex)
    {
        GameObject tooltip = null;

        switch (abilityIndex)
        {
            case 1: tooltip = ability1.tooltip; break;
            case 2: tooltip = ability2.tooltip; break;
            case 3: tooltip = ability3.tooltip; break;
            case 4: tooltip = ability4.tooltip; break;
        }

        if (tooltip != null)
            tooltip.SetActive(false);
    }

    /// <summary>
    /// Update player selection text based on current player state.
    /// </summary>
    private void UpdatePlayerSelectionText()
    {
        if (selectionUI.playerSelectionText == null) return;

        selectionUI.playerSelectionText.text = _currentPlayer == PlayerSelectState.Player1
            ? "PLAYER 1"
            : "PLAYER 2";
    }

    /// <summary>
    /// Handle hold button logic for back and select buttons.
    /// </summary>
    private void HandleHoldButtons()
    {
        bool backPressed = false;
        bool selectPressed = false;

        // Active gamepad input only
        if (_activeGamepad != null)
        {
            backPressed = _activeGamepad.bButton.isPressed;
            selectPressed = _activeGamepad.aButton.isPressed;
        }

        // Keyboard fallback (Escape for back, Enter for select)
        if (Keyboard.current != null)
        {
            backPressed = backPressed || Keyboard.current.escapeKey.isPressed;
            selectPressed = selectPressed || Keyboard.current.enterKey.isPressed;
        }

        // Back button (B / Escape)
        if (backPressed)
        {
            _backHoldTime += Time.unscaledDeltaTime;
            // Fill drains from 1 (full) to 0 (empty) as you hold
            float fillRatio = 1f - Mathf.Clamp01(_backHoldTime / holdBack.holdDuration);

            if (selectionUI.backButtonFill != null)
                selectionUI.backButtonFill.fillAmount = fillRatio;

            if (_backHoldTime >= holdBack.holdDuration)
            {
                HandleBackConfirm();
                _backHoldTime = 0f;
            }
        }
        else
        {
            _backHoldTime = 0f;
            if (selectionUI.backButtonFill != null)
                selectionUI.backButtonFill.fillAmount = 1f; // Reset to full
        }

        // Select button (A / Enter)
        if (selectPressed)
        {
            _selectHoldTime += Time.unscaledDeltaTime;
            // Fill drains from 1 (full) to 0 (empty) as you hold
            float fillRatio = 1f - Mathf.Clamp01(_selectHoldTime / holdSelect.holdDuration);

            if (selectionUI.selectButtonFill != null)
                selectionUI.selectButtonFill.fillAmount = fillRatio;

            if (_selectHoldTime >= holdSelect.holdDuration)
            {
                HandleSelectConfirm();
                _selectHoldTime = 0f;
            }
        }
        else
        {
            _selectHoldTime = 0f;
            if (selectionUI.selectButtonFill != null)
                selectionUI.selectButtonFill.fillAmount = 1f; // Reset to full
        }
    }

    /// <summary>
    /// Handle back button confirmation (hold complete).
    /// </summary>
    private void HandleBackConfirm()
    {
        if (holdBack.confirmSound != null)
            holdBack.confirmSound.Play(_audioSource);

        if (_currentPlayer == PlayerSelectState.Player1)
        {
            // Player 1 going back - return to main menu with proper transition
            Debug.Log("Player 1 backing out - returning to main menu");
            TitleScreenManager titleScreen = FindObjectOfType<TitleScreenManager>();
            if (titleScreen != null)
                titleScreen.TransitionToMainMenuFromShipSelect();
        }
        else
        {
            // Player 2 going back - return to Player 1 selection
            Debug.Log("Player 2 backing out - returning to Player 1 selection");
            _currentPlayer = PlayerSelectState.Player1;
            SetActiveGamepadForCurrentPlayer();
            _player2Selection = null;
            UpdatePlayerSelectionText();

            // Reset to Player 1's previous selection if they had one
            if (_player1Selection != null)
            {
                int p1Index = System.Array.IndexOf(availableShips, _player1Selection);
                if (p1Index >= 0)
                {
                    _currentShipIndex = p1Index;
                    LoadShip(_currentShipIndex);
                }
            }
        }
    }

    /// <summary>
    /// Handle select button confirmation (hold complete).
    /// </summary>
    private void HandleSelectConfirm()
    {
        if (_isProcessingSelection) return;

        if (holdSelect.confirmSound != null)
            holdSelect.confirmSound.Play(_audioSource);

        StartCoroutine(ProcessSelectionCoroutine());
    }

    /// <summary>
    /// Process ship selection: store data, animate, transition.
    /// </summary>
    private IEnumerator ProcessSelectionCoroutine()
    {
        _isProcessingSelection = true;

        // Store selected ship
        ShipData selectedShip = availableShips[_currentShipIndex];

        if (_currentPlayer == PlayerSelectState.Player1)
        {
            _player1Selection = selectedShip;
            Debug.Log($"Player 1 selected: {selectedShip.shipName}");
        }
        else
        {
            _player2Selection = selectedShip;
            Debug.Log($"Player 2 selected: {selectedShip.shipName}");
        }

        // Wait for confirmation delay
        yield return new WaitForSecondsRealtime(postSelection.confirmationDelay);

        // Spin animation
        yield return StartCoroutine(SpinShipAnimation());

        // Wait after spin
        yield return new WaitForSecondsRealtime(postSelection.postSpinDelay);

        // Transition based on player
        if (_currentPlayer == PlayerSelectState.Player1)
        {
            // Fade transition to Player 2 selection
            yield return StartCoroutine(TransitionToPlayer2());

            _isProcessingSelection = false;
        }
        else
        {
            // Player 2 done - store both selections and transition to gameplay
            StoreSelectionsInGameData();
            TransitionToGameplay();
        }
    }

    /// <summary>
    /// Slide transition from Player 1 to Player 2 selection.
    /// </summary>
    private IEnumerator TransitionToPlayer2()
    {
        if (uiContainer == null)
        {
            Debug.LogWarning("ShipSelectManager: No UI container assigned for slide transition!");
            // Fallback: just switch without animation
            _currentPlayer = PlayerSelectState.Player2;
            SetActiveGamepadForCurrentPlayer();
            UpdatePlayerSelectionText();
            _currentShipIndex = 0;
            LoadShip(_currentShipIndex);
            yield break;
        }

        // Use screen width for slide distance (automatically adapts to any resolution)
        float slideDistance = Screen.width;

        Vector2 startPos = uiContainer.anchoredPosition;
        Vector3 shipStartPos = Vector3.zero;
        GameObject currentShip = _shipModelInstances != null && _currentShipIndex < _shipModelInstances.Length
            ? _shipModelInstances[_currentShipIndex]
            : null;

        if (currentShip != null && postSelection.slideShipWithUI)
            shipStartPos = currentShip.transform.position;

        // --- SLIDE OUT (left) ---
        float elapsed = 0f;
        while (elapsed < postSelection.slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / postSelection.slideOutDuration));

            // Slide UI left
            Vector2 targetPos = startPos + Vector2.left * slideDistance;
            uiContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);

            // Slide ship left (convert screen space to world space offset)
            if (currentShip != null && postSelection.slideShipWithUI)
            {
                float shipOffset = Mathf.Lerp(0f, -slideDistance * 0.01f, t); // Scale down for world space
                currentShip.transform.position = shipStartPos + Vector3.right * shipOffset;
            }

            yield return null;
        }

        // Ensure final position
        uiContainer.anchoredPosition = startPos + Vector2.left * slideDistance;

        // --- SWITCH TO PLAYER 2 (while off-screen) ---
        _currentPlayer = PlayerSelectState.Player2;
        SetActiveGamepadForCurrentPlayer();
        UpdatePlayerSelectionText();

        // Reset to first ship for Player 2
        _currentShipIndex = 0;
        LoadShip(_currentShipIndex);

        GameObject newShip = _shipModelInstances != null && _currentShipIndex < _shipModelInstances.Length
            ? _shipModelInstances[_currentShipIndex]
            : null;

        // Position UI on the right side (ready to slide in)
        Vector2 rightPos = startPos + Vector2.right * slideDistance;
        uiContainer.anchoredPosition = rightPos;

        // Position ship on the right
        Vector3 shipRightPos = shipStartPos;
        if (newShip != null && postSelection.slideShipWithUI)
        {
            shipRightPos = shipStartPos + Vector3.right * (slideDistance * 0.01f);
            newShip.transform.position = shipRightPos;
        }

        // --- SLIDE IN (from right) ---
        elapsed = 0f;
        while (elapsed < postSelection.slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / postSelection.slideInDuration));

            // Slide UI to center
            uiContainer.anchoredPosition = Vector2.Lerp(rightPos, startPos, t);

            // Slide ship to center
            if (newShip != null && postSelection.slideShipWithUI)
            {
                newShip.transform.position = Vector3.Lerp(shipRightPos, shipStartPos, t);
            }

            yield return null;
        }

        // Ensure final position
        uiContainer.anchoredPosition = startPos;
        if (newShip != null && postSelection.slideShipWithUI)
            newShip.transform.position = shipStartPos;
    }

    /// <summary>
    /// Animate ship spinning 360 degrees on Y axis.
    /// </summary>
    private IEnumerator SpinShipAnimation()
    {
        Debug.Log($"[SpinShipAnimation] Starting spin animation for ship {_currentShipIndex}");

        if (_shipModelInstances == null || _currentShipIndex >= _shipModelInstances.Length)
        {
            Debug.LogWarning($"[SpinShipAnimation] Invalid ship instances or index");
            yield break;
        }

        GameObject currentShip = _shipModelInstances[_currentShipIndex];
        if (currentShip == null)
        {
            Debug.LogWarning($"[SpinShipAnimation] Ship at index {_currentShipIndex} is null");
            yield break;
        }

        if (!currentShip.activeSelf)
        {
            Debug.LogWarning($"[SpinShipAnimation] Ship {currentShip.name} is not active");
            yield break;
        }

        Debug.Log($"[SpinShipAnimation] Spinning ship: {currentShip.name}");

        Vector3 startEuler = currentShip.transform.rotation.eulerAngles;
        float startY = startEuler.y;
        float endY = startY + 360f;

        Debug.Log($"[SpinShipAnimation] Start Y: {startY}, End Y: {endY}");

        float elapsed = 0f;
        while (elapsed < postSelection.spinDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / postSelection.spinDuration;

            // Interpolate Y rotation from start to start+360
            float currentY = Mathf.Lerp(startY, endY, t);
            Vector3 currentEuler = new Vector3(startEuler.x, currentY, startEuler.z);
            currentShip.transform.rotation = Quaternion.Euler(currentEuler);

            yield return null;
        }

        // Ensure final rotation (normalize back to 0-360 range)
        Vector3 finalEuler = new Vector3(startEuler.x, startY, startEuler.z);
        currentShip.transform.rotation = Quaternion.Euler(finalEuler);

        // Update rotation tracking
        _currentRotation = currentShip.transform.rotation;
        _targetRotation = _currentRotation;

        Debug.Log($"[SpinShipAnimation] Spin complete! Final rotation: {currentShip.transform.rotation.eulerAngles}");
    }

    /// <summary>
    /// Store both player selections in GameDataManager.
    /// </summary>
    private void StoreSelectionsInGameData()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogError("ShipSelectManager: GameDataManager instance not found!");
            return;
        }

        GameDataManager.Instance.selectedShipClasses.Clear();

        if (_player1Selection != null)
        {
            GameDataManager.Instance.selectedShipClasses.Add(_player1Selection);
            Debug.Log($"Stored Player 1 selection: {_player1Selection.shipName}");
        }

        if (_player2Selection != null)
        {
            GameDataManager.Instance.selectedShipClasses.Add(_player2Selection);
            Debug.Log($"Stored Player 2 selection: {_player2Selection.shipName}");
        }
    }

    /// <summary>
    /// Transition to gameplay scene.
    /// </summary>
    private void TransitionToGameplay()
    {
        if (string.IsNullOrEmpty(postSelection.gameplaySceneName))
        {
            Debug.LogError("ShipSelectManager: No gameplay scene name configured!");
            return;
        }

        Debug.Log($"Transitioning to gameplay scene: {postSelection.gameplaySceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(postSelection.gameplaySceneName);
    }

    private void Reset()
    {
        // Default values (match ShipData ranges)
        statMaxValues.maxDamage = 50f;
        statMaxValues.maxHull = 500f;
        statMaxValues.maxShield = 500f;
        statMaxValues.maxSpeed = 100f;

        shipModel.displayPosition = new Vector3(0f, 0f, 5f);
        shipModel.displayRotation = new Vector3(0f, 180f, 0f);
        shipModel.displayScale = 1f;

        navigation.navigationCooldown = 0.2f;

        textSizes.statTextSize = 30f;
        textSizes.shipNameSize = 48f;
        textSizes.tooltipTitleSize = 24f;
        textSizes.tooltipDescriptionSize = 18f;

        shipRotation.yawSensitivity = 2f;
        shipRotation.pitchSensitivity = 2f;
        shipRotation.rollSensitivity = 1f;
        shipRotation.rotationSmoothing = 0.15f;
        shipRotation.inputDeadzone = 0.1f;

        // Navigation button effect defaults
        navigationEffects.flashDuration = 0.15f;

        // Hold button defaults
        holdBack.holdDuration = 1.5f;
        holdSelect.holdDuration = 1.0f;

        // Post-selection animation defaults
        postSelection.confirmationDelay = 0.3f;
        postSelection.spinDuration = 1.5f;
        postSelection.postSpinDelay = 0.5f;
        postSelection.slideOutDuration = 0.5f;
        postSelection.slideInDuration = 0.5f;
        postSelection.slideShipWithUI = true;
        postSelection.gameplaySceneName = "SampleScene";
    }
}
