using UnityEngine;

[DisallowMultipleComponent]
public class MissileProjectile3D : Projectile3D
{
    public enum GuidanceMode3D
    {
        Straight = 0,
        Guided = 1
    }

    [System.Serializable]
    public struct AreaDamageConfig3D
    {
        [Tooltip("If enabled, missile impacts damage every valid target inside explosionRadius instead of only the directly hit target.")]
        public bool enabled;
        [Tooltip("World-space radius around the missile impact point that receives missile splash damage.")]
        public float explosionRadius;
        [Tooltip("Damage dealt to each valid target inside the missile explosion. If this is 0 or less, the missile's base projectile damage is used.")]
        public float explosionDamage;
        [Tooltip("Radial velocity impulse applied to each valid target inside the missile explosion. If this is 0 or less, the missile's base impact force is used.")]
        public float explosionImpactForce;
        [Tooltip("Physics layers included in the missile explosion overlap query.")]
        public LayerMask collisionMask;
        [Tooltip("Maximum colliders checked by the missile explosion query. Raise this only for dense scenes where splash can overlap more targets.")]
        public int maxOverlapColliders;
    }

    [System.Serializable]
    public struct GuidanceConfig3D
    {
        [Tooltip("Choose whether this missile flies straight or steers toward a target.")]
        public GuidanceMode3D mode;
        [Tooltip("Maximum turn rate in degrees/second when guidance is enabled.")]
        public float turnRateDegPerSecond;
        [Tooltip("Delay before guidance starts after spawn.")]
        public float guidanceStartDelay;
    }

    [System.Serializable]
    public struct WarmupConfig3D
    {
        [Tooltip("If enabled, the missile launches slowly before ramping to cruise speed.")]
        public bool enabled;
        [Tooltip("Initial launch speed during warmup.")]
        public float initialSpeed;
        [Tooltip("How long the missile stays at initial speed before ramping up.")]
        public float lowSpeedDuration;
        [Tooltip("How long the speed ramp takes to reach cruise speed.")]
        public float accelerationDuration;
    }

    [System.Serializable]
    public struct DespawnConfig3D
    {
        [Tooltip("Stop emission and keep the missile alive briefly so fire and trails can fade naturally.")]
        public bool delayDespawn;
        [Tooltip("Delay before returning the missile to the pool after impact or end-of-life.")]
        public float despawnDelay;
        [Tooltip("Particle systems that should be allowed to keep fading during the despawn delay.")]
        public ParticleSystem[] delayedParticles;
    }

    [System.Serializable]
    public struct ImpactConfig3D
    {
        [Tooltip("Explosion prefab spawned at the missile position on impact or end-of-life.")]
        public GameObject explosionPrefab;
        [Tooltip("Uniform scale multiplier applied to the spawned explosion.")]
        public float explosionScale;
        [Tooltip("Optional impact sound played from the missile before despawn.")]
        public SoundEffect impactSound;
    }

    [Header("Missile Guidance")]
    [SerializeField] private GuidanceConfig3D guidance = new GuidanceConfig3D
    {
        mode = GuidanceMode3D.Guided,
        turnRateDegPerSecond = 120f,
        guidanceStartDelay = 0.05f
    };

    [Header("Missile Warmup")]
    [SerializeField] private WarmupConfig3D warmup = new WarmupConfig3D
    {
        enabled = true,
        initialSpeed = 6f,
        lowSpeedDuration = 0.08f,
        accelerationDuration = 0.25f
    };

    [Header("Missile Despawn")]
    [SerializeField] private DespawnConfig3D despawn = new DespawnConfig3D
    {
        delayDespawn = true,
        despawnDelay = 0.35f
    };

    [Header("Impact")]
    [SerializeField] private ImpactConfig3D impact = new ImpactConfig3D
    {
        explosionScale = 1f
    };

    [Header("Area Damage")]
    [SerializeField] private AreaDamageConfig3D areaDamage = new AreaDamageConfig3D
    {
        enabled = true,
        explosionRadius = 6f,
        explosionDamage = 20f,
        explosionImpactForce = 8f,
        collisionMask = ~0,
        maxOverlapColliders = 16
    };

