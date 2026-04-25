using Forge3D;
using UnityEngine;

[DisallowMultipleComponent]
public class ForceFieldBoundaryWall3D : MonoBehaviour
{
    public enum WallSide
    {
        North,
        South,
        East,
        West,
        Top,
        Bottom
    }

    private static readonly int StaticVisibilityId = Shader.PropertyToID("_Static");
    private static readonly int InnerTintId = Shader.PropertyToID("_InnerTint");
    private static readonly int OuterTintId = Shader.PropertyToID("_OuterTint");

    [Header("Wall")]
    [SerializeField] private WallSide side;
    [SerializeField] private Renderer forceFieldRenderer;
    [SerializeField] private Forcefield forceField;
    [SerializeField] private BoxCollider blocker;
    [SerializeField] private bool autoPlaceTransform = true;
    [SerializeField] private bool autoScaleVisual = true;
    [SerializeField] private bool autoScaleBlocker = true;

    [Header("Idle Visuals")]
    [SerializeField] private Color idleInnerTint = new Color(0.35f, 0.9f, 1f, 0.45f);
    [SerializeField] private Color idleOuterTint = new Color(0.1f, 0.45f, 1f, 0.4f);
    [SerializeField] [Range(0f, 1f)] private float idleStaticVisibility;

    [Header("Shrink Warning Visuals")]
    [SerializeField] private Color shrinkInnerTint = new Color(1f, 0.38f, 0.18f, 0.9f);
    [SerializeField] private Color shrinkOuterTint = new Color(1f, 0.08f, 0.02f, 0.85f);
    [SerializeField] [Range(0f, 1f)] private float shrinkMinStaticVisibility = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float shrinkMaxStaticVisibility = 1f;

    [Header("Proximity Reveal")]
    [SerializeField] private float revealDistance = 12f;
    [SerializeField] [Range(0f, 1f)] private float proximityStaticVisibility = 0.35f;
    [SerializeField] private float revealHitPower = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float revealHitAlpha = 0.85f;
    [SerializeField] private float revealRefreshInterval = 0.08f;
    [SerializeField] private float proximityFadeSpeed = 8f;

    private MaterialPropertyBlock _propertyBlock;
    private float _nextRevealTime;
    private float _proximityVisibility;
    private float _targetProximityVisibility;
    private float _halfThickness = 0.5f;
    private Vector3 _center;
    private Vector3 _normal;
    private Vector3 _tangentA;
    private Vector3 _tangentB;
    private float _halfSpanA;
    private float _halfSpanB;
    private float _minY;
    private float _maxY;

    public WallSide Side => side;

