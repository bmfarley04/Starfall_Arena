using UnityEngine;

[DisallowMultipleComponent]
public class GuidedMissileProjectile3D : Projectile3D
{
    [System.Serializable]
    public struct GuidanceConfig3D
    {
        public bool enabled;
        public float turnRateDegPerSecond;
        public float guidanceStartDelay;
    }

    [System.Serializable]
    public struct WarmupConfig3D
    {
        public bool enabled;
        public float initialSpeed;
        public float lowSpeedDuration;
        public float accelerationDuration;
    }

    [Header("Missile Guidance")]
    [SerializeField] private GuidanceConfig3D guidance = new GuidanceConfig3D
    {
        enabled = true,
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

    private Transform _target;
    private Vector3 _inheritedVelocity;
    private Vector3 _currentDirection;
    private float _cruiseSpeed;
    private float _spawnTime;

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
        _target = AcquireTarget();
        transform.rotation = Quaternion.LookRotation(_currentDirection, Vector3.up);
    }

    protected override void Update()
    {
        UpdateGuidance();
        float speed = GetCurrentSpeed();
        _direction = _currentDirection;
        _velocity = (_currentDirection * speed) + _inheritedVelocity;
        if (_currentDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(_currentDirection, Vector3.up);
        }

        base.Update();
    }

    private void UpdateGuidance()
    {
        if (!guidance.enabled)
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

    private Transform AcquireTarget()
    {
        if (!string.IsNullOrEmpty(targetTag) && NetMovement3D.TryGetPlayerByTag(targetTag, out NetMovement3D targetMovement))
        {
            return targetMovement != null ? targetMovement.transform : null;
        }

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

            if (TargetFaction != Faction3D.Neutral)
            {
                Faction3D candidateFaction = FactionMember3D.ResolveFaction(candidate);
                if (candidateFaction != Faction3D.Neutral && candidateFaction != TargetFaction)
                {
                    continue;
                }
            }
            else if (!string.IsNullOrEmpty(targetTag) && !candidate.CompareTag(targetTag))
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
}
