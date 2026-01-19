using System.Collections.Generic;
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

[RequireComponent(typeof(Rigidbody2D))]
public abstract class ShipBase : MonoBehaviour
{
    [Header("Shield Settings")]
    public ShieldController shieldController;
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float maxShield = 50f;
    public bool hasShield = true;

    [Header("Movement Settings")]
    public float thrustForce = 10f;
    public float maxSpeed = 15f;
    public float rotationSpeed = 200f;
    public float lateralDamping = 0.92f;

    [Header("Combat Settings")]
    public List<GameObject> turrets = new();
    public ProjectileWeaponConfig projectileWeapon;
    public BeamWeaponConfig beamWeapon;

    [Header("Visual Effects")]
    public Transform visualModel;
    public float maxBankAngle = 45f;
    public float bankSensitivity = 0.1f;
    public float bankSmoothing = 5f;
    public float maxPitchAngle = 10f;
    public float pitchSensitivity = 0.5f;
    public float pitchSmoothing = 5f;

    [Tooltip("Multiplier for how much recoil/impulse affects pitch (independent of thrust pitch)")]
    public float impulseRecoilSensitivity = 1f;

    public GameObject explosionEffectPrefab;
    public float explosionScale = 1f;

    [Header("Thruster Settings")]
    [Tooltip("List of thruster particle systems attached to this ship")]
    public List<ParticleSystem> thrusters = new();

    [Tooltip("Time to ramp thruster emission up/down (seconds)")]
    public float thrusterRampTime = 0.3f;

    [Tooltip("Maximum emission rate when fully thrusting")]
    public float maxEmissionRate = 50f;

    [Tooltip("Minimum emission rate when not thrusting")]
    public float minEmissionRate = 0f;

    [Tooltip("Particle speed multiplier when fully thrusting")]
    public float thrustingSpeedMultiplier = 1f;

    [Tooltip("Particle speed multiplier when not thrusting")]
    public float idleSpeedMultiplier = 0f;

    [Tooltip("Particle lifetime multiplier when fully thrusting")]
    public float thrustingLifetimeMultiplier = 1f;

    [Tooltip("Particle lifetime multiplier when not thrusting")]
    public float idleLifetimeMultiplier = 0f;

    protected float currentHealth;
    protected float currentShield;
    protected Rigidbody2D _rb;
    protected bool _isThrusting = false;
    protected Vector2 _lastDamageDirection;
    private bool _isDead = false;

    private float _previousRotationZ;
    private Vector2 _previousVelocity;
    private Vector2 _acceleration;
    private float _currentBankAngle;
    private float _currentPitchAngle;
    private Quaternion _visualBaseLocalRotation;
    private float _currentThrusterIntensity = 0f;
    private Dictionary<ParticleSystem, (ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve lifetime)> _thrusterOriginalValues = new();
    private Vector2 _recentImpulse = Vector2.zero;
    private float _impulseDecayRate = 5f;

    protected const float ROTATION_OFFSET = -90f;

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

        // Freeze rotation to prevent collisions from causing unwanted spinning
        // Rotation is controlled by code (transform.rotation), not physics
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (visualModel != null)
        {
            _visualBaseLocalRotation = visualModel.localRotation;
        }
        else
        {
            _visualBaseLocalRotation = Quaternion.identity;
            Debug.LogWarning($"Visual model not assigned on {gameObject.name}. Visual banking/pitching will not work. Please assign the ship's visual mesh/sprite as a child object.");
        }

