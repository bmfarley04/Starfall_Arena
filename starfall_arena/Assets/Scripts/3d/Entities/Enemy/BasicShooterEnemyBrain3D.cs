using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class BasicShooterEnemyBrain3D : MonoBehaviour
{
    [Header("Basic Shooter")]
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private EnemyAIFlightController3D flightController;
    [SerializeField] private EnemyTargetSensor3D targetSensor;
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;
    [SerializeField] private float thinkInterval = 0.05f;
    [SerializeField] private float aimToleranceDegrees = 10f;
    [SerializeField] private float stopDistance = 18f;
    [SerializeField] private float fullSpeedDistance = 45f;
    [SerializeField] private bool useObstacleAvoidance;

    private NetworkObject _networkObject;
    private float _nextThinkTime;

    private void Awake()
    {
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            flightController?.ClearFlightIntent();
            return;
        }

        if (Time.time < _nextThinkTime)
        {
            return;
        }

        _nextThinkTime = Time.time + Mathf.Max(0.01f, thinkInterval);
        Think();
    }

    private void Think()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(toTarget)
            : toTarget.normalized;

        flightController?.SetMoveDirection(steeringDirection, ResolveDistanceSpeedScale(toTarget.magnitude));

        if (primaryWeapon == null || !IsAimedAtTarget(toTarget))
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.TryFireProjectilePattern(primaryWeapon, Faction3D.PlayerTeam);
            return;
        }

        primaryWeapon.TryFireAtFaction(Faction3D.PlayerTeam);
    }

    private bool IsAimedAtTarget(Vector3 toTarget)
    {
        return Vector3.Angle(transform.forward, toTarget.normalized) <= Mathf.Max(0f, aimToleranceDegrees);
    }

    private float ResolveDistanceSpeedScale(float distanceToTarget)
    {
        float stop = Mathf.Max(0f, stopDistance);
        float full = Mathf.Max(stop + 0.01f, fullSpeedDistance);

        if (distanceToTarget <= stop)
        {
            return 0f;
        }

        if (distanceToTarget >= full)
        {
            return 1f;
        }

        return Mathf.InverseLerp(stop, full, distanceToTarget);
    }

    private bool HasBrainAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return !_networkObject.IsSpawned
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }
}
