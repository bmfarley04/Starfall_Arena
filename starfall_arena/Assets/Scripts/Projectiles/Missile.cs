using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Missile : ProjectileScript
{
    [System.Serializable]
    public struct GuidanceConfig
    {
        [Tooltip("If true, missile steers toward target.")]
        public bool enabled;
        [Tooltip("Maximum turn rate in degrees/second. Lower = easier to dodge.")]
        public float turnRateDegPerSecond;
        [Tooltip("Delay before guidance starts after spawn (seconds).")]
        public float guidanceStartDelay;
    }

    [System.Serializable]
    public struct WarmupConfig
    {
        [Tooltip("If true, missile starts slow then ramps to cruise speed.")]
        public bool enabled;
        [Tooltip("Initial launch speed during warmup.")]
        public float initialSpeed;
        [Tooltip("How long missile stays at initial speed before ramp starts (seconds).")]
        public float lowSpeedDuration;
        [Tooltip("How long speed ramp takes to reach cruise speed (seconds).")]
        public float accelerationDuration;
    }

    [System.Serializable]
    public struct DespawnConfig
    {
        [Tooltip("Stop particle emission and delay despawn so trails fade naturally.")]
        public bool delayDespawn;
        [Tooltip("Delay before despawn after hit/end-of-life (seconds).")]
        public float despawnDelay;
        [Tooltip("Particles that should keep fading during delayed despawn.")]
        public ParticleSystem[] delayedParticles;
    }

    [Header("Missile Guidance")]
    [SerializeField] private GuidanceConfig guidance = new GuidanceConfig
    {
        enabled = true,
        turnRateDegPerSecond = 120f,
        guidanceStartDelay = 0.05f
    };

    [Header("Missile Warmup")]
    [SerializeField] private WarmupConfig warmup = new WarmupConfig
    {
        enabled = true,
        initialSpeed = 6f,
        lowSpeedDuration = 0.08f,
        accelerationDuration = 0.25f
    };

    [Header("Missile Despawn")]
    [SerializeField] private DespawnConfig despawn = new DespawnConfig
    {
        delayDespawn = true,
        despawnDelay = 0.35f
    };

    [Header("Impact")]
    [Tooltip("Spawned at the missile's position on any hit or end-of-life.")]
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Uniform scale multiplier applied to the spawned explosion.")]
    [SerializeField] private float explosionScale = 1f;

    private Transform _target;
    private Vector2 _inheritedVelocity;
    private Vector2 _currentDirection;
    private float _cruiseSpeed;
    private float _spawnTime;
    private bool _isHit;
    private bool _impactVisualSpawned;
    private Renderer[] _renderers;
    private ParticleSystem[] _particles;
    private Coroutine _lifetimeCoroutine;

    private void OnEnable()
    {
        _isHit = false;
        _impactVisualSpawned = false;
        _spawnTime = Time.time;
        _currentDirection = transform.up;
        _target = null;

        _renderers = GetComponentsInChildren<Renderer>(true);
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void Start()
    {
        if (_lifetimeCoroutine != null)
        {
            StopCoroutine(_lifetimeCoroutine);
        }
        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
    }

    private void FixedUpdate()
    {
        if (_isHit || _rb == null)
        {
            return;
        }

        if (_target != null && !string.IsNullOrEmpty(targetTag) && !_target.CompareTag(targetTag))
        {
            _target = null;
        }

        if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            _currentDirection = _rb.linearVelocity.normalized;
        }
        else if (_currentDirection.sqrMagnitude <= 0.0001f)
        {
            _currentDirection = transform.up;
        }

        UpdateGuidance();
        float speed = GetCurrentSpeed();
        _rb.linearVelocity = (_currentDirection * speed) + _inheritedVelocity;
    }

    public new void Initialize(Vector3 direction, Vector2 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity shooter = null, int accuracyAttackId = Player.InvalidAttackId)
    {
        base.Initialize(direction, shipVelocity, speed, damage, lifetime, impactForce, shooter, accuracyAttackId);

        _currentDirection = ((Vector2)direction).normalized;
        if (_currentDirection.sqrMagnitude <= 0.0001f)
        {
            _currentDirection = transform.up;
        }

        _inheritedVelocity = shipVelocity;
        _cruiseSpeed = speed;
        _spawnTime = Time.time;
        UpdateMissileRotation();
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void SetGuidanceEnabled(bool enabled)
    {
        guidance.enabled = enabled;
    }

    private IEnumerator LifetimeRoutine()
    {
        while (_lifetime <= 0f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(_lifetime);

        if (_isHit)
        {
            yield break;
        }

        // End-of-life despawn without impact VFX.
        _impactVisualSpawned = true;
        HandleHit();
    }

    private void UpdateGuidance()
    {
        if (!guidance.enabled || _target == null)
        {
            return;
        }

        if (Time.time < _spawnTime + Mathf.Max(0f, guidance.guidanceStartDelay))
        {
            return;
        }

        Vector2 toTarget = (Vector2)(_target.position - transform.position);
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 desiredDirection = toTarget.normalized;
        float turnStepRadians = Mathf.Max(0f, guidance.turnRateDegPerSecond) * Mathf.Deg2Rad * Time.fixedDeltaTime;
        Vector3 steered = Vector3.RotateTowards(_currentDirection, desiredDirection, turnStepRadians, 0f);
        _currentDirection = ((Vector2)steered).normalized;

        UpdateMissileRotation();
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

        float accelDuration = Mathf.Max(0.0001f, warmup.accelerationDuration);
        float t = Mathf.Clamp01((elapsed - lowDuration) / accelDuration);
        return Mathf.Lerp(safeInitial, _cruiseSpeed, t);
    }

    private void UpdateMissileRotation()
    {
        transform.rotation = GetDesiredFacingRotation();
    }

    private Quaternion GetDesiredFacingRotation()
    {
        float angle = Mathf.Atan2(_currentDirection.y, _currentDirection.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle);
    }

    protected override void OnTriggerEnter2D(Collider2D collider)
    {
        if (_isHit)
        {
            return;
        }

        if (_shooter != null && collider.gameObject == _shooter.gameObject)
        {
            return;
        }

        if (collider.CompareTag(targetTag))
        {
            Player hitPlayer = collider.GetComponent<Player>();
            if (hitPlayer != null && hitPlayer.TryGetComponent<Reflector>(out var reflectScript) && reflectScript.IsAbilityActive())
            {
                return;
            }

            Entity damageable = collider.GetComponent<Entity>();
            if (damageable == null)
            {
                return;
            }

            damageable.TakeDamage(_damage, _impactForce, transform.position, DamageSource.Projectile, _shooter, _accuracyAttackId);
            ApplyImpactForce(collider);

            if (_appliesSlow)
            {
                damageable.ApplySlow(_slowMultiplier, _slowDuration);
            }

            if (_canPierce && _pierceMultiplier > 0f)
            {
                _damage *= _pierceMultiplier;
                return;
            }

            HandleHit();
            return;
        }

        if (collider.CompareTag("Asteroid"))
        {
            AsteroidScript asteroid = collider.GetComponent<AsteroidScript>();
            if (asteroid != null)
            {
                asteroid.RequestDamage(_damage, _impactForce, transform.position);
            }

            if (_canPierce && _pierceMultiplier > 0f)
            {
                _damage *= _pierceMultiplier;
                return;
            }

            HandleHit();
        }
    }

    private void HandleHit()
    {
        if (_isHit)
        {
            return;
        }

        _isHit = true;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale *= explosionScale;
        }

        if (!_impactVisualSpawned && _visualController != null)
        {
            _visualController.OnProjectileImpact(transform.position, _currentDirection);
            _impactVisualSpawned = true;
        }

        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        if (despawn.delayDespawn)
        {
            DelayParticles();
            StartCoroutine(DespawnAfterDelay());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, despawn.despawnDelay));
        Destroy(gameObject);
    }

    // Carries over the FORGE3D delayed particle stop/clear behavior.
    private void DelayParticles()
    {
        if (_particles == null || _particles.Length == 0)
        {
            return;
        }

        ParticleSystem[] delayed = despawn.delayedParticles;

        for (int i = 0; i < _particles.Length; i++)
        {
            bool isDelayed = false;

            if (delayed != null)
            {
                for (int y = 0; y < delayed.Length; y++)
                {
                    if (_particles[i] == delayed[y])
                    {
                        isDelayed = true;
                        break;
                    }
                }
            }

            _particles[i].Stop(false);

            if (!isDelayed)
            {
                _particles[i].Clear(false);
            }
        }
    }

    private new void ApplyImpactForce(Collider2D collider)
    {
        Rigidbody2D targetRb = collider.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 forceDirection = _currentDirection.normalized;
            targetRb.linearVelocity += forceDirection * (_impactForce / targetRb.mass);
        }
    }
}
