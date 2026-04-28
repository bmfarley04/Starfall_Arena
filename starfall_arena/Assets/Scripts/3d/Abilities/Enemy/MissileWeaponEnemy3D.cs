using UnityEngine;

[DisallowMultipleComponent]
public class MissileWeaponEnemy3D : EnemyProjectileWeaponBase3D
{
    public override NetProjectileVisualType3D NetworkVisualType => NetProjectileVisualType3D.EnemyMissile;

    protected override bool SupportsMuzzleEffects => false;

    protected override bool ValidateProjectilePrefab(GameObject projectilePrefab)
    {
        return projectilePrefab != null && projectilePrefab.GetComponent<MissileProjectile3D>() != null;
    }
}
