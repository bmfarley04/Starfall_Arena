using UnityEngine;

public enum DamageSource3D
{
    Projectile,
    Beam,
    Direct
}

[RequireComponent(typeof(ShipFlight3D))]
public abstract class Entity3D : MonoBehaviour
{
    private const float MaxPrimaryWeaponEnergyCapacity = 100f;

    [System.Serializable]
    private struct PrimaryWeaponEnergyConfig3D
    {
        [Tooltip("How quickly the primary-weapon overheat meter cools back toward zero when not actively building heat.")]
        public float recoveryPerSecond;
    }

    [Header("3D Combat")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float maxShield = 50f;
    [SerializeField] protected ShieldController shieldController;
    [Header("Primary Weapon Energy")]
    [SerializeField] private PrimaryWeaponEnergyConfig3D primaryWeaponEnergy = new PrimaryWeaponEnergyConfig3D
    {
        recoveryPerSecond = 60f
    };

    [Header("3D Visual Feedback")]
    [Tooltip("Multiplier for how much recoil/impulse affects visual pitch independent of thrust pitch.")]
    [SerializeField] protected float impulseRecoilPitchSensitivity = 1f;

    [Header("Abilities")]
    [SerializeField] protected Ability3D[] abilities = new Ability3D[4];

    [Header("3D Systems")]
    [SerializeField] protected ShipFlight3D shipFlight;
    [SerializeField] protected ShipVisualTilt3D shipVisualTilt;
    [SerializeField] protected ShipThrusterVfx3D shipThrusterVfx;
    [SerializeField] protected ShipSpeedFx3D shipSpeedFx;
    [SerializeField] protected ProjectileWeapon3D primaryWeapon;
    [SerializeField] protected DeathEffects3D deathEffects;

    protected float currentHealth;
    protected float currentShield;
    protected float currentPrimaryWeaponEnergy;
    protected Vector3 lastDamageDirection;
    protected float currentSlowMultiplier = 1f;
    protected float slowEndTime;

    private bool _isDead;

    public ShipFlight3D Flight => shipFlight;
    public ShipVisualTilt3D VisualTilt => shipVisualTilt;
    public ShipThrusterVfx3D ThrusterVfx => shipThrusterVfx;
    public ShipSpeedFx3D SpeedFx => shipSpeedFx;
    public ProjectileWeapon3D PrimaryWeapon => primaryWeapon;
    public Ability3D[] Abilities => abilities;
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float CurrentPrimaryWeaponEnergy => currentPrimaryWeaponEnergy;
    public float MaxPrimaryWeaponEnergy => MaxPrimaryWeaponEnergyCapacity;
    public float CurrentPrimaryWeaponEnergyNormalized => currentPrimaryWeaponEnergy / MaxPrimaryWeaponEnergyCapacity;
    public float ImpulseRecoilPitchSensitivity => impulseRecoilPitchSensitivity;
    public float CurrentSlowMultiplier => GetSlowMultiplier();
    public bool IsSlowed => Time.time < slowEndTime && currentSlowMultiplier < 1f;

    public Ability3D GetAbility(int index)
    {
        if (index < 0 || index >= abilities.Length) return null;
        return abilities[index];
    }

    public float GetCombinedRotationMultiplier()
    {
        float multiplier = GetExternalRotationMultiplier();
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability3D ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            multiplier *= ability.GetRotationMultiplier();
        }

        return multiplier;
    }

    public float GetCombinedThrustMultiplier()
    {
        float multiplier = GetExternalThrustMultiplier();
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability3D ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            multiplier *= ability.GetThrustMultiplier();
        }

