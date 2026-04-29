using UnityEngine;

[DisallowMultipleComponent]
public class EnemyFlamethrowerWeapon3D : MonoBehaviour
{
    private static readonly Collider[] FlameHits = new Collider[32];

    [Header("Flame Gameplay")]
    [Tooltip("Faction damaged by this enemy flamethrower. In Invasion this should be PlayerTeam.")]
    [SerializeField] private Faction3D targetFaction = Faction3D.PlayerTeam;

    [Tooltip("Optional legacy tag fallback used only when Target Faction is Neutral. Leave empty for new Invasion enemies.")]
    [SerializeField] private string targetTag = string.Empty;

    [Tooltip("Layers that contain player-team ship colliders. Keep this mask narrow so the cone query stays cheap.")]
    [SerializeField] private LayerMask damageMask = ~0;

    [Tooltip("Damage applied per second while a valid player-team target remains inside the flame cone.")]
    [SerializeField] private float damagePerSecond = 24f;

    [Tooltip("Maximum flame damage reach in meters from the muzzle.")]
    [SerializeField] private float range = 32f;

    [Tooltip("Half-angle of the flame cone in degrees. A 25 degree value creates a 50 degree total cone.")]
    [SerializeField, Range(0f, 90f)] private float halfAngleDegrees = 24f;

    [Tooltip("Seconds between damage ticks. Lower values feel smoother but cost more physics and damage work.")]
    [SerializeField] private float damageTickInterval = 0.15f;

    [Tooltip("Seconds a single flame burst stays active once started.")]
    [SerializeField] private float burstDuration = 1.5f;

    [Tooltip("Seconds after a burst starts before another burst can begin.")]
    [SerializeField] private float cooldown = 3f;

    [Header("Flame Visuals")]
    [Tooltip("Authored flamethrower particle prefab spawned under the muzzle. Use 3d_flamethrower.prefab for the current enemy.")]
    [SerializeField] private GameObject flameVisualPrefab;

    [Tooltip("Muzzle transform whose local forward points down the intended flame lane. Falls back to this transform if unset.")]
    [SerializeField] private Transform muzzle;

    [Tooltip("Local position applied to the spawned flame visual under the muzzle.")]
    [SerializeField] private Vector3 visualLocalPosition;

    [Tooltip("Local Euler rotation applied to the spawned flame visual under the muzzle.")]
    [SerializeField] private Vector3 visualLocalEulerAngles;

    [Tooltip("Local scale applied to the spawned flame visual under the muzzle.")]
    [SerializeField] private Vector3 visualLocalScale = Vector3.one;

    [Header("Debug")]
    [Tooltip("If enabled, selected-scene gizmos show the flame range and cone edges.")]
    [SerializeField] private bool drawConeGizmo = true;

    private readonly Entity3D[] _damagedThisTick = new Entity3D[16];

    private Entity3D _owner;
    private GameObject _visualInstance;
    private ParticleSystem[] _visualParticles;
    private Light[] _visualLights;
    private bool _visualsActive;
    private bool _damageAuthoritative;
    private bool _isBurstActive;
    private bool _loggedMissingVisual;
    private float _burstEndTime = float.NegativeInfinity;
    private float _nextAllowedStartTime;
    private float _nextDamageTickTime;

    public bool IsBurstActive => _isBurstActive;
    public bool IsOnCooldown => Time.time < _nextAllowedStartTime;
    public float Range => range;
    public float BurstDuration => burstDuration;

    private void Awake()
    {
        _owner = GetComponent<Entity3D>();
        EnsureVisualInstance();
        StopVisuals(clearParticles: true);
    }

    private void OnValidate()
    {
        damagePerSecond = Mathf.Max(0f, damagePerSecond);
        range = Mathf.Max(0f, range);
        halfAngleDegrees = Mathf.Clamp(halfAngleDegrees, 0f, 90f);
        damageTickInterval = Mathf.Max(0.02f, damageTickInterval);
        burstDuration = Mathf.Max(0.01f, burstDuration);
        cooldown = Mathf.Max(0f, cooldown);
        if (visualLocalScale == Vector3.zero)
        {
            visualLocalScale = Vector3.one;
        }
    }

