using TMPro;
using UnityEngine;
using StarfallArena.UI;

public class PlayerHealthShieldHUD3D : PlayerHUDBindingTarget3D
{
    [Header("Health")]
    [SerializeField] private SegmentedBar healthBar;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Shield")]
    [SerializeField] private SegmentedBar shieldBar;
    [SerializeField] private TextMeshProUGUI shieldText;

    protected override void BindPlayer(Player3D player)
    {
        player.HealthChanged += HandleHealthChanged;
        player.ShieldChanged += HandleShieldChanged;
        InitializeBars(player);
        RefreshAll(player);
    }

    protected override void UnbindPlayer(Player3D player)
    {
        player.HealthChanged -= HandleHealthChanged;
        player.ShieldChanged -= HandleShieldChanged;
    }

    protected override void ClearBinding()
    {
        if (healthBar != null)
        {
            healthBar.InitializeBar(0f, 1f);
        }

        if (shieldBar != null)
        {
            shieldBar.InitializeBar(0f, 1f);
        }

        RefreshHealth(0f, 1f);
        RefreshShield(0f, 1f);
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        RefreshHealth(currentHealth, maxHealth);
    }

    private void HandleShieldChanged(float currentShield, float maxShield)
    {
        RefreshShield(currentShield, maxShield);
    }

    private void RefreshAll(Player3D player)
    {
        if (player == null)
        {
            ClearBinding();
            return;
        }

        RefreshHealth(player.CurrentHealth, player.MaxHealth);
        RefreshShield(player.CurrentShield, player.MaxShield);
    }

    private void InitializeBars(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        if (healthBar != null)
        {
            healthBar.InitializeBar(player.CurrentHealth, Mathf.Max(1f, player.MaxHealth));
        }

        if (shieldBar != null)
        {
            shieldBar.InitializeBar(player.CurrentShield, Mathf.Max(1f, player.MaxShield));
        }
    }

    private void RefreshHealth(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.UpdateBar(currentHealth, Mathf.Max(1f, maxHealth));
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(Mathf.Max(0f, currentHealth)).ToString();
        }
    }

    private void RefreshShield(float currentShield, float maxShield)
    {
        if (shieldBar != null)
        {
            shieldBar.UpdateBar(currentShield, Mathf.Max(1f, maxShield));
        }

        if (shieldText != null)
        {
            shieldText.text = Mathf.CeilToInt(Mathf.Max(0f, currentShield)).ToString();
        }
    }
}
