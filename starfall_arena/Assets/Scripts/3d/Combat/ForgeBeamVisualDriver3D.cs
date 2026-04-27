using UnityEngine;

[DisallowMultipleComponent]
public class ForgeBeamVisualDriver3D : BeamVisualDriver3D
{
    [Header("Forge Beam Visuals")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform impactAnchor;
    [SerializeField] private Transform muzzleAnchor;
    [SerializeField] private float muzzleForwardOffset = 0.1f;
    [SerializeField] private float impactBackwardOffset = 0.5f;
    [SerializeField] private float textureScaleMultiplier = 0.05f;
    [SerializeField] private bool animateUv = true;
    [SerializeField] private float uvTime = -6f;
    [SerializeField] private string textureScaleProperty = "_BaseMap";
    [SerializeField] private string uvOffsetProperty = "_Offset";

    private ParticleSystem[] _particleSystems;
    private Renderer[] _renderers;
    private ParticleSystem[] _impactParticleSystems;
    private Renderer[] _impactRenderers;
    private float _initialUvOffset;
    private float _animateUvTime;
    private bool _impactVisible;

    private void Awake()
    {
        lineRenderer ??= GetComponent<LineRenderer>();
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _impactParticleSystems = impactAnchor != null ? impactAnchor.GetComponentsInChildren<ParticleSystem>(true) : null;
        _impactRenderers = impactAnchor != null ? impactAnchor.GetComponentsInChildren<Renderer>(true) : null;
        _initialUvOffset = Random.Range(0f, 5f);
        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace = true;
        }
        SetRenderersEnabled(false);
        SetImpactVisualsActive(false);
        StopParticles();
    }

    public override void BeginFiring()
    {
        SetRenderersEnabled(true);
        PlayParticles();
        SetImpactVisualsActive(false);
        _animateUvTime = 0f;
    }

    public override void EndFiring()
    {
        SetImpactVisualsActive(false);
        SetRenderersEnabled(false);
        StopParticles();
    }

    public override void UpdateBeamVisual(
        Vector3 origin,
        Vector3 aimDirection,
        float beamLength,
        bool hitSomething,
        Vector3 hitPoint,
        Vector3 hitNormal)
    {
        if (lineRenderer == null || aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 normalizedDirection = aimDirection.normalized;
        Vector3 endPoint = hitSomething
            ? hitPoint
            : origin + (normalizedDirection * beamLength);

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, endPoint);

        UpdateTextureScale(beamLength);
        UpdateTextureOffset();

        if (muzzleAnchor != null)
        {
            muzzleAnchor.position = origin + (normalizedDirection * muzzleForwardOffset);
            muzzleAnchor.rotation = Quaternion.LookRotation(normalizedDirection, ResolveUpVector(normalizedDirection));
        }

        if (impactAnchor != null)
        {
            if (hitSomething)
            {
                SetImpactVisualsActive(true);
                impactAnchor.position = hitPoint - (normalizedDirection * impactBackwardOffset);
                impactAnchor.rotation = Quaternion.LookRotation(
                    hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -normalizedDirection,
                    ResolveUpVector(hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -normalizedDirection));
            }
            else
            {
                SetImpactVisualsActive(false);
            }
        }
    }

    private void UpdateTextureScale(float beamLength)
    {
        if (lineRenderer == null)
        {
            return;
        }

        Material material = lineRenderer.material;
        if (material == null)
        {
            return;
        }

        float textureScale = Mathf.Max(0f, beamLength) * textureScaleMultiplier;
        if (material.HasProperty(textureScaleProperty))
        {
            material.SetTextureScale(textureScaleProperty, new Vector2(textureScale, 1f));
        }
    }

    private void UpdateTextureOffset()
    {
        if (!animateUv || lineRenderer == null)
        {
            return;
        }

        Material material = lineRenderer.material;
        if (material == null || !material.HasProperty(uvOffsetProperty))
        {
            return;
        }

        _animateUvTime += Time.deltaTime;
        if (_animateUvTime > 1f)
        {
            _animateUvTime = 0f;
        }

        float offset = (_animateUvTime * uvTime) + _initialUvOffset;
        material.SetVector(uvOffsetProperty, new Vector2(offset, 0f));
    }

    private void SetRenderersEnabled(bool isEnabled)
    {
        if (_renderers == null)
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].enabled = isEnabled;
            }
        }
    }

    private void PlayParticles()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = _particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void StopParticles()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            if (_particleSystems[i] != null)
            {
                _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void SetImpactVisualsActive(bool isActive)
    {
        if (_impactVisible == isActive)
        {
            return;
        }

        _impactVisible = isActive;

        if (_impactRenderers != null)
        {
            for (int i = 0; i < _impactRenderers.Length; i++)
            {
                if (_impactRenderers[i] != null)
                {
                    _impactRenderers[i].enabled = isActive;
                }
            }
        }

        if (_impactParticleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _impactParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = _impactParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            if (isActive)
            {
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
            else
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private static Vector3 ResolveUpVector(Vector3 aimDirection)
    {
        Vector3 normalizedDirection = aimDirection.normalized;
        if (Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.995f)
        {
            return Vector3.forward;
        }

        return Vector3.up;
    }
}
