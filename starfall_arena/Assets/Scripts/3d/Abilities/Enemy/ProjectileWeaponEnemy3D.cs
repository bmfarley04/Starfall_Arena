using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileWeaponEnemy3D : EnemyProjectileWeaponBase3D
{
    public override NetProjectileVisualType3D NetworkVisualType => NetProjectileVisualType3D.EnemyProjectile;
}
