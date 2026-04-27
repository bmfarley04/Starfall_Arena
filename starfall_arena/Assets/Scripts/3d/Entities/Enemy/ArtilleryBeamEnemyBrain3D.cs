using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
[RequireComponent(typeof(BeamWeapon3D))]
public class ArtilleryBeamEnemyBrain3D : MonoBehaviour
{
    private static readonly RaycastHit[] LineOfSightHits = new RaycastHit[16];

    [Header("Artillery Beam Enemy")]
    [SerializeField] private EnemyAIFlightController3D flightController;
    [SerializeField] private EnemyTargetSensor3D targetSensor;
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;
    [SerializeField] private BeamWeapon3D beamWeapon;
    [SerializeField] private NetEnemyCombat3D netEnemyCombat;
    [SerializeField] private float thinkInterval = 0.05f;
    [SerializeField] private float aimToleranceDegrees = 8f;
    [SerializeField] private float keepAwayDistance = 20f;
    [SerializeField] private float retreatFullSpeedDistance = 10f;
    [SerializeField] private float optimalRange = 36f;
    [SerializeField] private float rangeBuffer = 5f;
    [SerializeField] private float maxEngagementRange = 60f;
    [SerializeField] private float minimumBeamRestartEnergy = 40f;
    [SerializeField] private bool useObstacleAvoidance = true;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private bool _beamActive;
    private Entity3D _currentTarget;

    private void Awake()
    {
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        beamWeapon ??= GetComponent<BeamWeapon3D>();
        netEnemyCombat ??= GetComponent<NetEnemyCombat3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        StopBeam();
        flightController?.ClearFlightIntent();
        _currentTarget = null;
    }

    private void Update()
    {
        if (!HasBrainAuthority())
        {
            StopBeam();
            flightController?.ClearFlightIntent();
            _currentTarget = null;
            return;
        }

        RefreshActiveBeamAim();

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
        _currentTarget = target;
        if (target == null)
        {
            StopBeam();
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            StopBeam();
            flightController?.SetFacingDirection(transform.forward);
            return;
        }

        Vector3 targetDirection = toTarget / distanceToTarget;
        UpdateMovement(targetDirection, distanceToTarget);
        UpdateBeam(target, targetDirection, distanceToTarget);
    }

    private void UpdateMovement(Vector3 targetDirection, float distanceToTarget)
    {
        float preferredMaxRange = Mathf.Max(keepAwayDistance + 0.01f, optimalRange + rangeBuffer);
        if (distanceToTarget < keepAwayDistance)
        {
            Vector3 retreatDirection = -targetDirection;
            float speedScale = ResolveRetreatSpeedScale(distanceToTarget);
            flightController?.SetFlightIntent(retreatDirection, targetDirection, speedScale, moveBackward: true);
            return;
        }

        if (distanceToTarget > preferredMaxRange)
        {
            Vector3 approachDirection = useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled
                ? obstacleAvoidance.ResolveSteeringDirection(targetDirection)
                : targetDirection;
            flightController?.SetFlightIntent(approachDirection, targetDirection, 1f, moveBackward: false);
            return;
        }

        flightController?.SetFacingDirection(targetDirection);
    }

    private void UpdateBeam(Entity3D target, Vector3 targetDirection, float distanceToTarget)
    {
        if (beamWeapon == null)
        {
            return;
        }

        Vector3 fireDirection = beamWeapon.GetBeamForwardDirection();
        Vector3 beamOrigin = beamWeapon.GetBeamOrigin(fireDirection);
        Vector3 toTargetFromBeam = ResolveTargetPoint(target) - beamOrigin;
        float beamToTargetDistance = toTargetFromBeam.magnitude;
        if (beamToTargetDistance <= 0.0001f)
        {
            StopBeam();
            return;
        }

        Vector3 targetDirectionFromBeam = toTargetFromBeam / beamToTargetDistance;
        bool isInRange = distanceToTarget <= Mathf.Max(0f, maxEngagementRange);
        bool isAimed = Vector3.Angle(fireDirection, targetDirectionFromBeam) <= Mathf.Max(0f, aimToleranceDegrees);
        bool canStartOrSustainBeam = CanStartOrSustainBeam();
        bool hasLineOfSight = isInRange && HasLineOfSight(target, beamOrigin, fireDirection);

        if (!isInRange || !isAimed || !hasLineOfSight || !canStartOrSustainBeam)
        {
            StopBeam();
            return;
        }

        StartOrUpdateBeam(fireDirection);
    }

    private bool CanStartOrSustainBeam()
    {
        if (beamWeapon == null)
        {
            return false;
        }

        if (beamWeapon.IsBeamActive)
        {
            return true;
        }

        if (!beamWeapon.CanStartBeamNow())
        {
            return false;
        }

        float remainingEnergy = beamWeapon.GetRemainingBeamEnergy();
        if (remainingEnergy <= 0f)
        {
            return false;
        }

        return remainingEnergy + 0.001f >= Mathf.Max(0f, minimumBeamRestartEnergy);
    }

    private void RefreshActiveBeamAim()
    {
        if (beamWeapon == null)
        {
            return;
        }

        _beamActive = beamWeapon.IsBeamActive;
        if (!_beamActive)
        {
            return;
        }

        Vector3 fireDirection = beamWeapon.GetBeamForwardDirection();
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.UpdateBeamAim(beamWeapon, fireDirection);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamAim(fireDirection);
        }
    }

    private void StartOrUpdateBeam(Vector3 aimDirection)
    {
        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, true, aimDirection);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamAim(aimDirection);
            beamWeapon.ApplyNetworkBeamState(true, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
        }

        _beamActive = true;
    }

    private void StopBeam()
    {
        if (!_beamActive || beamWeapon == null)
        {
            return;
        }

        if (NetTickUtil.IsActive && netEnemyCombat != null && netEnemyCombat.IsSpawned)
        {
            netEnemyCombat.SetBeamState(beamWeapon, false, transform.forward);
        }
        else
        {
            beamWeapon.ApplyNetworkBeamState(false, authoritative: true, PlayerCombatStats3D.InvalidAttackId);
        }

        _beamActive = false;
        _currentTarget = null;
    }

    private bool HasLineOfSight(Entity3D target, Vector3 origin, Vector3 fireDirection)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 normalizedDirection = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward;
        float rayDistance = Mathf.Max(0f, maxEngagementRange);
        if (rayDistance <= 0.0001f)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            normalizedDirection,
            LineOfSightHits,
            rayDistance,
            lineOfSightMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        Collider nearestCollider = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = LineOfSightHits[i];
            Collider collider = hit.collider;
            if (collider == null)
            {
                continue;
            }

            if (collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestCollider = collider;
            }
        }

        if (nearestCollider == null)
        {
            return true;
        }

        Entity3D hitEntity = nearestCollider.GetComponentInParent<Entity3D>();
        return hitEntity == target;
    }

    private float ResolveRetreatSpeedScale(float distanceToTarget)
    {
        float fullRetreatDistance = Mathf.Clamp(retreatFullSpeedDistance, 0f, keepAwayDistance);
        if (distanceToTarget <= fullRetreatDistance)
        {
            return 1f;
        }

        if (distanceToTarget >= keepAwayDistance)
        {
            return 0f;
        }

        return Mathf.InverseLerp(keepAwayDistance, fullRetreatDistance, distanceToTarget);
    }

    private static Vector3 ResolveTargetPoint(Entity3D target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null ? targetCollider.bounds.center : target.transform.position;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, keepAwayDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, optimalRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, optimalRange + rangeBuffer);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxEngagementRange);
    }
}
