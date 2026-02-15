using UnityEngine;

namespace StarfallArena.UI
{
    public class AbilityHUDPanel : MonoBehaviour
    {
        [Header("Ability Slots")]
        [Tooltip("UI slot for ability 1")]
        [SerializeField] private AbilitySlotUI slot1;
        [Tooltip("UI slot for ability 2")]
        [SerializeField] private AbilitySlotUI slot2;
        [Tooltip("UI slot for ability 3")]
        [SerializeField] private AbilitySlotUI slot3;
        [Tooltip("UI slot for ability 4")]
        [SerializeField] private AbilitySlotUI slot4;

        public void Bind(Player player)
        {
            if (player == null)
            {
                Debug.LogWarning("AbilityHUDPanel.Bind called with null player!", this);
                return;
            }

            if (slot1 != null) slot1.Bind(player, 1);
            if (slot2 != null) slot2.Bind(player, 2);
            if (slot3 != null) slot3.Bind(player, 3);
            if (slot4 != null) slot4.Bind(player, 4);
        }

        public void BindSlot4(Ability ability)
        {
            if (slot4 != null) slot4.Bind(ability);
        }
    }
}
