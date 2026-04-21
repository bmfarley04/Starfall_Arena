using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlazeOfGloryRuntime : AugmentRuntimeBase
{
    private readonly BlazeOfGlory _definition;

    public BlazeOfGloryRuntime(BlazeOfGlory definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        bool isActive = IsActive();

        if (_definition.bogEffect != null)
        {
            _definition.bogEffect.SetActive(isActive);
            _definition.bogEffect.transform.position = player.transform.position;
        }

        if (isActive && !player.damageMultipliers.ContainsKey(Definition.augmentID))
        {
            AddMultiplier(_definition.damageMultiplier, player.damageMultipliers);
        }
        else if (!isActive && player.damageMultipliers.ContainsKey(Definition.augmentID))
        {
            RemoveMultiplier(player.damageMultipliers);
        }
    }

    private bool IsActive()
    {
        if (!IsActiveByRounds()) return false;
        if (player.maxHealth <= 0f) return false;

        return player.CurrentHealth / player.maxHealth < _definition.healthThreshold;
    }
}

public sealed class AutoShieldsRuntime : AugmentRuntimeBase
{
    private readonly AutoShields _definition;
    private int _lastUsedRound = int.MinValue;

    public AutoShieldsRuntime(AutoShields definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _lastUsedRound = int.MinValue;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;
        if (!IsActiveByRounds() || !_definition.enabled) return;
        if (player.maxShield <= 0f) return;

        if (player.currentShield <= 0f && _lastUsedRound != player.currentRound)
        {
            player.SetShieldValue(player.maxShield);

            if (player.shieldController != null)
            {
                player.shieldController.SetRegeneration(false);
            }

            _lastUsedRound = player.currentRound;
        }
    }
}

public sealed class CloakRuntime : AugmentRuntimeBase
{
    private readonly Cloak _definition;
    private float _speedBoostEndTime;

    public CloakRuntime(Cloak definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        if (player == null) return;
        if (!IsActiveByRounds()) return;

        _speedBoostEndTime = Time.time + _definition.boostDuration;
        AddOrRefreshMultiplier(_definition.speedMultiplier, player.speedMultipliers);
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (player.speedMultipliers.ContainsKey(Definition.augmentID) && Time.time >= _speedBoostEndTime)
        {
            RemoveMultiplier(player.speedMultipliers);
        }
    }
}

public sealed class DaggerRuntime : AugmentRuntimeBase
{
    private readonly Dagger _definition;
    private float _damageBoostEndTime;

    public DaggerRuntime(Dagger definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        if (player == null) return;
        if (!IsActiveByRounds()) return;

        _damageBoostEndTime = Time.time + _definition.boostDuration;
        AddOrRefreshMultiplier(_definition.damageMultiplier, player.damageMultipliers);
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (player.damageMultipliers.ContainsKey(Definition.augmentID) && Time.time >= _damageBoostEndTime)
        {
            RemoveMultiplier(player.damageMultipliers);
        }
    }
}

public sealed class EvasionRuntime : AugmentRuntimeBase
{
    private readonly Evasion _definition;

    public EvasionRuntime(Evasion definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source)
    {
        if (player == null || !IsActiveByRounds()) return;

        if (!shieldIgnored && UnityEngine.Random.value < _definition.shieldIgnoreChance)
        {
            shieldIgnored = true;
        }

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
        }
    }

    public override void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source)
    {
        if (player == null || !IsActiveByRounds()) return;

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
        }
    }
}

public sealed class RegeneratorRuntime : AugmentRuntimeBase
{
    private readonly Regenerator _definition;
    private float _lastDamageTime;
    private float _anchorStartTime;

    public RegeneratorRuntime(Regenerator definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _lastDamageTime = -999f;
        _anchorStartTime = -999f;
    }

