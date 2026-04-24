using System.Collections.Generic;
using Unity.Netcode;
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
    }

    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ShipSpeedEffects3DConfig speedEffects;

    private NetworkObject _networkObject;
    private readonly List<RuntimeTrailLayerState> _runtimeTrailLayers = new();
    private float _currentTrailIntensity;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        _networkObject = GetComponent<NetworkObject>();

        if (speedEffects.speedDustParticles != null)
        {
            var emission = speedEffects.speedDustParticles.emission;
            emission.rateOverTime = 0f;
            speedEffects.speedDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnEnable()
    {
        SyncLocalPresentationState();
    }

    private void OnDisable()
    {
        ClearLocalPresentationState();
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

        if (!ShouldDriveLocalPresentation())
        {
            ClearLocalPresentationState();
            return;
        }

        EnsureRuntimeTrails();
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
        SyncLocalPresentationState();
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

        if (normalizedDustEmission > 0.01f)
        {
            if (!speedEffects.speedDustParticles.isPlaying)
            {
                speedEffects.speedDustParticles.Play();
            }
        }
        else if (speedEffects.speedDustParticles.isPlaying)
        {
            speedEffects.speedDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
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

        for (int i = 0; i < _runtimeTrailLayers.Count; i++)
        {
            RuntimeTrailLayerState layer = _runtimeTrailLayers[i];
            if (layer.source == null || layer.driver == null || layer.trailRenderer == null)
            {
                continue;
            }

            float layerIntensity = Mathf.SmoothStep(0f, 1f, _currentTrailIntensity);
            layer.driver.SetPositionAndRotation(layer.source.position, layer.source.rotation);
            layer.trailRenderer.time = Mathf.Lerp(layer.config.minLifetime, layer.config.maxLifetime, layerIntensity);
            layer.trailRenderer.widthMultiplier = Mathf.Lerp(layer.config.minWidth, layer.config.maxWidth, layerIntensity);
            layer.trailRenderer.emitting = layerIntensity > 0.01f;
        }
    }

    private void SyncLocalPresentationState()
    {
        if (ShouldDriveLocalPresentation())
        {
            EnsureRuntimeTrails();
        }
        else
        {
            ClearLocalPresentationState();
        }
    }

    private bool ShouldDriveLocalPresentation()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _networkObject != null && _networkObject.IsSpawned && _networkObject.IsOwner;
    }

    private void EnsureRuntimeTrails()
    {
        if (_runtimeTrailLayers.Count > 0)
        {
            return;
        }

        RebuildRuntimeTrails();
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

            TryCreateTrailLayer(source, speedEffects.coreTrail, "Core");
            TryCreateTrailLayer(source, speedEffects.softTrail, "Soft");
        }
    }

    private void TryCreateTrailLayer(Transform source, ShipSpeedTrailLayer3DConfig config, string layerName)
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
            config = config
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

    private void ClearLocalPresentationState()
    {
        _currentTrailIntensity = 0f;

        if (speedEffects.speedDustParticles != null)
        {
            var emission = speedEffects.speedDustParticles.emission;
            emission.rateOverTime = 0f;
            if (speedEffects.speedDustParticles.isPlaying)
            {
                speedEffects.speedDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        for (int i = 0; i < _runtimeTrailLayers.Count; i++)
        {
            TrailRenderer trailRenderer = _runtimeTrailLayers[i].trailRenderer;
            if (trailRenderer == null)
            {
                continue;
            }

            trailRenderer.emitting = false;
            trailRenderer.Clear();
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
