using UnityEngine;
using UnityEngine.InputSystem;

public class BatteryRam : Ability
{
    [System.Serializable]
    public struct RamConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between ram deployments when not using charges (seconds)")]
        public float cooldown;
        [Tooltip("Max duration the ram can stay active (0 = unlimited until broken/released)")]
        public float maxDuration;

        [Header("Ram Object")]
        [Tooltip("Prefab to use for the ram (optional). Collider will be added if missing.")]
        public GameObject ramPrefab;
        [Tooltip("Local offset from the ship where the ram sits (relative to ship transform)")]
        public Vector3 localOffset;
        [Tooltip("Fallback collider size when no prefab provided")]
        public Vector2 fallbackColliderSize;
        [Tooltip("Number of hits the ram can absorb from projectiles before breaking")]
        public float ramHealth;

        [Header("Impact Effects")]
        [Tooltip("Duration of the stun applied to enemy ships (seconds)")]
        public float stunDuration;
        [Tooltip("Knockback force applied to struck targets")]
        public float knockbackForce;
        [Tooltip("Recoil force applied back to the owner on impact")]
        public float selfRecoil;

        [Header("Audio")]
        public SoundEffect activateSound;
        public SoundEffect hitSound;
        public SoundEffect breakSound;
    }

    [Header("Ability - Battery Ram")]
    public RamConfig ram;

    private BatteryRamCollider _activeRam;
    private bool _ramActive;
    private float _ramStartTime;
    private float _currentHealth;
    private IChargeProvider _chargeProvider;

    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
        // Start off cooldown at match start
        lastUsedAbility = -ram.cooldown;
        if (ram.ramHealth <= 0f)
        {
            ram.ramHealth = 1f;
        }
        if (ram.fallbackColliderSize == Vector2.zero)
        {
            ram.fallbackColliderSize = new Vector2(1.5f, 0.6f);
        }
    }

    private void Update()
    {
        if (!_ramActive || _activeRam == null) return;

        UpdateRamTransform();

        if (ram.maxDuration > 0f && Time.time >= _ramStartTime + ram.maxDuration)
        {
            BreakRam(false);
        }
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (isLocked || isDisabledByOtherAbility)
        {
            return false;
        }

        if (value.isPressed)
        {
            if (_ramActive)
            {
                return false;
            }

            if (Time.time < lastUsedAbility + ram.cooldown)
            {
                Debug.Log($"❌ BatteryRam on cooldown: {(lastUsedAbility + ram.cooldown - Time.time):F1}s remaining.");
                return false;
            }

            UseAbility(value);
            return true;
        }
        else
        {
            if (_ramActive)
            {
                BreakRam(false);
                return true;
            }
        }

        return false;
    }

    public override void UseAbility(InputValue value)
    {
        ActivateRam();
    }

    public override bool IsAbilityActive()
    {
        return _ramActive;
    }

    public override bool IsResourceBased()
    {
        return false;
    }

    public override float GetHUDFillRatio()
    {
        if (ram.cooldown <= 0f) return 0f;
        float elapsed = Time.time - lastUsedAbility;
        if (elapsed >= ram.cooldown) return 0f;
        return 1f - (elapsed / ram.cooldown);
    }

    public override bool IsOnCooldown()
    {
        if (ram.cooldown <= 0f) return false;
        return Time.time < lastUsedAbility + ram.cooldown;
    }

    public override void Die()
    {
        base.Die();
        BreakRam(true);
    }

    private void ActivateRam()
    {
        _ramActive = true;
        _ramStartTime = Time.time;
        _currentHealth = ram.ramHealth;

        if (_activeRam != null)
        {
            Destroy(_activeRam.gameObject);
            _activeRam = null;
        }

        _activeRam = CreateRamInstance();
        UpdateRamTransform();

        if (ram.activateSound != null)
        {
            ram.activateSound.Play(player.GetAvailableAudioSource());
        }
    }

    private BatteryRamCollider CreateRamInstance()
    {
        GameObject ramObj;
        if (ram.ramPrefab != null)
        {
            ramObj = Instantiate(ram.ramPrefab, transform);
        }
        else
        {
            ramObj = new GameObject("BatteryRam_Instance");
            ramObj.transform.SetParent(transform);
        }

        ramObj.layer = gameObject.layer;

        // Ensure at least one collider exists
        var colliders = ramObj.GetComponentsInChildren<Collider2D>();
        if (colliders == null || colliders.Length == 0)
        {
            var box = ramObj.AddComponent<BoxCollider2D>();
            box.size = ram.fallbackColliderSize;
            colliders = new Collider2D[] { box };
        }

        // Ensure a root BatteryRamCollider exists
        var ramCollider = ramObj.GetComponent<BatteryRamCollider>();
        if (ramCollider == null)
        {
            ramCollider = ramObj.AddComponent<BatteryRamCollider>();
        }
        ramCollider.Initialize(this);
        ramCollider.BindChildColliders();

        return ramCollider;
    }

    private void UpdateRamTransform()
    {
        if (_activeRam == null) return;

        _activeRam.transform.localPosition = ram.localOffset;
        _activeRam.transform.localRotation = Quaternion.identity;
        _activeRam.transform.position = transform.TransformPoint(ram.localOffset);
        _activeRam.transform.rotation = transform.rotation;
    }

    private void BreakRam(bool broken)
    {
        if (!_ramActive) return;

        _ramActive = false;
        _currentHealth = 0f;

        if (_activeRam != null)
        {
            Destroy(_activeRam.gameObject);
            _activeRam = null;
        }

        if (broken)
        {
            lastUsedAbility = Time.time;
            if (ram.breakSound != null)
            {
                ram.breakSound.Play(player.GetAvailableAudioSource());
            }
        }
    }

    internal void HandleProjectileHit(ProjectileScript projectile)
    {
        if (!_ramActive) return;
        if (projectile != null)
        {
            var shooter = projectile.GetShooter();
            if (shooter == player)
            {
                return; // ignore self projectiles
            }

            Destroy(projectile.gameObject);
        }

        ApplyRamDamage(1f);
    }

    internal void HandleAsteroidHit(AsteroidScript asteroid, Collider2D collider)
    {
        if (!_ramActive) return;

        Vector2 direction = asteroid != null
            ? (Vector2)(asteroid.transform.position - transform.position).normalized
            : (Vector2)transform.up;
        if (direction == Vector2.zero)
        {
            direction = transform.up;
        }

        if (asteroid != null)
        {
            var asteroidRb = asteroid.GetComponent<Rigidbody2D>();
            if (asteroidRb != null)
            {
                asteroidRb.AddForce(direction * ram.knockbackForce, ForceMode2D.Impulse);
            }
        }

        GrantImpactCharge(true);
        ApplySelfRecoil(-direction);
        BreakRam(true);
    }

    internal void HandleEntityHit(Entity entity)
    {
        if (!_ramActive || entity == null || entity == player) return;

        Vector2 direction = (entity.transform.position - transform.position).normalized;
        if (direction == Vector2.zero)
        {
            direction = transform.up;
        }

        if (ram.stunDuration > 0f)
        {
            entity.ApplySlow(0f, ram.stunDuration);
        }

        var targetRb = entity.GetComponent<Rigidbody2D>();
        if (targetRb != null && ram.knockbackForce > 0f)
        {
            targetRb.AddForce(direction * ram.knockbackForce, ForceMode2D.Impulse);
        }

        if (ram.hitSound != null)
        {
            ram.hitSound.Play(player.GetAvailableAudioSource());
        }

        GrantImpactCharge(entity is Player);
        ApplySelfRecoil(-direction);
        BreakRam(true);
    }

    private void ApplySelfRecoil(Vector2 direction)
    {
        if (ram.selfRecoil <= 0f) return;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(direction.normalized * ram.selfRecoil, ForceMode2D.Impulse);
        }
    }

    private void ApplyRamDamage(float amount)
    {
        _currentHealth -= amount;

        if (ram.hitSound != null)
        {
            ram.hitSound.Play(player.GetAvailableAudioSource());
        }

        if (_currentHealth <= 0f)
        {
            BreakRam(true);
        }
    }

    private void GrantImpactCharge(bool shouldGrant)
    {
        if (!shouldGrant || _chargeProvider == null) return;
        _chargeProvider.GainCharges(1);
    }
}

