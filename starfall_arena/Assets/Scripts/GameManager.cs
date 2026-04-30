using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;
using System;

public class GameDataManager : MonoBehaviour
{
    public enum ShipRosterMode
    {
        Duel2D = 0,
        Duel3D = 1
    }

    public static GameDataManager Instance { get; private set; }

    [Header("Ship Registry - 2D")]
    [SerializeField] private ShipData[] known2DShips;

    [Header("Ship Registry - 3D")]
    [SerializeField] private ShipData[] known3DShips;

    [Header("Ship Registry Mode")]
    [SerializeField] private ShipRosterMode defaultShipRosterMode = ShipRosterMode.Duel2D;
    [Tooltip("Primary 3D gameplay scene name used to decide when the 3D ship roster should be active.")]
    [SerializeField] private string threeDGameplaySceneName = "3d";
    [Tooltip("Additional gameplay scene names that should still use the 3D ship roster, such as 3D invasion or dedicated 3D test scenes.")]
    [SerializeField] private string[] additional3DGameplaySceneNames = { "3d_invasion", "3dscene" };

    [Header("Augment Registry")]
    [SerializeField] private Augment[] knownAugments;

    public List<ShipData> selectedShipClasses = new List<ShipData>();

    private readonly Dictionary<string, ShipData> _shipsById = new Dictionary<string, ShipData>();
    private readonly Dictionary<string, Augment> _augmentsById = new Dictionary<string, Augment>();

    private ShipRosterMode _activeShipRosterMode = ShipRosterMode.Duel2D;

    public ShipRosterMode ActiveShipRosterMode => _activeShipRosterMode;
    public IReadOnlyList<ShipData> Known2DShips => known2DShips;
    public IReadOnlyList<ShipData> Known3DShips => known3DShips;
    public IReadOnlyList<ShipData> KnownShips => GetActiveShipRoster();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _activeShipRosterMode = defaultShipRosterMode;
        RebuildLookups();
    }

    private void OnValidate()
    {
        RebuildLookups();
    }

    public void SetSelectedShips(ShipData player1Ship, ShipData player2Ship)
    {
        selectedShipClasses.Clear();

        if (player1Ship != null)
        {
            selectedShipClasses.Add(player1Ship);
        }

        if (player2Ship != null)
        {
            selectedShipClasses.Add(player2Ship);
        }
    }

    public ShipData GetShipById(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        if (_shipsById.Count == 0)
        {
            RebuildLookups();
        }

        _shipsById.TryGetValue(shipId, out ShipData ship);
        return ship;
    }

    public Augment GetAugmentById(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return null;
        }

        if (_augmentsById.Count == 0)
        {
            RebuildLookups();
        }

        _augmentsById.TryGetValue(augmentId, out Augment augment);
        return augment;
    }

    public ShipData GetRandomShip()
    {
        ShipData[] activeRoster = GetActiveShipRoster();
        if (activeRoster == null || activeRoster.Length == 0)
        {
            return null;
        }

        return activeRoster[UnityEngine.Random.Range(0, activeRoster.Length)];
    }

    public ShipData[] GetActiveShipRoster()
    {
        return _activeShipRosterMode == ShipRosterMode.Duel3D ? known3DShips : known2DShips;
    }

    public void SetShipRosterMode(ShipRosterMode mode)
    {
        _activeShipRosterMode = mode;
    }

    public void SetShipRosterForGameplayScene(string gameplaySceneName)
    {
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            SetShipRosterMode(defaultShipRosterMode);
            return;
        }

        bool is3DScene = Is3DGameplayScene(gameplaySceneName);

        SetShipRosterMode(is3DScene ? ShipRosterMode.Duel3D : ShipRosterMode.Duel2D);
    }

    private bool Is3DGameplayScene(string gameplaySceneName)
    {
        string trimmedSceneName = gameplaySceneName.Trim();
        if (string.Equals(trimmedSceneName, threeDGameplaySceneName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (additional3DGameplaySceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < additional3DGameplaySceneNames.Length; i++)
        {
            string configuredSceneName = additional3DGameplaySceneNames[i];
            if (string.IsNullOrWhiteSpace(configuredSceneName))
            {
                continue;
            }

            if (string.Equals(trimmedSceneName, configuredSceneName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildLookups()
    {
        _shipsById.Clear();
        _augmentsById.Clear();

        AddShipsToLookup(known2DShips);
        AddShipsToLookup(known3DShips);

        if (knownAugments != null)
        {
            foreach (Augment augment in knownAugments)
            {
                if (augment == null || string.IsNullOrWhiteSpace(augment.augmentID))
                {
                    continue;
                }

                _augmentsById[augment.augmentID] = augment;
            }
        }
    }

    private void AddShipsToLookup(ShipData[] ships)
    {
        if (ships == null)
        {
            return;
        }

        foreach (ShipData ship in ships)
        {
            if (ship == null || string.IsNullOrWhiteSpace(ship.ShipId))
            {
                continue;
            }

            _shipsById[ship.ShipId] = ship;
        }
    }
}
