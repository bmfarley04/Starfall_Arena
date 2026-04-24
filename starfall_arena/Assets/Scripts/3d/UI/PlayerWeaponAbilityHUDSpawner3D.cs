using UnityEngine;

public class PlayerWeaponAbilityHUDSpawner3D : PlayerHUDBindingTarget3D
{
    [Tooltip("Temporary direct prefab hook. Replace this with a ShipData3D-driven lookup once the 3D ship data asset exists.")]
    [SerializeField] private GameObject playerWeaponAbilityHudPrefab;
    [SerializeField] private Transform instanceParent;
    [SerializeField] private bool destroyInstanceWhenPlayerUnbinds = true;

    private GameObject _instance;

    protected override void BindPlayer(Player3D player)
    {
        EnsureInstance();
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

    private void EnsureInstance()
    {
        if (_instance != null || playerWeaponAbilityHudPrefab == null)
        {
            return;
        }

        Transform parent = instanceParent != null ? instanceParent : transform;
        _instance = Instantiate(playerWeaponAbilityHudPrefab, parent);
        _instance.name = $"{playerWeaponAbilityHudPrefab.name}(Runtime)";
    }

    private void DestroyInstance()
    {
        if (_instance == null)
        {
            return;
        }

        Destroy(_instance);
        _instance = null;
    }
}
