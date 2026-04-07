using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ProjectileWeapon3D))]
public class StupidTurret3D : MonoBehaviour
{
    [Header("Turret Firing")]
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private bool fireContinuously = true;

    private void Awake()
    {
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
    }

    private void Update()
    {
        if (!fireContinuously || primaryWeapon == null)
        {
            return;
        }

        primaryWeapon.TryFire();
    }

    public void SetFireContinuously(bool shouldFire)
    {
        fireContinuously = shouldFire;
    }

    public void SetPrimaryWeapon(ProjectileWeapon3D weapon)
    {
        primaryWeapon = weapon;
    }
}
