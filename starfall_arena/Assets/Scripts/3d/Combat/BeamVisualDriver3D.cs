using UnityEngine;

public abstract class BeamVisualDriver3D : MonoBehaviour
{
    public abstract void BeginFiring();
    public abstract void EndFiring();
    public abstract void UpdateBeamVisual(
        Vector3 origin,
        Vector3 aimDirection,
        float beamLength,
        bool hitSomething,
        Vector3 hitPoint,
        Vector3 hitNormal);
}
