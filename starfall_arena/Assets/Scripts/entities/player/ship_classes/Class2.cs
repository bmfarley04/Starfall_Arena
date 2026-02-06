using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== ABILITY CONFIGURATION STRUCTS =====

[System.Serializable]
public struct EmpoweredShotAbilityConfig
{
    [Header("Cooldown")]
    [Tooltip("Cooldown between empowered shots (seconds)")]
    public float cooldown;

    [Header("Projectile")]
    [Tooltip("Empowered projectile prefab (different visual from normal shots)")]
    public GameObject projectilePrefab;
    [Tooltip("Damage multiplier compared to normal shot")]
    public float damageMultiplier;
    [Tooltip("Speed multiplier compared to normal shot")]
    public float speedMultiplier;
    [Tooltip("Impact force multiplier compared to normal shot")]
    public float impactMultiplier;
    [Tooltip("Recoil multiplier compared to normal shot")]
    public float recoilMultiplier;
    [Tooltip("Projectile lifetime (seconds)")]
    public float lifetime;

    [Header("Slow Effect")]
    [Tooltip("Slow multiplier applied to target (0.5 = 50% speed)")]
    [Range(0f, 1f)]
    public float slowMultiplier;
    [Tooltip("Duration of slow effect (seconds)")]
    public float slowDuration;

    [Header("Sound Effects")]
    [Tooltip("Sound played when firing empowered shot")]
    public SoundEffect fireSound;
}

[System.Serializable]
public struct ShieldAbilityConfig
{
    [Header("Timing")]
    [Tooltip("Cooldown between uses (seconds)")]
    public float cooldown;
    [Tooltip("Shield active duration (seconds)")]
    public float activeDuration;

    [Header("Shield")]
    [Tooltip("ReflectShield component (drag from Hierarchy)")]
    public ReflectShield shield;
    [Tooltip("Color of the shield when active")]
    public Color shieldColor;

    [Header("Sound Effects")]
    [Tooltip("Shield duration sound (loops while active)")]
    public SoundEffect shieldLoopSound;
    [Tooltip("Sound when projectile hits the shield")]
    public SoundEffect shieldHitSound;
}

[System.Serializable]
public struct Class2AbilitiesConfig
{
    [Header("Ability 3 - Empowered Shot")]
    public EmpoweredShotAbilityConfig empoweredShot;

    [Header("Ability 4 - Shield")]
    public ShieldAbilityConfig shield;
}

public class Class2 : Player
{
    // ===== ABILITIES =====
    [Header("Abilities")]
    public Class2AbilitiesConfig abilities;

    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    [SerializeField] private float _fireCooldown = 0.5f;

    [Header("Convergence Settings")]
    [Tooltip("Distance ahead of ship where all projectiles converge (all shots meet at this point)")]
    [SerializeField] private float convergenceDistance = 20f;

    // ===== PRIVATE STATE =====
    private float _lastEmpoweredShotTime = -999f;
    private float _lastShieldTime = -999f;
    private Coroutine _shieldCoroutine;
    private AudioSource _shieldSource;

    protected override void Awake()
    {
        base.Awake();
        // Sync Inspector value to parent's protected field
        fireCooldown = _fireCooldown;

        _shieldSource = gameObject.AddComponent<AudioSource>();
        _shieldSource.playOnAwake = false;
        _shieldSource.loop = true;
        _shieldSource.spatialBlend = 1f;
        _shieldSource.rolloffMode = AudioRolloffMode.Linear;
        _shieldSource.minDistance = 10f;
        _shieldSource.maxDistance = 50f;
        _shieldSource.dopplerLevel = 0f;
    }

    protected override void Update()
    {
        base.Update();
    }

