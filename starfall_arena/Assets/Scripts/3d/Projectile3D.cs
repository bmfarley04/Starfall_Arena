using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile3D : MonoBehaviour, IPooledObject3D
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    [Header("Runtime")]
    public string targetTag;

    [Header("Impact FX")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectLifetime = 2f;
    [SerializeField] private float hitEffectOffset = 0.02f;
    [SerializeField] private bool alignHitEffectToSurface = true;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float collisionRadiusScale = 0.5f;

    protected Collider _projectileCollider;
    protected float _damage;
    protected float _lifetime;
    protected float _impactForce;
    protected Vector3 _direction;
    protected Vector3 _velocity;
    protected Entity3D _shooter;
    protected float _age;
    protected float _collisionRadius;

    protected virtual void Awake()
    {
        _projectileCollider = GetComponent<Collider>();
        _projectileCollider.isTrigger = true;
        _collisionRadius = Mathf.Max(0.01f, _projectileCollider.bounds.extents.magnitude * collisionRadiusScale);
    }

    protected virtual void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _lifetime)
        {
            DespawnSelf();
            return;
        }

        float stepDistance = _velocity.magnitude * Time.deltaTime;
        if (stepDistance <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 origin = transform.position;
        Vector3 step = _velocity * Time.deltaTime;

        if (TryGetHit(origin, stepDistance, out RaycastHit hit))
        {
            transform.position = hit.point;
            ProcessHit(hit);
            return;
        }

        transform.position = origin + step;
    }

    public virtual void Initialize(Vector3 direction, Vector3 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity3D shooter = null)
    {
        _damage = damage;
        _lifetime = lifetime;
        _impactForce = impactForce;
        _direction = direction.normalized;
        _shooter = shooter;
        _velocity = (_direction * speed) + shipVelocity;
        _age = 0f;

        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    public void OnSpawnedFromPool()
    {
        _age = 0f;
    }

    public void OnDespawnedToPool()
    {
        _velocity = Vector3.zero;
    }

    protected virtual void ApplyDamageToEntity(Entity3D damageable, Vector3 hitPoint, Collider collider)
    {
        damageable.TakeDamage(_damage, hitPoint, _shooter);
        ApplyImpactForce(collider);
    }

    protected virtual void ApplyImpactForce(Collider collider)
    {
        Rigidbody targetRb = collider.attachedRigidbody;
        if (targetRb != null && _impactForce > 0f)
        {
            targetRb.linearVelocity += _direction.normalized * _impactForce;
        }
    }

    private bool TryGetHit(Vector3 origin, float stepDistance, out RaycastHit nearestHit)
    {
        nearestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            _collisionRadius,
            _direction,
            HitBuffer,
            stepDistance,
            collisionMask,
            QueryTriggerInteraction.Collide
        );

        float nearestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (!IsValidHit(hit))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool IsValidHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        if (hit.collider == _projectileCollider)
        {
            return false;
        }

        if (_shooter != null && hit.collider.transform.IsChildOf(_shooter.transform))
        {
            return false;
        }

        return true;
    }

    private void ProcessHit(RaycastHit hit)
    {
        Collider other = hit.collider;
        if (!string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag))
        {
            Entity3D damageable = other.GetComponent<Entity3D>();
            if (damageable != null)
            {
                ApplyDamageToEntity(damageable, hit.point, other);
            }
        }

        SpawnHitEffect(hit);
        DespawnSelf();
    }

    private void SpawnHitEffect(RaycastHit hit)
    {
        if (hitEffectPrefab == null)
        {
            return;
        }

        Vector3 position = hit.point + (hit.normal * hitEffectOffset);
        Quaternion rotation = alignHitEffectToSurface
            ? Quaternion.LookRotation(hit.normal, Vector3.up)
            : Quaternion.identity;

        GameObject effectObject = GameObjectPool3D.Spawn(hitEffectPrefab, position, rotation);
        PooledObject3D pooled = effectObject != null ? effectObject.GetComponent<PooledObject3D>() : null;
        if (pooled != null)
        {
            pooled.ScheduleDespawn(hitEffectLifetime);
        }
    }

    protected void DespawnSelf()
    {
        GameObjectPool3D.Despawn(gameObject);
    }
}
