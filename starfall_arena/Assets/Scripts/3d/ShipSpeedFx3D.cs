using UnityEngine;

public class ShipSpeedFx3D : MonoBehaviour
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ShipSpeedEffects3DConfig speedEffects;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        if (speedEffects.speedDustParticles != null)
        {
            var emission = speedEffects.speedDustParticles.emission;
            emission.rateOverTime = 0f;
        }
    }

    private void Update()
    {
        if (shipFlight == null || speedEffects.speedDustParticles == null)
        {
            return;
        }

        float normalizedDustEmission = Mathf.InverseLerp(speedEffects.dustSpeedThreshold, 1f, shipFlight.ForwardSpeedNormalized);
        var emission = speedEffects.speedDustParticles.emission;
        emission.rateOverTime = normalizedDustEmission * speedEffects.maxDustEmissionRate;
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetSpeedEffects(ShipSpeedEffects3DConfig config)
    {
        speedEffects = config;
    }
}