        // Cache original thruster particle system values
        foreach (var thruster in thrusters)
        {
            if (thruster != null)
            {
                var main = thruster.main;
                _thrusterOriginalValues[thruster] = (main.startSpeed, main.startLifetime);
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        if (_rb == null) return;

        Vector2 currentVelocity = _rb.linearVelocity;
        _acceleration = (currentVelocity - _previousVelocity) / Time.fixedDeltaTime;
        _previousVelocity = currentVelocity;
    }

    protected virtual void Update()
    {
        UpdateThrusters();
    }

    protected virtual void LateUpdate()
    {
        UpdateVisualRotation();
    }

    protected void ApplyLateralDamping()
    {
        Vector2 forwardDirection = transform.up;
        Vector2 currentVelocity = _rb.linearVelocity;

        float forwardSpeed = Vector2.Dot(currentVelocity, forwardDirection);
        Vector2 forwardVelocity = forwardDirection * forwardSpeed;
        Vector2 lateralVelocity = currentVelocity - forwardVelocity;

        lateralVelocity *= lateralDamping;

        _rb.linearVelocity = forwardVelocity + lateralVelocity;
    }

    protected void ClampVelocity()
    {
        if (_rb.linearVelocity.magnitude > maxSpeed)
        {
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }
    }

public virtual void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        // Prevent multiple damage calls after death
        if (_isDead) return;

        // Calculate damage direction (from attacker position to this ship)
        // This way sparks always point away from whoever dealt the damage
        if (hitPoint != Vector3.zero)
        {
            _lastDamageDirection = ((Vector2)transform.position - (Vector2)hitPoint).normalized;
        }
        else
        {
            _lastDamageDirection = Vector2.zero; // No direction info available
        }

        if (hasShield)
        {
            // 1. Capture state BEFORE damage is applied
            bool hadShields = currentShield > 0;

            // 2. Calculate damage to shield
            float shieldDamage = Mathf.Min(currentShield, damage);
            currentShield -= shieldDamage;
            OnShieldChanged();

            // 3. Handle Visuals based on the NEW shield state
            if (shieldController != null)
            {
                // If we had shields but now they are gone (or 0), trigger the break effect
                if (hadShields && currentShield <= 0)
                {
                    shieldController.BreakShield();
                    // Optional: Play shield break sound here
                }
                // Otherwise, play normal hit effect (excluding lasers which usually have their own loop)
                else if (currentShield > 0 && source != DamageSource.LaserBeam)
                {
                    Vector3 collisionPoint = hitPoint != Vector3.zero ? hitPoint : transform.position;
                    shieldController.OnHit(collisionPoint);
                }
            }

            // 4. Update shield state flags
            if (currentShield <= 0)
            {
                hasShield = false;
            }

            // 5. If shield absorbed all damage, exit here
            if (shieldDamage >= damage) return;

            // 6. Calculate overflow damage for health
            damage -= shieldDamage;
        }

        // Apply remaining damage to health
        currentHealth -= damage;
        OnHealthChanged();

        // Notify about impact (can be overridden by subclasses)

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void ScatterShipParts()
    {
        if (visualModel == null) return;

        ShipPartScatter[] parts = visualModel.GetComponentsInChildren<ShipPartScatter>();
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

    protected virtual void Die()
    {
        // Prevent multiple death calls
        if (_isDead) return;
        _isDead = true;

        // Scatter ship parts before explosion
        ScatterShipParts();

        if (explosionEffectPrefab != null)
        {
            // Use pool if available, otherwise fallback to instantiate
            if (ExplosionPool.Instance != null)
            {
                Vector2? impactDir = _lastDamageDirection != Vector2.zero ? _lastDamageDirection : (Vector2?)null;
                ExplosionPool.Instance.GetExplosion(transform.position, transform.rotation, explosionScale, impactDir);
            }
            else
            {
                GameObject explosion = Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
                explosion.transform.localScale = Vector3.one * explosionScale;

                // Set impact direction if we have it
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

        Destroy(gameObject);
    }

    protected void ApplyRecoil(float recoilForce)
    {
        if (_rb != null)
        {
            // modify linearVelocity directly to remain consistent with custom impulses
            Vector2 impulse = (Vector2)(-transform.up) * recoilForce;
            _rb.linearVelocity += impulse;

            // Track this impulse for visual feedback
            _recentImpulse += impulse;
        }
    }

    private void UpdateVisualRotation()
    {
        if (visualModel == null) return;

        // Skip visual rotation update when deltaTime is zero (game paused)
        // to prevent division by zero and NaN corruption
        if (Time.deltaTime <= 0f) return;

        float currentZRotation = transform.eulerAngles.z;
        float deltaRotation = Mathf.DeltaAngle(_previousRotationZ, currentZRotation);
        float angularVelocity = deltaRotation / Time.deltaTime;
        _previousRotationZ = currentZRotation;

        float targetBankAngle = Mathf.Clamp(
            -angularVelocity * bankSensitivity,
            -maxBankAngle,
            maxBankAngle
        );

        float forwardAcceleration = Vector2.Dot(_acceleration, new Vector2(transform.up.x, transform.up.y));

        // Add impulse contribution to pitch (with separate sensitivity)
        float impulseContribution = Vector2.Dot(_recentImpulse, new Vector2(transform.up.x, transform.up.y));

        float targetPitchAngle = Mathf.Clamp(
            (forwardAcceleration * pitchSensitivity) + (impulseContribution * impulseRecoilSensitivity),
            -maxPitchAngle,
            maxPitchAngle
        );

        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBankAngle, Time.deltaTime * bankSmoothing);
        _currentPitchAngle = Mathf.Lerp(_currentPitchAngle, targetPitchAngle, Time.deltaTime * pitchSmoothing);

        // Decay the impulse over time
        _recentImpulse = Vector2.Lerp(_recentImpulse, Vector2.zero, Time.deltaTime * _impulseDecayRate);

        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchAngle, Vector3.right);
        Quaternion bankQuat = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        Quaternion finalRot = _visualBaseLocalRotation * pitchQuat * bankQuat;

        visualModel.localRotation = finalRot;
    }

    private void UpdateThrusters()
    {
        // Skip thruster update when deltaTime is zero (game paused)
        if (Time.deltaTime <= 0f) return;

        // Ramp intensity towards target (1 when thrusting, 0 when not)
        float targetIntensity = _isThrusting ? 1f : 0f;
        _currentThrusterIntensity = Mathf.MoveTowards(
            _currentThrusterIntensity,
            targetIntensity,
            (1f / thrusterRampTime) * Time.deltaTime
        );

        // Apply to all thruster particle systems
        foreach (var thruster in thrusters)
        {
            if (thruster == null || !_thrusterOriginalValues.ContainsKey(thruster))
                continue;

            var emission = thruster.emission;
            var main = thruster.main;
            var originalValues = _thrusterOriginalValues[thruster];

            // Interpolate emission rate
            emission.rateOverTime = Mathf.Lerp(
                minEmissionRate,
                maxEmissionRate,
                _currentThrusterIntensity
            );

            // Interpolate particle speed multiplier
            float speedMultiplier = Mathf.Lerp(
                idleSpeedMultiplier,
                thrustingSpeedMultiplier,
                _currentThrusterIntensity
            );

            // Apply speed multiplier to cached original speed
            if (originalValues.speed.mode == ParticleSystemCurveMode.Constant)
            {
                main.startSpeed = originalValues.speed.constant * speedMultiplier;
            }
            else if (originalValues.speed.mode == ParticleSystemCurveMode.TwoConstants)
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(
                    originalValues.speed.constantMin * speedMultiplier,
                    originalValues.speed.constantMax * speedMultiplier
                );
            }

            // Interpolate particle lifetime multiplier
            float lifetimeMultiplier = Mathf.Lerp(
                idleLifetimeMultiplier,
                thrustingLifetimeMultiplier,
                _currentThrusterIntensity
            );

            // Apply lifetime multiplier to cached original lifetime
            if (originalValues.lifetime.mode == ParticleSystemCurveMode.Constant)
            {
                main.startLifetime = originalValues.lifetime.constant * lifetimeMultiplier;
            }
            else if (originalValues.lifetime.mode == ParticleSystemCurveMode.TwoConstants)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(
                    originalValues.lifetime.constantMin * lifetimeMultiplier,
                    originalValues.lifetime.constantMax * lifetimeMultiplier
                );
            }
        }
    }

    protected virtual void OnHealthChanged()
    {
    }

    protected virtual void OnShieldChanged()
    {
    }

}
