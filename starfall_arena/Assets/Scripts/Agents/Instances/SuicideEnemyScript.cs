using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Suicide drone enemy that pursues the player relentlessly and explodes on contact or low health
/// </summary>
public class SuicideEnemyScript : EnemyScript
{
    [Header("Suicide Drone Settings")]
    [Tooltip("Damage dealt by explosion")]
    public float explosionDamage = 50f;

    [Tooltip("Radius of explosion damage")]
    public float explosionRadius = 3f;

    [Tooltip("Impact force applied to player when explosion hits")]
    public float explosionImpactForce = 100f;

    [Tooltip("Health percentage threshold to trigger explosion (0-1)")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.2f;

    [Header("Screen Shake")]
    [Tooltip("multiplier for screen shake force")]
    public float shakeForceMultiplier = 2f;

    private bool _hasDetectedPlayer = false;
    private bool _hasExploded = false;
    // Note: _impulseSource is inherited from EnemyScript base class

    protected override void UpdateEnemyState()
    {
        // Once player is detected, always pursue directly - no searching or patrolling
        if (_hasDetectedPlayer)
        {
            _currentState = EnemyState.Pursuing;
            if (_target != null)
            {
                _lastKnownPlayerPosition = _target.position;
            }
            return;
        }

        // Before detection, use normal patrol behavior
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                if (IsPlayerInRange())
                {
                    _currentState = EnemyState.Pursuing;
                    _lastKnownPlayerPosition = _target.position;
                    _hasDetectedPlayer = true;
                }
                break;
        }
    }

    protected override void TryFire()
    {
        // Suicide drones don't fire projectiles - they only explode
        // Override to prevent shooting
    }

    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.TakeDamage(damage, impactForce, hitPoint, source);

        // Check if health dropped below threshold
        if (currentHealth > 0 && currentHealth <= maxHealth * lowHealthThreshold)
        {
            Explode();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Explode on collision with player
        if (collision.gameObject.CompareTag("Player"))
        {
            Explode();
        
        }
    }

    private void Explode()
    {
        // Prevent multiple explosions
        if (_hasExploded)
            return;

        _hasExploded = true;

        // Check if player is within explosion radius
        if (_target != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, _target.position);

            if (distanceToPlayer <= explosionRadius)
            {
                ShipBase playerShip = _target.GetComponent<ShipBase>();
                if (playerShip != null)
                {
                    // Apply explosion damage with impact force
                    playerShip.TakeDamage(explosionDamage, explosionImpactForce, transform.position, DamageSource.Explosion);

                    // Calculate direction from enemy to player for directional shake
                    Vector2 explosionDirection = (_target.position - transform.position).normalized;

                    // Generate directional screen shake
                    if (_impulseSource != null)
                    {
                        float shakeForce = explosionDamage * shakeForceMultiplier;
                        Vector3 impactVelocity = explosionDirection * shakeForce;
                        _impulseSource.GenerateImpulse(impactVelocity);
                    }
                }
            }
        }

        // Destroy the drone
        base.Die();
    }

    // Optional: Visualize explosion radius in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
