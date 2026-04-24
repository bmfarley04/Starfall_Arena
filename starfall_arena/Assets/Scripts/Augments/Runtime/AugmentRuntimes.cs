using System;
using System.Collections.Generic;
using StarfallArena.UI;
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

public sealed class BubbleShieldRuntime : AugmentRuntimeBase
{
    private readonly BubbleShield _definition;
    private float _anchoredDamageTaken;
    private float _stunEndTime;
    private float _lastAnchoredHitTime;
    private GameObject _bubbleShieldEffectInstance;

    public BubbleShieldRuntime(BubbleShield definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _anchoredDamageTaken = 0f;
        _stunEndTime = -999f;
        _lastAnchoredHitTime = Time.time - Mathf.Max(0f, _definition.damageRegenDelay);
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

        bool shieldVisualActive = false;

        if (!IsActiveByRounds())
        {
            RemoveMultiplier(player.speedMultipliers);
            RemoveMultiplier(player.rotationMultipliers);
            _anchoredDamageTaken = 0f;
            _lastAnchoredHitTime = Time.time;
            SetAttachedEffectActive(ref _bubbleShieldEffectInstance, _definition.bubbleShieldPrefab, false, "BubbleShield");
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

            SetAttachedEffectActive(ref _bubbleShieldEffectInstance, _definition.bubbleShieldPrefab, false, "BubbleShield");
            return;
        }

        RemoveMultiplier(player.speedMultipliers);
        RemoveMultiplier(player.rotationMultipliers);

        if (player.IsAnchored)
        {
            shieldVisualActive = true;
        }

        RegenerateAnchoredDamageDebt(Time.deltaTime);

        SetAttachedEffectActive(ref _bubbleShieldEffectInstance, _definition.bubbleShieldPrefab, shieldVisualActive, "BubbleShield");
        UpdateBubbleShieldScale();
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
        if (incomingDamage <= 0f)
        {
            return;
        }

        _anchoredDamageTaken += incomingDamage;
        _lastAnchoredHitTime = Time.time;
        damage = incomingDamage * Mathf.Max(0f, _definition.anchoredDamageMultiplier);
        PlaySoundEffect(_definition.blockSound);

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

    private void RegenerateAnchoredDamageDebt(float deltaTime)
    {
        if (_anchoredDamageTaken <= 0f)
        {
            return;
        }

        float regenDelay = Mathf.Max(0f, _definition.damageRegenDelay);
        if (Time.time < _lastAnchoredHitTime + regenDelay)
        {
            return;
        }

        float regenPerSecond = Mathf.Max(0f, _definition.damageRegenPerSecond);
        if (regenPerSecond <= 0f)
        {
            return;
        }

        _anchoredDamageTaken = Mathf.Max(0f, _anchoredDamageTaken - regenPerSecond * Mathf.Max(0f, deltaTime));
    }

    private void UpdateBubbleShieldScale()
    {
        if (_bubbleShieldEffectInstance == null || _definition.bubbleShieldPrefab == null)
        {
            return;
        }

        float threshold = Mathf.Max(0.001f, _definition.damageThresholdBeforeStun);
        float progress = Mathf.Clamp01(_anchoredDamageTaken / threshold);
        float visualMultiplier = Mathf.Lerp(
            Mathf.Max(0.01f, _definition.maxVisualScaleMultiplier),
            Mathf.Max(0.01f, _definition.minVisualScaleMultiplier),
            progress);

        Vector3 baseScale = _definition.bubbleShieldPrefab.transform.localScale * Mathf.Max(0.01f, player.ShipSize);
        _bubbleShieldEffectInstance.transform.localScale = baseScale * visualMultiplier;
    }

    public override void OnRemoved()
    {
        if (player != null)
        {
            RemoveMultiplier(player.speedMultipliers);
            RemoveMultiplier(player.rotationMultipliers);
        }

        SetAttachedEffectActive(ref _bubbleShieldEffectInstance, _definition.bubbleShieldPrefab, false, "BubbleShield");
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
    private GameObject _speedUpEffectInstance;

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

        bool boostActive = IsActiveByRounds() && Time.time < _burstEndTime;

        if (!boostActive)
        {
            RemoveMultiplier(player.speedMultipliers);
        }

        SetAttachedEffectActive(ref _speedUpEffectInstance, _definition.speedUpEffectPrefab, boostActive, "BurstSpeedUp");
    }

    public override void OnRemoved()
    {
        if (player != null)
        {
            RemoveMultiplier(player.speedMultipliers);
        }

        SetAttachedEffectActive(ref _speedUpEffectInstance, _definition.speedUpEffectPrefab, false, "BurstSpeedUp");
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
            _definition.burnTickInterval,
            _definition.burnTickEffectPrefab,
            _definition.burnTickRandomRadius);
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
    private GameObject _readyGlowEffectInstance;

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

            SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, false, "AutoCounterReadyGlow");
            return;
        }

        if (!_isActive)
        {
            SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, false, "AutoCounterReadyGlow");

            if (Time.time >= _nextCastTime)
            {
                _isActive = true;
                _deactivateTime = Time.time + Mathf.Max(0.05f, _definition.activeDuration);
                _reflector.SetActive(true);
                SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, true, "AutoCounterReadyGlow");
            }
            return;
        }

        SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, true, "AutoCounterReadyGlow");

        if (Time.time >= _deactivateTime)
        {
            _isActive = false;
            _reflector.SetActive(false);
            _nextCastTime = Time.time + Mathf.Max(0.05f, _definition.autocastInterval);
            SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, false, "AutoCounterReadyGlow");
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

        SetAttachedEffectActive(ref _readyGlowEffectInstance, _definition.readyGlowPrefab, false, "AutoCounterReadyGlow");

        _isActive = false;
    }
}

