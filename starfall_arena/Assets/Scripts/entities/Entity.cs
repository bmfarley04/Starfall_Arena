using StarfallArena.UI;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum DamageSource
{
    Projectile,
    LaserBeam,
    Explosion,
    Other
}

[System.Serializable]
public struct ProjectileWeaponConfig
{
    [Header("Projectile Settings")]
    public GameObject prefab;
    public float baseDamage;
    [HideInInspector]
    public float damage;
    public float speed;
    public float recoilForce;
    public float lifetime;
    public float impactForce;
}

[System.Serializable]
public struct BeamWeaponConfig
{
    [Header("Beam Settings")]
    public GameObject prefab;
    public float damagePerSecond;
    public float maxDistance;
    public float recoilForcePerSecond;
    public float impactForce;
}

[System.Serializable]
public struct MovementConfig
{
    public float thrustForce;
    public float maxSpeed;
    public float rotationSpeed;
    public float lateralDamping;
}

[System.Serializable]
public struct VisualEffectsConfig
{
    [Header("Visual Model")]
    public Transform visualModel;

    [Header("Banking (Roll) Effects")]
    public float maxBankAngle;
    public float bankSensitivity;
    public float bankSmoothing;

    [Header("Pitching Effects")]
    public float maxPitchAngle;
    public float pitchSensitivity;
    public float pitchSmoothing;

    [Header("Recoil & Explosions")]
    [Tooltip("Multiplier for how much recoil/impulse affects pitch (independent of thrust pitch)")]
    public float impulseRecoilSensitivity;
    public GameObject explosionEffectPrefab;
    public float explosionScale;
}

[System.Serializable]
public struct ThrusterConfig
{
    [Tooltip("List of thruster particle systems attached to this ship")]
    public ParticleSystem[] thrusters;
    [Tooltip("Time to ramp thruster emission up/down (seconds)")]
    public float rampTime;
    public bool invertColors;
}

[System.Serializable]
public struct SlowEffectVisualConfig
{
    [Header("Particle Effect")]
    [Tooltip("Particle system to play when slowed (should be a child of this entity)")]
    public ParticleSystem slowParticleSystem;
}

[RequireComponent(typeof(Rigidbody2D))]
public abstract class Entity : MonoBehaviour
{
    // ===== MANAGER REFERENCES =====
    public int currentRound;

    // ===== AUGMENT LISTS =====
    [Header("Augments")]
    public List<Augment> augments = new List<Augment>();

    // ===== CORE COMBAT STATS =====
    [Header("Core Combat Stats")]
    public float maxHealth = 100f;
    public float maxShield = 50f;
    public ShieldController shieldController;

    // ===== MOVEMENT =====
    [Header("Movement")]
    public MovementConfig movement;
    private float baseMaxSpeed;
    private float baseRotationSpeed;

    // ===== TURRETS =====
    [Header("Turrets")]
    public Transform[] turrets;

    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon")]
    public ProjectileWeaponConfig projectileWeapon;

    // ===== VISUAL EFFECTS =====
    [Header("Visual Effects")]
    public VisualEffectsConfig visualEffects;

    // ===== THRUSTER EFFECTS =====
    [Header("Thruster Effects")]
    public ThrusterConfig thrusters;

    // ===== SLOW EFFECT VISUALS =====
    [Header("Slow Effect Visuals")]
    public SlowEffectVisualConfig slowEffectVisuals;

    // ===== RUNTIME STATE - AUGMENTS =====
    public Dictionary<string, float> damageMultipliers = new Dictionary<string, float>();
    public Dictionary<string, float> speedMultipliers = new Dictionary<string, float>();
    public Dictionary<string, float> rotationMultipliers = new Dictionary<string, float>();

    // ===== EVENTS =====
    public System.Action<Entity> onDeath;

    // ===== RUNTIME STATE - COMBAT =====
    protected float currentHealth = 0;
    public float CurrentHealth => currentHealth; // Public getter for health
    public float currentShield;  // Public so projectiles can check shield status
    protected Vector2 _lastDamageDirection;
    private bool _isDead = false;
    private bool _ignoreIncomingDamage = false;