    public override void ExecuteEffects()
    {
        if (!IsActiveByRounds()) return;
        if (player == null) return;

        if (player.IsAnchored)
        {
            if (_anchorStartTime < 0f) _anchorStartTime = Time.time;

            if (Time.time >= _anchorStartTime + _definition.healDelay &&
                Time.time >= _lastDamageTime + _definition.damageInterruptCooldown)
            {
                float amount = _definition.healRate * Time.deltaTime;
                player.Heal(amount);
            }
        }
        else
        {
            _anchorStartTime = -999f;
        }
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        _lastDamageTime = Time.time;
    }
}

public sealed class ReinforcedHullRuntime : AugmentRuntimeBase
{
    private readonly ReinforcedHull _definition;
    private float _appliedAmount;
    private bool _isApplied;

    public ReinforcedHullRuntime(ReinforcedHull definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (IsActiveByRounds())
        {
            if (!_isApplied)
            {
                ApplyHealthBonus();
            }
        }
        else if (_isApplied)
        {
            RemoveHealthBonus();
        }
    }

    private void ApplyHealthBonus()
    {
        float originalMax = player.maxHealth;
        _appliedAmount = originalMax * (_definition.healthMultiplier - 1f);

        player.SetMaxHealthAndClampCurrent(originalMax + _appliedAmount);
        player.Heal(_appliedAmount);

        _isApplied = true;
    }

    private void RemoveHealthBonus()
    {
        player.SetMaxHealthAndClampCurrent(player.maxHealth - _appliedAmount);

        _appliedAmount = 0f;
        _isApplied = false;
    }
}

public sealed class RotatorRuntime : AugmentRuntimeBase
{
    private readonly Rotator _definition;

    public RotatorRuntime(Rotator definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (IsActiveByRounds() && !player.rotationMultipliers.ContainsKey(Definition.augmentID))
        {
            AddMultiplier(_definition.rotationMultiplier, player.rotationMultipliers);
        }
        else if (!IsActiveByRounds() && player.rotationMultipliers.ContainsKey(Definition.augmentID))
        {
            RemoveMultiplier(player.rotationMultipliers);
        }
    }
}

public sealed class ThornsRuntime : AugmentRuntimeBase
{
    private readonly Thorns _definition;
    private readonly Dictionary<GameObject, float> _hitTimers = new Dictionary<GameObject, float>();

    public ThornsRuntime(Thorns definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnContact(Collision2D collision)
    {
        if (!IsActiveByRounds()) return;
        if (player == null) return;

        Collider2D otherCollider = collision.collider;
        if (otherCollider == null) return;

        GameObject other = otherCollider.gameObject;

        if (_hitTimers.ContainsKey(other) && Time.time < _hitTimers[other])
        {
            return;
        }

        if (other == player.gameObject) return;

        Entity target = other.GetComponent<Entity>();
        if (target == null) return;

        Vector3 hitPoint = otherCollider.ClosestPoint(player.transform.position);
        target.TakeDamage(_definition.contactDamage, _definition.contactImpactForce, hitPoint, DamageSource.Other);

        _hitTimers[other] = Time.time + _definition.hitCooldown;
    }
}

[Serializable]
public sealed class ArtificialFairyPersistentState
{
    public bool triggered;
}

public sealed class ArtificialFairyRuntime : AugmentRuntimeBase
{
    private readonly ArtificialFairy _definition;

    private bool _triggered;
    private float _originalShield;
    private bool _restoreShieldNextFixedUpdate;

    public ArtificialFairyRuntime(ArtificialFairy definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (_restoreShieldNextFixedUpdate && player != null)
        {
            player.SetShieldValue(_originalShield);
            _restoreShieldNextFixedUpdate = false;
        }
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        if (player == null) return;
        if (!IsActive()) return;

        float availableShield = player.currentShield;
        float shieldAbsorb = Mathf.Min(availableShield, damage);
        float damageToHealth = damage - shieldAbsorb;

        if (damageToHealth >= player.CurrentHealth)
        {
            _triggered = true;

            _originalShield = player.currentShield;
            player.SetShieldValue(player.currentShield + damage + 1f, notify: false, clampToMax: false);

            float target = player.maxHealth * _definition.healFraction;
            float amountToHeal = target - player.CurrentHealth;
            if (amountToHeal > 0f)
            {
                player.Heal(amountToHeal);
            }

            _restoreShieldNextFixedUpdate = true;

            Debug.Log($"{Definition.augmentName} triggered: prevented death and healed to {Mathf.CeilToInt(target)} HP");
        }
    }

