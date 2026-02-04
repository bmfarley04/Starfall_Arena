using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AsteroidPattern
{
    Scattered,
    Clustered,
    Falling
}

public class MapManagerScript : MonoBehaviour
{
    #region Asteroid Prefabs
    [Header("Asteroid Prefabs")]
    [Tooltip("Primary asteroid variant")]
    public GameObject asteroidPrefab1;
    [Tooltip("Secondary asteroid variant")]
    public GameObject asteroidPrefab2;
    [Range(0f, 1f)]
    [Tooltip("Chance to spawn asteroidPrefab1 (0.6 = 60%)")]
    public float asteroidPrefab1SpawnChance = 0.6f;
    #endregion

    #region Pattern Selection
    [Header("Pattern Selection")]
    public AsteroidPattern pattern = AsteroidPattern.Scattered;
    public int asteroidCount = 50;
    #endregion

    #region Random Seed
    [Header("Random Seed")]
    public bool useSeed = true;
    public int seed = 12345;
    #endregion

    #region Scattered Pattern Settings
    [Header("Scattered Pattern")]
    [Tooltip("Size of the rectangular spawn area")]
    public Vector2 scatteredAreaSize = new Vector2(18f, 10f);
    [Tooltip("Center offset for scattered spawning")]
    public Vector2 scatteredCenter = Vector2.zero;
    #endregion

    #region Clustered Pattern Settings
    [Header("Clustered Pattern")]
    [Tooltip("Size of the area where clusters can spawn")]
    public Vector2 clusteredAreaSize = new Vector2(18f, 10f);
    [Tooltip("Center offset for clustered spawning")]
    public Vector2 clusteredCenter = Vector2.zero;
    [Tooltip("Number of cluster centers to generate")]
    public int clusterCount = 5;
    [Tooltip("Radius of each cluster")]
    public float clusterRadius = 2f;
    [Tooltip("Minimum distance between cluster centers")]
    public float minClusterSpacing = 3f;
    #endregion

    #region Falling Pattern Settings
    [Header("Falling Pattern")]
    [Tooltip("Size of the horizontal play area")]
    public Vector2 fallingAreaSize = new Vector2(18f, 10f);
    [Tooltip("Center offset for falling spawning")]
    public Vector2 fallingCenter = Vector2.zero;
    [Tooltip("Extra distance outside the play area to spawn asteroids (top only)")]
    public float fallingSpawnMargin = 2f;
    [Tooltip("Minimum falling velocity (downward, negative Y)")]
    public float fallingMinVelocity = 2f;
    [Tooltip("Maximum falling velocity (downward, negative Y)")]
    public float fallingMaxVelocity = 8f;
    [Tooltip("Minimum horizontal drift velocity (X axis)")]
    public float fallingMinDrift = -1f;
    [Tooltip("Maximum horizontal drift velocity (X axis)")]
    public float fallingMaxDrift = 1f;
    [Tooltip("Time delay between spawning each asteroid (in seconds)")]
    public float fallingSpawnDelay = 0.1f;
    [Tooltip("Enable asteroid recycling when they reach the bottom")]
    public bool recycleFallingAsteroids = true;
    #endregion

    #region Asteroid Variation
    [Header("Asteroid Variation")]
    [Range(0f, 1f)]
    [Tooltip("Random position offset as fraction of spacing")]
    public float positionJitter = 0.2f;
    public Vector2 scaleMin = Vector2.one * 0.5f;
    public Vector2 scaleMax = Vector2.one * 1.5f;
    [Tooltip("Random rotation range in degrees")]
    public float rotationRange = 180f;
    [Tooltip("Use uniform scale for circular asteroids")]
    public bool uniformScale = true;
    #endregion

    #region Animation Settings
    [Header("Animation")]
    [Tooltip("Time for asteroids to grow from zero to full size")]
    public float growDuration = 0.5f;
    [Tooltip("Time for asteroids to shrink before destruction")]
    public float shrinkDuration = 0.3f;
    #endregion

    private Transform asteroidsParent;
    private List<GameObject> fallingAsteroids = new List<GameObject>();
    private System.Random fallingRng;

    void OnEnable()
    {
        SpawnAsteroids();
    }

    void Update()
    {
        if (pattern == AsteroidPattern.Falling && recycleFallingAsteroids)
        {
            RecycleFallingAsteroids();
        }
    }

    [ContextMenu("Spawn Asteroids")]
    public void SpawnAsteroids()
    {
        if (asteroidPrefab1 == null && asteroidPrefab2 == null)
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
            case AsteroidPattern.Scattered:
                SpawnScattered(rng);
                break;
            case AsteroidPattern.Clustered:
                SpawnClustered(rng);
                break;
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
        if (existing != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(existing.gameObject);
            else
                Destroy(existing.gameObject);
#else
            Destroy(existing.gameObject);
#endif
        }
    }

    public void ShrinkAndDestroy()
    {
        if (asteroidsParent != null)
        {
            StartCoroutine(ShrinkAllAsteroids());
        }
    }

