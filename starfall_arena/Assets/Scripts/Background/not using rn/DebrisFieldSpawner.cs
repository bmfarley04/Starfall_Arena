using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a debris field of ship parts to indicate distant combat.
/// Parts slowly drift and rotate indefinitely without fading.
/// Dynamically updates when parameters change in play mode.
/// </summary>
public class DebrisFieldSpawner : MonoBehaviour
{
    [Header("Ship Configuration")]
    [Tooltip("List of ship prefabs to explode (must have ShipPartScatter components on child parts)")]
    [SerializeField] private List<GameObject> shipPrefabs = new List<GameObject>();

    [Header("Spawn Area")]
    [Tooltip("Size of the area to spawn debris across")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(800f, 600f);

    [Tooltip("Visualize spawn area in scene view")]
    [SerializeField] private bool visualizeSpawnArea = true;

    [Header("Movement Settings")]
    [Tooltip("Minimum drift velocity (slow)")]
    [SerializeField] private float minDriftSpeed = 0.5f;

    [Tooltip("Maximum drift velocity (slow)")]
    [SerializeField] private float maxDriftSpeed = 2f;

    [Tooltip("Minimum angular velocity (deg/s)")]
    [SerializeField] private float minAngularVelocity = 10f;

    [Tooltip("Maximum angular velocity (deg/s)")]
    [SerializeField] private float maxAngularVelocity = 45f;

    [Tooltip("Cone angle around blast direction (±45 = 90° cone)")]
    [SerializeField] private float scatterConeAngle = 60f;

    [Header("Physics Settings")]
    [Tooltip("Rigidbody2D mass for debris pieces")]
    [SerializeField] private float debrisMass = 0.3f;

    [Tooltip("Linear drag (slows linear movement over time)")]
    [SerializeField] private float linearDrag = 0.1f;

    [Tooltip("Angular drag (slows rotation over time)")]
    [SerializeField] private float angularDrag = 0.05f;

    [Header("Scale Variation")]
    [Tooltip("Minimum scale multiplier for debris")]
    [SerializeField] private float minScale = 0.7f;

    [Tooltip("Maximum scale multiplier for debris")]
    [SerializeField] private float maxScale = 1.3f;

    [Header("Randomization")]
    [Tooltip("Seed for reproducible debris layouts (change to get different patterns)")]
    [SerializeField] private int randomSeed = 0;

    private GameObject debrisContainer;

    // Cached parameters for change detection
    private int lastShipCount;
    private Vector2 lastSpawnAreaSize;
    private float lastMinDriftSpeed;
    private float lastMaxDriftSpeed;
    private float lastMinAngularVelocity;
    private float lastMaxAngularVelocity;
    private float lastScatterConeAngle;
    private float lastDebrisMass;
    private float lastLinearDrag;
    private float lastAngularDrag;
    private float lastMinScale;
    private float lastMaxScale;
    private int lastRandomSeed;

    void Start()
    {
        SpawnDebrisField();
        CacheParameters();
    }

    void Update()
    {
        // Check if any parameters changed
        if (HasParametersChanged())
        {
            SpawnDebrisField();
            CacheParameters();
        }
    }

    void SpawnDebrisField()
    {
        // Clean up old debris
        if (debrisContainer != null)
        {
            if (Application.isPlaying)
            {
                Destroy(debrisContainer);
            }
            else
            {
                DestroyImmediate(debrisContainer);
            }
        }

        if (shipPrefabs == null || shipPrefabs.Count == 0)
        {
            Debug.LogWarning($"DebrisFieldSpawner on {gameObject.name}: No ship prefabs assigned");
            return;
        }

        // Set random seed for reproducible results
        Random.InitState(randomSeed);

        // Create container
        debrisContainer = new GameObject("Debris Field Container");
        debrisContainer.transform.SetParent(transform);
        debrisContainer.transform.localPosition = Vector3.zero;

        int totalDebrisSpawned = 0;

        // Explode each ship in the list
        foreach (GameObject shipPrefab in shipPrefabs)
        {
            if (shipPrefab == null) continue;

            // Random blast origin for this ship
            Vector2 blastOrigin = new Vector2(
                Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f)
            );

            // Random blast direction for this ship
            Vector2 blastDirection = Random.insideUnitCircle.normalized;

            // Find all ShipPartScatter components in the ship prefab
            ShipPartScatter[] parts = shipPrefab.GetComponentsInChildren<ShipPartScatter>(true);

            if (parts.Length == 0)
            {
                Debug.LogWarning($"Ship prefab {shipPrefab.name} has no ShipPartScatter components. Skipping.");
                continue;
            }

            // Spawn debris for each part
            foreach (ShipPartScatter part in parts)
            {
                CreateDebrisPiece(part.gameObject, blastOrigin, blastDirection);
                totalDebrisSpawned++;
            }
        }

        Debug.Log($"Spawned {totalDebrisSpawned} debris pieces from {shipPrefabs.Count} ships");
    }

