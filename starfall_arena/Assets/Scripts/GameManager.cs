using System.Collections.Generic;
using UnityEngine;
using StarfallArena.UI;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    [Header("Ship Registry")]
    [SerializeField] private ShipData[] knownShips;

    [Header("Augment Registry")]
    [SerializeField] private Augment[] knownAugments;

    public List<ShipData> selectedShipClasses = new List<ShipData>();

    private readonly Dictionary<string, ShipData> _shipsById = new Dictionary<string, ShipData>();
    private readonly Dictionary<string, Augment> _augmentsById = new Dictionary<string, Augment>();

    public IReadOnlyList<ShipData> KnownShips => knownShips;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        if (knownShips == null || knownShips.Length == 0)
        {
            return null;
        }

        return knownShips[Random.Range(0, knownShips.Length)];
    }

    private void RebuildLookups()
    {
        _shipsById.Clear();
        _augmentsById.Clear();

        if (knownShips != null)
        {
            foreach (ShipData ship in knownShips)
            {
                if (ship == null || string.IsNullOrWhiteSpace(ship.ShipId))
                {
                    continue;
                }

                _shipsById[ship.ShipId] = ship;
            }
        }

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
}
