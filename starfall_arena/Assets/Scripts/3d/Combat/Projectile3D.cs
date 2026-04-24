using System.Collections.Generic;
using UnityEngine;

public interface IProjectileImpactHandler3D
{
    bool TryHandleProjectileImpact(Projectile3D projectile, RaycastHit hit);
}

public class Projectile3D : MonoBehaviour, IPooledObject3D
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
    private static readonly Collider[] OverlapBuffer = new Collider[16];

    protected struct OverlapHitInfo
    {
        public Collider collider;
        public Vector3 point;
        public Vector3 normal;
    }

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
    protected bool _appliesSlow;
    protected float _slowMultiplier = 1f;
    protected float _slowDuration;
    protected float _slowEngineEmissionScale = 1f;
    protected readonly HashSet<int> _hitEntityIds = new HashSet<int>();
    protected NetCombat3D _networkAuthority;
    protected int _requestedFireTick = -1;
    protected int _spawnServerTick = -1;
    protected bool _isCosmeticOnly;
    protected NetProjectileVisualType3D _visualType = NetProjectileVisualType3D.Primary;

    public Vector3 Direction => _direction;
    public float Speed => _velocity.magnitude;
    public float Damage => _damage;
    public float ImpactForce => _impactForce;
    public float RemainingLifetime => Mathf.Max(0f, _lifetime - _age);
    public NetProjectileVisualType3D VisualType => _visualType;

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

        if (TryGetOverlapHit(origin, out OverlapHitInfo overlapHit))
        {
            transform.position = overlapHit.point;
            ProcessOverlapHit(overlapHit);
            return;
        }

        if (TryGetHit(origin, stepDistance, out RaycastHit hit))
        {
            transform.position = hit.point;
            ProcessHit(hit);
            return;
        }

        if (TryProcessLagCompensatedHit(origin, origin + step))
        {
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
        _appliesSlow = false;
        _slowMultiplier = 1f;
        _slowDuration = 0f;
        _slowEngineEmissionScale = 1f;
        _hitEntityIds.Clear();

        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    public void SetCosmeticOnly(bool isCosmeticOnly)
    {
        _isCosmeticOnly = isCosmeticOnly;
    }

    public void SetNetworkAuthority(NetCombat3D networkAuthority, int requestedFireTick)
    {
        _networkAuthority = networkAuthority;
        _requestedFireTick = requestedFireTick;
        _spawnServerTick = NetTickUtil.IsActive ? NetTickUtil.ServerTick : requestedFireTick;
    }

    public void SetNetworkVisualType(NetProjectileVisualType3D visualType)
    {
        _visualType = visualType;
    }

    public void EnableSlow(float slowMultiplier, float slowDuration, float slowEngineEmissionScale = 1f)
    {
        _appliesSlow = true;
        _slowMultiplier = slowMultiplier;
        _slowDuration = slowDuration;
        _slowEngineEmissionScale = Mathf.Clamp01(slowEngineEmissionScale);
    }

    public void OnSpawnedFromPool()
    {
        _age = 0f;
        _hitEntityIds.Clear();
        _networkAuthority = null;
        _requestedFireTick = -1;
        _spawnServerTick = -1;
        _isCosmeticOnly = false;
        _visualType = NetProjectileVisualType3D.Primary;
    }

    public void OnDespawnedToPool()
    {
        _velocity = Vector3.zero;
        _appliesSlow = false;
        _slowMultiplier = 1f;
        _slowDuration = 0f;
        _slowEngineEmissionScale = 1f;
        _hitEntityIds.Clear();
        _networkAuthority = null;
        _requestedFireTick = -1;
        _spawnServerTick = -1;
        _isCosmeticOnly = false;
        _visualType = NetProjectileVisualType3D.Primary;
    }

    protected virtual void ApplyDamageToEntity(Entity3D damageable, Vector3 hitPoint, Collider collider)
    {
        if (!CanApplyGameplay())
        {
            return;
        }

        damageable.TakeDamage(_damage, hitPoint, _shooter, DamageSource3D.Projectile);
        ApplyImpactForce(collider);
    }

    protected virtual void ApplyImpactForce(Collider collider)
    {
        if (!CanApplyGameplay())
        {
            return;
        }

        Rigidbody targetRb = collider.attachedRigidbody;
        if (targetRb != null && _impactForce > 0f)
        {
            Vector3 velocityDelta = _direction.normalized * _impactForce;
            targetRb.linearVelocity += velocityDelta;
            targetRb.GetComponent<NetMovement3D>()?.ApplyCombatVelocityDelta(velocityDelta);
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

    private bool TryGetOverlapHit(Vector3 origin, out OverlapHitInfo nearestHit)
    {
        nearestHit = default;

        float overlapRadius = Mathf.Max(0.001f, hitscanRadius);
        int overlapCount = Physics.OverlapSphereNonAlloc(
            origin,
            overlapRadius,
            OverlapBuffer,
            collisionMask,
            QueryTriggerInteraction.Collide
        );

        float nearestSqrDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = OverlapBuffer[i];
            if (overlap == null)
            {
                continue;
            }

            if (!IsValidOverlapHit(overlap))
            {
                continue;
            }

            OverlapHitInfo hit = BuildOverlapHit(origin, overlap);
            float sqrDistance = (hit.point - origin).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private OverlapHitInfo BuildOverlapHit(Vector3 origin, Collider overlap)
    {
        Vector3 closestPoint = overlap.ClosestPoint(origin);
        Vector3 normal = origin - closestPoint;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = -_direction;
        }
        else
        {
            normal.Normalize();
        }

        return new OverlapHitInfo
        {
            collider = overlap,
            point = closestPoint,
            normal = normal
        };
    }

    protected virtual bool IsValidHit(RaycastHit hit)
    {
        return IsValidOverlapHit(hit.collider);
    }

    protected virtual bool IsValidOverlapHit(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (_shooter != null && collider.transform.IsChildOf(_shooter.transform))
        {
            return false;
        }

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        return true;
    }

    protected virtual void ProcessHit(RaycastHit hit)
    {
        Collider other = hit.collider;
        IProjectileImpactHandler3D impactHandler = ResolveImpactHandler(other);
        if (impactHandler != null && impactHandler.TryHandleProjectileImpact(this, hit))
        {
            return;
        }

        ReflectShield3D reflectShield = ResolveReflectShield(other);
        if (reflectShield != null && reflectShield.TryReflectProjectile(this, hit.point))
        {
            return;
        }

        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            ApplyDamageToEntity(damageable, hit.point, other);
            if (CanApplyGameplay() && _appliesSlow)
            {
                damageable.ApplySlow(_slowMultiplier, _slowDuration);
                if (_slowEngineEmissionScale < 1f)
                {
                    damageable.ThrusterVfx?.ApplyTemporaryEmissionRateScale(_slowEngineEmissionScale, _slowDuration);
                }
            }
        }

        SpawnHitEffect(hit);
        DespawnSelf();
    }

    protected virtual void ProcessOverlapHit(OverlapHitInfo hit)
    {
        Collider other = hit.collider;
        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            ApplyDamageToEntity(damageable, hit.point, other);
        }

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

    protected IProjectileImpactHandler3D ResolveImpactHandler(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        IProjectileImpactHandler3D impactHandler = hitCollider.GetComponent<IProjectileImpactHandler3D>();
        if (impactHandler != null)
        {
            return impactHandler;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            impactHandler = hitCollider.attachedRigidbody.GetComponentInChildren<IProjectileImpactHandler3D>(true);
            if (impactHandler != null)
            {
                return impactHandler;
            }
        }

        return hitCollider.GetComponentInParent<IProjectileImpactHandler3D>();
    }

    protected bool IsMatchingTarget(Entity3D entity)
    {
        return entity != null
            && !string.IsNullOrEmpty(targetTag)
            && entity.CompareTag(targetTag);
    }

    protected bool CanApplyGameplay()
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

    private bool TryProcessLagCompensatedHit(Vector3 from, Vector3 to)
    {
        if (!CanApplyGameplay() || string.IsNullOrEmpty(targetTag))
        {
            return false;
        }

        if (!NetMovement3D.TryGetPlayerByTag(targetTag, out NetMovement3D targetMovement))
        {
            return false;
        }

        int currentServerTick = NetTickUtil.ServerTick;
        int elapsedTicks = Mathf.Max(0, currentServerTick - _spawnServerTick);
        int desiredTargetTick = _requestedFireTick >= 0 ? _requestedFireTick + elapsedTicks : currentServerTick;
        if (!targetMovement.TryGetHistoricalState(desiredTargetTick, out NetStateSnapshot3D targetSnapshot))
        {
            return false;
        }

        float radius = targetMovement.GetCollisionRadius();
        Vector3 closest = ClosestPointOnSegment(from, to, targetSnapshot.Position);
        if ((closest - targetSnapshot.Position).sqrMagnitude > radius * radius)
        {
            return false;
        }

        Entity3D damageable = targetMovement.GetComponent<Entity3D>();
        Collider targetCollider = targetMovement.GetComponent<Collider>();
        if (damageable == null || targetCollider == null || !IsMatchingTarget(damageable))
        {
            return false;
        }

        if (!TryRegisterEntityHit(damageable))
        {
            return false;
        }

        transform.position = closest;
        ApplyDamageToEntity(damageable, closest, targetCollider);
        if (_appliesSlow)
        {
            damageable.ApplySlow(_slowMultiplier, _slowDuration);
            if (_slowEngineEmissionScale < 1f)
            {
                damageable.ThrusterVfx?.ApplyTemporaryEmissionRateScale(_slowEngineEmissionScale, _slowDuration);
            }
        }

        DespawnSelf();
        return true;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 from, Vector3 to, Vector3 point)
    {
        Vector3 segment = to - from;
        float segmentSqrMagnitude = segment.sqrMagnitude;
        if (segmentSqrMagnitude <= Mathf.Epsilon)
        {
            return from;
        }

        float t = Vector3.Dot(point - from, segment) / segmentSqrMagnitude;
        return from + segment * Mathf.Clamp01(t);
    }
}
