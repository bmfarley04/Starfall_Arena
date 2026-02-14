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
            Debug.Log($"{Definition.augmentName} restored shields for {player.gameObject.name} (round {player.currentRound})");
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
            Debug.Log($"{player.name} evaded shield damage!");
        }

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
            Debug.Log($"{player.name} evaded damage!");
        }
    }

    public override void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source)
    {
        if (player == null || !IsActiveByRounds()) return;

        if (!healthIgnored && UnityEngine.Random.value < _definition.healthIgnoreChance)
        {
            healthIgnored = true;
            Debug.Log($"{player.name} evaded direct damage!");
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
                Debug.Log($"[Regenerator] Healing player for {amount:F1} HP (current: {player.CurrentHealth}/{player.maxHealth})");
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
        Debug.Log($"[Regenerator] Player took damage, interrupting healing until {_lastDamageTime + _definition.damageInterruptCooldown}");
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
        Debug.Log($"{Definition.augmentName} applied: +{_appliedAmount} max health");
    }

    private void RemoveHealthBonus()
    {
        player.SetMaxHealthAndClampCurrent(player.maxHealth - _appliedAmount);

        Debug.Log($"{Definition.augmentName} removed: -{_appliedAmount} max health");

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
