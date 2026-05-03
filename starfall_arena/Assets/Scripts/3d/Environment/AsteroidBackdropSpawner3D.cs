using System.Collections.Generic;
using UnityEngine;

public class AsteroidBackdropSpawner3D : MonoBehaviour
{
    private const string SpawnedContainerName = "_SpawnedBackdropAsteroids";
    private const int MaxSeedValue = int.MaxValue;
    private const int MaxShellSampleAttempts = 256;

    [Header("Asteroid Selection")]
    [Tooltip("Visual-only asteroid prefabs that this spawner can place outside the arena. Each prefab currently has equal spawn chance.")]
    [SerializeField] private List<GameObject> asteroidPrefabs = new();

    [Tooltip("Exact number of backdrop asteroids to try spawning when this component generates.")]
    [SerializeField] private int spawnCount = 48;

    [Header("Generation Seed")]
    [Tooltip("If true, this spawner uses the configured seed so the same settings rebuild the same backdrop layout deterministically.")]
    [SerializeField] private bool useDeterministicSeed = true;

    [Tooltip("Seed used for deterministic prefab selection, position, scale, and initial rotation.")]
    [SerializeField] private int generationSeed = 54321;

    [Tooltip("If true, Generate Backdrop Asteroids rolls a new seed first. Useful when you want a fresh vista quickly.")]
    [SerializeField] private bool randomizeSeedOnGenerate;

    [Header("Spawn Shell")]
    [Tooltip("Full width, height, and depth of the outer box volume centered on this spawner transform.")]
    [SerializeField] private Vector3 outerSpawnBoxSize = new Vector3(2200f, 2200f, 2200f);

    [Tooltip("Full width, height, and depth of the inner exclusion box that stays empty so backdrop asteroids remain outside the main arena volume.")]
    [SerializeField] private Vector3 innerExclusionBoxSize = new Vector3(1200f, 1200f, 1200f);

    [Tooltip("Extra clearance added beyond the inner exclusion box before a backdrop asteroid is allowed to spawn.")]
    [SerializeField] private float innerExclusionPadding = 50f;

    [Header("Scale")]
    [Tooltip("Minimum uniform scale applied to each backdrop asteroid.")]
    [SerializeField] private float minUniformScale = 80f;

    [Tooltip("Maximum uniform scale applied to each backdrop asteroid.")]
    [SerializeField] private float maxUniformScale = 240f;

    [Header("Visual Cleanup")]
    [Tooltip("If true, disables every 3D collider found on the spawned backdrop asteroids so they stay visual-only.")]
    [SerializeField] private bool disableSpawnedColliders = true;

    [Tooltip("Optional parent for the generated backdrop container. If empty, this spawner transform is used.")]
    [SerializeField] private Transform spawnedAsteroidParent;

    [Tooltip("If true, logs setup warnings when prefabs are missing or the shell dimensions are invalid.")]
    [SerializeField] private bool logPlacementWarnings = true;

    private void Awake()
    {
        GenerateBackdropAsteroids();
    }

    [ContextMenu("Generate Backdrop Asteroids")]
    public void GenerateBackdropAsteroids()
    {
        ClearSpawnedBackdropAsteroids();

        if (!ValidateSetup(out int resolvedSpawnCount, out float resolvedMinScale, out float resolvedMaxScale))
        {
            return;
        }

        Random.State previousRandomState = Random.state;
        PrepareRandomState();

        for (int i = 0; i < resolvedSpawnCount; i++)
        {
            SpawnBackdropAsteroid(resolvedMinScale, resolvedMaxScale);
        }

        Random.state = previousRandomState;
    }

    [ContextMenu("Clear Spawned Backdrop Asteroids")]
    public void ClearSpawnedBackdropAsteroids()
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

    [ContextMenu("Roll New Backdrop Seed")]
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

        if (!AreAllAxesPositive(outerSpawnBoxSize))
        {
            LogPlacementWarning("generation ignored because Outer Spawn Box Size must be greater than 0 on every axis.");
            return false;
        }

        if (!AreAllAxesPositive(innerExclusionBoxSize))
        {
            LogPlacementWarning("generation ignored because Inner Exclusion Box Size must be greater than 0 on every axis.");
            return false;
        }

        Vector3 paddedInnerSize = innerExclusionBoxSize + Vector3.one * Mathf.Max(0f, innerExclusionPadding * 2f);
        if (paddedInnerSize.x >= outerSpawnBoxSize.x
            || paddedInnerSize.y >= outerSpawnBoxSize.y
            || paddedInnerSize.z >= outerSpawnBoxSize.z)
        {
            LogPlacementWarning("generation ignored because the padded inner exclusion box is as large as or larger than the outer spawn box.");
            return false;
        }

        return true;
    }

    private void SpawnBackdropAsteroid(float resolvedMinScale, float resolvedMaxScale)
    {
        GameObject prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Count)];
        if (prefab == null)
        {
            LogPlacementWarning("skipped a null asteroid prefab entry.");
            return;
        }

        float uniformScale = Random.Range(resolvedMinScale, resolvedMaxScale);
        if (!TryGetRandomPositionInShell(out Vector3 candidatePosition))
        {
            LogPlacementWarning("skipped a backdrop asteroid because a valid shell position could not be found. Increase the outer box or reduce the inner exclusion volume.");
            return;
        }

        Quaternion candidateRotation = Random.rotationUniform;

        GameObject instance = Instantiate(prefab, candidatePosition, candidateRotation, ResolveSpawnedContainer());
        instance.transform.localScale = Vector3.one * uniformScale;
        instance.name = prefab.name;

        if (disableSpawnedColliders)
        {
            DisableAllColliders(instance);
        }
    }

    private bool TryGetRandomPositionInShell(out Vector3 worldPosition)
    {
        Vector3 outerHalfSize = outerSpawnBoxSize * 0.5f;
        Vector3 innerHalfSize = (innerExclusionBoxSize * 0.5f) + Vector3.one * Mathf.Max(0f, innerExclusionPadding);

        for (int attempt = 0; attempt < MaxShellSampleAttempts; attempt++)
        {
            Vector3 localOffset = new Vector3(
                Random.Range(-outerHalfSize.x, outerHalfSize.x),
                Random.Range(-outerHalfSize.y, outerHalfSize.y),
                Random.Range(-outerHalfSize.z, outerHalfSize.z));

            if (Mathf.Abs(localOffset.x) < innerHalfSize.x
                && Mathf.Abs(localOffset.y) < innerHalfSize.y
                && Mathf.Abs(localOffset.z) < innerHalfSize.z)
            {
                continue;
            }

            worldPosition = transform.TransformPoint(localOffset);
            return true;
        }

        worldPosition = transform.position;
        return false;
    }

    private void DisableAllColliders(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
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
        Transform container = containerObject.transform;
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

    private static bool AreAllAxesPositive(Vector3 size)
    {
        return size.x > 0f && size.y > 0f && size.z > 0f;
    }

    private void LogPlacementWarning(string message)
    {
        if (!logPlacementWarnings)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(AsteroidBackdropSpawner3D)}] {name} {message}", this);
    }
}
