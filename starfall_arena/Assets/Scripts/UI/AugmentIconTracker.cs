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
        private const string Player1Tag = "Player1";
        private const string Player2Tag = "Player2";

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

        [Header("Runtime Player Tracking")]
        [Tooltip("Optional player entity to track. Tracker validates Player1/Player2 tags before reading augment icons.")]
        [SerializeField] private Entity trackedPlayer;

        [Tooltip("Continuously refresh icon slots from the tracked player's augment list.")]
        [SerializeField] private bool trackPlayerAugments = true;

        [Tooltip("Refresh interval in seconds for tracked player augments. 0 means every frame.")]
        [Min(0f)]
        [SerializeField] private float trackedRefreshInterval = 0f;

        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Augment> _cachedTrackedAugments = new List<Augment>();
        private float _nextTrackedRefreshTime;
        private bool _loggedInvalidTrackedTag;

        private void Awake()
        {
            RebuildSlotCache();
            EnsureSlotCount(minimumSlotCount);
            RefreshSlotVisibility(0);
            SetTrackedPlayer(trackedPlayer);
        }

        private void Update()
        {
            TryRefreshFromTrackedPlayer();
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

            CacheEntryDefinitions(augments);
        }

        public void ResetAll()
        {
            SetAugments(null);
        }

        public void SetTrackedPlayer(Entity player)
        {
            trackedPlayer = player;
            _loggedInvalidTrackedTag = false;
            _nextTrackedRefreshTime = 0f;
            _cachedTrackedAugments.Clear();

            if (trackedPlayer == null)
            {
                return;
            }

            TryRefreshFromTrackedPlayer(forceRefresh: true);
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

        private void TryRefreshFromTrackedPlayer(bool forceRefresh = false)
        {
            if (!trackPlayerAugments || trackedPlayer == null)
            {
                return;
            }

            if (!forceRefresh && trackedRefreshInterval > 0f && Time.unscaledTime < _nextTrackedRefreshTime)
            {
                return;
            }

            _nextTrackedRefreshTime = Time.unscaledTime + trackedRefreshInterval;

            if (!HasRecognizedPlayerTag(trackedPlayer))
            {
                if (!_loggedInvalidTrackedTag)
                {
                    Debug.LogWarning($"{name}: tracked player '{trackedPlayer.name}' must use tag '{Player1Tag}' or '{Player2Tag}'.", this);
                    _loggedInvalidTrackedTag = true;
                }

                return;
            }

            _loggedInvalidTrackedTag = false;

            List<Augment> trackedAugments = trackedPlayer.augments;
            if (!forceRefresh && !HasTrackedAugmentsChanged(trackedAugments))
            {
                return;
            }

            SetAugmentDefinitions(trackedAugments);
            CacheTrackedAugments(trackedAugments);
        }

        private void SetAugmentDefinitions(IReadOnlyList<Augment> augments)
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

                Augment definition = augments[i];
                Sprite icon = definition != null ? definition.icon : null;
                slot.sprite = icon != null ? icon : fallbackIcon;
            }
        }

        private bool HasTrackedAugmentsChanged(IReadOnlyList<Augment> augments)
        {
            int count = augments != null ? augments.Count : 0;
            if (_cachedTrackedAugments.Count != count)
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (_cachedTrackedAugments[i] != augments[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheTrackedAugments(IReadOnlyList<Augment> augments)
        {
            _cachedTrackedAugments.Clear();
            if (augments == null)
            {
                return;
            }

            for (int i = 0; i < augments.Count; i++)
            {
                _cachedTrackedAugments.Add(augments[i]);
            }
        }

        private void CacheEntryDefinitions(IReadOnlyList<AugmentLoadoutEntry> augments)
        {
            _cachedTrackedAugments.Clear();
            if (augments == null)
            {
                return;
            }

            for (int i = 0; i < augments.Count; i++)
            {
                _cachedTrackedAugments.Add(augments[i] != null ? augments[i].definition : null);
            }
        }

        private static bool HasRecognizedPlayerTag(Component playerComponent)
        {
            if (playerComponent == null)
            {
                return false;
            }

            return playerComponent.CompareTag(Player1Tag) || playerComponent.CompareTag(Player2Tag);
        }
    }
}
