using UnityEngine;

[RequireComponent(typeof(ShipFlight3D))]
public abstract class Entity3D : MonoBehaviour
{
    [Header("3D Combat")]
    [SerializeField] protected float maxHealth = 100f;

    [Header("3D Systems")]
    [SerializeField] protected ShipFlight3D shipFlight;
    [SerializeField] protected ShipVisualTilt3D shipVisualTilt;
    [SerializeField] protected ShipThrusterVfx3D shipThrusterVfx;
    [SerializeField] protected ShipSpeedFx3D shipSpeedFx;
    [SerializeField] protected ProjectileWeapon3D primaryWeapon;

    protected float currentHealth;

    public ShipFlight3D Flight => shipFlight;
    public ShipVisualTilt3D VisualTilt => shipVisualTilt;
    public ShipThrusterVfx3D ThrusterVfx => shipThrusterVfx;
    public ShipSpeedFx3D SpeedFx => shipSpeedFx;
    public ProjectileWeapon3D PrimaryWeapon => primaryWeapon;
    public float CurrentHealth => currentHealth;

    protected virtual void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        shipVisualTilt ??= GetComponent<ShipVisualTilt3D>();
        shipThrusterVfx ??= GetComponent<ShipThrusterVfx3D>();
        shipSpeedFx ??= GetComponent<ShipSpeedFx3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Entity3D attacker = null)
    {
        if (damage <= 0f || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public virtual void TakeDirectDamage(float damage, Vector3 hitPoint, Entity3D attacker = null)
    {
        TakeDamage(damage, hitPoint, attacker);
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
