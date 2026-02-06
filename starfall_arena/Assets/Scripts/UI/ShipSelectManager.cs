using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
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

    [Header("Navigation Buttons")]
    [Tooltip("Left arrow button for previous ship")]
    [SerializeField] private Button leftButton;

    [Tooltip("Right arrow button for next ship")]
    [SerializeField] private Button rightButton;

    [Tooltip("Left button's Image component (for material effects)")]
    [SerializeField] private Image leftButtonImage;

    [Tooltip("Right button's Image component (for material effects)")]
    [SerializeField] private Image rightButtonImage;

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

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        // Wire up button callbacks
        if (leftButton != null)
            leftButton.onClick.AddListener(NavigatePrevious);

        if (rightButton != null)
            rightButton.onClick.AddListener(NavigateNext);

        // Hide all tooltips initially
        HideAllTooltips();

        // Ensure all ability icons start disabled
        DisableAllAbilityIcons();

        // Ships are now spawned by TitleScreenManager at scene load
        // This keeps ShipSelectManager completely independent
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

        // Validate navigation button references
        if (leftButton != null && leftButtonImage == null)
        {
            Debug.LogWarning("ShipSelectManager: Left Button is assigned but Left Button Image is not. Material flash won't work.");
        }

        if (rightButton != null && rightButtonImage == null)
        {
            Debug.LogWarning("ShipSelectManager: Right Button is assigned but Right Button Image is not. Material flash won't work.");
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

    private IEnumerator ClearSelectionNextFrame()
    {
        yield return null; // Wait one frame
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDisable()
    {
        Debug.Log("[OnDisable] ShipSelectManager disabled!");

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
        // STICKS ALWAYS rotate ship (separated from D-pad navigation)
        HandleShipRotation();

        // D-pad navigates UI (EventSystem handles automatically)
        HandleDPadNavigation();

        // Shoulder buttons switch ships
        HandleShipNavigation();

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
        if (Gamepad.current == null) return;

        // Read stick input
        Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
        Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

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
        }
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
        if (Gamepad.current == null || EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        // D-pad DOWN
        if (Gamepad.current.dpad.down.wasPressedThisFrame)
        {
            if (selected == null)
            {
                // Nothing selected - select first ability
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
                        EventSystem.current.SetSelectedGameObject(downNeighbor.gameObject);
                }
            }
        }

        // D-pad UP
        if (Gamepad.current.dpad.up.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable upNeighbor = selectable.FindSelectableOnUp();
                if (upNeighbor != null)
                {
                    EventSystem.current.SetSelectedGameObject(upNeighbor.gameObject);
                }
                else
                {
                    // No neighbor above - deselect to return to ship rotation mode
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        // D-pad LEFT
        if (Gamepad.current.dpad.left.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable leftNeighbor = selectable.FindSelectableOnLeft();
                if (leftNeighbor != null)
                    EventSystem.current.SetSelectedGameObject(leftNeighbor.gameObject);
            }
        }

        // D-pad RIGHT
        if (Gamepad.current.dpad.right.wasPressedThisFrame && selected != null)
        {
            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null)
            {
                Selectable rightNeighbor = selectable.FindSelectableOnRight();
                if (rightNeighbor != null)
                    EventSystem.current.SetSelectedGameObject(rightNeighbor.gameObject);
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

        // Gamepad shoulder buttons for ship navigation
        if (Gamepad.current != null)
        {
            navigateLeft = Gamepad.current.leftShoulder.wasPressedThisFrame;
            navigateRight = Gamepad.current.rightShoulder.wasPressedThisFrame;
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
        FlashNavigationButton(leftButtonImage);
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
        FlashNavigationButton(rightButtonImage);
        _lastNavigationTime = Time.unscaledTime;
    }

    /// <summary>
    /// Briefly flash a navigation button with the pressed material.
    /// </summary>
    private void FlashNavigationButton(Image buttonImage)
    {
        if (buttonImage != null)
            StartCoroutine(FlashNavigationButtonCoroutine(buttonImage));
    }

    /// <summary>
    /// Coroutine to flash a navigation button material.
    /// Unity UI Images require instantiated materials for runtime changes.
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
    /// Confirm ship selection and proceed to gameplay.
    /// Called by external button (e.g., "Select Ship" button).
    /// </summary>
    public void ConfirmSelection()
    {
        if (navigation.confirmSound != null)
            navigation.confirmSound.Play(_audioSource);

        // TODO: Store selected ship index/data for gameplay scene
        // TODO: Transition to gameplay scene
        Debug.Log($"Ship selected: {availableShips[_currentShipIndex].shipName}");
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
    }
}
