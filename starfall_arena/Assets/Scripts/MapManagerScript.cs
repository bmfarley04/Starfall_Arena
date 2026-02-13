using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [Enum and Struct definitions]
public enum AsteroidPattern { Scattered, Clustered, Falling }

[System.Serializable]
public struct AsteroidPrefabConfig
{
    [Tooltip("Primary asteroid variant")]
    public GameObject prefab1;
    [Tooltip("Secondary asteroid variant")]
    public GameObject prefab2;
    [Range(0f, 1f)]
    [Tooltip("Chance to spawn prefab1 (0.6 = 60%)")]
    public float prefab1Chance;
}

[System.Serializable]
public struct AreaConfig
{
    public Vector2 size;
    public Vector2 center;

    public AreaConfig(float width, float height)
    {
        size = new Vector2(width, height);
        center = Vector2.zero;
    }
}

[System.Serializable]
public struct ClusterConfig
{
    public AreaConfig area;
    public int clusterCount;
    public float clusterRadius;
    public float minSpacing;
}

[System.Serializable]
public struct FallingConfig
{
    public AreaConfig area;
    [Tooltip("Extra distance above play area to spawn asteroids")]
    public float spawnMargin;
    [Tooltip("Velocity range (downward)")]
    public Vector2 velocityRange;
    [Tooltip("Horizontal drift range")]
    public Vector2 driftRange;
    [Tooltip("Angular velocity range in degrees/sec")]
    public float maxAngularVelocity;
    public float spawnDelay;
    public bool recycle;
}

[System.Serializable]
public struct AsteroidVariationConfig
{
    [Range(0f, 1f)]
    public float positionJitter;
    public Vector2 scaleMin;
    public Vector2 scaleMax;
    public float rotationRange;
    public bool uniformScale;
}

[System.Serializable]
public struct AnimationConfig
{
    public float growDuration;
    public float shrinkDuration;
}

[System.Serializable]
public struct RingOfFireConfig
{
    public List<Wave> waves;

    [Header("Game Logic")]
    [Tooltip("Auto-start Ring of Fire when map is enabled")]
    public bool autoStart;
    [Tooltip("Automatically chain waves - each wave's endCenterBox is set from previous wave's safeBox")]
    public bool autoChainWaves;

    [Header("Line Renderer Visuals")]
    [Tooltip("Material for the line (Use Particles/Additive or similar)")]
    public Material fireLineMaterial;
    [Tooltip("Width of the fire line")]
    public float lineWidth;

    [Header("Unsafe Area Visuals")]
    [Tooltip("Half-extent (in world units) from the safe zone center to use when drawing outside masks. Increase if your map is larger.")]
    public float maskExtent;
    [Tooltip("Material for the outside safe zone masks (optional, will use a default unlit color if not set)")]
    public Material outsideMaskMaterial;

    public RingOfFireConfig(bool init)
    {
        waves = null;
        autoStart = true;
        autoChainWaves = true;
        fireLineMaterial = null;
        lineWidth = 0.2f;
        maskExtent = 50f;
        outsideMaskMaterial = null;
    }
}

public class MapManagerScript : MonoBehaviour
{
    [Header("Pattern Selection")]
    public AsteroidPattern pattern = AsteroidPattern.Scattered;
    public int asteroidCount = 50;
    public bool useSeed = true;
    public int seed = 12345;

    [Header("Prefabs")]
    public AsteroidPrefabConfig prefabs = new AsteroidPrefabConfig { prefab1Chance = 0.6f };

    [Header("Scattered Pattern")]
    public AreaConfig scattered = new AreaConfig(18f, 10f);

    [Header("Clustered Pattern")]
    public ClusterConfig clustered = new ClusterConfig
    {
        area = new AreaConfig(18f, 10f),
        clusterCount = 5,
        clusterRadius = 2f,
        minSpacing = 3f
    };

    [Header("Falling Pattern")]
    public FallingConfig falling = new FallingConfig
    {
        area = new AreaConfig(18f, 10f),
        spawnMargin = 2f,
        velocityRange = new Vector2(2f, 8f),
        driftRange = new Vector2(-1f, 1f),
        maxAngularVelocity = 90f,
        spawnDelay = 0.1f,
        recycle = true
    };

