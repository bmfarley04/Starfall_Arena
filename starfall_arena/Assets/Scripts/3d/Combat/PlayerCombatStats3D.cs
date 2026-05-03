using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombatStats3D : MonoBehaviour
{
    public const int InvalidAttackId = -1;

    public int shotsFired;
    public int shotsHit;
    public int enemiesKilled;
    public float damageDealt;
    public float damageTaken;

    private int _nextAttackId = 1;
    private readonly HashSet<int> _registeredFiredAttackIds = new HashSet<int>();
    private readonly HashSet<int> _registeredHitAttackIds = new HashSet<int>();

    public void ResetStats()
    {
        shotsFired = 0;
        shotsHit = 0;
        enemiesKilled = 0;
        damageDealt = 0f;
        damageTaken = 0f;
        _nextAttackId = 1;
        _registeredFiredAttackIds.Clear();
        _registeredHitAttackIds.Clear();
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

    public int BeginTrackedAttack(bool countsTowardAccuracy = true)
    {
        if (!countsTowardAccuracy || !HasStatsAuthority())
        {
            return InvalidAttackId;
        }

        int attackId = _nextAttackId++;
        RecordTrackedAttackFired(attackId);
        return attackId;
    }

    public void RecordTrackedAttackFired(int attackId)
    {
        if (!HasStatsAuthority() || attackId == InvalidAttackId)
        {
            return;
        }

        if (_registeredFiredAttackIds.Add(attackId))
        {
            shotsFired++;
        }
    }

    public void RecordShotHit()
    {
        if (!HasStatsAuthority())
        {
            return;
        }

        shotsHit++;
    }

    public void RegisterAttackHit(int attackId)
    {
        if (!HasStatsAuthority() || attackId == InvalidAttackId)
        {
            return;
        }

        if (_registeredHitAttackIds.Add(attackId))
        {
            shotsHit++;
        }
    }

    public void RecordDamageDealt(float amount)
    {
        if (!HasStatsAuthority() || amount <= 0f)
        {
            return;
        }

        damageDealt += amount;
    }

    public void RecordEnemyKilled()
    {
        if (!HasStatsAuthority())
        {
            return;
        }

        enemiesKilled++;
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
