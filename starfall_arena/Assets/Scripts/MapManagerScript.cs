using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    private Transform asteroidsParent;
    private List<GameObject> fallingAsteroids = new List<GameObject>();
    private System.Random fallingRng;

    void OnEnable() => SpawnAsteroids();

    void Update()
    {
        if (pattern == AsteroidPattern.Falling && falling.recycle)
            RecycleFallingAsteroids();
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
        if (centers.Count == 0)
        {
            Debug.LogWarning("Could not place any clusters. Try reducing minSpacing or increasing area size.");
            return;
        }

        int perCluster = asteroidCount / centers.Count;
        int remainder = asteroidCount % centers.Count;

        for (int c = 0; c < centers.Count; c++)
        {
            int count = perCluster + (c < remainder ? 1 : 0);
            Vector2 center = centers[c];

            for (int i = 0; i < count; i++)
            {
                // Polar coordinates for even distribution within cluster
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
                if (rb != null) ApplyFallingVelocity(rb, fallingRng);
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
