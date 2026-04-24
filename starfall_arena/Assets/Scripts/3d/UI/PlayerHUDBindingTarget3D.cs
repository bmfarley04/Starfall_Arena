using UnityEngine;

public abstract class PlayerHUDBindingTarget3D : MonoBehaviour
{
    [SerializeField] private PlayerHUDManager3D hudManager;

    protected Player3D BoundPlayer { get; private set; }
    protected PlayerHUDManager3D HUDManager => hudManager;

    protected virtual void Awake()
    {
        hudManager ??= GetComponentInParent<PlayerHUDManager3D>(true);
    }

    protected virtual void OnEnable()
    {
        if (hudManager != null)
        {
            hudManager.BoundPlayerChanged += HandleBoundPlayerChanged;
            ApplyBoundPlayer(hudManager.BoundPlayer);
            return;
        }

        ApplyBoundPlayer(null);
    }

    protected virtual void OnDisable()
    {
        if (hudManager != null)
        {
            hudManager.BoundPlayerChanged -= HandleBoundPlayerChanged;
        }

        ApplyBoundPlayer(null);
    }

    private void HandleBoundPlayerChanged(Player3D player)
    {
        ApplyBoundPlayer(player);
    }

    private void ApplyBoundPlayer(Player3D player)
    {
        if (ReferenceEquals(BoundPlayer, player))
        {
            RefreshBoundPlayer(player);
            return;
        }

        if (BoundPlayer != null)
        {
            UnbindPlayer(BoundPlayer);
        }

        BoundPlayer = player;

        if (BoundPlayer != null)
        {
            BindPlayer(BoundPlayer);
            return;
        }

        ClearBinding();
    }

    protected virtual void RefreshBoundPlayer(Player3D player)
    {
    }

    protected abstract void BindPlayer(Player3D player);
    protected abstract void UnbindPlayer(Player3D player);
    protected abstract void ClearBinding();
}
