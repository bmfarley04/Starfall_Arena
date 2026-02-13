using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace StarfallArena.UI
{
    /// <summary>
    /// Manages the augment selection screen with tier-based random selection,
    /// animated UI transitions, and controller navigation support.
    /// </summary>
    public class AugmentSelectManager : MonoBehaviour
    {
        [Header("Augment Pools")]
        [Tooltip("Tier 1 augments (common)")]
        [SerializeField] private List<Augment> tier1Augments = new List<Augment>();

        [Tooltip("Tier 2 augments (rare)")]
        [SerializeField] private List<Augment> tier2Augments = new List<Augment>();

        [Tooltip("Tier 3 augments (legendary)")]
        [SerializeField] private List<Augment> tier3Augments = new List<Augment>();

        [Header("Tier Selection Probabilities")]
        [Tooltip("Probability of tier 1 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier1Probability = 0.6f;

        [Tooltip("Probability of tier 2 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier2Probability = 0.3f;

        [Tooltip("Probability of tier 3 appearing (must sum to 1 with other tiers)")]
        [Range(0f, 1f)]
        [SerializeField] private float tier3Probability = 0.1f;

        [Header("Tier 1 UI References - Choice 1")]
        [SerializeField] private Image tier1Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice1Name;
        [SerializeField] private TextMeshProUGUI tier1Choice1Description;

        [Header("Tier 1 UI References - Choice 2")]
        [SerializeField] private Image tier1Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice2Name;
        [SerializeField] private TextMeshProUGUI tier1Choice2Description;

        [Header("Tier 1 UI References - Choice 3")]
        [SerializeField] private Image tier1Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier1Choice3Name;
        [SerializeField] private TextMeshProUGUI tier1Choice3Description;

        [Header("Tier 2 UI References - Choice 1")]
        [SerializeField] private Image tier2Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice1Name;
        [SerializeField] private TextMeshProUGUI tier2Choice1Description;

        [Header("Tier 2 UI References - Choice 2")]
        [SerializeField] private Image tier2Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice2Name;
        [SerializeField] private TextMeshProUGUI tier2Choice2Description;

        [Header("Tier 2 UI References - Choice 3")]
        [SerializeField] private Image tier2Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier2Choice3Name;
        [SerializeField] private TextMeshProUGUI tier2Choice3Description;

        [Header("Tier 3 UI References - Choice 1")]
        [SerializeField] private Image tier3Choice1Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice1Name;
        [SerializeField] private TextMeshProUGUI tier3Choice1Description;

        [Header("Tier 3 UI References - Choice 2")]
        [SerializeField] private Image tier3Choice2Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice2Name;
        [SerializeField] private TextMeshProUGUI tier3Choice2Description;

        [Header("Tier 3 UI References - Choice 3")]
        [SerializeField] private Image tier3Choice3Icon;
        [SerializeField] private TextMeshProUGUI tier3Choice3Name;
        [SerializeField] private TextMeshProUGUI tier3Choice3Description;

        [Header("Canvas Groups")]
        [Tooltip("Canvas group containing tier 1 UI")]
        [SerializeField] private CanvasGroup tier1CanvasGroup;

        [Tooltip("Canvas group containing tier 2 UI")]
        [SerializeField] private CanvasGroup tier2CanvasGroup;

        [Tooltip("Canvas group containing tier 3 UI")]
        [SerializeField] private CanvasGroup tier3CanvasGroup;

        [Header("Controller Navigation")]
        [Tooltip("Default selected button for tier 1")]
        [SerializeField] private Button tier1DefaultButton;

        [Tooltip("Default selected button for tier 2")]
        [SerializeField] private Button tier2DefaultButton;

        [Tooltip("Default selected button for tier 3")]
        [SerializeField] private Button tier3DefaultButton;

        [Header("Animation Settings")]
        [Tooltip("Duration of the entrance animation")]
        [SerializeField] private float animationDuration = 0.6f;

        [Tooltip("Animation curve for entrance (bounce/overshoot recommended)")]
        [SerializeField] private AnimationCurve entranceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Starting scale for cards (0 = from center point)")]
        [SerializeField] private float startScale = 0f;

        [Tooltip("Delay between each card animation")]
        [SerializeField] private float cardAnimationDelay = 0.1f;

        [Header("Audio")]
        [Tooltip("Sound played when augment screen appears")]
        [SerializeField] private SoundEffect augmentAppearSound;

        [Tooltip("AudioSource for playing UI sounds (2D)")]
        [SerializeField] private AudioSource audioSource;

        [Header("Debug")]
        [Tooltip("Automatically show augment select on Start (for testing)")]
        [SerializeField] private bool debugShowOnStart = false;

        [Tooltip("Enable F1 key to trigger augment select during play (for testing)")]
        [SerializeField] private bool debugEnableKeyTrigger = true;

        // Internal state
        private int currentTier;
        private List<Augment> selectedAugments = new List<Augment>(3);
        private bool isShowing = false;

        private void OnValidate()
        {
            // Warn if probabilities don't sum to 1
            float sum = tier1Probability + tier2Probability + tier3Probability;
            if (Mathf.Abs(sum - 1f) > 0.001f)
            {
                Debug.LogWarning($"Tier probabilities sum to {sum:F3}, but should sum to 1.0");
            }
        }

        private void Start()
        {
            // Hide all tiers at start
            HideAllTiers();

            // Debug: Auto-show if enabled
            if (debugShowOnStart)
            {
                ShowAugmentSelect();
            }
        }

        private void Update()
        {
            // Debug: Trigger with F1 key press (new Input System)
            if (debugEnableKeyTrigger && !isShowing)
            {
                if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                {
                    ShowAugmentSelect();
                }
            }
        }

        /// <summary>
        /// MAIN FUNCTION: Call this from your scene manager to show the augment select screen.
        /// This is the only function you need to call to trigger augment selection.
        /// </summary>
        [ContextMenu("Debug: Show Augment Select")]
        public void ShowAugmentSelect()
        {
            if (isShowing)
            {
                Debug.LogWarning("Augment select is already showing!");
                return;
            }

            isShowing = true;

            // Select tier based on probabilities
            currentTier = SelectRandomTier();
            Debug.Log($"Selected augment tier: {currentTier}");

            // Get 3 random augments from the selected tier
            selectedAugments = SelectRandomAugments(currentTier);

            // Populate UI with selected augments
            PopulateUI(currentTier, selectedAugments);

            // Show only the selected tier's canvas group (activates GameObject)
            ShowTierCanvas(currentTier);

            // Play entrance animation
            StartCoroutine(AnimateEntrance(currentTier));

            // Play sound effect
            if (augmentAppearSound != null && audioSource != null)
            {
                augmentAppearSound.Play(audioSource);
            }
        }

        /// <summary>
        /// Hides the augment select screen. Call this after an augment is chosen.
        /// </summary>
        public void HideAugmentSelect()
        {
            HideAllTiers();
            isShowing = false;
        }

        /// <summary>
        /// Hides all tier canvas groups.
        /// </summary>
        private void HideAllTiers()
        {
            SetCanvasGroupVisibility(tier1CanvasGroup, false);
            SetCanvasGroupVisibility(tier2CanvasGroup, false);
            SetCanvasGroupVisibility(tier3CanvasGroup, false);
        }

        /// <summary>
        /// Selects a random tier based on the configured probabilities.
        /// </summary>
        private int SelectRandomTier()
        {
            float roll = Random.Range(0f, 1f);

            if (roll < tier1Probability)
            {
                return 1;
            }
            else if (roll < tier1Probability + tier2Probability)
            {
                return 2;
            }
            else
            {
                return 3;
            }
        }

        /// <summary>
        /// Selects 3 random augments from the specified tier without duplicates.
        /// </summary>
        private List<Augment> SelectRandomAugments(int tier)
        {
            List<Augment> sourceList = tier switch
            {
                1 => tier1Augments,
                2 => tier2Augments,
                3 => tier3Augments,
                _ => tier1Augments
            };

            if (sourceList.Count < 3)
            {
                Debug.LogError($"Tier {tier} has fewer than 3 augments! Cannot populate augment select.");
                return new List<Augment>();
            }

            // Create copy to avoid modifying original list
            List<Augment> availableAugments = new List<Augment>(sourceList);
            List<Augment> selected = new List<Augment>(3);

            // Pick 3 random augments without replacement
            for (int i = 0; i < 3; i++)
            {
                int randomIndex = Random.Range(0, availableAugments.Count);
                selected.Add(availableAugments[randomIndex]);
                availableAugments.RemoveAt(randomIndex);
            }

            return selected;
        }

        /// <summary>
        /// Populates the UI elements with the selected augments.
        /// </summary>
        private void PopulateUI(int tier, List<Augment> augments)
        {
            if (augments.Count < 3)
            {
                Debug.LogError("Not enough augments selected to populate UI!");
                return;
            }

            switch (tier)
            {
                case 1:
                    SetUIElements(tier1Choice1Icon, tier1Choice1Name, tier1Choice1Description, augments[0]);
                    SetUIElements(tier1Choice2Icon, tier1Choice2Name, tier1Choice2Description, augments[1]);
                    SetUIElements(tier1Choice3Icon, tier1Choice3Name, tier1Choice3Description, augments[2]);
                    break;

                case 2:
                    SetUIElements(tier2Choice1Icon, tier2Choice1Name, tier2Choice1Description, augments[0]);
                    SetUIElements(tier2Choice2Icon, tier2Choice2Name, tier2Choice2Description, augments[1]);
                    SetUIElements(tier2Choice3Icon, tier2Choice3Name, tier2Choice3Description, augments[2]);
                    break;

                case 3:
                    SetUIElements(tier3Choice1Icon, tier3Choice1Name, tier3Choice1Description, augments[0]);
                    SetUIElements(tier3Choice2Icon, tier3Choice2Name, tier3Choice2Description, augments[1]);
                    SetUIElements(tier3Choice3Icon, tier3Choice3Name, tier3Choice3Description, augments[2]);
                    break;
            }
        }

        /// <summary>
        /// Sets the UI elements (icon, name, description) for a single augment choice.
        /// </summary>
        private void SetUIElements(Image icon, TextMeshProUGUI nameText, TextMeshProUGUI descriptionText, Augment augment)
        {
            if (icon != null)
            {
                icon.sprite = augment.icon;
            }

            if (nameText != null)
            {
                nameText.text = augment.augmentName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = augment.description;
            }
        }

        /// <summary>
        /// Shows the canvas group for the selected tier, hides others.
        /// IMPORTANT: This activates the GameObject to make it visible.
        /// </summary>
        private void ShowTierCanvas(int tier)
        {
            // Hide all canvas groups first
            HideAllTiers();

            // Show selected tier
            CanvasGroup targetGroup = tier switch
            {
                1 => tier1CanvasGroup,
                2 => tier2CanvasGroup,
                3 => tier3CanvasGroup,
                _ => tier1CanvasGroup
            };

            if (targetGroup != null)
            {
                Debug.Log($"Activating tier {tier} canvas group: {targetGroup.name}");

                // CRITICAL: Activate the GameObject so it's visible
                targetGroup.gameObject.SetActive(true);
                targetGroup.alpha = 1f; // Changed: Start visible (cards will handle their own fade)
                targetGroup.interactable = false;
                targetGroup.blocksRaycasts = false;
            }
            else
            {
                Debug.LogError($"Tier {tier} canvas group is not assigned!");
            }
        }

        /// <summary>
        /// Sets a canvas group's visibility, interactability, and raycast blocking.
        /// </summary>
        private void SetCanvasGroupVisibility(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (!visible)
            {
                group.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Animates the entrance of augment cards with staggered timing.
        /// </summary>
        private IEnumerator AnimateEntrance(int tier)
        {
            CanvasGroup targetGroup = tier switch
            {
                1 => tier1CanvasGroup,
                2 => tier2CanvasGroup,
                3 => tier3CanvasGroup,
                _ => tier1CanvasGroup
            };

            if (targetGroup == null)
            {
                Debug.LogError("Target canvas group is null!");
                yield break;
            }

            // Make parent canvas group visible immediately
            targetGroup.alpha = 1f;
            targetGroup.interactable = false;
            targetGroup.blocksRaycasts = false;

            // Get the three card transforms (assume they're the first 3 children)
            Transform[] cardTransforms = new Transform[3];
            int childCount = Mathf.Min(3, targetGroup.transform.childCount);

            Debug.Log($"Found {childCount} children in tier {tier} canvas group");

            for (int i = 0; i < childCount; i++)
            {
                cardTransforms[i] = targetGroup.transform.GetChild(i);
                Debug.Log($"Card {i}: {cardTransforms[i].name}");
            }

            // Animate each card with staggered timing
            for (int i = 0; i < cardTransforms.Length; i++)
            {
                if (cardTransforms[i] != null)
                {
                    StartCoroutine(AnimateCard(cardTransforms[i]));
                }
                yield return new WaitForSecondsRealtime(cardAnimationDelay);
            }

            // Wait for last animation to finish
            yield return new WaitForSecondsRealtime(animationDuration);

            // Enable interactability
            targetGroup.interactable = true;
            targetGroup.blocksRaycasts = true;

            // Set default EventSystem selection
            SetDefaultSelection(tier);
        }

        /// <summary>
        /// Animates a single card using scale and fade.
        /// </summary>
        private IEnumerator AnimateCard(Transform card)
        {
            CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
            if (cardGroup == null)
            {
                cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            }

            Vector3 originalScale = card.localScale;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                float curveValue = entranceCurve.Evaluate(t);

                // Scale animation
                card.localScale = Vector3.Lerp(Vector3.one * startScale, originalScale, curveValue);

                // Fade animation
                cardGroup.alpha = curveValue;

                yield return null;
            }

            // Ensure final state
            card.localScale = originalScale;
            cardGroup.alpha = 1f;
        }

        /// <summary>
        /// Sets the default EventSystem selection for controller navigation.
        /// </summary>
        private void SetDefaultSelection(int tier)
        {
            Button defaultButton = tier switch
            {
                1 => tier1DefaultButton,
                2 => tier2DefaultButton,
                3 => tier3DefaultButton,
                _ => tier1DefaultButton
            };

            if (defaultButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultButton.gameObject);
            }
        }

        /// <summary>
        /// Call this when an augment is selected (hook this up to button onClick events).
        /// Pass the choice index (0, 1, or 2) to identify which augment was chosen.
        /// </summary>
        /// <param name="choiceIndex">0-2 for the three choices</param>
        public void OnAugmentSelected(int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= selectedAugments.Count)
            {
                Debug.LogError($"Invalid augment choice index: {choiceIndex}");
                return;
            }

            Augment selectedAugment = selectedAugments[choiceIndex];
            Debug.Log($"Player selected augment: {selectedAugment.augmentName}");

            // Apply the augment effect
            //if (selectedAugment.effectScript != null)
            //{
            //    selectedAugment.effectScript.ApplyEffect();
            //}
            //else
            //{
            //    Debug.LogWarning($"Augment '{selectedAugment.augmentName}' has no effect script assigned!");
            //}

            // Hide the augment select screen
            HideAugmentSelect();

            // TODO: Your scene manager can listen to this or add custom logic here
            // For example: Resume game, spawn next wave, etc.
        }
    }
}