    // ===== RUNTIME STATE - PHYSICS & MOVEMENT =====
    protected Rigidbody2D _rb;
    protected bool _isThrusting = false;
    private Vector2 _previousVelocity;
    private Vector2 _acceleration;
    private Vector2 _recentImpulse = Vector2.zero;
    private float _impulseDecayRate = 5f;

    // ===== RUNTIME STATE - SLOW EFFECT =====
    private float _slowMultiplier = 1f;
    private float _slowEndTime = 0f;
    private bool _slowVisualsActive = false;

    // ===== RUNTIME STATE - VISUAL =====
    private float _previousRotationZ;
    private float _currentBankAngle;
    private float _currentPitchAngle;
    private Quaternion _visualBaseLocalRotation;
    private bool _useExternalVisualState = false;
    private float _currentThrusterIntensity = 0f;
    private Dictionary<ParticleSystem, (ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve lifetime)> _thrusterOriginalValues = new();
    private Dictionary<ParticleSystem, Color> _thrusterOriginalColors = new();
    private AugmentController _augmentController;

    // ===== CONSTANTS =====
    protected const float ROTATION_OFFSET = -90f;

    // ===== INITIALIZATION =====
    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        currentShield = maxShield;

        _previousRotationZ = transform.eulerAngles.z;
        _previousVelocity = Vector2.zero;
        _acceleration = Vector2.zero;
        _currentBankAngle = 0f;
        _currentPitchAngle = 0f;

        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // --- Deterministic physics: prevent the engine from modifying velocity ---
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;

        if (visualEffects.visualModel != null)
        {
            _visualBaseLocalRotation = visualEffects.visualModel.localRotation;
        }
        else
        {
            _visualBaseLocalRotation = Quaternion.identity;
            Debug.LogWarning($"Visual model not assigned on {gameObject.name}. Visual banking/pitching will not work. Please assign the ship's visual mesh/sprite as a child object.");
        }

        if (thrusters.thrusters != null)
        {
            foreach (var thruster in thrusters.thrusters)
            {
                if (thruster != null)
                {
                    var main = thruster.main;
                    _thrusterOriginalValues[thruster] = (main.startSpeed, main.startLifetime);
                    _thrusterOriginalColors[thruster] = main.startColor.color;
                }
            }
        }
        
        projectileWeapon.damage = projectileWeapon.baseDamage;
        baseMaxSpeed = movement.maxSpeed;
        baseRotationSpeed = movement.rotationSpeed;
        