    void CreateDebrisPiece(GameObject partPrefab, Vector2 blastOrigin, Vector2 blastDirection)
    {
        // Instantiate the ship part
        GameObject debris = Instantiate(partPrefab, debrisContainer.transform);

        // Calculate scatter direction with cone variation
        float randomAngle = Random.Range(-scatterConeAngle, scatterConeAngle);
        Vector2 scatterDir = Rotate(blastDirection, randomAngle);

        // Position: start at blast origin, then offset in scatter direction
        float distanceFromBlast = Random.Range(10f, 50f);
        Vector2 position = blastOrigin + scatterDir * distanceFromBlast;
        debris.transform.position = new Vector3(position.x, position.y, 0);

        // Random scale
        float scale = Random.Range(minScale, maxScale);
        debris.transform.localScale = partPrefab.transform.localScale * scale;

        // Random initial rotation
        debris.transform.rotation = Quaternion.Euler(
            Random.Range(0f, 360f),
            Random.Range(0f, 360f),
            Random.Range(0f, 360f)
        );

        // Detach from any parent hierarchy
        debris.transform.SetParent(debrisContainer.transform);

        // Add Rigidbody2D if not present
        Rigidbody2D rb = debris.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = debris.AddComponent<Rigidbody2D>();
        }

        rb.mass = debrisMass;
        rb.linearDamping = linearDrag;
        rb.angularDamping = angularDrag;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.None; // Allow full rotation

        // Set low drift velocity
        float driftSpeed = Random.Range(minDriftSpeed, maxDriftSpeed);
        rb.linearVelocity = scatterDir * driftSpeed;

        // Set low angular velocity (2D rotation only)
        float angularVel = Random.Range(minAngularVelocity, maxAngularVelocity);
        if (Random.value < 0.5f) angularVel *= -1f;
        rb.angularVelocity = angularVel;

        // Disable colliders (visual only, don't interact with gameplay)
        Collider2D[] colliders = debris.GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Remove ShipPartScatter component (we don't want the fade-out behavior)
        ShipPartScatter scatterComponent = debris.GetComponent<ShipPartScatter>();
        if (scatterComponent != null)
        {
            Destroy(scatterComponent);
        }

        // Add 3D rotation component for visual tumbling
        DebrisRotation rotationScript = debris.AddComponent<DebrisRotation>();
        rotationScript.SetRotationVelocity(
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1f : 1f),
            Random.Range(minAngularVelocity, maxAngularVelocity) * (Random.value < 0.5f ? -1f : 1f)
        );
    }

    private Vector2 Rotate(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    bool HasParametersChanged()
    {
        return shipPrefabs.Count != lastShipCount ||
               spawnAreaSize != lastSpawnAreaSize ||
               !Mathf.Approximately(minDriftSpeed, lastMinDriftSpeed) ||
               !Mathf.Approximately(maxDriftSpeed, lastMaxDriftSpeed) ||
               !Mathf.Approximately(minAngularVelocity, lastMinAngularVelocity) ||
               !Mathf.Approximately(maxAngularVelocity, lastMaxAngularVelocity) ||
               !Mathf.Approximately(scatterConeAngle, lastScatterConeAngle) ||
               !Mathf.Approximately(debrisMass, lastDebrisMass) ||
               !Mathf.Approximately(linearDrag, lastLinearDrag) ||
               !Mathf.Approximately(angularDrag, lastAngularDrag) ||
               !Mathf.Approximately(minScale, lastMinScale) ||
               !Mathf.Approximately(maxScale, lastMaxScale) ||
               randomSeed != lastRandomSeed;
    }

    void CacheParameters()
    {
        lastShipCount = shipPrefabs.Count;
        lastSpawnAreaSize = spawnAreaSize;
        lastMinDriftSpeed = minDriftSpeed;
        lastMaxDriftSpeed = maxDriftSpeed;
        lastMinAngularVelocity = minAngularVelocity;
        lastMaxAngularVelocity = maxAngularVelocity;
        lastScatterConeAngle = scatterConeAngle;
        lastDebrisMass = debrisMass;
        lastLinearDrag = linearDrag;
        lastAngularDrag = angularDrag;
        lastMinScale = minScale;
        lastMaxScale = maxScale;
        lastRandomSeed = randomSeed;
    }

    void OnDrawGizmosSelected()
    {
        if (visualizeSpawnArea)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0));
        }
    }
}

/// <summary>
/// Handles 3D rotation for debris pieces to create tumbling effect.
/// Separate from Rigidbody2D's 2D angular velocity.
/// </summary>
public class DebrisRotation : MonoBehaviour
{
    private Vector3 rotationVelocity;

    public void SetRotationVelocity(float x, float y, float z)
    {
        rotationVelocity = new Vector3(x, y, z);
    }

    void Update()
    {
        // Apply 3D rotation for visual tumbling
        transform.Rotate(rotationVelocity * Time.deltaTime, Space.World);
    }
}
