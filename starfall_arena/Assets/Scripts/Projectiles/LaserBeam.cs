using NUnit.Framework.Internal;
using UnityEngine;

/// <summary>
/// Continuous laser beam weapon with particle effects
/// Reuses particle prefabs from projectile system
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LaserBeam : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [SerializeField] private Color beamColor = Color.cyan;

    [Header("Shield Effects")]
    [SerializeField] private float laserHitInterval = 0.2f;

    private bool _isFiring = false;
    private string _targetTag;
    private float _maxBeamDistance;
    private float _damagePerSecond;
    private float _recoilForcePerSecond;
    private float _impactForce;
    private ParticleSystem _activeMuzzleFlash;
    private ParticleSystem _activeImpactEffect;
    private float _timeSinceLastLaserHit = 0f;
    private ShieldController _currentTargetShield = null;
    private Entity _shooter;
    private bool _hasBeenReflected = false;

    void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
            
        // Start with beam disabled
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }

    
    /// <summary>
    /// Initialize the beam with target tag and weapon stats
    /// </summary>
    public void Initialize(string targetTag, float damagePerSecond, float maxBeamDistance, float recoilForcePerSecond, float impactForce, Entity shooter = null)
    {
        _targetTag = targetTag;
        _damagePerSecond = damagePerSecond;
        _maxBeamDistance = maxBeamDistance;
        _recoilForcePerSecond = recoilForcePerSecond;
        _impactForce = impactForce;
        _shooter = shooter;
    }

    public float GetRecoilForcePerSecond()
    {
        return _recoilForcePerSecond;
    }

    public Entity GetShooter()
    {
        return _shooter;
    }

    public float GetDamagePerSecond()
    {
        return _damagePerSecond;
    }

    public float GetImpactForce()
    {
        return _impactForce;
    }

    public bool HasBeenReflected()
    {
        return _hasBeenReflected;
    }

    public void MarkAsReflected()
    {
        _hasBeenReflected = true;
    }

    public void StartFiring()
    {
        _isFiring = true;
        if (lineRenderer != null)
            lineRenderer.enabled = true;

        // Spawn muzzle flash
        SpawnMuzzleFlash();
    }

    public void StopFiring()
    {
        _isFiring = false;
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        // Reset timer for next firing sequence
        _timeSinceLastLaserHit = 0f;
        // Clear current target so next firing gets instant ripple
        _currentTargetShield = null;

        // Clean up effects
        CleanupEffects();
    }

    void Update()
    {
        if (_isFiring)
        {
            FireBeam();
        }
    }

    private void FireBeam()
    {
        if (lineRenderer == null)
            return;

        // Use the beam's own transform for position and direction
        Vector2 fireDirection = transform.up;
        Vector2 startPosition = transform.position;

        // Raycast to find all objects the beam passes through
        RaycastHit2D[] hits = Physics2D.RaycastAll(startPosition, fireDirection, _maxBeamDistance);

        Vector2 endPosition;
        bool hitSomething = false;
        RaycastHit2D? validHit = null;

        // Find the first valid hit (asteroid or target tag)
        // Ignore everything else (friendly units, projectiles, etc.)
        if (hits.Length > 0)
        {
            // Sort hits by distance to find closest valid hit
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit2D hit in hits)
            {
                // Skip self
                if (hit.collider.gameObject == gameObject)
                    continue;

                // Check if we hit a player with active reflect shield
                Class1 player = hit.collider.GetComponent<Class1>();
                if (player != null)
                {
                    player.TryGetComponent<Reflector>(out var reflectScript);
                    if (reflectScript.IsAbilityActive())
                    {
                        // Trigger visual ripple effect
                        reflectScript.reflect.shield.OnReflectHit(hit.point);

                        // Stop the beam at the shield (no damage due to player immunity)
                        validHit = hit;
                        hitSomething = true;
                        break;
                    }
                }

                // Check if this is a valid target (stops the beam)
                bool isTargetTag = hit.collider.CompareTag(_targetTag);
                // TODO: Re-enable asteroid check when AsteroidScript is implemented
                // bool isAsteroid = hit.collider.CompareTag("Asteroid");

                if (isTargetTag) // || isAsteroid)
                {
                    validHit = hit;
                    hitSomething = true;
                    break; // Stop at first valid hit
                }
                // Otherwise, beam passes through (friendly units, projectiles, etc.)
            }
        }

        if (hitSomething && validHit.HasValue)
        {
            endPosition = validHit.Value.point;

            // Update or spawn impact effect at hit point
            UpdateImpactEffect(validHit.Value.point, fireDirection);

            // Deal damage if we hit a valid target
            if (validHit.Value.collider.CompareTag(_targetTag))
            {
                Entity damageable = validHit.Value.collider.GetComponent<Entity>();
                if (damageable != null)
                {
                    float damageThisFrame = _damagePerSecond * Time.deltaTime;
                    float impactForceThisFrame = _impactForce * Time.deltaTime;

                    // Apply damage with LaserBeam source to skip OnHit() and preserve shield alpha
                    damageable.TakeDamage(damageThisFrame, impactForceThisFrame, validHit.Value.point, DamageSource.LaserBeam);

                    // Apply impact force continuously (scaled by deltaTime for smooth application)
                    Rigidbody2D targetRb = validHit.Value.collider.GetComponent<Rigidbody2D>();
                    if (targetRb != null)
                    {
                        targetRb.AddForce(fireDirection * impactForceThisFrame, ForceMode2D.Impulse);
                    }

                    // Call OnLaserHit on shield at specified interval (only if shield is active)
                    ShieldController shieldController = damageable.shieldController;
                    if (shieldController != null && damageable.currentShield > 0)
                    {
                        // Trigger instant ripple on first hit of this shield
                        if (_currentTargetShield != shieldController)
                        {
                            shieldController.OnLaserHit(validHit.Value.point);
                            _currentTargetShield = shieldController;
                            _timeSinceLastLaserHit = 0f;
                        }
                        else
                        {
                            _timeSinceLastLaserHit += Time.deltaTime;
                            if (_timeSinceLastLaserHit >= laserHitInterval)
                            {
                                shieldController.OnLaserHit(validHit.Value.point);
                                _timeSinceLastLaserHit = 0f;
                            }
                        }
                    }
                }
            }
            // Deal damage if we hit an asteroid
            // TODO: Re-enable when AsteroidScript is implemented
            // else if (validHit.Value.collider.CompareTag("Asteroid"))
            // {
            //     AsteroidScript asteroid = validHit.Value.collider.GetComponent<AsteroidScript>();
            //     if (asteroid != null)
            //     {
            //         float damageThisFrame = _damagePerSecond * Time.deltaTime;
            //         float impactForceThisFrame = _impactForce * Time.deltaTime;
            //         asteroid.TakeDamage(damageThisFrame, impactForceThisFrame, validHit.Value.point);
            //     }
            // }
        }
        else
        {
            // Beam extends to max distance
            endPosition = startPosition + fireDirection * _maxBeamDistance;
            // Reset timer when not hitting anything
            _timeSinceLastLaserHit = 0f;
            // Clear current target so next shield gets instant ripple
            _currentTargetShield = null;
        }

        // Hide impact effect if not hitting anything
        if (!hitSomething && _activeImpactEffect != null)
        {
            _activeImpactEffect.gameObject.SetActive(false);
        }

        // Update line renderer positions
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
    }
    
    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null)
            return;
            
        // Spawn muzzle flash at beam origin
        _activeMuzzleFlash = Instantiate(muzzleFlashPrefab, transform.position, transform.rotation, transform);
        
        // Colorize to match beam
        var main = _activeMuzzleFlash.main;
        main.startColor = beamColor;
        
        // Make it loop for continuous effect
        main.loop = true;
    }
    
    private void UpdateImpactEffect(Vector3 hitPoint, Vector2 hitNormal)
    {
        if (impactEffectPrefab == null)
            return;
            
        // Create impact effect if it doesn't exist
        if (_activeImpactEffect == null)
        {
            _activeImpactEffect = Instantiate(impactEffectPrefab, hitPoint, Quaternion.identity);
            
            // Colorize to match beam
            var main = _activeImpactEffect.main;
            main.startColor = beamColor;
            
            // Make it loop for continuous effect
            main.loop = true;
        }
        
        // Make sure it's active
        if (!_activeImpactEffect.gameObject.activeSelf)
            _activeImpactEffect.gameObject.SetActive(true);
        
        // Update position to follow hit point
        _activeImpactEffect.transform.position = hitPoint;
        
        // Rotate to face hit normal
        float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
        _activeImpactEffect.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    
    private void CleanupEffects()
    {
        // Destroy muzzle flash
        if (_activeMuzzleFlash != null)
        {
            Destroy(_activeMuzzleFlash.gameObject);
            _activeMuzzleFlash = null;
        }
        
        // Destroy impact effect
        if (_activeImpactEffect != null)
        {
            Destroy(_activeImpactEffect.gameObject);
            _activeImpactEffect = null;
        }
    }
    
    void OnDestroy()
    {
        CleanupEffects();
    }
}