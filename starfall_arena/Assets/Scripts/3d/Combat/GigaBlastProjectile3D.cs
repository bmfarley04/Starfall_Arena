using UnityEngine;

public class GigaBlastProjectile3D : Projectile3D
{
    private bool _canPierce;
    private float _pierceDamageMultiplier = 1f;

    public override void Initialize(Vector3 direction, Vector3 shipVelocity, float speed, float damage, float lifetime, float impactForce, Entity3D shooter = null)
    {
        base.Initialize(direction, shipVelocity, speed, damage, lifetime, impactForce, shooter);
        _canPierce = false;
        _pierceDamageMultiplier = 1f;
    }

    public void EnablePiercing(float damageMultiplierPerPierce)
    {
        _canPierce = true;
        _pierceDamageMultiplier = damageMultiplierPerPierce;
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

            if (_canPierce && _pierceDamageMultiplier > 0f)
            {
                _damage *= _pierceDamageMultiplier;
                return;
            }

            DespawnSelf();
            return;
        }

        SpawnHitEffect(hit);
        DespawnSelf();
    }
}
