using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponAbilityHUDSpawner3D : PlayerHUDBindingTarget3D
{
    [Tooltip("Optional fallback prefab used when the bound player's 3D ShipData does not provide an ability HUD prefab.")]
    [SerializeField] private GameObject playerWeaponAbilityHudPrefab;
    [SerializeField] private Transform instanceParent;
    [SerializeField] private bool destroyInstanceWhenPlayerUnbinds = true;

    private GameObject _instance;
    private GameObject _activePrefab;

    protected override void BindPlayer(Player3D player)
    {
        EnsureInstance(player);
    }

    protected override void UnbindPlayer(Player3D player)
    {
        if (destroyInstanceWhenPlayerUnbinds)
        {
            DestroyInstance();
        }
    }

    protected override void ClearBinding()
    {
        if (destroyInstanceWhenPlayerUnbinds)
        {
            DestroyInstance();
        }
    }

    private void EnsureInstance(Player3D player)
    {
        GameObject resolvedPrefab = ResolveHudPrefab(player);
        if (resolvedPrefab == null)
        {
            return;
        }

        if (_instance != null && ReferenceEquals(_activePrefab, resolvedPrefab))
        {
            return;
        }

        DestroyInstance();

        Transform parent = instanceParent != null ? instanceParent : transform;
        _instance = Instantiate(resolvedPrefab, parent);
        _instance.name = $"{resolvedPrefab.name}(Runtime)";
        _activePrefab = resolvedPrefab;
    }

    private void DestroyInstance()
    {
        if (_instance == null)
        {
            return;
        }

        Destroy(_instance);
        _instance = null;
        _activePrefab = null;
    }

    private GameObject ResolveHudPrefab(Player3D player)
    {
        ShipData shipData = ResolveShipData(player);
        if (shipData != null && shipData.abilityHUDPrefab != null)
        {
            return shipData.abilityHUDPrefab;
        }

        return playerWeaponAbilityHudPrefab;
    }

    private ShipData ResolveShipData(Player3D player)
    {
        if (player == null || GameDataManager.Instance == null)
        {
            return null;
        }

        string prefabName = player.gameObject.name.Replace("(Clone)", string.Empty).Trim();
        IReadOnlyList<ShipData> knownShips = GameDataManager.Instance.Known3DShips;
        if (knownShips == null)
        {
            return null;
        }

        for (int i = 0; i < knownShips.Count; i++)
        {
            ShipData ship = knownShips[i];
            if (ship == null || ship.shipPrefab == null)
            {
                continue;
            }

            if (string.Equals(ship.shipPrefab.name, prefabName, System.StringComparison.OrdinalIgnoreCase))
            {
                return ship;
            }
        }

        return null;
    }
}
