using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlazeOfGloryRuntime : AugmentRuntimeBase
{
    private readonly BlazeOfGlory _definition;
    private GameObject _damageBoostEffectInstance;

    public BlazeOfGloryRuntime(BlazeOfGlory definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        bool isActive = IsActive();
        SetAttachedEffectActive(ref _damageBoostEffectInstance, _definition.damageBoostPrefab, isActive, "BlazeOfGloryDamageBoost");

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

            if (_definition.particlePrefab != null)
            {
                SpawnTransientEffect(_definition.particlePrefab);
            }
        }
    }
}

public sealed class CloakRuntime : AugmentRuntimeBase
{
    private readonly Cloak _definition;
    private float _speedBoostEndTime;
    private GameObject _speedBoostEffectInstance;

    public CloakRuntime(Cloak definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        if (player == null) return;
        if (!IsActiveByRounds()) return;

        bool wasBoostActive = player.speedMultipliers.ContainsKey(Definition.augmentID) && Time.time < _speedBoostEndTime;
        _speedBoostEndTime = Time.time + _definition.boostDuration;
        AddOrRefreshMultiplier(_definition.speedMultiplier, player.speedMultipliers);

        if (!wasBoostActive)
        {
            PlaySoundEffect(_definition.activationSound);
        }
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        bool roundsActive = IsActiveByRounds();
        bool hasBoost = player.speedMultipliers.ContainsKey(Definition.augmentID);
        bool isBoostActive = roundsActive && hasBoost && Time.time < _speedBoostEndTime;

        if (!isBoostActive && hasBoost)
        {
            RemoveMultiplier(player.speedMultipliers);
        }

        SetAttachedEffectActive(ref _speedBoostEffectInstance, _definition.speedBoostPrefab, isBoostActive, "CloakSpeedBoost");
    }
}

public sealed class DaggerRuntime : AugmentRuntimeBase
{
    private readonly Dagger _definition;
    private float _damageBoostEndTime;
    private GameObject _damageBoostEffectInstance;

    public DaggerRuntime(Dagger definition) : base(definition)
    {
        _definition = definition;
    }

    public override void OnTakeDamage(float damage, float impactForce, Vector3 hitPoint, DamageSource source)
    {
        if (player == null) return;
        if (!IsActiveByRounds()) return;

        bool wasBoostActive = player.damageMultipliers.ContainsKey(Definition.augmentID) && Time.time < _damageBoostEndTime;
        _damageBoostEndTime = Time.time + _definition.boostDuration;
        AddOrRefreshMultiplier(_definition.damageMultiplier, player.damageMultipliers);

        if (!wasBoostActive)
        {
            PlaySoundEffect(_definition.activationSound);
        }
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        bool roundsActive = IsActiveByRounds();
        bool hasBoost = player.damageMultipliers.ContainsKey(Definition.augmentID);
        bool isBoostActive = roundsActive && hasBoost && Time.time < _damageBoostEndTime;

        if (!isBoostActive && hasBoost)
        {
            RemoveMultiplier(player.damageMultipliers);
        }

        SetAttachedEffectActive(ref _damageBoostEffectInstance, _definition.damageBoostPrefab, isBoostActive, "DaggerDamageBoost");
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

        bool successfulEvade = false;

        if (!shieldIgnored && UnityEngine.Random.value < _definition.shieldIgnoreChance)
        {
            shieldIgnored = true;
            successfulEvade = true;
        }

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
            successfulEvade = true;
        }

        if (successfulEvade)
        {
            TriggerSuccessfulEvadePresentation();
        }
    }

    public override void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source)
    {
        if (player == null || !IsActiveByRounds()) return;

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
            TriggerSuccessfulEvadePresentation();
        }
    }

    public void NotifySuccessfulEvasion()
    {
        if (player == null || !IsActiveByRounds())
        {
            return;
        }

        TriggerSuccessfulEvadePresentation();
    }

    private void TriggerSuccessfulEvadePresentation()
    {
        PlaySoundEffect(_definition.successfulEvadeSound);
        SpawnTransientEffect(_definition.successfulEvadePrefab);
    }
}

public sealed class RegeneratorRuntime : AugmentRuntimeBase
{
    private readonly Regenerator _definition;
    private float _lastDamageTime;
    private float _anchorStartTime;
    private GameObject _regenerationEffectInstance;

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
        if (player == null) return;

        bool canRun = IsActiveByRounds();
        bool isRegenerating = false;

        if (!canRun)
        {
            _anchorStartTime = -999f;
            SetAttachedEffectActive(ref _regenerationEffectInstance, _definition.regenerationPrefab, false, "RegeneratorHealing");
            return;
        }