    [Header("Variation")]
    public AsteroidVariationConfig variation = new AsteroidVariationConfig
    {
        positionJitter = 0.2f,
        scaleMin = Vector2.one * 0.5f,
        scaleMax = Vector2.one * 1.5f,
        rotationRange = 180f,
        uniformScale = true
    };

    [Header("Animation")]
    public AnimationConfig animation = new AnimationConfig { growDuration = 0.5f, shrinkDuration = 0.3f };

    [Header("Ring of Fire")]
    public RingOfFireConfig ringOfFire;

    private Transform asteroidsParent;
    private List<GameObject> fallingAsteroids = new List<GameObject>();
    private System.Random fallingRng;

    // Ring of Fire state
    private bool _ringOfFireActive = false;
    private int _currentWaveIndex = 0;
    private float _waveTimer = 0f;
    private float _lastDamageTickTime;

    // Interpolation vars
    private Vector2 _currentSafeCenter;
    private float _currentSafeWidth;
    private float _currentSafeLength;
    private Vector2 _targetSafeCenter;
    private float _targetSafeWidth;
    private float _targetSafeLength;
    private Vector2 _startSafeCenter;
    private float _startSafeWidth;
    private float _startSafeLength;

    // OPTIMIZATION: Replaced pool with LineRenderer
    private LineRenderer _lineRenderer;
    private GameObject _lineObj;

    // OPTIMIZATION: Cached list of entities to damage
    private List<Entity> _cachedEntities = new List<Entity>();
    private float _lastCacheUpdateTime;
    private const float CACHE_UPDATE_INTERVAL = 1.0f;

    private WaveBox _currentSafeBox;

    // Unsafe-area mask objects (4 quads)
    private GameObject _maskParent;
    private MeshRenderer[] _maskRenderers = new MeshRenderer[4];

    void OnEnable()
    {
        SpawnAsteroids();

        if (ringOfFire.autoStart && ringOfFire.waves != null && ringOfFire.waves.Count > 0)
        {
            StartRingOfFire();
        }
    }

    void Update()
    {
        if (pattern == AsteroidPattern.Falling && falling.recycle)
            RecycleFallingAsteroids();

        if (_ringOfFireActive)
            UpdateRingOfFire();
    }

    [ContextMenu("Spawn Asteroids")]
    public void SpawnAsteroids()
    {
        if (prefabs.prefab1 == null && prefabs.prefab2 == null)
        {
            Debug.LogWarning("No asteroid prefabs assigned.");
            return;
        }

        ClearAsteroids();
        asteroidsParent = new GameObject("Asteroids").transform;
        asteroidsParent.SetParent(transform, false);

        var rng = useSeed ? new System.Random(seed) : new System.Random();

        switch (pattern)
        {
            case AsteroidPattern.Scattered: SpawnScattered(rng); break;
            case AsteroidPattern.Clustered: SpawnClustered(rng); break;
            case AsteroidPattern.Falling:
                fallingRng = rng;
                StartCoroutine(SpawnFallingStaggered(rng));
                break;
        }
    }

