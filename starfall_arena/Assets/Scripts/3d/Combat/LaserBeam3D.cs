using UnityEngine;

public class LaserBeam3D : MonoBehaviour
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

    [Header("Effect Prefabs")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private GameObject muzzleEffectPrefab;

    [Header("Shield Effects")]
    [SerializeField] private float laserHitInterval = 0.2f;

    [Header("Collision")]
    [SerializeField] private float hitscanRadius = 1f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private bool _isFiring;
    private string _targetTag;
    private float _maxDistance;
    private float _damagePerSecond;
    private float _recoilForcePerSecond;
    private float _impactForce;
    private Entity3D _shooter;
    private Transform _positionAnchor;
    private Transform _directionSource;
    private float _anchorOffset;
    private float _verticalOffset;
    private float _timeSinceLastShieldHit;
    private ShieldController _currentTargetShield;

    private GameObject _impactInstance;
    private GameObject _muzzleInstance;

    void Awake()
    {
        if (beamCore != null)
        {
            beamCore.gameObject.SetActive(false);
        }
        if (beamGlow != null)
        {
            beamGlow.gameObject.SetActive(false);
        }
    }

    public void Initialize(string targetTag, float damagePerSecond, float maxDistance,
        float recoilForcePerSecond, float impactForce, Entity3D shooter,
        Transform positionAnchor = null, float anchorOffset = 0f, float verticalOffset = 0f)
    {
        _targetTag = targetTag;
        _damagePerSecond = damagePerSecond;
        _maxDistance = maxDistance;
        _recoilForcePerSecond = recoilForcePerSecond;
        _impactForce = impactForce;
        _shooter = shooter;
        _positionAnchor = positionAnchor != null ? positionAnchor : shooter.transform;
        _directionSource = shooter.transform;
        _anchorOffset = anchorOffset;
        _verticalOffset = verticalOffset;
    }

    public float GetRecoilForcePerSecond()
    {
        return _recoilForcePerSecond;
    }

    public void StartFiring()
    {
        _isFiring = true;

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

    public void StopFiring()
    {
        _isFiring = false;

        if (beamCore != null)
        {
            beamCore.gameObject.SetActive(false);
        }
        if (beamGlow != null)
        {
            beamGlow.gameObject.SetActive(false);
        }

        _timeSinceLastShieldHit = 0f;
        _currentTargetShield = null;

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
        Vector3 aimDirection = _directionSource.forward;
        Vector3 origin = _positionAnchor.position + aimDirection * _anchorOffset + _positionAnchor.up * _verticalOffset;

        bool hitSomething = Physics.SphereCast(origin, Mathf.Max(0f, hitscanRadius), aimDirection, out RaycastHit hit, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore);

        // Filter: skip shooter and its children
        if (hitSomething && _shooter != null && hit.collider.transform.IsChildOf(_shooter.transform))
        {
            hitSomething = false;
        }

        float beamLength;

        if (hitSomething)
        {
            beamLength = ResolveBeamLength(origin, aimDirection, hit);

            Entity3D damageable = ResolveHitEntity(hit.collider);
            if (damageable != null && IsMatchingTarget(damageable))
            {
                float damageThisFrame = _damagePerSecond * Time.deltaTime;
                damageable.TakeDamage(damageThisFrame, hit.point, _shooter, DamageSource3D.Beam);

                Rigidbody targetRb = hit.collider.attachedRigidbody;
                if (targetRb != null && _impactForce > 0f)
                {
                    float impactForceThisFrame = _impactForce * Time.deltaTime;
                    targetRb.linearVelocity += aimDirection * impactForceThisFrame;
                }

                UpdateShieldHitEffects(damageable, hit.point);
            }

            if (_impactInstance != null)
            {
                _impactInstance.SetActive(true);
                _impactInstance.transform.position = hit.point;
                _impactInstance.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
            }
        }
        else
        {
            beamLength = _maxDistance;
            _timeSinceLastShieldHit = 0f;
            _currentTargetShield = null;

            if (_impactInstance != null)
            {
                _impactInstance.SetActive(false);
            }
        }

        // Position and scale beam visuals
        // Capsule default is 2 units tall along its local Y axis.
        // We orient it so local Y points along aimDirection, then scale Y to match beam length.
        // Position offset = half the length along aimDirection so the base stays at origin.
        Quaternion beamRotation = Quaternion.LookRotation(aimDirection, Vector3.up)
                                * Quaternion.Euler(90f, 0f, 0f); // rotate so capsule Y aligns with forward
        Vector3 beamCenter = origin + aimDirection * (beamLength * 0.5f);
        float halfLength = beamLength * 0.5f;

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
            _muzzleInstance.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
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
        return entity != null
            && !string.IsNullOrEmpty(_targetTag)
            && entity.CompareTag(_targetTag);
    }
}
