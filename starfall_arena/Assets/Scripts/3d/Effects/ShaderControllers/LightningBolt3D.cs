using UnityEngine;

/// <summary>
/// Dynamically builds and orients a camera-facing billboard quad stretched between
/// two world-space Transform anchors, then drives LightningBolt3D.shader on it.
///
/// Setup:
///   1. Add this component to a child GameObject on your ship (position does not matter).
///   2. Assign <see cref="BoltSetup.startPoint"/> and <see cref="BoltSetup.endPoint"/>
///      to the Transforms marking the wing-tip and body attachment point.
///   3. Assign a Material using the Custom/LightningBolt3D shader.
///   4. Call <see cref="Activate"/> when entering anchor mode, <see cref="Deactivate"/> when leaving.
///
/// For multiple bolts (e.g. left wing → body AND right wing → body), add one
/// LightningBolt3D component per bolt — each manages its own quad and material block.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightningBolt3D : MonoBehaviour
{
    [System.Serializable]
    public struct BoltSetup
    {
        [Tooltip("Start anchor (e.g. wing tip Transform)")]
        public Transform startPoint;

        [Tooltip("End anchor (e.g. body attachment Transform)")]
        public Transform endPoint;

        [Tooltip("Width of the billboard quad in world units. Should be wide enough " +
                 "that branches remain visible — roughly 30–50% of expected bolt length.")]
        public float quadWidth;
    }

    [Header("Bolt Setup")]
    [SerializeField] private BoltSetup bolt = new BoltSetup { quadWidth = 2f };

    [Header("Material")]
    [Tooltip("Material using the Custom/LightningBolt3D shader.")]
    [SerializeField] private Material lightningMaterial;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private MeshFilter   _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh         _mesh;
    private Camera       _mainCamera;
    private MaterialPropertyBlock _propBlock;
    private bool         _meshInitialized;

    // Pre-allocated to avoid per-frame GC pressure.
    private readonly Vector3[] _vertices = new Vector3[4];

    // UVs and triangle winding never change: set once on first UpdateQuad() call.
    private static readonly Vector2[] _staticUVs =
    {
        new Vector2(0f, 0f),   // v0: start, -widthAxis
        new Vector2(0f, 1f),   // v1: start, +widthAxis
        new Vector2(1f, 1f),   // v2: end,   +widthAxis
        new Vector2(1f, 0f),   // v3: end,   -widthAxis
    };

    private static readonly int[] _staticTriangles = { 0, 1, 2, 0, 2, 3 };

    // Shader property ID — cached to avoid repeated string hashing.
    private static readonly int ShaderBoltLength = Shader.PropertyToID("_BoltLength");

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _meshFilter   = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _propBlock    = new MaterialPropertyBlock();

        _mesh = new Mesh { name = "LightningBoltQuad" };
        _mesh.MarkDynamic();    // Hint to Unity that vertices will change every frame.
        _meshFilter.mesh = _mesh;

        if (lightningMaterial != null)
            _meshRenderer.sharedMaterial = lightningMaterial;

        // Start hidden; Activate() enables rendering.
        _meshRenderer.enabled = false;
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // LateUpdate ensures the quad is rebuilt after any physics/animation
        // that may have moved the anchor Transforms this frame.
        if (!_meshRenderer.enabled) return;
        if (bolt.startPoint == null || bolt.endPoint == null) return;

        UpdateQuad();
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Enables lightning rendering. Call when entering anchor mode.</summary>
    public void Activate()
    {
        _meshRenderer.enabled = true;
    }

    /// <summary>Disables lightning rendering. Call when leaving anchor mode.</summary>
    public void Deactivate()
    {
        _meshRenderer.enabled = false;
    }

    /// <summary>Returns true while the bolt is visible.</summary>
    public bool IsActive => _meshRenderer != null && _meshRenderer.enabled;

    // -------------------------------------------------------------------------
    // Quad construction
    // -------------------------------------------------------------------------

    private void UpdateQuad()
    {
        // Resolve camera lazily (scene changes, etc.)
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 start = bolt.startPoint.position;
        Vector3 end   = bolt.endPoint.position;

        Vector3 boltVec    = end - start;
        float   boltLength = boltVec.magnitude;
        if (boltLength < 0.001f) return;

        Vector3 boltDirN = boltVec / boltLength;

        // Width axis: perpendicular to the bolt, oriented so the quad faces the camera.
        // Cross(boltDir, toCamera) gives a vector perpendicular to both, which sweeps
        // the quad "around" the bolt as the camera orbits — preventing the billboard
        // from ever going edge-on.
        Vector3 midpoint = (start + end) * 0.5f;
        Vector3 toCamera = (_mainCamera.transform.position - midpoint).normalized;
        Vector3 widthAxis = Vector3.Cross(boltDirN, toCamera);

        // Degenerate guard: bolt is pointing almost directly at the camera.
        if (widthAxis.sqrMagnitude < 0.0001f)
            widthAxis = Vector3.Cross(boltDirN, _mainCamera.transform.up);

        widthAxis = widthAxis.normalized * (bolt.quadWidth * 0.5f);

        // Build the four quad corners in world space, then convert to local space.
        // Using InverseTransformPoint means this script works regardless of where
        // its GameObject sits in the hierarchy or what transforms it inherits.
        _vertices[0] = transform.InverseTransformPoint(start - widthAxis);
        _vertices[1] = transform.InverseTransformPoint(start + widthAxis);
        _vertices[2] = transform.InverseTransformPoint(end   + widthAxis);
        _vertices[3] = transform.InverseTransformPoint(end   - widthAxis);

        _mesh.vertices = _vertices;

        // UVs and triangles only need to be written once.
        if (!_meshInitialized)
        {
            _mesh.uv        = _staticUVs;
            _mesh.triangles = _staticTriangles;
            _meshInitialized = true;
        }

        // Recalculate bounds so Unity's frustum culling remains accurate as the
        // ship (and anchors) move through the world.
        _mesh.RecalculateBounds();

        // Push the current world-space bolt length to the shader.
        // Using a MaterialPropertyBlock avoids creating a material instance per bolt,
        // so multiple LightningBolt3D components can share one Material asset.
        _meshRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(ShaderBoltLength, boltLength);
        _meshRenderer.SetPropertyBlock(_propBlock);
    }
}
