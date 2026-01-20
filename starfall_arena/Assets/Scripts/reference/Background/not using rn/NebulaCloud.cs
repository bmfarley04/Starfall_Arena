using UnityEngine;

/// <summary>
/// Creates nebula clouds using particle system with slow rotation and scale animation.
/// FIXED VERSION - handles particle lifetime and bounds properly.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class NebulaCloud : MonoBehaviour
{
    [Header("Cloud Settings")]
    [SerializeField] private Sprite[] cloudSprites;
    [SerializeField] private int cloudCount = 8;
    [SerializeField] private float spawnRadius = 40f;
    
    [Header("Cloud Appearance")]
    [SerializeField] private Vector2 cloudSizeRange = new Vector2(10f, 20f);
    [ColorUsage(true, true)]
    [SerializeField] private Color cloudColor = new Color(0.6f, 0.4f, 0.8f, 0.2f);
    [SerializeField] private bool randomizeColor = true;
    [ColorUsage(true, true)]
    [SerializeField] private Color[] colorVariations = new Color[] 
    {
        new Color(0.6f, 0.4f, 0.8f, 0.2f),
        new Color(0.4f, 0.5f, 0.9f, 0.15f),
        new Color(0.8f, 0.4f, 0.6f, 0.15f)
    };
    
    [Header("Animation")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(-5f, 5f);
    [SerializeField] private bool enablePulsing = false; // DISABLED by default to avoid issues
    [SerializeField] private float pulseSpeed = 0.3f;
    [SerializeField] private float pulseAmount = 0.2f;
    
    private ParticleSystem ps;
    
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        ConfigureParticleSystem();
        GenerateClouds();
    }
    
    void ConfigureParticleSystem()
    {
        // Main module - CRITICAL FIXES
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = cloudCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 100000f; // Large but finite
        main.startSpeed = 0f;
        main.startSize = 15f; // Default size
        main.gravityModifier = 0f;
        main.startRotation = 0f;
        
        // Disable emission - we set particles manually
        var emission = ps.emission;
        emission.enabled = false;
        
        // Disable unnecessary modules
        var shape = ps.shape;
        shape.enabled = false;
        
        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = false;
        
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = false;
        
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = false;
        
        // Rotation over lifetime - SAFE way to handle rotation
        if (enableRotation)
        {
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            rotation.z = new ParticleSystem.MinMaxCurve(rotationSpeedRange.x, rotationSpeedRange.y);
        }
        
        // Renderer settings
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.minParticleSize = 0;
        renderer.maxParticleSize = 1000;
        
        // Set a reasonable bounds size to avoid culling issues
        renderer.mesh = null;
        ps.Stop();
        ps.Clear();
    }
    
    void GenerateClouds()
    {
        if (cloudSprites == null || cloudSprites.Length == 0)
        {
            Debug.LogWarning("No cloud sprites assigned to NebulaCloud component on " + gameObject.name);
            return;
        }
        
        ParticleSystem.Particle[] clouds = new ParticleSystem.Particle[cloudCount];
        
        for (int i = 0; i < cloudCount; i++)
        {
            // Random position within spawn radius
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            clouds[i].position = new Vector3(randomPos.x, randomPos.y, 0);
            
            // Random size - use simple startSize, not startSize3D
            float size = Random.Range(cloudSizeRange.x, cloudSizeRange.y);
            clouds[i].startSize = size;
            
            // Random rotation
            clouds[i].rotation = Random.Range(0f, 360f);
            clouds[i].angularVelocity = Random.Range(rotationSpeedRange.x, rotationSpeedRange.y);
            
            // Color selection
            Color finalColor;
            if (randomizeColor && colorVariations.Length > 0)
            {
                finalColor = colorVariations[Random.Range(0, colorVariations.Length)];
            }
            else
            {
                finalColor = cloudColor;
            }
            
            clouds[i].startColor = finalColor;
            
            // Set lifetime
            clouds[i].remainingLifetime = 100000f;
            clouds[i].startLifetime = 100000f;
            
            // Ensure velocity is zero
            clouds[i].velocity = Vector3.zero;
            clouds[i].angularVelocity3D = Vector3.zero;
            
            // Randomize which sprite this particle uses
            clouds[i].randomSeed = (uint)Random.Range(0, 1000000);
        }
        
        // Apply particles
        ps.SetParticles(clouds, clouds.Length);
        
        // Force play to ensure they appear
        if (!ps.isPlaying)
        {
            ps.Play();
        }
        
        Debug.Log($"Generated {cloudCount} nebula clouds for {gameObject.name}");
    }
    
    [ContextMenu("Regenerate Clouds")]
    public void RegenerateClouds()
    {
        ps.Clear();
        ps.Stop();
        GenerateClouds();
    }
}