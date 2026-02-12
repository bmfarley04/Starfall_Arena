using UnityEngine;

/// <summary>
/// ScriptableObject containing all data for a ship displayed in the ship selection screen.
/// Stores stats, abilities, visual references, and metadata for UI population.
/// </summary>
[CreateAssetMenu(fileName = "NewShipData", menuName = "Starfall Arena/Ship Data", order = 0)]
public class ShipData : ScriptableObject
{
    [System.Serializable]
    public struct ShipStats
    {
        [Tooltip("Primary weapon damage value (0-100)")]
        [Range(0f, 50f)]
        public float damage;

        [Tooltip("Hull health value (0-100)")]
        [Range(0f, 500f)]
        public float hull;

        [Tooltip("Shield capacity value (0-100)")]
        [Range(0f, 500f)]
        public float shield;

        [Tooltip("Movement speed value (0-100)")]
        [Range(0f, 100f)]
        public float speed;
    }

    [System.Serializable]
    public struct AbilityData
    {
        [Tooltip("Name of the ability displayed in tooltip")]
        public string abilityName;

        [Tooltip("Description of the ability displayed in tooltip")]
        [TextArea(2, 4)]
        public string abilityDescription;

        [Tooltip("Icon sprite displayed on the ability button")]
        public Sprite abilityIcon;
    }

    [Header("Ship Identity")]
    [Tooltip("Display name shown at top of ship select screen (e.g., 'VX-ATLAS')")]
    public string shipName;

    [Tooltip("Ship model prefab spawned for preview")]
    public GameObject shipModelPrefab;
    [Tooltip("Ship model prefab spawned for preview")]
    public GameObject player1VSPrefab;
    [Tooltip("JUST USED IN VS SCREEN")]
    public GameObject player2VSPrefab;
    [Tooltip("JUST USED IN VS SCREEN")]
    public GameObject shipPrefab;

    [Header("VS Screen Position Offsets")]
    [Tooltip("Additive position offset when this ship is displayed on the Player 1 VS card")]
    public Vector3 player1VSPositionOffset;

    [Tooltip("Additive position offset when this ship is displayed on the Player 2 VS card")]
    public Vector3 player2VSPositionOffset;

    [Header("Ability HUD")]
    [Tooltip("Canvas prefab for this ship's ability HUD (unique per ship class)")]
    public GameObject abilityHUDPrefab;

    [Header("Ship Stats")]
    [Tooltip("Stat values displayed as bars (0-100 range)")]
    public ShipStats stats;

    [Header("Abilities")]
    [Tooltip("Ability 1 data (top-left ability button)")]
    public AbilityData ability1;

    [Tooltip("Ability 2 data (top-right ability button)")]
    public AbilityData ability2;

    [Tooltip("Ability 3 data (bottom-left ability button)")]
    public AbilityData ability3;

    [Tooltip("Ability 4 data (bottom-right ability button)")]
    public AbilityData ability4;
}
