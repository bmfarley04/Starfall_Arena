using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class EnemyProjectileChargeAttack3D : NetworkBehaviour
{
    [Header("Weapon")]
    [Tooltip("Projectile or missile weapon that this charge driver will fire. The assigned component must implement IEnemyProjectileWeapon3D, such as ProjectileWeaponEnemy3D, MissileWeaponEnemy3D, or a staggered enemy weapon.")]
    [SerializeField] private MonoBehaviour projectileWeaponComponent;

    [Tooltip("Network combat helper used to spawn server-authoritative projectiles in networked Invasion. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Header("Telegraph")]
    [Tooltip("Visual charge tell played during the windup. Auto-assigned from this GameObject or children when left empty.")]
    [SerializeField] private ProjectileChargeTelegraph3D chargeTelegraph;

    [Tooltip("Seconds to wait between a brain choosing to attack and the projectile or missile actually launching. Set to 0 for no windup.")]
    [SerializeField] private float chargeDuration = 0.4f;

    [Tooltip("If true, the charge visual stops as soon as the projectile or missile fires. Disable only when another component intentionally owns the fade-out timing.")]
    [SerializeField] private bool stopTelegraphOnFire = true;

    private IEnemyProjectileWeapon3D _projectileWeapon;
    private bool _isCharging;
    private float _fireAtTime;
    private Vector3 _lockedFireDirection;
    private Faction3D _targetFaction = Faction3D.PlayerTeam;

    public bool IsCharging => _isCharging;
    public bool IsFireGateReady => !_isCharging && ResolveProjectileWeapon() != null && _projectileWeapon.IsFireGateReady;
    public float ChargeDuration => chargeDuration;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        chargeDuration = Mathf.Max(0f, chargeDuration);

        if (projectileWeaponComponent != null && projectileWeaponComponent is not IEnemyProjectileWeapon3D)
        {
            Debug.LogWarning($"[{nameof(EnemyProjectileChargeAttack3D)}] {projectileWeaponComponent.name} does not implement IEnemyProjectileWeapon3D and cannot be fired by this charge driver.", this);
            projectileWeaponComponent = null;
        }
    }

    private void OnDisable()
    {
        CancelCharge(immediate: true);
    }

    private void Update()
    {
        if (!_isCharging || !HasFireAuthority())
        {
            return;
        }

        if (Time.time < _fireAtTime)
        {
            return;
        }

        FireLockedShot();
    }

    public bool TryBeginCharge(Faction3D targetFaction, Vector3 fireDirection)
    {
        CacheReferences();

        if (_isCharging || ResolveProjectileWeapon() == null || !_projectileWeapon.IsFireGateReady)
        {
            return false;
        }

        if (fireDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _targetFaction = targetFaction;
        _lockedFireDirection = fireDirection.normalized;

        if (chargeDuration <= 0.0001f)
        {
            FireLockedShot();
            return true;
        }

        _isCharging = true;
        _fireAtTime = Time.time + chargeDuration;
        PlayChargeTelegraph(chargeDuration);
        return true;
    }

    public void CancelCharge(bool immediate = false)
    {
        if (!_isCharging && !immediate)
        {
            return;
        }

        _isCharging = false;
        _lockedFireDirection = Vector3.zero;
        StopChargeTelegraph(immediate);
    }

    private void FireLockedShot()
    {
        if (ResolveProjectileWeapon() == null)
        {
            CancelCharge(immediate: true);
            return;
        }

        Vector3 fireDirection = _lockedFireDirection.sqrMagnitude > 0.0001f
            ? _lockedFireDirection
            : transform.forward;

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.TryFireProjectilePattern(_projectileWeapon, _targetFaction, fireDirection);
        }
        else
        {
            _projectileWeapon.TryFireAtFaction(_targetFaction, fireDirection);
        }

        _isCharging = false;
        _lockedFireDirection = Vector3.zero;

        if (stopTelegraphOnFire)
        {
            StopChargeTelegraph(immediate: false);
        }
    }

    private void CacheReferences()
    {
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        chargeTelegraph ??= GetComponentInChildren<ProjectileChargeTelegraph3D>(true);
        ResolveProjectileWeapon();
    }

    private IEnemyProjectileWeapon3D ResolveProjectileWeapon()
    {
        if (_projectileWeapon != null)
        {
            return _projectileWeapon;
        }

        if (projectileWeaponComponent is IEnemyProjectileWeapon3D assignedWeapon)
        {
            _projectileWeapon = assignedWeapon;
            return _projectileWeapon;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyProjectileWeapon3D foundWeapon)
            {
                projectileWeaponComponent = behaviours[i];
                _projectileWeapon = foundWeapon;
                return _projectileWeapon;
            }
        }

        return null;
    }

    private void PlayChargeTelegraph(float duration)
    {
        chargeTelegraph?.PlayCharge(duration);

        if (ShouldReplicateTelegraph())
        {
            PlayChargeTelegraphClientRpc(duration, ResolveNetworkServerTime());
        }
    }

    private void StopChargeTelegraph(bool immediate)
    {
        chargeTelegraph?.StopCharge(immediate);

        if (ShouldReplicateTelegraph())
        {
            StopChargeTelegraphClientRpc(immediate);
        }
    }

    private bool ShouldReplicateTelegraph()
    {
        return NetTickUtil.IsActive
            && NetworkManager.Singleton != null
            && IsServer
            && IsSpawned;
    }

    private double ResolveNetworkServerTime()
    {
        return NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d;
    }

    private bool HasFireAuthority()
    {
        return !NetTickUtil.IsActive
            || NetworkManager.Singleton == null
            || IsServer;
    }

    [ClientRpc]
    private void PlayChargeTelegraphClientRpc(float duration, double serverStartTime)
    {
        if (IsServer)
        {
            return;
        }

        float elapsed = 0f;
        if (NetworkManager.Singleton != null && serverStartTime > 0d)
        {
            elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - serverStartTime));
        }

        chargeTelegraph?.PlayCharge(duration, elapsed);
    }

    [ClientRpc]
    private void StopChargeTelegraphClientRpc(bool immediate)
    {
        if (IsServer)
        {
            return;
        }

        chargeTelegraph?.StopCharge(immediate);
    }
}
