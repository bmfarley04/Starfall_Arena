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
        [Tooltip("Seconds this wave takes to reach its target dimensions.")]
        public float duration;
        [Tooltip("Target arena width on world X.")]
        public float targetWidth;
        [Tooltip("Target arena length on world Z.")]
        public float targetLength;
        [Tooltip("If enabled, this wave waits without shrinking.")]
        public bool stationary;
    }

    private const float MinDimension = 1f;

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

    [Header("Arena Bounds")]
    [SerializeField] private Vector2 center;
    [SerializeField] private float startWidth = 240f;
    [SerializeField] private float startLength = 160f;
    [SerializeField] private float minY = -35f;
    [SerializeField] private float maxY = 35f;
    [SerializeField] private float wallThickness = 3f;

    [Header("Shrink")]
    [SerializeField] private bool autoStart;
    [SerializeField] private List<BoundaryWave> waves = new List<BoundaryWave>();

    [Header("Walls")]
    [SerializeField] private ForceFieldBoundaryWall3D[] walls;

    [Header("Warning Visuals")]
    [SerializeField] private float warningFlashRate = 4f;

    [Header("Enforcement")]
    [SerializeField] private float clampInterval = 0.1f;
    [SerializeField] private float inwardSafetyMargin = 0.25f;
    [SerializeField] private float maxClampRadius = 25f;

    [Header("Proximity Reveal")]
    [SerializeField] private float localViewerRefreshInterval = 0.25f;

    private readonly List<Transform> _localViewers = new List<Transform>(2);
    private float _currentWidth;
    private float _currentLength;
    private float _waveStartWidth;
    private float _waveStartLength;
    private int _currentWaveIndex;
    private float _waveTimer;
    private float _nextClampTime;
    private float _nextLocalViewerRefreshTime;
    private bool _active;
    private bool _isShrinking;

    public bool Active => _active;
    public bool IsShrinking => _isShrinking;
    public Bounds CurrentBounds
    {
        get
        {
            float yCenter = (minY + maxY) * 0.5f;
            return new Bounds(
                new Vector3(center.x, yCenter, center.y),
                new Vector3(_currentWidth, Mathf.Abs(maxY - minY), _currentLength));
        }
    }

    private void Awake()
    {
        if (_activeBoundary == null)
        {
            _activeBoundary = this;
        }

        ValidateConfig();
        ResetBoundary();
        RefreshWallTransforms();
    }

    private void OnEnable()
    {
        if (_activeBoundary == null)
        {
            _activeBoundary = this;
        }

        if (autoStart && !NetMgr.IsNetworked)
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
            if (autoStart)
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
            EnforceBoundsIfReady();
            SyncStateToNetwork();
        }

        RefreshWallTransforms();
        UpdateWallVisuals();
        UpdateProximityReveal();
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
        SyncStateToNetwork();
        RefreshWallTransforms();
        UpdateWallVisuals();
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
        UpdateWallVisuals();
    }

    [ContextMenu("Reset Boundary")]
    public void ResetBoundary()
    {
        ValidateConfig();
        _currentWidth = startWidth;
        _currentLength = startLength;
        _waveStartWidth = _currentWidth;
        _waveStartLength = _currentLength;
        _currentWaveIndex = 0;
        _waveTimer = 0f;
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
            Mathf.Clamp(position.y, minY + yMargin, maxY - yMargin),
            Mathf.Clamp(position.z, center.y - halfLength, center.y + halfLength));
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
        _waveTimer += Time.deltaTime;

        if (wave.stationary)
        {
            _isShrinking = false;
        }
        else
        {
            _isShrinking = true;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_waveTimer / duration));
            _currentWidth = Mathf.Lerp(_waveStartWidth, Mathf.Max(MinDimension, wave.targetWidth), progress);
            _currentLength = Mathf.Lerp(_waveStartLength, Mathf.Max(MinDimension, wave.targetLength), progress);
        }

        if (_waveTimer < duration)
        {
            return;
        }

        _currentWidth = wave.stationary ? _currentWidth : Mathf.Max(MinDimension, wave.targetWidth);
        _currentLength = wave.stationary ? _currentLength : Mathf.Max(MinDimension, wave.targetLength);
        _currentWaveIndex++;
        _waveTimer = 0f;
        _waveStartWidth = _currentWidth;
        _waveStartLength = _currentLength;
    }

    private void EnforceBoundsIfReady()
    {
        if (!_active || Time.time < _nextClampTime)
        {
            return;
        }

        _nextClampTime = Time.time + Mathf.Max(0.02f, clampInterval);
        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            EnforceBounds(players[i]);
        }
    }

    private void EnforceBounds(Player3D player)
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

        float radius = Mathf.Min(
            movement != null ? movement.GetCollisionRadius() : ResolveCollisionRadius(player),
            maxClampRadius);
        Vector3 position = player.transform.position;
        Vector3 correctedPosition = ClampPositionInside(position, radius);
        if ((correctedPosition - position).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        Vector3 correctedVelocity = rb != null ? RemoveOutwardVelocity(rb.linearVelocity, position, correctedPosition) : Vector3.zero;
        if (movement != null)
        {
            movement.ApplyBoundaryCorrection(correctedPosition, correctedVelocity);
            return;
        }

        if (rb != null)
        {
            rb.position = correctedPosition;
            rb.linearVelocity = correctedVelocity;
        }

        player.transform.position = correctedPosition;
    }

    private Vector3 RemoveOutwardVelocity(Vector3 velocity, Vector3 originalPosition, Vector3 correctedPosition)
    {
        if (!Mathf.Approximately(originalPosition.x, correctedPosition.x) && Mathf.Sign(velocity.x) == Mathf.Sign(originalPosition.x - correctedPosition.x))
        {
            velocity.x = 0f;
        }

        if (!Mathf.Approximately(originalPosition.y, correctedPosition.y) && Mathf.Sign(velocity.y) == Mathf.Sign(originalPosition.y - correctedPosition.y))
        {
            velocity.y = 0f;
        }

        if (!Mathf.Approximately(originalPosition.z, correctedPosition.z) && Mathf.Sign(velocity.z) == Mathf.Sign(originalPosition.z - correctedPosition.z))
        {
            velocity.z = 0f;
        }

        return velocity;
    }

    private static float ResolveCollisionRadius(Player3D player)
    {
        Collider[] colliders = player.GetComponentsInChildren<Collider>();
        float radius = 0.5f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            radius = Mathf.Max(radius, bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        return radius;
    }

    private void RefreshWallTransforms()
    {
        if (walls == null)
        {
            return;
        }

        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
            {
                walls[i].Configure(center, _currentWidth, _currentLength, minY, maxY, wallThickness);
            }
        }
    }

    private void UpdateWallVisuals()
    {
        if (walls == null)
        {
            return;
        }

        float pulse = _isShrinking
            ? 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, warningFlashRate) * Mathf.PI * 2f)
            : 0f;

        for (int i = 0; i < walls.Length; i++)
        {
            walls[i]?.ApplyVisualState(_active, _isShrinking, pulse);
        }
    }

    private void UpdateProximityReveal()
    {
        if (!_active || walls == null)
        {
            return;
        }

        RefreshLocalViewersIfReady();
        for (int i = 0; i < _localViewers.Count; i++)
        {
            Transform viewer = _localViewers[i];
            if (viewer == null)
            {
                continue;
            }

            for (int wallIndex = 0; wallIndex < walls.Length; wallIndex++)
            {
                walls[wallIndex]?.UpdateProximityReveal(viewer);
            }
        }
    }

    private void RefreshLocalViewersIfReady()
    {
        if (Time.time < _nextLocalViewerRefreshTime)
        {
            return;
        }

        _nextLocalViewerRefreshTime = Time.time + Mathf.Max(0.05f, localViewerRefreshInterval);
        _localViewers.Clear();

        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
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

    private void ApplyNetworkState()
    {
        _active = _netActive.Value;
        _isShrinking = _netShrinking.Value;
        center = _netCenter.Value;
        _currentWidth = Mathf.Max(MinDimension, _netWidth.Value);
        _currentLength = Mathf.Max(MinDimension, _netLength.Value);
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
    }

    private void ValidateConfig()
    {
        startWidth = Mathf.Max(MinDimension, startWidth);
        startLength = Mathf.Max(MinDimension, startLength);
        if (maxY < minY)
        {
            (minY, maxY) = (maxY, minY);
        }

        wallThickness = Mathf.Max(0.01f, wallThickness);
        warningFlashRate = Mathf.Max(0.01f, warningFlashRate);
        clampInterval = Mathf.Max(0.02f, clampInterval);
        inwardSafetyMargin = Mathf.Max(0f, inwardSafetyMargin);
        maxClampRadius = Mathf.Max(0f, maxClampRadius);
        localViewerRefreshInterval = Mathf.Max(0.05f, localViewerRefreshInterval);
    }
}
