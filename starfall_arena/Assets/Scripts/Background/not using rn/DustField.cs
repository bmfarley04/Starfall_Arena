using UnityEngine;

/// <summary>
/// Creates a field of very small, faint particles representing space dust.
/// Adds subtle texture to empty space areas.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class DustField : MonoBehaviour
{
    [Header("Dust Settings")]
    [SerializeField] private int dustParticleCount = 1500;
    [SerializeField] private float spawnRadius = 50f;
    
    [Header("Appearance")]
    [SerializeField] private Vector2 dustSizeRange = new Vector2(0.01f, 0.03f);
    [ColorUsage(true, true)]
    [SerializeField] private Color dustColor = new Color(0.8f, 0.8f, 0.9f, 0.15f); // Faint blue-gray
    [SerializeField] private bool randomizeBrightness = true;
    [Range(0.05f, 0.3f)]
    [SerializeField] private float minAlpha = 0.08f;
    [Range(0.1f, 0.5f)]
    [SerializeField] private float maxAlpha = 0.2f;
    
    [Header("Optional Movement")]
    [SerializeField] private bool enableDrift = false;
    [SerializeField] private Vector2 driftSpeedRange = new Vector2(-0.1f, 0.1f);
    
    private ParticleSystem ps;
    
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        ConfigureParticleSystem();
        GenerateDust();
    }
    
    void ConfigureParticleSystem()
    {
        // Main module
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = dustParticleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        
        // Disable emission
        var emission = ps.emission;
        emission.enabled = false;
        
        // Disable shape
        var shape = ps.shape;
        shape.enabled = false;
        
        // Optional: slight drift
        if (enableDrift)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(driftSpeedRange.x, driftSpeedRange.y);
            velocity.y = new ParticleSystem.MinMaxCurve(driftSpeedRange.x, driftSpeedRange.y);
        }
        
        // Renderer
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.minParticleSize = 0;
        renderer.maxParticleSize = 100;
    }
    
    void GenerateDust()
    {
        ParticleSystem.Particle[] dust = new ParticleSystem.Particle[dustParticleCount];
        
        for (int i = 0; i < dustParticleCount; i++)
        {
            // Random position
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            dust[i].position = new Vector3(randomPos.x, randomPos.y, 0);
            
            // Very small size
            dust[i].startSize = Random.Range(dustSizeRange.x, dustSizeRange.y);
            
            // Faint color with randomized alpha
            Color finalColor = dustColor;
            if (randomizeBrightness)
            {
                float alpha = Random.Range(minAlpha, maxAlpha);
                finalColor.a = alpha;
            }
            
            dust[i].startColor = finalColor;
            
            // Set lifetime
            dust[i].remainingLifetime = 100000f;
            dust[i].startLifetime = 100000f;
            
            // Initial velocity (if drift enabled)
            if (enableDrift)
            {
                dust[i].velocity = new Vector3(
                    Random.Range(driftSpeedRange.x, driftSpeedRange.y),
                    Random.Range(driftSpeedRange.x, driftSpeedRange.y),
                    0
                );
            }
            else
            {
                dust[i].velocity = Vector3.zero;
            }
        }
        
        ps.SetParticles(dust, dust.Length);
        
        if (!ps.isPlaying)
        {
            ps.Play();
        }
        
        Debug.Log($"Generated {dustParticleCount} dust particles for {gameObject.name}");
    }
    
    [ContextMenu("Regenerate Dust")]
    public void RegenerateDust()
    {
        ps.Clear();
        ps.Stop();
        GenerateDust();
    }
}