    private void Update()
    {
        if (!_isBurstActive)
        {
            return;
        }

        if (Time.time >= _burstEndTime)
        {
            StopBurst();
            return;
        }

        if (_damageAuthoritative && Time.time >= _nextDamageTickTime)
        {
            ApplyConeDamage();
            _nextDamageTickTime = Time.time + Mathf.Max(0.02f, damageTickInterval);
        }
    }

    private void OnDisable()
    {
        StopBurst();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.FlamethrowerWeaponStats stats)
    {
        damagePerSecond = Mathf.Max(0f, stats.damagePerSecond);
        range = Mathf.Max(0f, stats.range);
        halfAngleDegrees = Mathf.Clamp(stats.halfAngleDegrees, 0f, 90f);
        damageTickInterval = Mathf.Max(0.02f, stats.damageTickInterval);
        burstDuration = Mathf.Max(0.01f, stats.burstDuration);
        cooldown = Mathf.Max(0f, stats.cooldown);
    }

    public bool CanStartBurst()
    {
        return !_isBurstActive && Time.time >= _nextAllowedStartTime;
    }

    public bool TryStartBurst(bool authoritativeDamage)
    {
        if (!CanStartBurst())
        {
            return false;
        }

        ApplyNetworkFlameState(true, authoritativeDamage);
        return true;
    }

    public void ApplyNetworkFlameState(bool isFiring, bool authoritativeDamage)
    {
        if (isFiring)
        {
            StartBurst(authoritativeDamage);
        }
        else
        {
            StopBurst();
        }
    }

    private void StartBurst(bool authoritativeDamage)
    {
        _damageAuthoritative = authoritativeDamage;
        _isBurstActive = true;
        _burstEndTime = Time.time + Mathf.Max(0.01f, burstDuration);
        _nextAllowedStartTime = Time.time + Mathf.Max(cooldown, burstDuration);
        _nextDamageTickTime = Time.time;
        StartVisuals();
    }

    public void StopBurst()
    {
        if (!_isBurstActive && !_visualsActive)
        {
            return;
        }

        _isBurstActive = false;
        _damageAuthoritative = false;
        StopVisuals(clearParticles: false);
    }

    private void ApplyConeDamage()
    {
        Transform origin = ResolveMuzzle();
        if (origin == null || range <= 0f || damagePerSecond <= 0f)
        {
            return;
        }

        Vector3 flameOrigin = origin.position;
        Vector3 flameForward = ResolveForward(origin);
        int damagedCount = 0;
        int hitCount = Physics.OverlapSphereNonAlloc(
            flameOrigin,
            range,
            FlameHits,
            damageMask,
            QueryTriggerInteraction.Ignore);

        float damageThisTick = damagePerSecond * Mathf.Max(0.02f, damageTickInterval);
        float allowedAngle = Mathf.Max(0f, halfAngleDegrees);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = FlameHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Entity3D target = hit.GetComponentInParent<Entity3D>();
            if (target == null || target == _owner || target.CurrentHealth <= 0f || !IsMatchingTarget(target))
            {
                continue;
            }

            if (WasAlreadyDamaged(target, damagedCount))
            {
                continue;
            }

            Vector3 targetPoint = hit.bounds.center;
            Vector3 toTarget = targetPoint - flameOrigin;
            if (toTarget.sqrMagnitude <= 0.0001f || toTarget.magnitude > range)
            {
                continue;
            }

            if (Vector3.Angle(flameForward, toTarget.normalized) > allowedAngle)
            {
                continue;
            }

            target.TakeDamage(damageThisTick, targetPoint, _owner, DamageSource3D.Beam, PlayerCombatStats3D.InvalidAttackId);
            if (damagedCount < _damagedThisTick.Length)
            {
                _damagedThisTick[damagedCount++] = target;
            }
        }

