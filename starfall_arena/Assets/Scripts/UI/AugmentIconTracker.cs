using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallArena.UI
{
    /// <summary>
    /// Displays a player's acquired augments in ordered icon slots.
    /// Slot Images can be authored manually or auto-generated as direct children.
    /// </summary>
    public class AugmentIconTracker : MonoBehaviour
    {
        [Header("Slot Generation")]
        [Tooltip("Minimum number of icon slots to keep available. Extra slots are auto-created when augment count exceeds this value.")]
        [Min(0)]
        [SerializeField] private int minimumSlotCount = 3;

        [Tooltip("Optional prefab used when auto-creating slot Images. Must include an Image component.")]
        [SerializeField] private Image slotImagePrefab;

        [Tooltip("Default size applied to auto-created slots when no prefab is provided.")]
        [SerializeField] private Vector2 autoSlotSize = new Vector2(48f, 48f);

        [Header("Display")]
        [Tooltip("Hide slots that do not currently contain an augment icon.")]
        [SerializeField] private bool hideUnusedSlots = true;

        [Header("Fallback")]
        [Tooltip("Optional fallback sprite shown if an augment has no icon assigned.")]
        [SerializeField] private Sprite fallbackIcon;

        private readonly List<Image> _slots = new List<Image>();

        private void Awake()
        {
            RebuildSlotCache();
            EnsureSlotCount(minimumSlotCount);
            RefreshSlotVisibility(0);
        }

        private void OnTransformChildrenChanged()
        {
            RebuildSlotCache();
        }

        private void OnValidate()
        {
            if (minimumSlotCount < 0)
            {
                minimumSlotCount = 0;
            }
        }

        public void SetAugments(IReadOnlyList<AugmentLoadoutEntry> augments)
        {
            RebuildSlotCache();

            int augmentCount = augments != null ? augments.Count : 0;
            EnsureSlotCount(Mathf.Max(minimumSlotCount, augmentCount));

            for (int i = 0; i < _slots.Count; i++)
            {
                Image slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                bool hasAugment = i < augmentCount;
                slot.gameObject.SetActive(!hideUnusedSlots || hasAugment);
                if (!hasAugment)
                {
                    slot.sprite = null;
                    continue;
                }

                AugmentLoadoutEntry entry = augments[i];
                Sprite icon = entry != null && entry.definition != null ? entry.definition.icon : null;
                slot.sprite = icon != null ? icon : fallbackIcon;
            }
        }

        public void ResetAll()
        {
            SetAugments(null);
        }

        private void RebuildSlotCache()
        {
            _slots.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                Image slotImage = child.GetComponent<Image>();
                if (slotImage != null)
                {
                    _slots.Add(slotImage);
                }
            }
        }

        private void EnsureSlotCount(int requiredCount)
        {
            while (_slots.Count < requiredCount)
            {
                Image newSlot = CreateSlotImage(_slots.Count);
                if (newSlot == null)
                {
                    break;
                }

                _slots.Add(newSlot);
            }
        }

        private void RefreshSlotVisibility(int augmentCount)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Image slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                bool hasAugment = i < augmentCount;
                slot.gameObject.SetActive(!hideUnusedSlots || hasAugment);
            }
        }

        private Image CreateSlotImage(int index)
        {
            if (slotImagePrefab != null)
            {
                Image prefabSlot = Instantiate(slotImagePrefab, transform);
                prefabSlot.name = $"AugmentIconSlot_{index + 1}";
                return prefabSlot;
            }

            GameObject slotObject = new GameObject($"AugmentIconSlot_{index + 1}", typeof(RectTransform), typeof(Image));
            slotObject.transform.SetParent(transform, false);

            RectTransform rectTransform = slotObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = autoSlotSize;
            }

            Image image = slotObject.GetComponent<Image>();
            if (image != null)
            {
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.sprite = null;
            }

            return image;
        }
    }
}
