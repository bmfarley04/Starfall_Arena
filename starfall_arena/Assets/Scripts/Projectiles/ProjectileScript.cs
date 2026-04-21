using UnityEngine;
using System.Collections.Generic;

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
    protected NetMovement _networkAuthority;
    protected int _requestedFireTick = -1;
    protected int _spawnServerTick = -1;
    protected int _accuracyAttackId = Player.InvalidAttackId;
    protected bool _isCosmeticOnly = false;
    private Vector2 _previousPosition;

    // Pierce mechanics
    protected bool _canPierce = false;
    protected float _pierceMultiplier = 1f;

    // Reflection tracking
    protected bool _isReflected = false;

    // Prevent piercing projectiles from repeatedly damaging the same target
    // across multiple trigger/sweep evaluations during a single flight.
    private readonly HashSet<int> _hitEntityIds = new HashSet<int>();
    private readonly HashSet<int> _hitAsteroidIds = new HashSet<int>();

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
        _previousPosition = _rb.position;
        Destroy(gameObject, _lifetime);
    }

    public void Initialize(Vector3 direction, Vector2 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity shooter = null, int accuracyAttackId = Player.InvalidAttackId)
    {
        _damage = damage;
        _lifetime = lifetime;
        _impactForce = impactForce;
        _direction = direction.normalized;
        _shooter = shooter;
        _accuracyAttackId = accuracyAttackId;

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

    public void SetCosmeticOnly(bool isCosmeticOnly)
    {
        _isCosmeticOnly = isCosmeticOnly;
    }

    public void SetNetworkAuthority(NetMovement networkAuthority, int requestedFireTick)
    {
        _networkAuthority = networkAuthority;
        _requestedFireTick = requestedFireTick;
        _spawnServerTick = NetTickUtil.IsActive ? NetTickUtil.ServerTick : requestedFireTick;
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
        _accuracyAttackId = Player.InvalidAttackId;
        _direction = -_direction;
        _rb.linearVelocity = -_rb.linearVelocity;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle + ROTATION_OFFSET);

        if (_visualController != null)
        {
            _visualController.ResetVisualState();
            _visualController.SetProjectileColor(reflectColor);
        }

        // Reflection starts a new ownership/damage phase, so prior hit suppression
        // should not block valid post-reflect hits.
        _hitEntityIds.Clear();
        _hitAsteroidIds.Clear();
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

    protected virtual void FixedUpdate()
    {
        if (_isCosmeticOnly || !NetTickUtil.IsActive)
        {
            _previousPosition = _rb.position;
            return;
        }

        if (_networkAuthority == null || !_networkAuthority.IsServer)
        {
            _previousPosition = _rb.position;
            return;
        }

        EvaluateServerHitSweep(_previousPosition, _rb.position);
        _previousPosition = _rb.position;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collider)
    {
        if (_isCosmeticOnly)
        {
            if (collider.CompareTag(targetTag) || collider.CompareTag("Asteroid"))
            {
                HandleCosmeticImpact();
            }
            return;
        }

        if (NetTickUtil.IsActive && (_networkAuthority == null || !_networkAuthority.IsServer))
        {
            return;
        }

        // Check for ship collision
        if (collider.CompareTag(targetTag))
        {
            if (NetTickUtil.IsActive)
            {
                return;
            }

            // Check if player has active reflect shield
            Player player = collider.GetComponent<Player>();
            if (player != null && player.TryGetComponent<Reflector>(out var reflectScript) && reflectScript.IsAbilityActive())
            {
                // Let Player handle the reflection
                return;
            }

            if (player != null && player.TryGetComponent<IProjectileReflectorAugment>(out var augmentReflector))
            {
                if (augmentReflector.TryReflectProjectile(this, transform.position))
                {
                    return;
                }
            }

            var damageable = collider.GetComponent<Entity>();
            if (damageable != null)
            {
                if (!TryRegisterEntityHit(damageable))
                {
                    return;
                }

                ApplyDamageToEntity(damageable, transform.position, collider);

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
        else if (collider.CompareTag("Asteroid"))
        {
            var asteroid = collider.GetComponent<AsteroidScript>();
            if (asteroid != null)
            {
                if (!TryRegisterAsteroidHit(asteroid))
                {
                    return;
                }

                asteroid.RequestDamage(_damage, _impactForce, transform.position);
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

    protected virtual void ApplyImpactForce(Collider2D collider)
    {
        Rigidbody2D targetRb = collider.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            // Direct velocity change (deterministic equivalent of ForceMode2D.Impulse)
            Vector2 forceDirection = _direction.normalized;
            targetRb.linearVelocity += forceDirection * (_impactForce / targetRb.mass);
        }
    }

    protected virtual void ApplyDamageToEntity(Entity damageable, Vector2 hitPoint, Collider2D collider)
    {
        damageable.TakeDamage(_damage, _impactForce, hitPoint, DamageSource.Projectile, _shooter, _accuracyAttackId);
        _shooter?.NotifyPrimaryProjectileHit(damageable, hitPoint, _damage);
        ApplyImpactForce(collider);
    }

    protected void HandleCosmeticImpact()
    {
        if (_visualController != null)
        {
            _visualController.OnProjectileImpact(transform.position, _direction);
        }

        if (_canPierce && _pierceMultiplier > 0f)
        {
            _damage *= _pierceMultiplier;
            return;
        }

        Destroy(gameObject);
    }

    private void EvaluateServerHitSweep(Vector2 from, Vector2 to)
    {
        if (string.IsNullOrEmpty(targetTag) || !NetMovement.TryGetPlayerByTag(targetTag, out NetMovement targetMovement))
        {
            return;
        }

        int currentServerTick = NetTickUtil.ServerTick;
        int elapsedTicks = Mathf.Max(0, currentServerTick - _spawnServerTick);
        int desiredTargetTick = _requestedFireTick >= 0 ? _requestedFireTick + elapsedTicks : currentServerTick;

        if (!targetMovement.TryGetHistoricalState(desiredTargetTick, out NetStateSnapshot targetSnapshot))
        {
            return;
        }

        float targetRadius = targetMovement.GetCollisionRadius();
        if (!SegmentIntersectsCircle(from, to, targetSnapshot.Position, targetRadius))
        {
            return;
        }

        Collider2D targetCollider = targetMovement.GetComponent<Collider2D>();
        Entity damageable = targetMovement.GetComponent<Entity>();
        if (targetCollider == null || damageable == null)
        {
            return;
        }

        Vector2 hitPoint = ClosestPointOnSegment(from, to, targetSnapshot.Position);

        if (targetMovement.TryGetComponent(out Player player))
        {
            Collider2D projectileCollider = GetComponent<Collider2D>();
            if (projectileCollider != null && player.TryProcessIncomingProjectileCollision(projectileCollider))
            {
                // Check if the projectile was reflected (targetTag changed away from us)
                if (targetTag != player.thisPlayerTag)
                {
                    // Projectile was reflected — don't destroy it, let it continue
                    return;
                }

                if (_visualController != null)
                {
                    _visualController.OnProjectileImpact(hitPoint, _direction);
                }

                Destroy(gameObject);
                return;
            }

            if (player.TryGetComponent<IProjectileReflectorAugment>(out var augmentReflector))
            {
                if (augmentReflector.TryReflectProjectile(this, hitPoint))
                {
                    return;
                }
            }
        }

        if (!TryRegisterEntityHit(damageable))
        {
            return;
        }

        ApplyDamageToEntity(damageable, hitPoint, targetCollider);

        if (_appliesSlow)
        {
            damageable.ApplySlow(_slowMultiplier, _slowDuration);
        }

        if (_visualController != null)
        {
            _visualController.OnProjectileImpact(hitPoint, _direction);
        }

        if (_canPierce && _pierceMultiplier > 0f)
        {
            _damage *= _pierceMultiplier;
            return;
        }

        Destroy(gameObject);
    }

    private static bool SegmentIntersectsCircle(Vector2 from, Vector2 to, Vector2 center, float radius)
    {
        Vector2 closest = ClosestPointOnSegment(from, to, center);
        float radiusSq = radius * radius;
        return (closest - center).sqrMagnitude <= radiusSq;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 from, Vector2 to, Vector2 point)
    {
        Vector2 segment = to - from;
        float segmentSqrMagnitude = segment.sqrMagnitude;
        if (segmentSqrMagnitude <= Mathf.Epsilon)
        {
            return from;
        }

        float t = Vector2.Dot(point - from, segment) / segmentSqrMagnitude;
        t = Mathf.Clamp01(t);
        return from + segment * t;
    }

    protected bool TryRegisterEntityHit(Entity target)
    {
        return target != null && _hitEntityIds.Add(target.GetInstanceID());
    }

    protected bool TryRegisterAsteroidHit(AsteroidScript asteroid)
    {
        return asteroid != null && _hitAsteroidIds.Add(asteroid.GetInstanceID());
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
