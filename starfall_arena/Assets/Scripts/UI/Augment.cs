using UnityEngine;

namespace StarfallArena.UI
{
    /// <summary>
    /// ScriptableObject that defines an augment with its visual data and effect script.
    /// Create via: Right-click -> Create -> Starfall Arena -> Augment
    /// </summary>
    [CreateAssetMenu(fileName = "New Augment", menuName = "Starfall Arena/Augment", order = 1)]
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

        [Header("Effect")]
        [Tooltip("The script that defines what this augment does when selected")]
        public AugmentEffect effectScript;
    }
}
