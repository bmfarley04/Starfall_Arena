using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private float hitscanRadius = 0.1f;

    protected float _damage;
    protected float _lifetime;
    protected float _impactForce;
    protected Vector3 _direction;
    protected Vector3 _velocity;
    protected Entity3D _shooter;
    protected float _age;
    protected readonly HashSet<int> _hitEntityIds = new HashSet<int>();

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
        _hitEntityIds.Clear();

        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    public void OnSpawnedFromPool()
    {
        _age = 0f;
        _hitEntityIds.Clear();
    }

    public void OnDespawnedToPool()
    {
        _velocity = Vector3.zero;
        _hitEntityIds.Clear();
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
            Mathf.Max(0f, hitscanRadius),
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

    protected virtual bool IsValidHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        if (_shooter != null && hit.collider.transform.IsChildOf(_shooter.transform))
        {
            return false;
        }

        if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        return true;
    }

    protected virtual void ProcessHit(RaycastHit hit)
    {
        Collider other = hit.collider;
        ReflectShield3D reflectShield = ResolveReflectShield(other);
        if (reflectShield != null && reflectShield.TryReflectProjectile(this, hit.point))
        {
            return;
        }

        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            ApplyDamageToEntity(damageable, hit.point, other);
        }

        SpawnHitEffect(hit);
        DespawnSelf();
    }

    protected Entity3D ResolveHitEntity(Collider hitCollider)
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

    protected ReflectShield3D ResolveReflectShield(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        ReflectShield3D shield = hitCollider.GetComponent<ReflectShield3D>();
        if (shield != null)
        {
            return shield;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            shield = hitCollider.attachedRigidbody.GetComponentInChildren<ReflectShield3D>(true);
            if (shield != null)
            {
                return shield;
            }
        }

        return hitCollider.GetComponentInParent<ReflectShield3D>();
    }

    protected bool IsMatchingTarget(Entity3D entity)
    {
        return entity != null
            && !string.IsNullOrEmpty(targetTag)
            && entity.CompareTag(targetTag);
    }

    protected void SpawnHitEffect(RaycastHit hit)
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

    protected bool TryRegisterEntityHit(Entity3D target)
    {
        return target != null && _hitEntityIds.Add(target.GetInstanceID());
    }

    protected void DespawnSelf()
    {
        GameObjectPool3D.Despawn(gameObject);
    }
}
