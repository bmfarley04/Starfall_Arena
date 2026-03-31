using UnityEngine;
using UnityEngine.UI;

public class PlayerWeaponSelectionHUD3D : PlayerHUDBindingTarget3D
{
    private enum WeaponHudFillMode3D
    {
        None,
        ResourceAvailable,
        CooldownReadyProgress
    }

    [System.Serializable]
    private struct WeaponHudSlot3D
    {
        [Tooltip("Outline or weapon image that changes color/alpha when selected.")]
        public Image weaponImage;
        [Tooltip("Fill image paired with this weapon. Configure the Image as Filled in the editor.")]
        public Image selectBar;
        [Tooltip("Controls how this slot interprets the paired weapon state.")]
        public WeaponHudFillMode3D fillMode;
    }

    [System.Serializable]
    private struct WeaponHudVisualState3D
    {
        [Tooltip("Base alpha used while the weapon is not selected. Base color stays white.")]
        [Range(0f, 1f)]
        public float baseAlpha;
        [Tooltip("Tint applied while the weapon is selected.")]
        public Color selectedColor;
        [Tooltip("Alpha applied while the weapon is selected.")]
        [Range(0f, 1f)]
        public float selectedAlpha;
    }

    [System.Serializable]
    private struct WeaponHudFillVisualState3D
    {
        [Tooltip("Base fill color used for resource displays and for cooldown slots once they become ready.")]
        public Color readyFillColor;
        [Tooltip("Fill color used while a cooldown-driven slot is still recharging.")]
        public Color cooldownFillColor;
        [Tooltip("How long a cooldown-driven slot briefly flashes white once it becomes ready.")]
        public float readyFlashDuration;
    }

