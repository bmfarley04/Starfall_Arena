using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserBeam3D : MonoBehaviour
{
    [Header("Effect Prefabs")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private GameObject muzzleEffectPrefab;

    [Header("Beam Width")]
    [Tooltip("Width of the beam LineRenderer")]
    [SerializeField] private float beamWidth = 0.5f;

    [Header("UV Animation")]
    [SerializeField] private bool animateUV;
    [SerializeField] private float uvSpeed = 1f;
    [SerializeField] private float uvTilingPerUnit = 0.1f;

    [Header("Shield Effects")]
    [SerializeField] private float laserHitInterval = 0.2f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;

    private LineRenderer _lineRenderer;
    private bool _isFiring;
    private string _targetTag;
    private float _maxDistance;
    private float _damagePerSecond;
    private float _recoilForcePerSecond;
    private float _impactForce;
    private Entity3D _shooter;
    private Transform _positionAnchor;
    private float _anchorOffset;
    private Camera _aimCamera;
    private float _convergenceDistance;
    private float _uvOffset;
    private float _timeSinceLastShieldHit;
    private ShieldController _currentTargetShield;

    private GameObject _impactInstance;
    private GameObject _muzzleInstance;

    void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
            _lineRenderer.startWidth = beamWidth;
            _lineRenderer.endWidth = beamWidth;
        }
    }

    public void Initialize(string targetTag, float damagePerSecond, float maxDistance,
        float recoilForcePerSecond, float impactForce, Entity3D shooter,
        Transform positionAnchor = null, float anchorOffset = 0f,
        Camera aimCamera = null, float convergenceDistance = 150f)
    {
        _targetTag = targetTag;
        _damagePerSecond = damagePerSecond;
        _maxDistance = maxDistance;
        _recoilForcePerSecond = recoilForcePerSecond;
        _impactForce = impactForce;
        _shooter = shooter;
        _positionAnchor = positionAnchor != null ? positionAnchor : shooter.transform;
        _anchorOffset = anchorOffset;
        _aimCamera = aimCamera != null ? aimCamera : Camera.main;
        _convergenceDistance = convergenceDistance;
    }

    public float GetRecoilForcePerSecond()
    {
        return _recoilForcePerSecond;
    }

    public void StartFiring()
    {
        _isFiring = true;
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = true;
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
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
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

        if (animateUV && _lineRenderer != null)
        {
            _uvOffset += Time.deltaTime * uvSpeed;
            if (_uvOffset > 1f)
            {
                _uvOffset -= 1f;
            }
            _lineRenderer.material.SetVector("_Offset", new Vector2(_uvOffset, 0f));
        }
    }

    private void FireBeam()
    {
        if (_lineRenderer == null)
        {
            return;
        }

        Vector3 origin = _positionAnchor.position + _positionAnchor.forward * _anchorOffset;
        Vector3 aimDirection = ResolveAimDirection(origin);

        bool hitSomething = Physics.Raycast(origin, aimDirection, out RaycastHit hit, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore);

        // Filter: skip shooter and its children
        if (hitSomething && _shooter != null && hit.collider.transform.IsChildOf(_shooter.transform))
        {
            hitSomething = false;
        }

        Vector3 endPosition;

        if (hitSomething)
        {
            endPosition = hit.point;

            Entity3D damageable = ResolveHitEntity(hit.collider);
            if (damageable != null && IsMatchingTarget(damageable))
            {
                float damageThisFrame = _damagePerSecond * Time.deltaTime;
                damageable.TakeDamage(damageThisFrame, hit.point, _shooter);

                // Apply impact force
                Rigidbody targetRb = hit.collider.attachedRigidbody;
                if (targetRb != null && _impactForce > 0f)
                {
                    float impactForceThisFrame = _impactForce * Time.deltaTime;
                    targetRb.linearVelocity += aimDirection * impactForceThisFrame;
                }

                // Shield hit effects at intervals
                UpdateShieldHitEffects(damageable, hit.point);
            }

            // Position impact effect at hit point
            if (_impactInstance != null)
            {
                _impactInstance.SetActive(true);
                _impactInstance.transform.position = hit.point;
                _impactInstance.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
            }
        }
        else
        {
            endPosition = origin + aimDirection * _maxDistance;
            _timeSinceLastShieldHit = 0f;
            _currentTargetShield = null;

            if (_impactInstance != null)
            {
                _impactInstance.SetActive(false);
            }
        }

        // Update line renderer
        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, endPosition);

        // Position muzzle effect at beam origin
        if (_muzzleInstance != null)
        {
            _muzzleInstance.transform.position = origin;
            _muzzleInstance.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        }

        // Scale UV to beam length
        float beamLength = Vector3.Distance(origin, endPosition);
        _lineRenderer.material.SetTextureScale("_BaseMap", new Vector2(beamLength * uvTilingPerUnit, 1f));
    }

    private Vector3 ResolveAimDirection(Vector3 beamOrigin)
    {
        if (_aimCamera == null)
        {
            return _positionAnchor.forward;
        }

        Ray centerRay = _aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Find convergence point from camera center, matching ProjectileWeapon3D
        float convergeDist = _convergenceDistance;
        if (Physics.Raycast(centerRay, out RaycastHit hit, _maxDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            convergeDist = Mathf.Max(_convergenceDistance, hit.distance);
        }
        else
        {
            convergeDist = Mathf.Max(_convergenceDistance, _maxDistance);
        }

        Vector3 convergencePoint = centerRay.origin + centerRay.direction * convergeDist;
        Vector3 direction = convergencePoint - beamOrigin;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return centerRay.direction;
        }

        return direction.normalized;
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
