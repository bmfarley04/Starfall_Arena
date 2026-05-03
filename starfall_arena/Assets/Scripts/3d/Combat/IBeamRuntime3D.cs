using UnityEngine;

public interface IBeamRuntime3D
{
    void Initialize(string targetTag, Faction3D targetFaction, float damagePerSecond, float maxDistance,
        float recoilForcePerSecond, float impactForce, Entity3D shooter,
        Transform positionAnchor = null, float anchorOffset = 0f, float verticalOffset = 0f, Camera aimCamera = null);
    void SetCosmeticOnly(bool isCosmeticOnly);
    void SetNetworkAuthority(NetCombat3D networkAuthority);
    void SetServerAuthoritativeGameplay(bool serverAuthoritativeGameplay);
    void SetAccuracyAttackId(int attackId);
    void SetNetworkAim(Vector3 direction);
    void ClearNetworkAim();
    float GetRecoilForcePerSecond();
    void StartFiring();
    void StopFiring();
}

public interface IBeamDirectionSource3D
{
    void SetBeamDirectionSource(Transform directionSource);
}

public interface IBeamAimConstraint3D
{
    void SetAllowExplicitAimBehindForward(bool allowExplicitAimBehindForward);
}
