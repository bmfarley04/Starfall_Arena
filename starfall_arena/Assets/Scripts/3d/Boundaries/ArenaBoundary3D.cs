using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class ArenaBoundary3D : NetworkBehaviour
{
    [System.Serializable]
    public struct BoundaryWave
    {
        [Tooltip("Seconds this wave takes to reach its target size percent.")]
        public float duration;
        [Tooltip("Seconds to wait at this wave's target size before the next wave starts.")]
        public float timeUntilNextWave;
        [Tooltip("Target size as a percent of the starting arena dimensions. 100 means full size, 80 means 80% of the starting width/height/length.")]
        [Range(1f, 100f)] public float targetSizePercent;
    }

    [System.Serializable]
    private struct VisualSettings
    {
        [Tooltip("Material using Starfall/3D/ProceduralHexArenaBoundary.")]
        public Material boundaryMaterial;
        [Tooltip("Optional tileable hex mask texture. White pixels emit; black pixels stay transparent.")]
        public Texture2D hexMask;
        [Tooltip("World units from the player to the wall where hexes can begin appearing.")]
        public float revealDistance;
        [Tooltip("World-space radius of the visible patch centered on the nearest wall point.")]
        public float visiblePatchRadius;
        [Tooltip("Baseline wall visibility while active but not shrinking.")]
        [Range(0f, 1f)] public float idleVisibility;
        [Tooltip("Maximum visibility around the local player.")]
        [Range(0f, 1f)] public float proximityVisibility;
        [Tooltip("Minimum whole-arena visibility during a shrink wave.")]
        [Range(0f, 1f)] public float shrinkMinVisibility;
        [Tooltip("Maximum whole-arena visibility during a shrink pulse.")]
        [Range(0f, 1f)] public float shrinkMaxVisibility;
        [Tooltip("World units covered by one full texture tile.")]
        public float textureWorldSize;
        [Tooltip("Minimum sampled RGB brightness required before a texel contributes to the mask.")]
        [Range(0f, 1f)] public float maskThreshold;
        [Tooltip("Softness around the mask threshold.")]
        [Range(0.001f, 0.5f)] public float maskSoftness;
        [Tooltip("Raises or softens the sampled mask after thresholding. Higher values make faint pixels disappear sooner.")]
        [Range(0.25f, 4f)] public float maskPower;
        [Tooltip("Speed of the broad force-field brightness pulse.")]
        public float pulseSpeed;
        [Tooltip("Strength of the broad force-field brightness pulse.")]
        [Range(0f, 1f)] public float pulseStrength;
        [Tooltip("World-mask scale for the basic crackling energy noise.")]
        public float crackleScale;
        [Tooltip("Scroll speed for the basic crackling energy noise.")]
        public float crackleSpeed;
        [Tooltip("Strength of the basic crackling energy noise.")]
        [Range(0f, 1f)] public float crackleStrength;
        [ColorUsage(true, true)] public Color proximityColor;
        [ColorUsage(true, true)] public Color shrinkColor;
    }

    private const float MinDimension = 1f;
    private const float ViewerFallbackDistance = 100000f;
    private const int MaxRevealSamples = 12;
    private static readonly int ActiveId = Shader.PropertyToID("_Active");
    private static readonly int RevealSampleCountId = Shader.PropertyToID("_RevealSampleCount");
    private static readonly int RevealCentersId = Shader.PropertyToID("_RevealCenters");
    private static readonly int RevealDistancesId = Shader.PropertyToID("_RevealDistances");
    private static readonly int RevealDistanceId = Shader.PropertyToID("_RevealDistance");
    private static readonly int VisiblePatchRadiusId = Shader.PropertyToID("_VisiblePatchRadius");
    private static readonly int IdleVisibilityId = Shader.PropertyToID("_IdleVisibility");
    private static readonly int ProximityVisibilityId = Shader.PropertyToID("_ProximityVisibility");
    private static readonly int ShrinkVisibilityId = Shader.PropertyToID("_ShrinkVisibility");
    private static readonly int IsShrinkingId = Shader.PropertyToID("_IsShrinking");
    private static readonly int ShrinkPulseId = Shader.PropertyToID("_ShrinkPulse");
    private static readonly int HexMaskId = Shader.PropertyToID("_HexMask");
    private static readonly int TextureWorldSizeId = Shader.PropertyToID("_TextureWorldSize");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int MaskPowerId = Shader.PropertyToID("_MaskPower");
    private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId = Shader.PropertyToID("_PulseStrength");
    private static readonly int CrackleScaleId = Shader.PropertyToID("_CrackleScale");
    private static readonly int CrackleSpeedId = Shader.PropertyToID("_CrackleSpeed");
    private static readonly int CrackleStrengthId = Shader.PropertyToID("_CrackleStrength");
    private static readonly int ProximityColorId = Shader.PropertyToID("_ProximityColor");
    private static readonly int ShrinkColorId = Shader.PropertyToID("_ShrinkColor");

    private static ArenaBoundary3D _activeBoundary;

    private readonly NetworkVariable<bool> _netActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> _netShrinking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector2> _netCenter = new NetworkVariable<Vector2>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _netWidth = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _netLength = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _netMinY = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _netMaxY = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [Header("Arena Bounds")]
    [SerializeField] private Vector2 center;
    [SerializeField] private float startWidth = 240f;
    [SerializeField] private float startLength = 160f;
    [SerializeField] private float minY = -35f;
    [SerializeField] private float maxY = 35f;
    [SerializeField] private float wallThickness = 3f;

    [Header("Shrink")]
    [Tooltip("Starts the generated boundary when this component enables. Leave on for standalone arena testing; SceneManager3D can still reset/start/stop it during rounds.")]
    [SerializeField] private bool startActiveOnEnable = true;
    [SerializeField] private bool autoStart;
    [SerializeField] private List<BoundaryWave> waves = new List<BoundaryWave>();

    [Header("Generated Hex Visual")]
    [SerializeField]
    private VisualSettings visuals = new VisualSettings
    {
        revealDistance = 14f,
        visiblePatchRadius = 8f,
        idleVisibility = 0f,
        proximityVisibility = 0.9f,
        shrinkMinVisibility = 0.18f,
        shrinkMaxVisibility = 1f,
        textureWorldSize = 32f,
        maskThreshold = 0.15f,
        maskSoftness = 0.08f,
        maskPower = 1f,
        pulseSpeed = 2f,
        pulseStrength = 0.25f,
        crackleScale = 0.65f,
        crackleSpeed = 3f,
        crackleStrength = 0.2f,
        proximityColor = new Color(0.1f, 4f, 5f, 1f),
        shrinkColor = new Color(5f, 0.2f, 0.05f, 1f)
    };

    [Header("Enforcement")]
    [Tooltip("If enabled, generated blocker colliders stop players at the arena faces. Leave disabled for damage-zone boundary behavior.")]
    [SerializeField] private bool blockPlayersAtBoundary;
    [SerializeField] private float clampInterval = 0.1f;
    [SerializeField] private float inwardSafetyMargin = 0.25f;
    [SerializeField] private float maxClampRadius = 25f;

    [Header("Outside Penalty")]
    [SerializeField, Min(0.02f)] private float outsidePenaltyInterval = 0.1f;
    [Tooltip("Damage per second while outside, as a fraction of MaxHealth + MaxShield. Example: 0.05 removes 5% of total durability per second.")]
    [SerializeField, Range(0f, 1f)] private float outsideDamagePercentPerSecond = 0.05f;
    [Tooltip("Fullscreen vignette alpha sent to the local player's HUD while outside the arena.")]
    [SerializeField, Range(0f, 1f)] private float outsideVignetteAlpha = 1f;
    [SerializeField] private Color outsideVignetteColor = new Color(1f, 0f, 0f, 1f);

    [Header("Proximity Reveal")]
    [SerializeField] private float localViewerRefreshInterval = 0.25f;
    [Tooltip("Reveal trigger distance used while the local player is outside the arena.")]
    [SerializeField] private float outsideRevealDistance = 1000f;
    [Tooltip("Visible surface patch radius used while the local player is outside the arena.")]
    [SerializeField] private float outsideVisiblePatchRadius = 180f;

    private readonly List<Transform> _localViewers = new List<Transform>(2);
    private readonly List<Vector3> _meshVertices = new List<Vector3>(24);
    private readonly List<Vector3> _meshNormals = new List<Vector3>(24);
    private readonly List<Vector2> _meshUvs = new List<Vector2>(24);
    private readonly List<int> _meshTriangles = new List<int>(36);
    private readonly Vector4[] _revealCenters = new Vector4[MaxRevealSamples];
    private readonly float[] _revealDistances = new float[MaxRevealSamples];
    private readonly BoxCollider[] _blockers = new BoxCollider[6];

    private Mesh _visualMesh;
    private MeshFilter _visualMeshFilter;
    private MeshRenderer _visualRenderer;
    private Material _runtimeFallbackMaterial;
    private MaterialPropertyBlock _visualPropertyBlock;
    private float _currentWidth;
    private float _currentLength;
    private float _currentMinY;
    private float _currentMaxY;
    private float _currentSizePercent = 100f;
    private float _waveStartSizePercent = 100f;
    private float _effectiveRevealDistance;
    private float _effectiveVisiblePatchRadius;
    private int _currentWaveIndex;
    private float _waveTimer;
    private float _waveHoldTimer;
    private float _nextClampTime;
    private float _lastOutsidePenaltyTime;
    private float _nextLocalViewerRefreshTime;
    private bool _active;
    private bool _isShrinking;

    public bool Active => _active;
    public bool IsShrinking => _isShrinking;
    public bool BlocksMovement => blockPlayersAtBoundary;
    public Bounds CurrentBounds
    {
        get
        {
            float yCenter = (_currentMinY + _currentMaxY) * 0.5f;
            return new Bounds(
                new Vector3(center.x, yCenter, center.y),
                new Vector3(_currentWidth, Mathf.Abs(_currentMaxY - _currentMinY), _currentLength));
        }
    }

    private void Awake()
    {
        if (_activeBoundary == null)
        {
            _activeBoundary = this;
        }

        ValidateConfig();
        EnsureGeneratedObjects();
        ResetBoundary();
        RefreshGeneratedGeometry();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        if (_activeBoundary == null)
        {
            _activeBoundary = this;
        }

        if ((startActiveOnEnable || autoStart) && !NetMgr.IsNetworked)
        {
            StartBoundary();
        }
    }

    private void OnDisable()
    {
        if (_activeBoundary == this)
        {
            _activeBoundary = null;
        }
    }

    private void OnDestroy()
    {
        if (_visualMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_visualMesh);
            }
            else
            {
                DestroyImmediate(_visualMesh);
            }
        }

        if (_runtimeFallbackMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_runtimeFallbackMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeFallbackMaterial);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (_activeBoundary == null)
        {
            _activeBoundary = this;
        }

        if (IsServer)
        {
            SyncStateToNetwork();
            if (startActiveOnEnable || autoStart)
            {
                StartBoundary();
            }
        }
        else
        {
            ApplyNetworkState();
        }
    }

    private void Update()
    {
        if (NetMgr.IsNetworked && IsSpawned && !IsServer)
        {
            ApplyNetworkState();
        }
        else if (!NetMgr.IsNetworked || IsServer)
        {
            UpdateShrink();
            ApplyOutsideDamageIfReady();
            SyncStateToNetwork();
        }

        RefreshGeneratedGeometry();
        UpdateLocalViewers();
        PublishLocalOutsideVignettes();
        ApplyVisualState();
    }

    private void OnValidate()
    {
        ValidateConfig();
    }

    public static bool TryGetActive(out ArenaBoundary3D boundary)
    {
        boundary = _activeBoundary;
        return boundary != null && boundary.Active;
    }

    [ContextMenu("Start Boundary")]
    public void StartBoundary()
    {
        if (NetMgr.IsNetworked && IsSpawned && !IsServer)
        {
            return;
        }

        ResetBoundary();
        _active = true;
        _nextClampTime = 0f;
        _lastOutsidePenaltyTime = Time.time;
        SyncStateToNetwork();
        RefreshGeneratedGeometry();
        ApplyVisualState();
    }

    [ContextMenu("Stop Boundary")]
    public void StopBoundary()
    {
        if (NetMgr.IsNetworked && IsSpawned && !IsServer)
        {
            return;
        }

        _active = false;
        _isShrinking = false;
        SyncStateToNetwork();
        ApplyVisualState();
    }

    [ContextMenu("Reset Boundary")]
    public void ResetBoundary()
    {
        ValidateConfig();
        _currentWidth = startWidth;
        _currentLength = startLength;
        _currentSizePercent = 100f;
        _waveStartSizePercent = _currentSizePercent;
        ApplyCurrentDimensionsFromPercent();
        _currentWaveIndex = 0;
        _waveTimer = 0f;
        _waveHoldTimer = 0f;
        _isShrinking = false;
        SyncStateToNetwork();
    }

    public bool IsInside(Vector3 position, float radius = 0f)
    {
        if (!_active)
        {
            return true;
        }

        Vector3 clamped = ClampPositionInside(position, radius);
        return (clamped - position).sqrMagnitude <= 0.0001f;
    }

    public Vector3 ClampPositionInside(Vector3 position, float radius = 0f)
    {
        radius = Mathf.Min(Mathf.Max(0f, radius), maxClampRadius);
        float margin = Mathf.Max(0f, radius + inwardSafetyMargin);
        float halfWidth = Mathf.Max(0f, (_currentWidth * 0.5f) - margin);
        float halfLength = Mathf.Max(0f, (_currentLength * 0.5f) - margin);
        float yMargin = Mathf.Max(0f, inwardSafetyMargin);

        return new Vector3(
            Mathf.Clamp(position.x, center.x - halfWidth, center.x + halfWidth),
            Mathf.Clamp(position.y, _currentMinY + yMargin, _currentMaxY - yMargin),
            Mathf.Clamp(position.z, center.y - halfLength, center.y + halfLength));
    }

    public Bounds GetCurrentWorldBounds(float margin = 0f)
    {
        margin = Mathf.Max(0f, margin);
        float halfWidth = Mathf.Max(0f, (_currentWidth * 0.5f) - margin);
        float halfLength = Mathf.Max(0f, (_currentLength * 0.5f) - margin);
        float minBoundsY = Mathf.Min(_currentMinY + margin, _currentMaxY);
        float maxBoundsY = Mathf.Max(_currentMaxY - margin, minBoundsY);
        Vector3 boundsCenter = new Vector3(
            center.x,
            (minBoundsY + maxBoundsY) * 0.5f,
            center.y);
        Vector3 boundsSize = new Vector3(
            halfWidth * 2f,
            Mathf.Max(0f, maxBoundsY - minBoundsY),
            halfLength * 2f);
        return new Bounds(boundsCenter, boundsSize);
    }

    private void UpdateShrink()
    {
        if (!_active || _currentWaveIndex >= waves.Count)
        {
            _isShrinking = false;
            return;
        }

        BoundaryWave wave = waves[_currentWaveIndex];
        float duration = Mathf.Max(0.01f, wave.duration);
        float targetSizePercent = Mathf.Clamp(wave.targetSizePercent <= 0f ? 100f : wave.targetSizePercent, 1f, 100f);

        if (_waveTimer >= duration)
        {
            _currentSizePercent = targetSizePercent;
            ApplyCurrentDimensionsFromPercent();
            _isShrinking = false;

            float holdDuration = Mathf.Max(0f, wave.timeUntilNextWave);
            _waveHoldTimer += Time.deltaTime;
            if (_waveHoldTimer < holdDuration)
            {
                return;
            }

            _currentWaveIndex++;
            _waveTimer = 0f;
            _waveHoldTimer = 0f;
            _waveStartSizePercent = _currentSizePercent;
            return;
        }

        _waveTimer += Time.deltaTime;
        _isShrinking = !Mathf.Approximately(_waveStartSizePercent, targetSizePercent);
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_waveTimer / duration));
        _currentSizePercent = Mathf.Lerp(_waveStartSizePercent, targetSizePercent, progress);
        ApplyCurrentDimensionsFromPercent();
    }

    private void ApplyCurrentDimensionsFromPercent()
    {
        float scale = Mathf.Clamp(_currentSizePercent, 1f, 100f) * 0.01f;
        _currentWidth = Mathf.Max(MinDimension, startWidth * scale);
        _currentLength = Mathf.Max(MinDimension, startLength * scale);

        float startHeight = Mathf.Max(MinDimension, maxY - minY);
        float currentHeight = Mathf.Max(MinDimension, startHeight * scale);
        float yCenter = (minY + maxY) * 0.5f;
        _currentMinY = yCenter - currentHeight * 0.5f;
        _currentMaxY = yCenter + currentHeight * 0.5f;
    }

    private void ApplyOutsideDamageIfReady()
    {
        if (!_active || Time.time < _nextClampTime)
        {
            return;
        }

        float now = Time.time;
        float deltaTime = _lastOutsidePenaltyTime > 0f ? now - _lastOutsidePenaltyTime : outsidePenaltyInterval;
        _lastOutsidePenaltyTime = now;
        _nextClampTime = Time.time + Mathf.Max(0.02f, outsidePenaltyInterval);
        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            ApplyOutsideDamage(players[i], deltaTime);
        }
    }

    private void ApplyOutsideDamage(Player3D player, float deltaTime)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            return;
        }

        NetMovement3D movement = player.GetComponent<NetMovement3D>();
        if (NetMgr.IsNetworked && movement != null && !movement.IsServer)
        {
            return;
        }

        if (!IsOutsideRawBounds(player.transform.position))
        {
            return;
        }

        float totalDurability = Mathf.Max(0f, player.MaxHealth + player.MaxShield);
        float damage = totalDurability * Mathf.Clamp01(outsideDamagePercentPerSecond) * Mathf.Max(0f, deltaTime);
        player.TakeDamage(damage, player.transform.position, null, DamageSource3D.Direct);
    }

    private void EnsureGeneratedObjects()
    {
        if (_visualPropertyBlock == null)
        {
            _visualPropertyBlock = new MaterialPropertyBlock();
        }

        if (_visualMesh == null)
        {
            _visualMesh = new Mesh
            {
                name = $"{name}_GeneratedHexArenaMesh"
            };
        }

        if (_visualMeshFilter == null || _visualRenderer == null)
        {
            Transform visualTransform = transform.Find("Generated Hex Boundary Visual");
            if (visualTransform == null)
            {
                GameObject visualObject = new GameObject("Generated Hex Boundary Visual");
                visualTransform = visualObject.transform;
                visualTransform.SetParent(transform, false);
            }

            _visualMeshFilter = visualTransform.GetComponent<MeshFilter>();
            if (_visualMeshFilter == null)
            {
                _visualMeshFilter = visualTransform.gameObject.AddComponent<MeshFilter>();
            }

            _visualRenderer = visualTransform.GetComponent<MeshRenderer>();
            if (_visualRenderer == null)
            {
                _visualRenderer = visualTransform.gameObject.AddComponent<MeshRenderer>();
            }

            _visualMeshFilter.sharedMesh = _visualMesh;
            _visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _visualRenderer.receiveShadows = false;
            AssignBoundaryMaterial();
        }

        for (int i = 0; i < _blockers.Length; i++)
        {
            if (_blockers[i] != null)
            {
                continue;
            }

            string blockerName = $"Generated Boundary Blocker {i}";
            Transform blockerTransform = transform.Find(blockerName);
            if (blockerTransform == null)
            {
                GameObject blockerObject = new GameObject(blockerName);
                blockerTransform = blockerObject.transform;
                blockerTransform.SetParent(transform, false);
            }

            BoxCollider blocker = blockerTransform.GetComponent<BoxCollider>();
            if (blocker == null)
            {
                blocker = blockerTransform.gameObject.AddComponent<BoxCollider>();
            }

            _blockers[i] = blocker;
        }
    }

    private void AssignBoundaryMaterial()
    {
        if (_visualRenderer == null)
        {
            return;
        }

        if (visuals.boundaryMaterial != null)
        {
            _visualRenderer.sharedMaterial = visuals.boundaryMaterial;
            return;
        }

        Shader shader = Shader.Find("Starfall/3D/ProceduralHexArenaBoundary");
        if (shader == null)
        {
            return;
        }

        if (_runtimeFallbackMaterial == null)
        {
            _runtimeFallbackMaterial = new Material(shader)
            {
                name = $"{name}_RuntimeHexBoundaryMaterial"
            };
        }

        _visualRenderer.sharedMaterial = _runtimeFallbackMaterial;
    }

    private void RefreshGeneratedGeometry()
    {
        EnsureGeneratedObjects();

        float halfWidth = _currentWidth * 0.5f;
        float halfLength = _currentLength * 0.5f;
        float yCenter = (_currentMinY + _currentMaxY) * 0.5f;
        float height = Mathf.Max(0.01f, _currentMaxY - _currentMinY);

        Vector3 min = new Vector3(center.x - halfWidth, _currentMinY, center.y - halfLength);
        Vector3 max = new Vector3(center.x + halfWidth, _currentMaxY, center.y + halfLength);

        _meshVertices.Clear();
        _meshNormals.Clear();
        _meshUvs.Clear();
        _meshTriangles.Clear();

        AddFace(
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
            Vector3.back,
            _currentWidth,
            height);
        AddFace(
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            Vector3.forward,
            _currentWidth,
            height);
        AddFace(
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(max.x, max.y, min.z),
            Vector3.left,
            _currentLength,
            height);
        AddFace(
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            Vector3.right,
            _currentLength,
            height);
        AddFace(
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            Vector3.down,
            _currentWidth,
            _currentLength);
        AddFace(
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            Vector3.up,
            _currentWidth,
            _currentLength);

        _visualMesh.Clear();
        _visualMesh.SetVertices(_meshVertices);
        _visualMesh.SetNormals(_meshNormals);
        _visualMesh.SetUVs(0, _meshUvs);
        _visualMesh.SetTriangles(_meshTriangles, 0);
        _visualMesh.RecalculateBounds();

        ConfigureBlocker(0, new Vector3(center.x, yCenter, center.y + halfLength), Quaternion.identity, new Vector3(_currentWidth, height, wallThickness));
        ConfigureBlocker(1, new Vector3(center.x, yCenter, center.y - halfLength), Quaternion.Euler(0f, 180f, 0f), new Vector3(_currentWidth, height, wallThickness));
        ConfigureBlocker(2, new Vector3(center.x + halfWidth, yCenter, center.y), Quaternion.Euler(0f, 90f, 0f), new Vector3(_currentLength, height, wallThickness));
        ConfigureBlocker(3, new Vector3(center.x - halfWidth, yCenter, center.y), Quaternion.Euler(0f, -90f, 0f), new Vector3(_currentLength, height, wallThickness));
        ConfigureBlocker(4, new Vector3(center.x, _currentMaxY, center.y), Quaternion.Euler(90f, 0f, 0f), new Vector3(_currentWidth, _currentLength, wallThickness));
        ConfigureBlocker(5, new Vector3(center.x, _currentMinY, center.y), Quaternion.Euler(-90f, 0f, 0f), new Vector3(_currentWidth, _currentLength, wallThickness));
    }

    private void AddFace(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight, Vector3 normal, float width, float height)
    {
        int start = _meshVertices.Count;
        _meshVertices.Add(transform.InverseTransformPoint(bottomLeft));
        _meshVertices.Add(transform.InverseTransformPoint(bottomRight));
        _meshVertices.Add(transform.InverseTransformPoint(topLeft));
        _meshVertices.Add(transform.InverseTransformPoint(topRight));

        for (int i = 0; i < 4; i++)
        {
            _meshNormals.Add(transform.InverseTransformDirection(normal).normalized);
        }

        _meshUvs.Add(new Vector2(0f, 0f));
        _meshUvs.Add(new Vector2(width, 0f));
        _meshUvs.Add(new Vector2(0f, height));
        _meshUvs.Add(new Vector2(width, height));

        _meshTriangles.Add(start);
        _meshTriangles.Add(start + 2);
        _meshTriangles.Add(start + 1);
        _meshTriangles.Add(start + 2);
        _meshTriangles.Add(start + 3);
        _meshTriangles.Add(start + 1);
    }

    private void ConfigureBlocker(int index, Vector3 position, Quaternion rotation, Vector3 size)
    {
        if (index < 0 || index >= _blockers.Length || _blockers[index] == null)
        {
            return;
        }

        BoxCollider blocker = _blockers[index];
        blocker.transform.SetPositionAndRotation(position, rotation);
        blocker.transform.localScale = Vector3.one;
        blocker.center = Vector3.zero;
        blocker.size = new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));
        blocker.enabled = _active && blockPlayersAtBoundary;
    }

    private void UpdateLocalViewers()
    {
        if (!_active || Time.time < _nextLocalViewerRefreshTime)
        {
            return;
        }

        _nextLocalViewerRefreshTime = Time.time + Mathf.Max(0.05f, localViewerRefreshInterval);
        _localViewers.Clear();

        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length && _localViewers.Count < 2; i++)
        {
            Player3D player = players[i];
            if (player == null)
            {
                continue;
            }

            NetMovement3D movement = player.GetComponent<NetMovement3D>();
            if (NetMgr.IsNetworked && movement != null && !movement.IsOwner)
            {
                continue;
            }

            _localViewers.Add(player.transform);
        }
    }

    private void ApplyVisualState()
    {
        EnsureGeneratedObjects();

        if (_visualRenderer == null)
        {
            return;
        }

        AssignBoundaryMaterial();
        _visualRenderer.enabled = _active;

        _visualRenderer.GetPropertyBlock(_visualPropertyBlock);
        _visualPropertyBlock.SetFloat(ActiveId, _active ? 1f : 0f);
        int revealSampleCount = BuildRevealSamples();
        _visualPropertyBlock.SetFloat(RevealSampleCountId, revealSampleCount);
        _visualPropertyBlock.SetVectorArray(RevealCentersId, _revealCenters);
        _visualPropertyBlock.SetFloatArray(RevealDistancesId, _revealDistances);
        _visualPropertyBlock.SetFloat(RevealDistanceId, _effectiveRevealDistance);
        _visualPropertyBlock.SetFloat(VisiblePatchRadiusId, _effectiveVisiblePatchRadius);
        _visualPropertyBlock.SetFloat(IdleVisibilityId, visuals.idleVisibility);
        _visualPropertyBlock.SetFloat(ProximityVisibilityId, visuals.proximityVisibility);
        _visualPropertyBlock.SetFloat(ShrinkVisibilityId, 0f);
        _visualPropertyBlock.SetFloat(IsShrinkingId, 0f);
        _visualPropertyBlock.SetFloat(ShrinkPulseId, 0f);
        if (visuals.hexMask != null)
        {
            _visualPropertyBlock.SetTexture(HexMaskId, visuals.hexMask);
        }

        _visualPropertyBlock.SetFloat(TextureWorldSizeId, visuals.textureWorldSize);
        _visualPropertyBlock.SetFloat(MaskThresholdId, visuals.maskThreshold);
        _visualPropertyBlock.SetFloat(MaskSoftnessId, visuals.maskSoftness);
        _visualPropertyBlock.SetFloat(MaskPowerId, visuals.maskPower);
        _visualPropertyBlock.SetFloat(PulseSpeedId, visuals.pulseSpeed);
        _visualPropertyBlock.SetFloat(PulseStrengthId, visuals.pulseStrength);
        _visualPropertyBlock.SetFloat(CrackleScaleId, visuals.crackleScale);
        _visualPropertyBlock.SetFloat(CrackleSpeedId, visuals.crackleSpeed);
        _visualPropertyBlock.SetFloat(CrackleStrengthId, visuals.crackleStrength);
        _visualPropertyBlock.SetColor(ProximityColorId, visuals.proximityColor);
        _visualPropertyBlock.SetColor(ShrinkColorId, visuals.shrinkColor);
        _visualRenderer.SetPropertyBlock(_visualPropertyBlock);

        for (int i = 0; i < _blockers.Length; i++)
        {
            if (_blockers[i] != null)
            {
                _blockers[i].enabled = _active && blockPlayersAtBoundary;
            }
        }
    }

    private void PublishLocalOutsideVignettes()
    {
        if (!_active)
        {
            return;
        }

        for (int i = 0; i < _localViewers.Count; i++)
        {
            Player3D player = _localViewers[i] != null ? _localViewers[i].GetComponent<Player3D>() : null;
            if (player == null || !IsOutsideRawBounds(player.transform.position))
            {
                continue;
            }

            player.PublishHUDVignetteMessage(new PlayerHUDVignetteMessage3D(
                PlayerHUDVignetteChannel3D.ArenaBoundary,
                outsideVignetteAlpha,
                outsideVignetteColor));
        }
    }

    private int BuildRevealSamples()
    {
        _effectiveRevealDistance = visuals.revealDistance;
        _effectiveVisiblePatchRadius = visuals.visiblePatchRadius;

        for (int i = 0; i < MaxRevealSamples; i++)
        {
            _revealCenters[i] = new Vector4(ViewerFallbackDistance, ViewerFallbackDistance, ViewerFallbackDistance, 1f);
            _revealDistances[i] = ViewerFallbackDistance;
        }

        int sampleCount = 0;
        for (int i = 0; i < _localViewers.Count && sampleCount < MaxRevealSamples; i++)
        {
            Transform viewer = _localViewers[i];
            if (viewer == null)
            {
                continue;
            }

            sampleCount = AddRevealSamplesForViewer(viewer.position, sampleCount);
        }

        return sampleCount;
    }

    private int AddRevealSamplesForViewer(Vector3 position, int sampleCount)
    {
        if (IsOutsideRawBounds(position))
        {
            _effectiveRevealDistance = Mathf.Max(_effectiveRevealDistance, outsideRevealDistance);
            _effectiveVisiblePatchRadius = Mathf.Max(_effectiveVisiblePatchRadius, outsideVisiblePatchRadius);
            return AddRevealSample(GetClosestRawBoundarySurfacePoint(position), 0f, sampleCount, _effectiveRevealDistance);
        }

        float halfWidth = _currentWidth * 0.5f;
        float halfLength = _currentLength * 0.5f;
        float minX = center.x - halfWidth;
        float maxX = center.x + halfWidth;
        float minZ = center.y - halfLength;
        float maxZ = center.y + halfLength;

        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        float clampedY = Mathf.Clamp(position.y, _currentMinY, _currentMaxY);
        float clampedZ = Mathf.Clamp(position.z, minZ, maxZ);

        sampleCount = AddRevealSample(new Vector3(minX, clampedY, clampedZ), Mathf.Abs(position.x - minX), sampleCount, visuals.revealDistance);
        sampleCount = AddRevealSample(new Vector3(maxX, clampedY, clampedZ), Mathf.Abs(position.x - maxX), sampleCount, visuals.revealDistance);
        sampleCount = AddRevealSample(new Vector3(clampedX, _currentMinY, clampedZ), Mathf.Abs(position.y - _currentMinY), sampleCount, visuals.revealDistance);
        sampleCount = AddRevealSample(new Vector3(clampedX, _currentMaxY, clampedZ), Mathf.Abs(position.y - _currentMaxY), sampleCount, visuals.revealDistance);
        sampleCount = AddRevealSample(new Vector3(clampedX, clampedY, minZ), Mathf.Abs(position.z - minZ), sampleCount, visuals.revealDistance);
        sampleCount = AddRevealSample(new Vector3(clampedX, clampedY, maxZ), Mathf.Abs(position.z - maxZ), sampleCount, visuals.revealDistance);
        return sampleCount;
    }

    private int AddRevealSample(Vector3 centerPoint, float wallDistance, int sampleCount, float revealDistance)
    {
        if (sampleCount >= MaxRevealSamples || wallDistance > revealDistance)
        {
            return sampleCount;
        }

        _revealCenters[sampleCount] = new Vector4(centerPoint.x, centerPoint.y, centerPoint.z, 1f);
        _revealDistances[sampleCount] = wallDistance;
        return sampleCount + 1;
    }

    private bool IsOutsideRawBounds(Vector3 position)
    {
        float halfWidth = _currentWidth * 0.5f;
        float halfLength = _currentLength * 0.5f;
        return position.x < center.x - halfWidth ||
            position.x > center.x + halfWidth ||
            position.y < _currentMinY ||
            position.y > _currentMaxY ||
            position.z < center.y - halfLength ||
            position.z > center.y + halfLength;
    }

    private Vector3 GetClosestRawBoundarySurfacePoint(Vector3 position)
    {
        float halfWidth = _currentWidth * 0.5f;
        float halfLength = _currentLength * 0.5f;
        float minX = center.x - halfWidth;
        float maxX = center.x + halfWidth;
        float minZ = center.y - halfLength;
        float maxZ = center.y + halfLength;

        float clampedX = Mathf.Clamp(position.x, minX, maxX);
        float clampedY = Mathf.Clamp(position.y, _currentMinY, _currentMaxY);
        float clampedZ = Mathf.Clamp(position.z, minZ, maxZ);

        Vector3 closestPoint = new Vector3(minX, clampedY, clampedZ);
        float closestDistance = Mathf.Abs(position.x - minX);
        TryClosest(new Vector3(maxX, clampedY, clampedZ), Mathf.Abs(position.x - maxX));
        TryClosest(new Vector3(clampedX, _currentMinY, clampedZ), Mathf.Abs(position.y - _currentMinY));
        TryClosest(new Vector3(clampedX, _currentMaxY, clampedZ), Mathf.Abs(position.y - _currentMaxY));
        TryClosest(new Vector3(clampedX, clampedY, minZ), Mathf.Abs(position.z - minZ));
        TryClosest(new Vector3(clampedX, clampedY, maxZ), Mathf.Abs(position.z - maxZ));
        return closestPoint;

        void TryClosest(Vector3 candidate, float distance)
        {
            if (distance >= closestDistance)
            {
                return;
            }

            closestDistance = distance;
            closestPoint = candidate;
        }
    }

    private void ApplyNetworkState()
    {
        _active = _netActive.Value;
        _isShrinking = _netShrinking.Value;
        center = _netCenter.Value;
        _currentWidth = Mathf.Max(MinDimension, _netWidth.Value);
        _currentLength = Mathf.Max(MinDimension, _netLength.Value);
        _currentMinY = _netMinY.Value;
        _currentMaxY = _netMaxY.Value;
    }

    private void SyncStateToNetwork()
    {
        if (!NetMgr.IsNetworked || !IsSpawned || !IsServer)
        {
            return;
        }

        _netActive.Value = _active;
        _netShrinking.Value = _isShrinking;
        _netCenter.Value = center;
        _netWidth.Value = _currentWidth;
        _netLength.Value = _currentLength;
        _netMinY.Value = _currentMinY;
        _netMaxY.Value = _currentMaxY;
    }

    private void ValidateConfig()
    {
        startWidth = Mathf.Max(MinDimension, startWidth);
        startLength = Mathf.Max(MinDimension, startLength);
        if (maxY < minY)
        {
            (minY, maxY) = (maxY, minY);
        }

        for (int i = 0; i < waves.Count; i++)
        {
            BoundaryWave wave = waves[i];
            wave.duration = Mathf.Max(0.01f, wave.duration);
            wave.timeUntilNextWave = Mathf.Max(0f, wave.timeUntilNextWave);
            wave.targetSizePercent = Mathf.Clamp(wave.targetSizePercent <= 0f ? 100f : wave.targetSizePercent, 1f, 100f);
            waves[i] = wave;
        }

        wallThickness = Mathf.Max(0.01f, wallThickness);
        clampInterval = Mathf.Max(0.02f, clampInterval);
        inwardSafetyMargin = Mathf.Max(0f, inwardSafetyMargin);
        maxClampRadius = Mathf.Max(0f, maxClampRadius);
        outsidePenaltyInterval = Mathf.Max(0.02f, outsidePenaltyInterval);
        outsideDamagePercentPerSecond = Mathf.Clamp01(outsideDamagePercentPerSecond);
        outsideVignetteAlpha = Mathf.Clamp01(outsideVignetteAlpha);
        localViewerRefreshInterval = Mathf.Max(0.05f, localViewerRefreshInterval);
        outsideRevealDistance = Mathf.Max(0.01f, outsideRevealDistance);
        outsideVisiblePatchRadius = Mathf.Max(0.01f, outsideVisiblePatchRadius);
        visuals.revealDistance = Mathf.Max(0.01f, visuals.revealDistance);
        visuals.visiblePatchRadius = Mathf.Max(0.01f, visuals.visiblePatchRadius);
        visuals.proximityVisibility = Mathf.Max(visuals.idleVisibility, visuals.proximityVisibility);
        visuals.shrinkMaxVisibility = Mathf.Max(visuals.shrinkMinVisibility, visuals.shrinkMaxVisibility);
        visuals.textureWorldSize = Mathf.Max(0.01f, visuals.textureWorldSize);
        visuals.maskSoftness = Mathf.Max(0.001f, visuals.maskSoftness);
        visuals.maskPower = Mathf.Max(0.25f, visuals.maskPower);
        visuals.pulseSpeed = Mathf.Max(0f, visuals.pulseSpeed);
        visuals.crackleScale = Mathf.Max(0.001f, visuals.crackleScale);
        visuals.crackleSpeed = Mathf.Max(0f, visuals.crackleSpeed);
    }
}