    [Header("Weapons HUD")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private WeaponHudSlot3D[] weaponSlots = new WeaponHudSlot3D[3];
    [SerializeField] private WeaponHudVisualState3D weaponImageVisuals = new WeaponHudVisualState3D
    {
        baseAlpha = 0.35f,
        selectedColor = Color.white,
        selectedAlpha = 1f
    };
    [SerializeField] private WeaponHudFillVisualState3D selectBarFillVisuals = new WeaponHudFillVisualState3D
    {
        readyFillColor = Color.white,
        cooldownFillColor = Color.red,
        readyFlashDuration = 0.1f
    };

    private bool[] _slotWasOnCooldown = System.Array.Empty<bool>();
    private float[] _slotReadyFlashEndTimes = System.Array.Empty<float>();

    protected override void Awake()
    {
        base.Awake();
        targetCanvas ??= GetComponentInParent<Canvas>(true);
        EnsureRuntimeState();
        BindRenderCamera();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BindRenderCamera();
    }

    private void Update()
    {
        UpdateReadyFlashTimers();
    }

    protected override void BindPlayer(Player3D player)
    {
        EnsureRuntimeState();
        ResetRuntimeState();
        BindRenderCamera();
        player.SelectedWeaponChanged += HandleSelectedWeaponChanged;
        player.WeaponAvailabilityChanged += HandleWeaponAvailabilityChanged;
        RefreshWeaponHUD(player.SelectedWeaponIndex);
    }

    protected override void UnbindPlayer(Player3D player)
    {
        player.SelectedWeaponChanged -= HandleSelectedWeaponChanged;
        player.WeaponAvailabilityChanged -= HandleWeaponAvailabilityChanged;
    }

    protected override void ClearBinding()
    {
        EnsureRuntimeState();
        BindRenderCamera();
        ResetRuntimeState();
        RefreshWeaponHUD(-1);
    }

    private void BindRenderCamera()
    {
        if (targetCanvas == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        targetCanvas.worldCamera = mainCamera;
    }

    private void HandleSelectedWeaponChanged(int selectedWeaponIndex)
    {
        RefreshWeaponHUD(selectedWeaponIndex);
    }

    private void HandleWeaponAvailabilityChanged(int weaponIndex, Weapon3D weapon)
    {
        RefreshWeaponFill(weaponIndex, weapon);
    }

    private void RefreshWeaponHUD(int selectedIndex)
    {
        EnsureRuntimeState();

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            bool isSelected = i == selectedIndex;
            ApplyVisualState(weaponSlots[i].weaponImage, weaponImageVisuals, isSelected);
            RefreshWeaponFill(i, BoundPlayer != null ? BoundPlayer.GetWeapon(i) : null);
        }
    }

    private static void ApplyVisualState(Image image, WeaponHudVisualState3D visuals, bool isSelected)
    {
        if (image == null)
        {
            return;
        }

        Color targetColor = isSelected ? visuals.selectedColor : Color.white;
        targetColor.a = isSelected ? visuals.selectedAlpha : visuals.baseAlpha;
        image.color = targetColor;
    }

    private void RefreshWeaponFill(int slotIndex, Weapon3D weapon)
    {
        if (slotIndex < 0 || slotIndex >= weaponSlots.Length)
        {
            return;
        }

        EnsureRuntimeState();

        Image selectBar = weaponSlots[slotIndex].selectBar;
        if (selectBar == null)
        {
            return;
        }

        WeaponHudFillMode3D fillMode = weaponSlots[slotIndex].fillMode;
        if (fillMode == WeaponHudFillMode3D.None || weapon == null)
        {
            _slotWasOnCooldown[slotIndex] = false;
            _slotReadyFlashEndTimes[slotIndex] = 0f;
            selectBar.fillAmount = 0f;
            selectBar.color = selectBarFillVisuals.readyFillColor;
            return;
        }

        float fillAmount;
        Color fillColor;
        bool isOnCooldown = false;

        switch (fillMode)
        {
            case WeaponHudFillMode3D.ResourceAvailable:
                fillAmount = weapon.AvailableResourceRatio;
                fillColor = selectBarFillVisuals.readyFillColor;
                break;

            case WeaponHudFillMode3D.CooldownReadyProgress:
                fillAmount = weapon.CooldownReadyRatio;
                isOnCooldown = weapon.UsesCooldownAvailability && weapon.CooldownRemaining > 0f;

                if (!isOnCooldown && _slotWasOnCooldown[slotIndex])
                {
                    _slotReadyFlashEndTimes[slotIndex] = Time.time + Mathf.Max(0f, selectBarFillVisuals.readyFlashDuration);
                }

                bool isFlashingReady = _slotReadyFlashEndTimes[slotIndex] > Time.time;
                fillColor = isOnCooldown
                    ? selectBarFillVisuals.cooldownFillColor
                    : isFlashingReady
                        ? Color.white
                        : selectBarFillVisuals.readyFillColor;
                break;

            default:
                fillAmount = 0f;
                fillColor = selectBarFillVisuals.readyFillColor;
                break;
        }

        _slotWasOnCooldown[slotIndex] = isOnCooldown;
        if (isOnCooldown)
        {
            _slotReadyFlashEndTimes[slotIndex] = 0f;
        }

        selectBar.fillAmount = Mathf.Clamp01(fillAmount);
        selectBar.color = fillColor;
    }

    private void UpdateReadyFlashTimers()
    {
        if (BoundPlayer == null)
        {
            return;
        }

        EnsureRuntimeState();

        bool needsRefresh = false;
        for (int i = 0; i < _slotReadyFlashEndTimes.Length; i++)
        {
            if (_slotReadyFlashEndTimes[i] <= 0f || Time.time < _slotReadyFlashEndTimes[i])
            {
                continue;
            }

            _slotReadyFlashEndTimes[i] = 0f;
            needsRefresh = true;
        }

        if (!needsRefresh)
        {
            return;
        }

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            RefreshWeaponFill(i, BoundPlayer.GetWeapon(i));
        }
    }

    private void EnsureRuntimeState()
    {
        if (_slotWasOnCooldown.Length == weaponSlots.Length && _slotReadyFlashEndTimes.Length == weaponSlots.Length)
        {
            return;
        }

        _slotWasOnCooldown = new bool[weaponSlots.Length];
        _slotReadyFlashEndTimes = new float[weaponSlots.Length];
    }

    private void ResetRuntimeState()
    {
        EnsureRuntimeState();

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            _slotWasOnCooldown[i] = false;
            _slotReadyFlashEndTimes[i] = 0f;
        }
    }
}