    protected override void TryFireProjectile()
    {
        if (projectileWeapon.prefab == null)
            return;

        if (Time.time < _lastFireTime + fireCooldown)
            return;

        // Calculate convergence point ahead of ship
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;

        for (int i = 0; i < turrets.Length; i++)
        {
            Transform turret = turrets[i];

            // Calculate direction from turret to convergence point
            Vector3 directionToConvergence = (convergencePoint - turret.position).normalized;

            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    projectileWeapon.speed,
                    projectileWeapon.damage,
                    projectileWeapon.lifetime,
                    projectileWeapon.impactForce,
                    this
                );
            }
        }

        ApplyRecoil(projectileWeapon.recoilForce);

        if (projectileFireSound != null)
        {
            projectileFireSound.Play(GetAvailableAudioSource());
        }

        _lastFireTime = Time.time;
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnAbility3()
    {
        if (Time.time < _lastEmpoweredShotTime + abilities.empoweredShot.cooldown)
        {
            Debug.Log($"Empowered Shot on cooldown: {(_lastEmpoweredShotTime + abilities.empoweredShot.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (abilities.empoweredShot.projectilePrefab == null)
        {
            Debug.LogWarning("Empowered Shot projectile prefab not assigned!");
            return;
        }

        FireEmpoweredShot();
        _lastEmpoweredShotTime = Time.time;
    }

    // ===== EMPOWERED SHOT ABILITY =====
    private void FireEmpoweredShot()
    {
        // Calculate base stats from primary weapon
        float damage = projectileWeapon.damage * abilities.empoweredShot.damageMultiplier;
        float speed = projectileWeapon.speed * abilities.empoweredShot.speedMultiplier;
        float impactForce = projectileWeapon.impactForce * abilities.empoweredShot.impactMultiplier;
        float recoil = projectileWeapon.recoilForce * abilities.empoweredShot.recoilMultiplier;

        // Calculate convergence point ahead of ship
        Vector3 convergencePoint = transform.position + transform.up * convergenceDistance;

        for (int i = 0; i < turrets.Length; i++)
        {
            Transform turret = turrets[i];

            // Calculate direction from turret to convergence point
            Vector3 directionToConvergence = (convergencePoint - turret.position).normalized;

            GameObject projectile = Instantiate(abilities.empoweredShot.projectilePrefab, turret.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = enemyTag;
                projectileScript.Initialize(
                    directionToConvergence,
                    Vector2.zero,
                    speed,
                    damage,
                    abilities.empoweredShot.lifetime,
                    impactForce,
                    this
                );

                // Enable slow effect
                projectileScript.EnableSlow(abilities.empoweredShot.slowMultiplier, abilities.empoweredShot.slowDuration);
            }
        }

        ApplyRecoil(recoil);

        if (abilities.empoweredShot.fireSound != null)
        {
            abilities.empoweredShot.fireSound.Play(GetAvailableAudioSource());
        }

        Debug.Log($"Empowered Shot fired! Damage: {damage:F1}, Speed: {speed:F1}, Slow: {abilities.empoweredShot.slowMultiplier * 100}% for {abilities.empoweredShot.slowDuration}s");
    }

    // ===== SHIELD ABILITY =====
    void OnAbility4()
    {
        if (Time.time < _lastShieldTime + abilities.shield.cooldown)
        {
            Debug.Log($"Shield on cooldown: {(_lastShieldTime + abilities.shield.cooldown - Time.time):F1}s remaining");
            return;
        }

        if (abilities.shield.shield == null)
        {
            Debug.LogWarning("Shield not assigned!");
            return;
        }

        _lastShieldTime = Time.time;

        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }
        _shieldCoroutine = StartCoroutine(ActivateShield());
    }

    private System.Collections.IEnumerator ActivateShield()
    {
        abilities.shield.shield.Activate(abilities.shield.shieldColor);

        if (abilities.shield.shieldLoopSound != null && _shieldSource != null)
        {
            abilities.shield.shieldLoopSound.Play(_shieldSource);
        }

        yield return new WaitForSeconds(abilities.shield.activeDuration);

        abilities.shield.shield.Deactivate();

        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }
    }

    // ===== DAMAGE OVERRIDE =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        if (abilities.shield.shield != null && abilities.shield.shield.IsActive())
        {
            return;
        }

        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }

        base.Die();
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (abilities.shield.shield != null && abilities.shield.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == thisPlayerTag)
            {
                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                abilities.shield.shield.OnReflectHit(hitPoint);

                if (abilities.shield.shieldHitSound != null)
                {
                    abilities.shield.shieldHitSound.Play(GetAvailableAudioSource());
                }

                Destroy(projectile.gameObject);
            }
        }
    }
}