public abstract class NearbyBindingRuntimeBase<TDefinition> : AugmentRuntimeBase where TDefinition : Augment
{
    private readonly Collider2D[] _nearbyBuffer = new Collider2D[32];
    private readonly List<Entity> _nearbyEnemies = new List<Entity>(8);

    protected readonly TDefinition bindingDefinition;
    protected NetMovement netMovement;
    private BindingLinkVisualController _bindingLinkVisual;

    protected NearbyBindingRuntimeBase(TDefinition definition) : base(definition)
    {
        bindingDefinition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        netMovement = player != null ? player.GetComponent<NetMovement>() : null;
    }

    protected bool HasAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        return netMovement != null && netMovement.IsServer;
    }

    protected List<Entity> CollectNearbyEnemies(float radius)
    {
        _nearbyEnemies.Clear();
        if (player == null)
        {
            return _nearbyEnemies;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(player.transform.position, Mathf.Max(0f, radius), _nearbyBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = _nearbyBuffer[i];
            _nearbyBuffer[i] = null;
            if (collider == null)
            {
                continue;
            }

            Entity target = collider.GetComponentInParent<Entity>();
            if (target == null || target == player)
            {
                continue;
            }

            if (!target.CompareTag(player.enemyTag))
            {
                continue;
            }

            if (_nearbyEnemies.Contains(target))
            {
                continue;
            }

            _nearbyEnemies.Add(target);
        }

        return _nearbyEnemies;
    }

    protected float GetDistanceFactor(Entity target, float radius, float exponent = 1f)
    {
        if (target == null)
        {
            return 0f;
        }

        float safeRadius = Mathf.Max(0.001f, radius);
        float distance = Vector2.Distance(player.transform.position, target.transform.position);
        float normalized = 1f - Mathf.Clamp01(distance / safeRadius);
        return Mathf.Pow(normalized, Mathf.Max(0.01f, exponent));
    }

    protected void UpdateBindingLinks(List<Entity> targets, float radius, BindingLinkVisualSettings visualSettings)
    {
        if (player == null)
        {
            return;
        }

        if (!IsActiveByRounds() || visualSettings == null || !visualSettings.enabled)
        {
            HideBindingLinks();
            return;
        }

        if (_bindingLinkVisual == null)
        {
            _bindingLinkVisual = player.gameObject.AddComponent<BindingLinkVisualController>();
        }

        _bindingLinkVisual.Initialize(player, visualSettings, Definition.augmentID);
        _bindingLinkVisual.SetTargets(targets, radius);
    }

    protected void HideBindingLinks()
    {
        if (_bindingLinkVisual != null)
        {
            _bindingLinkVisual.HideAll();
        }
    }
}

public sealed class TwinFireRuntime : AugmentRuntimeBase
{
    private readonly TwinFire _definition;
    private float _pendingSecondShotTime = -1f;
    private bool _pendingSecondShot;
    private NetMovement _netMovement;

    public TwinFireRuntime(TwinFire definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _netMovement = player != null ? player.GetComponent<NetMovement>() : null;
        PrimaryFireExecutionBus.PrimaryFireExecuted += HandlePrimaryFireExecuted;
    }