    [ContextMenu("Clear Asteroids")]
    public void ClearAsteroids()
    {
        fallingAsteroids.Clear();
        var existing = transform.Find("Asteroids");
        if (existing == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(existing.gameObject);
        else Destroy(existing.gameObject);
#else
        Destroy(existing.gameObject);
#endif
    }

    public void ShrinkAndDestroy()
    {
        if (asteroidsParent != null)
            StartCoroutine(ShrinkAllAsteroids());
    }

    #region Ring of Fire

    [ContextMenu("Start Ring of Fire")]
    public void StartRingOfFire()
    {
        if (ringOfFire.waves == null || ringOfFire.waves.Count == 0)
        {
            Debug.LogWarning("Cannot start Ring of Fire: No waves configured!");
            return;
        }

        // Auto-chain waves if enabled
        if (ringOfFire.autoChainWaves && ringOfFire.waves.Count > 1)
        {
            for (int i = 1; i < ringOfFire.waves.Count; i++)
            {
                Wave previousWave = ringOfFire.waves[i - 1];
                Wave currentWave = ringOfFire.waves[i];
                
                // Set the current wave's safeBox center to match previous wave's safeBox (where it ended)
                currentWave.safeBox = new WaveBox(
                    previousWave.safeBox.centerPoint,
                    currentWave.safeBox.width,
                    currentWave.safeBox.length
                );
                
                // Set the current wave's endCenterBox to match the current wave's safeBox
                // This means the wave will move to end at the current safe zone position
                currentWave.endCenterBox = new WaveBox(
                    currentWave.safeBox.centerPoint,
                    currentWave.safeBox.width,
                    currentWave.safeBox.length
                );
            }
        }

        _currentWaveIndex = 0;
        _waveTimer = 0f;
        _ringOfFireActive = true;
        _lastDamageTickTime = Time.time;
        _lastCacheUpdateTime = -10f; // Force update immediately

        Wave firstWave = ringOfFire.waves[0];
        _currentSafeCenter = firstWave.safeBox.centerPoint;
        _currentSafeWidth = firstWave.safeBox.width;
        _currentSafeLength = firstWave.safeBox.length;

        _currentSafeBox = new WaveBox(_currentSafeCenter, _currentSafeWidth, _currentSafeLength);

        _startSafeCenter = _currentSafeCenter;
        _startSafeWidth = _currentSafeWidth;
        _startSafeLength = _currentSafeLength;

        SetNextTargetSafeZone();
        InitializeLineRenderer();
        InitializeSafeZoneMask();

        Debug.Log($"Ring of Fire started! Wave 1/{ringOfFire.waves.Count}");
    }

    [ContextMenu("Stop Ring of Fire")]
    public void StopRingOfFire()
    {
        _ringOfFireActive = false;
        if (_lineObj != null) _lineObj.SetActive(false);
        if (_maskParent != null) _maskParent.SetActive(false);
        Debug.Log("Ring of Fire stopped!");
    }

    private void UpdateRingOfFire()
    {
        if (_currentWaveIndex >= ringOfFire.waves.Count) return;

        _waveTimer += Time.deltaTime;

        UpdateSafeZoneInterpolation();
        _currentSafeBox = new WaveBox(_currentSafeCenter, _currentSafeWidth, _currentSafeLength);

        // Update the visual line
        UpdateLineRendererVisuals();

        // Get current wave's damage settings
        Wave currentWave = ringOfFire.waves[_currentWaveIndex];
        float tickInterval = currentWave.damageTickInterval > 0 ? currentWave.damageTickInterval : 0.5f;
        
        if (Time.time - _lastDamageTickTime >= tickInterval)
        {
            ApplyFireDamage();
            _lastDamageTickTime = Time.time;
        }

        CheckWaveTransition();
    }

    private void SetNextTargetSafeZone()
    {
        if (_currentWaveIndex >= ringOfFire.waves.Count) return;

        Wave currentWave = ringOfFire.waves[_currentWaveIndex];
        _targetSafeCenter = currentWave.endCenterBox.GetRandomPoint();
        _targetSafeWidth = currentWave.safeBox.width;
        _targetSafeLength = currentWave.safeBox.length;
    }

    private void UpdateSafeZoneInterpolation()
    {
        if (_currentWaveIndex >= ringOfFire.waves.Count) return;

        Wave currentWave = ringOfFire.waves[_currentWaveIndex];
        float progress = Mathf.Clamp01(_waveTimer / currentWave.duration);
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

        _currentSafeCenter = Vector2.Lerp(_startSafeCenter, _targetSafeCenter, smoothProgress);
        _currentSafeWidth = Mathf.Lerp(_startSafeWidth, _targetSafeWidth, smoothProgress);
        _currentSafeLength = Mathf.Lerp(_startSafeLength, _targetSafeLength, smoothProgress);
    }

    private void CheckWaveTransition()
    {
        if (_currentWaveIndex >= ringOfFire.waves.Count) return;

        Wave currentWave = ringOfFire.waves[_currentWaveIndex];

        if (_waveTimer >= currentWave.duration)
        {
            _currentWaveIndex++;
            _waveTimer = 0f;

            if (_currentWaveIndex < ringOfFire.waves.Count)
            {
                _startSafeCenter = _currentSafeCenter;
                _startSafeWidth = _currentSafeWidth;
                _startSafeLength = _currentSafeLength;
                SetNextTargetSafeZone();
            }
        }
    }

    private void InitializeLineRenderer()
    {
        if (_lineObj == null)
        {
            _lineObj = new GameObject("FireRing_Line");
            _lineObj.transform.SetParent(transform, false);
            _lineRenderer = _lineObj.AddComponent<LineRenderer>();
        }

        _lineObj.SetActive(true);

        // Set to world space so we don't have to worry about the MapManager's scale/rotation
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = 4;

        // Use a basic unlit material if none is provided
        if (ringOfFire.fireLineMaterial != null)
        {
            _lineRenderer.material = ringOfFire.fireLineMaterial;
        }
        else
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startWidth = ringOfFire.lineWidth > 0 ? ringOfFire.lineWidth : 0.2f;
        _lineRenderer.endWidth = _lineRenderer.startWidth;

        // Remove complex texture modes
        _lineRenderer.textureMode = LineTextureMode.Stretch;
        _lineRenderer.alignment = LineAlignment.View;
    }

    private void UpdateLineRendererVisuals()
    {
        if (_lineRenderer == null) return;

        float hw = _currentSafeWidth / 2f;
        float hl = _currentSafeLength / 2f;
        float x = _currentSafeCenter.x;
        float y = _currentSafeCenter.y;
        float z = -0.1f; // Slightly in front of the map

        // Simple rectangular boundary
        _lineRenderer.SetPosition(0, new Vector3(x - hw, y + hl, z)); // Top Left
        _lineRenderer.SetPosition(1, new Vector3(x + hw, y + hl, z)); // Top Right
        _lineRenderer.SetPosition(2, new Vector3(x + hw, y - hl, z)); // Bottom Right
        _lineRenderer.SetPosition(3, new Vector3(x - hw, y - hl, z)); // Bottom Left

        // No texture scrolling or complex tiling needed here anymore

        UpdateSafeZoneMasks();
    }
    // -------------------------------------

    private void UpdateSafeZoneMasks()
    {
        if (_maskParent == null || _maskRenderers == null) return;
        if (!_maskParent.activeSelf) _maskParent.SetActive(true);

        // extent around safe center that represents "map bounds" for mask purposes
        float extent = ringOfFire.maskExtent > 0f ? ringOfFire.maskExtent : 50f;

        float hw = _currentSafeWidth / 2f;
        float hl = _currentSafeLength / 2f;
        float x = _currentSafeCenter.x;
        float y = _currentSafeCenter.y;
        float z = 0f;

        float worldLeft = x - extent;
        float worldRight = x + extent;
        float worldTop = y + extent;
        float worldBottom = y - extent;

        // Top mask (covers area above safe rect)
        float topYStart = y + hl;
        float topHeight = Mathf.Max(0f, worldTop - topYStart);
        float topCenterY = topYStart + topHeight * 0.5f;
        float fullWidth = worldRight - worldLeft;

        // Bottom mask (below safe rect)
        float bottomYEnd = y - hl;
        float bottomHeight = Mathf.Max(0f, bottomYEnd - worldBottom);
        float bottomCenterY = worldBottom + bottomHeight * 0.5f;

        // Left mask (left of safe rect within vertical safe band)
        float leftXEnd = x - hw;
        float leftWidth = Mathf.Max(0f, leftXEnd - worldLeft);
        float leftCenterX = worldLeft + leftWidth * 0.5f;

        // Right mask
        float rightXStart = x + hw;
        float rightWidth = Mathf.Max(0f, worldRight - rightXStart);
        float rightCenterX = rightXStart + rightWidth * 0.5f;

        // Ensure renderers exist
        for (int i = 0; i < 4; i++)
        {
            var mr = _maskRenderers[i];
            if (mr == null) continue;
            Transform t = mr.transform;
            switch (i)
            {
                // Top
                case 0:
                    t.position = new Vector3(x, topCenterY, z);
                    t.localScale = new Vector3(fullWidth, topHeight, 1f);
                    break;
                // Bottom
                case 1:
                    t.position = new Vector3(x, bottomCenterY, z);
                    t.localScale = new Vector3(fullWidth, bottomHeight, 1f);
                    break;
                // Left
                case 2:
                    t.position = new Vector3(leftCenterX, y, z);
                    t.localScale = new Vector3(leftWidth, hl * 2f, 1f);
                    break;
                // Right
                case 3:
                    t.position = new Vector3(rightCenterX, y, z);
                    t.localScale = new Vector3(rightWidth, hl * 2f, 1f);
                    break;
            }
        }
    }

    private void InitializeSafeZoneMask()
    {
        if (_maskParent == null)
        {
            _maskParent = new GameObject("FireRing_Mask");
            _maskParent.transform.SetParent(transform, false);

            // Create 4 quads (top, bottom, left, right)
            for (int i = 0; i < 4; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Mask_" + i;
                // remove collider
                var col = q.GetComponent<Collider>();
                if (col != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(col);
                    else
#endif
                        Destroy(col);
                }

                q.transform.SetParent(_maskParent.transform, false);
                var mr = q.GetComponent<MeshRenderer>();
                mr.material = ringOfFire.outsideMaskMaterial;
                // fallback color
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _maskRenderers[i] = mr;
            }
        }

        _maskParent.SetActive(true);
    }

    private void ApplyFireDamage()
    {
        if (_currentWaveIndex >= ringOfFire.waves.Count) return;

        Wave currentWave = ringOfFire.waves[_currentWaveIndex];
        float tickInterval = currentWave.damageTickInterval > 0 ? currentWave.damageTickInterval : 0.5f;
        float damageThisTick = currentWave.fireDamage * tickInterval;

        // Update cache periodically to find new spawned players
        if (Time.time - _lastCacheUpdateTime > CACHE_UPDATE_INTERVAL)
        {
            RefreshEntityCache();
        }

        // Iterate backwards in case entities were destroyed
        for (int i = _cachedEntities.Count - 1; i >= 0; i--)
        {
            Entity entity = _cachedEntities[i];

            // Clean up nulls
            if (entity == null || entity.gameObject == null)
            {
                _cachedEntities.RemoveAt(i);
                continue;
            }

            if (!entity.gameObject.activeInHierarchy) continue;

            if (!IsInsideSafeZone(entity.transform.position))
            {
                entity.TakeDamage(damageThisTick, 0f, entity.transform.position, DamageSource.Other);
            }
        }
    }

    private void RefreshEntityCache()
    {
        _cachedEntities.Clear();
        string[] tagsToFind = new string[] { "Player1", "Player2", "Player" };

        foreach (var tag in tagsToFind)
        {
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(tag); }
            catch { continue; }

            foreach (var obj in found)
            {
                var ent = obj.GetComponent<Entity>();
                if (ent != null) _cachedEntities.Add(ent);
            }
        }
        _lastCacheUpdateTime = Time.time;
    }