    private Transform _target;
    private Vector3 _inheritedVelocity;
    private Vector3 _currentDirection;
    private Vector3 _formationForward;
    private Vector3 _formationRight;
    private Vector3 _formationUp;
    private Vector3 _formationRadialDirection;
    private float _cruiseSpeed;
    private float _spawnTime;
    private float _formationFanArcDegrees;
    private float _formationFanOutDuration;
    private float _formationHoldDuration;
    private float _formationConvergeDuration;
    private float _formationConvergenceRadius;
    private float _formationMaxSpeedMultiplier;
    private int _formationSlotIndex;
    private int _formationSlotCount;
    private bool _isImpacted;
    private bool _impactVisualSpawned;
    private bool _endOfLifeTriggered;
    private bool _usesFormationGuidance;
    private Renderer[] _visibleRenderers;
    private ParticleSystem[] _particles;
    private TrailRenderer[] _trails;
    private AudioSource _impactAudioSource;
    private PooledObject3D _pooledObject;
    private Collider[] _areaDamageColliders;

    private bool UsesGuidance => guidance.mode == GuidanceMode3D.Guided;

    private void OnEnable()
    {
        _target = null;
        _inheritedVelocity = Vector3.zero;
        _currentDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        _formationForward = _currentDirection;
        _formationRight = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;
        _formationUp = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
        _formationRadialDirection = _formationRight;
        _cruiseSpeed = 0f;
        _spawnTime = Time.time;
        _formationSlotIndex = 0;
        _formationSlotCount = 0;
        _formationFanArcDegrees = 0f;
        _formationFanOutDuration = 0f;
        _formationHoldDuration = 0f;
        _formationConvergeDuration = 0f;
        _formationConvergenceRadius = 0f;
        _formationMaxSpeedMultiplier = 1f;
        _isImpacted = false;
        _impactVisualSpawned = false;
        _endOfLifeTriggered = false;
        _usesFormationGuidance = false;

        CacheVisualComponentsIfNeeded();
        ResetVisualState();
        EnsureImpactAudioSource();
        EnsureAreaDamageBuffer();
    }

    private void OnDisable()
    {
        _isImpacted = false;
        _impactVisualSpawned = false;
        _endOfLifeTriggered = false;
    }

    public override void Initialize(Vector3 direction, Vector3 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity3D shooter = null, int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        base.Initialize(direction, shipVelocity, speed, damage, lifetime, impactForce, shooter, accuracyAttackId);

        _currentDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        if (_currentDirection.sqrMagnitude <= 0.0001f)
        {
            _currentDirection = Vector3.forward;
        }

        _inheritedVelocity = shipVelocity;
        _cruiseSpeed = speed;
        _spawnTime = Time.time;
        _target = UsesGuidance ? AcquireTarget() : null;
        UpdateMissileRotation();
    }

    public void ConfigureFormationGuidance(
        int slotIndex,
        int slotCount,
        float fanArcDegrees,
        float fanOutDuration,
        float holdDuration,
        float convergeDuration,
        float convergenceRadius,
        float maxSpeedMultiplier,
        Vector3 formationForward,
        Vector3 formationUp)
    {
        _formationSlotIndex = Mathf.Max(0, slotIndex);
        _formationSlotCount = Mathf.Max(1, slotCount);
        _formationFanArcDegrees = Mathf.Max(0f, fanArcDegrees);
        _formationFanOutDuration = Mathf.Max(0f, fanOutDuration);
        _formationHoldDuration = Mathf.Max(0f, holdDuration);
        _formationConvergeDuration = Mathf.Max(0.01f, convergeDuration);
        _formationConvergenceRadius = Mathf.Max(0f, convergenceRadius);
        _formationMaxSpeedMultiplier = Mathf.Max(1f, maxSpeedMultiplier);
        _formationForward = formationForward.sqrMagnitude > 0.0001f ? formationForward.normalized : _currentDirection;
        _formationUp = formationUp.sqrMagnitude > 0.0001f ? formationUp.normalized : Vector3.up;
        if (Mathf.Abs(Vector3.Dot(_formationForward, _formationUp)) > 0.98f)
        {
            _formationUp = Vector3.up;
        }

        _formationRight = Vector3.Cross(_formationUp, _formationForward);
        if (_formationRight.sqrMagnitude <= 0.0001f)
        {
            _formationRight = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;
        }
        else
        {
            _formationRight.Normalize();
        }

        _formationRadialDirection = ResolveFormationRadialDirection();
        _usesFormationGuidance = _formationSlotCount > 1;
        if (_usesFormationGuidance && UsesGuidance && _target == null)
        {
            _target = AcquireTarget();
        }
    }

