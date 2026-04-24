using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombatStats3D : MonoBehaviour
{
    public int shotsFired;
    public int shotsHit;
    public float damageDealt;
    public float damageTaken;

    public void ResetStats()
    {
        shotsFired = 0;
        shotsHit = 0;
        damageDealt = 0f;
        damageTaken = 0f;
    }

    public bool HasStatsAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject != null && networkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    public void RecordShotFired(int count = 1)
    {
        if (!HasStatsAuthority() || count <= 0)
        {
            return;
        }

        shotsFired += count;
    }

    public void RecordShotHit()
    {
        if (!HasStatsAuthority())
        {
            return;
        }

        shotsHit++;
    }

    public void RecordDamageDealt(float amount)
    {
        if (!HasStatsAuthority() || amount <= 0f)
        {
            return;
        }

        damageDealt += amount;
    }

    public void RecordDamageTaken(float amount)
    {
        if (!HasStatsAuthority() || amount <= 0f)
        {
            return;
        }

        damageTaken += amount;
    }
}