public class BatteryRamCollider : MonoBehaviour
{
    private BatteryRam _ability;

    public void Initialize(BatteryRam ability)
    {
        _ability = ability;
    }

    public void BindChildColliders()
    {
        var colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            col.isTrigger = true;
            if (col.gameObject.GetComponent<BatteryRamCollider>() == null)
            {
                var forwarder = col.gameObject.GetComponent<BatteryRamChildColliderForwarder>();
                if (forwarder == null)
                {
                    forwarder = col.gameObject.AddComponent<BatteryRamChildColliderForwarder>();
                }
                forwarder.Initialize(this);
            }
        }
    }

    internal void HandleTrigger(Collider2D other)
    {
        if (_ability == null || !_ability.IsAbilityActive()) return;
        if (other.transform.IsChildOf(_ability.transform)) return;

        var projectile = other.GetComponent<ProjectileScript>();
        if (projectile != null)
        {
            // Only break on incoming shots (ignore owner projectiles)
            if (projectile.GetShooter() != _ability.GetComponent<Player>())
            {
                _ability.HandleProjectileHit(projectile);
            }
            return;
        }

        var asteroid = other.GetComponentInParent<AsteroidScript>();
        if (asteroid != null)
        {
            _ability.HandleAsteroidHit(asteroid, other);
            return;
        }

        var entity = other.GetComponentInParent<Entity>();
        if (entity != null)
        {
            _ability.HandleEntityHit(entity);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }
}

public class BatteryRamChildColliderForwarder : MonoBehaviour
{
    private BatteryRamCollider _parentCollider;

    public void Initialize(BatteryRamCollider parent)
    {
        _parentCollider = parent;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        _parentCollider?.HandleTrigger(other);
    }
}
