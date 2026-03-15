using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// Networked asteroid with server-authoritative health and collision damage.
/// When a network session is active:
///   - Server:  owns health, processes damage and collisions, despawns on death
///   - Client:  receives explosion/impact VFX via ClientRpc, runs cosmetic rotation locally
///
/// When no network session is running (local multiplayer), all logic runs locally
/// as before — no RPCs or NetworkVariables are used.
/// </summary>
public class AsteroidScript : NetworkBehaviour
{
    // ===== CONFIGURATION =====

    [Header("Health Settings")]
    public float maxHealth = 500f;

    [Header("Rotation Settings")]
    [Tooltip("The child GameObject with the visual mesh to rotate (not the collider parent)")]
    public GameObject visualObject;
    public float minRotationSpeed = 10f;
    public float maxRotationSpeed = 60f;

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
    [Tooltip("Duration over which knockback is applied (helps overcome player movement resistance)")]
    public float knockbackDuration = 0.15f;
    [Tooltip("Sound played when asteroid impacts and damages a player")]
    public AudioClip impactSound;
    [Range(0f, 3f)]
    [Tooltip("Volume for impact sound")]
    public float impactVolume = 1f;

    [Header("Debug")]
    [Tooltip("Enable debug logging for collision and damage events")]
    public bool debugCollisionDamage = false;

    // ===== NETWORK STATE =====