        if (player.IsAnchored)
        {
            if (_anchorStartTime < 0f) _anchorStartTime = Time.time;

            if (Time.time >= _anchorStartTime + _definition.healDelay &&
                Time.time >= _lastDamageTime + _definition.damageInterruptCooldown)
            {
                if (player.CurrentHealth < player.maxHealth)
                {
                    float amount = _definition.healRate * Time.deltaTime;
                    player.Heal(amount);
                    isRegenerating = true;
                }
            }
        }
        else
        {
            _anchorStartTime = -999f;
        }

        SetAttachedEffectActive(ref _regenerationEffectInstance, _definition.regenerationPrefab, isRegenerating, "RegeneratorHealing");
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
    private Transform _scaleTarget;
    private Vector3 _originalScale;
    private float _appliedScaleMultiplier = 1f;
    private bool _scaleApplied;

    public ReinforcedHullRuntime(ReinforcedHull definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);

        _scaleTarget = ResolveScaleTarget();
        _originalScale = _scaleTarget != null ? _scaleTarget.localScale : Vector3.one;
        _scaleApplied = false;
        _appliedScaleMultiplier = 1f;
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

        ApplyScaleBonus();

        _isApplied = true;
    }

    private void RemoveHealthBonus()
    {
        player.SetMaxHealthAndClampCurrent(player.maxHealth - _appliedAmount);

        RemoveScaleBonus();

        _appliedAmount = 0f;
        _isApplied = false;
    }

    private Transform ResolveScaleTarget()
    {
        return player != null ? player.transform : null;
    }

    private void ApplyScaleBonus()
    {
        if (_scaleTarget == null)
        {
            return;
        }

        _appliedScaleMultiplier = Mathf.Max(0.1f, _definition.healthMultiplier);
        _scaleTarget.localScale = _originalScale * _appliedScaleMultiplier;
        _scaleApplied = true;
    }

    private void RemoveScaleBonus()
    {
        if (!_scaleApplied || _scaleTarget == null)
        {
            return;
        }

        _scaleTarget.localScale = _originalScale;
        _scaleApplied = false;
        _appliedScaleMultiplier = 1f;
    }
}

public sealed class RotatorRuntime : AugmentRuntimeBase
{
    private readonly Rotator _definition;
    private GameObject _turningEffectInstance;
    private bool _hasRotationSample;
    private float _lastRotation;

    public RotatorRuntime(Rotator definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _lastRotation = player != null ? player.transform.eulerAngles.z : 0f;
        _hasRotationSample = false;
    }

    public override void ExecuteEffects()
    {
        if (player == null) return;

        bool isActive = IsActiveByRounds();

        if (isActive && !player.rotationMultipliers.ContainsKey(Definition.augmentID))
        {
            AddMultiplier(_definition.rotationMultiplier, player.rotationMultipliers);
        }
        else if (!isActive && player.rotationMultipliers.ContainsKey(Definition.augmentID))
        {
            RemoveMultiplier(player.rotationMultipliers);
        }

        bool isTurning = false;
        if (isActive)
        {
            float currentRotation = player.transform.eulerAngles.z;
            if (_hasRotationSample)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(_lastRotation, currentRotation));
                float turnRate = deltaAngle / dt;
                isTurning = turnRate >= _definition.turnRateThreshold;
            }

            _lastRotation = currentRotation;
            _hasRotationSample = true;
        }
        else
        {
            _hasRotationSample = false;
        }

        SetAttachedEffectActive(ref _turningEffectInstance, _definition.turningPrefab, isTurning, "RotatorTurning");
    }
}

public sealed class ThornsRuntime : AugmentRuntimeBase
{
    private readonly Thorns _definition;
    private readonly Dictionary<GameObject, float> _hitTimers = new Dictionary<GameObject, float>();
    private GameObject _auraEffectInstance;

    public ThornsRuntime(Thorns definition) : base(definition)
    {
        _definition = definition;
    }