        return multiplier;
    }

    public bool IsPrimaryFireDisabledByAbility()
    {
        for (int i = 0; i < abilities.Length; i++)
        {
            Ability3D ability = abilities[i];
            if (ability != null && ability.DisablePrimaryFire())
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        shipVisualTilt ??= GetComponent<ShipVisualTilt3D>();
        shipThrusterVfx ??= GetComponent<ShipThrusterVfx3D>();
        shipSpeedFx ??= GetComponent<ShipSpeedFx3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
        deathEffects ??= GetComponent<DeathEffects3D>();
        shieldController ??= GetComponentInChildren<ShieldController>(true);
        currentHealth = maxHealth;
        currentShield = maxShield;
        currentPrimaryWeaponEnergy = 0f;
        lastDamageDirection = Vector3.zero;
        currentSlowMultiplier = 1f;
        slowEndTime = 0f;
    }

    private void LateUpdate()
    {
        RecoverPrimaryWeaponEnergy(Time.deltaTime);
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Entity3D attacker = null, DamageSource3D source = DamageSource3D.Projectile)
    {
        if (damage <= 0f || currentHealth <= 0f || _isDead)
        {
            return;
        }

        lastDamageDirection = ResolveDamageDirection(hitPoint);

        if (currentShield > 0f)
        {
            float shieldDamage = Mathf.Min(currentShield, damage);
            currentShield -= shieldDamage;
            damage -= shieldDamage;
            OnShieldChanged();

            if (shieldController != null)
            {
                if (currentShield <= 0f)
                {
                    shieldController.BreakShield();
                }
                else
                {
                    Vector3 collisionPoint = hitPoint != Vector3.zero ? hitPoint : transform.position;
                    shieldController.OnHit(collisionPoint);
                }
            }

            if (damage <= 0f)
            {
                return;
            }
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public virtual void TakeDirectDamage(float damage, Vector3 hitPoint, Entity3D attacker = null)
    {
        if (damage <= 0f || currentHealth <= 0f || _isDead)
        {
            return;
        }

        lastDamageDirection = ResolveDamageDirection(hitPoint);
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void ApplySlow(float slowMultiplier, float duration)
    {
        float clampedMultiplier = Mathf.Clamp01(slowMultiplier);
        if (duration <= 0f || clampedMultiplier >= 1f)
        {
            return;
        }

        if (clampedMultiplier < currentSlowMultiplier || Time.time + duration > slowEndTime)
        {
            currentSlowMultiplier = clampedMultiplier;
            slowEndTime = Time.time + duration;
        }
    }

    public float GetSlowMultiplier()
    {
        if (Time.time >= slowEndTime)
        {
            currentSlowMultiplier = 1f;
            slowEndTime = 0f;
        }

        return currentSlowMultiplier;
    }

    public bool CanSpendPrimaryWeaponEnergy(float cost)
    {
        float clampedCost = Mathf.Max(0f, cost);
        if (clampedCost <= 0f)
        {
            return true;
        }

        return currentPrimaryWeaponEnergy + clampedCost <= MaxPrimaryWeaponEnergyCapacity + 0.001f;
    }

    public bool TrySpendPrimaryWeaponEnergy(float cost)
    {
        float clampedCost = Mathf.Max(0f, cost);
        if (!CanSpendPrimaryWeaponEnergy(clampedCost))
        {
            return false;
        }

        if (clampedCost <= 0f)
        {
            return true;
        }

        currentPrimaryWeaponEnergy = Mathf.Min(MaxPrimaryWeaponEnergyCapacity, currentPrimaryWeaponEnergy + clampedCost);
        OnPrimaryWeaponEnergyChanged();
        return true;
    }

    public void RecoverPrimaryWeaponEnergy(float deltaTime)
    {
        if (deltaTime <= 0f || currentPrimaryWeaponEnergy <= 0f)
        {
            return;
        }

        float recoveryRate = Mathf.Max(0f, primaryWeaponEnergy.recoveryPerSecond);
        if (recoveryRate <= 0f)
        {
            return;
        }

        float previousEnergy = currentPrimaryWeaponEnergy;
        currentPrimaryWeaponEnergy = Mathf.Max(0f, currentPrimaryWeaponEnergy - (recoveryRate * deltaTime));
        if (!Mathf.Approximately(previousEnergy, currentPrimaryWeaponEnergy))
        {
            OnPrimaryWeaponEnergyChanged();
        }
    }

    protected virtual void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null)
            {
                abilities[i].Die();
            }
        }

        deathEffects?.PlayDeathEffects(lastDamageDirection);

        Destroy(gameObject);
    }

    private Vector3 ResolveDamageDirection(Vector3 hitPoint)
    {
        if (hitPoint == Vector3.zero)
        {
            return Vector3.zero;
        }

        Vector3 damageDirection = transform.position - hitPoint;
        return damageDirection.sqrMagnitude > 0.0001f ? damageDirection.normalized : Vector3.zero;
    }

    protected virtual void OnHealthChanged()
    {
    }

    protected virtual void OnShieldChanged()
    {
    }

    protected virtual void OnPrimaryWeaponEnergyChanged()
    {
    }

    protected virtual float GetExternalRotationMultiplier()
    {
        return 1f;
    }

    protected virtual float GetExternalThrustMultiplier()
    {
        return 1f;
    }
}