    public override object CapturePersistentState()
    {
        return new ArtificialFairyPersistentState
        {
            triggered = _triggered
        };
    }

    protected override void LoadPersistentState(object persistentState)
    {
        if (persistentState is ArtificialFairyPersistentState state)
        {
            _triggered = state.triggered;
        }
        else
        {
            _triggered = false;
        }

        _restoreShieldNextFixedUpdate = false;
    }

    private bool IsActive()
    {
        return IsActiveByRounds() && !_triggered;
    }
}

public sealed class AugmentorRuntime : AugmentRuntimeBase
{
    public AugmentorRuntime(Augmentor definition) : base(definition) { }
}

public sealed class BubbleShieldRuntime : AugmentRuntimeBase
{
    private readonly BubbleShield _definition;
    private float _anchoredDamageTaken;
    private float _stunEndTime;

    public BubbleShieldRuntime(BubbleShield definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _anchoredDamageTaken = 0f;
        _stunEndTime = -999f;
    }

    public override void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source)
    {
        ApplyAnchoredMitigation(ref damage);
    }

    public override void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source)
    {
        if (healthIgnored) return;
        ApplyAnchoredMitigation(ref damage);
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (!IsActiveByRounds())
        {
            RemoveMultiplier(player.speedMultipliers);
            RemoveMultiplier(player.rotationMultipliers);
            _anchoredDamageTaken = 0f;
            return;
        }

        if (IsStunned())
        {
            player.ForceAnchorState(false);

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            AddOrRefreshMultiplier(_definition.stunnedSpeedMultiplier, player.speedMultipliers);
            AddOrRefreshMultiplier(_definition.stunnedRotationMultiplier, player.rotationMultipliers);
            return;
        }

        RemoveMultiplier(player.speedMultipliers);
        RemoveMultiplier(player.rotationMultipliers);

        if (!player.IsAnchored)
        {
            _anchoredDamageTaken = 0f;
        }
    }

    private bool IsStunned()
    {
        return Time.time < _stunEndTime;
    }

    private void ApplyAnchoredMitigation(ref float damage)
    {
        if (player == null || !IsActiveByRounds()) return;
        if (!player.IsAnchored) return;
        if (IsStunned()) return;

        float incomingDamage = Mathf.Max(0f, damage);
        _anchoredDamageTaken += incomingDamage;
        damage = incomingDamage * Mathf.Max(0f, _definition.anchoredDamageMultiplier);

        if (_anchoredDamageTaken >= _definition.damageThresholdBeforeStun)
        {
            TriggerStun();
        }
    }

    private void TriggerStun()
    {
        _anchoredDamageTaken = 0f;
        _stunEndTime = Time.time + Mathf.Max(0.1f, _definition.stunDuration);
        player.ForceAnchorState(false);
    }
}

public sealed class WeakmakerRuntime : AugmentRuntimeBase
{
    private readonly Weakmaker _definition;
    private readonly RaycastHit2D[] _raycastHits = new RaycastHit2D[8];
    private ContactFilter2D _raycastFilter;
    private LineRenderer _lineRenderer;
    private Material _lineMaterial;
    private NetMovement _netMovement;

    public WeakmakerRuntime(Weakmaker definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);

        _netMovement = player != null ? player.GetComponent<NetMovement>() : null;
        _raycastFilter = ContactFilter2D.noFilter;
        _raycastFilter.useTriggers = Physics2D.queriesHitTriggers;
        if (player == null) return;

        Transform pointerTransform = player.transform.Find("WeakmakerPointer");
        GameObject pointerObject = pointerTransform != null ? pointerTransform.gameObject : new GameObject("WeakmakerPointer");

        if (pointerTransform == null)
        {
            pointerObject.transform.SetParent(player.transform, false);
        }

        pointerObject.SetActive(true);
        pointerObject.layer = player.gameObject.layer;

