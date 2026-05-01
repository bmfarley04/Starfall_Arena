using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class EnemyProjectileChargeAttack3D : NetworkBehaviour
{
    private enum ChargedEnemyWeaponType
    {
        Projectile,
        Beam,
        Flamethrower
    }

    [Header("Weapon")]
    [Tooltip("Weapon family triggered after the charge finishes. Projectile preserves the original behavior; Beam starts BeamWeapon3D; Flamethrower starts EnemyFlamethrowerWeapon3D.")]
    [SerializeField] private ChargedEnemyWeaponType weaponType = ChargedEnemyWeaponType.Projectile;

    [Tooltip("Projectile or missile weapon fired after charge. The assigned component must implement IEnemyProjectileWeapon3D, such as ProjectileWeaponEnemy3D, MissileWeaponEnemy3D, or a staggered enemy weapon.")]
    [SerializeField] private MonoBehaviour projectileWeaponComponent;

    [Tooltip("Beam weapon started after charge when Weapon Type is Beam. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private BeamWeapon3D beamWeapon;

    [Tooltip("Flamethrower weapon started after charge when Weapon Type is Flamethrower. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyFlamethrowerWeapon3D flamethrowerWeapon;

    [Tooltip("Network combat helper used to spawn server-authoritative attacks in networked Invasion. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;

    [Tooltip("Presentation-only attack reporter used by TargetAwarenessHUD3D. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private TargetAwarenessAttackReporter3D attackReporter;

    [Header("Telegraph")]
    [Tooltip("Visual charge tell played during the windup. Auto-assigned from this GameObject or children when left empty.")]
    [SerializeField] private ProjectileChargeTelegraph3D chargeTelegraph;

    [Tooltip("Seconds to wait between a brain choosing to attack and the weapon actually firing. Set to 0 for no windup.")]
    [SerializeField] private float chargeDuration = 0.4f;

    [Tooltip("If true, the charge visual stops as soon as the weapon fires. Disable only when another component intentionally owns the fade-out timing.")]
    [SerializeField] private bool stopTelegraphOnFire = true;

    private IEnemyProjectileWeapon3D _projectileWeapon;
    private bool _isCharging;
    private float _fireAtTime;
    private Vector3 _lockedFireDirection;
    private Faction3D _targetFaction = Faction3D.PlayerTeam;
    private Entity3D _intendedTarget;

    public bool IsCharging => _isCharging;
    public bool IsFireGateReady => !_isCharging && IsSelectedWeaponReady();
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

        FireChargedWeapon();
    }

    public bool TryBeginCharge(Faction3D targetFaction, Vector3 fireDirection)
    {
        return TryBeginCharge(targetFaction, fireDirection, null);
    }

    public bool TryBeginCharge(Faction3D targetFaction, Vector3 fireDirection, Entity3D intendedTarget)
    {
        CacheReferences();

        if (_isCharging || !IsSelectedWeaponReady())
        {
            return false;
        }

        if (fireDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        _targetFaction = targetFaction;
        _lockedFireDirection = fireDirection.normalized;
        _intendedTarget = intendedTarget;

        if (chargeDuration <= 0.0001f)
        {
            FireChargedWeapon();
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
        _intendedTarget = null;
        StopChargeTelegraph(immediate);
    }

    private void FireChargedWeapon()
    {
        bool fired = weaponType switch
        {
            ChargedEnemyWeaponType.Beam => FireBeam(),
            ChargedEnemyWeaponType.Flamethrower => FireFlamethrower(),
            _ => FireProjectile()
        };

        _isCharging = false;
        _lockedFireDirection = Vector3.zero;
        _intendedTarget = null;

        if (stopTelegraphOnFire || !fired)
        {
            StopChargeTelegraph(immediate: false);
        }
    }

    private bool FireProjectile()
    {
        if (ResolveProjectileWeapon() == null)
        {
            CancelCharge(immediate: true);
            return false;
        }

        Vector3 fireDirection = ResolveLockedFireDirection();
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.TryFireProjectilePattern(_projectileWeapon, _targetFaction, fireDirection, _intendedTarget);
        }

        bool fired = _projectileWeapon.TryFireAtFaction(_targetFaction, fireDirection);
        if (fired)
        {
            attackReporter?.ReportAttack(_intendedTarget);
        }

        return fired;
    }

    private bool FireBeam()
    {
        if (ResolveBeamWeapon() == null || !beamWeapon.CanStartBeamNow())
        {
            CancelCharge(immediate: true);
            return false;
        }

        Vector3 aimDirection = ResolveLockedFireDirection();
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.SetBeamState(beamWeapon, true, aimDirection, _intendedTarget);
        }

        beamWeapon.ApplyNetworkBeamAim(aimDirection);
        beamWeapon.ApplyNetworkBeamState(true, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
        attackReporter?.ReportSustainedAttack(_intendedTarget, 0.25f);
        return true;
    }

    private bool FireFlamethrower()
    {
        if (ResolveFlamethrowerWeapon() == null || !flamethrowerWeapon.CanStartBurst())
        {
            CancelCharge(immediate: true);
            return false;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            return netEnemyCombat.SetFlamethrowerState(flamethrowerWeapon, true, _intendedTarget);
        }

        bool fired = flamethrowerWeapon.TryStartBurst(authoritativeDamage: true);
        if (fired)
        {
            attackReporter?.ReportSustainedAttack(_intendedTarget, flamethrowerWeapon.BurstDuration);
        }

        return fired;
    }

    private Vector3 ResolveLockedFireDirection()
    {
        return _lockedFireDirection.sqrMagnitude > 0.0001f
            ? _lockedFireDirection
            : transform.forward;
    }

    private void CacheReferences()
    {
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        attackReporter ??= GetComponent<TargetAwarenessAttackReporter3D>() ?? gameObject.AddComponent<TargetAwarenessAttackReporter3D>();
        chargeTelegraph ??= GetComponentInChildren<ProjectileChargeTelegraph3D>(true);
        beamWeapon ??= GetComponent<BeamWeapon3D>();
        flamethrowerWeapon ??= GetComponent<EnemyFlamethrowerWeapon3D>();
        ResolveProjectileWeapon();
    }

    private bool IsSelectedWeaponReady()
    {
        return weaponType switch
        {
            ChargedEnemyWeaponType.Beam => ResolveBeamWeapon() != null && beamWeapon.CanStartBeamNow(),
            ChargedEnemyWeaponType.Flamethrower => ResolveFlamethrowerWeapon() != null && flamethrowerWeapon.CanStartBurst(),
            _ => ResolveProjectileWeapon() != null && _projectileWeapon.IsFireGateReady
        };
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

    private BeamWeapon3D ResolveBeamWeapon()
    {
        beamWeapon ??= GetComponent<BeamWeapon3D>();
        return beamWeapon;
    }

    private EnemyFlamethrowerWeapon3D ResolveFlamethrowerWeapon()
    {
        flamethrowerWeapon ??= GetComponent<EnemyFlamethrowerWeapon3D>();
        return flamethrowerWeapon;
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
