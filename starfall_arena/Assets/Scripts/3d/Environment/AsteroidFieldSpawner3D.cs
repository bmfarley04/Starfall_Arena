using System.Collections.Generic;
using UnityEngine;

public class AsteroidFieldSpawner3D : MonoBehaviour
{
    private const string SpawnedContainerName = "_SpawnedAsteroids";
    private const int OverlapBufferSize = 64;
    private const int MaxSeedValue = int.MaxValue;

    [Header("Asteroid Selection")]
    [Tooltip("Asteroid prefabs that this spawner can place. Each prefab currently has equal spawn chance.")]
    [SerializeField] private List<GameObject> asteroidPrefabs = new();

    [Tooltip("Exact number of asteroids to try spawning when this field generates.")]
    [SerializeField] private int spawnCount = 24;

    [Header("Generation Seed")]
    [Tooltip("If true, this spawner uses the configured seed so the same settings rebuild the same asteroid field layout deterministically.")]
    [SerializeField] private bool useDeterministicSeed = true;

    [Tooltip("Seed used for deterministic asteroid placement, prefab selection, scale, and initial rotation.")]
    [SerializeField] private int generationSeed = 12345;

    [Tooltip("If true, Generate Asteroids rolls a new seed first. Useful when you want variety instead of repeatability.")]
    [SerializeField] private bool randomizeSeedOnGenerate;

    [Header("Spawn Volume")]
    [Tooltip("Full width, height, and depth of the box volume centered on this spawner transform.")]
    [SerializeField] private Vector3 spawnBoxSize = new Vector3(1000f, 1000f, 1000f);

    [Tooltip("Keeps a clear spherical fight space around the spawner center so giant asteroids do not block the opening arena.")]
    [SerializeField] private float centerSafeZoneRadius = 150f;

    [Header("Scale")]
    [Tooltip("Minimum uniform asteroid scale applied at spawn. Use large values here when these are meant to read as enormous obstacles.")]
    [SerializeField] private float minUniformScale = 40f;

    [Tooltip("Maximum uniform asteroid scale applied at spawn.")]
    [SerializeField] private float maxUniformScale = 120f;

    [Header("Placement Validation")]
    [Tooltip("Layers treated as blocking when validating asteroid placement against other obstacles or world geometry.")]
    [SerializeField] private LayerMask overlapLayers = ~0;

    [Tooltip("Extra world-space clearance added around collider bounds so asteroids do not barely graze each other.")]
    [SerializeField] private float collisionPadding = 5f;

    [Tooltip("How many random position attempts are allowed for each asteroid before this spawner gives up on that slot.")]
    [SerializeField] private int maxPlacementAttemptsPerAsteroid = 20;

    [Header("Spawned Hierarchy")]
    [Tooltip("Optional parent for the generated asteroid container. If empty, this spawner transform is used.")]
    [SerializeField] private Transform spawnedAsteroidParent;

    [Tooltip("If true, logs setup and placement warnings when prefabs are missing, scales are invalid, or valid positions run out.")]
    [SerializeField] private bool logPlacementWarnings = true;

    private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

    private void Awake()
    {
        GenerateAsteroids();
    }

    [ContextMenu("Generate Asteroids")]
    public void GenerateAsteroids()
    {
        ClearSpawnedAsteroids();

        if (!ValidateSetup(out int resolvedSpawnCount, out float resolvedMinScale, out float resolvedMaxScale))
        {
            return;
        }

        Random.State previousRandomState = Random.state;
        PrepareRandomState();

        int spawned = 0;
        for (int i = 0; i < resolvedSpawnCount; i++)
        {
            if (TrySpawnAsteroid(resolvedMinScale, resolvedMaxScale))
            {
                spawned++;
            }
            else
            {
                LogPlacementWarning($"could not place asteroid {i + 1}/{resolvedSpawnCount} after {Mathf.Max(1, maxPlacementAttemptsPerAsteroid)} attempts. The field may be too dense for the current size, safe-zone radius, or padding.");
            }
        }

        if (spawned < resolvedSpawnCount)
        {
            LogPlacementWarning($"spawned {spawned} of {resolvedSpawnCount} requested asteroids. Reduce Spawn Count, scale range, safe-zone radius, or collision padding if you need denser coverage.");
        }

        Random.state = previousRandomState;
    }

    [ContextMenu("Clear Spawned Asteroids")]
    public void ClearSpawnedAsteroids()
    {
        Transform container = FindSpawnedContainer();
        if (container == null)
        {
            return;
        }

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        if (Application.isPlaying)
        {
            Destroy(container.gameObject);
        }
        else
        {
            DestroyImmediate(container.gameObject);
        }
    }

