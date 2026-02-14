using UnityEngine;

namespace StarfallArena.UI
{
    /// <summary>
    /// Tracks round wins for a single player by toggling mask GameObjects
    /// inside win indicator rectangles. Each rectangle has a mask child that,
    /// when active, makes the rectangle look "empty." Deactivating the mask
    /// reveals the filled state.
    /// </summary>
    public class WinTracker : MonoBehaviour
    {
        [Header("Win Indicator Masks")]
        [Tooltip("Ordered array of mask GameObjects inside each win rectangle. " +
                 "Index 0 = first win slot, index 1 = second, etc. " +
                 "Active mask = empty/unfilled, inactive mask = filled/won.")]
        [SerializeField] private GameObject[] winMasks;

        /// <summary>
        /// Sets the number of filled (won) indicators.
        /// Masks at indices below <paramref name="wins"/> are deactivated (filled),
        /// masks at indices >= <paramref name="wins"/> are activated (empty).
        /// </summary>
        public void SetWins(int wins)
        {
            if (winMasks == null) return;

            for (int i = 0; i < winMasks.Length; i++)
            {
                if (winMasks[i] == null) continue;
                // Deactivate mask = filled (won), activate mask = empty
                winMasks[i].SetActive(i >= wins);
            }
        }

        /// <summary>
        /// Resets all indicators to empty (all masks active).
        /// </summary>
        public void ResetAll()
        {
            SetWins(0);
        }
    }
}
