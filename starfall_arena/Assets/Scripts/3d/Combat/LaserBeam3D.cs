using UnityEngine;

public class LaserBeam3D : MonoBehaviour, IBeamRuntime3D, IBeamDirectionSource3D, IBeamAimConstraint3D
{
    [Header("Beam Visual - Core")]
    [Tooltip("Inner beam mesh (capsule). Bright core of the beam.")]
    [SerializeField] private Transform beamCore;
    [Tooltip("Radius of the core beam")]
    [SerializeField] private float coreRadius = 0.15f;

    [Header("Beam Visual - Glow")]
    [Tooltip("Outer beam mesh (capsule). Larger, more transparent glow layer.")]
    [SerializeField] private Transform beamGlow;
    [Tooltip("Radius of the glow beam")]
    [SerializeField] private float glowRadius = 0.4f;

    [Header("Optional External Visual Driver")]
    [SerializeField] private BeamVisualDriver3D visualDriver;

    [Header("Effect Prefabs")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private GameObject muzzleEffectPrefab;

    [Header("Shield Effects")]
    [SerializeField] private float laserHitInterval = 0.2f;

    [Header("Collision")]
    [SerializeField] private float hitscanRadius = 1f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Aim Constraints")]
    [SerializeField] private bool requireForwardAim;

    [Header("Visual Smoothing")]
    [SerializeField] private bool smoothVisualEndpoint = true;
    [SerializeField] private float visualEndpointSmoothTime = 0.05f;
    [SerializeField] private float visualEndpointDeadzone = 0.35f;
    [SerializeField] private float visualEndpointSnapDistance = 10f;

    private bool _isFiring;
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

    private GameObject _impactInstance;
    private GameObject _muzzleInstance;
    private Vector3 _smoothedVisualEndpoint;
    private Vector3 _visualEndpointVelocity;
    private bool _hasSmoothedVisualEndpoint;

    void Awake()
    {
        visualDriver ??= GetComponent<BeamVisualDriver3D>();

        if (beamCore != null)
        {
            beamCore.gameObject.SetActive(false);
        }
        if (beamGlow != null)
        {
            beamGlow.gameObject.SetActive(false);
        }
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
        _directionSource = shooter.transform;
        _aimCamera = aimCamera;
        _anchorOffset = anchorOffset;
        _verticalOffset = verticalOffset;
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
        ResetVisualSmoothing();

        if (visualDriver != null)
        {
            visualDriver.BeginFiring();
        }
        else
        {
            if (beamCore != null)
            {
                beamCore.gameObject.SetActive(true);
            }
            if (beamGlow != null)
            {
                beamGlow.gameObject.SetActive(true);
            }

            if (muzzleEffectPrefab != null)
            {
                _muzzleInstance = Instantiate(muzzleEffectPrefab);
                _muzzleInstance.SetActive(true);
            }

            if (impactEffectPrefab != null)
            {
                _impactInstance = Instantiate(impactEffectPrefab);
                _impactInstance.SetActive(false);
            }
        }
    }

    public void StopFiring()
    {
        _isFiring = false;
        ResetVisualSmoothing();

        if (visualDriver != null)
        {
            visualDriver.EndFiring();
        }
        else
        {
            if (beamCore != null)
            {
                beamCore.gameObject.SetActive(false);
            }
            if (beamGlow != null)
            {
                beamGlow.gameObject.SetActive(false);
            }

            if (_muzzleInstance != null)
            {
                Destroy(_muzzleInstance);
                _muzzleInstance = null;
            }

            if (_impactInstance != null)
            {
                Destroy(_impactInstance);
                _impactInstance = null;
            }
        }

        _timeSinceLastShieldHit = 0f;
        _currentTargetShield = null;
    }

    void Update()
    {
        if (!_isFiring)
        {
            return;
        }

        FireBeam();
    }

    private void FireBeam()
    {
        Vector3 aimDirection = ResolveAimDirection();
        Vector3 origin = _positionAnchor.position + aimDirection * _anchorOffset + _positionAnchor.up * _verticalOffset;

        bool hitSomething = Physics.SphereCast(origin, Mathf.Max(0f, hitscanRadius), aimDirection, out RaycastHit hit, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore);

        // Filter: skip shooter and its children
        if (hitSomething && _shooter != null && hit.collider.transform.IsChildOf(_shooter.transform))
        {
            hitSomething = false;
        }

        float beamLength;
        Vector3 actualEndpoint;

        if (hitSomething)
        {
            beamLength = ResolveBeamLength(origin, aimDirection, hit);
            actualEndpoint = hit.point;

            Entity3D damageable = ResolveHitEntity(hit.collider);
            if (damageable != null && IsMatchingTarget(damageable))
            {
                if (CanApplyGameplay())
                {
                    float damageThisFrame = _damagePerSecond * Time.deltaTime;
                    if (_shooter != null)
                    {
                        damageThisFrame = _shooter.ModifyOutgoingDamage(damageThisFrame, damageable, DamageSource3D.Beam, _accuracyAttackId);
                    }

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

            if (_impactInstance != null)
            {
                _impactInstance.SetActive(true);
            }
        }
        else
        {
            beamLength = _maxDistance;
            actualEndpoint = origin + (aimDirection * beamLength);
            _timeSinceLastShieldHit = 0f;
            _currentTargetShield = null;

            if (_impactInstance != null)
            {
                _impactInstance.SetActive(false);
            }
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

        if (_impactInstance != null && hitSomething)
        {
            _impactInstance.transform.position = visualEndpoint;
            _impactInstance.transform.rotation = Quaternion.LookRotation(-visualDirection, ResolveStableUpVector(visualDirection));
        }

        if (visualDriver != null)
        {
            visualDriver.UpdateBeamVisual(
                origin,
                visualDirection,
                visualBeamLength,
                hitSomething,
                visualEndpoint,
                -visualDirection);
        }
        else
        {
            // Position and scale beam visuals
            // Capsule default is 2 units tall along its local Y axis.
            // We orient it so local Y points along aimDirection, then scale Y to match beam length.
            // Position offset = half the length along aimDirection so the base stays at origin.
            Quaternion beamRotation = Quaternion.LookRotation(visualDirection, ResolveStableUpVector(visualDirection))
                                    * Quaternion.Euler(90f, 0f, 0f); // rotate so capsule Y aligns with forward
            Vector3 beamCenter = origin + visualDirection * (visualBeamLength * 0.5f);
            float halfLength = visualBeamLength * 0.5f;

            if (beamCore != null)
            {
                beamCore.position = beamCenter;
                beamCore.rotation = beamRotation;
                float coreDiameter = coreRadius * 2f;
                beamCore.localScale = new Vector3(coreDiameter, halfLength, coreDiameter);
            }

            if (beamGlow != null)
            {
                beamGlow.position = beamCenter;
                beamGlow.rotation = beamRotation;
                float glowDiameter = glowRadius * 2f;
                beamGlow.localScale = new Vector3(glowDiameter, halfLength, glowDiameter);
            }

            // Position muzzle effect at beam origin
            if (_muzzleInstance != null)
            {
                _muzzleInstance.transform.position = origin;
                _muzzleInstance.transform.rotation = Quaternion.LookRotation(visualDirection, ResolveStableUpVector(visualDirection));
            }
        }
    }

    private float ResolveBeamLength(Vector3 origin, Vector3 aimDirection, RaycastHit hit)
    {
        // SphereCast distance stops when the cast volume first touches the collider surface.
        // That is shorter than the visible contact point when the beam uses a non-zero forgiving radius.
        float projectedHitDistance = Vector3.Dot(hit.point - origin, aimDirection);
        return Mathf.Clamp(Mathf.Max(hit.distance, projectedHitDistance), 0f, _maxDistance);
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
        Vector3 resolvedDirection = Vector3.zero;

        if (_hasNetworkAim)
        {
            return _allowExplicitAimBehindForward
                ? _networkAimDirection.normalized
                : ResolveForwardConstrainedDirection(_networkAimDirection);
        }
        else if (_aimCamera != null)
        {
            Ray centerRay = _aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (centerRay.direction.sqrMagnitude > 0.0001f)
            {
                resolvedDirection = centerRay.direction.normalized;
                return ResolveForwardConstrainedDirection(resolvedDirection);
            }
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
        if (!requireForwardAim)
        {
            return resolvedDirection.normalized;
        }

        Vector3 forwardReference = _directionSource != null && _directionSource.forward.sqrMagnitude > 0.0001f
            ? _directionSource.forward.normalized
            : transform.forward.normalized;

        Vector3 normalizedDirection = resolvedDirection.sqrMagnitude > 0.0001f
            ? resolvedDirection.normalized
            : forwardReference;

        return Vector3.Dot(forwardReference, normalizedDirection) > 0f
            ? normalizedDirection
            : forwardReference;
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

    private static Vector3 ResolveStableUpVector(Vector3 direction)
    {
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.995f)
        {
            return Vector3.forward;
        }

        return Vector3.up;
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
}
