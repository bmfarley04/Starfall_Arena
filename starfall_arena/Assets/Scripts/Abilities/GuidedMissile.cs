using UnityEngine;
using UnityEngine.InputSystem;

public class GuidedMissile : Ability
{
    [System.Serializable]
    public struct MissileVariantConfig
    {
        [Tooltip("Missile prefab for this variant (should include Missile or ProjectileScript).")]
        public GameObject missilePrefab;
        [Tooltip("Damage multiplier relative to player's base projectile damage.")]
        public float damageMultiplier;
        [Tooltip("Speed multiplier relative to player's base projectile speed.")]
        public float speedMultiplier;
        [Tooltip("Impact force multiplier relative to player's base projectile impact.")]
        public float impactMultiplier;
        [Tooltip("Recoil multiplier relative to player's base projectile recoil.")]
        public float recoilMultiplier;
        [Tooltip("Optional lifetime override for this variant (seconds). <= 0 uses base projectile lifetime.")]
        public float lifetimeOverride;
        [Tooltip("Missile scale multiplier for this variant.")]
        public float sizeMultiplier;
    }

    [System.Serializable]
    public struct GuidedMissileConfig
    {
        [Header("Spawn")]
        [Tooltip("Fallback spawn transform. If null, uses first turret, then ship transform.")]
        public Transform spawnPoint;
        [Tooltip("Offset along spawn forward/up direction.")]
        public float spawnOffset;
        [Tooltip("Use ship velocity as inherited missile velocity.")]
        public bool inheritShipVelocity;

        [Header("Targeting")]
        [Tooltip("Optional explicit target transform. Used first when assigned.")]
        public Transform specifiedTarget;
        [Tooltip("If true and explicit target is missing, find nearest target by enemy tag.")]
        public bool acquireNearestEnemyByTag;
        [Tooltip("Max search radius for nearest target (0 = unlimited).")]
        public float targetSearchRadius;

        [Header("Variant Selection")]
        [Tooltip("If true, fire empowered variant by default.")]
        public bool startEmpowered;

        [Header("Sound Effects")]
        public SoundEffect fireSound;

        [Header("Regular Missile")]
        public MissileVariantConfig regular;

        [Header("Empowered Missile")]
        public MissileVariantConfig empowered;
    }

    [Header("Guided Missile")]
    public GuidedMissileConfig guidedMissile;

    private bool _isEmpowered;
    private float _lastMissileFireTime = -999f;

    protected override void Awake()
    {
        base.Awake();
        _isEmpowered = guidedMissile.startEmpowered;
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        FireMissile();
    }

    public void SetEmpowered(bool empowered)
    {
        _isEmpowered = empowered;
    }

    public bool IsEmpowered()
    {
        return _isEmpowered;
    }

    public override bool IsAbilityActive()
    {
        return false;
    }

    private void FireMissile()
    {
        MissileVariantConfig variant = _isEmpowered ? guidedMissile.empowered : guidedMissile.regular;

        if (variant.missilePrefab == null)
        {
            Debug.LogWarning("GuidedMissile: Missile prefab is not assigned.");
            return;
        }

        Transform firingTransform = ResolveSpawnTransform();
        Vector3 spawnPosition = firingTransform.position + (firingTransform.up * guidedMissile.spawnOffset);
        Vector3 fireDirection = firingTransform.up;

        GameObject missileObject = Instantiate(variant.missilePrefab, spawnPosition, firingTransform.rotation);

        float baseDamage = player.projectileWeapon.damage;
        float baseSpeed = player.projectileWeapon.speed;
        float baseImpact = player.projectileWeapon.impactForce;
        float baseRecoil = player.projectileWeapon.recoilForce;
        float lifetime = variant.lifetimeOverride > 0f ? variant.lifetimeOverride : player.projectileWeapon.lifetime;

        float damage = baseDamage * Mathf.Max(0f, variant.damageMultiplier);
        float speed = baseSpeed * Mathf.Max(0f, variant.speedMultiplier);
        float impact = baseImpact * Mathf.Max(0f, variant.impactMultiplier);
        float recoil = baseRecoil * Mathf.Max(0f, variant.recoilMultiplier);
        float scaleMultiplier = Mathf.Max(0.01f, variant.sizeMultiplier);
        missileObject.transform.localScale *= scaleMultiplier;

        Vector2 inheritedVelocity = Vector2.zero;
        if (guidedMissile.inheritShipVelocity && player != null && player.TryGetComponent<Rigidbody2D>(out var rb))
        {
            inheritedVelocity = rb.linearVelocity;
        }

        Transform target = ResolveTarget();

        if (missileObject.TryGetComponent<Missile>(out var missile))
        {
            missile.targetTag = player.enemyTag;
            missile.Initialize(fireDirection, inheritedVelocity, speed, damage, lifetime, impact, player);
            missile.SetTarget(target);
        }
        else if (missileObject.TryGetComponent<ProjectileScript>(out var projectile))
        {
            projectile.targetTag = player.enemyTag;
            projectile.Initialize(fireDirection, inheritedVelocity, speed, damage, lifetime, impact, player);
        }
        else
        {
            Debug.LogWarning("GuidedMissile: Missile prefab does not contain Missile or ProjectileScript.");
            Destroy(missileObject);
            return;
        }

        player.shotsFired++;
        player.ApplyRecoil(recoil);

        if (guidedMissile.fireSound != null)
        {
            guidedMissile.fireSound.Play(player.GetAvailableAudioSource());
        }

        _lastMissileFireTime = Time.time;
    }

    private Transform ResolveSpawnTransform()
    {
        if (guidedMissile.spawnPoint != null)
        {
            return guidedMissile.spawnPoint;
        }

        if (player.turrets != null && player.turrets.Length > 0 && player.turrets[0] != null)
        {
            return player.turrets[0];
        }

        return transform;
    }

    private Transform ResolveTarget()
    {
        if (guidedMissile.specifiedTarget != null)
        {
            return guidedMissile.specifiedTarget;
        }

        if (!guidedMissile.acquireNearestEnemyByTag)
        {
            return null;
        }

        GameObject[] candidates = GameObject.FindGameObjectsWithTag(player.enemyTag);
        Transform bestTarget = null;
        float bestDistanceSqr = float.MaxValue;
        float maxDistanceSqr = guidedMissile.targetSearchRadius > 0f
            ? guidedMissile.targetSearchRadius * guidedMissile.targetSearchRadius
            : float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject go = candidates[i];
            if (go == null || !go.activeInHierarchy || go == gameObject)
            {
                continue;
            }

            float distSqr = (go.transform.position - transform.position).sqrMagnitude;
            if (distSqr > maxDistanceSqr || distSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distSqr;
            bestTarget = go.transform;
        }

        return bestTarget;
    }

    public override float GetHUDFillRatio()
    {
        if (stats.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastMissileFireTime;
        if (elapsed >= stats.cooldown) return 0f;
        return 1f - (elapsed / stats.cooldown);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastMissileFireTime + stats.cooldown;
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return false;
        }

        if (isLocked || isDisabledByOtherAbility)
        {
            return false;
        }

        if (Time.time < _lastMissileFireTime + stats.cooldown)
        {
            return false;
        }

        UseAbility(value);
        return true;
    }
}
