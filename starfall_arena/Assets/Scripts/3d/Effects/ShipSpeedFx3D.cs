using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShipSpeedFx3D : MonoBehaviour
{
    private sealed class RuntimeTrailLayerState
    {
        public Transform source;
        public Transform driver;
        public TrailRenderer trailRenderer;
        public ShipSpeedTrailLayer3DConfig config;
        public float noiseSeed;
    }

    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ShipSpeedEffects3DConfig speedEffects;

    private readonly List<RuntimeTrailLayerState> _runtimeTrailLayers = new();
    private float _currentTrailIntensity;

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

        RebuildRuntimeTrails();
    }

    private void OnDisable()
    {
        SetTrailEmissionEnabled(false);
    }

    private void OnDestroy()
    {
        ClearRuntimeTrails();
    }

    private void Update()
    {
        if (shipFlight == null)
        {
            return;
        }

        UpdateDust();
        UpdateWingTrails();
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetSpeedEffects(ShipSpeedEffects3DConfig config)
    {
        speedEffects = config;
        RebuildRuntimeTrails();
    }

    private void UpdateDust()
    {
        if (speedEffects.speedDustParticles == null)
        {
            return;
        }

        float normalizedDustEmission = Mathf.InverseLerp(speedEffects.dustSpeedThreshold, 1f, shipFlight.ForwardSpeedNormalized);
        var emission = speedEffects.speedDustParticles.emission;
        emission.rateOverTime = normalizedDustEmission * speedEffects.maxDustEmissionRate;
    }

    private void UpdateWingTrails()
    {
        if (_runtimeTrailLayers.Count == 0 || Time.deltaTime <= 0f)
        {
            return;
        }

        float targetIntensity = Mathf.InverseLerp(speedEffects.trailSpeedThreshold, 1f, shipFlight.ForwardSpeedNormalized);
        float rampStep = speedEffects.trailRampTime > 0f ? Time.deltaTime / speedEffects.trailRampTime : 1f;
        _currentTrailIntensity = Mathf.MoveTowards(_currentTrailIntensity, targetIntensity, rampStep);

        Camera effectCamera = GetEffectCamera();

        for (int i = 0; i < _runtimeTrailLayers.Count; i++)
        {
            RuntimeTrailLayerState layer = _runtimeTrailLayers[i];
            if (layer.source == null || layer.driver == null || layer.trailRenderer == null)
            {
                continue;
            }

            float layerIntensity = Mathf.SmoothStep(0f, 1f, _currentTrailIntensity);
            Vector3 sourcePosition = layer.source.position;

            if (effectCamera != null && layerIntensity > 0f && layer.config.cameraBias > 0f)
            {
                Vector3 toCamera = effectCamera.transform.position - sourcePosition;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    sourcePosition += toCamera.normalized * (layer.config.cameraBias * layerIntensity);
                }
            }

            if (layerIntensity > 0f && layer.config.jitterAmplitude > 0f && layer.config.jitterFrequency > 0f)
            {
                float noiseTime = Time.time * layer.config.jitterFrequency + layer.noiseSeed;
                Vector3 jitterDirection =
                    (layer.source.right * Mathf.Sin(noiseTime)) +
                    (layer.source.up * Mathf.Cos(noiseTime * 1.37f));
                sourcePosition += jitterDirection * (layer.config.jitterAmplitude * layerIntensity);
            }

            layer.driver.SetPositionAndRotation(sourcePosition, layer.source.rotation);
            layer.trailRenderer.time = Mathf.Lerp(layer.config.minLifetime, layer.config.maxLifetime, layerIntensity);
            layer.trailRenderer.widthMultiplier = Mathf.Lerp(layer.config.minWidth, layer.config.maxWidth, layerIntensity);
            layer.trailRenderer.emitting = layerIntensity > 0.01f;
        }
    }

    private void RebuildRuntimeTrails()
    {
        ClearRuntimeTrails();
        _currentTrailIntensity = 0f;

        if (speedEffects.wingTrailSources == null || speedEffects.wingTrailSources.Count == 0)
        {
            return;
        }

        for (int i = 0; i < speedEffects.wingTrailSources.Count; i++)
        {
            Transform source = speedEffects.wingTrailSources[i];
            if (source == null)
            {
                continue;
            }

            TryCreateTrailLayer(source, speedEffects.coreTrail, "Core", i);
            TryCreateTrailLayer(source, speedEffects.softTrail, "Soft", i + 1000);
        }
    }

    private void TryCreateTrailLayer(Transform source, ShipSpeedTrailLayer3DConfig config, string layerName, int seedOffset)
    {
        if (source == null || config.material == null)
        {
            return;
        }

        GameObject trailObject = new GameObject($"SpeedTrail_{layerName}_{source.name}");
        trailObject.transform.SetParent(transform, false);

        TrailRenderer trailRenderer = trailObject.AddComponent<TrailRenderer>();
        ConfigureTrailRenderer(trailRenderer, config);

        _runtimeTrailLayers.Add(new RuntimeTrailLayerState
        {
            source = source,
            driver = trailObject.transform,
            trailRenderer = trailRenderer,
            config = config,
            noiseSeed = seedOffset * 0.6180339f
        });
    }

    private static void ConfigureTrailRenderer(TrailRenderer trailRenderer, ShipSpeedTrailLayer3DConfig config)
    {
        trailRenderer.enabled = true;
        trailRenderer.emitting = false;
        trailRenderer.autodestruct = false;
        trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        trailRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        trailRenderer.generateLightingData = false;
        trailRenderer.material = config.material;
        trailRenderer.alignment = config.alignment;
        trailRenderer.textureMode = config.textureMode;
        trailRenderer.minVertexDistance = config.minVertexDistance > 0f ? config.minVertexDistance : 0.08f;
        trailRenderer.numCornerVertices = Mathf.Max(0, config.cornerVertices);
        trailRenderer.numCapVertices = Mathf.Max(0, config.endCapVertices);
        trailRenderer.widthCurve = CreateWidthCurve(config.widthCurve);
        trailRenderer.colorGradient = CreateColorGradient(config.colorGradient);
        trailRenderer.widthMultiplier = 0f;
        trailRenderer.time = 0f;
        trailRenderer.Clear();
    }

    private static AnimationCurve CreateWidthCurve(AnimationCurve configuredCurve)
    {
        if (configuredCurve != null && configuredCurve.length > 0)
        {
            return configuredCurve;
        }

        return new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.28f, 1f),
            new Keyframe(1f, 0f));
    }

    private static Gradient CreateColorGradient(Gradient configuredGradient)
    {
        if (configuredGradient != null)
        {
            return configuredGradient;
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.92f, 0.98f, 1f), 0f),
                new GradientColorKey(new Color(0.25f, 0.92f, 1f), 0.4f),
                new GradientColorKey(new Color(0.08f, 0.68f, 0.95f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.45f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private Camera GetEffectCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        return null;
    }

    private void SetTrailEmissionEnabled(bool enabled)
    {
        for (int i = 0; i < _runtimeTrailLayers.Count; i++)
        {
            TrailRenderer trailRenderer = _runtimeTrailLayers[i].trailRenderer;
            if (trailRenderer == null)
            {
                continue;
            }

            trailRenderer.emitting = enabled;
            if (!enabled)
            {
                trailRenderer.Clear();
            }
        }
    }

    private void ClearRuntimeTrails()
    {
        for (int i = 0; i < _runtimeTrailLayers.Count; i++)
        {
            RuntimeTrailLayerState layer = _runtimeTrailLayers[i];
            if (layer.driver == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(layer.driver.gameObject);
            }
            else
            {
                DestroyImmediate(layer.driver.gameObject);
            }
        }

        _runtimeTrailLayers.Clear();
    }
}
