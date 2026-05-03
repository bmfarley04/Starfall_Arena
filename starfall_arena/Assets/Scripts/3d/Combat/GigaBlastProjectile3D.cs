using UnityEngine;

public class GigaBlastProjectile3D : Projectile3D
{
    public override void Initialize(Vector3 direction, Vector3 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity3D shooter = null, int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        base.Initialize(direction, shipVelocity, speed, damage, lifetime, impactForce, shooter, accuracyAttackId);
    }

    protected override bool IsValidHit(RaycastHit hit)
    {
        if (!base.IsValidHit(hit))
        {
            return false;
        }

        if (!_canPierce)
        {
            return true;
        }

        Entity3D damageable = ResolveHitEntity(hit.collider);
        return damageable == null || !_hitEntityIds.Contains(damageable.GetInstanceID());
    }

    protected override bool IsValidOverlapHit(Collider collider)
    {
        if (!base.IsValidOverlapHit(collider))
        {
            return false;
        }

        if (!_canPierce)
        {
            return true;
        }

        Entity3D damageable = ResolveHitEntity(collider);
        return damageable == null || !_hitEntityIds.Contains(damageable.GetInstanceID());
    }

    protected override void ProcessHit(RaycastHit hit)
    {
        Collider other = hit.collider;
        ReflectShield3D reflectShield = ResolveReflectShield(other);
        if (reflectShield != null && reflectShield.TryReflectProjectile(this, hit.point))
        {
            return;
        }

        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            if (_canPierce && !TryRegisterEntityHit(damageable))
            {
                return;
            }

            ApplyDamageToEntity(damageable, hit.point, other);
            SpawnHitEffect(hit);

            if (TryContinueAfterPierce())
            {
                return;
            }

            DespawnSelf();
            return;
        }

        SpawnHitEffect(hit);
        DespawnSelf();
    }

    protected override void ProcessOverlapHit(OverlapHitInfo hit)
    {
        Collider other = hit.collider;
        Entity3D damageable = ResolveHitEntity(other);
        if (damageable != null && IsMatchingTarget(damageable))
        {
            if (_canPierce && !TryRegisterEntityHit(damageable))
            {
                return;
            }

            ApplyDamageToEntity(damageable, hit.point, other);

            if (TryContinueAfterPierce())
            {
                return;
            }
        }

        DespawnSelf();
    }
}
