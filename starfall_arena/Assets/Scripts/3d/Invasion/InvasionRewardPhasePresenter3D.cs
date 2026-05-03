using System;
using System.Collections;
using System.Collections.Generic;
using StarfallArena.UI;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionRewardPhasePresenter3D : MonoBehaviour
{
    [Tooltip("Old augment-card UI manager reused only for the between-wave Invasion reward visuals and controller navigation.")]
    [SerializeField] private AugmentSelectManager augmentSelectManager;
    [Tooltip("Optional shared presentation root that should be forced active before showing the reused augment UI. Leave empty to use this presenter object, which should usually be the upgradeSelector root.")]
    [SerializeField] private GameObject rewardPresentationRoot;
    [Tooltip("Seconds a local player has to choose before the presenter auto-picks the left-most reward.")]
    [Min(1f)]
    [SerializeField] private float selectionTimeLimitSeconds = 12f;

    private readonly List<Augment> _runtimeAugments = new List<Augment>(3);
    private readonly List<InvasionStatRewardDefinition3D> _currentRewards = new List<InvasionStatRewardDefinition3D>(3);
    private Action<int> _selectionCallback;
    private Coroutine _countdownCoroutine;
    private bool _selectionCommitted;

    public bool IsShowing => augmentSelectManager != null && augmentSelectManager.IsShowing;
    public float SelectionTimeLimitSeconds => Mathf.Max(1f, selectionTimeLimitSeconds);

    private void Awake()
    {
        augmentSelectManager ??= FindFirstObjectByType<AugmentSelectManager>(FindObjectsInactive.Include);
        rewardPresentationRoot ??= gameObject;
    }

    private void OnEnable()
    {
        if (augmentSelectManager != null)
        {
            augmentSelectManager.onAugmentChosen -= HandleAugmentChosen;
            augmentSelectManager.onAugmentChosen += HandleAugmentChosen;
        }
    }

    private void OnDisable()
    {
        if (augmentSelectManager != null)
        {
            augmentSelectManager.onAugmentChosen -= HandleAugmentChosen;
        }

        CleanupRuntimeAugments();
    }

    public void ShowOffers(int playerSlot, InvasionRewardTier3D rewardTier, IReadOnlyList<InvasionStatRewardDefinition3D> offers, float statBoostMultiplier, Action<int> onSelected)
    {
        if (augmentSelectManager == null)
        {
            Debug.LogWarning("[InvasionRewardPhasePresenter3D] Missing AugmentSelectManager reference; auto-selecting the first reward.", this);
            onSelected?.Invoke(0);
            return;
        }

        CleanupRuntimeAugments();
        _selectionCallback = onSelected;
        _selectionCommitted = false;
        _currentRewards.Clear();
        List<int> visualTiers = new List<int>(offers.Count);

        for (int i = 0; i < offers.Count; i++)
        {
            InvasionStatRewardDefinition3D reward = offers[i];
            if (reward == null)
            {
                continue;
            }

            _currentRewards.Add(reward);
            Augment displayAugment = ScriptableObject.CreateInstance<Augment>();
            displayAugment.name = $"RuntimeInvasionReward_{reward.RewardId}";
            displayAugment.augmentName = reward.DisplayName;
            displayAugment.description = reward.BuildDescription(rewardTier, statBoostMultiplier);
            displayAugment.icon = reward.Icon;
            _runtimeAugments.Add(displayAugment);
            visualTiers.Add(ResolveVisualTier(reward.ResolveVisualTier(rewardTier)));
        }

        if (_runtimeAugments.Count == 0)
        {
            onSelected?.Invoke(0);
            return;
        }

        EnsurePresentationHierarchyVisible();
        augmentSelectManager.ShowNetworkAugmentSelect(playerSlot, ResolveVisualTier(rewardTier), _runtimeAugments, visualTiers);
        Canvas.ForceUpdateCanvases();

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
        }

        _countdownCoroutine = StartCoroutine(RunExternalCountdown());
    }

    private IEnumerator RunExternalCountdown()
    {
        float remaining = Mathf.Max(1f, selectionTimeLimitSeconds);
        while (!_selectionCommitted && remaining > 0f)
        {
            augmentSelectManager?.SetCountdownValue(remaining);
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_selectionCommitted)
        {
            _countdownCoroutine = null;
            yield break;
        }

        augmentSelectManager?.SetCountdownValue(0f);
        CommitSelection(0);
        _countdownCoroutine = null;
    }

    private void HandleAugmentChosen(Augment augment, int index)
    {
        if (_selectionCommitted)
        {
            return;
        }

        CommitSelection(index);
    }

    private void CommitSelection(int index)
    {
        if (_selectionCommitted)
        {
            return;
        }

        _selectionCommitted = true;
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        Action<int> callback = _selectionCallback;
        _selectionCallback = null;
        callback?.Invoke(Mathf.Clamp(index, 0, Mathf.Max(0, _currentRewards.Count - 1)));
        augmentSelectManager?.HideAugmentSelect();
    }

    private void CleanupRuntimeAugments()
    {
        if (augmentSelectManager != null && augmentSelectManager.IsShowing)
        {
            augmentSelectManager.HideAugmentSelect();
        }

        for (int i = 0; i < _runtimeAugments.Count; i++)
        {
            if (_runtimeAugments[i] != null)
            {
                Destroy(_runtimeAugments[i]);
            }
        }

        _runtimeAugments.Clear();
        _currentRewards.Clear();
        _selectionCommitted = false;
        _selectionCallback = null;

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    private void EnsurePresentationHierarchyVisible()
    {
        GameObject root = rewardPresentationRoot != null ? rewardPresentationRoot : gameObject;
        SetHierarchyActive(root != null ? root.transform : null);
        SetHierarchyActive(augmentSelectManager != null ? augmentSelectManager.transform : null);

        if (root == null)
        {
            return;
        }

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            SetHierarchyActive(canvas.transform);
            canvas.enabled = true;
        }

        CanvasGroup[] canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < canvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = canvasGroups[i];
            if (canvasGroup == null)
            {
                continue;
            }

            if (canvasGroup.transform == root.transform)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }

    private static void SetHierarchyActive(Transform leaf)
    {
        Transform current = leaf;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }
    }

    private static int ResolveVisualTier(InvasionRewardTier3D rewardTier)
    {
        return rewardTier switch
        {
            InvasionRewardTier3D.Epic => 2,
            InvasionRewardTier3D.High => 3,
            InvasionRewardTier3D.Tier4 => 4,
            _ => 1
        };
    }
}
