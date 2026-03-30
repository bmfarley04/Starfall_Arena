using System;
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
    [SerializeField] private bool autoBindToMatchingPlayer = true;
    [Tooltip("Optional tag filter such as Player1 or Player2.")]
    [SerializeField] private string playerTagFilter;
    [Tooltip("Optional case-insensitive name match. Example: class1 matches 3d_class1_player(Clone).")]
    [SerializeField] private string playerNameContains;

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
    private bool _isSubscribedToPlayerEvents;
    private Player3D _boundPlayer;

    public Player3D BoundPlayer => _boundPlayer;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player3D>();
        }
    }

    private void OnEnable()
    {
        Player3D.PlayerSpawned += HandlePlayerSpawned;
        Player3D.PlayerDespawned += HandlePlayerDespawned;
        TryBindToPlayer();
    }

    private void OnDisable()
    {
        Player3D.PlayerSpawned -= HandlePlayerSpawned;
        Player3D.PlayerDespawned -= HandlePlayerDespawned;
        UnsubscribeFromPlayerEvents();
    }

    public void Bind(Player3D targetPlayer)
    {
        if (ReferenceEquals(_boundPlayer, targetPlayer) && _isSubscribedToPlayerEvents)
        {
            InitializeHUD();
            return;
        }

        UnsubscribeFromPlayerEvents();
        _boundPlayer = targetPlayer;

        if (_boundPlayer != null)
        {
            SubscribeToPlayerEvents();
        }

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
        int selectedIndex = _boundPlayer != null ? _boundPlayer.SelectedWeaponIndex : -1;
        RefreshWeaponHUD(selectedIndex);
    }

    private void InitializeHUD()
    {
        if (_boundPlayer == null)
        {
            ClearHUD();
            return;
        }

        if (healthBar != null)
        {
            healthBar.InitializeBar(_boundPlayer.CurrentHealth, _boundPlayer.MaxHealth);
        }

        if (shieldBar != null)
        {
            shieldBar.InitializeBar(_boundPlayer.CurrentShield, _boundPlayer.MaxShield);
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, _boundPlayer.CurrentHealth)).ToString();
        }

        if (shieldText != null)
        {
            shieldText.text = Mathf.CeilToInt(Mathf.Max(0f, _boundPlayer.CurrentShield)).ToString();
        }

        RefreshWeaponHUD(_boundPlayer.SelectedWeaponIndex);
    }

    private void ClearHUD()
    {
        if (healthBar != null)
        {
            healthBar.InitializeBar(0f, 1f);
        }

        if (shieldBar != null)
        {
            shieldBar.InitializeBar(0f, 1f);
        }

        if (healthText != null)
        {
            healthText.text = "0";
        }

        if (shieldText != null)
        {
            shieldText.text = "0";
        }

        RefreshWeaponHUD(-1);
    }

    private void SubscribeToPlayerEvents()
    {
        if (_boundPlayer == null || _isSubscribedToPlayerEvents)
        {
            return;
        }

        _boundPlayer.HealthChanged += HandleHealthChanged;
        _boundPlayer.ShieldChanged += HandleShieldChanged;
        _boundPlayer.SelectedWeaponChanged += HandleSelectedWeaponChanged;
        _isSubscribedToPlayerEvents = true;
    }

    private void UnsubscribeFromPlayerEvents()
    {
        if (_boundPlayer == null || !_isSubscribedToPlayerEvents)
        {
            return;
        }

        _boundPlayer.HealthChanged -= HandleHealthChanged;
        _boundPlayer.ShieldChanged -= HandleShieldChanged;
        _boundPlayer.SelectedWeaponChanged -= HandleSelectedWeaponChanged;
        _isSubscribedToPlayerEvents = false;
    }

    private void HandlePlayerSpawned(Player3D spawnedPlayer)
    {
        if (spawnedPlayer == null || _isSubscribedToPlayerEvents)
        {
            return;
        }

        if (player != null)
        {
            if (ReferenceEquals(player, spawnedPlayer))
            {
                Bind(spawnedPlayer);
            }

            return;
        }

        if (MatchesBindingCriteria(spawnedPlayer))
        {
            Bind(spawnedPlayer);
        }
    }

    private void HandlePlayerDespawned(Player3D despawnedPlayer)
    {
        if (!ReferenceEquals(_boundPlayer, despawnedPlayer))
        {
            return;
        }

        UnsubscribeFromPlayerEvents();
        _boundPlayer = null;

        if (!TryBindToPlayer())
        {
            ClearHUD();
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        RefreshHealth(currentHealth, maxHealth);
    }

    private void HandleShieldChanged(float currentShield, float maxShield)
    {
        RefreshShield(currentShield, maxShield);
    }

    private void HandleSelectedWeaponChanged(int selectedWeaponIndex)
    {
        RefreshWeaponHUD(selectedWeaponIndex);
    }

    private bool TryBindToPlayer()
    {
        if (_boundPlayer != null)
        {
            if (_boundPlayer.isActiveAndEnabled)
            {
                Bind(_boundPlayer);
                return true;
            }
        }

        if (player != null)
        {
            if (player.isActiveAndEnabled)
            {
                Bind(player);
                return true;
            }

            ClearHUD();
            return false;
        }

        if (!autoBindToMatchingPlayer)
        {
            ClearHUD();
            return false;
        }

        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (!MatchesBindingCriteria(players[i]))
            {
                continue;
            }

            Bind(players[i]);
            return true;
        }

        ClearHUD();
        return false;
    }

    private bool MatchesBindingCriteria(Player3D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTagFilter) && !candidate.CompareTag(playerTagFilter))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerNameContains)
            && candidate.name.IndexOf(playerNameContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return true;
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
