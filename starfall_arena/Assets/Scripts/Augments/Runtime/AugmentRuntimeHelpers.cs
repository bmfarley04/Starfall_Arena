using System;
using System.Collections.Generic;
using UnityEngine;

public interface IProjectileReflectorAugment
{
    bool TryReflectProjectile(ProjectileScript projectile, Vector2 hitPoint);
}

public sealed class WeakmakerExposureTracker : MonoBehaviour
{
    private struct ExposureState
    {
        public float multiplier;
        public float expiresAt;
    }

    private readonly Dictionary<string, ExposureState> _statesBySource = new Dictionary<string, ExposureState>();

    public void ApplyExposure(string sourceId, float multiplier, float duration)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return;

        _statesBySource[sourceId] = new ExposureState
        {
            multiplier = Mathf.Max(1f, multiplier),
            expiresAt = Time.time + Mathf.Max(0.01f, duration)
        };
    }

    public float GetCombinedMultiplier()
    {
        PruneExpired();

        float total = 1f;
        foreach (ExposureState state in _statesBySource.Values)
        {
            total *= state.multiplier;
        }

        return total;
    }

    private void LateUpdate()
    {
        PruneExpired();
    }

    private void PruneExpired()
    {
        if (_statesBySource.Count == 0) return;

        List<string> expired = null;
        foreach (KeyValuePair<string, ExposureState> entry in _statesBySource)
        {
            if (Time.time > entry.Value.expiresAt)
            {
                expired ??= new List<string>();
                expired.Add(entry.Key);
            }
        }

        if (expired == null) return;
        foreach (string key in expired)
        {
            _statesBySource.Remove(key);
        }
    }
}

public sealed class BurnerDebuffController : MonoBehaviour
{
    private sealed class BurnState
    {
        public Player owner;
        public float damagePerSecond;
        public float tickInterval;
        public float nextTickAt;
        public float expiresAt;
        public GameObject tickEffectPrefab;
        public float tickEffectRandomRadius;
    }

    private readonly Dictionary<string, BurnState> _activeBurns = new Dictionary<string, BurnState>();
    private Entity _target;
    private NetMovement _targetNetMovement;

    private void Awake()
    {
        _target = GetComponent<Entity>();
        _targetNetMovement = GetComponent<NetMovement>();
    }

    public void ApplyBurn(string sourceId, Player owner, float dps, float duration, float tickInterval, GameObject tickEffectPrefab, float tickEffectRandomRadius)
    {
        if (_target == null || string.IsNullOrWhiteSpace(sourceId)) return;

        float safeTickInterval = Mathf.Max(0.05f, tickInterval);
        float safeDps = Mathf.Max(0f, dps);
        float refreshedExpireTime = Time.time + Mathf.Max(0.05f, duration);

        if (_activeBurns.TryGetValue(sourceId, out BurnState existingState))
        {
            existingState.owner = owner ?? existingState.owner;
            existingState.damagePerSecond = safeDps;
            existingState.tickInterval = safeTickInterval;
            existingState.tickEffectPrefab = tickEffectPrefab;
            existingState.tickEffectRandomRadius = tickEffectRandomRadius;

            // Refresh duration without pushing the next scheduled tick farther out.
            existingState.expiresAt = Mathf.Max(existingState.expiresAt, refreshedExpireTime);
            existingState.nextTickAt = Mathf.Min(existingState.nextTickAt, Time.time + safeTickInterval);
            return;
        }

        _activeBurns[sourceId] = new BurnState
        {
            owner = owner,
            damagePerSecond = safeDps,
            tickInterval = safeTickInterval,
            nextTickAt = Time.time + safeTickInterval,
            expiresAt = refreshedExpireTime,
            tickEffectPrefab = tickEffectPrefab,
            tickEffectRandomRadius = tickEffectRandomRadius
        };
    }

