using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class SuicideDroneEnemyBrain3D : MonoBehaviour
{
    [Header("Suicide Drone")]
    [SerializeField] private Enemy3D enemy;
    [SerializeField] private EnemyAIFlightController3D flightController;
    [SerializeField] private EnemyTargetSensor3D targetSensor;
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;
    [SerializeField] private EnemyPatrol3D patrol;
    [SerializeField] private float thinkInterval = 0.05f;
    [SerializeField] private float detonationDamage = 35f;
    [SerializeField] private float detonationRadius = 4f;
    [SerializeField] private float contactDetonationDistance = 1.75f;
    [SerializeField] private bool useObstacleAvoidance = true;

    private readonly Collider[] _overlapResults = new Collider[8];
    private readonly Entity3D[] _damagedEntities = new Entity3D[8];

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private bool _hasDetonated;

    private void Awake()
    {
        enemy ??= GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    public void ApplyProfile(EnemyBalanceProfile3D.SuicideDroneBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        detonationDamage = Mathf.Max(0f, stats.detonationDamage);
        detonationRadius = Mathf.Max(0f, stats.detonationRadius);
        contactDetonationDistance = Mathf.Max(0f, stats.contactDetonationDistance);
    }

    private void OnDisable()
    {
        _hasDetonated = false;
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

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasBrainAuthority() || _hasDetonated)
        {
            return;
        }

        Entity3D hitEntity = ResolveEntity(collision.collider);
        if (IsTargetEntity(hitEntity))
        {
            Detonate();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasBrainAuthority() || _hasDetonated)
        {
            return;
        }

        Entity3D hitEntity = ResolveEntity(other);
        if (IsTargetEntity(hitEntity))
        {
            Detonate();
        }
    }

    private void Think()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            Detonate();
            return;
        }

        if (distanceToTarget <= Mathf.Max(0f, contactDetonationDistance))
        {
            Detonate();
            return;
        }

        Vector3 steeringDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
            ? obstacleAvoidance.ResolveSteeringDirection(toTarget)
            : toTarget.normalized;

        flightController?.SetMoveDirection(steeringDirection, 1f);
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private void Detonate()
    {
        if (_hasDetonated || enemy == null)
        {
            return;
        }

        _hasDetonated = true;
        flightController?.ClearFlightIntent();

        float radius = Mathf.Max(Mathf.Max(0f, detonationRadius), Mathf.Max(0.1f, contactDetonationDistance));
        if (radius > 0f)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapResults);
            int damagedCount = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Entity3D hitEntity = ResolveEntity(_overlapResults[i]);
                if (!IsTargetEntity(hitEntity) || WasAlreadyDamaged(hitEntity, damagedCount))
                {
                    continue;
                }

                if (damagedCount < _damagedEntities.Length)
                {
                    _damagedEntities[damagedCount] = hitEntity;
                    damagedCount++;
                }

                hitEntity.TakeDamage(detonationDamage, transform.position, enemy, DamageSource3D.Direct);
            }
        }

        enemy.TakeDirectDamage(enemy.CurrentHealth, transform.position, null);
    }

    private bool IsTargetEntity(Entity3D candidate)
    {
        return candidate != null
            && candidate.CurrentHealth > 0f
            && FactionMember3D.ResolveFaction(candidate) == Faction3D.PlayerTeam;
    }

    private static Entity3D ResolveEntity(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        return collider.GetComponentInParent<Entity3D>();
    }

    private bool WasAlreadyDamaged(Entity3D candidate, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (_damagedEntities[i] == candidate)
            {
                return true;
            }
        }

        return false;
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