    public bool IsInsideSafeZone(Vector3 position)
    {
        if (!_ringOfFireActive) return true;

        float halfWidth = _currentSafeWidth / 2f;
        float halfLength = _currentSafeLength / 2f;

        // Optimization: Pre-calculate bounds
        return position.x >= (_currentSafeCenter.x - halfWidth) &&
               position.x <= (_currentSafeCenter.x + halfWidth) &&
               position.y >= (_currentSafeCenter.y - halfLength) &&
               position.y <= (_currentSafeCenter.y + halfLength);
    }

    public bool IsRingOfFireActive() => _ringOfFireActive;

    public Rect GetCurrentSafeZoneBounds()
    {
        return new Rect(
            _currentSafeCenter.x - _currentSafeWidth / 2f,
            _currentSafeCenter.y - _currentSafeLength / 2f,
            _currentSafeWidth,
            _currentSafeLength
        );
    }

    public int GetCurrentWaveIndex() => _currentWaveIndex;

    public float GetCurrentWaveProgress()
    {
        if (!_ringOfFireActive || _currentWaveIndex >= ringOfFire.waves.Count) return 1f;

        Wave currentWave = ringOfFire.waves[_currentWaveIndex];
        return Mathf.Clamp01(_waveTimer / currentWave.duration);
    }
    #endregion

