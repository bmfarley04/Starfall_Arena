using UnityEngine;
using UnityEngine.UI;

public class TeammateHealthShieldHUD3D : PlayerHUDBindingTarget3D
{
    [Header("Teammate Binding")]
    [Tooltip("If enabled, the teammate HUD hides until another same-faction Player3D proxy exists for the local player.")]
    [SerializeField] private bool hideWhenNoTeammate = true;

    [Tooltip("How often the HUD retries teammate discovery after network spawn, despawn, or respawn timing changes.")]
    [SerializeField] private float teammateRefreshInterval = 0.25f;

    [Header("Health")]
    [Tooltip("Filled Image used for the teammate hull value. This should be the foreground fill image, not the frame/background.")]
    [SerializeField] private Image healthFillImage;

    [Header("Shield")]
    [Tooltip("Filled Image used for the teammate shield value. This should be the foreground fill image, not the frame/background.")]
    [SerializeField] private Image shieldFillImage;

    [Header("Presentation")]
    [Tooltip("Optional CanvasGroup controlling visibility for the whole teammate indicator. If left empty, one is found or added on this GameObject at runtime.")]
    [SerializeField] private CanvasGroup visibilityGroup;

    [Tooltip("If enabled, assigned Images are forced to Filled/Horizontal so fillAmount changes are visible.")]
    [SerializeField] private bool autoConfigureFillImages = true;

    private Player3D _teammate;
    private float _nextTeammateRefreshTime;

    protected override void Awake()
    {
        base.Awake();
        visibilityGroup ??= GetComponent<CanvasGroup>();
        if (visibilityGroup == null)
        {
            visibilityGroup = gameObject.AddComponent<CanvasGroup>();
        }

        ConfigureFillImage(healthFillImage);
        ConfigureFillImage(shieldFillImage);
        SetVisible(!hideWhenNoTeammate);
    }

    protected override void OnEnable()
    {
        Player3D.PlayerSpawned += HandlePlayerSpawned;
        Player3D.PlayerDespawned += HandlePlayerDespawned;
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        Player3D.PlayerSpawned -= HandlePlayerSpawned;
        Player3D.PlayerDespawned -= HandlePlayerDespawned;
        UnbindTeammate();
        base.OnDisable();
    }

    private void Update()
    {
        if (BoundPlayer == null)
        {
            return;
        }

        if (_teammate != null && _teammate.isActiveAndEnabled)
        {
            return;
        }

        if (Time.time < _nextTeammateRefreshTime)
        {
            return;
        }

        _nextTeammateRefreshTime = Time.time + Mathf.Max(0.05f, teammateRefreshInterval);
        RefreshTeammateBinding();
    }

    protected override void BindPlayer(Player3D player)
    {
        RefreshTeammateBinding();
    }

    protected override void UnbindPlayer(Player3D player)
    {
        UnbindTeammate();
    }

    protected override void ClearBinding()
    {
        UnbindTeammate();
        RefreshHealth(0f, 1f);
        RefreshShield(0f, 1f);
        SetVisible(!hideWhenNoTeammate);
    }

    protected override void RefreshBoundPlayer(Player3D player)
    {
        RefreshTeammateBinding();
    }

    private void HandlePlayerSpawned(Player3D player)
    {
        if (BoundPlayer == null || player == BoundPlayer)
        {
            return;
        }

        RefreshTeammateBinding();
    }

    private void HandlePlayerDespawned(Player3D player)
    {
        if (player != _teammate)
        {
            return;
        }

        UnbindTeammate();
        RefreshTeammateBinding();
    }

    private void RefreshTeammateBinding()
    {
        Player3D teammate = FindTeammate(BoundPlayer);
        if (teammate == _teammate)
        {
            RefreshAll(_teammate);
            SetVisible(_teammate != null || !hideWhenNoTeammate);
            return;
        }

        UnbindTeammate();
        BindTeammate(teammate);
    }

    private Player3D FindTeammate(Player3D localPlayer)
    {
        if (localPlayer == null)
        {
            return null;
        }

        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            Player3D candidate = players[i];
            if (candidate == null || candidate == localPlayer || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (FactionMember3D.AreAllied(localPlayer, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void BindTeammate(Player3D teammate)
    {
        _teammate = teammate;

        if (_teammate == null)
        {
            RefreshHealth(0f, 1f);
            RefreshShield(0f, 1f);
            SetVisible(!hideWhenNoTeammate);
            return;
        }

        _teammate.HealthChanged += HandleHealthChanged;
        _teammate.ShieldChanged += HandleShieldChanged;
        RefreshAll(_teammate);
        SetVisible(true);
    }

    private void UnbindTeammate()
    {
        if (_teammate == null)
        {
            return;
        }

        _teammate.HealthChanged -= HandleHealthChanged;
        _teammate.ShieldChanged -= HandleShieldChanged;
        _teammate = null;
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
            RefreshHealth(0f, 1f);
            RefreshShield(0f, 1f);
            return;
        }

        RefreshHealth(player.CurrentHealth, player.MaxHealth);
        RefreshShield(player.CurrentShield, player.MaxShield);
    }

    private void RefreshHealth(float currentHealth, float maxHealth)
    {
        SetFillAmount(healthFillImage, currentHealth, maxHealth);
    }

    private void RefreshShield(float currentShield, float maxShield)
    {
        SetFillAmount(shieldFillImage, currentShield, maxShield);
    }

    private void SetFillAmount(Image image, float currentValue, float maxValue)
    {
        if (image == null)
        {
            return;
        }

        float safeMax = Mathf.Max(1f, maxValue);
        image.fillAmount = Mathf.Clamp01(Mathf.Max(0f, currentValue) / safeMax);
    }

    private void ConfigureFillImage(Image image)
    {
        if (!autoConfigureFillImages || image == null)
        {
            return;
        }

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
    }

    private void SetVisible(bool visible)
    {
        if (visibilityGroup == null)
        {
            return;
        }

        visibilityGroup.alpha = visible ? 1f : 0f;
        visibilityGroup.interactable = false;
        visibilityGroup.blocksRaycasts = false;
    }
}
