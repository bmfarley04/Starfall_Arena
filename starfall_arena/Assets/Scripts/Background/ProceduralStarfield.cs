using UnityEngine;

/// <summary>
/// Generates a starfield using Unity's Particle System.
/// Fixed version that properly handles particle lifetime and display.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class ProceduralStarfield : MonoBehaviour
{
    [Header("Star Generation")]
    [SerializeField] private int starCount = 500;
    [SerializeField] private float spawnRadius = 50f;
    [SerializeField] private Vector2 starSizeRange = new Vector2(0.05f, 0.2f);
    
    [Header("Star Appearance")]
    [ColorUsage(true, true)]
    [SerializeField] private Color starColor = Color.white;
    [SerializeField] private bool randomizeBrightness = true;
    [Range(0.5f, 1f)]
    [SerializeField] private float minBrightness = 0.7f;
    
    [Header("Color Variation (Optional)")]
    [SerializeField] private bool useColorVariation = false;
    [ColorUsage(true, true)]
    [SerializeField] private Color[] starColors = new Color[] { Color.white, new Color(0.8f, 0.9f, 1f) };
    
    private ParticleSystem ps;
    private ParticleSystem.Particle[] stars;
    
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        ConfigureParticleSystem();
        GenerateStars();
    }
    
    void ConfigureParticleSystem()
    {
        // Main module settings - CRITICAL FIXES
        var main = ps.main;
        main.loop = true; // Changed to true
        main.playOnAwake = true; // Changed to true
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 999999f; // Very long lifetime instead of infinity
        main.startSpeed = 0f;
        main.startSize = 0.1f; // Default size (we'll override per particle)
        main.gravityModifier = 0f;
        
        // Emission - emit all at once
        var emission = ps.emission;
        emission.enabled = false; // We'll set particles manually
        
        // Shape module
        var shape = ps.shape;
        shape.enabled = false;
        
        // Disable velocity
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = false;
        
        // Disable size over lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;
        
        // Disable color over lifetime
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;
        
        // Renderer settings - IMPORTANT
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.minParticleSize = 0;
        renderer.maxParticleSize = 1000;
    }
    
    void GenerateStars()
    {
        stars = new ParticleSystem.Particle[starCount];
        
        for (int i = 0; i < starCount; i++)
        {
            // Random position within spawn radius
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            stars[i].position = new Vector3(randomPos.x, randomPos.y, 0);
            
            // Random size
            stars[i].startSize = Random.Range(starSizeRange.x, starSizeRange.y);
            
            // Color selection
            Color finalColor;
            if (useColorVariation && starColors.Length > 0)
            {
                finalColor = starColors[Random.Range(0, starColors.Length)];
            }
            else
            {
                finalColor = starColor;
            }
            
            // Apply brightness variation
            if (randomizeBrightness)
            {
                float brightness = Random.Range(minBrightness, 1f);
                finalColor = new Color(
                    finalColor.r * brightness,
                    finalColor.g * brightness,
                    finalColor.b * brightness,
                    finalColor.a
                );
            }
            
            stars[i].startColor = finalColor;
            
            // Set very long lifetime
            stars[i].remainingLifetime = 999999f;
            stars[i].startLifetime = 999999f;
            
            // No velocity
            stars[i].velocity = Vector3.zero;
        }
        
        // Apply all particles to the system
        ps.SetParticles(stars, stars.Length);
        
        // Make sure particle system is playing
        if (!ps.isPlaying)
        {
            ps.Play();
        }
        
        Debug.Log($"Generated {starCount} stars for {gameObject.name}");
    }
    
    // Optional: Regenerate stars at runtime
    [ContextMenu("Regenerate Stars")]
    public void RegenerateStars()
    {
        GenerateStars();
    }
}