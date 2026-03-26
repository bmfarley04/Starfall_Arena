using UnityEngine;

public class PhysicalProjectile3D : Projectile3D
{
    protected override void ApplyDamageToEntity(Entity3D damageable, Vector3 hitPoint, Collider collider)
    {
        damageable.TakeDirectDamage(_damage, hitPoint, _shooter);
        ApplyImpactForce(collider);
    }
}