    public override void ExecuteEffects()
    {
        if (player == null)
        {
            return;
        }

        bool active = IsActiveByRounds();
        if (active)
        {
            AddOrRefreshMultiplier(_definition.baseDamageMultiplier, player.damageMultipliers);
        }
        else
        {
            RemoveMultiplier(player.damageMultipliers);
            _pendingSecondShot = false;
            return;
        }

        if (!_pendingSecondShot || Time.time < _pendingSecondShotTime)
        {
            return;
        }

        if (!HasAuthority())
        {
            return;
        }

        if (TryFirePrimaryVolleyFromAugment(_definition.secondShotDamageMultiplier, _definition.ignoreCooldownForSecondShot, PrimaryFireExecutionSource.TwinFire, playSound: false))
        {
            PlaySoundEffect(_definition.secondShotSound);
        }

        _pendingSecondShot = false;
    }

    public override void OnRemoved()
    {
        PrimaryFireExecutionBus.PrimaryFireExecuted -= HandlePrimaryFireExecuted;
        _pendingSecondShot = false;

        if (player != null)
        {
            RemoveMultiplier(player.damageMultipliers);
        }
    }

    private void HandlePrimaryFireExecuted(Player shooter, PrimaryFireExecutionSource source)
    {
        if (player == null || shooter != player)
        {
            return;
        }

        if (!IsActiveByRounds())
        {
            return;
        }

        if (source == PrimaryFireExecutionSource.TwinFire)
        {
            return;
        }

        _pendingSecondShot = true;
        _pendingSecondShotTime = Time.time + Mathf.Max(0f, _definition.secondShotDelay);
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

public sealed class SoulBindingRuntime : NearbyBindingRuntimeBase<SoulBinding>
{
    private readonly SoulBinding _definition;
    private float _lastKnownHealth;

    public SoulBindingRuntime(SoulBinding definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _lastKnownHealth = player != null ? player.CurrentHealth : 0f;
    }

    public override void ExecuteEffects()
    {
        if (player == null)
        {
            return;
        }

        List<Entity> enemies = CollectNearbyEnemies(_definition.bindingRadius);
        UpdateBindingLinks(enemies, _definition.bindingRadius, _definition.linkVisual);

        float currentHealth = player.CurrentHealth;
        float healthLoss = Mathf.Max(0f, _lastKnownHealth - currentHealth);
        _lastKnownHealth = currentHealth;

        if (!IsActiveByRounds() || !HasAuthority())
        {
            return;
        }

        if (healthLoss < Mathf.Max(0f, _definition.minHealthLossToTrigger))
        {
            return;
        }

        bool appliedAny = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            Entity enemy = enemies[i];
            if (enemy == null)
            {
                continue;
            }

            float distanceFactor = GetDistanceFactor(enemy, _definition.bindingRadius);
            float transferMultiplier = Mathf.Lerp(
                Mathf.Max(0f, _definition.edgeTransferMultiplier),
                Mathf.Max(0f, _definition.pointBlankTransferMultiplier),
                distanceFactor);
            float transferDamage = healthLoss * transferMultiplier;
            if (transferDamage <= 0f)
            {
                continue;
            }

            enemy.TakeDirectDamage(transferDamage, 0f, enemy.transform.position, DamageSource.Other, player);
            appliedAny = true;
        }

        if (appliedAny)
        {
            SpawnTransientEffect(_definition.triggerEffectPrefab);
        }
    }

    public override void OnRemoved()
    {
        HideBindingLinks();
    }
}

public sealed class MindBindingRuntime : NearbyBindingRuntimeBase<MindBinding>
{
    private readonly MindBinding _definition;
    private float _nextAllowedTriggerTime;

    public MindBindingRuntime(MindBinding definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _nextAllowedTriggerTime = -999f;
        PrimaryFireExecutionBus.PrimaryFireExecuted += HandlePrimaryFireExecuted;
    }

    public override void ExecuteEffects()
    {
        if (player == null)
        {
            return;
        }

        List<Entity> nearbyEnemies = CollectNearbyEnemies(_definition.bindingRadius);
        UpdateBindingLinks(nearbyEnemies, _definition.bindingRadius, _definition.linkVisual);
    }

    public override void OnRemoved()
    {
        PrimaryFireExecutionBus.PrimaryFireExecuted -= HandlePrimaryFireExecuted;
        HideBindingLinks();
    }

