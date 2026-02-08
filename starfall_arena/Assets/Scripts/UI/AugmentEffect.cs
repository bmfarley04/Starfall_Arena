using UnityEngine;

namespace StarfallArena.UI
{
    /// <summary>
    /// Abstract base class for augment effects.
    /// Extend this class to create specific augment behaviors.
    /// Attach derived scripts to GameObjects, then reference them in Augment ScriptableObjects.
    /// </summary>
    public abstract class AugmentEffect : MonoBehaviour
    {
        /// <summary>
        /// Called when this augment is selected by the player.
        /// Implement augment-specific logic here (stat boosts, ability unlocks, etc.)
        /// </summary>
        public abstract void ApplyEffect();

        /// <summary>
        /// Optional: Called when the augment is deselected or removed.
        /// Override if your augment needs cleanup logic.
        /// </summary>
        public virtual void RemoveEffect()
        {
            // Default: no cleanup needed
        }
    }
}