    [ContextMenu("Roll New Seed")]
    public void RollNewSeed()
    {
        generationSeed = UnityEngine.Random.Range(0, MaxSeedValue);
    }

    private bool ValidateSetup(out int resolvedSpawnCount, out float resolvedMinScale, out float resolvedMaxScale)
    {
        resolvedSpawnCount = Mathf.Max(0, spawnCount);
        resolvedMinScale = Mathf.Max(0.01f, minUniformScale);
        resolvedMaxScale = Mathf.Max(resolvedMinScale, maxUniformScale);

        if (asteroidPrefabs == null || asteroidPrefabs.Count == 0)
        {
            LogPlacementWarning("generation ignored because Asteroid Prefabs is empty.");
            return false;
        }

        if (resolvedSpawnCount <= 0)
        {
            LogPlacementWarning("generation ignored because Spawn Count is 0.");
            return false;
        }

        if (spawnBoxSize.x <= 0f || spawnBoxSize.y <= 0f || spawnBoxSize.z <= 0f)
        {
            LogPlacementWarning("generation ignored because Spawn Box Size must be greater than 0 on every axis.");
            return false;
        }

        return true;
    }

    private bool TrySpawnAsteroid(float resolvedMinScale, float resolvedMaxScale)
    {
        int attempts = Mathf.Max(1, maxPlacementAttemptsPerAsteroid);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            GameObject prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Count)];
            if (prefab == null)
            {
                LogPlacementWarning("skipped a null asteroid prefab entry.");
                continue;
            }

            float uniformScale = Random.Range(resolvedMinScale, resolvedMaxScale);
            Vector3 candidatePosition = GetRandomPositionInBox();
            Quaternion candidateRotation = Random.rotationUniform;

            if (IsInsideCenterSafeZone(candidatePosition))
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, candidatePosition, candidateRotation, ResolveSpawnedContainer());
            instance.transform.localScale = Vector3.one * uniformScale;
            instance.name = prefab.name;

            if (HasValidPlacement(instance))
            {
                return true;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        return false;
    }

    private bool HasValidPlacement(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            LogPlacementWarning($"spawned asteroid prefab '{instance.name}' has no 3D Collider, so placement validation cannot guarantee obstacle spacing.");
            return true;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            Vector3 halfExtents = bounds.extents + Vector3.one * Mathf.Max(0f, collisionPadding);
            int overlapCount = Physics.OverlapBoxNonAlloc(bounds.center, halfExtents, _overlapBuffer, collider.transform.rotation, overlapLayers, QueryTriggerInteraction.Ignore);
            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider overlap = _overlapBuffer[overlapIndex];
                if (overlap == null || overlap.transform.IsChildOf(instance.transform))
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    private bool IsInsideCenterSafeZone(Vector3 candidatePosition)
    {
        float radius = Mathf.Max(0f, centerSafeZoneRadius);
        if (radius <= 0f)
        {
            return false;
        }

        return (candidatePosition - transform.position).sqrMagnitude < radius * radius;
    }

    private Vector3 GetRandomPositionInBox()
    {
        Vector3 halfSize = spawnBoxSize * 0.5f;
        Vector3 localOffset = new Vector3(
            Random.Range(-halfSize.x, halfSize.x),
            Random.Range(-halfSize.y, halfSize.y),
            Random.Range(-halfSize.z, halfSize.z));
        return transform.TransformPoint(localOffset);
    }

    private Transform ResolveSpawnedContainer()
    {
        Transform container = FindSpawnedContainer();
        if (container != null)
        {
            return container;
        }

        Transform parent = spawnedAsteroidParent != null ? spawnedAsteroidParent : transform;
        GameObject containerObject = new GameObject(SpawnedContainerName);
        container = containerObject.transform;
        container.SetParent(parent, false);
        container.localPosition = Vector3.zero;
        container.localRotation = Quaternion.identity;
        container.localScale = Vector3.one;
        return container;
    }

    private Transform FindSpawnedContainer()
    {
        Transform parent = spawnedAsteroidParent != null ? spawnedAsteroidParent : transform;
        return parent.Find(SpawnedContainerName);
    }

    private void LogPlacementWarning(string message)
    {
        if (!logPlacementWarnings)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(AsteroidFieldSpawner3D)}] {name} {message}", this);
    }

    private void PrepareRandomState()
    {
        if (!useDeterministicSeed)
        {
            return;
        }

        if (randomizeSeedOnGenerate)
        {
            generationSeed = UnityEngine.Random.Range(0, MaxSeedValue);
        }

        Random.InitState(generationSeed);
    }
}