    public override void ExecuteEffects()
    {
        if (player == null)
        {
            return;
        }

        bool isActive = IsActiveByRounds();
        SetAttachedEffectActive(ref _auraEffectInstance, _definition.auraPrefab, isActive, "ThornsAura");
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
    private bool _triggeredThisDamageEvent;
    private float _originalShield;
    private bool _restoreShieldNextFixedUpdate;
    private bool _reviveSequenceActive;
    private bool _regroupStarted;
    private float _regroupStartTime;
    private float _reviveEndTime;
    private string _tagBeforeRevive;
    private readonly List<ShipPartScatter> _scatteredParts = new List<ShipPartScatter>();

    public ArtificialFairyRuntime(ArtificialFairy definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _reviveSequenceActive = false;
        _regroupStarted = false;
        _scatteredParts.Clear();
    }

    public override void ExecuteEffects()
    {
        if (_restoreShieldNextFixedUpdate && player != null)
        {
            player.SetShieldValue(_originalShield);
            _restoreShieldNextFixedUpdate = false;
        }

        if (!_reviveSequenceActive || player == null)
        {
            return;
        }

        if (!_regroupStarted && Time.time >= _regroupStartTime)
        {
            StartRegroup();
        }

        if (_regroupStarted && Time.time >= _reviveEndTime)
        {
            EndReviveSequence();
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
            _triggeredThisDamageEvent = true;

            _originalShield = player.currentShield;
            player.SetShieldValue(player.currentShield + damage + 1f, notify: false, clampToMax: false);

            float target = player.maxHealth * _definition.healFraction;
            float amountToHeal = target - player.CurrentHealth;
            if (amountToHeal > 0f)
            {
                player.Heal(amountToHeal);
            }

            _restoreShieldNextFixedUpdate = true;
            BeginReviveSequence();

            Debug.Log($"{Definition.augmentName} triggered: prevented death and healed to {Mathf.CeilToInt(target)} HP");
        }
    }

    public bool ConsumeTriggeredThisDamageEvent()
    {
        bool triggered = _triggeredThisDamageEvent;
        _triggeredThisDamageEvent = false;
        return triggered;
    }

    public void NotifyTriggeredFromNetwork()
    {
        if (player == null || _reviveSequenceActive)
        {
            return;
        }

        _triggered = true;
        BeginReviveSequence();
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
        _triggeredThisDamageEvent = false;
        _reviveSequenceActive = false;
        _regroupStarted = false;
        _tagBeforeRevive = null;
        _scatteredParts.Clear();
    }

    private bool IsActive()
    {
        return IsActiveByRounds() && !_triggered;
    }

    private void BeginReviveSequence()
    {
        if (player == null)
        {
            return;
        }

        SpawnTransientEffect(_definition.reviveFlashPrefab);

        Vector2 scatterDirection = UnityEngine.Random.insideUnitCircle;
        if (scatterDirection.sqrMagnitude <= 0.0001f)
        {
            scatterDirection = Vector2.up;
        }

        ScatterShipParts(scatterDirection.normalized);

        _tagBeforeRevive = player.gameObject.tag;
        TrySetPlayerTag("ShipPart");

        player.SetIncomingDamageIgnored(true);

        _reviveSequenceActive = true;
        _regroupStarted = false;
        _regroupStartTime = Time.time + Mathf.Max(0f, _definition.intangibleDuration);
        _reviveEndTime = _regroupStartTime + Mathf.Max(0.01f, _definition.reassemblyDuration);
    }

    private void ScatterShipParts(Vector2 scatterDirection)
    {
        _scatteredParts.Clear();

        Transform visualModel = player.visualEffects.visualModel;
        if (visualModel == null)
        {
            return;
        }

        ShipPartScatter[] parts = visualModel.GetComponentsInChildren<ShipPartScatter>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            ShipPartScatter part = parts[i];
            if (part == null)
            {
                continue;
            }

            part.ScatterForRevive(scatterDirection);
            _scatteredParts.Add(part);
        }
    }

    private void StartRegroup()
    {
        float regroupDuration = Mathf.Max(0.01f, _definition.reassemblyDuration);
        for (int i = 0; i < _scatteredParts.Count; i++)
        {
            if (_scatteredParts[i] != null)
            {
                _scatteredParts[i].RegroupToOriginal(regroupDuration);
            }
        }

        _regroupStarted = true;
        _reviveEndTime = Time.time + regroupDuration;
    }

    private void EndReviveSequence()
    {
        if (player == null)
        {
            return;
        }

        player.SetIncomingDamageIgnored(false);
        RestorePlayerTag();

        _reviveSequenceActive = false;
        _regroupStarted = false;
        _scatteredParts.Clear();
    }

    private void TrySetPlayerTag(string tag)
    {
        if (player == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        try
        {
            player.gameObject.tag = tag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"{Definition.augmentName}: Tag '{tag}' is not defined in project tags.");
        }
    }

    private void RestorePlayerTag()
    {
        if (player == null || string.IsNullOrWhiteSpace(_tagBeforeRevive))
        {
            return;
        }

        TrySetPlayerTag(_tagBeforeRevive);
        player.RefreshCombatTags();
        _tagBeforeRevive = null;
    }
}

public sealed class AugmentorRuntime : AugmentRuntimeBase
{
    public AugmentorRuntime(Augmentor definition) : base(definition) { }
}