    public void SetGuidanceEnabled(bool enabled)
    {
        guidance.mode = enabled ? GuidanceMode3D.Guided : GuidanceMode3D.Straight;
        if (!enabled)
        {
            _target = null;
        }
    }

    public void SetGuidanceMode(GuidanceMode3D mode)
    {
        guidance.mode = mode;
        if (mode != GuidanceMode3D.Guided)
        {
            _target = null;
        }
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    protected override void Update()
    {
        if (_isImpacted)
        {
            return;
        }

        if (!_endOfLifeTriggered && _lifetime > 0f && (_age + Time.deltaTime) >= _lifetime)
        {
            _endOfLifeTriggered = true;
            HandleImpact(transform.position, -_currentDirection);
            return;
        }

        UpdateGuidance();
        float speed = GetCurrentSpeed();
        if (_usesFormationGuidance)
        {
            speed = ResolveFormationSpeed(speed);
        }

        _direction = _currentDirection;
        _velocity = (_currentDirection * speed) + _inheritedVelocity;
        UpdateMissileRotation();

        base.Update();
    }

    protected override void ProcessHit(RaycastHit hit)
    {
        if (_isImpacted)
        {
            return;
        }

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
            if (areaDamage.enabled)
            {
                ApplyAreaDamage(hit.point);
            }
            else
            {
                ApplyDamageToEntity(damageable, hit.point, other);
                ApplySlowIfEnabled(damageable);
            }
        }

        HandleImpact(hit.point, hit.normal);
    }

    protected override void ProcessOverlapHit(OverlapHitInfo hit)
    {
        if (_isImpacted)
        {
            return;
        }

        Collider other = hit.collider;
        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            if (areaDamage.enabled)
            {
                ApplyAreaDamage(hit.point);
            }
            else
            {
                ApplyDamageToEntity(damageable, hit.point, other);
                ApplySlowIfEnabled(damageable);
            }
        }

