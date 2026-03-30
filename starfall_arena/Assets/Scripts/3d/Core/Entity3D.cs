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
    [Header("3D Combat")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float maxShield = 50f;
    [SerializeField] protected ShieldController shieldController;

    [Header("3D Visual Feedback")]
    [Tooltip("Multiplier for how much recoil/impulse affects visual pitch independent of thrust pitch.")]
    [SerializeField] protected float impulseRecoilPitchSensitivity = 1f;

    [Header("Weapons")]
    [SerializeField] protected Weapon3D[] weapons = new Weapon3D[3];
    [SerializeField] protected int selectedWeaponIndex;

    [Header("Abilities")]
    [SerializeField] protected Ability3D[] abilities = new Ability3D[2];

    [Header("3D Systems")]
    [SerializeField] protected ShipFlight3D shipFlight;
    [SerializeField] protected ShipVisualTilt3D shipVisualTilt;
    [SerializeField] protected ShipThrusterVfx3D shipThrusterVfx;
    [SerializeField] protected ShipSpeedFx3D shipSpeedFx;
    [SerializeField] protected DeathEffects3D deathEffects;

    protected float currentHealth;
    protected float currentShield;
    protected Vector3 lastDamageDirection;
    protected float currentSlowMultiplier = 1f;
    protected float slowEndTime;

    private bool _isDead;

    public ShipFlight3D Flight => shipFlight;
    public ShipVisualTilt3D VisualTilt => shipVisualTilt;
    public ShipThrusterVfx3D ThrusterVfx => shipThrusterVfx;
    public ShipSpeedFx3D SpeedFx => shipSpeedFx;
    public Weapon3D[] Weapons => weapons;
    public ProjectileWeapon3D PrimaryWeapon => GetWeapon(0) as ProjectileWeapon3D;
    public Weapon3D SelectedWeapon => GetWeapon(selectedWeaponIndex);
    public int SelectedWeaponIndex => selectedWeaponIndex;
    public Ability3D[] Abilities => abilities;
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float ImpulseRecoilPitchSensitivity => impulseRecoilPitchSensitivity;
    public float CurrentSlowMultiplier => GetSlowMultiplier();
    public bool IsSlowed => Time.time < slowEndTime && currentSlowMultiplier < 1f;

    public Ability3D GetAbility(int index)
    {
        if (index < 0 || index >= abilities.Length)
        {
            return null;
        }

        return abilities[index];
    }

    public Weapon3D GetWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length)
        {
            return null;
        }

        return weapons[index];
    }

    public bool SelectWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length || weapons[index] == null)
        {
            return false;
        }

        selectedWeaponIndex = index;
        return true;
    }

    public float GetCombinedRotationMultiplier()
    {
        return GetBaseRotationMultiplier() * GetAbilityRotationMultiplier() * GetWeaponRotationMultiplier();
    }

    public float GetBaseRotationMultiplier()
    {
        return GetFlatBaseRotationMultiplier();
    }

    public float GetAbilityRotationMultiplier()
    {
        float multiplier = 1f;
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

    public float GetWeaponRotationMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon3D weapon = weapons[i];
            if (weapon == null)
            {
                continue;
            }

            multiplier *= weapon.GetRotationMultiplier();
        }

        return multiplier;
    }

    public float GetCombinedThrustMultiplier()
    {
        float multiplier = GetExternalThrustMultiplier() * GetWeaponThrustMultiplier();
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

    public float GetWeaponThrustMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon3D weapon = weapons[i];
            if (weapon == null)
            {
                continue;
            }

            multiplier *= weapon.GetThrustMultiplier();
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
        deathEffects ??= GetComponent<DeathEffects3D>();
        shieldController ??= GetComponentInChildren<ShieldController>(true);
        CacheCombatSlotsIfNeeded();
        selectedWeaponIndex = Mathf.Clamp(selectedWeaponIndex, 0, Mathf.Max(0, weapons.Length - 1));
        if (weapons.Length > 0 && weapons[selectedWeaponIndex] == null)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                {
                    selectedWeaponIndex = i;
                    break;
                }
            }
        }

        currentHealth = maxHealth;
        currentShield = maxShield;
        lastDamageDirection = Vector3.zero;
        currentSlowMultiplier = 1f;
        slowEndTime = 0f;
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

    protected virtual void Die()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].Die();
            }
        }

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

    private void CacheCombatSlotsIfNeeded()
    {
        if (weapons.Length == 0)
        {
            weapons = new Weapon3D[3];
        }

        if (abilities.Length == 0)
        {
            abilities = new Ability3D[2];
        }

        if (NeedsWeaponDiscovery())
        {
            Weapon3D[] discoveredWeapons = GetComponents<Weapon3D>();
            for (int i = 0; i < weapons.Length && i < discoveredWeapons.Length; i++)
            {
                weapons[i] = discoveredWeapons[i];
            }
        }

        if (NeedsAbilityDiscovery())
        {
            Ability3D[] discoveredAbilities = GetComponents<Ability3D>();
            for (int i = 0; i < abilities.Length && i < discoveredAbilities.Length; i++)
            {
                abilities[i] = discoveredAbilities[i];
            }
        }
    }

    private bool NeedsWeaponDiscovery()
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private bool NeedsAbilityDiscovery()
    {
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    protected virtual void OnHealthChanged()
    {
    }

    protected virtual void OnShieldChanged()
    {
    }

    protected virtual float GetFlatBaseRotationMultiplier()
    {
        return 1f;
    }

    protected virtual float GetExternalThrustMultiplier()
    {
        return 1f;
    }
}
