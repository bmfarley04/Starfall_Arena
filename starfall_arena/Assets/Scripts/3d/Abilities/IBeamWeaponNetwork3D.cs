using UnityEngine;

public interface IBeamWeaponNetwork3D
{
    void ApplyNetworkBeamState(bool isFiring, bool authoritative, int accuracyAttackId);
    void ApplyNetworkBeamAim(Vector3 aimDirection);
}
