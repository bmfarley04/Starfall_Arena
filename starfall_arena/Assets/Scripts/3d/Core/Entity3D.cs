using UnityEngine;

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

    [Header("Abilities")]
    [SerializeField] protected Ability3D[] abilities = new Ability3D[4];

    [Header("3D Systems")]
    [SerializeField] protected ShipFlight3D shipFlight;
    [SerializeField] protected ShipVisualTilt3D shipVisualTilt;
    [SerializeField] protected ShipThrusterVfx3D shipThrusterVfx;
    [SerializeField] protected ShipSpeedFx3D shipSpeedFx;
    [SerializeField] protected ProjectileWeapon3D primaryWeapon;

    protected float currentHealth;
    protected float currentShield;

    public ShipFlight3D Flight => shipFlight;
    public ShipVisualTilt3D VisualTilt => shipVisualTilt;
    public ShipThrusterVfx3D ThrusterVfx => shipThrusterVfx;
    public ShipSpeedFx3D SpeedFx => shipSpeedFx;
    public ProjectileWeapon3D PrimaryWeapon => primaryWeapon;
    public Ability3D[] Abilities => abilities;
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float ImpulseRecoilPitchSensitivity => impulseRecoilPitchSensitivity;

    public Ability3D GetAbility(int index)
    {
        if (index < 0 || index >= abilities.Length) return null;
        return abilities[index];
    }

    public float GetCombinedRotationMultiplier()
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

    public float GetCombinedThrustMultiplier()
    {
        float multiplier = 1f;
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
        shieldController ??= GetComponentInChildren<ShieldController>(true);
        currentHealth = maxHealth;
        currentShield = maxShield;
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Entity3D attacker = null)
    {
        Debug.Log("take damage called on " + gameObject.name + " with damage: " + damage);
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

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
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        for (int i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] != null)
            {
                abilities[i].Die();
            }
        }
        Destroy(gameObject);
    }

    protected virtual void OnHealthChanged()
    {
    }

    protected virtual void OnShieldChanged()
    {
    }
}
