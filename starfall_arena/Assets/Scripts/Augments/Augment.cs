using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace StarfallArena.UI
{
    /// <summary>
    /// ScriptableObject definition for an augment.
    /// This asset is authoring data only and must not hold per-player runtime state.
    /// </summary>
    [CreateAssetMenu(fileName = "New Augment", menuName = "Starfall Arena/Generic Augment", order = 1)]
    public class Augment : ScriptableObject
    {
        [Header("Display Info")]
        [Tooltip("Icon displayed in augment select UI")]
        public Sprite icon;

        [Tooltip("Name of the augment")]
        public string augmentName;

        [Tooltip("Description shown in augment select UI")]
        [TextArea(3, 5)]
        public string description;

        [FormerlySerializedAs("augmentID")]
        [HideInInspector]
        [SerializeField] private string _augmentID;

        public string augmentID => _augmentID;

        [Tooltip("Number of rounds this augment lasts (-1 for unlimited)")]
        public int rounds = -1;

        public virtual IAugmentRuntime CreateRuntime()
        {
            return new NoOpAugmentRuntime(this);
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_augmentID))
            {
                _augmentID = Guid.NewGuid().ToString("N");
            }
        }
    }
}
