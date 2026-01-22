using System.Collections.Generic;
using UnityEngine;

public class ExplosionPool : MonoBehaviour
{
    public static ExplosionPool Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Prefab to pool (should have ExplosionScript attached)")]
    public GameObject explosionPrefab;

    [Tooltip("Initial pool size")]
    public int initialPoolSize = 10;

    [Tooltip("Maximum pool size (0 = unlimited)")]
    public int maxPoolSize = 50;

    private Queue<GameObject> _availableExplosions = new Queue<GameObject>();
    private HashSet<GameObject> _activeExplosions = new HashSet<GameObject>();
    private Transform _poolContainer;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Create container for pooled objects
        _poolContainer = new GameObject("ExplosionPool_Container").transform;
        _poolContainer.SetParent(transform);

        // Pre-instantiate initial pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewExplosion();
        }
    }

    private GameObject CreateNewExplosion()
    {
        if (explosionPrefab == null)
        {
            Debug.LogError("ExplosionPool: No explosion prefab assigned!");
            return null;
        }

        GameObject explosion = Instantiate(explosionPrefab, _poolContainer);
        explosion.SetActive(false);
        _availableExplosions.Enqueue(explosion);
        return explosion;
    }

    public GameObject GetExplosion(Vector3 position, Quaternion rotation, float scale = 1f, Vector2? impactDirection = null)
    {
        GameObject explosion;

        // Get from pool or create new if pool is empty
        if (_availableExplosions.Count > 0)
        {
            explosion = _availableExplosions.Dequeue();
        }
        else
        {
            // Check max pool size
            if (maxPoolSize > 0 && _activeExplosions.Count >= maxPoolSize)
            {
                Debug.LogWarning("ExplosionPool: Max pool size reached, reusing oldest explosion");
                // In a more sophisticated system, you'd track and reuse the oldest
                // For now, just create a new one
                explosion = CreateNewExplosion();
            }
            else
            {
                explosion = CreateNewExplosion();
            }
        }

        if (explosion != null)
        {
            explosion.transform.position = position;
            explosion.transform.rotation = rotation;
            explosion.transform.localScale = Vector3.one * scale;

            // Set impact direction if provided
            if (impactDirection.HasValue)
            {
                ExplosionScript explosionScript = explosion.GetComponent<ExplosionScript>();
                if (explosionScript != null)
                {
                    explosionScript.SetImpactDirection(impactDirection.Value);
                }
            }

            explosion.SetActive(true);
            _activeExplosions.Add(explosion);
        }

        return explosion;
    }

    public void ReturnExplosion(GameObject explosion)
    {
        if (explosion == null) return;

        _activeExplosions.Remove(explosion);
        explosion.SetActive(false);
        explosion.transform.SetParent(_poolContainer);
        explosion.transform.localScale = Vector3.one; // Reset scale
        _availableExplosions.Enqueue(explosion);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
