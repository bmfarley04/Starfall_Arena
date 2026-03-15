using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerBomb : Ability
{
    // This file was heavily written with AI
    [System.Serializable]
    public struct TriggerBombAbilityConfig
    {
        [Header("Spawning")]
        [Tooltip("Distance in front of ship to spawn the bomb")]
        public float spawnOffset;

        [Header("Cooldown & Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;

        [Header("Bomb Projectile")]
        [Tooltip("Bomb projectile prefab")]
        public GameObject bombPrefab;
        [Tooltip("Speed of the bomb when launched")]
        public float launchSpeed;
        [Tooltip("Maximum time bomb can exist before auto-exploding (seconds)")]
        public float maxLifetime;
        [Tooltip("Recoil force when launching bomb")]
        public float recoilForce;

        [Header("Explosion")]
        [Tooltip("Explosion effect prefab")]
        public GameObject explosionPrefab;
        [Tooltip("Explosion damage")]
        public float explosionDamage;
        [Tooltip("Explosion radius")]
        public float explosionRadius;
        [Tooltip("Impact force applied to entities in explosion")]
        public float explosionImpactForce;
        [Tooltip("Scale of explosion visual effect")]
        public float explosionScale;

        [Header("Sound Effects")]
        [Tooltip("Sound played when launching bomb")]
        public SoundEffect launchSound;
        [Tooltip("Sound played when bomb explodes")]
        public SoundEffect explosionSound;
    }

    public TriggerBombAbilityConfig bomb;

    // ===== PRIVATE STATE =====
    private float _lastBombTime = -999f;
    private GameObject _activeBomb;
    private Rigidbody2D _activeBombRb;
    private Coroutine _autoDetonateCoroutine;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        (player?.ability2 as Invisibility)?.BreakInvisibilityFromAction();

        if (value.isPressed)
        {
            // Launch bomb
            if (_activeBomb == null && Time.time >= _lastBombTime + bomb.cooldown)
            {
                LaunchBomb();
            }
        }
        else
        {
            // Detonate bomb
            if (_activeBomb != null)
            {
                DetonateBomb();
            }
        }
    }

    private void LaunchBomb()
    {
        if (bomb.bombPrefab == null)
        {
            Debug.LogWarning("Bomb prefab not assigned!");
            return;
        }

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;

        // Spawn bomb in front of ship
        Vector3 spawnPosition = transform.position + transform.up * bomb.spawnOffset;
        Vector2 launchVelocity = (Vector2)transform.up * bomb.launchSpeed;

        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkBombLaunch(new NetTriggerBombLaunchState
                {
                    SpawnPosition = spawnPosition,
                    Velocity = launchVelocity
                }, authoritative: false);
                player.ApplyRecoil(bomb.recoilForce);
            }

            _netMovement.RequestTriggerBombLaunch(spawnPosition, launchVelocity);
        }
        else
        {
            ApplyNetworkBombLaunch(new NetTriggerBombLaunchState
            {
                SpawnPosition = spawnPosition,
                Velocity = launchVelocity
            }, authoritative: true);
        }

    }

    private System.Collections.IEnumerator AutoDetonateBomb()
    {
        yield return new WaitForSeconds(bomb.maxLifetime);

        if (_activeBomb != null)
        {
            DetonateBomb();
        }
    }

    private void DetonateBomb()
    {
        if (_activeBomb == null) return;

        // Stop auto-detonate coroutine if it's running
        if (_autoDetonateCoroutine != null)
        {
            StopCoroutine(_autoDetonateCoroutine);
            _autoDetonateCoroutine = null;
        }

        Vector3 explosionPosition = _activeBomb.transform.position;
        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;

        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkBombDetonation(new NetTriggerBombDetonateState { Position = explosionPosition }, authoritative: false);
            }

            _netMovement.RequestTriggerBombDetonate(explosionPosition);
            return;
        }

        ApplyNetworkBombDetonation(new NetTriggerBombDetonateState { Position = explosionPosition }, authoritative: true);
    }

    public void ApplyNetworkBombLaunch(NetTriggerBombLaunchState state, bool authoritative)
    {
        if (_activeBomb != null)
        {
            Destroy(_activeBomb);
        }

        _activeBomb = Instantiate(bomb.bombPrefab, state.SpawnPosition, transform.rotation);
        _activeBombRb = _activeBomb.GetComponent<Rigidbody2D>();
        if (_activeBombRb != null)
        {
            _activeBombRb.linearVelocity = state.Velocity;
        }

        if (!NetTickUtil.IsActive || authoritative)
        {
            player.ApplyRecoil(bomb.recoilForce);
        }

        bomb.launchSound?.Play(player.GetAvailableAudioSource());

        if (_autoDetonateCoroutine != null)
        {
            StopCoroutine(_autoDetonateCoroutine);
        }

        if (!NetTickUtil.IsActive || authoritative)
        {
            _autoDetonateCoroutine = StartCoroutine(AutoDetonateBomb());
        }
        else
        {
            _autoDetonateCoroutine = null;
        }
    }

    public void ApplyNetworkBombDetonation(NetTriggerBombDetonateState state, bool authoritative)
    {
        if (_activeBomb == null)
        {
            return;
        }

        Vector3 explosionPosition = state.Position;

        // Create explosion visual
        if (bomb.explosionPrefab != null)
        {
            if (ExplosionPool.Instance != null)
            {
                ExplosionPool.Instance.GetExplosion(explosionPosition, Quaternion.identity, bomb.explosionScale, null);
            }
            else
            {
                GameObject explosion = Instantiate(bomb.explosionPrefab, explosionPosition, Quaternion.identity);
                explosion.transform.localScale = Vector3.one * bomb.explosionScale;
            }
        }

        // Deal damage to entities in radius
        if (!NetTickUtil.IsActive || authoritative)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionPosition, bomb.explosionRadius);
            foreach (Collider2D col in hitColliders)
            {
                // Skip the bomb itself
                if (col.gameObject == _activeBomb) continue;

                Entity entity = col.GetComponent<Entity>();
                if (entity != null)
                {
                    if (col.CompareTag(player.enemyTag))
                    {
                        entity.TakeDamage(bomb.explosionDamage, bomb.explosionImpactForce, explosionPosition, DamageSource.Explosion);
                    }
                }

                if (col.CompareTag("Asteroid"))
                {
                    AsteroidScript asteroid = col.GetComponent<AsteroidScript>();
                    if (asteroid != null)
                    {
                        asteroid.TakeDamage(bomb.explosionDamage, bomb.explosionImpactForce, explosionPosition);
                    }
                }
            }
        }

        // Play explosion sound
        if (bomb.explosionSound != null)
        {
            bomb.explosionSound.Play(player.GetAvailableAudioSource());
        }

        // Destroy bomb
        Destroy(_activeBomb);
        _activeBomb = null;
        _activeBombRb = null;

        _lastBombTime = Time.time;
    }

    public override bool IsAbilityActive()
    {
        return _activeBomb != null;
    }

    // ===== HUD STATE =====
    public override float GetHUDFillRatio()
    {
        if (bomb.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastBombTime;
        if (elapsed >= bomb.cooldown) return 0f;
        return 1f - (elapsed / bomb.cooldown);
    }
    public override bool IsOnCooldown()
    {
        return Time.time < _lastBombTime + bomb.cooldown;
    }

    public override void Die()
    {
        base.Die();

        // Stop auto-detonate coroutine if running
        if (_autoDetonateCoroutine != null)
        {
            StopCoroutine(_autoDetonateCoroutine);
            _autoDetonateCoroutine = null;
        }

        // Clean up active bomb if player dies
        if (_activeBomb != null)
        {
            Destroy(_activeBomb);
            _activeBomb = null;
            _activeBombRb = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visualize explosion radius in editor
        Gizmos.color = Color.red;
        if (_activeBomb != null)
        {
            Gizmos.DrawWireSphere(_activeBomb.transform.position, bomb.explosionRadius);
        }
        else
        {
            // Show preview at spawn position
            Vector3 previewPosition = transform.position + transform.up * bomb.spawnOffset;
            Gizmos.DrawWireSphere(previewPosition, bomb.explosionRadius);
        }
    }
}