        HandleImpact(hit.point, hit.normal);
    }

    private void UpdateGuidance()
    {
        if (!UsesGuidance)
        {
            return;
        }

        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            _target = AcquireTarget();
            if (_target == null)
            {
                return;
            }
        }

        if (Time.time < _spawnTime + Mathf.Max(0f, guidance.guidanceStartDelay))
        {
            return;
        }

        Vector3 desiredDirection = _usesFormationGuidance
            ? ResolveFormationDesiredDirection()
            : (_target.position - transform.position).normalized;
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float turnStepRadians = Mathf.Max(0f, guidance.turnRateDegPerSecond) * Mathf.Deg2Rad * Time.deltaTime;
        _currentDirection = Vector3.RotateTowards(_currentDirection, desiredDirection, turnStepRadians, 0f).normalized;
    }

    private Vector3 ResolveFormationDesiredDirection()
    {
        float elapsed = Mathf.Max(0f, Time.time - _spawnTime - Mathf.Max(0f, guidance.guidanceStartDelay));
        Vector3 fanDirection = ResolveFormationFanDirection();
        float fanOutDuration = Mathf.Max(0.01f, _formationFanOutDuration);
        if (elapsed < fanOutDuration)
        {
            float t = Mathf.Clamp01(elapsed / fanOutDuration);
            return Vector3.Slerp(_formationForward, fanDirection, t).normalized;
        }

        if (elapsed < fanOutDuration + _formationHoldDuration)
        {
            return fanDirection;
        }

        if (_target == null)
        {
            return fanDirection;
        }

        float convergeElapsed = elapsed - fanOutDuration - _formationHoldDuration;
        float convergeT = Mathf.Clamp01(convergeElapsed / Mathf.Max(0.01f, _formationConvergeDuration));
        Vector3 aimPoint = ResolveFormationAimPoint(convergeT);
        Vector3 toAimPoint = aimPoint - transform.position;
        return toAimPoint.sqrMagnitude > 0.0001f ? toAimPoint.normalized : fanDirection;
    }

    private Vector3 ResolveFormationFanDirection()
    {
        float coneRadians = Mathf.Clamp(_formationFanArcDegrees, 0f, 179f) * Mathf.Deg2Rad;
        Vector3 direction = (_formationForward * Mathf.Cos(coneRadians)) + (_formationRadialDirection * Mathf.Sin(coneRadians));
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : _formationForward;
    }

    private Vector3 ResolveFormationRadialDirection()
    {
        if (_formationSlotCount <= 1)
        {
            return _formationRight;
        }

        float angle = (_formationSlotIndex / (float)_formationSlotCount) * Mathf.PI * 2f;
        Vector3 radial = (_formationRight * Mathf.Cos(angle)) + (_formationUp * Mathf.Sin(angle));
        return radial.sqrMagnitude > 0.0001f ? radial.normalized : _formationRight;
    }

    private Vector3 ResolveFormationAimPoint(float convergeT)
    {
        if (_target == null)
        {
            return transform.position + _currentDirection;
        }

        float offset = _formationConvergenceRadius * (1f - Mathf.Clamp01(convergeT));
        return _target.position + (_formationRadialDirection * offset);
    }

    private float ResolveFormationSpeed(float baseSpeed)
    {
        if (_target == null)
        {
            return baseSpeed;
        }

        float elapsed = Mathf.Max(0f, Time.time - _spawnTime - Mathf.Max(0f, guidance.guidanceStartDelay));
        float convergeStart = Mathf.Max(0.01f, _formationFanOutDuration) + _formationHoldDuration;
        if (elapsed < convergeStart)
        {
            return baseSpeed;
        }

        float remaining = Mathf.Max(0.02f, _formationConvergeDuration - (elapsed - convergeStart));
        Vector3 finalTargetPoint = _target.position;
        float distanceToFinalTarget = Vector3.Distance(transform.position, finalTargetPoint);
        float synchronizedSpeed = distanceToFinalTarget / remaining;
        float maxSpeed = Mathf.Max(baseSpeed, _cruiseSpeed * _formationMaxSpeedMultiplier);
        return Mathf.Clamp(synchronizedSpeed, baseSpeed * 0.25f, maxSpeed);
    }

    private float GetCurrentSpeed()
    {
        if (!warmup.enabled)
        {
            return _cruiseSpeed;
        }

        float safeInitial = Mathf.Max(0f, warmup.initialSpeed);
        float elapsed = Time.time - _spawnTime;
        float lowDuration = Mathf.Max(0f, warmup.lowSpeedDuration);
        if (elapsed <= lowDuration)
        {
            return safeInitial;
        }

        float accelerationDuration = Mathf.Max(0.0001f, warmup.accelerationDuration);
        float t = Mathf.Clamp01((elapsed - lowDuration) / accelerationDuration);
        return Mathf.Lerp(safeInitial, _cruiseSpeed, t);
    }

    private void UpdateMissileRotation()
    {
        if (_currentDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(_currentDirection.normalized, Vector3.up);
    }

    private void HandleImpact(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_isImpacted)
        {
            return;
        }

        _isImpacted = true;
        _velocity = Vector3.zero;

        SpawnExplosion(hitPoint, hitNormal);
        PlayImpactSound();
        HideMissileVisuals();
        StopMissileTrailsAndParticles();

        if (despawn.delayDespawn)
        {
            ScheduleMissileDespawn();
            return;
        }

        DespawnSelf();
    }

    private void ApplyAreaDamage(Vector3 explosionPosition)
    {
        if (!CanApplyGameplay())
        {
            return;
        }

        float radius = Mathf.Max(0f, areaDamage.explosionRadius);
        if (radius <= 0f)
        {
            return;
        }

        EnsureAreaDamageBuffer();

        float damage = areaDamage.explosionDamage > 0f ? areaDamage.explosionDamage : _damage;
        float force = areaDamage.explosionImpactForce > 0f ? areaDamage.explosionImpactForce : _impactForce;
        int hitCount = Physics.OverlapSphereNonAlloc(
            explosionPosition,
            radius,
            _areaDamageColliders,
            areaDamage.collisionMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = _areaDamageColliders[i];
            if (hitCollider == null || !IsValidOverlapHit(hitCollider))
            {
                continue;
            }

            Entity3D damageable = ResolveHitEntity(hitCollider);
            if (damageable == null || !IsMatchingTarget(damageable) || !TryRegisterEntityHit(damageable))
            {
                continue;
            }

            damageable.TakeDamage(damage, explosionPosition, _shooter, DamageSource3D.Projectile, _accuracyAttackId);
            ApplyAreaImpactForce(hitCollider, explosionPosition, force);
            ApplySlowIfEnabled(damageable);
        }
    }

    private void ApplyAreaImpactForce(Collider hitCollider, Vector3 explosionPosition, float force)
    {
        if (!CanApplyGameplay() || force <= 0f || hitCollider == null)
        {
            return;
        }

        Rigidbody targetRb = hitCollider.attachedRigidbody;
        if (targetRb == null)
        {
            return;
        }

        Vector3 forceDirection = targetRb.worldCenterOfMass - explosionPosition;
        if (forceDirection.sqrMagnitude <= 0.0001f)
        {
            forceDirection = _currentDirection.sqrMagnitude > 0.0001f ? _currentDirection.normalized : transform.forward;
        }
        else
        {
            forceDirection.Normalize();
        }

        Vector3 velocityDelta = forceDirection * force;
        targetRb.linearVelocity += velocityDelta;
        targetRb.GetComponent<NetMovement3D>()?.ApplyCombatVelocityDelta(velocityDelta);
    }

    private void ApplySlowIfEnabled(Entity3D damageable)
    {
        if (!CanApplyGameplay() || !_appliesSlow || damageable == null)
        {
            return;
        }

        damageable.ApplySlow(_slowMultiplier, _slowDuration);
        if (_slowEngineEmissionScale < 1f)
        {
            damageable.ThrusterVfx?.ApplyTemporaryEmissionRateScale(_slowEngineEmissionScale, _slowDuration);
        }
    }

    private void EnsureAreaDamageBuffer()
    {
        int desiredSize = Mathf.Max(1, areaDamage.maxOverlapColliders);
        if (_areaDamageColliders == null || _areaDamageColliders.Length != desiredSize)
        {
            _areaDamageColliders = new Collider[desiredSize];
        }
    }

    private void SpawnExplosion(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (impact.explosionPrefab == null || _impactVisualSpawned)
        {
            return;
        }

        Quaternion rotation = hitNormal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(hitNormal.normalized, Vector3.up)
            : Quaternion.identity;

        GameObject spawnedExplosion = GameObjectPool3D.Spawn(impact.explosionPrefab, hitPoint, rotation);
        if (spawnedExplosion != null)
        {
            spawnedExplosion.transform.localScale *= Mathf.Max(0.01f, impact.explosionScale);

            TimedEffectCleanup3D cleanup = spawnedExplosion.GetComponent<TimedEffectCleanup3D>();
            if (cleanup == null)
            {
                cleanup = spawnedExplosion.AddComponent<TimedEffectCleanup3D>();
            }

            cleanup.BeginCleanup();
        }

        _impactVisualSpawned = true;
    }

    private void PlayImpactSound()
    {
        if (impact.impactSound == null || _impactAudioSource == null)
        {
            return;
        }

        impact.impactSound.Play(_impactAudioSource);
    }

    private void HideMissileVisuals()
    {
        if (_visibleRenderers == null)
        {
            return;
        }

        for (int i = 0; i < _visibleRenderers.Length; i++)
        {
            if (_visibleRenderers[i] != null)
            {
                _visibleRenderers[i].enabled = false;
            }
        }
    }

    private void StopMissileTrailsAndParticles()
    {
        if (_trails != null)
        {
            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer trail = _trails[i];
                if (trail == null)
                {
                    continue;
                }

                trail.emitting = false;
            }
        }

        if (_particles == null)
        {
            return;
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem particle = _particles[i];
            if (particle == null)
            {
                continue;
            }

            bool keepFading = IsDelayedParticle(particle);
            particle.Stop(false, keepFading
                ? ParticleSystemStopBehavior.StopEmitting
                : ParticleSystemStopBehavior.StopEmittingAndClear);

            if (!keepFading)
            {
                particle.Clear(false);
            }
        }
    }

    private bool IsDelayedParticle(ParticleSystem particle)
    {
        if (particle == null || despawn.delayedParticles == null)
        {
            return false;
        }

        for (int i = 0; i < despawn.delayedParticles.Length; i++)
        {
            if (ReferenceEquals(despawn.delayedParticles[i], particle))
            {
                return true;
            }
        }

        return false;
    }

    private void ScheduleMissileDespawn()
    {
        _pooledObject ??= GetComponent<PooledObject3D>();
        float delay = Mathf.Max(0f, despawn.despawnDelay);
        if (_pooledObject != null)
        {
            _pooledObject.ScheduleDespawn(delay);
            return;
        }

        if (delay <= 0f)
        {
            DespawnSelf();
            return;
        }

        Destroy(gameObject, delay);
    }

    private void CacheVisualComponentsIfNeeded()
    {
        if (_visibleRenderers == null)
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            int visibleCount = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] is TrailRenderer || allRenderers[i] is ParticleSystemRenderer)
                {
                    continue;
                }

                visibleCount++;
            }

            _visibleRenderers = new Renderer[visibleCount];
            int writeIndex = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i] is TrailRenderer || allRenderers[i] is ParticleSystemRenderer)
                {
                    continue;
                }

                _visibleRenderers[writeIndex++] = allRenderers[i];
            }
        }

        _particles ??= GetComponentsInChildren<ParticleSystem>(true);
        _trails ??= GetComponentsInChildren<TrailRenderer>(true);
    }

    private void ResetVisualState()
    {
        if (_visibleRenderers != null)
        {
            for (int i = 0; i < _visibleRenderers.Length; i++)
            {
                if (_visibleRenderers[i] != null)
                {
                    _visibleRenderers[i].enabled = true;
                }
            }
        }

        if (_trails != null)
        {
            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer trail = _trails[i];
                if (trail == null)
                {
                    continue;
                }

                trail.Clear();
                trail.emitting = true;
            }
        }
    }

    private void EnsureImpactAudioSource()
    {
        if (_impactAudioSource != null)
        {
            _impactAudioSource.playOnAwake = false;
            _impactAudioSource.loop = false;
            _impactAudioSource.spatialBlend = 1f;
            _impactAudioSource.rolloffMode = AudioRolloffMode.Linear;
            return;
        }

        _impactAudioSource = GetComponent<AudioSource>();
        if (_impactAudioSource == null)
        {
            _impactAudioSource = gameObject.AddComponent<AudioSource>();
        }

        _impactAudioSource.playOnAwake = false;
        _impactAudioSource.loop = false;
        _impactAudioSource.spatialBlend = 1f;
        _impactAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private Transform AcquireTarget()
    {
        // Prefer faction-driven targeting first so the same missile prefab works in duel and Invasion flows.
        if (TargetFaction == Faction3D.PlayerTeam || TargetFaction == Faction3D.EnemyTeam)
        {
            return AcquireTargetByFaction();
        }

        if (IsSpecificPlayerTargetTag(targetTag) && NetMovement3D.TryGetPlayerByTag(targetTag, out NetMovement3D targetMovement))
        {
            return targetMovement != null ? targetMovement.transform : null;
        }

        if (targetTag == "Enemy")
        {
            Transform enemyFactionTarget = AcquireTargetByFaction(Faction3D.EnemyTeam);
            if (enemyFactionTarget != null)
            {
                return enemyFactionTarget;
            }
        }

        return AcquireTargetByFilter(TargetFaction, targetTag);
    }

    private Transform AcquireTargetByFaction()
    {
        return AcquireTargetByFaction(TargetFaction);
    }

    private Transform AcquireTargetByFaction(Faction3D targetFaction)
    {
        return AcquireTargetByFilter(targetFaction, null);
    }

    private Transform AcquireTargetByFilter(Faction3D targetFaction, string requiredTag)
    {
        Entity3D[] entities = FindObjectsByType<Entity3D>(FindObjectsSortMode.None);
        Transform bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D candidate = entities[i];
            if (candidate == null || candidate == _shooter || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (targetFaction != Faction3D.Neutral)
            {
                Faction3D candidateFaction = FactionMember3D.ResolveFaction(candidate);
                if (candidateFaction != targetFaction)
                {
                    continue;
                }
            }
            else if (!string.IsNullOrEmpty(requiredTag) && !candidate.CompareTag(requiredTag))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }

    private static bool IsSpecificPlayerTargetTag(string value)
    {
        return value == "Player1" || value == "Player2";
    }

    private void OnDrawGizmosSelected()
    {
        if (!areaDamage.enabled)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, areaDamage.explosionRadius));
    }
}
