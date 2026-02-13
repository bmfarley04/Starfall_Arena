using UnityEngine;
using TMPro;
using StarfallArena.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Binding")]
    [Tooltip("Which player this HUD belongs to: 'Player1' or 'Player2'")]
    public string playerTag;

    [Header("Health")]
    public SegmentedBar healthBar;
    public TextMeshProUGUI healthText;

    [Header("Shield")]
    public SegmentedBar shieldBar;
    public TextMeshProUGUI shieldText;
}
