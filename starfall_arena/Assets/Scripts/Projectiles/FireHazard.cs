using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

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

    [Header("Movement Settings")]
    [Tooltip("Multiplier controlling how quickly the hazard slows over its lifetime (1 = stop by end, <1 slower, >1 faster)")]
    public float velocitySlowRate = 1f;

    [Header("Audio")]
    [Tooltip("Looping sound played for the hazard lifetime (optional)")]
    public SoundEffect loopSound;

    [HideInInspector]
    public bool disableVelocityDampening = false;

    // Private state
    private float _spawnTime;
    private bool _isFadingOut = false;
    private HashSet<Entity> _entitiesInside = new HashSet<Entity>();
    private SpriteRenderer _spriteRenderer;
    private ParticleSystem _particleSystem;
    private float _originalAlpha;
    private Rigidbody2D _rb;
    private Vector2 _initialVelocity;
    private bool _capturedInitialVelocity = false;
    private AudioSource _loopSource;
    private bool _loopStarted = false;
    private Entity _owner;
    private bool _isCosmeticOnly = false;

    private void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _particleSystem = GetComponentInChildren<ParticleSystem>();
        _rb = GetComponent<Rigidbody2D>();
        
        if (_spriteRenderer != null)
        {
            _originalAlpha = _spriteRenderer.color.a;
        }
    }

    private void Start()
    {
        _spawnTime = Time.time;
        CaptureInitialVelocity();
        StartLoopSound();
    }

    /// <summary>
    /// Initialize the fire hazard with combat settings.
    /// </summary>
    public void Initialize(string enemyTag, float dps, float duration, float force)
    {
        InitializeCore(enemyTag, dps, duration, force);
    }

    /// <summary>
    /// Initialize with a custom velocity slow rate.
    /// </summary>
    public void Initialize(string enemyTag, float dps, float duration, float force, float slowRate)
    {
        velocitySlowRate = slowRate;
        InitializeCore(enemyTag, dps, duration, force);
    }

    /// <summary>
    /// Initialize with an owning entity reference for damage attribution.
    /// </summary>
    public void Initialize(string enemyTag, float dps, float duration, float force, Entity owner)
    {
        _owner = owner;
        InitializeCore(enemyTag, dps, duration, force);
    }

    private void InitializeCore(string enemyTag, float dps, float duration, float force)
    {
        targetTag = enemyTag;
        damagePerSecond = dps;
        lifetime = duration;
        impactForce = force;
        _spawnTime = Time.time;
        _capturedInitialVelocity = false;
        CaptureInitialVelocity();
        StartLoopSound();
    }

    public void SetCosmeticOnly(bool isCosmeticOnly)
    {
        _isCosmeticOnly = isCosmeticOnly;
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
        ApplyVelocityDampening();

        if (!CanApplyGameplay())
        {
            return;
        }

        // Apply damage to all entities currently inside the hazard
        float damageThisFrame = damagePerSecond * Time.fixedDeltaTime;
        
        // Create a copy to iterate since entities might be destroyed
        List<Entity> entitiesToDamage = new List<Entity>(_entitiesInside);
        
        foreach (Entity entity in entitiesToDamage)
        {
            if (entity != null)
            {
                entity.TakeDamage(damageThisFrame, impactForce, transform.position, DamageSource.Other, _owner, Player.InvalidAttackId);
            }
        }
        
        // Clean up null references
        _entitiesInside.RemoveWhere(e => e == null);
    }

    private void ApplyVelocityDampening()
    {
        if (disableVelocityDampening)
        {
            return;
        }
        if (_rb == null || lifetime <= 0f)
        {
            return;
        }

        float elapsed = Time.time - _spawnTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        float initialSpeed = _initialVelocity.magnitude;

        if (initialSpeed <= 0f)
        {
            return;
        }

        float slowRate = Mathf.Max(velocitySlowRate, 0f);
        if (slowRate <= 0f)
        {
            return;
        }

        float targetSpeed = Mathf.Lerp(initialSpeed, 0f, Mathf.Clamp01(t * slowRate));
        Vector2 currentDirection;

        if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            currentDirection = _rb.linearVelocity.normalized;
        }
        else if (_initialVelocity.sqrMagnitude > 0f)
        {
            currentDirection = _initialVelocity.normalized;
        }
        else
        {
            currentDirection = Vector2.zero;
        }

        _rb.linearVelocity = currentDirection * targetSpeed;
    }

    private void CaptureInitialVelocity()
    {
        if (_rb == null || _capturedInitialVelocity)
        {
            return;
        }

        _initialVelocity = _rb.linearVelocity;
        _capturedInitialVelocity = true;
    }

    public void SetLoopSound(SoundEffect sound)
    {
        loopSound = sound;
        _loopStarted = false;
        StartLoopSound();
    }

    private void StartLoopSound()
    {
        if (_loopStarted || loopSound == null || loopSound.clip == null)
        {
            return;
        }

        if (_loopSource == null)
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.spatialBlend = 0f;
        }

        _loopSource.clip = loopSound.clip;
        _loopSource.volume = loopSound.volume;
        _loopSource.pitch = Random.Range(loopSound.minPitch, loopSound.maxPitch);
        _loopSource.Play();
        _loopStarted = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanApplyGameplay())
        {
            return;
        }

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
        if (!CanApplyGameplay())
        {
            return;
        }

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

    private bool CanApplyGameplay()
    {
        if (_isCosmeticOnly)
        {
            return false;
        }

        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
    }
}
