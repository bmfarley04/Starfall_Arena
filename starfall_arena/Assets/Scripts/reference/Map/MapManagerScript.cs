using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

#region Nested Types
[System.Serializable]
public class EnemySpawnConfig
{
    public GameObject enemyPrefab; // Reference to enemyTypeOne, Two, or Three
    public Vector2 spawnOffset = Vector2.zero; // Offset from center position
}

[System.Serializable]
public class WaveConfig
{
    public List<EnemySpawnConfig> enemies = new List<EnemySpawnConfig>();
    public Vector2 waveBasePosition = Vector2.zero; // Base position for this wave
}

public enum AsteroidPattern
{
    Field,
    Belt,
    Ring,
    Grid,
    Cluster
}

[System.Serializable]
public class AsteroidPatternConfig
{
    [Tooltip("The pattern type to spawn")]
    public AsteroidPattern pattern = AsteroidPattern.Field;
    [Tooltip("Number of asteroids to spawn for this pattern")]
    public int count = 100;
}
#endregion

public class MapManagerScript : MonoBehaviour
{
    #region Serialized Fields - UI
    [Header("UI References")]
    public TextMeshProUGUI waveText;

    [Header("Wave Text Settings")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private Color waveTextColor = Color.white;
    [SerializeField] private float waveTextGlowIntensity = 2f;
    [SerializeField] private float waveTextFontSize = 80f;
    #endregion

    #region Serialized Fields - Prefabs
    [Header("Prefabs")]
    public GameObject endBarrierPrefab;

    [Header("Asteroid Prefabs")]
    [Tooltip("First asteroid variant (default 60% spawn rate)")]
    public GameObject asteroidPrefab1;
    [Tooltip("Second asteroid variant (default 40% spawn rate)")]
    public GameObject asteroidPrefab2;
    [Range(0f, 1f)]
    [Tooltip("Percentage chance to spawn asteroidPrefab1 (e.g., 0.6 = 60%)")]
    public float asteroidPrefab1SpawnChance = 0.6f;

    public GameObject enemyTypeOne;
    #endregion

    #region Serialized Fields - Player
    [Header("Player Reference")]
    public GameObject player; // Reference to the player object

    [Header("Spawn Safety")]
    [Tooltip("Minimum distance from player that objects can spawn. Objects closer will be repositioned.")]
    public float minSpawnDistanceFromPlayer = 3f;
    [Tooltip("Minimum distance between spawned objects (asteroids and enemies).")]
    public float minSpawnDistanceBetweenObjects = 1.5f;
    [Tooltip("Maximum attempts to find a valid spawn position before skipping.")]
    public int maxSpawnAttempts = 15; // Increased slightly to account for tighter checks
    #endregion

    #region Serialized Fields - Scene Manager
    [Header("Scene Manager")]
    public SceneManagerScript sceneManager;
    #endregion

    #region Serialized Fields - Background
    [Header("Background")]
    [Tooltip("CloudSpawner reference for menace gradient shifting on enemy kills")]
    public CloudSpawner cloudSpawner;
    #endregion

    #region Serialized Fields - Map Boundaries
    [Header("Box Dimensions (world units)")]
    public float boxWidth = 20f;
    public float boxHeight = 10f;
    public float wallThickness = 1f;
    public Vector2 center = Vector2.zero;
    #endregion

    #region Serialized Fields - Asteroid Spawning
    [Header("Asteroid Spawning")]
    public bool autoSpawnAsteroids = true;

    [Tooltip("List of asteroid patterns to spawn. If empty, falls back to legacy single pattern.")]
    public List<AsteroidPatternConfig> asteroidPatterns = new List<AsteroidPatternConfig>();

    [Header("Legacy Single Pattern (Use asteroidPatterns list instead)")]
    public AsteroidPattern asteroidPattern = AsteroidPattern.Field;
    public int asteroidCount = 100;

    [Header("Random Seed")]
    public bool useSeed = true;
    public int seed = 12345;

    // Field specific
    public Vector2 fieldSize = new Vector2(18f, 8f);

    // Belt / Ring specific
    public float beltInnerRadius = 6f;
    public float beltThickness = 2f; // radial thickness from inner radius
    [Tooltip("Center position for Belt pattern. Defaults to map center if (0,0).")]
    public Vector2 beltCenter = Vector2.zero;

    public float ringRadius = 8f;
    [Tooltip("Center position for Ring pattern. Defaults to map center if (0,0).")]
    public Vector2 ringCenter = Vector2.zero;

    // Grid specific
    public Vector2 gridCellSize = new Vector2(1.5f, 1.5f);

    // Cluster specific
    public int clusters = 5;
    public float clusterRadius = 2f;

    // Variation
    [Range(0f, 1f)] public float positionJitter = 0.2f; // fraction of cell or radius for jitter
    [Tooltip("Minimum distance between spawned asteroids. Set to 0 to disable spacing check.")]
    public float minAsteroidSpacing = 0f;
    public Vector2 asteroidScaleMin = Vector2.one * 0.5f;
    public Vector2 asteroidScaleMax = Vector2.one * 1.5f;
    public float rotationJitterDegrees = 180f;

    [Header("Rendering / Shape")]
    [Tooltip("When true, asteroids are forced to be circular by using a single uniform scale value.")]
    public bool forceCircularAsteroids = true;

    [Header("Asteroid Animation")]
    [Tooltip("Duration for asteroids to grow from zero to full size on spawn.")]
    public float asteroidGrowDuration = 0.5f;
    [Tooltip("Duration for asteroids to shrink to zero before cleanup.")]
    public float asteroidShrinkDuration = 0.3f;
    #endregion

    #region Serialized Fields - Enemy Wave Spawning
    [Header("Enemy Wave Spawning")]
    public bool autoStartWaves = false;
    [Tooltip("Delay before the first wave spawns after StartEnemyWaves is called.")]
    public float firstWaveDelay = 0f;
    public float timeBetweenWaves = 10f;
    public int totalWaves = 3;
    public List<WaveConfig> waveConfigurations = new List<WaveConfig>();

    // Default spawn settings (used if no wave config is provided)
    public float defaultEnemySpacing = 2f;
    public Vector2 defaultSpawnOffset = new Vector2(0f, 4f); // 40% of box height up from center

    [Header("Enemy Cleanup")]
    [Tooltip("Distance outside the map bounds at which enemies are automatically destroyed. Set to 0 to disable.")]
    public float enemyOutOfBoundsDistance = 50f;
    [Tooltip("How often to check for out-of-bounds enemies (in seconds)")]
    public float outOfBoundsCheckInterval = 1f;

    #endregion

    #region Serialized Fields - Debug
    [Header("Debug")]
    public bool showDebugLogs = true;
    #endregion

    #region Private Fields
    private int currentWave = 0;
    private bool isSpawningWaves = false;
    private float waveTimer = 0f;
    private int activeEnemyCount = 0;
    private Transform waveParent;
    private float outOfBoundsCheckTimer = 0f;
    #endregion

    #region Unity Lifecycle Methods
    void Awake()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("Player reference not set and could not be found with Player tag!");
            }
        }
    }

    void OnEnable()
    {
        // Reset state for reuse
        currentWave = 0;
        isSpawningWaves = false;
        waveTimer = 0f;
        activeEnemyCount = 0;
        outOfBoundsCheckTimer = 0f;

        SetupWaveText();
        BuildBox();

        if (autoSpawnAsteroids)
            SpawnAsteroids();

        if (autoStartWaves)
            StartEnemyWaves();

        // Show wave text when map becomes active
        StartCoroutine(ShowWaveText());
    }

    void Update()
    {
        if (isSpawningWaves && currentWave <= totalWaves)
        {
            waveTimer += Time.deltaTime;

            if (waveTimer >= timeBetweenWaves)
            {
                waveTimer = 0f;
                SpawnWave();
            }
        }

        // Check for out-of-bounds enemies periodically
        if (enemyOutOfBoundsDistance > 0f && activeEnemyCount > 0)
        {
            outOfBoundsCheckTimer += Time.deltaTime;
            if (outOfBoundsCheckTimer >= outOfBoundsCheckInterval)
            {
                outOfBoundsCheckTimer = 0f;
                CheckAndDestroyOutOfBoundsEnemies();
            }
        }
    }
    #endregion

    #region Public API - Context Menu Methods
    [ContextMenu("Start Enemy Waves")]
    public void StartEnemyWaves()
    {
        if (enemyTypeOne == null)
        {
            Debug.LogWarning("enemyTypeOne prefab not assigned. Cannot spawn enemies.");
            return;
        }

        currentWave = 0;
        isSpawningWaves = true;

        // If no first wave delay, spawn immediately; otherwise let Update handle it
        if (firstWaveDelay <= 0f)
        {
            waveTimer = 0f;
            SpawnWave();
        }
        else
        {
            // Set timer so first wave spawns after firstWaveDelay
            waveTimer = timeBetweenWaves - firstWaveDelay;
        }
    }

    [ContextMenu("Rebuild Box")]
    public void BuildBox()
    {
        if (endBarrierPrefab == null)
        {
            Debug.LogWarning("endBarrierPrefab is not assigned. Cannot build map box.");
            return;
        }

        // Remove existing MapBounds child if present
        var existing = transform.Find("MapBounds");
        if (existing != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }

        // Parent object for organization
        var parent = new GameObject("MapBounds");
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = Vector3.zero;

        // Calculate positions
        float halfW = boxWidth * 0.5f;
        float halfH = boxHeight * 0.5f;

        // Bottom
        CreateWall(parent.transform, "BottomBarrier",
            new Vector3(center.x, center.y - halfH, 0f),
            new Vector3(boxWidth, wallThickness, 1f));

        // Top
        CreateWall(parent.transform, "TopBarrier",
            new Vector3(center.x, center.y + halfH, 0f),
            new Vector3(boxWidth, wallThickness, 1f));

        // Left
        CreateWall(parent.transform, "LeftBarrier",
            new Vector3(center.x - halfW, center.y, 0f),
            new Vector3(wallThickness, boxHeight, 1f));

        // Right
        CreateWall(parent.transform, "RightBarrier",
            new Vector3(center.x + halfW, center.y, 0f),
            new Vector3(wallThickness, boxHeight, 1f));
    }

    [ContextMenu("Spawn Asteroids")]
    public void SpawnAsteroids()
    {
        if (asteroidPrefab1 == null && asteroidPrefab2 == null)
        {
            Debug.LogWarning("No asteroid prefabs assigned. Aborting asteroid spawn.");
            return;
        }

        if (asteroidPrefab1 == null)
        {
            Debug.LogWarning("asteroidPrefab1 is not assigned. Using only asteroidPrefab2.");
        }

        if (asteroidPrefab2 == null)
        {
            Debug.LogWarning("asteroidPrefab2 is not assigned. Using only asteroidPrefab1.");
        }

        // Remove existing Asteroids child if present
        var existing = transform.Find("Asteroids");
        if (existing != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }

        var parent = new GameObject("Asteroids");
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = Vector3.zero;

        var rng = useSeed ? new System.Random(seed) : new System.Random(Guid.NewGuid().GetHashCode());

        // Keep track of positions generated in this batch to prevent overlap
        List<Vector3> placedPositions = new List<Vector3>();

        // Check if using new multi-pattern system or legacy single pattern
        if (asteroidPatterns != null && asteroidPatterns.Count > 0)
        {
            // Spawn multiple patterns
            foreach (var patternConfig in asteroidPatterns)
            {
                if (patternConfig.count <= 0)
                {
                    if (showDebugLogs)
                    {
                        Debug.Log($"Skipping pattern {patternConfig.pattern} with count {patternConfig.count}");
                    }
                    continue;
                }

                SpawnPatternByType(parent.transform, rng, placedPositions, patternConfig.pattern, patternConfig.count);
            }
        }
        else
        {
            // Fall back to legacy single pattern
            if (showDebugLogs)
            {
                Debug.Log("Using legacy single pattern mode");
            }
            SpawnPatternByType(parent.transform, rng, placedPositions, asteroidPattern, asteroidCount);
        }
    }

    private void SpawnPatternByType(Transform parent, System.Random rng, List<Vector3> placedPositions, AsteroidPattern pattern, int count)
    {
        // Temporarily store the count for pattern methods that use asteroidCount field
        int originalCount = asteroidCount;
        asteroidCount = count;

        switch (pattern)
        {
            case AsteroidPattern.Field:
                SpawnField(parent, rng, placedPositions);
                break;
            case AsteroidPattern.Belt:
                SpawnBelt(parent, rng, placedPositions);
                break;
            case AsteroidPattern.Ring:
                SpawnRing(parent, rng, placedPositions);
                break;
            case AsteroidPattern.Grid:
                SpawnGrid(parent, rng, placedPositions);
                break;
            case AsteroidPattern.Cluster:
                SpawnClusters(parent, rng, placedPositions);
                break;
            default:
                SpawnField(parent, rng, placedPositions);
                break;
        }

        // Restore original count
        asteroidCount = originalCount;
    }
    #endregion

    #region Event Handlers
    public void OnEnemyDestroyed(Vector3 position)
    {
        activeEnemyCount--;

        // Report to SceneManager for total tracking
        if (sceneManager != null)
        {
            sceneManager.OnEnemyDefeated();
        }

        // Increment menace gradient on background clouds
        if (cloudSpawner != null)
        {
            cloudSpawner.IncrementMenace();
        }

        if (showDebugLogs)
        {
            Debug.Log($"Enemy destroyed at {position}. Remaining enemies: {activeEnemyCount}. Spawning waves: {isSpawningWaves}");
        }

        // Check if this was the last enemy and we've finished spawning all waves
        if (activeEnemyCount <= 0 && currentWave >= totalWaves)
        {
            if (showDebugLogs)
            {
                Debug.Log("All enemies destroyed and waves complete. Cleaning up and activating next map.");
            }
            CleanupAndActivateNextMap();
        }
    }

    /// <summary>
    /// Called when a boss is spawned and needs to be tracked by this map manager.
    /// </summary>
    public void OnBossSpawned()
    {
        activeEnemyCount++;

        if (showDebugLogs)
        {
            Debug.Log($"Boss spawned! Active enemy count: {activeEnemyCount}");
        }
    }

    /// <summary>
    /// Called when the boss is destroyed. Reports to SceneManager for victory.
    /// </summary>
    public void OnBossDestroyed(Vector3 position)
    {
        activeEnemyCount--;

        // Report to SceneManager for total tracking
        if (sceneManager != null)
        {
            sceneManager.OnEnemyDefeated();
            sceneManager.OnBossDefeated();
        }

        if (showDebugLogs)
        {
            Debug.Log($"Boss destroyed at {position}!");
        }
    }
    #endregion

    #region Wave Management - Private Methods
    private void CheckAndDestroyOutOfBoundsEnemies()
    {
        var enemyWavesParent = transform.Find("EnemyWaves");
        if (enemyWavesParent == null) return;

        float halfW = boxWidth * 0.5f;
        float halfH = boxHeight * 0.5f;

        // Calculate the extended bounds (map bounds + out of bounds distance)
        float minX = center.x - halfW - enemyOutOfBoundsDistance;
        float maxX = center.x + halfW + enemyOutOfBoundsDistance;
        float minY = center.y - halfH - enemyOutOfBoundsDistance;
        float maxY = center.y + halfH + enemyOutOfBoundsDistance;

        List<GameObject> enemiesToDestroy = new List<GameObject>();

        // Iterate through all wave containers
        foreach (Transform waveContainer in enemyWavesParent)
        {
            foreach (Transform enemy in waveContainer)
            {
                if (enemy == null) continue;

                Vector3 pos = enemy.position;
                if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY)
                {
                    enemiesToDestroy.Add(enemy.gameObject);
                }
            }
        }

        // Destroy out-of-bounds enemies
        foreach (var enemy in enemiesToDestroy)
        {
            if (showDebugLogs)
            {
                Debug.Log($"Destroying out-of-bounds enemy: {enemy.name} at {enemy.transform.position}");
            }

            // Manually call OnEnemyDestroyed since we're bypassing the normal destruction
            OnEnemyDestroyed(enemy.transform.position);

            // Remove the tracker to prevent double-counting
            var tracker = enemy.GetComponent<EnemyDestructionTracker>();
            if (tracker != null)
            {
                Destroy(tracker);
            }

            Destroy(enemy);
        }
    }

    private void SpawnWave()
    {
        if (currentWave >= totalWaves)
        {
            isSpawningWaves = false;
            if (showDebugLogs)
            {
                Debug.Log("All waves completed.");
            }
            return;
        }

        // Create/get parent object for organization
        waveParent = transform.Find("EnemyWaves");
        if (waveParent == null)
        {
            waveParent = new GameObject("EnemyWaves").transform;
            waveParent.SetParent(transform, false);
            waveParent.localPosition = Vector3.zero;
        }

        // Create wave container
        var wave = new GameObject($"Wave_{currentWave + 1}").transform;
        wave.SetParent(waveParent, false);
        wave.localPosition = Vector3.zero;

        if (currentWave < waveConfigurations.Count && waveConfigurations[currentWave].enemies.Count > 0)
        {
            SpawnConfiguredWave(wave, waveConfigurations[currentWave]);
        }
        else
        {
            SpawnDefaultWave(wave);
        }

        currentWave++;

        if (showDebugLogs)
        {
            Debug.Log($"Wave {currentWave}/{totalWaves} spawned. Active enemies: {activeEnemyCount}");
        }
    }

    private void SpawnConfiguredWave(Transform waveTransform, WaveConfig config)
    {
        Vector2 basePos = center + config.waveBasePosition;
        List<Vector3> enemiesInThisWave = new List<Vector3>();

        for (int i = 0; i < config.enemies.Count; i++)
        {
            var spawnConfig = config.enemies[i];
            if (spawnConfig.enemyPrefab == null)
            {
                Debug.LogWarning($"Enemy prefab not assigned for enemy {i} in wave {currentWave + 1}");
                continue;
            }

            Vector3 position = new Vector3(
                basePos.x + spawnConfig.spawnOffset.x,
                basePos.y + spawnConfig.spawnOffset.y,
                0f
            );

            // Ensure enemy doesn't spawn on top of player, other enemies, or asteroids
            position = GetSafeEnemySpawnPosition(position, enemiesInThisWave);

            var enemy = Instantiate(spawnConfig.enemyPrefab, position, Quaternion.identity, waveTransform);
            enemy.name = $"Enemy_{currentWave + 1}_{i + 1}";

            // Track this position for the next iterations in the loop
            enemiesInThisWave.Add(enemy.transform.position);

            var tracker = enemy.AddComponent<EnemyDestructionTracker>();
            tracker.mapManager = this;

            activeEnemyCount++;
        }
    }

    private void SpawnDefaultWave(Transform waveTransform)
    {
        if (enemyTypeOne == null)
        {
            Debug.LogWarning($"Default enemy prefab (enemyTypeOne) not assigned for wave {currentWave + 1}");
            return;
        }

        int defaultEnemyCount = 3; // Default number of enemies if no configuration
        float totalWidth = (defaultEnemyCount - 1) * defaultEnemySpacing;
        float startX = center.x - totalWidth * 0.5f;
        float spawnY = center.y + defaultSpawnOffset.y;

        List<Vector3> enemiesInThisWave = new List<Vector3>();

        for (int i = 0; i < defaultEnemyCount; i++)
        {
            Vector3 position = new Vector3(
                startX + (i * defaultEnemySpacing),
                spawnY,
                0f
            );

            // Ensure enemy doesn't spawn on top of player, other enemies, or asteroids
            position = GetSafeEnemySpawnPosition(position, enemiesInThisWave);

            var enemy = Instantiate(enemyTypeOne, position, Quaternion.identity, waveTransform);
            enemy.name = $"Enemy_{currentWave + 1}_{i + 1}";

            // Track for next loop
            enemiesInThisWave.Add(enemy.transform.position);

            var tracker = enemy.AddComponent<EnemyDestructionTracker>();
            tracker.mapManager = this;

            activeEnemyCount++;
        }
    }

    private void CleanupAndActivateNextMap()
    {
        StartCoroutine(CleanupAndActivateNextMapCoroutine());
    }

    private IEnumerator CleanupAndActivateNextMapCoroutine()
    {
        // Shrink and destroy asteroids
        var asteroids = transform.Find("Asteroids");
        if (asteroids != null)
        {
            yield return StartCoroutine(ShrinkAsteroids(asteroids));
            Destroy(asteroids.gameObject);
            if (showDebugLogs)
            {
                Debug.Log("Asteroids destroyed.");
            }
        }

        // Destroy map boundaries
        var mapBounds = transform.Find("MapBounds");
        if (mapBounds != null)
        {
            Destroy(mapBounds.gameObject);
            if (showDebugLogs)
            {
                Debug.Log("Map boundaries destroyed.");
            }
        }

        // Destroy enemy waves container
        var enemyWaves = transform.Find("EnemyWaves");
        if (enemyWaves != null)
        {
            Destroy(enemyWaves.gameObject);
            if (showDebugLogs)
            {
                Debug.Log("Enemy waves container destroyed.");
            }
        }

        // Activate next map through SceneManager
        if (sceneManager != null)
        {
            sceneManager.ActivateNextMap();
        }
        else
        {
            Debug.LogWarning("SceneManager reference is null. Cannot activate next map.");
        }
    }

    private IEnumerator ShrinkAsteroids(Transform asteroidsParent)
    {
        // Collect all asteroid transforms and their current scales
        List<Transform> asteroidTransforms = new List<Transform>();
        List<Vector3> originalScales = new List<Vector3>();

        foreach (Transform child in asteroidsParent)
        {
            asteroidTransforms.Add(child);
            originalScales.Add(child.localScale);
        }

        // Animate all asteroids shrinking simultaneously
        float elapsed = 0f;
        while (elapsed < asteroidShrinkDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / asteroidShrinkDuration);
            // Use ease-in curve for shrinking
            float easedProgress = Mathf.Pow(progress, 2f);

            for (int i = 0; i < asteroidTransforms.Count; i++)
            {
                if (asteroidTransforms[i] != null)
                {
                    asteroidTransforms[i].localScale = Vector3.Lerp(originalScales[i], Vector3.zero, easedProgress);
                }
            }
            yield return null;
        }

        // Ensure all are at zero scale
        foreach (var asteroid in asteroidTransforms)
        {
            if (asteroid != null)
            {
                asteroid.localScale = Vector3.zero;
            }
        }
    }
    #endregion

    #region Spawn Safety - Private Methods
    private bool IsPositionSafe(Vector3 position, List<Vector3> otherObjectsToCheck)
    {
        // Check Player
        if (player != null)
        {
            float distToPlayer = Vector3.Distance(position, player.transform.position);
            if (distToPlayer < minSpawnDistanceFromPlayer)
            {
                if (showDebugLogs) Debug.Log($"Spawn Unsafe: Too close to Player. Dist: {distToPlayer:F2} < Min: {minSpawnDistanceFromPlayer}");
                return false;
            }
        }

        // Check against provided list of objects (e.g., other asteroids in this batch, or other enemies)
        if (otherObjectsToCheck != null)
        {
            foreach (Vector3 otherPos in otherObjectsToCheck)
            {
                float dist = Vector3.Distance(position, otherPos);
                if (dist < minSpawnDistanceBetweenObjects)
                {
                    if (showDebugLogs) Debug.Log($"Spawn Unsafe: Too close to Peer object. Dist: {dist:F2} < Min: {minSpawnDistanceBetweenObjects}");
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsAsteroidPositionValid(Vector3 position, List<Vector3> placedAsteroids)
    {
        // Check player distance
        if (player != null)
        {
            float distToPlayer = Vector3.Distance(position, player.transform.position);
            if (distToPlayer < minSpawnDistanceFromPlayer)
            {
                return false;
            }
        }

        // Check asteroid spacing (if enabled)
        if (minAsteroidSpacing > 0f && placedAsteroids != null)
        {
            foreach (Vector3 otherPos in placedAsteroids)
            {
                float dist = Vector3.Distance(position, otherPos);
                if (dist < minAsteroidSpacing)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Vector3 GetSafeSpawnPosition(Vector3 originalPosition, System.Random rng, float areaWidth, float areaHeight, List<Vector3> placedPositions)
    {
        if (IsAsteroidPositionValid(originalPosition, placedPositions))
        {
            return originalPosition;
        }

        if (showDebugLogs) Debug.Log($"Original spawn pos {originalPosition} unsafe. Attempting to find safe spot...");

        // Try to find a safe position
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            float rx = (float)(rng.NextDouble() * areaWidth) - areaWidth * 0.5f;
            float ry = (float)(rng.NextDouble() * areaHeight) - areaHeight * 0.5f;
            Vector3 newPos = new Vector3(center.x + rx, center.y + ry, 0f);

            if (IsAsteroidPositionValid(newPos, placedPositions))
            {
                if (showDebugLogs) Debug.Log($"Safe spot found at attempt {attempt}: {newPos}");
                return newPos;
            }
        }

        // If no safe position found, just return original (or could implement a push logic here)
        // But for asteroids, random overlapping is better than stacking on the player.
        // We will do a basic Player push check at least.
        if (player != null)
        {
            float dist = Vector3.Distance(originalPosition, player.transform.position);
            if (dist < minSpawnDistanceFromPlayer)
            {
                Vector3 directionFromPlayer = (originalPosition - player.transform.position).normalized;
                if (directionFromPlayer == Vector3.zero) directionFromPlayer = Vector3.up;
                Vector3 pushedPos = player.transform.position + directionFromPlayer * minSpawnDistanceFromPlayer;

                if (showDebugLogs) Debug.Log($"Max attempts reached. Pushing away from Player to {pushedPos}");
                return pushedPos;
            }
        }

        if (showDebugLogs) Debug.Log("Max attempts reached. Using original position (may be overlapping other objects).");
        return originalPosition;
    }

    private Vector3 GetSafeEnemySpawnPosition(Vector3 originalPosition, List<Vector3> currentWaveEnemies)
    {
        // First check: Player distance & Current Wave Peers
        bool isSafePeers = IsPositionSafe(originalPosition, currentWaveEnemies);
        bool isClearAsteroids = IsPositionClearOfAsteroids(originalPosition);

        if (isSafePeers && isClearAsteroids)
        {
            return originalPosition;
        }

        if (showDebugLogs) Debug.Log($"Enemy spawn {originalPosition} unsafe. PeersSafe: {isSafePeers}, AsteroidsClear: {isClearAsteroids}. Retrying...");

        // Attempt to find a safe spot nearby
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // Jitter the position slightly to find a free spot
            Vector3 randomOffset = UnityEngine.Random.insideUnitCircle * minSpawnDistanceBetweenObjects * 2f;
            Vector3 candidatePos = originalPosition + randomOffset;

            if (IsPositionSafe(candidatePos, currentWaveEnemies) && IsPositionClearOfAsteroids(candidatePos))
            {
                if (showDebugLogs) Debug.Log($"Found safe Enemy spot at attempt {i}");
                return candidatePos;
            }
        }

        // Fallback: Push enemy away from player to safe distance if that was the main issue
        if (player != null)
        {
            Vector3 directionFromPlayer = (originalPosition - player.transform.position).normalized;
            if (directionFromPlayer == Vector3.zero)
            {
                directionFromPlayer = Vector3.up;
            }

            if (showDebugLogs)
            {
                Debug.Log($"Enemy spawn position adjusted from {originalPosition} - too close to player or obstacles.");
            }

            return player.transform.position + directionFromPlayer * minSpawnDistanceFromPlayer;
        }

        return originalPosition;
    }

    private bool IsPositionClearOfAsteroids(Vector3 position)
    {
        // Method 1: Check against the Asteroids parent container logic
        var asteroidsParent = transform.Find("Asteroids");
        if (asteroidsParent != null)
        {
            foreach (Transform asteroid in asteroidsParent)
            {
                float dist = Vector3.Distance(position, asteroid.position);
                if (dist < minSpawnDistanceBetweenObjects)
                {
                    if (showDebugLogs) Debug.Log($"Spawn Unsafe: Hit Asteroid at {asteroid.position}. Dist: {dist:F2}");
                    return false;
                }
            }
        }
        return true;
    }
    #endregion

    #region Asteroid Spawning - Private Methods
    private void SpawnField(Transform parent, System.Random rng, List<Vector3> placedPositions)
    {
        // Uniform random inside a rectangle centered at `center` with size `fieldSize`
        for (int i = 0; i < asteroidCount; i++)
        {
            float rx = (float)(rng.NextDouble() * fieldSize.x) - fieldSize.x * 0.5f;
            float ry = (float)(rng.NextDouble() * fieldSize.y) - fieldSize.y * 0.5f;
            Vector3 pos = new Vector3(center.x + rx, center.y + ry, 0f);

            pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
            placedPositions.Add(pos); // Track this position

            CreateAsteroid(parent, pos, rng);
        }
    }

    private void SpawnBelt(Transform parent, System.Random rng, List<Vector3> placedPositions)
    {
        // Determine which center to use: beltCenter if set, otherwise map center
        Vector2 effectiveCenter = (beltCenter != Vector2.zero) ? beltCenter : center;

        // Place asteroids along a ring sector around center with inner radius and thickness
        for (int i = 0; i < asteroidCount; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double radius = beltInnerRadius + rng.NextDouble() * beltThickness;
            // jitter radial and angular spacing
            float r = (float)radius;
            Vector3 pos = new Vector3(
                effectiveCenter.x + Mathf.Cos((float)angle) * r,
                effectiveCenter.y + Mathf.Sin((float)angle) * r,
                0f);
            // small jitter orthogonal to the radial direction
            pos += RandomJitter(rng, positionJitter * Mathf.Min(1f, beltThickness));

            pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
            placedPositions.Add(pos);

            CreateAsteroid(parent, pos, rng);
        }
    }

    private void SpawnRing(Transform parent, System.Random rng, List<Vector3> placedPositions)
    {
        // Determine which center to use: ringCenter if set, otherwise map center
        Vector2 effectiveCenter = (ringCenter != Vector2.zero) ? ringCenter : center;

        // Place asteroids roughly on a ring of radius `ringRadius` with radial jitter
        for (int i = 0; i < asteroidCount; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double radius = ringRadius + (rng.NextDouble() - 0.5) * beltThickness;
            float r = (float)radius;
            Vector3 pos = new Vector3(
                effectiveCenter.x + Mathf.Cos((float)angle) * r,
                effectiveCenter.y + Mathf.Sin((float)angle) * r,
                0f);
            pos += RandomJitter(rng, positionJitter * 0.5f);

            pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
            placedPositions.Add(pos);

            CreateAsteroid(parent, pos, rng);
        }
    }

    private void SpawnGrid(Transform parent, System.Random rng, List<Vector3> placedPositions)
    {
        // Build a roughly grid-aligned set of asteroids inside fieldSize
        int cols = Mathf.Max(1, Mathf.FloorToInt(fieldSize.x / gridCellSize.x));
        int rows = Mathf.Max(1, Mathf.FloorToInt(fieldSize.y / gridCellSize.y));
        int count = 0;

        for (int y = 0; y < rows && count < asteroidCount; y++)
        {
            for (int x = 0; x < cols && count < asteroidCount; x++)
            {
                Vector3 cellCenter = new Vector3(
                    center.x + (x - (cols - 1) * 0.5f) * gridCellSize.x,
                    center.y + (y - (rows - 1) * 0.5f) * gridCellSize.y,
                    0f);
                // place with jitter
                Vector3 pos = cellCenter + RandomJitter(rng, positionJitter * Mathf.Min(gridCellSize.x, gridCellSize.y));

                pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
                placedPositions.Add(pos);

                CreateAsteroid(parent, pos, rng);
                count++;
            }
        }

        // If there's remaining count, fill randomly
        for (int i = 0; count < asteroidCount; i++, count++)
        {
            float rx = (float)(rng.NextDouble() * fieldSize.x) - fieldSize.x * 0.5f;
            float ry = (float)(rng.NextDouble() * fieldSize.y) - fieldSize.y * 0.5f;
            Vector3 pos = new Vector3(center.x + rx, center.y + ry, 0f);

            pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
            placedPositions.Add(pos);

            CreateAsteroid(parent, pos, rng);
        }
    }

    private void SpawnClusters(Transform parent, System.Random rng, List<Vector3> placedPositions)
    {
        // Generate cluster centers inside the field and spawn asteroids around those centers
        var clusterCenters = new List<Vector2>();
        for (int c = 0; c < clusters; c++)
        {
            float rx = (float)(rng.NextDouble() * fieldSize.x) - fieldSize.x * 0.5f;
            float ry = (float)(rng.NextDouble() * fieldSize.y) - fieldSize.y * 0.5f;
            clusterCenters.Add(new Vector2(center.x + rx, center.y + ry));
        }

        for (int i = 0; i < asteroidCount; i++)
        {
            int ci = rng.Next(clusterCenters.Count);
            // local polar around cluster center
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double radius = rng.NextDouble() * clusterRadius;
            Vector3 pos = new Vector3(
                clusterCenters[ci].x + Mathf.Cos((float)angle) * (float)radius,
                clusterCenters[ci].y + Mathf.Sin((float)angle) * (float)radius,
                0f);
            pos += RandomJitter(rng, positionJitter * clusterRadius);

            pos = GetSafeSpawnPosition(pos, rng, fieldSize.x, fieldSize.y, placedPositions);
            placedPositions.Add(pos);

            CreateAsteroid(parent, pos, rng);
        }
    }

    private Vector3 RandomJitter(System.Random rng, float magnitude)
    {
        if (magnitude <= 0f) return Vector3.zero;
        float jx = (float)((rng.NextDouble() * 2.0) - 1.0) * magnitude;
        float jy = (float)((rng.NextDouble() * 2.0) - 1.0) * magnitude;
        return new Vector3(jx, jy, 0f);
    }

    private void CreateAsteroid(Transform parent, Vector3 worldPosition, System.Random rng)
    {
        // Select which prefab to use based on spawn chance (deterministic with seed)
        GameObject prefabToUse;
        if (asteroidPrefab1 != null && asteroidPrefab2 != null)
        {
            // Both prefabs available - use weighted random selection
            double roll = rng.NextDouble();
            prefabToUse = (roll < asteroidPrefab1SpawnChance) ? asteroidPrefab1 : asteroidPrefab2;
        }
        else if (asteroidPrefab1 != null)
        {
            // Only prefab1 available
            prefabToUse = asteroidPrefab1;
        }
        else
        {
            // Only prefab2 available (we already checked that at least one exists)
            prefabToUse = asteroidPrefab2;
        }

        var go = Instantiate(prefabToUse, parent);

        // Preserve the prefab's Z position for proper camera rendering
        Vector3 finalPosition = new Vector3(worldPosition.x, worldPosition.y, prefabToUse.transform.position.z);
        go.transform.position = finalPosition;

        go.transform.rotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * rotationJitterDegrees * 2.0 - rotationJitterDegrees));

        // Get the prefab's original scale to use as base
        Vector3 prefabOriginalScale = prefabToUse.transform.localScale;

        // Generate scale multiplier (min to max range from Inspector)
        float t = (float)rng.NextDouble();
        float scaleMultiplier;
        Vector3 targetScale;

        if (forceCircularAsteroids)
        {
            // Uniform multiplier for circular appearance
            float minMultiplier = (asteroidScaleMin.x + asteroidScaleMin.y) * 0.5f;
            float maxMultiplier = (asteroidScaleMax.x + asteroidScaleMax.y) * 0.5f;
            scaleMultiplier = Mathf.Lerp(minMultiplier, maxMultiplier, t);

            // Apply uniform multiplier to prefab's original scale
            targetScale = prefabOriginalScale * scaleMultiplier;
        }
        else
        {
            // Independent X/Y multipliers
            float mx = Mathf.Lerp(asteroidScaleMin.x, asteroidScaleMax.x, t);
            float my = Mathf.Lerp(asteroidScaleMin.y, asteroidScaleMax.y, t);
            targetScale = new Vector3(prefabOriginalScale.x * mx, prefabOriginalScale.y * my, prefabOriginalScale.z);

            // Use average for health calculation
            scaleMultiplier = (mx + my) * 0.5f;
        }

        // Adjust health based on scale multiplier
        AsteroidScript asteroidData = go.GetComponent<AsteroidScript>();
        if (asteroidData != null)
        {
            asteroidData.maxHealth *= scaleMultiplier;
        }

        // Start at zero scale and animate to target
        go.transform.localScale = Vector3.zero;
        StartCoroutine(AnimateAsteroidGrow(go.transform, targetScale));
    }

    private IEnumerator AnimateAsteroidGrow(Transform asteroid, Vector3 targetScale)
    {
        float elapsed = 0f;
        while (elapsed < asteroidGrowDuration)
        {
            if (asteroid == null) yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / asteroidGrowDuration);
            // Use ease-out curve for smoother animation
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
            asteroid.localScale = Vector3.Lerp(Vector3.zero, targetScale, easedProgress);
            yield return null;
        }

        if (asteroid != null)
        {
            asteroid.localScale = targetScale;
        }
    }
    #endregion

    #region Boundary Building - Private Methods
    private void CreateWall(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        var wall = Instantiate(endBarrierPrefab, parent);
        wall.name = name;
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;

        // If prefab has non-1 base scale, this will overwrite it. Adjust if needed.
        wall.transform.localScale = localScale;
    }
    #endregion

    #region UI - Wave Text - Private Methods
    void SetupWaveText()
    {
        if (waveText != null)
        {
            // Re-enable in case it was disabled after previous use
            waveText.gameObject.SetActive(true);

            // Set font size and style
            waveText.fontSize = waveTextFontSize;
            waveText.fontStyle = FontStyles.Bold;

            // Set color with glow
            waveText.color = waveTextColor * waveTextGlowIntensity;

            // Center alignment
            waveText.alignment = TextAlignmentOptions.Center;

            //// Set text content
            //string sceneName = SceneManager.GetActiveScene().name;
            waveText.text = waveText.text.ToUpper();

            // Start fully transparent
            SetTextAlpha(0f);
        }
        else
        {
            Debug.LogWarning("Wave Text reference is missing!");
        }
    }

    IEnumerator ShowWaveText()
    {
        if (waveText == null) yield break;

        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            SetTextAlpha(alpha);
            yield return null;
        }
        SetTextAlpha(1f);

        // Display
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            SetTextAlpha(alpha);
            yield return null;
        }
        SetTextAlpha(0f);

        // Optionally disable the text object after fade out
        waveText.gameObject.SetActive(false);
    }

    private void SetTextAlpha(float alpha)
    {
        if (waveText != null)
        {
            Color color = waveText.color;
            color.a = alpha;
            waveText.color = color;
        }
    }
    #endregion
}