    #region Spawn Patterns
    private void SpawnScattered(System.Random rng)
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            float x = (float)(rng.NextDouble() - 0.5) * scatteredAreaSize.x;
            float y = (float)(rng.NextDouble() - 0.5) * scatteredAreaSize.y;
            Vector3 pos = new Vector3(scatteredCenter.x + x, scatteredCenter.y + y, 0f);

            CreateAsteroid(pos, rng);
        }
    }

    private void SpawnClustered(System.Random rng)
    {
        // Generate cluster centers with minimum spacing
        List<Vector2> clusterCenters = new List<Vector2>();
        int maxAttempts = 100;

        for (int i = 0; i < clusterCount; i++)
        {
            Vector2 candidate = Vector2.zero;
            bool valid = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float x = (float)(rng.NextDouble() - 0.5) * clusteredAreaSize.x + clusteredCenter.x;
                float y = (float)(rng.NextDouble() - 0.5) * clusteredAreaSize.y + clusteredCenter.y;
                candidate = new Vector2(x, y);

                valid = true;
                foreach (var center in clusterCenters)
                {
                    if (Vector2.Distance(candidate, center) < minClusterSpacing)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid) break;
            }

            if (valid)
                clusterCenters.Add(candidate);
        }

        if (clusterCenters.Count == 0)
        {
            Debug.LogWarning("Could not place any clusters. Try reducing minClusterSpacing or increasing clusteredAreaSize.");
            return;
        }

        // Distribute asteroids among clusters
        int asteroidsPerCluster = asteroidCount / clusterCenters.Count;
        int remainder = asteroidCount % clusterCenters.Count;

        for (int c = 0; c < clusterCenters.Count; c++)
        {
            int count = asteroidsPerCluster + (c < remainder ? 1 : 0);
            Vector2 center = clusterCenters[c];

            for (int i = 0; i < count; i++)
            {
                // Random position within cluster radius using polar coordinates for even distribution
                float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
                float dist = (float)Math.Sqrt(rng.NextDouble()) * clusterRadius;

                float x = center.x + Mathf.Cos(angle) * dist;
                float y = center.y + Mathf.Sin(angle) * dist;
                Vector3 pos = new Vector3(x, y, 0f);

                // Add jitter
                pos += GetJitter(rng, positionJitter * clusterRadius * 0.5f);

                CreateAsteroid(pos, rng);
            }
        }
    }

    private IEnumerator SpawnFallingStaggered(System.Random rng)
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            SpawnSingleFallingAsteroid(rng);

            if (fallingSpawnDelay > 0f && i < asteroidCount - 1)
            {
                yield return new WaitForSeconds(fallingSpawnDelay);
            }
        }
    }

    private void SpawnSingleFallingAsteroid(System.Random rng)
    {
        float spawnY = fallingCenter.y + (fallingAreaSize.y * 0.5f) + fallingSpawnMargin;

        // Random X position across the horizontal area
        float x = (float)(rng.NextDouble() - 0.5) * fallingAreaSize.x + fallingCenter.x;
        Vector3 pos = new Vector3(x, spawnY, 0f);

        // Create the asteroid and get its GameObject
        GameObject asteroid = CreateFallingAsteroid(pos, rng);

        if (asteroid != null)
        {
            // Add Rigidbody2D if it doesn't exist
            Rigidbody2D rb = asteroid.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = asteroid.AddComponent<Rigidbody2D>();
            }

            // Configure Rigidbody2D for falling motion
            rb.bodyType = RigidbodyType2D.Dynamic; // Ensure it's dynamic, not kinematic/static
            rb.gravityScale = 0f; // No gravity in space
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Set falling velocity
            float verticalVelocity = -(float)(rng.NextDouble() * (fallingMaxVelocity - fallingMinVelocity) + fallingMinVelocity);
            float horizontalDrift = (float)(rng.NextDouble() * (fallingMaxDrift - fallingMinDrift) + fallingMinDrift);
            rb.linearVelocity = new Vector2(horizontalDrift, verticalVelocity);

            // Set angular velocity for rotation while falling
            float angularVelocity = (float)(rng.NextDouble() * 2 - 1) * 90f; // -90 to 90 degrees per second
            rb.angularVelocity = angularVelocity;

            // Track this asteroid for recycling
            fallingAsteroids.Add(asteroid);
        }
    }

    private void RecycleFallingAsteroids()
    {
        if (fallingAsteroids.Count == 0 || fallingRng == null) return;

        float despawnY = fallingCenter.y - (fallingAreaSize.y * 0.5f) - fallingSpawnMargin;
        float spawnY = fallingCenter.y + (fallingAreaSize.y * 0.5f) + fallingSpawnMargin;

        for (int i = fallingAsteroids.Count - 1; i >= 0; i--)
        {
            GameObject asteroid = fallingAsteroids[i];

            // Remove null references (destroyed asteroids)
            if (asteroid == null)
            {
                fallingAsteroids.RemoveAt(i);
                continue;
            }

            // Check if asteroid has fallen below the bottom
            if (asteroid.transform.position.y < despawnY)
            {
                // Reposition at top with new random X
                float x = (float)(fallingRng.NextDouble() - 0.5) * fallingAreaSize.x + fallingCenter.x;
                asteroid.transform.position = new Vector3(x, spawnY, asteroid.transform.position.z);

                // Reset velocity
                Rigidbody2D rb = asteroid.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float verticalVelocity = -(float)(fallingRng.NextDouble() * (fallingMaxVelocity - fallingMinVelocity) + fallingMinVelocity);
                    float horizontalDrift = (float)(fallingRng.NextDouble() * (fallingMaxDrift - fallingMinDrift) + fallingMinDrift);
                    rb.linearVelocity = new Vector2(horizontalDrift, verticalVelocity);

                    float angularVelocity = (float)(fallingRng.NextDouble() * 2 - 1) * 90f;
                    rb.angularVelocity = angularVelocity;
                }
            }
        }
    }
    #endregion

    #region Asteroid Creation
    private void CreateAsteroid(Vector3 position, System.Random rng)
    {
        GameObject prefab = SelectPrefab(rng);
        if (prefab == null) return;

        var asteroid = Instantiate(prefab, asteroidsParent);
        asteroid.transform.position = new Vector3(position.x, position.y, prefab.transform.position.z);
        asteroid.transform.rotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 2 - 1) * rotationRange);

        Vector3 targetScale = CalculateScale(prefab, rng);
        asteroid.transform.localScale = Vector3.zero;

        StartCoroutine(GrowAsteroid(asteroid.transform, targetScale));
    }

    private GameObject CreateFallingAsteroid(Vector3 position, System.Random rng)
    {
        GameObject prefab = SelectPrefab(rng);
        if (prefab == null) return null;

        var asteroid = Instantiate(prefab, asteroidsParent);
        asteroid.transform.position = new Vector3(position.x, position.y, prefab.transform.position.z);
        asteroid.transform.rotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 2 - 1) * rotationRange);

        Vector3 targetScale = CalculateScale(prefab, rng);
        asteroid.transform.localScale = Vector3.zero;

        StartCoroutine(GrowAsteroid(asteroid.transform, targetScale));

        return asteroid;
    }

    private GameObject SelectPrefab(System.Random rng)
    {
        if (asteroidPrefab1 != null && asteroidPrefab2 != null)
        {
            return rng.NextDouble() < asteroidPrefab1SpawnChance ? asteroidPrefab1 : asteroidPrefab2;
        }
        return asteroidPrefab1 ?? asteroidPrefab2;
    }

    private Vector3 CalculateScale(GameObject prefab, System.Random rng)
    {
        Vector3 baseScale = prefab.transform.localScale;
        float t = (float)rng.NextDouble();

        if (uniformScale)
        {
            float multiplier = Mathf.Lerp((scaleMin.x + scaleMin.y) * 0.5f, (scaleMax.x + scaleMax.y) * 0.5f, t);
            return baseScale * multiplier;
        }
        else
        {
            float mx = Mathf.Lerp(scaleMin.x, scaleMax.x, t);
            float my = Mathf.Lerp(scaleMin.y, scaleMax.y, t);
            return new Vector3(baseScale.x * mx, baseScale.y * my, baseScale.z);
        }
    }
    #endregion

    #region Helpers
    private Vector3 GetJitter(System.Random rng, float magnitude)
    {
        if (magnitude <= 0f) return Vector3.zero;
        float jx = (float)(rng.NextDouble() * 2 - 1) * magnitude;
        float jy = (float)(rng.NextDouble() * 2 - 1) * magnitude;
        return new Vector3(jx, jy, 0f);
    }
    #endregion

    #region Animations
    private IEnumerator GrowAsteroid(Transform asteroid, Vector3 targetScale)
    {
        float elapsed = 0f;
        while (elapsed < growDuration)
        {
            if (asteroid == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2f); // Ease out
            asteroid.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased);
            yield return null;
        }

        if (asteroid != null)
            asteroid.localScale = targetScale;
    }

    private IEnumerator ShrinkAllAsteroids()
    {
        if (asteroidsParent == null) yield break;

        List<Transform> asteroids = new List<Transform>();
        List<Vector3> originalScales = new List<Vector3>();

        foreach (Transform child in asteroidsParent)
        {
            asteroids.Add(child);
            originalScales.Add(child.localScale);
        }

        float elapsed = 0f;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            float eased = Mathf.Pow(t, 2f); // Ease in

            for (int i = 0; i < asteroids.Count; i++)
            {
                if (asteroids[i] != null)
                    asteroids[i].localScale = Vector3.Lerp(originalScales[i], Vector3.zero, eased);
            }
            yield return null;
        }

        Destroy(asteroidsParent.gameObject);
        asteroidsParent = null;
    }
    #endregion
}
