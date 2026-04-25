using System.Collections.Generic;
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
    [SerializeField] private MeshFilter forceFieldMeshFilter;
    [SerializeField] private BoxCollider blocker;
    [SerializeField] private bool autoPlaceTransform = true;
    [SerializeField] private bool autoScaleVisual = true;
    [SerializeField] private bool buildWorldScaleVisualMesh = true;
    [SerializeField] private bool buildSegmentedVisuals = true;
    [SerializeField] private bool autoScaleBlocker = true;

    [Header("Texture Scale")]
    [SerializeField] private float textureWorldSize = 75f;
    [SerializeField] private float visualSegmentWorldSize = 100f;
    [SerializeField] private int maxVisualSegments = 256;

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
    [SerializeField] [Range(0f, 1f)] private float minProximityStaticVisibility = 0.05f;
    [SerializeField] [Range(0f, 1f)] private float maxProximityStaticVisibility = 0.65f;
    [SerializeField] private float proximityFadeSpeed = 8f;

    private MaterialPropertyBlock _propertyBlock;
    private readonly List<VisualSegment> _visualSegments = new List<VisualSegment>();
    private float _halfThickness = 0.5f;
    private Vector3 _center;
    private Vector3 _normal;
    private Vector3 _tangentA;
    private Vector3 _tangentB;
    private float _halfSpanA;
    private float _halfSpanB;
    private float _minY;
    private float _maxY;
    private Mesh _runtimeVisualMesh;
    private Transform _segmentRoot;

    public WallSide Side => side;

    private sealed class VisualSegment
    {
        public GameObject GameObject;
        public Mesh Mesh;
        public MeshFilter MeshFilter;
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public Vector2 Center;
        public Vector2 HalfSize;
        public float Visibility;
        public float TargetVisibility;
    }

    private void Awake()
    {
        CacheReferences();
        _propertyBlock = new MaterialPropertyBlock();
    }

    private void OnValidate()
    {
        revealDistance = Mathf.Max(0f, revealDistance);
        maxProximityStaticVisibility = Mathf.Max(minProximityStaticVisibility, maxProximityStaticVisibility);
        proximityFadeSpeed = Mathf.Max(0.01f, proximityFadeSpeed);
        textureWorldSize = Mathf.Max(0.01f, textureWorldSize);
        visualSegmentWorldSize = Mathf.Max(1f, visualSegmentWorldSize);
        maxVisualSegments = Mathf.Max(1, maxVisualSegments);
        shrinkMaxStaticVisibility = Mathf.Max(shrinkMinStaticVisibility, shrinkMaxStaticVisibility);
        CacheReferences();
    }

    private void OnDestroy()
    {
        if (_runtimeVisualMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_runtimeVisualMesh);
        }
        else
        {
            DestroyImmediate(_runtimeVisualMesh);
        }

        for (int i = 0; i < _visualSegments.Count; i++)
        {
            DestroyVisualSegment(_visualSegments[i]);
        }
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
                : idleStaticVisibility
            : 0f;
        Color innerTint = shrinking ? shrinkInnerTint : idleInnerTint;
        Color outerTint = shrinking ? shrinkOuterTint : idleOuterTint;

        forceFieldRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(StaticVisibilityId, staticVisibility);
        _propertyBlock.SetColor(InnerTintId, innerTint);
        _propertyBlock.SetColor(OuterTintId, outerTint);
        forceFieldRenderer.SetPropertyBlock(_propertyBlock);
        ApplySegmentBaseState(active, shrinking, staticVisibility, innerTint, outerTint);

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
        float distance = Vector3.Distance(viewer.position, closestPoint);
        if (distance > revealDistance)
        {
            return;
        }

        if (_visualSegments.Count == 0)
        {
            return;
        }

        Vector3 wallOffset = closestPoint - _center;
        Vector2 closestSurfacePoint = new Vector2(
            Vector3.Dot(wallOffset, _tangentA),
            Vector3.Dot(wallOffset, _tangentB));

        for (int i = 0; i < _visualSegments.Count; i++)
        {
            VisualSegment segment = _visualSegments[i];
            float surfaceDistance = DistanceToSegmentRect(closestSurfacePoint, segment);
            float segmentDistance = Mathf.Sqrt(distance * distance + surfaceDistance * surfaceDistance);
            if (segmentDistance > revealDistance)
            {
                continue;
            }

            float closeness = 1f - Mathf.Clamp01(segmentDistance / Mathf.Max(0.01f, revealDistance));
            float revealStrength = Mathf.SmoothStep(0f, 1f, closeness);
            float visibility = Mathf.Lerp(minProximityStaticVisibility, maxProximityStaticVisibility, revealStrength);
            segment.TargetVisibility = Mathf.Max(segment.TargetVisibility, visibility);
        }
    }

    private void LateUpdate()
    {
        if (_visualSegments.Count == 0)
        {
            return;
        }

        float lerpFactor = 1f - Mathf.Exp(-proximityFadeSpeed * Time.deltaTime);
        for (int i = 0; i < _visualSegments.Count; i++)
        {
            VisualSegment segment = _visualSegments[i];
            segment.Visibility = Mathf.Lerp(segment.Visibility, Mathf.Clamp01(segment.TargetVisibility), lerpFactor);
            ApplySegmentPropertyBlock(segment, segment.Visibility);
        }
    }

    private static float DistanceToSegmentRect(Vector2 point, VisualSegment segment)
    {
        float dx = Mathf.Max(Mathf.Abs(point.x - segment.Center.x) - segment.HalfSize.x, 0f);
        float dy = Mathf.Max(Mathf.Abs(point.y - segment.Center.y) - segment.HalfSize.y, 0f);
        return Mathf.Sqrt(dx * dx + dy * dy);
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

            if (buildWorldScaleVisualMesh && forceFieldMeshFilter != null)
            {
                forceFieldRenderer.transform.localScale = Vector3.one;
                if (buildSegmentedVisuals)
                {
                    RebuildVisualSegments(width, height);
                }
                else
                {
                    ClearVisualSegments();
                    RebuildVisualMesh(width, height);
                }
            }
            else
            {
                ClearVisualSegments();
                forceFieldRenderer.transform.localScale = new Vector3(width, height, 1f);
            }
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

        if (forceFieldMeshFilter == null)
        {
            forceFieldMeshFilter = GetComponentInChildren<MeshFilter>();
        }

        if (blocker == null)
        {
            blocker = GetComponentInChildren<BoxCollider>();
        }
    }

    private void RebuildVisualMesh(float width, float height)
    {
        if (_runtimeVisualMesh == null)
        {
            _runtimeVisualMesh = new Mesh
            {
                name = $"{name}_BoundaryWallRuntimeMesh"
            };
        }

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float uvWidth = width / textureWorldSize;
        float uvHeight = height / textureWorldSize;

        _runtimeVisualMesh.Clear();
        _runtimeVisualMesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f)
        };
        _runtimeVisualMesh.uv = new[]
        {
            Vector2.zero,
            new Vector2(uvWidth, 0f),
            new Vector2(0f, uvHeight),
            new Vector2(uvWidth, uvHeight)
        };
        _runtimeVisualMesh.normals = new[]
        {
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back
        };
        _runtimeVisualMesh.triangles = new[]
        {
            0, 2, 1,
            2, 3, 1
        };
        _runtimeVisualMesh.RecalculateBounds();
        forceFieldMeshFilter.sharedMesh = _runtimeVisualMesh;
        forceFieldRenderer.enabled = true;
    }

    private void RebuildVisualSegments(float width, float height)
    {
        if (forceFieldRenderer == null)
        {
            return;
        }

        EnsureSegmentRoot();
        int columns = Mathf.Max(1, Mathf.CeilToInt(width / visualSegmentWorldSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(height / visualSegmentWorldSize));
        while (columns * rows > maxVisualSegments)
        {
            if (columns >= rows && columns > 1)
            {
                columns--;
            }
            else if (rows > 1)
            {
                rows--;
            }
            else
            {
                break;
            }
        }

        int requiredSegments = columns * rows;
        EnsureVisualSegmentCount(requiredSegments);
        forceFieldRenderer.enabled = false;

        float segmentWidth = width / columns;
        float segmentHeight = height / rows;
        float startX = -width * 0.5f;
        float startY = -height * 0.5f;
        int index = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                float minX = startX + x * segmentWidth;
                float minY = startY + y * segmentHeight;
                float maxX = minX + segmentWidth;
                float maxY = minY + segmentHeight;
                ConfigureVisualSegment(_visualSegments[index], minX, minY, maxX, maxY);
                index++;
            }
        }
    }

    private void EnsureSegmentRoot()
    {
        if (_segmentRoot != null)
        {
            return;
        }

        GameObject root = new GameObject($"{name}_VisualSegments");
        _segmentRoot = root.transform;
        _segmentRoot.SetParent(forceFieldRenderer.transform, false);
        _segmentRoot.localPosition = Vector3.zero;
        _segmentRoot.localRotation = Quaternion.identity;
        _segmentRoot.localScale = Vector3.one;
    }

    private void EnsureVisualSegmentCount(int count)
    {
        while (_visualSegments.Count < count)
        {
            _visualSegments.Add(CreateVisualSegment(_visualSegments.Count));
        }

        for (int i = 0; i < _visualSegments.Count; i++)
        {
            _visualSegments[i].GameObject.SetActive(i < count);
        }
    }

    private VisualSegment CreateVisualSegment(int index)
    {
        GameObject segmentObject = new GameObject($"Visual Segment {index:00}");
        segmentObject.transform.SetParent(_segmentRoot, false);
        MeshFilter meshFilter = segmentObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = segmentObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = forceFieldRenderer.sharedMaterials;
        meshRenderer.shadowCastingMode = forceFieldRenderer.shadowCastingMode;
        meshRenderer.receiveShadows = forceFieldRenderer.receiveShadows;
        meshRenderer.lightProbeUsage = forceFieldRenderer.lightProbeUsage;
        meshRenderer.reflectionProbeUsage = forceFieldRenderer.reflectionProbeUsage;

        Mesh mesh = new Mesh
        {
            name = $"{name}_BoundaryWallSegment_{index:00}"
        };
        meshFilter.sharedMesh = mesh;

        return new VisualSegment
        {
            GameObject = segmentObject,
            Mesh = mesh,
            MeshFilter = meshFilter,
            Renderer = meshRenderer,
            PropertyBlock = new MaterialPropertyBlock()
        };
    }

    private void ConfigureVisualSegment(VisualSegment segment, float minX, float minY, float maxX, float maxY)
    {
        float width = maxX - minX;
        float height = maxY - minY;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;

        segment.GameObject.transform.localPosition = new Vector3(centerX, centerY, 0f);
        segment.GameObject.transform.localRotation = Quaternion.identity;
        segment.GameObject.transform.localScale = Vector3.one;
        segment.Center = new Vector2(centerX, centerY);
        segment.HalfSize = new Vector2(halfWidth, halfHeight);

        segment.Mesh.Clear();
        segment.Mesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f)
        };
        segment.Mesh.uv = new[]
        {
            new Vector2(minX / textureWorldSize, minY / textureWorldSize),
            new Vector2(maxX / textureWorldSize, minY / textureWorldSize),
            new Vector2(minX / textureWorldSize, maxY / textureWorldSize),
            new Vector2(maxX / textureWorldSize, maxY / textureWorldSize)
        };
        segment.Mesh.normals = new[]
        {
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back
        };
        segment.Mesh.triangles = new[]
        {
            0, 2, 1,
            2, 3, 1
        };
        segment.Mesh.RecalculateBounds();
    }

    private void ApplySegmentBaseState(bool active, bool shrinking, float staticVisibility, Color innerTint, Color outerTint)
    {
        for (int i = 0; i < _visualSegments.Count; i++)
        {
            VisualSegment segment = _visualSegments[i];
            if (!segment.GameObject.activeSelf)
            {
                continue;
            }

            segment.TargetVisibility = active
                ? shrinking ? staticVisibility : idleStaticVisibility
                : 0f;
            if (shrinking || !active)
            {
                segment.Visibility = segment.TargetVisibility;
            }

            segment.PropertyBlock.SetColor(InnerTintId, innerTint);
            segment.PropertyBlock.SetColor(OuterTintId, outerTint);
            segment.PropertyBlock.SetFloat(StaticVisibilityId, shrinking || !Application.isPlaying ? segment.TargetVisibility : segment.Visibility);
            segment.Renderer.SetPropertyBlock(segment.PropertyBlock);
        }
    }

    private void ApplySegmentPropertyBlock(VisualSegment segment, float staticVisibility)
    {
        segment.Renderer.GetPropertyBlock(segment.PropertyBlock);
        segment.PropertyBlock.SetFloat(StaticVisibilityId, staticVisibility);
        segment.Renderer.SetPropertyBlock(segment.PropertyBlock);
    }

    private void ClearVisualSegments()
    {
        forceFieldRenderer.enabled = true;
        for (int i = 0; i < _visualSegments.Count; i++)
        {
            DestroyVisualSegment(_visualSegments[i]);
        }

        _visualSegments.Clear();
        if (_segmentRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_segmentRoot.gameObject);
        }
        else
        {
            DestroyImmediate(_segmentRoot.gameObject);
        }

        _segmentRoot = null;
    }

    private static void DestroyVisualSegment(VisualSegment segment)
    {
        if (segment == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(segment.Mesh);
            Object.Destroy(segment.GameObject);
        }
        else
        {
            Object.DestroyImmediate(segment.Mesh);
            Object.DestroyImmediate(segment.GameObject);
        }
    }
}
