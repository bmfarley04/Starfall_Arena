using Unity.Netcode;
using UnityEngine;
using System;

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

    [Header("Upright Recovery")]
    [Tooltip("If enabled, the entity gently rolls back upright after recent combat and manual rotation have settled.")]
    [SerializeField] private bool uprightRecoveryEnabled = true;
    [Tooltip("Seconds after taking damage or firing before upright recovery can begin.")]
    [SerializeField] private float uprightRecoveryDelay = 2.5f;
    [Tooltip("Maximum roll correction speed once upright recovery is active.")]
    [SerializeField] private float uprightRecoveryDegreesPerSecond = 90f;
    [Tooltip("Look/turn input below this value is treated as idle for upright recovery.")]
    [SerializeField] private float uprightRecoveryInputDeadZone = 0.05f;

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
    protected NetCombat3D netCombat3D;
    protected NetEnemyCombat3D netEnemyCombat3D;

    private bool _isDead;
    private float _uprightRecoverySuppressedUntil;

    public event Action<Entity3D> Died;

    public ShipFlight3D Flight => shipFlight;
    public ShipVisualTilt3D VisualTilt => shipVisualTilt;
    public ShipThrusterVfx3D ThrusterVfx => shipThrusterVfx;
    public ShipSpeedFx3D SpeedFx => shipSpeedFx;
    public Weapon3D[] Weapons => weapons;
    public ProjectileWeapon3D PrimaryWeapon => GetWeapon(0) as ProjectileWeapon3D;
    public Weapon3D SelectedWeapon => GetWeapon(selectedWeaponIndex);
    public int SelectedWeaponIndex => selectedWeaponIndex;
    public Ability3D[] Abilities => abilities;
    public float MaxHealth => maxHealth;
    public float MaxShield => maxShield;
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float ImpulseRecoilPitchSensitivity => impulseRecoilPitchSensitivity;
    public float CurrentSlowMultiplier => GetSlowMultiplier();
    public bool IsSlowed => Time.time < slowEndTime && currentSlowMultiplier < 1f;
    public bool UprightRecoveryEnabled => uprightRecoveryEnabled;
    public float UprightRecoveryInputDeadZone => uprightRecoveryInputDeadZone;

    public void OverrideMaxHealthAndShield(float newMaxHealth, float newMaxShield, bool refillCurrentValues)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        maxShield = Mathf.Max(0f, newMaxShield);

        if (refillCurrentValues)
        {
            currentHealth = maxHealth;
            currentShield = maxShield;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            currentShield = Mathf.Clamp(currentShield, 0f, maxShield);
        }

        OnHealthChanged();
        OnShieldChanged();
    }

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

        if (selectedWeaponIndex == index)
        {
            return true;
        }

        selectedWeaponIndex = index;
        OnSelectedWeaponChanged();
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
        netCombat3D ??= GetComponent<NetCombat3D>();
        netEnemyCombat3D ??= GetComponent<NetEnemyCombat3D>();
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
        RecordCombatActivity();
    }

    protected virtual void OnValidate()
    {
        uprightRecoveryDelay = Mathf.Max(0f, uprightRecoveryDelay);
        uprightRecoveryDegreesPerSecond = Mathf.Max(0f, uprightRecoveryDegreesPerSecond);
        uprightRecoveryInputDeadZone = Mathf.Max(0f, uprightRecoveryInputDeadZone);
    }

    public virtual void TakeDamage(float damage, Vector3 hitPoint, Entity3D attacker = null, DamageSource3D source = DamageSource3D.Projectile, int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        if (damage <= 0f || currentHealth <= 0f || _isDead)
        {
            return;
        }

        if (NetTickUtil.IsActive && netCombat3D != null && !netCombat3D.IsServer)
        {
            return;
        }

        float previousShield = currentShield;
        float previousHealth = currentHealth;

        RecordCombatActivity();
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
                RecordDamageStats(attacker, previousHealth, previousShield, source, accuracyAttackId);
                BroadcastNetworkCombatState(hitPoint, source, previousShield);
                return;
            }
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged();
        RecordDamageStats(attacker, previousHealth, previousShield, source, accuracyAttackId);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        BroadcastNetworkCombatState(hitPoint, source, previousShield);
    }

    public virtual void TakeDirectDamage(float damage, Vector3 hitPoint, Entity3D attacker = null, int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        if (damage <= 0f || currentHealth <= 0f || _isDead)
        {
            return;
        }

        if (NetTickUtil.IsActive && netCombat3D != null && !netCombat3D.IsServer)
        {
            return;
        }

        float previousShield = currentShield;
        float previousHealth = currentHealth;

        RecordCombatActivity();
        lastDamageDirection = ResolveDamageDirection(hitPoint);
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged();
        RecordDamageStats(attacker, previousHealth, previousShield, DamageSource3D.Direct, accuracyAttackId);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        BroadcastNetworkCombatState(hitPoint, DamageSource3D.Direct, previousShield);
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

    public void RecordCombatActivity(float extraDelay = 0f)
    {
        float recoveryDelay = Mathf.Max(0f, uprightRecoveryDelay + extraDelay);
        _uprightRecoverySuppressedUntil = Mathf.Max(_uprightRecoverySuppressedUntil, Time.time + recoveryDelay);
    }

    public bool ShouldApplyUprightRecovery(bool hasRotationIntent)
    {
        if (!uprightRecoveryEnabled || _isDead || hasRotationIntent || Time.time < _uprightRecoverySuppressedUntil)
        {
            return false;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon3D weapon = weapons[i];
            if (weapon != null && (weapon.IsFireHeld || weapon.IsReticleSpinActive()))
            {
                return false;
            }
        }

        return true;
    }

    public Quaternion ApplyUprightRecovery(Quaternion currentRotation, float deltaTime, bool hasRotationIntent)
    {
        if (deltaTime <= 0f || !ShouldApplyUprightRecovery(hasRotationIntent))
        {
            return currentRotation;
        }

        Quaternion targetRotation = ResolveUprightRotation(currentRotation);
        float maxDegreesDelta = Mathf.Max(0f, uprightRecoveryDegreesPerSecond) * deltaTime;
        return Quaternion.RotateTowards(currentRotation, targetRotation, maxDegreesDelta);
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
        Died?.Invoke(this);

        if (NetTickUtil.IsActive && TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
        {
            netCombat3D?.BroadcastDeath(transform.position, transform.rotation, lastDamageDirection);
            netEnemyCombat3D?.BroadcastDeath(transform.position, transform.rotation, lastDamageDirection);
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Despawn(true);
            }
            return;
        }

        Destroy(gameObject);
    }

    public void ApplyNetworkCombatState(NetCombatState3D state)
    {
        float previousHealth = currentHealth;
        float previousShield = currentShield;
        currentHealth = Mathf.Clamp(state.Health, 0f, maxHealth);
        currentShield = Mathf.Clamp(state.Shield, 0f, maxShield);

        if (!Mathf.Approximately(previousHealth, currentHealth))
        {
            OnHealthChanged();
        }

        if (!Mathf.Approximately(previousShield, currentShield))
        {
            OnShieldChanged();
        }

        if (state.ShieldBreak)
        {
            shieldController?.BreakShield();
        }
        else if (state.ShieldHit)
        {
            shieldController?.OnHit(state.HitPoint);
        }

        if (state.SlowRemainingTime > 0f)
        {
            ApplySlow(state.SlowMultiplier, state.SlowRemainingTime);
        }

        OnNetworkDamageFeedback(previousHealth, previousShield, state);
    }

    public void PlayNetworkDeath(Vector3 position, Quaternion rotation, Vector3 damageDirection)
    {
        transform.SetPositionAndRotation(position, rotation);
        deathEffects?.PlayDeathEffects(damageDirection);
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

    private static Quaternion ResolveUprightRotation(Quaternion currentRotation)
    {
        Vector3 forward = currentRotation * Vector3.forward;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return currentRotation;
        }

        forward.Normalize();
        Vector3 upReference = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (upReference.sqrMagnitude <= 0.0001f)
        {
            Vector3 right = Vector3.ProjectOnPlane(currentRotation * Vector3.right, Vector3.up);
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }

            upReference = Vector3.Cross(forward, right.normalized);
        }

        return Quaternion.LookRotation(forward, upReference.normalized);
    }

    private void RecordDamageStats(Entity3D attacker, float previousHealth, float previousShield, DamageSource3D source, int accuracyAttackId)
    {
        float appliedDamage = Mathf.Max(0f, (previousHealth + previousShield) - (currentHealth + currentShield));
        if (appliedDamage <= 0f)
        {
            return;
        }

        PlayerCombatStats3D targetStats = GetComponent<PlayerCombatStats3D>();
        targetStats?.RecordDamageTaken(appliedDamage);

        PlayerCombatStats3D attackerStats = attacker != null ? attacker.GetComponent<PlayerCombatStats3D>() : null;
        if (attackerStats == null || ReferenceEquals(attackerStats, targetStats))
        {
            return;
        }

        attackerStats.RecordDamageDealt(appliedDamage);
        if (accuracyAttackId != PlayerCombatStats3D.InvalidAttackId)
        {
            attackerStats.RegisterAttackHit(accuracyAttackId);
        }
        else if (source == DamageSource3D.Projectile || source == DamageSource3D.Direct)
        {
            attackerStats.RecordShotHit();
        }
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

    protected virtual void OnSelectedWeaponChanged()
    {
    }

    protected virtual void OnNetworkDamageFeedback(float previousHealth, float previousShield, NetCombatState3D state)
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

    private void BroadcastNetworkCombatState(Vector3 hitPoint, DamageSource3D source, float previousShield)
    {
        if (!NetTickUtil.IsActive)
        {
            return;
        }

        bool isSlowed = IsSlowed;
        NetCombatState3D state = new NetCombatState3D
        {
            Health = currentHealth,
            Shield = currentShield,
            HitPoint = hitPoint,
            DamageSource = (int)source,
            ShieldHit = previousShield > currentShield && currentShield > 0f,
            ShieldBreak = previousShield > 0f && currentShield <= 0f,
            SlowMultiplier = isSlowed ? GetSlowMultiplier() : 1f,
            SlowRemainingTime = isSlowed ? Mathf.Max(0f, slowEndTime - Time.time) : 0f
        };

        if (netCombat3D != null && netCombat3D.IsServer)
        {
            netCombat3D.BroadcastCombatState(state);
        }

        if (netEnemyCombat3D != null && netEnemyCombat3D.IsServer)
        {
            netEnemyCombat3D.BroadcastCombatState(state);
        }
    }
}
