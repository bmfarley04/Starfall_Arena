using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TargetAwarenessHUD3D : PlayerHUDBindingTarget3D
{
    private const float DefaultAwarenessRange = 900f;

    private struct TargetRuntime3D
    {
        public Entity3D Target;
        public TargetAwarenessWidget3D Widget;
        public TargetAwarenessVisibility3D CurrentState;
        public float LastStateChangeTime;
        public Vector2 LastIndicatorDirection;
        public bool HasPresented;
    }

    [System.Serializable]
    private struct TargetDiscoveryConfig3D
    {
        [Tooltip("How often active Entity3D targets are rediscovered. This keeps spawn/despawn handling cheap while still covering network timing.")]
        public float refreshInterval;
        [Tooltip("Optional target layer filter. Leave as Everything unless target roots are on a dedicated ship layer.")]
        public LayerMask targetMask;
        [Tooltip("If true, inactive scene objects are ignored. Keep enabled for gameplay HUDs.")]
        public bool ignoreInactiveTargets;
    }

    [System.Serializable]
    private struct DistanceConfig3D
    {
        [Tooltip("Maximum world-space distance at which targets are tracked at all. Targets outside this range are ignored until they come back in range.")]
        public float awarenessRange;
        [Tooltip("Target UI is hidden when the target is closer than this distance.")]
        public float closeHideDistance;
        [Tooltip("Visible, unoccluded targets use brackets from closeHideDistance up to this distance.")]
        public float bracketMaxDistance;
        [Tooltip("Distance buffer used to avoid rapid state flicker around thresholds.")]
        public float thresholdHysteresis;
    }

    [System.Serializable]
    private struct ScreenClampConfig3D
    {
        [Tooltip("Padding from the left/right canvas edge for ellipse-clamped indicators.")]
        public float edgeHorizontalPadding;
        [FormerlySerializedAs("edgeVerticalPadding")]
        [Tooltip("Padding from the top canvas edge for ellipse-clamped indicators.")]
        public float edgeTopPadding;
        [Tooltip("Padding from the bottom canvas edge for ellipse-clamped indicators.")]
        public float edgeBottomPadding;
        [Tooltip("Padding from the left/right canvas edge for in-FOV floating indicators.")]
        public float floatingHorizontalPadding;
        [FormerlySerializedAs("floatingVerticalPadding")]
        [Tooltip("Padding from the top canvas edge for in-FOV floating indicators.")]
        public float floatingTopPadding;
        [Tooltip("Padding from the bottom canvas edge for in-FOV floating indicators.")]
        public float floatingBottomPadding;
    }

    [System.Serializable]
    private struct OcclusionConfig3D
    {
        [Tooltip("World layers that can block brackets/bars. Do not include ship-only layers unless ships should block each other.")]
        public LayerMask occlusionMask;
        [Tooltip("Local-space offset from target origin used for UI projection. Keep this near the visual center of the ship.")]
        public Vector3 targetUiOffset;
        [Tooltip("Local-space offset from target origin used for floating/offscreen indicator projection. Keep this at the point the indicator should sit over.")]
        public Vector3 targetIndicatorOffset;
        [Tooltip("Local-space offset from target origin used as the occlusion probe point.")]
        public Vector3 targetAimOffset;
        [Tooltip("Radius for a forgiving occlusion probe. Set to 0 for a thin raycast.")]
        public float occlusionProbeRadius;
        [Tooltip("Minimum time before switching between occluded and unoccluded presentation states.")]
        public float stateHoldSeconds;
    }

    [System.Serializable]
    private struct ScaleConfig3D
    {
        public Vector2 distanceRange;
        public Vector2 indicatorScaleRange;
        public Vector2 bracketScaleRange;
        public Vector2 barScaleRange;
    }

    [Header("References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private Camera gameplayCameraOverride;
    [SerializeField] private TargetAwarenessWidget3D widgetTemplate;
    [SerializeField] private RectTransform widgetContainer;

    [Header("Target Discovery")]
    [Tooltip("Faction this HUD should treat as hostile targets. In Invasion this should usually stay on EnemyTeam.")]
    [SerializeField] private Faction3D enemyFaction = Faction3D.EnemyTeam;
    [SerializeField] private TargetDiscoveryConfig3D discovery = new TargetDiscoveryConfig3D
    {
        refreshInterval = 0.25f,
        targetMask = ~0,
        ignoreInactiveTargets = true
    };

    [Header("Distance")]
    [SerializeField] private DistanceConfig3D distance = new DistanceConfig3D
    {
        awarenessRange = 900f,
        closeHideDistance = 45f,
        bracketMaxDistance = 240f,
        thresholdHysteresis = 8f
    };

    [Header("Screen Clamping")]
    [SerializeField] private ScreenClampConfig3D screenClamp = new ScreenClampConfig3D
    {
        edgeHorizontalPadding = 72f,
        edgeTopPadding = 150f,
        edgeBottomPadding = 56f,
        floatingHorizontalPadding = 96f,
        floatingTopPadding = 72f,
        floatingBottomPadding = 72f
    };

    [Header("Occlusion")]
    [SerializeField] private OcclusionConfig3D occlusion = new OcclusionConfig3D
    {
        occlusionMask = ~0,
        targetUiOffset = Vector3.zero,
        targetIndicatorOffset = Vector3.zero,
        targetAimOffset = new Vector3(0f, 1.5f, 0f),
        occlusionProbeRadius = 0.25f,
        stateHoldSeconds = 0.08f
    };

    [Header("Scale")]
    [SerializeField] private ScaleConfig3D scale = new ScaleConfig3D
    {
        distanceRange = new Vector2(45f, 900f),
        indicatorScaleRange = new Vector2(1.15f, 0.72f),
        bracketScaleRange = new Vector2(1.2f, 0.82f),
        barScaleRange = new Vector2(1f, 0.72f)
    };

    private readonly List<TargetRuntime3D> _targets = new();
    private readonly List<TargetAwarenessWidget3D> _widgetPool = new();
    private readonly RaycastHit[] _occlusionHits = new RaycastHit[16];
    private float _nextDiscoveryTime;
    private bool _loggedMissingTemplate;
    private bool _loggedMissingCanvas;
    private bool _loggedTemplateContainerFallback;
    private bool _loggedInactiveCanvasFallback;

    protected override void Awake()
    {
        base.Awake();
        targetCanvas ??= GetComponentInParent<Canvas>(true);
        canvasRoot ??= targetCanvas != null ? targetCanvas.transform as RectTransform : GetComponentInParent<RectTransform>(true);
        widgetContainer ??= canvasRoot;
        ResolveTemplateAuthoringConflicts();

        if (widgetTemplate != null)
        {
            widgetTemplate.gameObject.SetActive(false);
        }

        BindRenderCamera();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BindRenderCamera();
        ForceRefreshTargets();
    }

    private void Update()
    {
        BindRenderCamera();

        if (BoundPlayer == null)
        {
            HideAllTargets();
            return;
        }

        if (Time.unscaledTime >= _nextDiscoveryTime)
        {
            RefreshTargets();
        }

        UpdateTargetPresentations(Time.unscaledDeltaTime);
    }

    protected override void BindPlayer(Player3D player)
    {
        ClearTargetBindings();
        ForceRefreshTargets();
    }

    protected override void UnbindPlayer(Player3D player)
    {
        ClearTargetBindings();
    }

    protected override void ClearBinding()
    {
        ClearTargetBindings();
    }

    private void ForceRefreshTargets()
    {
        _nextDiscoveryTime = 0f;
        RefreshTargets();
    }

    private void RefreshTargets()
    {
        _nextDiscoveryTime = Time.unscaledTime + Mathf.Max(0.02f, discovery.refreshInterval);

        if (BoundPlayer == null)
        {
            ClearTargetBindings();
            return;
        }

        Entity3D[] entities = FindObjectsByType<Entity3D>(
            discovery.ignoreInactiveTargets ? FindObjectsInactive.Exclude : FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        MarkAllTargetsMissing();

        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D entity = entities[i];
            if (!IsValidTarget(entity))
            {
                continue;
            }

            EnsureTargetRuntime(entity);
        }

        RemoveMissingTargets();
    }

    private void UpdateTargetPresentations(float deltaTime)
    {
        Camera gameplayCamera = ResolveGameplayCamera();
        if (gameplayCamera == null || canvasRoot == null)
        {
            LogMissingCanvasOnce(gameplayCamera);
            HideAllTargets();
            return;
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            TargetRuntime3D runtime = _targets[i];
            if (runtime.Target == null || runtime.Widget == null || !IsValidTarget(runtime.Target))
            {
                runtime.Widget?.ApplyPresentation(new TargetAwarenessPresentation3D
                {
                    State = TargetAwarenessVisibility3D.Hidden,
                    IndicatorDirection = runtime.LastIndicatorDirection,
                    SnapPosition = !runtime.HasPresented
                }, deltaTime);
                _targets[i] = runtime;
                continue;
            }

            TargetAwarenessPresentation3D presentation = BuildPresentation(ref runtime, gameplayCamera);
            runtime.CurrentState = presentation.State;
            runtime.HasPresented = true;
            if (presentation.IndicatorDirection.sqrMagnitude > 0.0001f)
            {
                runtime.LastIndicatorDirection = presentation.IndicatorDirection;
            }

            runtime.Widget.ApplyPresentation(presentation, deltaTime);
            _targets[i] = runtime;
        }
    }

    private TargetAwarenessPresentation3D BuildPresentation(ref TargetRuntime3D runtime, Camera gameplayCamera)
    {
        Entity3D target = runtime.Target;
        Vector3 targetPoint = target.transform.TransformPoint(occlusion.targetUiOffset);
        Vector3 indicatorPoint = target.transform.TransformPoint(occlusion.targetIndicatorOffset);
        Vector3 occlusionPoint = target.transform.TransformPoint(occlusion.targetAimOffset);
        Vector3 cameraPosition = gameplayCamera.transform.position;
        float targetDistance = Vector3.Distance(BoundPlayer.transform.position, target.transform.position);

        TargetAwarenessPresentation3D presentation = new TargetAwarenessPresentation3D
        {
            State = TargetAwarenessVisibility3D.Hidden,
            IndicatorDirection = runtime.LastIndicatorDirection.sqrMagnitude > 0.0001f ? runtime.LastIndicatorDirection : Vector2.up,
            Health01 = target.MaxHealth > 0f ? target.CurrentHealth / target.MaxHealth : 0f,
            Shield01 = target.MaxShield > 0f ? target.CurrentShield / target.MaxShield : 0f,
            IndicatorScale = EvaluateScale(scale.indicatorScaleRange, targetDistance),
            BracketScale = EvaluateScale(scale.bracketScaleRange, targetDistance),
            BarScale = EvaluateScale(scale.barScaleRange, targetDistance),
            SnapPosition = !runtime.HasPresented
        };

        if (target.CurrentHealth <= 0f)
        {
            return presentation;
        }

        Vector3 viewport = gameplayCamera.WorldToViewportPoint(targetPoint);
        Vector3 cameraLocalTarget = gameplayCamera.transform.InverseTransformPoint(targetPoint);
        Vector3 indicatorViewport = gameplayCamera.WorldToViewportPoint(indicatorPoint);
        Vector3 cameraLocalIndicator = gameplayCamera.transform.InverseTransformPoint(indicatorPoint);
        bool behindCamera = cameraLocalTarget.z <= 0f || viewport.z <= 0f;
        bool insideViewport = !behindCamera
            && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
        bool occluded = insideViewport && IsTargetOccluded(cameraPosition, occlusionPoint, target);

        TargetAwarenessVisibility3D desiredState;
        if (!insideViewport)
        {
            desiredState = TargetAwarenessVisibility3D.EdgeIndicator;
        }
        else if (occluded)
        {
            desiredState = TargetAwarenessVisibility3D.FloatingIndicator;
        }
        else if (IsTooCloseForBrackets(targetDistance, runtime.CurrentState))
        {
            desiredState = TargetAwarenessVisibility3D.Hidden;
        }
        else if (IsInBracketDistance(targetDistance, runtime.CurrentState))
        {
            desiredState = TargetAwarenessVisibility3D.Bracket;
        }
        else
        {
            desiredState = TargetAwarenessVisibility3D.FloatingIndicator;
        }

        presentation.State = ResolveHeldState(ref runtime, desiredState);

        Vector2 localPosition;
        if (presentation.State == TargetAwarenessVisibility3D.EdgeIndicator)
        {
            Vector2 direction = ResolveScreenDirection(gameplayCamera, indicatorPoint, indicatorViewport, cameraLocalIndicator, runtime.LastIndicatorDirection);
            presentation.IndicatorDirection = direction;
            presentation.RotateIndicator = true;
            presentation.CanvasPosition = ClampToEdgeEllipse(direction);
            return presentation;
        }

        Vector3 projectionPoint = presentation.State == TargetAwarenessVisibility3D.FloatingIndicator
            ? indicatorPoint
            : targetPoint;

        if (!TryWorldToCanvasPoint(gameplayCamera, projectionPoint, out localPosition))
        {
            Vector2 direction = ResolveScreenDirection(gameplayCamera, indicatorPoint, indicatorViewport, cameraLocalIndicator, runtime.LastIndicatorDirection);
            presentation.State = TargetAwarenessVisibility3D.EdgeIndicator;
            presentation.IndicatorDirection = direction;
            presentation.RotateIndicator = true;
            presentation.CanvasPosition = ClampToEdgeEllipse(direction);
            return presentation;
        }

        Vector2 centerDirection = localPosition.sqrMagnitude > 0.0001f
            ? localPosition.normalized
            : runtime.LastIndicatorDirection.sqrMagnitude > 0.0001f
                ? runtime.LastIndicatorDirection
                : Vector2.up;

        presentation.IndicatorDirection = centerDirection;
        presentation.RotateIndicator = occluded;
        presentation.CanvasPosition = presentation.State == TargetAwarenessVisibility3D.FloatingIndicator
            ? ClampToFloatingSafeRect(localPosition)
            : localPosition;
        return presentation;
    }

    private TargetAwarenessVisibility3D ResolveHeldState(ref TargetRuntime3D runtime, TargetAwarenessVisibility3D desiredState)
    {
        if (runtime.CurrentState == TargetAwarenessVisibility3D.Hidden || desiredState == TargetAwarenessVisibility3D.Hidden)
        {
            runtime.LastStateChangeTime = Time.unscaledTime;
            return desiredState;
        }

        if (runtime.CurrentState == desiredState)
        {
            return desiredState;
        }

        float holdSeconds = Mathf.Max(0f, occlusion.stateHoldSeconds);
        if (Time.unscaledTime < runtime.LastStateChangeTime + holdSeconds)
        {
            return runtime.CurrentState;
        }

        runtime.LastStateChangeTime = Time.unscaledTime;
        return desiredState;
    }

    private bool IsTargetOccluded(Vector3 origin, Vector3 targetPoint, Entity3D target)
    {
        Vector3 toTarget = targetPoint - origin;
        float maxDistance = toTarget.magnitude;
        if (maxDistance <= 0.01f)
        {
            return false;
        }

        Vector3 directionToTarget = toTarget / maxDistance;
        int hitCount = occlusion.occlusionProbeRadius > 0f
            ? Physics.SphereCastNonAlloc(origin, occlusion.occlusionProbeRadius, directionToTarget, _occlusionHits, maxDistance, occlusion.occlusionMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, directionToTarget, _occlusionHits, maxDistance, occlusion.occlusionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _occlusionHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform.IsChildOf(target.transform) || hitTransform.IsChildOf(BoundPlayer.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private Vector2 ResolveScreenDirection(
        Camera gameplayCamera,
        Vector3 targetPoint,
        Vector3 viewport,
        Vector3 cameraLocalTarget,
        Vector2 fallbackDirection)
    {
        Vector2 direction;
        if (cameraLocalTarget.z <= 0f || viewport.z <= 0f)
        {
            direction = new Vector2(cameraLocalTarget.x, cameraLocalTarget.y);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.up;
            }
        }
        else if (TryWorldToCanvasPoint(gameplayCamera, targetPoint, out Vector2 projectedPoint))
        {
            direction = projectedPoint;
        }
        else
        {
            direction = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.up;
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
    }

    private bool TryWorldToCanvasPoint(Camera gameplayCamera, Vector3 worldPoint, out Vector2 localPoint)
    {
        Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0f)
        {
            localPoint = Vector2.zero;
            return false;
        }

        Camera canvasCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPoint, canvasCamera, out localPoint);
    }

    private Vector2 ClampToEdgeEllipse(Vector2 direction)
    {
        Rect rect = canvasRoot.rect;
        float xRadius = Mathf.Max(1f, rect.width * 0.5f - Mathf.Max(0f, screenClamp.edgeHorizontalPadding));
        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        float verticalPadding = normalizedDirection.y >= 0f
            ? Mathf.Max(0f, screenClamp.edgeTopPadding)
            : Mathf.Max(0f, screenClamp.edgeBottomPadding);
        float yRadius = Mathf.Max(1f, rect.height * 0.5f - verticalPadding);
        float denominator = Mathf.Sqrt(
            (normalizedDirection.x * normalizedDirection.x) / (xRadius * xRadius)
            + (normalizedDirection.y * normalizedDirection.y) / (yRadius * yRadius));

        float scaleFactor = denominator > 0.0001f ? 1f / denominator : 0f;
        return normalizedDirection * scaleFactor;
    }

    private Vector2 ClampToFloatingSafeRect(Vector2 point)
    {
        Rect rect = canvasRoot.rect;
        float xLimit = Mathf.Max(1f, rect.width * 0.5f - Mathf.Max(0f, screenClamp.floatingHorizontalPadding));
        float topLimit = Mathf.Max(1f, rect.height * 0.5f - Mathf.Max(0f, screenClamp.floatingTopPadding));
        float bottomLimit = Mathf.Max(1f, rect.height * 0.5f - Mathf.Max(0f, screenClamp.floatingBottomPadding));
        return new Vector2(Mathf.Clamp(point.x, -xLimit, xLimit), Mathf.Clamp(point.y, -bottomLimit, topLimit));
    }

    private float EvaluateScale(Vector2 range, float targetDistance)
    {
        float minDistance = Mathf.Min(scale.distanceRange.x, scale.distanceRange.y);
        float maxDistance = Mathf.Max(scale.distanceRange.x, scale.distanceRange.y);
        float t = Mathf.InverseLerp(minDistance, maxDistance, targetDistance);
        return Mathf.Lerp(range.x, range.y, t);
    }

    private bool IsTooCloseForBrackets(float targetDistance, TargetAwarenessVisibility3D currentState)
    {
        float closeDistance = Mathf.Max(0f, distance.closeHideDistance);
        if (currentState != TargetAwarenessVisibility3D.Bracket)
        {
            return targetDistance < closeDistance;
        }

        return targetDistance < closeDistance - Mathf.Max(0f, distance.thresholdHysteresis);
    }

    private bool IsInBracketDistance(float targetDistance, TargetAwarenessVisibility3D currentState)
    {
        float bracketDistance = Mathf.Max(distance.closeHideDistance, distance.bracketMaxDistance);
        if (currentState == TargetAwarenessVisibility3D.Bracket)
        {
            return targetDistance <= bracketDistance + Mathf.Max(0f, distance.thresholdHysteresis);
        }

        return targetDistance <= bracketDistance;
    }

    private bool IsValidTarget(Entity3D entity)
    {
        if (entity == null || BoundPlayer == null || ReferenceEquals(entity, BoundPlayer))
        {
            return false;
        }

        if (discovery.ignoreInactiveTargets && !entity.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!IsLayerInMask(entity.gameObject.layer, discovery.targetMask))
        {
            return false;
        }

        if (!IsWithinAwarenessRange(entity))
        {
            return false;
        }

        if (!MatchesEnemyFaction(entity) && !IsDuelOpponent(entity))
        {
            return false;
        }

        if (ShouldSuppressSameFactionTarget(entity))
        {
            return false;
        }

        return entity.CurrentHealth > 0f;
    }

    private bool IsWithinAwarenessRange(Entity3D entity)
    {
        float awarenessRange = ResolveAwarenessRange();

        float sqrDistance = (entity.transform.position - BoundPlayer.transform.position).sqrMagnitude;
        return sqrDistance <= awarenessRange * awarenessRange;
    }

    private bool MatchesEnemyFaction(Entity3D entity)
    {
        return FactionMember3D.ResolveFaction(entity) == ResolveEnemyFaction();
    }

    private float ResolveAwarenessRange()
    {
        return distance.awarenessRange > 0f ? distance.awarenessRange : DefaultAwarenessRange;
    }

    private Faction3D ResolveEnemyFaction()
    {
        return enemyFaction != Faction3D.Neutral ? enemyFaction : Faction3D.EnemyTeam;
    }

    private bool ShouldSuppressSameFactionTarget(Entity3D entity)
    {
        if (!FactionMember3D.TryGetExplicitFaction(BoundPlayer, out Faction3D boundFaction)
            || !FactionMember3D.TryGetExplicitFaction(entity, out Faction3D targetFaction)
            || boundFaction == Faction3D.Neutral
            || boundFaction != targetFaction)
        {
            return false;
        }

        // Duel opponents still share PlayerTeam after the PvE faction rollout.
        // Preserve Player1/Player2 hostility so target awareness does not hide the remote player.
        return !IsDuelOpponent(entity);
    }

    private bool IsDuelOpponent(Entity3D entity)
    {
        if (entity == null || BoundPlayer == null || entity is not Player3D)
        {
            return false;
        }

        return TryResolveOpponentPlayerTag(BoundPlayer, out string opponentTag)
            && entity.CompareTag(opponentTag);
    }

    private static bool TryResolveOpponentPlayerTag(Entity3D entity, out string opponentTag)
    {
        opponentTag = null;
        if (entity == null)
        {
            return false;
        }

        NetMovement3D movement = entity.GetComponent<NetMovement3D>();
        byte playerSlot = movement != null ? movement.PlayerSlot : (byte)0;
        if (playerSlot == 1)
        {
            opponentTag = "Player2";
            return true;
        }

        if (playerSlot == 2)
        {
            opponentTag = "Player1";
            return true;
        }

        if (entity.CompareTag("Player1"))
        {
            opponentTag = "Player2";
            return true;
        }

        if (entity.CompareTag("Player2"))
        {
            opponentTag = "Player1";
            return true;
        }

        return false;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void EnsureTargetRuntime(Entity3D target)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            TargetRuntime3D existing = _targets[i];
            if (!ReferenceEquals(existing.Target, target))
            {
                continue;
            }

            existing.Target = target;
            _targets[i] = existing;
            return;
        }

        TargetAwarenessWidget3D widget = GetWidget();
        if (widget == null)
        {
            return;
        }

        _targets.Add(new TargetRuntime3D
        {
            Target = target,
            Widget = widget,
            CurrentState = TargetAwarenessVisibility3D.Hidden,
            LastIndicatorDirection = Vector2.up,
            LastStateChangeTime = Time.unscaledTime
        });
    }

    private TargetAwarenessWidget3D GetWidget()
    {
        for (int i = 0; i < _widgetPool.Count; i++)
        {
            TargetAwarenessWidget3D pooled = _widgetPool[i];
            if (pooled != null && !pooled.gameObject.activeSelf)
            {
                pooled.gameObject.SetActive(true);
                pooled.Initialize();
                return pooled;
            }
        }

        if (widgetTemplate == null)
        {
            if (!_loggedMissingTemplate)
            {
                Debug.LogWarning("[TargetAwarenessHUD3D] No widget template is assigned, so target indicators cannot be shown.", this);
                _loggedMissingTemplate = true;
            }

            return null;
        }

        Transform parent = ResolveWidgetParent();
        TargetAwarenessWidget3D widget = Instantiate(widgetTemplate, parent);
        widget.name = $"{widgetTemplate.name}_runtime";
        widget.gameObject.SetActive(true);
        widget.Initialize();
        _widgetPool.Add(widget);
        return widget;
    }

    private void MarkAllTargetsMissing()
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            TargetRuntime3D runtime = _targets[i];
            runtime.Target = runtime.Target != null && IsValidTarget(runtime.Target) ? runtime.Target : null;
            _targets[i] = runtime;
        }
    }

    private void RemoveMissingTargets()
    {
        for (int i = _targets.Count - 1; i >= 0; i--)
        {
            TargetRuntime3D runtime = _targets[i];
            if (runtime.Target != null && IsValidTarget(runtime.Target))
            {
                continue;
            }

            ReleaseWidget(runtime.Widget);
            _targets.RemoveAt(i);
        }
    }

    private void ClearTargetBindings()
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            ReleaseWidget(_targets[i].Widget);
        }

        _targets.Clear();
    }

    private void HideAllTargets()
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            TargetRuntime3D runtime = _targets[i];
            runtime.Widget?.ApplyPresentation(new TargetAwarenessPresentation3D
            {
                State = TargetAwarenessVisibility3D.Hidden,
                IndicatorDirection = runtime.LastIndicatorDirection,
                SnapPosition = !runtime.HasPresented
            }, Time.unscaledDeltaTime);
            _targets[i] = runtime;
        }
    }

    private static void ReleaseWidget(TargetAwarenessWidget3D widget)
    {
        if (widget == null)
        {
            return;
        }

        widget.HideImmediate();
        widget.gameObject.SetActive(false);
    }

    private Camera ResolveGameplayCamera()
    {
        if (gameplayCameraOverride != null && gameplayCameraOverride.isActiveAndEnabled)
        {
            return gameplayCameraOverride;
        }

        return Camera.main;
    }

    private void BindRenderCamera()
    {
        if (targetCanvas == null)
        {
            return;
        }

        Camera camera = ResolveGameplayCamera();
        if (camera != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            targetCanvas.worldCamera = camera;
        }
    }

    private void ResolveTemplateAuthoringConflicts()
    {
        if (widgetTemplate == null)
        {
            return;
        }

        Transform templateTransform = widgetTemplate.transform;
        if (widgetContainer == templateTransform)
        {
            widgetContainer = templateTransform.parent as RectTransform;
            LogTemplateContainerFallback(
                "[TargetAwarenessHUD3D] Widget Container was assigned to the inactive widget template. " +
                "Runtime widgets will be spawned under the template's parent instead.");
        }

        if (widgetContainer != null && !widgetContainer.gameObject.activeInHierarchy)
        {
            RectTransform fallbackParent = templateTransform.parent as RectTransform;
            if (fallbackParent != null)
            {
                widgetContainer = fallbackParent;
                LogTemplateContainerFallback(
                    "[TargetAwarenessHUD3D] Widget Container is inactive in the hierarchy. " +
                    "Runtime widgets will be spawned under the template's parent instead.");
            }
        }

        if (canvasRoot == templateTransform || (canvasRoot != null && !canvasRoot.gameObject.activeInHierarchy))
        {
            RectTransform fallbackRoot = FindActiveParentRectTransform(templateTransform);
            if (fallbackRoot != null)
            {
                canvasRoot = fallbackRoot;
                LogInactiveCanvasFallback(
                    "[TargetAwarenessHUD3D] Canvas Root was assigned to the inactive widget template. " +
                    "Using the nearest active parent RectTransform for screen projection.");
            }
        }

        if (targetCanvas == widgetTemplate.GetComponent<Canvas>() || (targetCanvas != null && !targetCanvas.gameObject.activeInHierarchy))
        {
            Canvas fallbackCanvas = FindActiveParentCanvas(templateTransform);
            if (fallbackCanvas != null)
            {
                targetCanvas = fallbackCanvas;
                LogInactiveCanvasFallback(
                    "[TargetAwarenessHUD3D] Target Canvas was assigned to the inactive widget template. " +
                    "Using the nearest active parent Canvas for camera binding.");
            }
        }
    }

    private Transform ResolveWidgetParent()
    {
        if (widgetTemplate == null)
        {
            return widgetContainer;
        }

        if (widgetContainer != null && widgetContainer.gameObject.activeInHierarchy && widgetContainer != widgetTemplate.transform)
        {
            return widgetContainer;
        }

        Transform fallbackParent = widgetTemplate.transform.parent;
        if (fallbackParent != null)
        {
            LogTemplateContainerFallback(
                "[TargetAwarenessHUD3D] Runtime widget parent was inactive or pointed at the template. " +
                "Using the template's parent so cloned widgets can become visible.");
            return fallbackParent;
        }

        return transform;
    }

    private static RectTransform FindActiveParentRectTransform(Transform start)
    {
        Transform current = start.parent;
        while (current != null)
        {
            if (current.gameObject.activeInHierarchy && current is RectTransform rectTransform)
            {
                return rectTransform;
            }

            current = current.parent;
        }

        return null;
    }

    private static Canvas FindActiveParentCanvas(Transform start)
    {
        Transform current = start.parent;
        while (current != null)
        {
            if (current.gameObject.activeInHierarchy && current.TryGetComponent(out Canvas canvas))
            {
                return canvas;
            }

            current = current.parent;
        }

        return null;
    }

    private void LogTemplateContainerFallback(string message)
    {
        if (_loggedTemplateContainerFallback)
        {
            return;
        }

        Debug.LogWarning(message, this);
        _loggedTemplateContainerFallback = true;
    }

    private void LogInactiveCanvasFallback(string message)
    {
        if (_loggedInactiveCanvasFallback)
        {
            return;
        }

        Debug.LogWarning(message, this);
        _loggedInactiveCanvasFallback = true;
    }

    private void LogMissingCanvasOnce(Camera gameplayCamera)
    {
        if (_loggedMissingCanvas)
        {
            return;
        }

        if (gameplayCamera == null || canvasRoot == null)
        {
            Debug.LogWarning("[TargetAwarenessHUD3D] Target HUD needs a gameplay camera and canvas root before it can project targets.", this);
            _loggedMissingCanvas = true;
        }
    }
}
