using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileScript : MonoBehaviour
{
    [Header("Settings")]
    public string targetTag;
    private const float ROTATION_OFFSET = -90f;

    protected Rigidbody2D _rb;
    protected float _damage;
    protected float _lifetime;
    protected float _impactForce;
    protected Vector2 _direction;
    protected ProjectileVisualController _visualController;
    protected Entity _shooter;

    // Pierce mechanics
    protected bool _canPierce = false;
    protected float _pierceMultiplier = 1f;

    // Reflection tracking
    protected bool _isReflected = false;

    // Slow effect
    protected bool _appliesSlow = false;
    protected float _slowMultiplier = 1f;
    protected float _slowDuration = 0f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _visualController = GetComponent<ProjectileVisualController>();
    }

    void Start()
    {
        Destroy(gameObject, _lifetime);
    }

    public void Initialize(Vector3 direction, Vector2 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity shooter = null)
    {
        _damage = damage;
        _lifetime = lifetime;
        _impactForce = impactForce;
        _direction = direction.normalized;
        _shooter = shooter;

        Vector2 ownVelocity = _direction * speed;
        _rb.linearVelocity = ownVelocity + shipVelocity;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + ROTATION_OFFSET);

        // Notify visual controller that projectile has spawned
        if (_visualController != null)
        {
            _visualController.OnProjectileSpawned();
        }
    }

    public Entity GetShooter()
    {
        return _shooter;
    }

    public Vector2 GetDirection()
    {
        return _direction;
    }

    public float GetSpeed()
    {
        return _rb != null ? _rb.linearVelocity.magnitude : 0f;
    }

    public float GetDamage()
    {
        return _damage;
    }

    public float GetLifetime()
    {
        return _lifetime;
    }

    public float GetImpactForce()
    {
        return _impactForce;
    }

    public void Reflect(string newTargetTag, Color reflectColor, Entity newShooter)
    {
        targetTag = newTargetTag;
        _shooter = newShooter;
        _direction = -_direction;
        _rb.linearVelocity = -_rb.linearVelocity;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + ROTATION_OFFSET);

        if (_visualController != null)
        {
            _visualController.ResetVisualState();
            _visualController.SetProjectileColor(reflectColor);
        }
    }

    public void EnablePiercing(float damageMultiplierPerHit)
    {
        _canPierce = true;
        _pierceMultiplier = damageMultiplierPerHit;
    }

    public void EnableSlow(float slowMultiplier, float slowDuration)
    {
        _appliesSlow = true;
        _slowMultiplier = slowMultiplier;
        _slowDuration = slowDuration;
    }

    public void ApplyDamageMultiplier(float multiplier)
    {
        _damage *= multiplier;
    }

    /// <summary>
    /// Marks this projectile as reflected. Used by boss to apply reduced reflection damage.
    /// </summary>
    public void MarkAsReflected()
    {
        _isReflected = true;
    }

    /// <summary>
    /// Returns true if this projectile was reflected by the player.
    /// </summary>
    public bool IsReflected()
    {
        return _isReflected;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collider)
    {
        // Check for ship collision
        if (collider.CompareTag(targetTag))
        {
            // Check if player has active reflect shield
            Class1 player = collider.GetComponent<Class1>();
            if (player != null && player.abilities.reflect.shield != null && player.abilities.reflect.shield.IsActive())
            {
                // Let Class1 handle the reflection
                return;
            }

            var damageable = collider.GetComponent<Entity>();
            if (damageable != null)
            {
                // Deal damage to the entity
                damageable.TakeDamage(_damage, _impactForce, transform.position);
                ApplyImpactForce(collider);

                // Apply slow effect if enabled
                if (_appliesSlow)
                {
                    damageable.ApplySlow(_slowMultiplier, _slowDuration);
                }

                if (_visualController != null)
                {
                    _visualController.OnProjectileImpact(transform.position, _direction);
                }

                // If piercing is enabled, reduce damage and continue; otherwise destroy
                if (_canPierce && _pierceMultiplier > 0)
                {
                    _damage *= _pierceMultiplier;
                    // Continue traveling through the target
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
        // Check for asteroid collision
        // TODO: Re-enable when AsteroidScript is implemented
        else if (collider.CompareTag("Asteroid"))
        {
            var asteroid = collider.GetComponent<AsteroidScript>();
            if (asteroid != null)
            {
                asteroid.TakeDamage(_damage, _impactForce, transform.position);
            }

            if (_visualController != null)
            {
                _visualController.OnProjectileImpact(transform.position, _direction);
            }

            // If piercing is enabled, reduce damage and continue; otherwise destroy
            if (_canPierce && _pierceMultiplier > 0)
            {
                _damage *= _pierceMultiplier;
                // Continue traveling through the asteroid
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void ApplyImpactForce(Collider2D collider)
    {
        Rigidbody2D targetRb = collider.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            // Apply force in the direction the projectile was traveling
            Vector2 forceDirection = _direction.normalized;
            targetRb.AddForce(forceDirection * _impactForce, ForceMode2D.Impulse);
        }
    }

    void OnDestroy()
    {
        // Clean up visual effects if destroyed without impact
        if (_visualController != null)
        {
            _visualController.OnProjectileDestroyed();
        }
    }
}