        _lineRenderer = pointerObject.GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            _lineRenderer = pointerObject.AddComponent<LineRenderer>();
        }

        _lineRenderer.positionCount = 2;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.startWidth = _definition.pointerWidth;
        _lineRenderer.endWidth = _definition.pointerWidth;
        _lineRenderer.startColor = _definition.pointerColor;
        _lineRenderer.endColor = _definition.pointerColor;
        _lineRenderer.numCapVertices = 4;

        SpriteRenderer playerSprite = player.GetComponentInChildren<SpriteRenderer>();
        if (playerSprite != null)
        {
            _lineRenderer.sortingLayerID = playerSprite.sortingLayerID;
            _lineRenderer.sortingOrder = playerSprite.sortingOrder + 1;
        }
        else
        {
            _lineRenderer.sortingOrder = 10;
        }

        if (_lineMaterial != null)
        {
            UnityEngine.Object.Destroy(_lineMaterial);
            _lineMaterial = null;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            _lineMaterial = new Material(shader);
            _lineRenderer.material = _lineMaterial;
        }

        _lineRenderer.enabled = true;
    }

    public override void ExecuteEffects()
    {
        if (player == null || _lineRenderer == null) return;

        bool active = IsActiveByRounds();
        _lineRenderer.enabled = active;
        if (!active) return;

        Vector2 start = player.transform.position;
        Vector2 direction = player.transform.up;
        float range = Mathf.Max(0.1f, _definition.pointerRange);
        Vector2 end = start + direction * range;

        RaycastHit2D selectedHit = default;
        bool hasValidHit = false;
        int hitCount = Physics2D.Raycast(start, direction, _raycastFilter, _raycastHits, range);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = _raycastHits[i].collider;
            if (hitCollider == null) continue;
            if (hitCollider.transform == player.transform || hitCollider.transform.IsChildOf(player.transform)) continue;

            selectedHit = _raycastHits[i];
            hasValidHit = true;
            break;
        }

        if (hasValidHit)
        {
            end = selectedHit.point;

            Entity target = selectedHit.collider.GetComponent<Entity>();
            if (target != null && selectedHit.collider.CompareTag(player.enemyTag) && HasAuthority())
            {
                WeakmakerExposureTracker tracker = target.GetComponent<WeakmakerExposureTracker>();
                if (tracker == null)
                {
                    tracker = target.gameObject.AddComponent<WeakmakerExposureTracker>();
                }

                tracker.ApplyExposure(Definition.augmentID, _definition.pointedDamageMultiplier, _definition.exposureRefreshDuration);
            }
        }

        _lineRenderer.SetPosition(0, start);
        _lineRenderer.SetPosition(1, end);
    }

    public override void OnRemoved()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
        }

        if (_lineMaterial != null)
        {
            UnityEngine.Object.Destroy(_lineMaterial);
            _lineMaterial = null;
        }
    }

    private bool HasAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _netMovement != null && _netMovement.IsServer;
    }
}

public sealed class BurstRuntime : AugmentRuntimeBase
{
    private readonly Burst _definition;
    private float _burstEndTime;
    private float _lastBurstTime;

    public BurstRuntime(Burst definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _burstEndTime = -999f;
        _lastBurstTime = -999f;
    }

    public override void OnContact(Collision2D collision)
    {
        if (!IsActiveByRounds()) return;
        if (player == null || collision == null || collision.collider == null) return;
        if (Time.time < _lastBurstTime + _definition.contactCooldown) return;

        Entity target = collision.collider.GetComponent<Entity>();
        if (target == null || target == player) return;

        _lastBurstTime = Time.time;
        _burstEndTime = Time.time + Mathf.Max(0.05f, _definition.burstDuration);
        AddOrRefreshMultiplier(_definition.speedMultiplier, player.speedMultipliers);
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        if (!IsActiveByRounds() || Time.time >= _burstEndTime)
        {
            RemoveMultiplier(player.speedMultipliers);
        }
    }
}

public sealed class BurnerRuntime : AugmentRuntimeBase
{
    private readonly Burner _definition;
    private readonly Dictionary<int, float> _reapplyTimers = new Dictionary<int, float>();
    private NetMovement _netMovement;