        if (this is Player playerEntity)
        {
            _augmentController = GetComponent<AugmentController>();
            if (_augmentController == null)
            {
                _augmentController = gameObject.AddComponent<AugmentController>();
            }

            _augmentController.Initialize(playerEntity);
            _augmentController.SetCurrentRound(currentRound);

            if (augments.Count > 0)
            {
                List<AugmentLoadoutEntry> defaultLoadout = new List<AugmentLoadoutEntry>(augments.Count);
                foreach (Augment augment in augments)
                {
                    if (augment == null) continue;

                    defaultLoadout.Add(new AugmentLoadoutEntry
                    {
                        definition = augment,
                        roundAcquired = currentRound,
                        persistentState = null
                    });
                }

                _augmentController.ImportLoadout(defaultLoadout, currentRound);
            }
        }
    }

    // ===== UPDATE LOOPS =====
    protected virtual void FixedUpdate()
    {
        if (_rb == null) return;

        Vector2 currentVelocity = _rb.linearVelocity;
        _acceleration = (currentVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = currentVelocity;

        _augmentController?.ExecuteEffects();
    }

    protected virtual void Update()
    {
        UpdateThrusters();
        UpdateSlowVisuals();
    }

    protected virtual void LateUpdate()
    {
        UpdateVisualRotation(Time.deltaTime);
    }

    // ===== MOVEMENT HELPERS =====
    protected void ApplyLateralDamping()
    {
        Vector2 forwardDirection = transform.up;
        Vector2 currentVelocity = _rb.linearVelocity;

        float forwardSpeed = Vector2.Dot(currentVelocity, forwardDirection);
        Vector2 forwardVelocity = forwardDirection * forwardSpeed;
        Vector2 lateralVelocity = currentVelocity - forwardVelocity;

        lateralVelocity *= movement.lateralDamping;

        _rb.linearVelocity = forwardVelocity + lateralVelocity;
    }

    protected void ClampVelocity()
    {
        if (_rb.linearVelocity.magnitude > movement.maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * movement.maxSpeed;
        }
    }

    // ===== DAMAGE SYSTEM =====
    public virtual void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile, Entity attacker = null, int accuracyAttackId = Player.InvalidAttackId)
    {
        if (_isDead || _ignoreIncomingDamage) return;

        // Keep legacy call ordering: post hook is invoked before pre-apply hook.
        _augmentController?.OnTakeDamage(damage, impactForce, hitPoint, source);
        bool artificialFairyTriggered = _augmentController != null && _augmentController.ConsumeArtificialFairyTriggeredFlag();

        // --- NEW: Allow augments to modify or cancel incoming damage before it's applied ---
        bool shieldIgnored = false;
        bool healthIgnored = false;

        // Give each augment a chance to modify the damage or mark portions ignored
        _augmentController?.OnBeforeTakeDamage(ref damage, ref shieldIgnored, ref healthIgnored, source);

        if (hitPoint != Vector3.zero)
        {
            _lastDamageDirection = ((Vector2)transform.position - (Vector2)hitPoint).normalized;
        }
        else
        {
            _lastDamageDirection = Vector2.zero;
        }

        bool hasShield = currentShield > 0 && !shieldIgnored;
        bool shieldTookHit = false;
        bool shieldBroke = false;
        float appliedShieldDamage = 0f;
        float appliedHealthDamage = 0f;

        if (hasShield)
        {
            bool hadShields = currentShield > 0;

            float shieldDamage = Mathf.Min(currentShield, damage);
            currentShield -= shieldDamage;
            appliedShieldDamage = shieldDamage;
            shieldTookHit = shieldDamage > 0f;
            OnShieldChanged();

            if (shieldController != null)
            {
                if (hadShields && currentShield <= 0)
                {
                    shieldBroke = true;
                    shieldController.BreakShield();
                }
                else if (currentShield > 0 && source != DamageSource.LaserBeam)
                {
                    Vector3 collisionPoint = hitPoint != Vector3.zero ? hitPoint : transform.position;
                    shieldController.OnHit(collisionPoint);
                }
            }

            if (shieldDamage >= damage)
            {
                RecordDamageStats(attacker, accuracyAttackId, appliedShieldDamage);
                BroadcastAuthoritativeCombatState(hitPoint, source, shieldTookHit, shieldBroke, impactForce, shieldIgnored || healthIgnored, artificialFairyTriggered);
                return;
            }

            damage -= shieldDamage;
        }

        if (!healthIgnored)
        {
            appliedHealthDamage = Mathf.Min(currentHealth, damage);
            currentHealth -= damage;
        }
        OnHealthChanged();

        if (currentHealth <= 0)
        {
            Die();
        }

        RecordDamageStats(attacker, accuracyAttackId, appliedShieldDamage + appliedHealthDamage);
        BroadcastAuthoritativeCombatState(hitPoint, source, shieldTookHit, shieldBroke, impactForce, shieldIgnored || healthIgnored, artificialFairyTriggered);
    }

    /// <summary>
    /// Restore health to this entity (clamped to maxHealth).
    /// Intended for augments and other game systems that heal the entity.
    /// Does nothing if amount is non-positive or the entity is already dead.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (currentHealth <= 0f) return; // dead or destroyed

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged();
    }

    protected virtual void ScatterShipParts()
    {
        if (visualEffects.visualModel == null) return;

        ShipPartScatter[] parts = visualEffects.visualModel.GetComponentsInChildren<ShipPartScatter>();
        if (parts.Length == 0) return;

        Vector2 scatterDirection = _lastDamageDirection != Vector2.zero
            ? _lastDamageDirection
            : Random.insideUnitCircle.normalized;

        foreach (ShipPartScatter part in parts)
        {
            if (Random.value < 0.75f)
            {
                part.Scatter(scatterDirection);
            }
        }
    }

    /// <summary>
    /// Deals damage directly to health, bypassing shields entirely.
    /// Used by physical projectiles that ignore shields.
    /// </summary>
    public virtual void TakeDirectDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile, Entity attacker = null, int accuracyAttackId = Player.InvalidAttackId)
    {
        if (_isDead || _ignoreIncomingDamage) return;

        // Keep legacy call ordering: post hook is invoked before pre-apply hook.
        _augmentController?.OnTakeDirectDamage(damage, impactForce, hitPoint, source);

        // Allow augments to cancel or modify direct damage before it's applied
        bool healthIgnored = false;
        _augmentController?.OnBeforeTakeDirectDamage(ref damage, ref healthIgnored, source);

        if (hitPoint != Vector3.zero)
        {
            _lastDamageDirection = ((Vector2)transform.position - (Vector2)hitPoint).normalized;
        }
        else
        {
            _lastDamageDirection = Vector2.zero;
        }

        float appliedHealthDamage = 0f;
        if (!healthIgnored)
        {
            appliedHealthDamage = Mathf.Min(currentHealth, damage);
            currentHealth -= damage;
        }
        OnHealthChanged();

        if (currentHealth <= 0)
        {
            Die();
        }

        RecordDamageStats(attacker, accuracyAttackId, appliedHealthDamage);
        BroadcastAuthoritativeCombatState(hitPoint, source, false, false, impactForce, healthIgnored, false);
    }

    protected void RecordDamageStats(Entity attacker, int accuracyAttackId, float appliedDamage)
    {
        if (appliedDamage <= 0f)
        {
            return;
        }

        if (this is Player targetPlayer)
        {
            targetPlayer.RecordDamageTaken(appliedDamage);

            if (attacker is Player attackingPlayer)
            {
                attackingPlayer.RecordDamageDealt(appliedDamage);
                attackingPlayer.RegisterAttackHit(accuracyAttackId);
            }
        }
    }

    public void ApplyAuthoritativeCombatState(float health, float shield, Vector2 hitPoint, DamageSource source, bool shieldHit, bool shieldBreak)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        currentShield = Mathf.Clamp(shield, 0f, maxShield);
        OnHealthChanged();
        OnShieldChanged();

        if (shieldController == null || hitPoint == Vector2.zero)
        {
            if (shieldController != null && shieldBreak)
            {
                shieldController.BreakShield();
            }
            return;
        }

        if (shieldBreak)
        {
            shieldController.BreakShield();
            return;
        }

        if (!shieldHit)
        {
            return;
        }

        if (source == DamageSource.LaserBeam)
        {
            shieldController.OnLaserHit(hitPoint);
        }
        else
        {
            shieldController.OnHit(hitPoint);
        }
    }

    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Broadcast death to remote clients before destroying so they can play effects
        BroadcastNetworkDeath();

        onDeath?.Invoke(this);

        ScatterShipParts();

        if (visualEffects.explosionEffectPrefab != null)
        {
            if (ExplosionPool.Instance != null)
            {
                Vector2? impactDir = _lastDamageDirection != Vector2.zero ? _lastDamageDirection : (Vector2?)null;
                ExplosionPool.Instance.GetExplosion(transform.position, transform.rotation, visualEffects.explosionScale, impactDir);
            }
            else
            {
                GameObject explosion = Instantiate(visualEffects.explosionEffectPrefab, transform.position, transform.rotation);
                explosion.transform.localScale = Vector3.one * visualEffects.explosionScale;

                if (_lastDamageDirection != Vector2.zero)
                {
                    ExplosionScript explosionScript = explosion.GetComponent<ExplosionScript>();
                    if (explosionScript != null)
                    {
                        explosionScript.SetImpactDirection(_lastDamageDirection);
                    }
                }
            }
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Despawn(true);
            }

            return;
        }

        Destroy(gameObject);
    }

    private void BroadcastNetworkDeath()
    {
        if (!NetTickUtil.IsActive)
        {
            return;
        }

        NetMovement netMovement = GetComponent<NetMovement>();
        if (netMovement == null || !netMovement.IsServer)
        {
            return;
        }

        float rotZ = transform.rotation.eulerAngles.z;
        netMovement.BroadcastDeath(transform.position, rotZ, _lastDamageDirection);
    }

    // ===== RECOIL & VISUAL EFFECTS =====
    public void ApplyRecoil(float recoilForce)
    {
        if (_rb != null)
        {
            Vector2 impulse = (Vector2)(-transform.up) * recoilForce;
            _rb.linearVelocity += impulse;
            _recentImpulse += impulse;
        }
    }

    private void UpdateVisualRotation(float deltaTime)
    {
        if (visualEffects.visualModel == null) return;
        if (_useExternalVisualState)
        {
            ApplyVisualModelRotation();
            return;
        }
        if (deltaTime <= 0f) return;

        float currentZRotation = transform.eulerAngles.z;
        float deltaRotation = Mathf.DeltaAngle(_previousRotationZ, currentZRotation);
        float angularVelocity = deltaRotation / deltaTime;
        _previousRotationZ = currentZRotation;

        float targetBankAngle = Mathf.Clamp(
            -angularVelocity * visualEffects.bankSensitivity,
            -visualEffects.maxBankAngle,
            visualEffects.maxBankAngle
        );

        float forwardAcceleration = Vector2.Dot(_acceleration, new Vector2(transform.up.x, transform.up.y));
        float impulseContribution = Vector2.Dot(_recentImpulse, new Vector2(transform.up.x, transform.up.y));

        float targetPitchAngle = Mathf.Clamp(
            (forwardAcceleration * visualEffects.pitchSensitivity) + (impulseContribution * visualEffects.impulseRecoilSensitivity),
            -visualEffects.maxPitchAngle,
            visualEffects.maxPitchAngle
        );

        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBankAngle, deltaTime * visualEffects.bankSmoothing);
        _currentPitchAngle = Mathf.Lerp(_currentPitchAngle, targetPitchAngle, deltaTime * visualEffects.pitchSmoothing);

        _recentImpulse = Vector2.Lerp(_recentImpulse, Vector2.zero, deltaTime * _impulseDecayRate);

        ApplyVisualModelRotation();
    }

    private void ApplyVisualModelRotation()
    {
        if (visualEffects.visualModel == null) return;

        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchAngle, Vector3.right);
        Quaternion bankQuat = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        Quaternion finalRot = _visualBaseLocalRotation * pitchQuat * bankQuat;
        visualEffects.visualModel.localRotation = finalRot;
    }

    public void SetExternalVisualStateEnabled(bool enabled)
    {
        _useExternalVisualState = enabled;

        if (!enabled)
        {
            _previousRotationZ = transform.eulerAngles.z;
        }
    }

    public void GetVisualTiltState(out float bankAngle, out float pitchAngle)
    {
        bankAngle = _currentBankAngle;
        pitchAngle = _currentPitchAngle;
    }

    public void RefreshVisualState(float deltaTime)
    {
        UpdateVisualRotation(deltaTime);
    }

    public void ApplyExternalVisualTiltState(float bankAngle, float pitchAngle)
    {
        if (visualEffects.visualModel == null) return;

        _useExternalVisualState = true;
        _currentBankAngle = bankAngle;
        _currentPitchAngle = pitchAngle;
        _previousRotationZ = transform.eulerAngles.z;
        ApplyVisualModelRotation();
    }

    /// <summary>
    /// Sets thrust state and updates thruster visuals. Used by NetMovement
    /// to drive thruster effects on remote clients where Player is disabled.
    /// </summary>
    public void ApplyNetworkThrustState(bool isThrusting)
    {
        _isThrusting = isThrusting;
        UpdateThrusters();
    }

    /// <summary>
    /// Updates shield value and refreshes HUD. Used by NetMovement to sync
    /// shield state on remote clients where Player.HandleShieldRegeneration doesn't run.
    /// </summary>
    public void ApplyNetworkShieldState(float shield)
    {
        currentShield = Mathf.Clamp(shield, 0f, maxShield);
        OnShieldChanged();
    }

    public void ApplyNetworkHealthState(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        OnHealthChanged();
    }

    private void UpdateThrusters()
    {
        if (Time.deltaTime <= 0f) return;
        if (thrusters.thrusters == null || thrusters.thrusters.Length == 0) return;

        float targetIntensity = _isThrusting ? 1f : 0f;
        
        // Ensure rampTime isn't 0 to avoid division by zero
        float rampStep = (thrusters.rampTime > 0) ? (1f / thrusters.rampTime) * Time.deltaTime : 1f;
        
        _currentThrusterIntensity = Mathf.MoveTowards(
            _currentThrusterIntensity,
            targetIntensity,
            rampStep
        );

        foreach (var thruster in thrusters.thrusters)
        {
            if (thruster == null || !_thrusterOriginalValues.ContainsKey(thruster))
                continue;

            var emission = thruster.emission;
            var main = thruster.main;
            var originals = _thrusterOriginalValues[thruster];

            // 1. Fix Emission: Always multiply against a constant base value, not the current value
            // Assuming a base rate of 50 if you don't have it stored, 
            // OR better: use a fixed variable or the original rate if you store it in Awake.
            emission.rateOverTime = 50f * _currentThrusterIntensity;

            // 2. Restore Speed and Lifetime: These were missing in your provided script
            main.startSpeed = new ParticleSystem.MinMaxCurve(originals.speed.constant * _currentThrusterIntensity);
            main.startLifetime = new ParticleSystem.MinMaxCurve(originals.lifetime.constant * _currentThrusterIntensity);

            // Handle color inversion
            if (_thrusterOriginalColors.ContainsKey(thruster))
            {
                if (thrusters.invertColors)
                {
                    Color orig = _thrusterOriginalColors[thruster];
                    Color inv = new Color(1f - orig.r, 1f - orig.g, 1f - orig.b, orig.a);
                    main.startColor = inv;
                }
                else
                {
                    main.startColor = _thrusterOriginalColors[thruster];
                }
            }
        }
    }

    protected virtual void OnHealthChanged()
    {
    }

    protected virtual void OnShieldChanged()
    {
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision) // Collision-based contact since ships are not triggers, this will capture physical collisions with other entities for thorns and similar effects
    {
        _augmentController?.OnContact(collision);
    }

    public void SetCurrentRound(int round)
    {
        currentRound = round;
        _augmentController?.SetCurrentRound(round);
    }

    public void TickAugmentEffects()
    {
        _augmentController?.ExecuteEffects();
    }

    public void NotifyAuthoritativeDamageReceived(DamageSource source)
    {
        _augmentController?.OnTakeDamage(0f, 0f, transform.position, source);
    }

    public void NotifyNetworkEvasionTriggered()
    {
        _augmentController?.NotifyEvasionTriggered();
    }

    public void NotifyNetworkArtificialFairyTriggered()
    {
        _augmentController?.NotifyArtificialFairyTriggered();
    }

    public void SetIncomingDamageIgnored(bool ignored)
    {
        _ignoreIncomingDamage = ignored;
    }

    public void SetShieldValue(float value, bool notify = true, bool clampToMax = true)
    {
        currentShield = clampToMax ? Mathf.Clamp(value, 0f, maxShield) : Mathf.Max(0f, value);
        if (notify)
        {
            OnShieldChanged();
        }
    }

    private void BroadcastAuthoritativeCombatState(Vector3 hitPoint, DamageSource source, bool shieldHit, bool shieldBreak, float impactForce, bool evasionTriggered, bool artificialFairyTriggered)
    {
        if (!NetTickUtil.IsActive)
        {
            return;
        }

        NetMovement netMovement = GetComponent<NetMovement>();
        if (netMovement == null || !netMovement.IsServer)
        {
            return;
        }

        netMovement.BroadcastCombatState(currentHealth, currentShield, hitPoint, source, shieldHit, shieldBreak, impactForce, evasionTriggered, artificialFairyTriggered);
    }

    public void SetMaxHealthAndClampCurrent(float newMaxHealth, bool notify = true)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        if (notify)
        {
            OnHealthChanged();
        }
    }

    // ===== SLOW EFFECT SYSTEM =====

    public void ApplySlow(float slowMultiplier, float duration)
    {
        bool wasSlowed = IsSlowed();

        // Only apply if this slow is stronger or refreshes duration
        if (slowMultiplier < _slowMultiplier || Time.time + duration > _slowEndTime)
        {
            _slowMultiplier = slowMultiplier;
            _slowEndTime = Time.time + duration;
        }

        // Start visuals if not already active
        if (!wasSlowed && IsSlowed())
        {
            StartSlowVisuals();
        }
    }

    public float GetSlowMultiplier()
    {
        if (Time.time >= _slowEndTime)
        {
            _slowMultiplier = 1f;
            return 1f;
        }
        return _slowMultiplier;
    }

    public bool IsSlowed()
    {
        return Time.time < _slowEndTime && _slowMultiplier < 1f;
    }

    public float GetSlowRemainingTime()
    {
        float remaining = _slowEndTime - Time.time;
        return remaining > 0f ? remaining : 0f;
    }

    private void StartSlowVisuals()
    {
        _slowVisualsActive = true;

        if (slowEffectVisuals.slowParticleSystem != null)
        {
            slowEffectVisuals.slowParticleSystem.Play();
        }
    }

    private void StopSlowVisuals()
    {
        _slowVisualsActive = false;

        if (slowEffectVisuals.slowParticleSystem != null)
        {
            slowEffectVisuals.slowParticleSystem.Stop();
        }
    }

    private void UpdateSlowVisuals()
    {
        if (slowEffectVisuals.slowParticleSystem == null) return;

        // Check if slow just ended
        if (_slowVisualsActive && !IsSlowed())
        {
            StopSlowVisuals();
        }
    }

    /// <summary>
    /// Checks and stops slow visuals when the slow effect expires.
    /// Used by NetMovement for remote/server copies where Entity.Update doesn't run.
    /// </summary>
    public void TickSlowVisuals()
    {
        UpdateSlowVisuals();
    }

    // ===== AUGMENTS =====
    public void AcquireAugment(Augment augment, int currentRound)
    {
        if (augment == null) return;

        SetCurrentRound(currentRound);
        augments.Add(augment);
        _augmentController?.AcquireAugment(augment, currentRound);
        BroadcastNetworkAugmentLoadout();
    }

    public void ImportAugmentLoadout(List<AugmentLoadoutEntry> entries, int round)
    {
        SetCurrentRound(round);
        augments.Clear();

        if (entries != null)
        {
            foreach (AugmentLoadoutEntry entry in entries)
            {
                if (entry == null || entry.definition == null) continue;
                augments.Add(entry.definition);
            }
        }

        _augmentController?.ImportLoadout(entries, round);
        BroadcastNetworkAugmentLoadout();
    }

    public List<AugmentLoadoutEntry> ExportAugmentLoadout()
    {
        if (_augmentController == null)
        {
            return new List<AugmentLoadoutEntry>();
        }

        return _augmentController.ExportLoadout();
    }

    public List<NetworkAugmentLoadoutEntry> ExportNetworkAugmentLoadout()
    {
        if (_augmentController == null)
        {
            return new List<NetworkAugmentLoadoutEntry>();
        }

        return _augmentController.ExportNetworkLoadout();
    }

    public void ImportNetworkAugmentLoadout(List<NetworkAugmentLoadoutEntry> entries, int round)
    {
        SetCurrentRound(round);
        augments.Clear();

        if (entries != null && GameDataManager.Instance != null)
        {
            foreach (NetworkAugmentLoadoutEntry entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                Augment augment = GameDataManager.Instance.GetAugmentById(entry.augmentId);
                if (augment != null)
                {
                    augments.Add(augment);
                }
            }
        }

        _augmentController?.ImportNetworkLoadout(entries, round);
    }

    private void BroadcastNetworkAugmentLoadout()
    {
        if (!NetTickUtil.IsActive)
        {
            return;
        }

        NetMovement netMovement = GetComponent<NetMovement>();
        if (netMovement == null || !netMovement.IsServer)
        {
            return;
        }

        netMovement.SyncCurrentAugmentLoadoutAuthoritative(currentRound, ExportNetworkAugmentLoadout());
    }

    public void SetAugmentVariables()
    {
        SetDamageMultiplier();
        SetSpeedMultiplier();
        SetRotationMultiplier();
    }

    void SetDamageMultiplier()
    {
        float total = 1.0f;
        foreach (var mult in damageMultipliers.Values)
        {
            total *= mult;
        }
        projectileWeapon.damage = projectileWeapon.baseDamage * total;
    }

    void SetSpeedMultiplier()
    {
        float total = 1.0f;
        foreach (var mult in speedMultipliers.Values)
        {
            total *= mult;
        }
        movement.maxSpeed = baseMaxSpeed * total;
    }

    void SetRotationMultiplier()
    {
        float total = 1.0f;
        foreach (var mult in rotationMultipliers.Values)
        {
            total *= mult;
        }
        movement.rotationSpeed = baseRotationSpeed * total;
    }
}
