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

    private Transform _target;
    private Vector3 _inheritedVelocity;
    private Vector3 _currentDirection;
    private float _cruiseSpeed;
    private float _spawnTime;
    private bool _isImpacted;
    private bool _impactVisualSpawned;
    private bool _endOfLifeTriggered;
    private Renderer[] _visibleRenderers;
    private ParticleSystem[] _particles;
    private TrailRenderer[] _trails;
    private AudioSource _impactAudioSource;
    private PooledObject3D _pooledObject;

    private bool UsesGuidance => guidance.mode == GuidanceMode3D.Guided;

    private void OnEnable()
    {
        _target = null;
        _inheritedVelocity = Vector3.zero;
        _currentDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        _cruiseSpeed = 0f;
        _spawnTime = Time.time;
        _isImpacted = false;
        _impactVisualSpawned = false;
        _endOfLifeTriggered = false;

        CacheVisualComponentsIfNeeded();
        ResetVisualState();
        EnsureImpactAudioSource();
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

        Vector3 toTarget = _target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 desiredDirection = toTarget.normalized;
        float turnStepRadians = Mathf.Max(0f, guidance.turnRateDegPerSecond) * Mathf.Deg2Rad * Time.deltaTime;
        _currentDirection = Vector3.RotateTowards(_currentDirection, desiredDirection, turnStepRadians, 0f).normalized;
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
}
