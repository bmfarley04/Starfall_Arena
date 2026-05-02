using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ForgeEnemyBeam3D : MonoBehaviour, IBeamRuntime3D, IBeamDirectionSource3D, IBeamAimConstraint3D
{
    private static readonly RaycastHit[] BeamHits = new RaycastHit[16];

    [Header("Forge Beam Visuals")]
    [Tooltip("Primary line renderer controlled by this runtime. Keep this assigned for single-line Forge beam prefabs.")]
    [SerializeField] private LineRenderer lineRenderer;
    [Tooltip("Extra line renderers that must follow the same gameplay ray. Use this for lightning prefabs with child or overlay line renderers.")]
    [SerializeField] private LineRenderer[] additionalLineRenderers = new LineRenderer[0];
    [Tooltip("If true, every child LineRenderer not under the impact or muzzle anchors is driven by this runtime. This prevents lightning prefabs from leaving secondary strands on stale local-forward data.")]
    [SerializeField] private bool autoRegisterChildLineRenderers = true;
    [Tooltip("If true, disables stock Forge F3DLightning components so they cannot run their own ray/line updates beside the 3D combat beam runtime.")]
    [SerializeField] private bool disableStockForgeLightning = true;
    [Tooltip("If true, controlled beam line renderers receive world-space points. This is safest for Forge lightning prefabs with child line-renderer transforms that are not guaranteed to match the beam root.")]
    [SerializeField] private bool driveLineRenderersInWorldSpace = true;
    [SerializeField] private Transform impactAnchor;
    [SerializeField] private Transform muzzleAnchor;
    [SerializeField] private float muzzleForwardOffset = 0.1f;
    [SerializeField] private float impactBackwardOffset = 0.05f;
    [SerializeField] private float textureScaleMultiplier = 0.05f;
    [SerializeField] private bool animateUv = true;
    [SerializeField] private float uvTime = -6f;
    [SerializeField] private string textureScaleProperty = "_BaseMap";
    [SerializeField] private string uvOffsetProperty = "_Offset";

    [Header("Beam Hit")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float hitscanRadius = 0.35f;
    [SerializeField] private float laserHitInterval = 0.2f;
    [SerializeField] private bool requireForwardAim = true;
    [SerializeField] private bool followAnchorTransform = true;

    [Header("Visual Smoothing")]
    [SerializeField] private bool smoothVisualEndpoint = true;
    [SerializeField] private float visualEndpointSmoothTime = 0.05f;
    [SerializeField] private float visualEndpointDeadzone = 0.35f;
    [SerializeField] private float visualEndpointSnapDistance = 10f;

    [Header("Lightning Shape")]
    [Tooltip("If enabled, the line renderer is rebuilt with jittered intermediate points instead of a single straight segment.")]
    [SerializeField] private bool useLightningJitter;
    [Tooltip("Total points in the line, including start and end. Values below 2 fall back to a straight two-point beam.")]
    [SerializeField] private int lightningPointCount = 2;
    [Tooltip("Maximum sideways offset applied to each intermediate lightning point, in local beam units.")]
    [SerializeField] private float lightningAmplitude = 0.25f;
    [Tooltip("Seconds between lightning shape randomization steps.")]
    [SerializeField] private float lightningJitterInterval = 0.05f;

    private readonly List<ParticleSystem> _beamParticles = new List<ParticleSystem>(8);
    private readonly List<Renderer> _beamRenderers = new List<Renderer>(8);
    private readonly List<LineRenderer> _controlledLineRenderers = new List<LineRenderer>(2);
    private ParticleSystem[] _impactParticleSystems;
    private Renderer[] _impactRenderers;
    private Vector3[] _lightningPoints;

    private bool _isFiring;
    private bool _impactVisible;
    private string _targetTag;
    private Faction3D _targetFaction;
    private float _maxDistance;
    private float _damagePerSecond;
    private float _recoilForcePerSecond;
    private float _impactForce;
    private Entity3D _shooter;
    private Transform _positionAnchor;
    private Transform _directionSource;
    private Camera _aimCamera;
    private bool _isCosmeticOnly;
    private NetCombat3D _networkAuthority;
    private bool _hasNetworkAim;
    private Vector3 _networkAimDirection;
    private bool _allowExplicitAimBehindForward;
    private int _accuracyAttackId = PlayerCombatStats3D.InvalidAttackId;
    private float _anchorOffset;
    private float _verticalOffset;
    private float _timeSinceLastShieldHit;
    private ShieldController _currentTargetShield;
    private float _initialUvOffset;
    private float _animateUvTime;
    private Vector3 _smoothedVisualEndpoint;
    private Vector3 _visualEndpointVelocity;
    private bool _hasSmoothedVisualEndpoint;
    private float _nextLightningJitterTime;
    private bool _isFixedEndpointCosmetic;
    private Transform _fixedEndpointStart;
    private Transform _fixedEndpointEnd;

    private void Awake()
    {
        lineRenderer ??= GetComponent<LineRenderer>();
        DisableStockForgeLightningComponents();
        CacheControlledLineRenderers();

        _impactParticleSystems = impactAnchor != null ? impactAnchor.GetComponentsInChildren<ParticleSystem>(true) : null;
        _impactRenderers = impactAnchor != null ? impactAnchor.GetComponentsInChildren<Renderer>(true) : null;
        CacheNonImpactVisuals();

        _initialUvOffset = Random.Range(0f, 5f);
        SetBeamVisualsActive(false);
        SetImpactVisualsActive(false);
    }

    public void Initialize(string targetTag, Faction3D targetFaction, float damagePerSecond, float maxDistance,
        float recoilForcePerSecond, float impactForce, Entity3D shooter,
        Transform positionAnchor = null, float anchorOffset = 0f, float verticalOffset = 0f, Camera aimCamera = null)
    {
        _targetTag = targetTag;
        _targetFaction = targetFaction;
        _damagePerSecond = damagePerSecond;
        _maxDistance = maxDistance;
        _recoilForcePerSecond = recoilForcePerSecond;
        _impactForce = impactForce;
        _shooter = shooter;
        _positionAnchor = positionAnchor != null ? positionAnchor : shooter.transform;
        _directionSource = positionAnchor != null ? positionAnchor : shooter.transform;
        _aimCamera = aimCamera;
        _anchorOffset = anchorOffset;
        _verticalOffset = verticalOffset;
        AttachToAnchorTransform();
    }

    public void SetBeamDirectionSource(Transform directionSource)
    {
        _directionSource = directionSource != null ? directionSource : _positionAnchor;
    }

    public void SetAllowExplicitAimBehindForward(bool allowExplicitAimBehindForward)
    {
        _allowExplicitAimBehindForward = allowExplicitAimBehindForward;
    }

    public void SetCosmeticOnly(bool isCosmeticOnly)
    {
        _isCosmeticOnly = isCosmeticOnly;
    }

    public void SetNetworkAuthority(NetCombat3D networkAuthority)
    {
        _networkAuthority = networkAuthority;
    }

    public void SetAccuracyAttackId(int attackId)
    {
        _accuracyAttackId = attackId;
    }

    public void SetNetworkAim(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _hasNetworkAim = true;
        _networkAimDirection = direction.normalized;
    }

    public void ClearNetworkAim()
    {
        _hasNetworkAim = false;
    }

    public float GetRecoilForcePerSecond()
    {
        return _recoilForcePerSecond;
    }

    public void StartFiring()
    {
        _isFiring = true;
        _animateUvTime = 0f;
        ResetVisualSmoothing();
        SetBeamVisualsActive(true);
        SetImpactVisualsActive(false);
    }

    public void StartCosmeticLink(Transform startAnchor, Transform endAnchor, int pointCount, float amplitude, float jitterInterval)
    {
        if (startAnchor == null || endAnchor == null)
        {
            return;
        }

        _fixedEndpointStart = startAnchor;
        _fixedEndpointEnd = endAnchor;
        _isFixedEndpointCosmetic = true;
        _isCosmeticOnly = true;
        _targetTag = null;
        _targetFaction = Faction3D.Neutral;
        _damagePerSecond = 0f;
        _impactForce = 0f;
        _recoilForcePerSecond = 0f;
        lightningPointCount = Mathf.Max(2, pointCount);
        lightningAmplitude = Mathf.Max(0f, amplitude);
        lightningJitterInterval = Mathf.Max(0.01f, jitterInterval);
        useLightningJitter = lightningPointCount > 2 && lightningAmplitude > 0f;
        transform.SetParent(null, true);
        StartFiring();
    }

    public void StopFiring()
    {
        _isFiring = false;
        _isFixedEndpointCosmetic = false;
        _fixedEndpointStart = null;
        _fixedEndpointEnd = null;
        ResetVisualSmoothing();
        SetImpactVisualsActive(false);
        SetBeamVisualsActive(false);
        _timeSinceLastShieldHit = 0f;
        _currentTargetShield = null;
    }

    private void Update()
    {
        if (!_isFiring)
        {
            return;
        }

        if (_isFixedEndpointCosmetic)
        {
            FireFixedEndpointCosmeticBeam();
        }
        else
        {
            FireBeam();
        }
    }

    private void FireFixedEndpointCosmeticBeam()
    {
        if (_fixedEndpointStart == null || _fixedEndpointEnd == null)
        {
            StopFiring();
            return;
        }

        Vector3 origin = _fixedEndpointStart.position;
        Vector3 endpoint = _fixedEndpointEnd.position;
        Vector3 span = endpoint - origin;
        float distance = span.magnitude;
        if (distance <= 0.001f)
        {
            SetImpactVisualsActive(false);
            return;
        }

        Vector3 visualDirection = span / distance;
        UpdateBeamVisuals(origin, visualDirection, distance, hitSomething: true, endpoint);
    }

    private void FireBeam()
    {
        Vector3 aimDirection = ResolveAimDirection();
        Vector3 origin = ResolveBeamOrigin(aimDirection);

        bool hitSomething = TryFindBeamHit(origin, aimDirection, out RaycastHit hit);

        float beamLength = _maxDistance;
        Vector3 actualEndpoint = origin + (aimDirection * _maxDistance);

        if (hitSomething)
        {
            beamLength = Mathf.Clamp(hit.distance, 0f, _maxDistance);
            actualEndpoint = hit.point;

            Entity3D damageable = ResolveHitEntity(hit.collider);
            if (damageable != null && IsMatchingTarget(damageable))
            {
                if (CanApplyGameplay())
                {
                    float damageThisFrame = _damagePerSecond * Time.deltaTime;
                    damageable.TakeDamage(damageThisFrame, hit.point, _shooter, DamageSource3D.Beam, _accuracyAttackId);

                    Rigidbody targetRb = hit.collider.attachedRigidbody;
                    if (targetRb != null && _impactForce > 0f)
                    {
                        Vector3 velocityDelta = aimDirection * (_impactForce * Time.deltaTime);
                        NetMovement3D netMovement = targetRb.GetComponent<NetMovement3D>();
                        if (netMovement != null)
                        {
                            netMovement.ApplyCombatVelocityDelta(velocityDelta);
                        }
                        else if (!targetRb.isKinematic)
                        {
                            targetRb.linearVelocity += velocityDelta;
                        }
                    }
                }

                UpdateShieldHitEffects(damageable, hit.point);
            }
            else
            {
                _timeSinceLastShieldHit = 0f;
                _currentTargetShield = null;
            }
        }
        else
        {
            _timeSinceLastShieldHit = 0f;
            _currentTargetShield = null;
        }

        Vector3 visualEndpoint = ResolveVisualEndpoint(actualEndpoint);
        Vector3 visualDirection = visualEndpoint - origin;
        if (visualDirection.sqrMagnitude <= 0.0001f)
        {
            visualDirection = aimDirection;
        }
        else
        {
            visualDirection = visualDirection.normalized;
        }

        float visualBeamLength = Vector3.Distance(origin, visualEndpoint);
        UpdateBeamVisuals(origin, visualDirection, visualBeamLength, hitSomething, visualEndpoint);
    }

    private void UpdateBeamVisuals(Vector3 origin, Vector3 visualDirection, float beamLength, bool hitSomething, Vector3 impactPoint)
    {
        transform.position = origin;
        transform.rotation = Quaternion.LookRotation(visualDirection, ResolveUpVector(visualDirection));

        UpdateLineRendererPoints(origin, visualDirection, beamLength);
        UpdateTextureScale(beamLength);
        UpdateTextureOffset();

        if (muzzleAnchor != null)
        {
            muzzleAnchor.localPosition = new Vector3(0f, 0f, muzzleForwardOffset);
            muzzleAnchor.localRotation = Quaternion.identity;
        }

        if (impactAnchor != null)
        {
            if (hitSomething)
            {
                SetImpactVisualsActive(true);
                impactAnchor.position = impactPoint - (transform.forward * Mathf.Max(0f, impactBackwardOffset));
                impactAnchor.rotation = Quaternion.LookRotation(-visualDirection, ResolveUpVector(-visualDirection));
            }
            else
            {
                SetImpactVisualsActive(false);
            }
        }
    }

    private void UpdateTextureScale(float beamLength)
    {
        float textureScale = Mathf.Max(0f, beamLength) * textureScaleMultiplier;
        for (int i = 0; i < _controlledLineRenderers.Count; i++)
        {
            LineRenderer renderer = _controlledLineRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material == null || !material.HasProperty(textureScaleProperty))
            {
                continue;
            }

            material.SetTextureScale(textureScaleProperty, new Vector2(textureScale, 1f));
        }
    }

    private void UpdateLineRendererPoints(Vector3 origin, Vector3 visualDirection, float beamLength)
    {
        if (_controlledLineRenderers.Count == 0)
        {
            return;
        }

        Vector3 normalizedDirection = visualDirection.sqrMagnitude > 0.0001f ? visualDirection.normalized : transform.forward;
        if (!useLightningJitter || lightningPointCount <= 2 || lightningAmplitude <= 0f)
        {
            for (int i = 0; i < _controlledLineRenderers.Count; i++)
            {
                LineRenderer renderer = _controlledLineRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.positionCount = 2;
                if (driveLineRenderersInWorldSpace)
                {
                    renderer.SetPosition(0, origin);
                    renderer.SetPosition(1, origin + normalizedDirection * beamLength);
                }
                else
                {
                    renderer.SetPosition(0, Vector3.zero);
                    renderer.SetPosition(1, new Vector3(0f, 0f, beamLength));
                }
            }
            return;
        }

        int pointCount = Mathf.Max(2, lightningPointCount);
        if (_lightningPoints == null || _lightningPoints.Length != pointCount)
        {
            _lightningPoints = new Vector3[pointCount];
            _nextLightningJitterTime = 0f;
        }

        bool shouldRandomize = Time.time >= _nextLightningJitterTime;
        if (shouldRandomize)
        {
            _nextLightningJitterTime = Time.time + Mathf.Max(0.01f, lightningJitterInterval);
        }

        _lightningPoints[0] = Vector3.zero;
        _lightningPoints[pointCount - 1] = new Vector3(0f, 0f, beamLength);
        float lastPointIndex = Mathf.Max(1f, pointCount - 1f);
        Vector3 beamRight = Vector3.Cross(ResolveUpVector(normalizedDirection), normalizedDirection).normalized;
        if (beamRight.sqrMagnitude <= 0.0001f)
        {
            beamRight = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;
        }

        Vector3 beamUp = Vector3.Cross(normalizedDirection, beamRight).normalized;
        if (shouldRandomize)
        {
            for (int i = 1; i < pointCount - 1; i++)
            {
                float z = beamLength * (i / lastPointIndex);
                float x = Random.Range(-lightningAmplitude, lightningAmplitude);
                float y = Random.Range(-lightningAmplitude, lightningAmplitude);
                _lightningPoints[i] = new Vector3(x, y, z);
            }
        }
        else
        {
            for (int i = 1; i < pointCount - 1; i++)
            {
                _lightningPoints[i].z = beamLength * (i / lastPointIndex);
            }
        }

        for (int i = 0; i < _controlledLineRenderers.Count; i++)
        {
            LineRenderer renderer = _controlledLineRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.positionCount = pointCount;
            if (driveLineRenderersInWorldSpace)
            {
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    Vector3 localPoint = _lightningPoints[pointIndex];
                    renderer.SetPosition(
                        pointIndex,
                        origin
                            + normalizedDirection * localPoint.z
                            + beamRight * localPoint.x
                            + beamUp * localPoint.y);
                }
            }
            else
            {
                renderer.SetPositions(_lightningPoints);
            }
        }
    }

    private void UpdateTextureOffset()
    {
        if (!animateUv || _controlledLineRenderers.Count == 0)
        {
            return;
        }

        _animateUvTime += Time.deltaTime;
        float offset = (_animateUvTime * uvTime) + _initialUvOffset;
        for (int i = 0; i < _controlledLineRenderers.Count; i++)
        {
            LineRenderer renderer = _controlledLineRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material == null || !material.HasProperty(uvOffsetProperty))
            {
                continue;
            }

            material.SetVector(uvOffsetProperty, new Vector2(offset, 0f));
        }
    }

    private void UpdateShieldHitEffects(Entity3D target, Vector3 hitPoint)
    {
        if (target.CurrentShield <= 0f)
        {
            _currentTargetShield = null;
            return;
        }

        ShieldController shieldController = target.GetComponentInChildren<ShieldController>(true);
        if (shieldController == null)
        {
            return;
        }

        if (_currentTargetShield != shieldController)
        {
            shieldController.OnHit(hitPoint);
            _currentTargetShield = shieldController;
            _timeSinceLastShieldHit = 0f;
        }
        else
        {
            _timeSinceLastShieldHit += Time.deltaTime;
            if (_timeSinceLastShieldHit >= laserHitInterval)
            {
                shieldController.OnHit(hitPoint);
                _timeSinceLastShieldHit = 0f;
            }
        }
    }

    private Entity3D ResolveHitEntity(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        Entity3D entity = hitCollider.GetComponent<Entity3D>();
        if (entity != null)
        {
            return entity;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            entity = hitCollider.attachedRigidbody.GetComponent<Entity3D>();
            if (entity != null)
            {
                return entity;
            }
        }

        return hitCollider.GetComponentInParent<Entity3D>();
    }

    private bool IsMatchingTarget(Entity3D entity)
    {
        if (entity == null)
        {
            return false;
        }

        if (_targetFaction != Faction3D.Neutral)
        {
            if (FactionMember3D.AreAllied(_shooter, entity))
            {
                return false;
            }

            Faction3D entityFaction = FactionMember3D.ResolveFaction(entity);
            if (entityFaction != Faction3D.Neutral)
            {
                return entityFaction == _targetFaction;
            }
        }

        return !string.IsNullOrEmpty(_targetTag) && entity.CompareTag(_targetTag);
    }

    private Vector3 ResolveAimDirection()
    {
        if (_hasNetworkAim)
        {
            return _allowExplicitAimBehindForward
                ? _networkAimDirection.normalized
                : ResolveForwardConstrainedDirection(_networkAimDirection);
        }

        Vector3 resolvedDirection = Vector3.zero;

        if (_aimCamera != null)
        {
            Ray centerRay = _aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (centerRay.direction.sqrMagnitude > 0.0001f)
            {
                resolvedDirection = centerRay.direction.normalized;
                return ResolveForwardConstrainedDirection(resolvedDirection);
            }
        }

        if (followAnchorTransform && _directionSource != null && _directionSource.forward.sqrMagnitude > 0.0001f)
        {
            return _directionSource.forward.normalized;
        }

        if (resolvedDirection.sqrMagnitude <= 0.0001f)
        {
            resolvedDirection = _directionSource != null && _directionSource.forward.sqrMagnitude > 0.0001f
                ? _directionSource.forward.normalized
                : transform.forward;
        }

        return ResolveForwardConstrainedDirection(resolvedDirection);
    }

    private Vector3 ResolveForwardConstrainedDirection(Vector3 resolvedDirection)
    {
        Vector3 forwardReference = _directionSource != null && _directionSource.forward.sqrMagnitude > 0.0001f
            ? _directionSource.forward.normalized
            : transform.forward.normalized;

        if (!requireForwardAim)
        {
            return resolvedDirection.sqrMagnitude > 0.0001f ? resolvedDirection.normalized : forwardReference;
        }

        Vector3 normalizedDirection = resolvedDirection.sqrMagnitude > 0.0001f
            ? resolvedDirection.normalized
            : forwardReference;

        return Vector3.Dot(forwardReference, normalizedDirection) > 0f
            ? normalizedDirection
            : forwardReference;
    }

    private bool CanApplyGameplay()
    {
        if (_isCosmeticOnly)
        {
            return false;
        }

        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _networkAuthority != null && _networkAuthority.IsServer;
    }

    private void CacheNonImpactVisuals()
    {
        _beamParticles.Clear();
        _beamRenderers.Clear();

        ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < allParticles.Length; i++)
        {
            ParticleSystem particleSystem = allParticles[i];
            if (particleSystem == null || (impactAnchor != null && particleSystem.transform.IsChildOf(impactAnchor)))
            {
                continue;
            }

            _beamParticles.Add(particleSystem);
        }

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null || (impactAnchor != null && renderer.transform.IsChildOf(impactAnchor)))
            {
                continue;
            }

            _beamRenderers.Add(renderer);
        }
    }

    private void SetBeamVisualsActive(bool isActive)
    {
        for (int i = 0; i < _beamRenderers.Count; i++)
        {
            if (_beamRenderers[i] != null)
            {
                _beamRenderers[i].enabled = isActive;
            }
        }

        for (int i = 0; i < _beamParticles.Count; i++)
        {
            ParticleSystem particleSystem = _beamParticles[i];
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

    private Vector3 ResolveVisualEndpoint(Vector3 targetEndpoint)
    {
        if (!smoothVisualEndpoint || visualEndpointSmoothTime <= 0f)
        {
            _smoothedVisualEndpoint = targetEndpoint;
            _visualEndpointVelocity = Vector3.zero;
            _hasSmoothedVisualEndpoint = true;
            return targetEndpoint;
        }

        if (!_hasSmoothedVisualEndpoint)
        {
            _smoothedVisualEndpoint = targetEndpoint;
            _visualEndpointVelocity = Vector3.zero;
            _hasSmoothedVisualEndpoint = true;
            return targetEndpoint;
        }

        float distance = Vector3.Distance(_smoothedVisualEndpoint, targetEndpoint);
        if (distance >= Mathf.Max(0f, visualEndpointSnapDistance))
        {
            _smoothedVisualEndpoint = targetEndpoint;
            _visualEndpointVelocity = Vector3.zero;
            return targetEndpoint;
        }

        if (distance <= Mathf.Max(0f, visualEndpointDeadzone))
        {
            return _smoothedVisualEndpoint;
        }

        _smoothedVisualEndpoint = Vector3.SmoothDamp(
            _smoothedVisualEndpoint,
            targetEndpoint,
            ref _visualEndpointVelocity,
            visualEndpointSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
        return _smoothedVisualEndpoint;
    }

    private void ResetVisualSmoothing()
    {
        _smoothedVisualEndpoint = Vector3.zero;
        _visualEndpointVelocity = Vector3.zero;
        _hasSmoothedVisualEndpoint = false;
    }

    private void AttachToAnchorTransform()
    {
        if (!followAnchorTransform || _positionAnchor == null)
        {
            return;
        }

        transform.SetParent(_positionAnchor, false);
        transform.localPosition = new Vector3(0f, _verticalOffset, _anchorOffset);
        transform.localRotation = Quaternion.identity;
    }

    private Vector3 ResolveBeamOrigin(Vector3 aimDirection)
    {
        if (followAnchorTransform)
        {
            return transform.position;
        }

        Vector3 normalizedDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;
        Transform originAnchor = _positionAnchor != null ? _positionAnchor : transform;
        return originAnchor.position + (normalizedDirection * _anchorOffset) + (originAnchor.up * _verticalOffset);
    }

    private bool TryFindBeamHit(Vector3 origin, Vector3 aimDirection, out RaycastHit nearestHit)
    {
        nearestHit = default;
        Vector3 normalizedDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;
        int hitCount = hitscanRadius > 0.001f
            ? Physics.SphereCastNonAlloc(origin, hitscanRadius, normalizedDirection, BeamHits, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, normalizedDirection, BeamHits, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        bool foundHit = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = BeamHits[i];
            Collider candidateCollider = candidate.collider;
            if (candidateCollider == null)
            {
                continue;
            }

            if (_shooter != null && candidateCollider.transform.IsChildOf(_shooter.transform))
            {
                continue;
            }

            if (candidate.distance < nearestDistance)
            {
                nearestDistance = candidate.distance;
                nearestHit = candidate;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private void CacheControlledLineRenderers()
    {
        _controlledLineRenderers.Clear();
        AddControlledLineRenderer(lineRenderer);

        if (additionalLineRenderers != null)
        {
            for (int i = 0; i < additionalLineRenderers.Length; i++)
            {
                AddControlledLineRenderer(additionalLineRenderers[i]);
            }
        }

        if (autoRegisterChildLineRenderers)
        {
            LineRenderer[] childLineRenderers = GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < childLineRenderers.Length; i++)
            {
                LineRenderer childRenderer = childLineRenderers[i];
                if (childRenderer == null || IsEffectAnchorRenderer(childRenderer.transform))
                {
                    continue;
                }

                AddControlledLineRenderer(childRenderer);
            }
        }

        for (int i = 0; i < _controlledLineRenderers.Count; i++)
        {
            LineRenderer renderer = _controlledLineRenderers[i];
            if (renderer != null)
            {
                renderer.useWorldSpace = driveLineRenderersInWorldSpace;
            }
        }
    }

    private bool IsEffectAnchorRenderer(Transform candidate)
    {
        return candidate != null
            && ((impactAnchor != null && candidate.IsChildOf(impactAnchor))
                || (muzzleAnchor != null && candidate.IsChildOf(muzzleAnchor)));
    }

    private void AddControlledLineRenderer(LineRenderer renderer)
    {
        if (renderer == null || _controlledLineRenderers.Contains(renderer))
        {
            return;
        }

        _controlledLineRenderers.Add(renderer);
    }

    private void DisableStockForgeLightningComponents()
    {
        if (!disableStockForgeLightning)
        {
            return;
        }

        FORGE3D.F3DLightning[] forgeLightnings = GetComponentsInChildren<FORGE3D.F3DLightning>(true);
        for (int i = 0; i < forgeLightnings.Length; i++)
        {
            if (forgeLightnings[i] != null)
            {
                forgeLightnings[i].enabled = false;
            }
        }
    }
}
