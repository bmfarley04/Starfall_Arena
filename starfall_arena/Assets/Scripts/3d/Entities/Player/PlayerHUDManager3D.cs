using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarfallArena.UI;

public class PlayerHUDManager3D : MonoBehaviour
{
    [System.Serializable]
    private struct WeaponHudSlot3D
    {
        [Tooltip("Outline or weapon image that changes color/alpha when selected.")]
        public Image weaponImage;
        [Tooltip("Selection bar image paired with this weapon.")]
        public Image selectBar;
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

    [Header("Player Binding")]
    [SerializeField] private Player3D player;

    [Header("Health")]
    [SerializeField] private SegmentedBar healthBar;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Shield")]
    [SerializeField] private SegmentedBar shieldBar;
    [SerializeField] private TextMeshProUGUI shieldText;

    [Header("Weapons HUD")]
    [SerializeField] private WeaponHudSlot3D[] weaponSlots = new WeaponHudSlot3D[3];
    [SerializeField] private WeaponHudVisualState3D weaponImageVisuals = new WeaponHudVisualState3D
    {
        baseAlpha = 0.35f,
        selectedColor = Color.white,
        selectedAlpha = 1f
    };
    [SerializeField] private WeaponHudVisualState3D selectBarVisuals = new WeaponHudVisualState3D
    {
        baseAlpha = 0.15f,
        selectedColor = Color.white,
        selectedAlpha = 1f
    };

    private int _lastSelectedWeaponIndex = -1;

    public Player3D BoundPlayer => player;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player3D>();
        }
    }

    private void OnEnable()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<Player3D>();
        }

        if (player != null)
        {
            player.BindHUD(this);
        }
        else
        {
            RefreshWeaponHUD(-1);
        }
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        int selectedIndex = player.SelectedWeaponIndex;
        if (selectedIndex != _lastSelectedWeaponIndex)
        {
            RefreshWeaponHUD(selectedIndex);
        }
    }

    public void Bind(Player3D targetPlayer)
    {
        player = targetPlayer;
        InitializeHUD();
    }

    public void RefreshHealth(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.UpdateBar(currentHealth, maxHealth);
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, currentHealth)).ToString();
        }
    }

    public void RefreshShield(float currentShield, float maxShield)
    {
        if (shieldBar != null)
        {
            shieldBar.UpdateBar(currentShield, maxShield);
        }

        if (shieldText != null)
        {
            shieldText.text = Mathf.CeilToInt(Mathf.Max(0f, currentShield)).ToString();
        }
    }

    public void RefreshWeaponHUD()
    {
        int selectedIndex = player != null ? player.SelectedWeaponIndex : -1;
        RefreshWeaponHUD(selectedIndex);
    }

    private void InitializeHUD()
    {
        if (player == null)
        {
            RefreshWeaponHUD(-1);
            return;
        }

        if (healthBar != null)
        {
            healthBar.InitializeBar(player.CurrentHealth, player.MaxHealth);
        }

        if (shieldBar != null)
        {
            shieldBar.InitializeBar(player.CurrentShield, player.MaxShield);
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, player.CurrentHealth)).ToString();
        }

        if (shieldText != null)
        {
            shieldText.text = Mathf.CeilToInt(Mathf.Max(0f, player.CurrentShield)).ToString();
        }

        RefreshWeaponHUD(player.SelectedWeaponIndex);
    }

    private void RefreshWeaponHUD(int selectedIndex)
    {
        _lastSelectedWeaponIndex = selectedIndex;

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            bool isSelected = i == selectedIndex;
            ApplyVisualState(weaponSlots[i].weaponImage, weaponImageVisuals, isSelected);
            ApplyVisualState(weaponSlots[i].selectBar, selectBarVisuals, isSelected);
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
}
