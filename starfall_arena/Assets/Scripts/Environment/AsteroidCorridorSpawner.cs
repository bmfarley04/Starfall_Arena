using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns asteroids in a belt/arc pattern - a curved band of asteroids
/// where the camera views a portion of the belt.
///
/// Pattern Description:
/// - Asteroids form a curved belt/arc in 3D space
/// - Camera sees a section of this belt
/// - Perspective camera naturally handles size based on distance
/// - Dense clustering with natural randomness
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

    [Header("Belt Shape")]
    [Tooltip("Total number of asteroids to spawn")]
    [SerializeField] private int asteroidCount = 150;

    [Tooltip("Center point of the belt arc")]
    [SerializeField] private Vector3 beltCenter = new Vector3(0, 0, 50);

    [Tooltip("Radius of the belt arc from center")]
    [SerializeField] private float beltRadius = 60f;

    [Tooltip("Thickness of the belt (radial spread)")]
    [SerializeField] private float beltThickness = 30f;

    [Tooltip("Vertical spread of the belt")]
    [SerializeField] private float beltHeight = 25f;

    [Tooltip("Start angle of the arc (degrees)")]
    [SerializeField] private float arcStartAngle = -60f;

    [Tooltip("End angle of the arc (degrees)")]
    [SerializeField] private float arcEndAngle = 60f;

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

        // Check if any prefabs are valid
        bool hasValidPrefab = false;
        foreach (var prefab in asteroidPrefabs)
        {
            if (prefab != null)
            {
                hasValidPrefab = true;
                break;
            }
        }
        if (!hasValidPrefab)
            return;

        ClearAsteroids();
        CreateContainer();

        // Build list of valid prefabs
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (var prefab in asteroidPrefabs)
        {
            if (prefab != null)
                validPrefabs.Add(prefab);
        }

        Random.InitState(seed);

        // Spawn asteroids in belt pattern
        for (int i = 0; i < asteroidCount; i++)
        {
            // Random angle within the arc
            float angle = Random.Range(arcStartAngle, arcEndAngle) * Mathf.Deg2Rad;

            // Random distance from belt center (with thickness)
            float radius = beltRadius + Random.Range(-beltThickness / 2f, beltThickness / 2f);

            // Calculate position on the arc (XZ plane, with Y variation)
            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius;
            float y = Random.Range(-beltHeight / 2f, beltHeight / 2f);

            Vector3 position = beltCenter + new Vector3(x, y, z);

            // Random scale (perspective handles depth-based sizing)
            float scale = Random.Range(minScale, maxScale);

            // Random prefab selection
            GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

            SpawnAsteroid(prefab, position, scale);
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

    private void SpawnAsteroid(GameObject prefab, Vector3 position, float scale)
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

        asteroid.transform.localPosition = position;
        asteroid.transform.localScale = Vector3.one * scale;

        if (randomizeRotation)
        {
            asteroid.transform.localRotation = Random.rotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        // Draw belt center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + beltCenter, 2f);

        // Draw belt arc outline
        Gizmos.color = Color.cyan;
        int segments = 32;
        float angleStep = (arcEndAngle - arcStartAngle) / segments;

        // Inner edge
        Vector3 prevInner = Vector3.zero;
        Vector3 prevOuter = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (arcStartAngle + angleStep * i) * Mathf.Deg2Rad;
            float innerRadius = beltRadius - beltThickness / 2f;
            float outerRadius = beltRadius + beltThickness / 2f;

            Vector3 inner = transform.position + beltCenter + new Vector3(
                Mathf.Sin(angle) * innerRadius,
                0,
                Mathf.Cos(angle) * innerRadius
            );

            Vector3 outer = transform.position + beltCenter + new Vector3(
                Mathf.Sin(angle) * outerRadius,
                0,
                Mathf.Cos(angle) * outerRadius
            );

            if (i > 0)
            {
                Gizmos.DrawLine(prevInner, inner);
                Gizmos.DrawLine(prevOuter, outer);
            }

            // Draw connecting lines at start and end
            if (i == 0 || i == segments)
            {
                Gizmos.DrawLine(inner, outer);
            }

            prevInner = inner;
            prevOuter = outer;
        }

        // Draw height bounds
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        float midAngle = ((arcStartAngle + arcEndAngle) / 2f) * Mathf.Deg2Rad;
        Vector3 midPoint = transform.position + beltCenter + new Vector3(
            Mathf.Sin(midAngle) * beltRadius,
            0,
            Mathf.Cos(midAngle) * beltRadius
        );
        Gizmos.DrawLine(midPoint + Vector3.up * beltHeight / 2f, midPoint + Vector3.down * beltHeight / 2f);
    }
}
