using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

    private int _currentShipIndex = 0;
    private GameObject[] _shipModelInstances;
    private AudioSource _audioSource;
    private float _lastNavigationTime = 0f;

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
        // Spawn all ship models (deactivated) after scene setup
        SpawnAllShipModels();
        HideAllShipModels(); // Ensure all ships are hidden at start
    }

    private void OnEnable()
    {
        // Spawn ships if not already spawned (in case Start hasn't run yet)
        if (_shipModelInstances == null || _shipModelInstances.Length == 0)
        {
            SpawnAllShipModels();
        }

        // Load and show the current ship
        LoadShip(_currentShipIndex);
        SetDefaultSelection();
        HideAllTooltips(); // Ensure tooltips are hidden when screen opens
    }

    private void OnDisable()
    {
        HideAllShipModels();
        HideAllTooltips();

        // Clear EventSystem selection when leaving ship select
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        HandleControllerInput();
        EnsureEventSystemSelection();
    }

    /// <summary>
    /// Ensure EventSystem always has a selected GameObject for controller navigation.
    /// Fixes bug where navigation stops working after a few seconds.
    /// </summary>
    private void EnsureEventSystemSelection()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            // Re-select default button if nothing is selected
            if (defaultSelectedButton != null)
                EventSystem.current.SetSelectedGameObject(defaultSelectedButton);
        }
    }

    /// <summary>
    /// Handle gamepad/keyboard navigation input (controller-first design).
    /// Uses shoulder buttons (LB/RB) for ship navigation.
    /// D-pad/stick used for UI navigation on screen.
    /// </summary>
    private void HandleControllerInput()
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

        // Keyboard fallback (Q/E for shoulders, or arrow keys)
        if (!navigateLeft && !navigateRight && Keyboard.current != null)
        {
            navigateLeft = Keyboard.current.qKey.wasPressedThisFrame ||
                          Keyboard.current.leftArrowKey.wasPressedThisFrame;
            navigateRight = Keyboard.current.eKey.wasPressedThisFrame ||
                           Keyboard.current.rightArrowKey.wasPressedThisFrame;
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
    /// Load and display the specified ship's data.
    /// </summary>
    private void LoadShip(int index)
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

        // Show the correct ship model
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
    /// </summary>
    private void SpawnAllShipModels()
    {
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
        if (_shipModelInstances == null || index < 0 || index >= _shipModelInstances.Length)
            return;

        // Hide all ship models
        for (int i = 0; i < _shipModelInstances.Length; i++)
        {
            if (_shipModelInstances[i] != null)
                _shipModelInstances[i].SetActive(false);
        }

        // Show and position the selected ship
        GameObject selectedShip = _shipModelInstances[index];
        if (selectedShip != null)
        {
            selectedShip.SetActive(true);
            selectedShip.transform.position = shipModel.displayPosition;
            selectedShip.transform.rotation = Quaternion.Euler(shipModel.displayRotation);
            selectedShip.transform.localScale = Vector3.one * shipModel.displayScale;
        }
    }

    /// <summary>
    /// Hide all ship models.
    /// </summary>
    private void HideAllShipModels()
    {
        if (_shipModelInstances == null) return;

        foreach (var model in _shipModelInstances)
        {
            if (model != null)
                model.SetActive(false);
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
    /// Set the default selected button for controller navigation.
    /// </summary>
    private void SetDefaultSelection()
    {
        if (defaultSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(defaultSelectedButton);
        }
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
    }
}