    private void Update()
    {
        if (_target == null || _activeBurns.Count == 0) return;
        if (!HasDamageAuthority()) return;

        float totalTickDamage = 0f;
        List<string> expired = null;
        Player firstOwner = null;

        foreach (KeyValuePair<string, BurnState> entry in _activeBurns)
        {
            BurnState state = entry.Value;
            if (Time.time > state.expiresAt)
            {
                expired ??= new List<string>();
                expired.Add(entry.Key);
                continue;
            }

            float interval = Mathf.Max(0.05f, state.tickInterval);
            if (Time.time < state.nextTickAt)
            {
                continue;
            }

            int dueTicks = Mathf.Max(1, Mathf.FloorToInt((Time.time - state.nextTickAt) / interval) + 1);
            state.nextTickAt += dueTicks * interval;
            entry.Value.nextTickAt = state.nextTickAt;

            totalTickDamage += state.damagePerSecond * interval * dueTicks;
            if (firstOwner == null && state.owner != null)
            {
                firstOwner = state.owner;
            }

            for (int tick = 0; tick < dueTicks; tick++)
            {
                SpawnBurnTickEffect(state.tickEffectPrefab, state.tickEffectRandomRadius);
            }
        }

        if (expired != null)
        {
            foreach (string key in expired)
            {
                _activeBurns.Remove(key);
            }
        }

        if (totalTickDamage <= 0f) return;

        _target.TakeDamage(totalTickDamage, 0f, _target.transform.position, DamageSource.Other, firstOwner);
        try
        {
            string ownerName = firstOwner != null ? firstOwner.name : "(unknown)";
            float currentHealth = 0f;
            float currentShield = 0f;
            try
            {
                if (_target != null)
                {
                    currentHealth = _target.CurrentHealth;
                    currentShield = _target.currentShield;
                }
            }
            catch
            {
                currentHealth = 0f;
                currentShield = 0f;
            }

            Debug.Log($"[Burner] Applied burn tick {totalTickDamage:F2} to {_target.gameObject.name} (health={currentHealth:F2}, shield={currentShield:F2}) from {ownerName}");
        }
        catch (Exception)
        {
        }
    }

    private bool HasDamageAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _targetNetMovement != null && _targetNetMovement.IsServer;
    }

    private void SpawnBurnTickEffect(GameObject tickEffectPrefab, float randomRadius)
    {
        if (_target == null || tickEffectPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = ResolveRandomTargetPoint(randomRadius);
        GameObject effect = Instantiate(tickEffectPrefab, spawnPos, Quaternion.identity);

        float size = 1f;
        if (_target is Player targetPlayer)
        {
            size = Mathf.Max(0.01f, targetPlayer.ShipSize);
        }
        effect.transform.localScale = tickEffectPrefab.transform.localScale * size;

        ParticleSystem particle = effect.GetComponent<ParticleSystem>();
        float lifetime = 1.5f;
        if (particle != null)
        {
            lifetime = Mathf.Max(0.1f, particle.main.duration + particle.main.startLifetime.constantMax);
        }

        Destroy(effect, lifetime);
    }

    private Vector3 ResolveRandomTargetPoint(float fallbackRadius)
    {
        if (_target == null)
        {
            return Vector3.zero;
        }

        Transform visualModel = _target.visualEffects.visualModel != null ? _target.visualEffects.visualModel : _target.transform;
        Renderer[] renderers = visualModel.GetComponentsInChildren<Renderer>();

        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                _target.transform.position.z);
        }

        Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, fallbackRadius);
        return _target.transform.position + new Vector3(offset.x, offset.y, 0f);
    }
}

public sealed class AutoCounterReflectorController : MonoBehaviour, IProjectileReflectorAugment
{
    public event Action<Vector2> OnProjectileReflected;

    private Player _owner;
    private ReflectShield _shield;
    private Color _reflectColor;
    private bool _active;

    public void Initialize(Player owner, ReflectShield shieldPrefab, Color reflectColor)
    {
        _owner = owner;
        _reflectColor = reflectColor;

        if (_shield != null)
        {
            Destroy(_shield.gameObject);
            _shield = null;
        }

        if (shieldPrefab != null)
        {
            _shield = Instantiate(shieldPrefab, owner.transform);
            _shield.transform.localPosition = Vector3.zero;
            _shield.transform.localRotation = Quaternion.identity;
            _shield.gameObject.SetActive(true);
            _shield.Deactivate();
        }
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (_shield == null) return;

        if (active)
        {
            _shield.Activate(_reflectColor);
        }
        else
        {
            _shield.Deactivate();
        }
    }

    public bool TryReflectProjectile(ProjectileScript projectile, Vector2 hitPoint)
    {
        if (!_active || projectile == null || _owner == null) return false;

        if (_shield != null)
        {
            _shield.OnReflectHit(hitPoint);
            _shield.ReflectProjectile(projectile, _owner.enemyTag);
        }
        else
        {
            projectile.Reflect(_owner.enemyTag, _reflectColor, _owner);
        }

        projectile.MarkAsReflected();
        OnProjectileReflected?.Invoke(hitPoint);
        return true;
    }

    private void OnDestroy()
    {
        if (_shield != null)
        {
            Destroy(_shield.gameObject);
        }
    }
}

public sealed class FlyersSwarmController : MonoBehaviour
{
    private sealed class FlyerState
    {
        public Transform visual;
        public int orbitSlotIndex;
        public bool launched;
        public float launchStartTime;
        public float respawnTime;
    }

    private readonly List<FlyerState> _flyers = new List<FlyerState>();

    private Player _owner;
    private NetMovement _ownerNetMovement;
    private Flyers _config;
    private Entity _targetEntity;
    private float _nextRetargetTime;
    private float _orbitPhase;