    #region Spawn Patterns
    private void SpawnScattered(System.Random rng)
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            Vector3 pos = RandomPointInArea(rng, scattered);
            CreateAsteroid(pos, rng);
        }
    }

    private void SpawnClustered(System.Random rng)
    {
        var centers = GenerateClusterCenters(rng);
        if (centers.Count == 0) return;

        int perCluster = asteroidCount / centers.Count;
        int remainder = asteroidCount % centers.Count;

        for (int c = 0; c < centers.Count; c++)
        {
            int count = perCluster + (c < remainder ? 1 : 0);
            Vector2 center = centers[c];

            for (int i = 0; i < count; i++)
            {
                float angle = RandomRange(rng, 0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt((float)rng.NextDouble()) * clustered.clusterRadius;
                Vector3 pos = new Vector3(
                    center.x + Mathf.Cos(angle) * dist,
                    center.y + Mathf.Sin(angle) * dist, 0f);
                pos += GetJitter(rng, variation.positionJitter * clustered.clusterRadius * 0.5f);
                CreateAsteroid(pos, rng);
            }
        }
    }

    private List<Vector2> GenerateClusterCenters(System.Random rng)
    {
        var centers = new List<Vector2>();
        const int maxAttempts = 100;

        for (int i = 0; i < clustered.clusterCount; i++)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 candidate = RandomPointInArea(rng, clustered.area);
                if (IsValidClusterPosition(candidate, centers))
                {
                    centers.Add(candidate);
                    break;
                }
            }
        }
        return centers;
    }

    private bool IsValidClusterPosition(Vector2 candidate, List<Vector2> existing)
    {
        foreach (var center in existing)
            if (Vector2.Distance(candidate, center) < clustered.minSpacing)
                return false;
        return true;
    }

    private IEnumerator SpawnFallingStaggered(System.Random rng)
    {
        var wait = falling.spawnDelay > 0f ? new WaitForSeconds(falling.spawnDelay) : null;
        for (int i = 0; i < asteroidCount; i++)
        {
            SpawnFallingAsteroid(rng);
            if (wait != null && i < asteroidCount - 1)
                yield return wait;
        }
    }

    private void SpawnFallingAsteroid(System.Random rng)
    {
        float spawnY = falling.area.center.y + falling.area.size.y * 0.5f + falling.spawnMargin;
        float x = RandomRange(rng, -0.5f, 0.5f) * falling.area.size.x + falling.area.center.x;

        GameObject asteroid = CreateAsteroid(new Vector3(x, spawnY, 0f), rng, returnObject: true);
        if (asteroid == null) return;

        SetupFallingRigidbody(asteroid, rng);
        fallingAsteroids.Add(asteroid);
    }

    private void SetupFallingRigidbody(GameObject asteroid, System.Random rng)
    {
        Rigidbody2D rb = asteroid.GetComponent<Rigidbody2D>() ?? asteroid.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ApplyFallingVelocity(rb, rng);
    }

    private void ApplyFallingVelocity(Rigidbody2D rb, System.Random rng)
    {
        float vy = -RandomRange(rng, falling.velocityRange.x, falling.velocityRange.y);
        float vx = RandomRange(rng, falling.driftRange.x, falling.driftRange.y);
        rb.linearVelocity = new Vector2(vx, vy);
        rb.angularVelocity = RandomRange(rng, -falling.maxAngularVelocity, falling.maxAngularVelocity);
    }

    private void RecycleFallingAsteroids()
    {
        if (fallingAsteroids.Count == 0 || fallingRng == null) return;

        float halfHeight = falling.area.size.y * 0.5f;
        float despawnY = falling.area.center.y - halfHeight - falling.spawnMargin;
        float spawnY = falling.area.center.y + halfHeight + falling.spawnMargin;

        for (int i = fallingAsteroids.Count - 1; i >= 0; i--)
        {
            var asteroid = fallingAsteroids[i];
            if (asteroid == null)
            {
                fallingAsteroids.RemoveAt(i);
                continue;
            }

            if (asteroid.transform.position.y < despawnY)
            {
                float x = RandomRange(fallingRng, -0.5f, 0.5f) * falling.area.size.x + falling.area.center.x;
                asteroid.transform.position = new Vector3(x, spawnY, asteroid.transform.position.z);

                var rb = asteroid.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    ApplyFallingVelocity(rb, fallingRng);
                }
            }
        }
    }
    #endregion

    #region Asteroid Creation
    private GameObject CreateAsteroid(Vector3 position, System.Random rng, bool returnObject = false)
    {
        GameObject prefab = SelectPrefab(rng);
        if (prefab == null) return null;

        var asteroid = Instantiate(prefab, asteroidsParent);
        asteroid.transform.position = new Vector3(position.x, position.y, prefab.transform.position.z);
        asteroid.transform.rotation = Quaternion.Euler(0f, 0f, RandomRange(rng, -variation.rotationRange, variation.rotationRange));
        asteroid.transform.localScale = Vector3.zero;

        StartCoroutine(GrowAsteroid(asteroid.transform, CalculateScale(prefab, rng)));
        return returnObject ? asteroid : null;
    }

    private GameObject SelectPrefab(System.Random rng)
    {
        if (prefabs.prefab1 != null && prefabs.prefab2 != null)
            return rng.NextDouble() < prefabs.prefab1Chance ? prefabs.prefab1 : prefabs.prefab2;
        return prefabs.prefab1 ?? prefabs.prefab2;
    }

    private Vector3 CalculateScale(GameObject prefab, System.Random rng)
    {
        Vector3 baseScale = prefab.transform.localScale;
        float t = (float)rng.NextDouble();

        if (variation.uniformScale)
        {
            float multiplier = Mathf.Lerp(
                (variation.scaleMin.x + variation.scaleMin.y) * 0.5f,
                (variation.scaleMax.x + variation.scaleMax.y) * 0.5f, t);
            return baseScale * multiplier;
        }

        return new Vector3(
            baseScale.x * Mathf.Lerp(variation.scaleMin.x, variation.scaleMax.x, t),
            baseScale.y * Mathf.Lerp(variation.scaleMin.y, variation.scaleMax.y, t),
            baseScale.z);
    }
    #endregion

    #region Helpers
    private static float RandomRange(System.Random rng, float min, float max) =>
        (float)(rng.NextDouble() * (max - min) + min);

    private static Vector3 RandomPointInArea(System.Random rng, AreaConfig area) =>
        new Vector3(
            RandomRange(rng, -0.5f, 0.5f) * area.size.x + area.center.x,
            RandomRange(rng, -0.5f, 0.5f) * area.size.y + area.center.y, 0f);

    private Vector3 GetJitter(System.Random rng, float magnitude)
    {
        if (magnitude <= 0f) return Vector3.zero;
        return new Vector3(
            RandomRange(rng, -magnitude, magnitude),
            RandomRange(rng, -magnitude, magnitude), 0f);
    }
    #endregion

    #region Animations
    private IEnumerator GrowAsteroid(Transform asteroid, Vector3 targetScale)
    {
        for (float t = 0f; t < animation.growDuration; t += Time.deltaTime)
        {
            if (asteroid == null) yield break;
            float eased = 1f - Mathf.Pow(1f - t / animation.growDuration, 2f);
            asteroid.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased);
            yield return null;
        }
        if (asteroid != null) asteroid.localScale = targetScale;
    }

    private IEnumerator ShrinkAllAsteroids()
    {
        if (asteroidsParent == null) yield break;

        var asteroids = new List<(Transform t, Vector3 scale)>();
        foreach (Transform child in asteroidsParent)
            asteroids.Add((child, child.localScale));

        for (float t = 0f; t < animation.shrinkDuration; t += Time.deltaTime)
        {
            float eased = Mathf.Pow(t / animation.shrinkDuration, 2f);
            foreach (var (asteroid, scale) in asteroids)
                if (asteroid != null) asteroid.localScale = Vector3.Lerp(scale, Vector3.zero, eased);
            yield return null;
        }

        Destroy(asteroidsParent.gameObject);
        asteroidsParent = null;
    }
    #endregion
}