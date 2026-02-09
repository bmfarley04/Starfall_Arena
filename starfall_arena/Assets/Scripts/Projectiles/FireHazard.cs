using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A fire hazard zone that damages entities that enter it.
/// Spawned by the FireWall ability to create a trail of fire behind the player.
/// </summary>
public class FireHazard : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Damage dealt per second to entities inside the hazard")]
    public float damagePerSecond = 10f;
    
    [Tooltip("Tag of entities that will take damage (e.g., 'Player1', 'Player2', 'Enemy')")]
    public string targetTag;
    
    [Tooltip("Impact force applied when dealing damage")]
    public float impactForce = 1f;

    [Header("Lifetime Settings")]
    [Tooltip("How long this fire hazard exists (seconds)")]
    public float lifetime = 3f;

    [Header("Visual Settings")]
    [Tooltip("Duration of fade out effect before destruction (seconds)")]
    public float fadeOutDuration = 0.5f;

    // Private state
    private float _spawnTime;
    private bool _isFadingOut = false;
    private HashSet<Entity> _entitiesInside = new HashSet<Entity>();
    private SpriteRenderer _spriteRenderer;
    private ParticleSystem _particleSystem;
    private float _originalAlpha;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _particleSystem = GetComponentInChildren<ParticleSystem>();
        
        if (_spriteRenderer != null)
        {
            _originalAlpha = _spriteRenderer.color.a;
        }
    }

    private void Start()
    {
        _spawnTime = Time.time;
    }

    /// <summary>
    /// Initialize the fire hazard with combat settings.
    /// </summary>
    public void Initialize(string enemyTag, float dps, float duration, float force)
    {
        targetTag = enemyTag;
        damagePerSecond = dps;
        lifetime = duration;
        impactForce = force;
        _spawnTime = Time.time;
    }

    private void Update()
    {
        float elapsed = Time.time - _spawnTime;
        float remainingTime = lifetime - elapsed;

        // Start fading out before destruction
        if (!_isFadingOut && remainingTime <= fadeOutDuration)
        {
            _isFadingOut = true;
            
            // Stop particle emission so particles can fade naturally
            if (_particleSystem != null)
            {
                var emission = _particleSystem.emission;
                emission.enabled = false;
            }
        }

        // Handle fade out
        if (_isFadingOut && _spriteRenderer != null)
        {
            float fadeProgress = 1f - (remainingTime / fadeOutDuration);
            fadeProgress = Mathf.Clamp01(fadeProgress);
            
            Color color = _spriteRenderer.color;
            color.a = Mathf.Lerp(_originalAlpha, 0f, fadeProgress);
            _spriteRenderer.color = color;
        }

        // Destroy when lifetime expires
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        // Apply damage to all entities currently inside the hazard
        float damageThisFrame = damagePerSecond * Time.fixedDeltaTime;
        
        // Create a copy to iterate since entities might be destroyed
        List<Entity> entitiesToDamage = new List<Entity>(_entitiesInside);
        
        foreach (Entity entity in entitiesToDamage)
        {
            if (entity != null)
            {
                entity.TakeDamage(damageThisFrame, impactForce, transform.position, DamageSource.Other);
            }
        }
        
        // Clean up null references
        _entitiesInside.RemoveWhere(e => e == null);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(targetTag)) return;
        
        if (other.CompareTag(targetTag))
        {
            Entity entity = other.GetComponent<Entity>();
            if (entity != null)
            {
                _entitiesInside.Add(entity);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(targetTag)) return;
        
        if (other.CompareTag(targetTag))
        {
            Entity entity = other.GetComponent<Entity>();
            if (entity != null)
            {
                _entitiesInside.Remove(entity);
            }
        }
    }
}