    private void HandlePrimaryFireExecuted(Player shooter, PrimaryFireExecutionSource source)
    {
        if (player == null || shooter == null || shooter == player)
        {
            return;
        }

        if (!IsActiveByRounds() || !HasAuthority())
        {
            return;
        }

        if (source == PrimaryFireExecutionSource.MindBinding)
        {
            return;
        }

        if (!shooter.CompareTag(player.enemyTag))
        {
            return;
        }

        float maxDistance = Mathf.Max(0f, _definition.bindingRadius);
        if (Vector2.Distance(player.transform.position, shooter.transform.position) > maxDistance)
        {
            return;
        }

        if (Time.time < _nextAllowedTriggerTime)
        {
            return;
        }

        if (TryFirePrimaryVolleyFromAugment(_definition.mirroredShotDamageMultiplier, _definition.ignoreCooldownForMirroredShot, PrimaryFireExecutionSource.MindBinding, playSound: false))
        {
            PlaySoundEffect(_definition.mirroredShotSound);
            _nextAllowedTriggerTime = Time.time + Mathf.Max(0.01f, _definition.mirroredShotCooldown);
        }
    }
}

public sealed class BodyBindingRuntime : NearbyBindingRuntimeBase<BodyBinding>
{
    private readonly BodyBinding _definition;
    private readonly Dictionary<int, Entity> _activeTargets = new Dictionary<int, Entity>();
    private readonly HashSet<int> _seenThisTick = new HashSet<int>();
    private readonly List<int> _toRemove = new List<int>();
    private string _sourceKey;

    public BodyBindingRuntime(BodyBinding definition) : base(definition)
    {
        _definition = definition;
    }

    public override void Initialize(Player player, int roundAcquired, object persistentState = null)
    {
        base.Initialize(player, roundAcquired, persistentState);
        _sourceKey = $"{Definition.augmentID}_{(player != null ? player.GetInstanceID() : 0)}";
    }

    public override void ExecuteEffects()
    {
        if (player == null)
        {
            return;
        }

        if (!IsActiveByRounds() || !HasAuthority())
        {
            ClearAllAppliedSlow();
            return;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        float ownerSpeed = body != null ? body.linearVelocity.magnitude : 0f;
        float maxSpeed = Mathf.Max(0.01f, player.movement.maxSpeed);
        float speedRatio = Mathf.Clamp01(ownerSpeed / maxSpeed);
        if (speedRatio < Mathf.Clamp01(_definition.minOwnerSpeedRatioToAffect))
        {
            ClearAllAppliedSlow();
            return;
        }

        _seenThisTick.Clear();
        List<Entity> nearby = CollectNearbyEnemies(_definition.bindingRadius);
        UpdateBindingLinks(nearby, _definition.bindingRadius, _definition.linkVisual);
        for (int i = 0; i < nearby.Count; i++)
        {
            Entity target = nearby[i];
            if (target == null)
            {
                continue;
            }

            float distanceFactor = GetDistanceFactor(target, _definition.bindingRadius, _definition.distanceFalloffExponent);
            float blend = Mathf.Clamp01(speedRatio * distanceFactor);
            float slowMultiplier = Mathf.Lerp(1f, Mathf.Clamp(_definition.maxSlowMultiplier, 0.01f, 1f), blend);
            ApplySlowMultiplier(target, slowMultiplier);

            int targetId = target.GetInstanceID();
            _seenThisTick.Add(targetId);
            _activeTargets[targetId] = target;
        }

        _toRemove.Clear();
        foreach (KeyValuePair<int, Entity> entry in _activeTargets)
        {
            if (!_seenThisTick.Contains(entry.Key) || entry.Value == null)
            {
                _toRemove.Add(entry.Key);
            }
        }

        for (int i = 0; i < _toRemove.Count; i++)
        {
            int targetId = _toRemove[i];
            if (_activeTargets.TryGetValue(targetId, out Entity target) && target != null)
            {
                RemoveSlowMultiplier(target);
            }

            _activeTargets.Remove(targetId);
        }
    }

    public override void OnRemoved()
    {
        ClearAllAppliedSlow();
        HideBindingLinks();
    }

    private void ApplySlowMultiplier(Entity target, float multiplier)
    {
        if (target == null)
        {
            return;
        }

        if (target.speedMultipliers.TryGetValue(_sourceKey, out float current) && Mathf.Approximately(current, multiplier))
        {
            return;
        }

        target.speedMultipliers[_sourceKey] = multiplier;
        target.SetAugmentVariables();
    }

    private void RemoveSlowMultiplier(Entity target)
    {
        if (target == null)
        {
            return;
        }

        if (target.speedMultipliers.Remove(_sourceKey))
        {
            target.SetAugmentVariables();
        }
    }

    private void ClearAllAppliedSlow()
    {
        foreach (KeyValuePair<int, Entity> entry in _activeTargets)
        {
            RemoveSlowMultiplier(entry.Value);
        }

        _activeTargets.Clear();
        _seenThisTick.Clear();
        _toRemove.Clear();
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
