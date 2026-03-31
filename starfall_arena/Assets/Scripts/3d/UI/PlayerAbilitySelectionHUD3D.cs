using UnityEngine;
using UnityEngine.UI;

public class PlayerAbilitySelectionHUD3D : PlayerHUDBindingTarget3D
{
    [System.Serializable]
    private struct AbilityHudSlot3D
    {
        [Tooltip("Surrounding box image for this ability slot.")]
        public Image abilityImage;
        [Tooltip("Cooldown fill image for this ability slot. Configure the Image as Filled in the editor.")]
        public Image selectBar;
    }

    [System.Serializable]
    private struct AbilityHudVisualState3D
    {
        [Tooltip("Base alpha used while the ability is unavailable or cooling down. Base color stays white.")]
        [Range(0f, 1f)]
        public float baseAlpha;
        [Tooltip("Tint applied once the ability is ready.")]
        public Color readyColor;
        [Tooltip("Alpha applied once the ability is ready.")]
        [Range(0f, 1f)]
        public float readyAlpha;
        [Tooltip("How long the ability box flashes white once the cooldown finishes.")]
        public float readyFlashDuration;
        [Tooltip("Alpha used during the white ready-flash on the box.")]
        [Range(0f, 1f)]
        public float readyFlashAlpha;
    }

    [System.Serializable]
    private struct AbilityHudFillVisualState3D
    {
        [Tooltip("Fill color used while an ability is still cooling down.")]
        public Color cooldownFillColor;
        [Tooltip("Fill color used once an ability becomes ready.")]
        public Color readyFillColor;
    }

    [Header("Abilities HUD")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private AbilityHudSlot3D[] abilitySlots = new AbilityHudSlot3D[2];
    [SerializeField] private AbilityHudVisualState3D abilityImageVisuals = new AbilityHudVisualState3D
    {
        baseAlpha = 0.35f,
        readyColor = Color.white,
        readyAlpha = 1f,
        readyFlashDuration = 0.1f,
        readyFlashAlpha = 1f
    };
    [SerializeField] private AbilityHudFillVisualState3D selectBarFillVisuals = new AbilityHudFillVisualState3D
    {
        cooldownFillColor = Color.red,
        readyFillColor = Color.white
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
        SubscribeToAbilityAvailability(player);
        RefreshAbilityHUD();
    }

    protected override void UnbindPlayer(Player3D player)
    {
        UnsubscribeFromAbilityAvailability(player);
    }

    protected override void ClearBinding()
    {
        EnsureRuntimeState();
        BindRenderCamera();
        ResetRuntimeState();
        RefreshAbilityHUD();
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

    private void SubscribeToAbilityAvailability(Player3D player)
    {
        for (int i = 0; i < abilitySlots.Length; i++)
        {
            Ability3D ability = player.GetAbility(i);
            if (ability == null)
            {
                continue;
            }

            ability.AvailabilityChanged -= HandleAbilityAvailabilityChanged;
            ability.AvailabilityChanged += HandleAbilityAvailabilityChanged;
        }
    }

    private void UnsubscribeFromAbilityAvailability(Player3D player)
    {
        for (int i = 0; i < abilitySlots.Length; i++)
        {
            Ability3D ability = player.GetAbility(i);
            if (ability == null)
            {
                continue;
            }

            ability.AvailabilityChanged -= HandleAbilityAvailabilityChanged;
        }
    }

    private void HandleAbilityAvailabilityChanged(Ability3D ability)
    {
        if (BoundPlayer == null || ability == null)
        {
            return;
        }

        for (int i = 0; i < abilitySlots.Length; i++)
        {
            if (!ReferenceEquals(BoundPlayer.GetAbility(i), ability))
            {
                continue;
            }

            RefreshAbilitySlot(i, ability);
            return;
        }
    }

    private void RefreshAbilityHUD()
    {
        for (int i = 0; i < abilitySlots.Length; i++)
        {
            RefreshAbilitySlot(i, BoundPlayer != null ? BoundPlayer.GetAbility(i) : null);
        }
    }

    private void RefreshAbilitySlot(int slotIndex, Ability3D ability)
    {
        if (slotIndex < 0 || slotIndex >= abilitySlots.Length)
        {
            return;
        }

        EnsureRuntimeState();

        Image abilityImage = abilitySlots[slotIndex].abilityImage;
        Image selectBar = abilitySlots[slotIndex].selectBar;

        if (ability == null || ability.isLocked)
        {
            _slotWasOnCooldown[slotIndex] = false;
            _slotReadyFlashEndTimes[slotIndex] = 0f;

            if (abilityImage != null)
            {
                ApplyAbilityVisualState(abilityImage, isReady: false, isFlashingReady: false);
            }

            if (selectBar != null)
            {
                selectBar.fillAmount = 0f;
                selectBar.color = selectBarFillVisuals.cooldownFillColor;
            }

            return;
        }

        bool isOnCooldown = ability.CooldownRemaining > 0f;
        if (!isOnCooldown && _slotWasOnCooldown[slotIndex])
        {
            _slotReadyFlashEndTimes[slotIndex] = Time.time + Mathf.Max(0f, abilityImageVisuals.readyFlashDuration);
        }

        bool isFlashingReady = _slotReadyFlashEndTimes[slotIndex] > Time.time;
        _slotWasOnCooldown[slotIndex] = isOnCooldown;

        if (abilityImage != null)
        {
            ApplyAbilityVisualState(abilityImage, isReady: !isOnCooldown, isFlashingReady: isFlashingReady);
        }

        if (selectBar != null)
        {
            selectBar.fillAmount = Mathf.Clamp01(ability.CooldownReadyRatio);
            selectBar.color = isOnCooldown
                ? selectBarFillVisuals.cooldownFillColor
                : selectBarFillVisuals.readyFillColor;
        }
    }

    private void ApplyAbilityVisualState(Image image, bool isReady, bool isFlashingReady)
    {
        if (image == null)
        {
            return;
        }

        Color targetColor;
        if (isFlashingReady)
        {
            targetColor = Color.white;
            targetColor.a = abilityImageVisuals.readyFlashAlpha;
        }
        else if (isReady)
        {
            targetColor = abilityImageVisuals.readyColor;
            targetColor.a = abilityImageVisuals.readyAlpha;
        }
        else
        {
            targetColor = Color.white;
            targetColor.a = abilityImageVisuals.baseAlpha;
        }

        image.color = targetColor;
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

        RefreshAbilityHUD();
    }

    private void EnsureRuntimeState()
    {
        if (_slotWasOnCooldown.Length == abilitySlots.Length && _slotReadyFlashEndTimes.Length == abilitySlots.Length)
        {
            return;
        }

        _slotWasOnCooldown = new bool[abilitySlots.Length];
        _slotReadyFlashEndTimes = new float[abilitySlots.Length];
    }

    private void ResetRuntimeState()
    {
        EnsureRuntimeState();

        for (int i = 0; i < abilitySlots.Length; i++)
        {
            _slotWasOnCooldown[i] = false;
            _slotReadyFlashEndTimes[i] = 0f;
        }
    }
}