        for (int i = 0; i < damagedCount; i++)
        {
            _damagedThisTick[i] = null;
        }
    }

    private bool IsMatchingTarget(Entity3D target)
    {
        if (targetFaction != Faction3D.Neutral)
        {
            return FactionMember3D.ResolveFaction(target) == targetFaction;
        }

        return !string.IsNullOrEmpty(targetTag) && target.CompareTag(targetTag);
    }

    private bool WasAlreadyDamaged(Entity3D target, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (_damagedThisTick[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureVisualInstance()
    {
        if (_visualInstance != null)
        {
            return;
        }

        if (flameVisualPrefab == null)
        {
            if (!_loggedMissingVisual)
            {
                Debug.LogWarning($"[{nameof(EnemyFlamethrowerWeapon3D)}] {name} has no flame visual prefab assigned.", this);
                _loggedMissingVisual = true;
            }
            return;
        }

        Transform parent = ResolveMuzzle();
        _visualInstance = Instantiate(flameVisualPrefab, parent != null ? parent : transform);
        _visualInstance.transform.localPosition = visualLocalPosition;
        _visualInstance.transform.localRotation = Quaternion.Euler(visualLocalEulerAngles);
        _visualInstance.transform.localScale = visualLocalScale;
        _visualParticles = _visualInstance.GetComponentsInChildren<ParticleSystem>(true);
        _visualLights = _visualInstance.GetComponentsInChildren<Light>(true);
    }

    private void StartVisuals()
    {
        EnsureVisualInstance();
        if (_visualInstance == null)
        {
            return;
        }

        _visualInstance.SetActive(true);
        for (int i = 0; i < _visualParticles.Length; i++)
        {
            ParticleSystem particle = _visualParticles[i];
            if (particle != null)
            {
                particle.Play(withChildren: true);
            }
        }

        for (int i = 0; i < _visualLights.Length; i++)
        {
            if (_visualLights[i] != null)
            {
                _visualLights[i].enabled = true;
            }
        }

        _visualsActive = true;
    }

    private void StopVisuals(bool clearParticles)
    {
        if (_visualInstance == null)
        {
            return;
        }

        ParticleSystemStopBehavior stopBehavior = clearParticles
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < _visualParticles.Length; i++)
        {
            ParticleSystem particle = _visualParticles[i];
            if (particle != null)
            {
                particle.Stop(withChildren: true, stopBehavior);
            }
        }

        for (int i = 0; i < _visualLights.Length; i++)
        {
            if (_visualLights[i] != null)
            {
                _visualLights[i].enabled = false;
            }
        }

        _visualsActive = false;
    }

    private Transform ResolveMuzzle()
    {
        return muzzle != null ? muzzle : transform;
    }

    private static Vector3 ResolveForward(Transform origin)
    {
        return origin != null && origin.forward.sqrMagnitude > 0.0001f
            ? origin.forward.normalized
            : Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawConeGizmo)
        {
            return;
        }

        Transform origin = ResolveMuzzle();
        if (origin == null)
        {
            return;
        }

        Vector3 start = origin.position;
        Vector3 forward = ResolveForward(origin);
        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.9f);
        Gizmos.DrawWireSphere(start, range);
        Gizmos.DrawRay(start, forward * range);

        Quaternion left = Quaternion.AngleAxis(-halfAngleDegrees, origin.up);
        Quaternion right = Quaternion.AngleAxis(halfAngleDegrees, origin.up);
        Quaternion up = Quaternion.AngleAxis(-halfAngleDegrees, origin.right);
        Quaternion down = Quaternion.AngleAxis(halfAngleDegrees, origin.right);
        Gizmos.DrawRay(start, left * forward * range);
        Gizmos.DrawRay(start, right * forward * range);
        Gizmos.DrawRay(start, up * forward * range);
        Gizmos.DrawRay(start, down * forward * range);
    }
}
