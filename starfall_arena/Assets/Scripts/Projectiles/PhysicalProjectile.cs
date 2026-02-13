using NUnit.Framework.Internal;
using UnityEngine;

/// <summary>
/// A projectile that deals damage directly to health, bypassing shields entirely.
/// Useful for physical/kinetic weapons that pierce through energy shields.
/// </summary>
public class PhysicalProjectile : ProjectileScript
{
    protected override void OnTriggerEnter2D(Collider2D collider)
    {
        // Check for ship collision
        if (collider.CompareTag(targetTag))
        {
            // Check if player has active reflect shield
            Player player = collider.GetComponent<Player>();
            player.TryGetComponent<Reflector>(out var reflectScript);
            if (player != null && reflectScript.IsAbilityActive())
            {
                // Let Class1 handle the reflection
                return;
            }

            var damageable = collider.GetComponent<Entity>();
            if (damageable != null)
            {
                // Deal damage directly to health, bypassing shields
                damageable.TakeDirectDamage(_damage, _impactForce, transform.position);
                ApplyImpactForce(collider);

                // Apply slow effect if enabled
                if (_appliesSlow)
                {
                    damageable.ApplySlow(_slowMultiplier, _slowDuration);
                }

                if (_visualController != null)
                {
                    _visualController.OnProjectileImpact(transform.position, _direction);
                }

                // If piercing is enabled, reduce damage and continue; otherwise destroy
                if (_canPierce && _pierceMultiplier > 0)
                {
                    _damage *= _pierceMultiplier;
                    // Continue traveling through the target
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
        // Check for asteroid collision
        else if (collider.CompareTag("Asteroid"))
        {
            var asteroid = collider.GetComponent<AsteroidScript>();
            if (asteroid != null)
            {
                asteroid.TakeDamage(_damage, _impactForce, transform.position);
            }

            if (_visualController != null)
            {
                _visualController.OnProjectileImpact(transform.position, _direction);
            }

            // If piercing is enabled, reduce damage and continue; otherwise destroy
            if (_canPierce && _pierceMultiplier > 0)
            {
                _damage *= _pierceMultiplier;
                // Continue traveling through the asteroid
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void ApplyImpactForce(Collider2D collider)
    {
        Rigidbody2D targetRb = collider.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 forceDirection = _direction.normalized;
            targetRb.AddForce(forceDirection * _impactForce, ForceMode2D.Impulse);
        }
    }
}
