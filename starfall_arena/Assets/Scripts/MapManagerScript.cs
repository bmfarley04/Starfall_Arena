using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AsteroidPattern
{
    Scattered,
    CircularBorder
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

    #region Circular Border Pattern Settings
    [Header("Circular Border Pattern")]
    [Tooltip("Radius of the circular border")]
    public float borderRadius = 8f;
    [Tooltip("Thickness of the asteroid ring")]
    public float borderThickness = 2f;
    [Tooltip("Center of the circular border")]
    public Vector2 borderCenter = Vector2.zero;
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

    void OnEnable()
    {
        SpawnAsteroids();
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
            case AsteroidPattern.CircularBorder:
                SpawnCircularBorder(rng);
                break;
        }
    }

    [ContextMenu("Clear Asteroids")]
    public void ClearAsteroids()
    {
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

    private void SpawnCircularBorder(System.Random rng)
    {
        for (int i = 0; i < asteroidCount; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float radius = borderRadius + (float)(rng.NextDouble() - 0.5) * borderThickness;

            float x = borderCenter.x + Mathf.Cos(angle) * radius;
            float y = borderCenter.y + Mathf.Sin(angle) * radius;
            Vector3 pos = new Vector3(x, y, 0f);

            // Add jitter
            pos += GetJitter(rng, positionJitter * borderThickness * 0.5f);

            CreateAsteroid(pos, rng);
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