    // Target scale set by MapManagerScript before Spawn(), applied in OnNetworkSpawn.
    // All clients read this to run the grow animation with the correct final size.
    private NetworkVariable<Vector3> _netTargetScale = new NetworkVariable<Vector3>(
        Vector3.one, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Staging field: holds the scale value between SetTargetScale() and OnNetworkSpawn(),
    // since NetworkVariables can't be written before Spawn().
    private Vector3 _pendingTargetScale = Vector3.one;

    // ===== RUNTIME STATE =====

    private float _currentHealth;
    private Rigidbody2D _rb;
    private Vector2 _lastDamageDirection;
    private float _lastCollisionTime = -999f;

    // ===== COSMETIC ROTATION (client-local, not synced) =====

    private float _parentZRotationSpeed;
    private float _childYRotationSpeed;
    private float _originalChildX;
    private float _originalChildZ;
    private float _currentChildY;

    // ===== LIFECYCLE =====

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();

        // Rotation is purely cosmetic — each client picks its own random speeds
        _parentZRotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed) * (Random.value > 0.5f ? 1 : -1);
        _childYRotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed) * (Random.value > 0.5f ? 1 : -1);

        if (visualObject != null)
        {
            Vector3 childRot = visualObject.transform.localEulerAngles;
            _originalChildX = childRot.x;
            _originalChildZ = childRot.z;
            _currentChildY = childRot.y;
        }

        // Non-networked: init health locally
        if (!NetMgr.IsNetworked)
        {
            _currentHealth = maxHealth;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _currentHealth = maxHealth;
            // Write the pending scale into the NetworkVariable now that the object is spawned.
            // This value is included in the spawn message sent to clients.
            _netTargetScale.Value = _pendingTargetScale;
        }

        // All clients run the grow animation using the server-set target scale
        StartCoroutine(GrowAsteroid(_netTargetScale.Value));
    }

    // ===== COSMETIC UPDATE =====

    void Update()
    {
        // Parent rotates on Z axis (spins in 2D plane, keeps collider aligned)
        transform.Rotate(0f, 0f, _parentZRotationSpeed * Time.deltaTime, Space.Self);

        // Child rotates on Y axis only (turning-over effect), X and Z locked
        if (visualObject != null)
        {
            _currentChildY += _childYRotationSpeed * Time.deltaTime;
            visualObject.transform.localEulerAngles = new Vector3(_originalChildX, _currentChildY, _originalChildZ);
        }
    }

    // ===== DAMAGE: PUBLIC API =====

    /// <summary>
    /// Entry point for all external damage (projectiles, beams, bombs).
    /// Routes through ServerRpc when called from a client in networked mode.
    /// </summary>
    public void RequestDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default)
    {
        if (!NetMgr.IsNetworked)
        {
            TakeDamage(damage, impactForce, hitPoint);
            return;
        }

        if (IsServer)
        {
            TakeDamage(damage, impactForce, hitPoint);
        }
        else
        {
            TakeDamageServerRpc(damage, impactForce, hitPoint);
        }
    }

    // ===== DAMAGE: SERVER-AUTHORITATIVE =====

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float damage, float impactForce, Vector3 hitPoint)
    {
        TakeDamage(damage, impactForce, hitPoint);
    }

    public void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default)
    {
        // Networked: only the server owns health
        if (NetMgr.IsNetworked && !IsServer) return;

        // Track damage direction so explosion sparks point away from the attacker
        if (hitPoint != Vector3.zero)
        {
            _lastDamageDirection = ((Vector2)transform.position - (Vector2)hitPoint).normalized;
        }
        else
        {
            _lastDamageDirection = Vector2.zero;
        }

        _currentHealth -= damage;

        if (_currentHealth <= 0)
        {
            DestroyAsteroid();
        }
    }

    // ===== COLLISION DAMAGE (server-only in networked mode) =====

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!enableCollisionDamage) return;
        if (NetMgr.IsNetworked && !IsServer) return;

        // Cooldown check
        if (Time.time - _lastCollisionTime < collisionCooldown)
        {
            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Collision blocked by cooldown. Time since last hit: {Time.time - _lastCollisionTime:F2}s");
            return;
        }

        Player player = collision.gameObject.GetComponent<Player>();
        if (player == null) return;
        if (_rb == null) return;

        Vector2 asteroidVelocity = _rb.linearVelocity;
        float velocity = asteroidVelocity.magnitude;

        // Velocity threshold gate
        if (velocity < minimumVelocityThreshold) return;

        Vector3 collisionPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;

        // Only damage if hit from the traveling side (dot > 0)
        Vector2 toCollisionPoint = ((Vector2)collisionPoint - (Vector2)transform.position).normalized;
        Vector2 velocityDirection = asteroidVelocity.normalized;
        float alignment = Vector2.Dot(velocityDirection, toCollisionPoint);

        if (alignment <= 0f)
        {
            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Collision ignored - player hit non-traveling side. Alignment: {alignment:F2}");
            return;
        }

        // Apply damage (velocity-based, mass-independent)
        float damage = velocity * damagePerVelocity;

        if (debugCollisionDamage)
            Debug.Log($"[Asteroid] HIT PLAYER! Velocity: {velocity:F1}, Damage: {damage:F1}");

        player.TakeDamage(damage, collisionImpactForce, collisionPoint, DamageSource.Other);

        // Impact sound — broadcast to all clients in networked mode
        if (impactSound != null)
        {
            if (NetMgr.IsNetworked)
                PlayImpactSoundClientRpc(collisionPoint);
            else
                Play2DAudioAtPoint(impactSound, collisionPoint, impactVolume);
        }

        // Knockback — applied over multiple frames to overcome player movement resistance
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Vector2 knockbackDirection = ((Vector2)player.transform.position - (Vector2)collisionPoint).normalized;
            float knockbackSpeed = (collisionImpactForce * velocity) / playerRb.mass;

            player.StartCoroutine(ApplyKnockbackOverTime(playerRb, knockbackDirection, knockbackSpeed, knockbackDuration, debugCollisionDamage));

            if (debugCollisionDamage)
                Debug.Log($"[Asteroid] Starting knockback coroutine: Direction={knockbackDirection}, TotalSpeed={knockbackSpeed:F1}, Duration={knockbackDuration:F2}s");
        }

        _lastCollisionTime = Time.time;
    }

    // ===== DESTRUCTION =====

    private void DestroyAsteroid()
    {
        if (NetMgr.IsNetworked)
        {
            // Broadcast explosion VFX to all clients, then despawn
            PlayDestructionEffectsClientRpc(transform.position, transform.rotation, explosionScaleMultiplier, _lastDamageDirection);
            NetworkObject.Despawn(true);
        }
        else
        {
            PlayDestructionEffectsLocal(transform.position, transform.rotation, explosionScaleMultiplier, _lastDamageDirection);
            Destroy(gameObject);
        }
    }

    // ===== CLIENT RPCs: VFX & AUDIO =====

    [ClientRpc]
    private void PlayDestructionEffectsClientRpc(Vector3 position, Quaternion rotation, float scaleMultiplier, Vector2 damageDirection)
    {
        PlayDestructionEffectsLocal(position, rotation, scaleMultiplier, damageDirection);
    }

    [ClientRpc]
    private void PlayImpactSoundClientRpc(Vector3 position)
    {
        if (impactSound != null)
        {
            Play2DAudioAtPoint(impactSound, position, impactVolume);
        }
    }

    // ===== LOCAL VFX =====

    private void PlayDestructionEffectsLocal(Vector3 position, Quaternion rotation, float scaleMultiplier, Vector2 damageDirection)
    {
        if (explosionSound != null)
        {
            Play2DAudioAtPoint(explosionSound, position, explosionVolume);
        }

        if (explosionPrefab != null)
        {
            if (ExplosionPool.Instance != null)
            {
                Vector2? impactDir = damageDirection != Vector2.zero ? damageDirection : (Vector2?)null;
                ExplosionPool.Instance.GetExplosion(position, rotation, scaleMultiplier, impactDir);
            }
            else
            {
                GameObject explosion = Instantiate(explosionPrefab, position, rotation);
                explosion.transform.localScale = Vector3.one * scaleMultiplier;

                if (damageDirection != Vector2.zero)
                {
                    ExplosionScript explosionScript = explosion.GetComponent<ExplosionScript>();
                    if (explosionScript != null)
                    {
                        explosionScript.SetImpactDirection(damageDirection);
                    }
                }

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
    }

    // ===== SCALE / GROW ANIMATION =====

    /// <summary>
    /// Called by MapManagerScript to set the target scale before NetworkObject.Spawn().
    /// The value is staged in _pendingTargetScale and written to the NetworkVariable
    /// in OnNetworkSpawn once the object is registered with NGO.
    /// </summary>
    public void SetTargetScale(Vector3 scale)
    {
        _pendingTargetScale = scale;

        // Non-networked: start grow immediately
        if (!NetMgr.IsNetworked)
        {
            StartCoroutine(GrowAsteroid(scale));
        }
    }

    private IEnumerator GrowAsteroid(Vector3 targetScale)
    {
        float growDuration = 0.5f;
        for (float t = 0f; t < growDuration; t += Time.deltaTime)
        {
            if (this == null) yield break;
            float eased = 1f - Mathf.Pow(1f - t / growDuration, 2f);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased);
            yield return null;
        }
        if (this != null) transform.localScale = targetScale;
    }

    // ===== HELPERS =====

    /// <summary>
    /// Plays a 2D audio clip via a temporary GameObject that survives the
    /// source asteroid being destroyed/despawned.
    /// </summary>
    private static void Play2DAudioAtPoint(AudioClip clip, Vector3 position, float volume)
    {
        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("No AudioListener found in scene! Audio will not work correctly. Add an AudioListener component to your main camera.");
        }

        GameObject tempAudio = new GameObject("TempAudio_Explosion");
        tempAudio.transform.position = position;
        AudioSource audioSource = tempAudio.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f; // 2D sound for split-screen
        audioSource.Play();
        Object.Destroy(tempAudio, clip.length);
    }

    /// <summary>
    /// Applies knockback velocity over multiple fixed-update frames to overcome
    /// player movement code that may reset velocity each frame.
    /// Runs on the Player's MonoBehaviour so it survives asteroid despawn.
    /// </summary>
    private static IEnumerator ApplyKnockbackOverTime(Rigidbody2D rb, Vector2 direction, float totalSpeed, float duration, bool debug)
    {
        if (rb == null || duration <= 0f) yield break;

        float elapsed = 0f;
        float speedPerSecond = totalSpeed / duration;

        while (elapsed < duration)
        {
            if (rb == null) yield break;

            rb.linearVelocity += direction * speedPerSecond * Time.fixedDeltaTime;
            elapsed += Time.fixedDeltaTime;

            if (debug && elapsed <= Time.fixedDeltaTime)
                Debug.Log($"[Asteroid Knockback] Frame velocity add: {direction * speedPerSecond * Time.fixedDeltaTime}, Current velocity: {rb.linearVelocity}");

            yield return new WaitForFixedUpdate();
        }

        if (debug)
            Debug.Log($"[Asteroid Knockback] Complete. Final velocity: {rb?.linearVelocity}");
    }
}
