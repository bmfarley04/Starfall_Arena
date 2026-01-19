using UnityEngine;

/// <summary>
/// Spawns nebula prefabs across the map area with randomized variations.
/// Attach to a background object and it will populate nebulas on Awake.
/// </summary>
public class NebulaSpawner : MonoBehaviour
{
    [Header("Spawn Area")]
    [SerializeField] private float mapWidth = 600f;
    [SerializeField] private float mapHeight = 400f;
    [SerializeField] private Vector2 spawnCenter = Vector2.zero;

    [Header("Nebula Settings")]
    [SerializeField] private GameObject nebulaPrefab;
    [SerializeField] private int nebulaCount = 20;
    [SerializeField] private float minSpacing = 30f;

    [Header("Size Variation")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 2.0f);
    [SerializeField] private bool uniformScaling = true;

    [Header("Color Variation")]
    [SerializeField] private bool applyColorVariation = true;
    [ColorUsage(true, true)]
    [SerializeField] private Color[] colorVariations = new Color[]
    {
        new Color(0.6f, 0.4f, 0.8f, 0.2f),
        new Color(0.4f, 0.5f, 0.9f, 0.15f),
        new Color(0.8f, 0.4f, 0.6f, 0.15f),
        new Color(0.9f, 0.5f, 0.4f, 0.15f),
        new Color(0.4f, 0.8f, 0.6f, 0.15f)
    };

    [Header("Rotation Variation")]
    [SerializeField] private bool lockXRotation = true;
    [SerializeField] private float lockedXRotation = -90f;
    [SerializeField] private bool randomizeYRotation = false;
    [SerializeField] private bool randomizeZRotation = true;

    [Header("Debug")]
    [SerializeField] private bool showSpawnBounds = true;

    void Awake()
    {
        SpawnNebulas();
    }

    void SpawnNebulas()
    {
        if (nebulaPrefab == null)
        {
            Debug.LogWarning("NebulaSpawner: No nebula prefab assigned!");
            return;
        }

        Vector3[] spawnPositions = GenerateSpawnPositions();

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            GameObject nebula = Instantiate(nebulaPrefab, spawnPositions[i], Quaternion.identity, transform);
            nebula.name = $"Nebula_{i:00}";

            ApplyRandomization(nebula);
        }

        Debug.Log($"NebulaSpawner: Spawned {spawnPositions.Length} nebulas across {mapWidth}x{mapHeight} area");
    }

    Vector3[] GenerateSpawnPositions()
    {
        Vector3[] positions = new Vector3[nebulaCount];
        int attempts = 0;
        int maxAttemptsPerNebula = 30;
        int successfulSpawns = 0;

        for (int i = 0; i < nebulaCount && attempts < nebulaCount * maxAttemptsPerNebula; i++)
        {
            Vector3 candidatePos = GetRandomPositionInBounds();

            if (IsPositionValid(candidatePos, positions, successfulSpawns))
            {
                positions[successfulSpawns] = candidatePos;
                successfulSpawns++;
            }
            else
            {
                i--;
                attempts++;
            }
        }

        if (successfulSpawns < nebulaCount)
        {
            Debug.LogWarning($"NebulaSpawner: Could only spawn {successfulSpawns}/{nebulaCount} nebulas with spacing constraint of {minSpacing}");
            System.Array.Resize(ref positions, successfulSpawns);
        }

        return positions;
    }

    Vector3 GetRandomPositionInBounds()
    {
        float x = Random.Range(-mapWidth / 2f, mapWidth / 2f) + spawnCenter.x;
        float y = Random.Range(-mapHeight / 2f, mapHeight / 2f) + spawnCenter.y;
        float z = transform.position.z;

        return new Vector3(x, y, z);
    }

    bool IsPositionValid(Vector3 position, Vector3[] existingPositions, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float distance = Vector2.Distance(
                new Vector2(position.x, position.y),
                new Vector2(existingPositions[i].x, existingPositions[i].y)
            );

            if (distance < minSpacing)
            {
                return false;
            }
        }

        return true;
    }

    void ApplyRandomization(GameObject nebula)
    {
        float scale = Random.Range(scaleRange.x, scaleRange.y);

        if (uniformScaling)
        {
            nebula.transform.localScale = Vector3.one * scale;
        }
        else
        {
            float scaleX = Random.Range(scaleRange.x, scaleRange.y);
            float scaleY = Random.Range(scaleRange.x, scaleRange.y);
            float scaleZ = Random.Range(scaleRange.x, scaleRange.y);
            nebula.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
        }

        Vector3 rotation = Vector3.zero;

        if (lockXRotation)
        {
            rotation.x = lockedXRotation;
        }
        else
        {
            rotation.x = Random.Range(0f, 360f);
        }

        if (randomizeYRotation)
        {
            rotation.y = Random.Range(0f, 360f);
        }

        if (randomizeZRotation)
        {
            rotation.z = Random.Range(0f, 360f);
        }

        nebula.transform.rotation = Quaternion.Euler(rotation);

        if (applyColorVariation && colorVariations.Length > 0)
        {
            Color selectedColor = colorVariations[Random.Range(0, colorVariations.Length)];

            ParticleSystem ps = nebula.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = selectedColor;
            }

            SpriteRenderer sr = nebula.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = selectedColor;
            }

            MeshRenderer mr = nebula.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                mr.material.color = selectedColor;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!showSpawnBounds) return;

        Gizmos.color = Color.cyan;

        Vector3 center = new Vector3(spawnCenter.x, spawnCenter.y, transform.position.z);
        Vector3 size = new Vector3(mapWidth, mapHeight, 0.1f);

        Gizmos.DrawWireCube(center, size);
    }

    [ContextMenu("Clear All Nebulas")]
    public void ClearAllNebulas()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }

        Debug.Log("NebulaSpawner: Cleared all nebulas");
    }

    [ContextMenu("Respawn Nebulas")]
    public void RespawnNebulas()
    {
        ClearAllNebulas();
        SpawnNebulas();
    }
}
