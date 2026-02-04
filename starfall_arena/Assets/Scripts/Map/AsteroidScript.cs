using UnityEngine;

public class AsteroidScript : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 500f;
    private float currentHealth;

    [Header("Rotation Settings")]
    [Tooltip("The child GameObject with the visual mesh to rotate (not the collider parent)")]
    public GameObject visualObject;
    public float minRotationSpeed = 10f;   // minimum rotation speed for each axis
    public float maxRotationSpeed = 60f;   // maximum rotation speed for each axis

    [Header("Visual Effects")]
    [Tooltip("Particle system prefab to spawn when destroyed")]
    public GameObject explosionPrefab;
    [Tooltip("Adjusts the size of the explosion relative to the asteroid size")]
    public float explosionScaleMultiplier = 1.0f;

    [Header("Sound Effects")]
    [Tooltip("Explosion sound when destroyed")]
    public AudioClip explosionSound;
    [Range(0f, 3f)]
    [Tooltip("Volume for explosion sound")]
    public float explosionVolume = 1f;

    [Header("Collision Damage")]
    [Tooltip("Enable collision damage to players")]
    public bool enableCollisionDamage = true;
    [Tooltip("Minimum velocity magnitude required to deal damage")]
    public float minimumVelocityThreshold = 2f;
    [Tooltip("Damage multiplier per unit of velocity (ignores mass)")]
    public float damagePerVelocity = 5f;
    [Tooltip("Impact force multiplier for collision damage")]
    public float collisionImpactForce = 10f;
    [Tooltip("Cooldown in seconds between damage instances from the same asteroid")]
    public float collisionCooldown = 1.0f;
    [Tooltip("Sound played when asteroid impacts and damages a player")]
    public AudioClip impactSound;
    [Range(0f, 3f)]
    [Tooltip("Volume for impact sound")]
    public float impactVolume = 1f;

    [Header("Debug")]
    [Tooltip("Enable debug logging for collision and damage events")]
    public bool debugCollisionDamage = false;

    private Rigidbody2D _rb;
    private float parentZRotationSpeed;
    private float childYRotationSpeed;
    private float originalChildX;
    private float originalChildZ;
    private float currentChildY;
    private Vector2 _lastDamageDirection;
    private float _lastCollisionTime = -999f;

    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;

        // Get Rigidbody2D component for collision damage
        _rb = GetComponent<Rigidbody2D>();

        // Parent rotates on Z axis only (spinning in 2D plane)
        parentZRotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed) * (Random.value > 0.5f ? 1 : -1);

        // Child rotates on Y axis only (turning over effect)
        childYRotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed) * (Random.value > 0.5f ? 1 : -1);

        // Store child's original X and Z rotation values
        if (visualObject != null)
        {
            Vector3 childRot = visualObject.transform.localEulerAngles;
            originalChildX = childRot.x;
            originalChildZ = childRot.z;
            currentChildY = childRot.y;
        }
    }

    void Update()
    {
        // Rotate parent on Z axis (keeps collider aligned, spins in 2D plane)
        transform.Rotate(0f, 0f, parentZRotationSpeed * Time.deltaTime, Space.Self);

        // Rotate child on Y axis only, lock X and Z at original values
        if (visualObject != null)
        {
            currentChildY += childYRotationSpeed * Time.deltaTime;
            visualObject.transform.localEulerAngles = new Vector3(originalChildX, currentChildY, originalChildZ);
        }
    }

    public void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default)
    {
        // Calculate damage direction (from attacker position to this asteroid)
        // This way sparks always point away from whoever dealt the damage
        if (hitPoint != Vector3.zero)
        {
            _lastDamageDirection = ((Vector2)transform.position - (Vector2)hitPoint).normalized;
        }
        else
        {
            _lastDamageDirection = Vector2.zero; // No direction info available
        }

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            DestroyAsteroid();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if collision damage is enabled
        if (!enableCollisionDamage) return;

        // Check cooldown to prevent rapid successive hits
        if (Time.time - _lastCollisionTime < collisionCooldown)
        {
            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Collision blocked by cooldown. Time since last hit: {Time.time - _lastCollisionTime:F2}s");
            return;
        }

        // Check if we hit a player
        Player player = collision.gameObject.GetComponent<Player>();
        if (player == null) return;

        // Calculate velocity magnitude (ignores mass)
        if (_rb == null) return;

        Vector2 asteroidVelocity = _rb.linearVelocity;
        float velocity = asteroidVelocity.magnitude;

        // Only deal damage if velocity exceeds threshold
        if (velocity < minimumVelocityThreshold) return;

        // Get collision point for hit direction
        Vector3 collisionPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // Check if player was hit by the traveling side of the asteroid
        // Direction from asteroid center to collision point
        Vector2 toCollisionPoint = ((Vector2)collisionPoint - (Vector2)transform.position).normalized;
        // Direction the asteroid is traveling
        Vector2 velocityDirection = asteroidVelocity.normalized;

        // Dot product: positive means collision is on the front/traveling side
        float alignment = Vector2.Dot(velocityDirection, toCollisionPoint);

        // Only damage if hit from the traveling side (dot > 0 means collision point is in front)
        if (alignment <= 0f)
        {
            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Collision ignored - player hit non-traveling side. Alignment: {alignment:F2}");
            return;
        }

        // Calculate damage based on velocity only (mass-independent)
        float damage = velocity * damagePerVelocity;

        // DEBUG: Log damage details
        if (debugCollisionDamage)
            Debug.Log($"[Asteroid] HIT PLAYER! Velocity: {velocity:F1}, Damage: {damage:F1}");

        // Deal damage to the player
        player.TakeDamage(damage, collisionImpactForce, collisionPoint, DamageSource.Other);

        // Play impact sound
        if (impactSound != null)
        {
            Play2DAudioAtPoint(impactSound, collisionPoint, impactVolume);
        }

        // Apply knockback force to the player
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            // Calculate knockback direction (from asteroid to player)
            Vector2 knockbackDirection = ((Vector2)player.transform.position - (Vector2)collisionPoint).normalized;

            // Scale knockback by velocity for more dynamic impacts
            float knockbackMagnitude = collisionImpactForce * velocity;

            // Apply impulse force
            playerRb.AddForce(knockbackDirection * knockbackMagnitude, ForceMode2D.Impulse);

            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Applied knockback: Direction={knockbackDirection}, Magnitude={knockbackMagnitude:F1}");
        }

        // Update last collision time
        _lastCollisionTime = Time.time;
    }

    private void DestroyAsteroid()
    {
        // Play explosion sound (2D audio that survives GameObject destruction)
        if (explosionSound != null)
        {
            Play2DAudioAtPoint(explosionSound, transform.position, explosionVolume);
        }

        // Spawn explosion visual effect
        if (explosionPrefab != null)
        {
            // Use pool if available, otherwise fallback to instantiate
            if (ExplosionPool.Instance != null)
            {
                Vector2? impactDir = _lastDamageDirection != Vector2.zero ? _lastDamageDirection : (Vector2?)null;
                ExplosionPool.Instance.GetExplosion(transform.position, transform.rotation, explosionScaleMultiplier, impactDir);
            }
            else
            {
                GameObject explosion = Instantiate(explosionPrefab, transform.position, transform.rotation);
                explosion.transform.localScale = transform.localScale * explosionScaleMultiplier;

                // Set impact direction if we have it
                if (_lastDamageDirection != Vector2.zero)
                {
                    ExplosionScript explosionScript = explosion.GetComponent<ExplosionScript>();
                    if (explosionScript != null)
                    {
                        explosionScript.SetImpactDirection(_lastDamageDirection);
                    }
                }

                // Automatically destroy the explosion object after the particle effect finishes
                ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    Destroy(explosion, ps.main.duration + ps.main.startLifetime.constantMax);
                }
                else
                {
                    Destroy(explosion, 2f);
                }
            }
        }

        Destroy(gameObject);
    }

    // Helper method to play 3D spatial audio that survives GameObject destruction
    private static void Play2DAudioAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        // Check if AudioListener exists in scene
        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("No AudioListener found in scene! 3D spatial audio will not work correctly. Add an AudioListener component to your main camera.");
        }

        GameObject tempAudio = new GameObject("TempAudio_Explosion");
        tempAudio.transform.position = position;
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;

        // Configure 3D spatial audio
        audioSource.spatialBlend = 1f; // Full 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 10f; // Full volume within this distance
        audioSource.maxDistance = 50f; // Silent beyond this distance
        audioSource.dopplerLevel = 0f; // Disable doppler effect for explosions

        audioSource.Play();
        Object.Destroy(tempAudio, clip.length);
    }
}