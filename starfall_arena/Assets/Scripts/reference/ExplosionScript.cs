using UnityEngine;

public class ExplosionScript : MonoBehaviour
{
    [Header("Auto-Return Settings")]
    [Tooltip("Time before returning to pool. Set to 0 to use PS duration.")]
    public float lifetime = 2f;
    public bool useParticleSystemDuration = true;

    [Header("Directional Settings")]
    [Tooltip("Drag the specific child GameObject with the Sparks Particle System here.")]
    [SerializeField] private ParticleSystem sparksSystem;

    private float _timer;
    private ParticleSystem[] _particleSystems;
    private float _calculatedLifetime;
    private Quaternion _sparksBaseRotation;

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>();

        if (sparksSystem == null)
        {
             Debug.LogWarning($"Sparks System not assigned on {gameObject.name}. Directional sparks won't work.");
        }
        else
        {
            // Store the base local rotation from the prefab (e.g., 0, 90, 90)
            _sparksBaseRotation = sparksSystem.transform.localRotation;
        }

        // Calculate lifetime based on the longest system
        if (useParticleSystemDuration && _particleSystems.Length > 0)
        {
            _calculatedLifetime = 0f;
            foreach (var ps in _particleSystems)
            {
                // Ensure we look at the main module of each system
                var main = ps.main;
                // Use constantMax to account for variable lifetimes
                float duration = main.duration + main.startLifetime.constantMax;
                if (duration > _calculatedLifetime)
                {
                    _calculatedLifetime = duration;
                }
            }
        }
        else
        {
            _calculatedLifetime = lifetime;
        }
    }

    private void OnEnable()
    {
        _timer = 0f;

        // Reset rotation to base prefab rotation (not identity!)
        if (sparksSystem != null)
        {
             sparksSystem.transform.localRotation = _sparksBaseRotation;
        }

        // Restart all particle systems
        foreach (var ps in _particleSystems)
        {
            ps.Clear();
            ps.Play();
        }
    }

    /// <summary>
    /// Orients the sparks system to point along the provided direction vector.
    /// This works by rotating around the Z-axis in 2D space while preserving
    /// the base rotation needed for the particle system to work correctly.
    /// </summary>
    /// <param name="direction">The direction vector for sparks to travel.</param>
    public void SetImpactDirection(Vector2 direction)
    {
        if (sparksSystem == null) return;

        // Calculate angle from the positive X-axis (Right) in 2D
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Apply Z-axis rotation to parent (the explosion root), not the sparks directly
        // This way the base rotation (0, 90, 90) is preserved
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= _calculatedLifetime)
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (ExplosionPool.Instance != null)
        {
            ExplosionPool.Instance.ReturnExplosion(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}