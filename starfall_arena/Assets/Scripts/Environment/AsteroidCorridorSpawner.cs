using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns asteroids in a cone/frustum pattern matching perspective camera view.
/// Asteroids spread wider at far distances, narrower at close distances,
/// creating an even screen-space distribution.
/// </summary>
[ExecuteAlways]
public class AsteroidCorridorSpawner : MonoBehaviour
{
    [Header("Asteroid Prefabs")]
    [Tooltip("Drag asteroid prefab models here. They will be randomly selected.")]
    [SerializeField] private GameObject[] asteroidPrefabs;

    [Header("Randomization")]
    [Tooltip("Seed for deterministic spawning - same seed produces exact same layout")]
    [SerializeField] private int seed = 12345;

    [Header("Cone Shape")]
    [Tooltip("Total number of asteroids to spawn")]
    [SerializeField] private int asteroidCount = 150;

    [Tooltip("Near distance from spawner (cone tip)")]
    [SerializeField] private float nearDistance = 10f;

    [Tooltip("Far distance from spawner (cone base)")]
    [SerializeField] private float farDistance = 120f;

    [Tooltip("Horizontal spread angle in degrees (similar to camera FOV)")]
    [SerializeField] private float horizontalSpread = 70f;

    [Tooltip("Vertical spread angle in degrees")]
    [SerializeField] private float verticalSpread = 45f;

    [Tooltip("Offset the cone center (e.g., shift left/right/up/down)")]
    [SerializeField] private Vector3 coneOffset = Vector3.zero;

    [Header("Depth Distribution")]
    [Tooltip("Bias toward far (>0.5) or near (<0.5). 0.5 = uniform, 0.7 = more far asteroids")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float depthBias = 0.6f;

    [Header("Asteroid Size")]
    [Tooltip("Minimum asteroid scale")]
    [SerializeField] private float minScale = 0.3f;

    [Tooltip("Maximum asteroid scale")]
    [SerializeField] private float maxScale = 2.5f;

    [Header("Asteroid Rotation")]
    [SerializeField] private bool randomizeRotation = true;

    [Header("Spawn Container")]
    [Tooltip("Name of the child object that holds spawned asteroids")]
    [SerializeField] private string containerName = "SpawnedAsteroids";

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Transform asteroidContainer;

    private void OnEnable()
    {
        RegenerateAsteroids();
    }

    private void OnValidate()
    {
        #if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                RegenerateAsteroids();
            }
        };
        #endif
    }

    [ContextMenu("Regenerate Asteroids")]
    public void RegenerateAsteroids()
    {
        if (asteroidPrefabs == null || asteroidPrefabs.Length == 0)
            return;

        // Build list of valid prefabs
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (var prefab in asteroidPrefabs)
        {
            if (prefab != null)
                validPrefabs.Add(prefab);
        }
        if (validPrefabs.Count == 0)
            return;

        ClearAsteroids();
        CreateContainer();

        Random.InitState(seed);

        // Spawn asteroids in cone pattern
        for (int i = 0; i < asteroidCount; i++)
        {
            // Depth with bias (power function shifts distribution)
            // depthBias > 0.5 = more asteroids far away
            float t = Random.Range(0f, 1f);
            float biasedT = Mathf.Pow(t, 1f / (depthBias * 2f));
            float depth = Mathf.Lerp(nearDistance, farDistance, biasedT);

            // Calculate spread at this depth (cone gets wider with distance)
            float depthRatio = depth / farDistance;
            float hSpreadAtDepth = Mathf.Tan(horizontalSpread * 0.5f * Mathf.Deg2Rad) * depth;
            float vSpreadAtDepth = Mathf.Tan(verticalSpread * 0.5f * Mathf.Deg2Rad) * depth;

            // Random position within the cone cross-section at this depth
            float x = Random.Range(-hSpreadAtDepth, hSpreadAtDepth);
            float y = Random.Range(-vSpreadAtDepth, vSpreadAtDepth);

            Vector3 localPos = new Vector3(x, y, depth) + coneOffset;
            Vector3 worldPos = transform.TransformPoint(localPos);

            // Random scale
            float scale = Random.Range(minScale, maxScale);

            // Random prefab
            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

            SpawnAsteroid(prefab, worldPos, scale);
        }
    }

    private void ClearAsteroids()
    {
        Transform existing = transform.Find(containerName);
        if (existing != null)
        {
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }
    }

    private void CreateContainer()
    {
        GameObject containerObj = new GameObject(containerName);
        containerObj.transform.SetParent(transform);
        containerObj.transform.localPosition = Vector3.zero;
        containerObj.transform.localRotation = Quaternion.identity;
        containerObj.transform.localScale = Vector3.one;
        asteroidContainer = containerObj.transform;
    }

    private void SpawnAsteroid(GameObject prefab, Vector3 worldPosition, float scale)
    {
        if (prefab == null)
            return;

        GameObject asteroid;

        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            asteroid = (GameObject)PrefabUtility.InstantiatePrefab(prefab, asteroidContainer);
        }
        else
        {
            asteroid = Instantiate(prefab, asteroidContainer);
        }
        #else
        asteroid = Instantiate(prefab, asteroidContainer);
        #endif

        asteroid.transform.position = worldPosition;
        asteroid.transform.localScale = Vector3.one * scale;

        if (randomizeRotation)
        {
            asteroid.transform.rotation = Random.rotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        // Draw cone outline
        Vector3 origin = transform.position + transform.TransformDirection(coneOffset);

        // Calculate corners at near and far planes
        float nearH = Mathf.Tan(horizontalSpread * 0.5f * Mathf.Deg2Rad) * nearDistance;
        float nearV = Mathf.Tan(verticalSpread * 0.5f * Mathf.Deg2Rad) * nearDistance;
        float farH = Mathf.Tan(horizontalSpread * 0.5f * Mathf.Deg2Rad) * farDistance;
        float farV = Mathf.Tan(verticalSpread * 0.5f * Mathf.Deg2Rad) * farDistance;

        // Near plane corners (local space)
        Vector3[] nearCorners = new Vector3[]
        {
            new Vector3(-nearH, -nearV, nearDistance),
            new Vector3(nearH, -nearV, nearDistance),
            new Vector3(nearH, nearV, nearDistance),
            new Vector3(-nearH, nearV, nearDistance)
        };

        // Far plane corners (local space)
        Vector3[] farCorners = new Vector3[]
        {
            new Vector3(-farH, -farV, farDistance),
            new Vector3(farH, -farV, farDistance),
            new Vector3(farH, farV, farDistance),
            new Vector3(-farH, farV, farDistance)
        };

        // Transform to world space
        for (int i = 0; i < 4; i++)
        {
            nearCorners[i] = transform.TransformPoint(nearCorners[i] + coneOffset);
            farCorners[i] = transform.TransformPoint(farCorners[i] + coneOffset);
        }

        // Draw near plane
        Gizmos.color = Color.green;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(nearCorners[i], nearCorners[(i + 1) % 4]);
        }

        // Draw far plane
        Gizmos.color = Color.cyan;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(farCorners[i], farCorners[(i + 1) % 4]);
        }

        // Draw connecting edges
        Gizmos.color = Color.yellow;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(nearCorners[i], farCorners[i]);
        }

        // Draw origin
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, 1f);
    }
}
