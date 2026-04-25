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

        [Header("Empower Source")]
        [Tooltip("Reference to this ship's Empower ability. If null, auto-finds on this GameObject.")]
        public Empower empowerAbility;
        [Tooltip("If true, this ability always uses empowered variant (debug/testing).")]
        public bool forceEmpowered;

        [Header("Sound Effects")]
        public SoundEffect fireSound;

        [Header("Regular Missile")]
        public MissileVariantConfig regular;

        [Header("Empowered Missile")]
        public MissileVariantConfig empowered;
    }

    [Header("Guided Missile")]
    public GuidedMissileConfig guidedMissile;

    private float _lastMissileFireTime = -999f;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        if (guidedMissile.empowerAbility == null)
        {
            guidedMissile.empowerAbility = GetComponent<Empower>();
        }
        _netMovement = GetComponent<NetMovement>();
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        FireMissile();
    }

    public override bool IsAbilityActive()
    {
        return false;
    }

    private void FireMissile()
    {
        bool empowered = IsEmpoweredActive();
        MissileVariantConfig variant = empowered ? guidedMissile.empowered : guidedMissile.regular;

        if (variant.missilePrefab == null)
        {
            Debug.LogWarning("GuidedMissile: Missile prefab is not assigned.");
            return;
        }

        Transform firingTransform = ResolveSpawnTransform();
        Vector3 spawnPosition = firingTransform.position + (firingTransform.up * guidedMissile.spawnOffset);
        Vector3 fireDirection = firingTransform.up;

        float baseDamage = player.projectileWeapon.damage;
        float baseSpeed = player.projectileWeapon.speed;
        float baseImpact = player.projectileWeapon.impactForce;
        float baseRecoil = player.projectileWeapon.recoilForce;
        float lifetime = variant.lifetimeOverride > 0f ? variant.lifetimeOverride : player.projectileWeapon.lifetime;

        Vector2 inheritedVelocity = Vector2.zero;
        if (guidedMissile.inheritShipVelocity && player != null && player.TryGetComponent<Rigidbody2D>(out var rb))
        {
            inheritedVelocity = rb.linearVelocity;
        }

        NetGuidedMissileState state = new NetGuidedMissileState
        {
            SpawnPosition = spawnPosition,
            Direction = ((Vector2)fireDirection).normalized,
            InheritedVelocity = inheritedVelocity,
            Speed = baseSpeed * Mathf.Max(0f, variant.speedMultiplier),
            Damage = baseDamage * Mathf.Max(0f, variant.damageMultiplier),
            Lifetime = lifetime,
            ImpactForce = baseImpact * Mathf.Max(0f, variant.impactMultiplier),
            RecoilForce = baseRecoil * Mathf.Max(0f, variant.recoilMultiplier),
            SizeMultiplier = Mathf.Max(0.01f, variant.sizeMultiplier),
            Empowered = empowered,
            ApplyRecoil = true,
        };

        _lastMissileFireTime = Time.time;

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;

        if (useNetworkPath)
        {
            // Owner predicts the missile locally for responsiveness; server runs
            // the authoritative spawn and broadcasts to remote clients.
            if (!_netMovement.IsServer)
            {
                ApplyNetworkGuidedMissile(state, authoritative: false);
            }
            _netMovement.RequestGuidedMissile(state);
            return;
        }

        ApplyNetworkGuidedMissile(state, authoritative: true);
    }

    public void ApplyNetworkGuidedMissile(NetGuidedMissileState state, bool authoritative)
    {
        MissileVariantConfig variant = state.Empowered ? guidedMissile.empowered : guidedMissile.regular;
        if (variant.missilePrefab == null)
        {
            return;
        }

        Quaternion spawnRotation = state.Direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(Vector3.forward, state.Direction)
            : transform.rotation;

        GameObject missileObject = Instantiate(variant.missilePrefab, state.SpawnPosition, spawnRotation);
        missileObject.transform.localScale *= Mathf.Max(0.01f, state.SizeMultiplier);

        bool cosmeticOnly = NetTickUtil.IsActive && !authoritative;
        int attackId = authoritative ? player.BeginTrackedAttack() : Player.InvalidAttackId;
        Transform target = ResolveTarget();

        if (missileObject.TryGetComponent<Missile>(out var missile))
        {
            missile.targetTag = player.enemyTag;
            missile.SetCosmeticOnly(cosmeticOnly);
            if (NetTickUtil.IsActive && authoritative && _netMovement != null)
            {
                missile.SetNetworkAuthority(_netMovement, state.Tick);
            }
            missile.Initialize(state.Direction, state.InheritedVelocity, state.Speed, state.Damage, state.Lifetime, state.ImpactForce, player, attackId);
            missile.SetTarget(target);
        }
        else if (missileObject.TryGetComponent<ProjectileScript>(out var projectile))
        {
            projectile.targetTag = player.enemyTag;
            projectile.SetCosmeticOnly(cosmeticOnly);
            if (NetTickUtil.IsActive && authoritative && _netMovement != null)
            {
                projectile.SetNetworkAuthority(_netMovement, state.Tick);
            }
            projectile.Initialize(state.Direction, state.InheritedVelocity, state.Speed, state.Damage, state.Lifetime, state.ImpactForce, player, attackId);
        }
        else
        {
            Debug.LogWarning("GuidedMissile: Missile prefab does not contain Missile or ProjectileScript.");
            Destroy(missileObject);
            return;
        }

        // Recoil applies on local sim, server (authoritative), and the owner's predicted copy.
        // Skip on remote, non-owner cosmetic copies so we don't shove the other ship's visual rb.
        bool isRemoteCosmetic = NetTickUtil.IsActive && !authoritative && _netMovement != null && !_netMovement.IsOwner;
        if (state.ApplyRecoil && !isRemoteCosmetic)
        {
            player.ApplyRecoil(state.RecoilForce);
        }

        if (guidedMissile.fireSound != null)
        {
            guidedMissile.fireSound.Play(player.GetAvailableAudioSource());
        }
    }

    private bool IsEmpoweredActive()
    {
        if (guidedMissile.forceEmpowered)
        {
            return true;
        }

        return guidedMissile.empowerAbility != null && guidedMissile.empowerAbility.IsEmpoweredActive;
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