    public void Initialize(Player owner, Flyers config)
    {
        _owner = owner;
        _ownerNetMovement = owner != null ? owner.GetComponent<NetMovement>() : null;
        _config = config;
        _targetEntity = null;
        _nextRetargetTime = 0f;
        _orbitPhase = 0f;

        ClearFlyers();

        int count = Mathf.Max(1, config != null ? config.flyerCount : 1);
        for (int i = 0; i < count; i++)
        {
            _flyers.Add(new FlyerState
            {
                orbitSlotIndex = i,
                launched = false,
                launchStartTime = -999f,
                respawnTime = 0f,
                visual = CreateVisual(i)
            });
        }
    }

    private Transform CreateVisual(int index)
    {
        GameObject go;
        if (_config != null && _config.flyerPrefab != null)
        {
            go = Instantiate(_config.flyerPrefab, transform);
        }
        else
        {
            go = new GameObject($"Flyer_{index}");
            go.transform.SetParent(transform, false);
        }

        return go.transform;
    }

    private void Update()
    {
        if (_owner == null || _config == null) return;

        UpdateTarget();

        float orbitRadius = Mathf.Max(0.1f, _config.orbitRadius);
        float orbitSpeed = _config.orbitSpeed;
        float hitRadius = Mathf.Max(0.05f, _config.hitRadius);
        float homingSpeed = Mathf.Max(0.1f, _config.homingSpeed);
        float orbitSlotSpacing = _flyers.Count > 0 ? 360f / _flyers.Count : 360f;

        _orbitPhase += orbitSpeed * Time.deltaTime;
        if (_orbitPhase > 360f || _orbitPhase < -360f)
        {
            _orbitPhase %= 360f;
        }

        foreach (FlyerState flyer in _flyers)
        {
            if (flyer.visual == null) continue;

            if (flyer.respawnTime > Time.time)
            {
                flyer.visual.gameObject.SetActive(false);
                continue;
            }

            flyer.visual.gameObject.SetActive(true);

            if (!flyer.launched)
            {
                float slotAngle = _orbitPhase + (flyer.orbitSlotIndex * orbitSlotSpacing);
                Vector2 orbitOffset = Quaternion.Euler(0f, 0f, slotAngle) * Vector2.up * orbitRadius;
                flyer.visual.position = _owner.transform.position + (Vector3)orbitOffset;

                if (_targetEntity != null)
                {
                    float distanceToTarget = Vector2.Distance(flyer.visual.position, _targetEntity.transform.position);
                    if (distanceToTarget <= _config.engageRange)
                    {
                        flyer.launched = true;
                        flyer.launchStartTime = Time.time;
                    }
                }

                continue;
            }

            if (_targetEntity == null)
            {
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                continue;
            }

            float homingDuration = Mathf.Max(0.05f, _config.homingDuration);
            if (Time.time >= flyer.launchStartTime + homingDuration)
            {
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                flyer.respawnTime = Time.time + Mathf.Max(0.1f, _config.autocastInterval);
                continue;
            }

            Vector2 current = flyer.visual.position;
            Vector2 target = _targetEntity.transform.position;
            Vector2 next = Vector2.MoveTowards(current, target, homingSpeed * Time.deltaTime);
            flyer.visual.position = next;

            if (Vector2.Distance(next, target) <= hitRadius)
            {
                ApplyHit(_targetEntity, next);
                flyer.launched = false;
                flyer.launchStartTime = -999f;
                flyer.respawnTime = Time.time + Mathf.Max(0.1f, _config.autocastInterval);
            }
        }
    }

    private void ApplyHit(Entity target, Vector2 hitPoint)
    {
        if (target == null || !HasDamageAuthority()) return;

        target.TakeDamage(_config.hitDamage, _config.impactForce, hitPoint, DamageSource.Other, _owner);
    }

    private void UpdateTarget()
    {
        if (_targetEntity != null &&
            _targetEntity.CurrentHealth > 0f &&
            _targetEntity.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.time < _nextRetargetTime)
        {
            return;
        }

        _nextRetargetTime = Time.time + 0.35f;
        _targetEntity = null;

        if (string.IsNullOrWhiteSpace(_owner.enemyTag))
        {
            return;
        }

        GameObject targetObj = GameObject.FindGameObjectWithTag(_owner.enemyTag);
        if (targetObj == null)
        {
            return;
        }

        _targetEntity = targetObj.GetComponent<Entity>();
    }

    private bool HasDamageAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _ownerNetMovement != null && _ownerNetMovement.IsServer;
    }

    private void OnDisable()
    {
        foreach (FlyerState flyer in _flyers)
        {
            if (flyer?.visual != null)
            {
                flyer.visual.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        ClearFlyers();
    }

    private void ClearFlyers()
    {
        foreach (FlyerState flyer in _flyers)
        {
            if (flyer?.visual != null)
            {
                Destroy(flyer.visual.gameObject);
            }
        }

        _flyers.Clear();
    }
}