    public BurnerRuntime(Burner definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _netMovement = player != null ? player.GetComponent<NetMovement>() : null;
    }

    public override void OnPrimaryProjectileHit(Entity target, Vector2 hitPoint, float damage)
    {
        if (player == null || target == null) return;
        if (!IsActiveByRounds()) return;
        if (!HasAuthority()) return;

        int targetId = target.GetInstanceID();
        if (_reapplyTimers.TryGetValue(targetId, out float nextAllowedTime) && Time.time < nextAllowedTime)
        {
            return;
        }

        BurnerDebuffController burnController = target.GetComponent<BurnerDebuffController>();
        if (burnController == null)
        {
            burnController = target.gameObject.AddComponent<BurnerDebuffController>();
        }

        burnController.ApplyBurn(
            Definition.augmentID,
            player,
            _definition.burnDamagePerSecond,
            _definition.burnDuration,
            _definition.burnTickInterval);
        _reapplyTimers[targetId] = Time.time + Mathf.Max(0.01f, _definition.reapplyThrottle);
    }

    private bool HasAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return _netMovement != null && _netMovement.IsServer;
    }
}

public sealed class AutoCounterRuntime : AugmentRuntimeBase
{
    private readonly AutoCounter _definition;
    private AutoCounterReflectorController _reflector;
    private float _nextCastTime;
    private float _deactivateTime;
    private bool _isActive;

    public AutoCounterRuntime(AutoCounter definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        if (player == null) return;

        _reflector = player.GetComponent<AutoCounterReflectorController>();
        if (_reflector == null)
        {
            _reflector = player.gameObject.AddComponent<AutoCounterReflectorController>();
        }

        _reflector.Initialize(player, _definition.reflectShieldPrefab, _definition.reflectedProjectileColor);
        _reflector.OnProjectileReflected += HandleProjectileReflected;

        _nextCastTime = Time.time;
        _deactivateTime = -999f;
        _isActive = false;
        _reflector.SetActive(false);
    }

    public override void ExecuteEffects()
    {
        if (player == null || _reflector == null) return;

        if (!IsActiveByRounds())
        {
            if (_isActive)
            {
                _reflector.SetActive(false);
                _isActive = false;
            }
            return;
        }

        if (!_isActive)
        {
            if (Time.time >= _nextCastTime)
            {
                _isActive = true;
                _deactivateTime = Time.time + Mathf.Max(0.05f, _definition.activeDuration);
                _reflector.SetActive(true);
            }
            return;
        }

        if (Time.time >= _deactivateTime)
        {
            _isActive = false;
            _reflector.SetActive(false);
            _nextCastTime = Time.time + Mathf.Max(0.05f, _definition.autocastInterval);
        }
    }

    private void HandleProjectileReflected(Vector2 hitPoint)
    {
        if (!_isActive) return;

        _deactivateTime = Time.time + Mathf.Max(0.05f, _definition.delayedTurnOffAfterHit);
    }

    public override void OnRemoved()
    {
        if (_reflector != null)
        {
            _reflector.OnProjectileReflected -= HandleProjectileReflected;
            _reflector.SetActive(false);
        }

        _isActive = false;
    }
}

public sealed class FlyersRuntime : AugmentRuntimeBase
{
    private readonly Flyers _definition;
    private FlyersSwarmController _swarmController;

    public FlyersRuntime(Flyers definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        if (player == null) return;

        _swarmController = player.GetComponent<FlyersSwarmController>();
        if (_swarmController == null)
        {
            _swarmController = player.gameObject.AddComponent<FlyersSwarmController>();
        }

        _swarmController.Initialize(player, _definition);
        _swarmController.enabled = IsActiveByRounds();
    }

    public override void ExecuteEffects()
    {
        if (_swarmController == null) return;
        _swarmController.enabled = IsActiveByRounds();
    }

    public override void OnRemoved()
    {
        if (_swarmController != null)
        {
            _swarmController.enabled = false;
        }
    }
}