    private void Awake()
    {
        CacheReferences();
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void OnValidate()
    {
        revealDistance = Mathf.Max(0f, revealDistance);
        revealHitPower = Mathf.Max(0f, revealHitPower);
        revealRefreshInterval = Mathf.Max(0.01f, revealRefreshInterval);
        proximityFadeSpeed = Mathf.Max(0.01f, proximityFadeSpeed);
        shrinkMaxStaticVisibility = Mathf.Max(shrinkMinStaticVisibility, shrinkMaxStaticVisibility);
        CacheReferences();
    }

    public void Configure(
        WallSide configuredSide,
        Vector2 arenaCenter,
        float arenaWidth,
        float arenaLength,
        float minY,
        float maxY,
        float wallThickness)
    {
        side = configuredSide;
        Configure(arenaCenter, arenaWidth, arenaLength, minY, maxY, wallThickness);
    }

    public void Configure(
        Vector2 arenaCenter,
        float arenaWidth,
        float arenaLength,
        float minY,
        float maxY,
        float wallThickness)
    {
        _halfThickness = Mathf.Max(0.01f, wallThickness * 0.5f);
        _minY = Mathf.Min(minY, maxY);
        _maxY = Mathf.Max(minY, maxY);

        float height = Mathf.Max(0.01f, _maxY - _minY);
        float y = (_minY + _maxY) * 0.5f;
        float halfWidth = Mathf.Max(0.01f, arenaWidth * 0.5f);
        float halfLength = Mathf.Max(0.01f, arenaLength * 0.5f);
        float halfHeight = height * 0.5f;

        switch (side)
        {
            case WallSide.North:
                _center = new Vector3(arenaCenter.x, y, arenaCenter.y + halfLength);
                _normal = Vector3.back;
                SetSurfaceAxes(Vector3.right, Vector3.up, halfWidth, halfHeight);
                ConfigureTransform(_center, Quaternion.identity, arenaWidth, height, wallThickness);
                break;
            case WallSide.South:
                _center = new Vector3(arenaCenter.x, y, arenaCenter.y - halfLength);
                _normal = Vector3.forward;
                SetSurfaceAxes(Vector3.right, Vector3.up, halfWidth, halfHeight);
                ConfigureTransform(_center, Quaternion.Euler(0f, 180f, 0f), arenaWidth, height, wallThickness);
                break;
            case WallSide.East:
                _center = new Vector3(arenaCenter.x + halfWidth, y, arenaCenter.y);
                _normal = Vector3.left;
                SetSurfaceAxes(Vector3.forward, Vector3.up, halfLength, halfHeight);
                ConfigureTransform(_center, Quaternion.Euler(0f, 90f, 0f), arenaLength, height, wallThickness);
                break;
            case WallSide.West:
                _center = new Vector3(arenaCenter.x - halfWidth, y, arenaCenter.y);
                _normal = Vector3.right;
                SetSurfaceAxes(Vector3.forward, Vector3.up, halfLength, halfHeight);
                ConfigureTransform(_center, Quaternion.Euler(0f, -90f, 0f), arenaLength, height, wallThickness);
                break;
            case WallSide.Top:
                _center = new Vector3(arenaCenter.x, _maxY, arenaCenter.y);
                _normal = Vector3.down;
                SetSurfaceAxes(Vector3.right, Vector3.forward, halfWidth, halfLength);
                ConfigureTransform(_center, Quaternion.Euler(90f, 0f, 0f), arenaWidth, arenaLength, wallThickness);
                break;
            case WallSide.Bottom:
                _center = new Vector3(arenaCenter.x, _minY, arenaCenter.y);
                _normal = Vector3.up;
                SetSurfaceAxes(Vector3.right, Vector3.forward, halfWidth, halfLength);
                ConfigureTransform(_center, Quaternion.Euler(-90f, 0f, 0f), arenaWidth, arenaLength, wallThickness);
                break;
        }
    }

    public void ApplyVisualState(bool active, bool shrinking, float warningPulse)
    {
        if (forceFieldRenderer == null)
        {
            return;
        }

        float staticVisibility = active
            ? shrinking
                ? Mathf.Lerp(shrinkMinStaticVisibility, shrinkMaxStaticVisibility, Mathf.Clamp01(warningPulse))
                : Mathf.Max(idleStaticVisibility, _proximityVisibility)
            : 0f;
        Color innerTint = shrinking ? shrinkInnerTint : idleInnerTint;
        Color outerTint = shrinking ? shrinkOuterTint : idleOuterTint;

        forceFieldRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(StaticVisibilityId, staticVisibility);
        _propertyBlock.SetColor(InnerTintId, innerTint);
        _propertyBlock.SetColor(OuterTintId, outerTint);
        forceFieldRenderer.SetPropertyBlock(_propertyBlock);
        _targetProximityVisibility = 0f;

        if (blocker != null)
        {
            blocker.enabled = active;
        }
    }

    public void UpdateProximityReveal(Transform viewer)
    {
        if (viewer == null)
        {
            return;
        }

        Vector3 closestPoint = GetClosestPointOnWall(viewer.position);
        if ((viewer.position - closestPoint).sqrMagnitude > revealDistance * revealDistance)
        {
            return;
        }

        _targetProximityVisibility = Mathf.Max(_targetProximityVisibility, proximityStaticVisibility);
        if (forceField == null || Time.time < _nextRevealTime)
        {
            return;
        }

        forceField.OnHit(closestPoint, revealHitPower, revealHitAlpha);
        _nextRevealTime = Time.time + revealRefreshInterval;
    }

    private void LateUpdate()
    {
        float target = Mathf.Clamp01(_targetProximityVisibility);
        float lerpFactor = 1f - Mathf.Exp(-proximityFadeSpeed * Time.deltaTime);
        _proximityVisibility = Mathf.Lerp(_proximityVisibility, target, lerpFactor);
    }

    private Vector3 GetClosestPointOnWall(Vector3 position)
    {
        Vector3 localOffset = position - _center;
        float tangentAOffset = Mathf.Clamp(Vector3.Dot(localOffset, _tangentA), -_halfSpanA, _halfSpanA);
        float tangentBOffset = Mathf.Clamp(Vector3.Dot(localOffset, _tangentB), -_halfSpanB, _halfSpanB);
        return _center + _tangentA * tangentAOffset + _tangentB * tangentBOffset;
    }

    private void SetSurfaceAxes(Vector3 tangentA, Vector3 tangentB, float halfSpanA, float halfSpanB)
    {
        _tangentA = tangentA.normalized;
        _tangentB = tangentB.normalized;
        _halfSpanA = Mathf.Max(0.01f, halfSpanA);
        _halfSpanB = Mathf.Max(0.01f, halfSpanB);
    }

    private void ConfigureTransform(Vector3 center, Quaternion rotation, float width, float height, float thickness)
    {
        if (autoPlaceTransform)
        {
            transform.SetPositionAndRotation(center, rotation);
        }

        if (forceFieldRenderer != null && autoScaleVisual)
        {
            if (forceFieldRenderer.transform != transform)
            {
                forceFieldRenderer.transform.localPosition = Vector3.zero;
                forceFieldRenderer.transform.localRotation = Quaternion.identity;
            }

            forceFieldRenderer.transform.localScale = new Vector3(width, height, 1f);
        }

        if (blocker != null && autoScaleBlocker)
        {
            if (blocker.transform != transform)
            {
                blocker.transform.localPosition = Vector3.zero;
                blocker.transform.localRotation = Quaternion.identity;
            }

            bool blockerUsesScaledVisualTransform = forceFieldRenderer != null && blocker.transform == forceFieldRenderer.transform;
            blocker.size = blockerUsesScaledVisualTransform
                ? new Vector3(1f, 1f, Mathf.Max(0.01f, thickness))
                : new Vector3(width, height, Mathf.Max(0.01f, thickness));
            blocker.center = Vector3.zero;
        }
    }

    private void CacheReferences()
    {
        if (forceFieldRenderer == null)
        {
            forceFieldRenderer = GetComponentInChildren<Renderer>();
        }

        if (forceField == null)
        {
            forceField = GetComponentInChildren<Forcefield>();
        }

        if (blocker == null)
        {
            blocker = GetComponentInChildren<BoxCollider>();
        }
    }
}
