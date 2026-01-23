using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ProjectileVisualController : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer projectileSprite;
    [SerializeField] private TrailRenderer trailRenderer;

    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem muzzleFlashPrefab;
    [SerializeField] private ParticleSystem impactEffectPrefab;

    
    [Header("Visual Settings")]
    [SerializeField] private Color projectileColor = Color.cyan;
    [SerializeField] private float glowIntensity = 2f;
    [SerializeField] private bool spawnMuzzleFlash = true;
    [SerializeField] private bool detachImpactEffect = true;
    
    private ParticleSystem _activeTrailParticles;
    private Material _projectileMaterial;
    
    void Awake()
    {
        SetupVisuals();
    }
    
    private void SetupVisuals()
    {
        // 1. Reset SpriteRenderer color to White so it acts as a neutral canvas
        // This prevents the "Orange * Cyan = Black" issue.
        if (projectileSprite != null)
        {
            projectileSprite.color = Color.white;

            // Create instance of material
            _projectileMaterial = new Material(projectileSprite.material);
            projectileSprite.material = _projectileMaterial;
            
            // 2. Calculate Color with HDR Intensity explicitly
            // We multiply RGB by intensity, but keep Alpha separate to avoid breaking transparency
            Color hdrColor = new Color(
                projectileColor.r * glowIntensity,
                projectileColor.g * glowIntensity,
                projectileColor.b * glowIntensity,
                projectileColor.a // Keep original alpha (usually 1)
            );

            _projectileMaterial.color = hdrColor;
        }
        
        // Setup trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.startColor = projectileColor;
            trailRenderer.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);
        }
    }
    
    public void OnProjectileSpawned()
    {
        // Spawn muzzle flash at spawn position
        if (spawnMuzzleFlash && muzzleFlashPrefab != null)
        {
            SpawnMuzzleFlash();
        }
    }
    
    public void OnProjectileImpact(Vector3 impactPosition, Vector2 impactDirection)
    {
        // Spawn impact effect
        if (impactEffectPrefab != null)
        {
            SpawnImpactEffect(impactPosition, impactDirection);
        }
        
        // Disable sprite and light immediately
        if (projectileSprite != null)
            projectileSprite.enabled = false;
        
        
        // Stop trail particles
        if (_activeTrailParticles != null)
        {
            var emission = _activeTrailParticles.emission;
            emission.enabled = false;
        }
    }
    
    public void OnProjectileDestroyed()
    {
        // Called when projectile is destroyed without hitting anything
        // Let trail particles fade out naturally if they exist
        if (_activeTrailParticles != null)
        {
            _activeTrailParticles.transform.SetParent(null);
            Destroy(_activeTrailParticles.gameObject, _activeTrailParticles.main.duration + _activeTrailParticles.main.startLifetime.constantMax);
        }
    }

    public void SetProjectileColor(Color newColor)
    {
        // Update projectile color (used for reflected projectiles)
        projectileColor = newColor;

        // Update sprite material
        if (_projectileMaterial != null)
        {
            _projectileMaterial.color = newColor * glowIntensity;
        }

        // Update trail renderer
        if (trailRenderer != null)
        {
            trailRenderer.startColor = newColor;
            trailRenderer.endColor = new Color(newColor.r, newColor.g, newColor.b, 0f);
        }

        // Update all particle systems
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = newColor;
        }

        // Update line renderers (if any)
        foreach (var line in GetComponentsInChildren<LineRenderer>())
        {
            line.startColor = newColor;
            line.endColor = newColor;
        }
    }

    public void ResetVisualState()
    {
        // Re-enable sprite renderer (in case it was disabled on impact)
        if (projectileSprite != null)
        {
            projectileSprite.enabled = true;
        }

        // Re-enable ALL renderers on this object and children (includes mesh, sprite, particles)
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
        }

        // Re-enable all particle systems that might be stopped
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps.isPlaying)
            {
                ps.Play();
            }
            var emission = ps.emission;
            emission.enabled = true;
        }

        // Re-enable trail particles if they exist
        if (_activeTrailParticles != null)
        {
            var emission = _activeTrailParticles.emission;
            emission.enabled = true;
        }
    }

    private void SpawnMuzzleFlash()
    {
        ParticleSystem muzzleFlash = Instantiate(muzzleFlashPrefab, transform.position, transform.rotation);
        
        // Colorize muzzle flash
        var main = muzzleFlash.main;
        main.startColor = projectileColor;
        
        // Auto-destroy after playing
        Destroy(muzzleFlash.gameObject, main.duration);
    }
    
    private void SpawnImpactEffect(Vector3 position, Vector2 direction)
    {
        // Calculate rotation to face impact direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        
        ParticleSystem impact = Instantiate(impactEffectPrefab, position, rotation);
        
        // Colorize impact effect
        var main = impact.main;
        main.startColor = projectileColor;
        
        // Detach from projectile if specified
        if (detachImpactEffect)
        {
            impact.transform.SetParent(null);
        }
        
        // Auto-destroy after playing
        Destroy(impact.gameObject, main.duration + main.startLifetime.constantMax);
    }
    
    void OnDestroy()
    {
        // Clean up instanced material
        if (_projectileMaterial != null)
        {
            Destroy(_projectileMaterial);
        }
    }
}