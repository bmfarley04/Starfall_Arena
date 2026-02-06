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
