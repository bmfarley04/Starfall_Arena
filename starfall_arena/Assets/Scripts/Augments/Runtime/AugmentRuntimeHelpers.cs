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
        public float expiresAt;
    }

    private readonly Dictionary<string, BurnState> _activeBurns = new Dictionary<string, BurnState>();
    private Entity _target;
    private NetMovement _targetNetMovement;

    private void Awake()
    {
        _target = GetComponent<Entity>();
        _targetNetMovement = GetComponent<NetMovement>();
    }

    public void ApplyBurn(string sourceId, Player owner, float dps, float duration)
    {
        if (_target == null || string.IsNullOrWhiteSpace(sourceId)) return;

        _activeBurns[sourceId] = new BurnState
        {
            owner = owner,
            damagePerSecond = Mathf.Max(0f, dps),
            expiresAt = Time.time + Mathf.Max(0.05f, duration)
        };
    }

    private void Update()
    {
        if (_target == null || _activeBurns.Count == 0) return;
        if (!HasDamageAuthority()) return;

        float totalDamagePerSecond = 0f;
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

            totalDamagePerSecond += state.damagePerSecond;
            if (firstOwner == null)
            {
                firstOwner = state.owner;
            }
        }

        if (expired != null)
        {
            foreach (string key in expired)
            {
                _activeBurns.Remove(key);
            }
        }

        if (totalDamagePerSecond <= 0f) return;

        float tickDamage = totalDamagePerSecond * Time.deltaTime;
        _target.TakeDirectDamage(tickDamage, 0f, _target.transform.position, DamageSource.Other, firstOwner);
    }

    private bool HasDamageAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _targetNetMovement != null && _targetNetMovement.IsServer;
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
        public float orbitAngle;
        public bool launched;
        public float respawnTime;
    }

    private readonly List<FlyerState> _flyers = new List<FlyerState>();

    private Player _owner;
    private NetMovement _ownerNetMovement;
    private Flyers _config;
    private Entity _targetEntity;
    private float _nextRetargetTime;

    public void Initialize(Player owner, Flyers config)
    {
        _owner = owner;
        _ownerNetMovement = owner != null ? owner.GetComponent<NetMovement>() : null;
        _config = config;

        int count = Mathf.Max(1, config != null ? config.flyerCount : 1);
        for (int i = 0; i < count; i++)
        {
            _flyers.Add(new FlyerState
            {
                orbitAngle = (360f / count) * i,
                launched = false,
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
                flyer.orbitAngle += orbitSpeed * Time.deltaTime;
                Vector2 orbitOffset = Quaternion.Euler(0f, 0f, flyer.orbitAngle) * Vector2.up * orbitRadius;
                flyer.visual.position = _owner.transform.position + (Vector3)orbitOffset;

                if (_targetEntity != null)
                {
                    float distanceToTarget = Vector2.Distance(flyer.visual.position, _targetEntity.transform.position);
                    if (distanceToTarget <= _config.engageRange)
                    {
                        flyer.launched = true;
                    }
                }

                continue;
            }

            if (_targetEntity == null)
            {
                flyer.launched = false;
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
        if (_targetEntity != null && !_targetEntity.IsDead)
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

    private void OnDestroy()
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