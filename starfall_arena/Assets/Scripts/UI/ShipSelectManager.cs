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

    [Header("Navigation Buttons")]
    [Tooltip("Left arrow button for previous ship")]
    [SerializeField] private Button leftButton;

    [Tooltip("Right arrow button for next ship")]
    [SerializeField] private Button rightButton;

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

    private int _currentShipIndex = 0;
    private GameObject[] _shipModelInstances;
    private AudioSource _audioSource;
    private float _lastNavigationTime = 0f;
    private Quaternion _targetRotation;
    private Quaternion _currentRotation;
    private bool _isPreloaded = false;
    private InputSystemUIInputModule _uiInputModule;
    private bool _wasNavigationEnabled;

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
    }

    private void Start()
    {
        Debug.Log("[Start] ShipSelectManager Start called");

        // Spawn all ship models (deactivated) after scene setup
        SpawnAllShipModels();

        // DON'T hide ships here - OnEnable/OnDisable handles visibility
        // Hiding here would hide ships that OnEnable just showed!
        Debug.Log("[Start] Ships spawned, not hiding (OnEnable handles visibility)");
    }

    private void OnEnable()
    {
        Debug.Log("[OnEnable] ShipSelectManager enabled!");

        // Disable EventSystem's automatic navigation (stick input) - we handle navigation manually
        DisableEventSystemNavigation();

        // Ensure ships are spawned
        if (_shipModelInstances == null || _shipModelInstances.Length == 0)
        {
            Debug.Log("[OnEnable] Ships not spawned, spawning now...");
            SpawnAllShipModels();
        }
        else
        {
            Debug.Log($"[OnEnable] Ships already spawned: {_shipModelInstances.Length} instances");
        }

        // If not preloaded, load data now
        if (!_isPreloaded)
        {
            Debug.Log("[OnEnable] Not preloaded, loading ship data...");
            LoadShipDataOnly(_currentShipIndex);
        }
        else
        {
            Debug.Log("[OnEnable] Already preloaded, skipping data load");
        }

        // ALWAYS show the current ship model
        Debug.Log($"[OnEnable] About to show ship model {_currentShipIndex}");
        ShowShipModel(_currentShipIndex);

        HideAllTooltips(); // Ensure tooltips are hidden when screen opens

        // CRITICAL: Start with NO button selected (ship rotation mode)
        // Do this on next frame to ensure EventSystem doesn't auto-select
        StartCoroutine(ClearSelectionNextFrame());

        // Reset flag for next time
        _isPreloaded = false;

        Debug.Log("[OnEnable] Complete!");
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
        _lastNavigationTime = Time.unscaledTime;
    }

    /// <summary>
    /// Preload ship data before the screen is visible (called early in transition).
    /// </summary>
    public void PreloadShipData()
    {
        // Spawn ships if not already spawned
        if (_shipModelInstances == null || _shipModelInstances.Length == 0)
        {
            SpawnAllShipModels();
        }

        // Load current ship data (but don't show the model yet)
        LoadShipDataOnly(_currentShipIndex);

        _isPreloaded = true;
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

        // Update abilities
        UpdateAbility(ability1, ship.ability1);
        UpdateAbility(ability2, ship.ability2);
        UpdateAbility(ability3, ship.ability3);
        UpdateAbility(ability4, ship.ability4);
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
    /// </summary>
    private void UpdateAbility(AbilityButtonReferences abilityRef, ShipData.AbilityData abilityData)
    {
        // Find or create icon image as child of circle button (skip tooltip-related images)
        if (abilityRef.circleButton != null && abilityData.abilityIcon != null)
        {
            Transform circleTransform = abilityRef.circleButton.transform;
            Image iconImage = null;

            // Look for existing icon image (skip any Images that are part of the tooltip hierarchy)
            foreach (Transform child in circleTransform)
            {
                // Skip the tooltip and its children
                if (abilityRef.tooltip != null && (child.gameObject == abilityRef.tooltip || child.IsChildOf(abilityRef.tooltip.transform)))
                    continue;

                Image childImage = child.GetComponent<Image>();
                if (childImage != null && childImage != abilityRef.circleButton)
                {
                    iconImage = childImage;
                    break;
                }
            }

            // If no child image exists, create one
            if (iconImage == null)
            {
                GameObject iconObj = new GameObject("AbilityIcon");
                iconObj.transform.SetParent(circleTransform, false);
                iconImage = iconObj.AddComponent<Image>();

                // Set up the icon rect transform
                RectTransform iconRect = iconImage.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;

                // Size the icon to fit within the circle (80% of circle size for padding)
                RectTransform circleRect = abilityRef.circleButton.rectTransform;
                float circleSize = Mathf.Min(circleRect.rect.width, circleRect.rect.height);
                float iconSize = circleSize * 0.8f;
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                // Preserve aspect ratio
                iconImage.preserveAspect = true;
            }

            // Set the sprite (don't modify transform if it already exists)
            iconImage.sprite = abilityData.abilityIcon;
            iconImage.enabled = true;
            iconImage.color = Color.white;
        }

        // Update tooltip text ONLY (don't touch tooltip transform/layout)
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

        // Ensure tooltip starts hidden (just visibility, don't modify layout)
        if (abilityRef.tooltip != null)
            abilityRef.tooltip.SetActive(false);
    }

    /// <summary>
    /// Spawn all ship models at start (deactivated).
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
    }
}
