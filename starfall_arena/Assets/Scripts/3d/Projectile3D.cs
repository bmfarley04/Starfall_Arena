using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Projectile3D : MonoBehaviour
{
    [Header("Runtime")]
    public string targetTag;

    protected Rigidbody _rb;
    protected float _damage;
    protected float _lifetime;
    protected float _impactForce;
    protected Vector3 _direction;
    protected Entity3D _shooter;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;

        Collider projectileCollider = GetComponent<Collider>();
        projectileCollider.isTrigger = true;
    }

    protected virtual void Start()
    {
        Destroy(gameObject, _lifetime);
    }

    public virtual void Initialize(Vector3 direction, Vector3 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity3D shooter = null)
    {
        _damage = damage;
        _lifetime = lifetime;
        _impactForce = impactForce;
        _direction = direction.normalized;
        _shooter = shooter;

        _rb.linearVelocity = (_direction * speed) + shipVelocity;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(targetTag) && other.CompareTag(targetTag))
        {
            Entity3D damageable = other.GetComponent<Entity3D>();
            if (damageable != null)
            {
                ApplyDamageToEntity(damageable, other.ClosestPoint(transform.position), other);
                Destroy(gameObject);
            }
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
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
}
