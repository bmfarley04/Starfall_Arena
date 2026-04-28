using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum SwarmScoutMovementPattern
{
    OrbitHelix,
    FormationFlyby
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class SwarmScoutEnemyBrain3D : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;

    [Header("References")]
    [Tooltip("AI flight motor that drives the scout Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;
    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;
    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;
    [Tooltip("Optional inter-agent separation steering. Useful when many scouts are clustered tightly.")]
    [SerializeField] private EnemySeparation3D separation;
    [Tooltip("Optional spherecast obstacle avoidance. Leave empty for the cheapest swarm path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Header("Swarm Linking")]
    [Tooltip("Only scouts with the same key are considered part of the same organized swarm.")]
    [SerializeField] private string swarmKey = "SwarmScout";
    [Tooltip("Maximum distance used when auto-linking nearby scouts into one swarm.")]
    [SerializeField] private float autoLinkRadius = 80f;
    [Tooltip("Expected full swarm size. Used for stable phase spacing around the player.")]
    [SerializeField] private int intendedSwarmSize = 5;
    [Tooltip("Surviving linked scouts required before this swarm may alert other enemies.")]
    [SerializeField] private int requiredSurvivorsForAlert = 5;
    [Tooltip("Seconds between swarm discovery refreshes.")]
    [SerializeField] private float autoLinkRefreshInterval = 0.5f;

    [Header("Movement Mode")]
    [Tooltip("Seconds between AI steering decisions. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.04f;
    [Tooltip("Primary movement behavior. Formation Flyby sends the swarm through the player with a hole in the middle; Orbit Helix preserves the original circling fallback.")]
    [SerializeField] private SwarmScoutMovementPattern movementPattern = SwarmScoutMovementPattern.FormationFlyby;

    [Header("Orbit Movement")]
    [Tooltip("Preferred radius around the target while the scouts orbit.")]
    [SerializeField] private float orbitRadius = 65f;
    [Tooltip("Extra per-slot radius variation so the swarm reads as organized but not stacked.")]
    [SerializeField] private float orbitThickness = 12f;
    [Tooltip("How strongly scouts correct inward/outward when outside their assigned orbit band.")]
    [SerializeField] private float radialCorrectionWeight = 1.2f;
    [Tooltip("How strongly scouts bias along the tangent around the target.")]
    [SerializeField] private float tangentialWeight = 1.8f;
    [Tooltip("Vertical wave height used to make the swarm corkscrew through 3D space.")]
    [SerializeField] private float verticalAmplitude = 14f;
    [Tooltip("Vertical wave speed used by the orbit helix.")]
    [SerializeField] private float verticalFrequency = 1.1f;

    [Header("Formation Flyby")]
    [Tooltip("Radius of the polygon formation around its empty center. Larger values make the player pass through a wider hole.")]
    [SerializeField] private float formationRadius = 26f;
    [Tooltip("How far past the player the formation center tries to fly before beginning another run.")]
    [SerializeField] private float formationOvershootDistance = 180f;
    [Tooltip("How strongly each scout corrects toward its assigned polygon slot during a flyby.")]
    [SerializeField] private float formationSlotCorrectionWeight = 2.4f;
    [Tooltip("How strongly the whole formation keeps driving forward through the player.")]
    [SerializeField] private float formationForwardWeight = 2f;
    [Tooltip("Minimum distance from the player before the swarm can start a fresh run. Prevents tiny jittery pass resets inside the player pocket.")]
    [SerializeField] private float formationMinRunStartDistance = 70f;
    [Tooltip("Maximum seconds before a formation run is force-reset even if the swarm did not cleanly pass the player.")]
    [SerializeField] private float formationMaxRunDuration = 4.5f;
    [Tooltip("Degrees per second that the polygon slowly rolls while flying at the player.")]
    [SerializeField] private float formationRollDegreesPerSecond = 35f;

    [Header("Steering")]
    [Tooltip("If true, route orbit steering through the separation component when one is assigned.")]
    [SerializeField] private bool useSeparation = true;
    [Tooltip("If true, route orbit steering through the obstacle avoidance component when one is assigned.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    [Header("Alert")]
    [Tooltip("Distance from the player where the intact swarm can begin warming up an alert.")]
    [SerializeField] private float alertProbeRange = 450f;
    [Tooltip("Seconds the intact swarm must remain near the player before alerting other enemies.")]
    [SerializeField] private float alertWarmupSeconds = 3f;
    [Tooltip("Enemy sensors within this radius of the player receive the temporary target alert.")]
    [SerializeField] private float alertBroadcastRadius = 1400f;
    [Tooltip("How long alerted enemies remember the player if they do not naturally acquire them.")]
    [SerializeField] private float alertDuration = 6f;
    [Tooltip("Minimum seconds between repeated alert broadcasts from the same surviving swarm.")]
    [SerializeField] private float alertCooldown = 6f;

    private readonly List<SwarmScoutEnemyBrain3D> _linkedScouts = new List<SwarmScoutEnemyBrain3D>(8);

    private Enemy3D _enemy;
    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _nextAutoLinkTime;
    private float _nearTargetStartedAt = -1f;
    private float _nextAlertAllowedAt;
    private int _slotIndex;
    private Vector3 _formationRunDirection;
    private float _formationRunStartedAt;
    private bool _hasFormationRun;

    private bool IsAlive => _enemy != null && _enemy.CurrentHealth > 0f && gameObject.activeInHierarchy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnValidate()
    {
        thinkInterval = Mathf.Max(0.01f, thinkInterval);
        autoLinkRadius = Mathf.Max(0f, autoLinkRadius);
        intendedSwarmSize = Mathf.Max(1, intendedSwarmSize);
        requiredSurvivorsForAlert = Mathf.Max(1, requiredSurvivorsForAlert);
        autoLinkRefreshInterval = Mathf.Max(0.05f, autoLinkRefreshInterval);
        orbitRadius = Mathf.Max(0f, orbitRadius);
        orbitThickness = Mathf.Max(0f, orbitThickness);
        radialCorrectionWeight = Mathf.Max(0f, radialCorrectionWeight);
        tangentialWeight = Mathf.Max(0f, tangentialWeight);
        verticalAmplitude = Mathf.Max(0f, verticalAmplitude);
        verticalFrequency = Mathf.Max(0f, verticalFrequency);
        formationRadius = Mathf.Max(0f, formationRadius);
        formationOvershootDistance = Mathf.Max(0f, formationOvershootDistance);
        formationSlotCorrectionWeight = Mathf.Max(0f, formationSlotCorrectionWeight);
        formationForwardWeight = Mathf.Max(0f, formationForwardWeight);
        formationMinRunStartDistance = Mathf.Max(0f, formationMinRunStartDistance);
        formationMaxRunDuration = Mathf.Max(0.1f, formationMaxRunDuration);
        formationRollDegreesPerSecond = Mathf.Max(0f, formationRollDegreesPerSecond);
        alertProbeRange = Mathf.Max(0f, alertProbeRange);
        alertWarmupSeconds = Mathf.Max(0f, alertWarmupSeconds);
        alertBroadcastRadius = Mathf.Max(0f, alertBroadcastRadius);
        alertDuration = Mathf.Max(0f, alertDuration);
        alertCooldown = Mathf.Max(0f, alertCooldown);
    }

    private void OnEnable()
    {
        _nextThinkTime = 0f;
        _nextAutoLinkTime = 0f;
        _nearTargetStartedAt = -1f;
        _nextAlertAllowedAt = 0f;
        _hasFormationRun = false;
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        _linkedScouts.Clear();
        _nearTargetStartedAt = -1f;
        _hasFormationRun = false;
    }

    public void ApplyProfile(EnemyBalanceProfile3D.SwarmScoutBrainStats stats)
    {
        thinkInterval = Mathf.Max(0.01f, stats.thinkInterval);
        autoLinkRadius = Mathf.Max(0f, stats.autoLinkRadius);
        intendedSwarmSize = Mathf.Max(1, stats.intendedSwarmSize);
        requiredSurvivorsForAlert = Mathf.Max(1, stats.requiredSurvivorsForAlert);
        movementPattern = stats.movementPattern;
        orbitRadius = Mathf.Max(0f, stats.orbitRadius);
        orbitThickness = Mathf.Max(0f, stats.orbitThickness);
        radialCorrectionWeight = Mathf.Max(0f, stats.radialCorrectionWeight);
        tangentialWeight = Mathf.Max(0f, stats.tangentialWeight);
        verticalAmplitude = Mathf.Max(0f, stats.verticalAmplitude);
        verticalFrequency = Mathf.Max(0f, stats.verticalFrequency);
        formationRadius = Mathf.Max(0f, stats.formationRadius);
        formationOvershootDistance = Mathf.Max(0f, stats.formationOvershootDistance);
        formationSlotCorrectionWeight = Mathf.Max(0f, stats.formationSlotCorrectionWeight);
        formationForwardWeight = Mathf.Max(0f, stats.formationForwardWeight);
        formationMinRunStartDistance = Mathf.Max(0f, stats.formationMinRunStartDistance);
        formationMaxRunDuration = Mathf.Max(0.1f, stats.formationMaxRunDuration);
        formationRollDegreesPerSecond = Mathf.Max(0f, stats.formationRollDegreesPerSecond);
        alertProbeRange = Mathf.Max(0f, stats.alertProbeRange);
        alertWarmupSeconds = Mathf.Max(0f, stats.alertWarmupSeconds);
        alertBroadcastRadius = Mathf.Max(0f, stats.alertBroadcastRadius);
        alertDuration = Mathf.Max(0f, stats.alertDuration);
        alertCooldown = Mathf.Max(0f, stats.alertCooldown);
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
        RefreshSwarmIfNeeded();

        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            _nearTargetStartedAt = -1f;
            PatrolOrKeepMoving();
            return;
        }

        switch (movementPattern)
        {
            case SwarmScoutMovementPattern.OrbitHelix:
                FlyOrbit(target);
                break;
            case SwarmScoutMovementPattern.FormationFlyby:
            default:
                FlyFormationFlyby(target);
                break;
        }

        UpdateAlertWarmup(target);
    }

    private void RefreshSwarmIfNeeded()
    {
        if (Time.time < _nextAutoLinkTime)
        {
            return;
        }

        _nextAutoLinkTime = Time.time + Mathf.Max(0.05f, autoLinkRefreshInterval);
        _linkedScouts.Clear();

        SwarmScoutEnemyBrain3D[] scouts = FindObjectsByType<SwarmScoutEnemyBrain3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float linkRangeSqr = autoLinkRadius * autoLinkRadius;
        for (int i = 0; i < scouts.Length; i++)
        {
            SwarmScoutEnemyBrain3D scout = scouts[i];
            if (scout == null || !scout.IsAlive || scout.swarmKey != swarmKey)
            {
                continue;
            }

            if ((scout.transform.position - transform.position).sqrMagnitude > linkRangeSqr)
            {
                continue;
            }

            _linkedScouts.Add(scout);
        }

        _linkedScouts.Sort(CompareScouts);
        _slotIndex = Mathf.Max(0, _linkedScouts.IndexOf(this));
    }

    private static int CompareScouts(SwarmScoutEnemyBrain3D a, SwarmScoutEnemyBrain3D b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    private void FlyOrbit(Entity3D target)
    {
        Vector3 targetPosition = target.transform.position;
        Vector3 fromTarget = transform.position - targetPosition;
        Vector3 flatRadial = Vector3.ProjectOnPlane(fromTarget, Vector3.up);
        if (flatRadial.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            flatRadial = transform.right.sqrMagnitude > MinDirectionSqrMagnitude ? transform.right : Vector3.right;
        }

        flatRadial.Normalize();
        Vector3 tangent = Vector3.Cross(Vector3.up, flatRadial).normalized;
        int phaseCount = Mathf.Max(1, intendedSwarmSize);
        float phase01 = ((_slotIndex % phaseCount) + 0.5f) / phaseCount;
        float phaseRadians = phase01 * Mathf.PI * 2f;
        float radiusOffset = Mathf.Sin(phaseRadians) * orbitThickness;
        float desiredRadius = Mathf.Max(1f, orbitRadius + radiusOffset);
        float currentRadius = Vector3.ProjectOnPlane(fromTarget, Vector3.up).magnitude;
        Vector3 radialCorrection = flatRadial * Mathf.Clamp((desiredRadius - currentRadius) / Mathf.Max(1f, orbitRadius), -1f, 1f);
        float desiredY = targetPosition.y + Mathf.Sin(Time.time * verticalFrequency + phaseRadians) * verticalAmplitude;
        float yError = Mathf.Clamp((desiredY - transform.position.y) / Mathf.Max(1f, verticalAmplitude), -1f, 1f);

        Vector3 desired = tangent * tangentialWeight
            + radialCorrection * radialCorrectionWeight
            + Vector3.up * yError;
        desired = ResolveSteering(desired.sqrMagnitude > MinDirectionSqrMagnitude ? desired.normalized : tangent);
        flightController?.SetMoveDirection(desired, 1f);
    }

    private void FlyFormationFlyby(Entity3D target)
    {
        Vector3 targetPosition = target.transform.position;
        Vector3 swarmCenter = ResolveLinkedSwarmCenter();
        RefreshFormationRun(targetPosition, swarmCenter);

        Vector3 runDirection = _formationRunDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? _formationRunDirection.normalized
            : ResolveDirectionToTarget(targetPosition, swarmCenter);
        Vector3 right = Vector3.Cross(Vector3.up, runDirection);
        if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            right = Vector3.Cross(transform.up, runDirection);
        }

        right = right.sqrMagnitude > MinDirectionSqrMagnitude ? right.normalized : Vector3.right;
        Vector3 formationUp = Vector3.Cross(runDirection, right).normalized;
        int phaseCount = Mathf.Max(1, intendedSwarmSize);
        float slotRadians = ((_slotIndex % phaseCount) / (float)phaseCount) * Mathf.PI * 2f;
        slotRadians += Time.time * formationRollDegreesPerSecond * Mathf.Deg2Rad;

        Vector3 ringOffset = (right * Mathf.Cos(slotRadians) + formationUp * Mathf.Sin(slotRadians)) * formationRadius;
        Vector3 desiredFormationCenter = targetPosition + runDirection * formationOvershootDistance;
        Vector3 desiredSlot = desiredFormationCenter + ringOffset;
        Vector3 toSlot = desiredSlot - transform.position;
        Vector3 slotCorrection = toSlot.sqrMagnitude > MinDirectionSqrMagnitude ? toSlot.normalized : Vector3.zero;
        Vector3 desired = runDirection * formationForwardWeight + slotCorrection * formationSlotCorrectionWeight;

        desired = ResolveSteering(desired.sqrMagnitude > MinDirectionSqrMagnitude ? desired.normalized : runDirection);
        flightController?.SetMoveDirection(desired, 1f);
    }

    private void RefreshFormationRun(Vector3 targetPosition, Vector3 swarmCenter)
    {
        if (!_hasFormationRun)
        {
            BeginFormationRun(targetPosition, swarmCenter);
            return;
        }

        float runProgress = Vector3.Dot(swarmCenter - targetPosition, _formationRunDirection);
        bool passedTarget = runProgress >= formationOvershootDistance * 0.75f;
        bool timedOut = Time.time - _formationRunStartedAt >= formationMaxRunDuration;
        if (!passedTarget && !timedOut)
        {
            return;
        }

        float distanceToTarget = Vector3.Distance(swarmCenter, targetPosition);
        if (distanceToTarget >= formationMinRunStartDistance || timedOut)
        {
            BeginFormationRun(targetPosition, swarmCenter);
        }
    }

    private void BeginFormationRun(Vector3 targetPosition, Vector3 swarmCenter)
    {
        _formationRunDirection = ResolveDirectionToTarget(targetPosition, swarmCenter);
        _formationRunStartedAt = Time.time;
        _hasFormationRun = true;
    }

    private Vector3 ResolveDirectionToTarget(Vector3 targetPosition, Vector3 swarmCenter)
    {
        Vector3 toTarget = targetPosition - swarmCenter;
        if (toTarget.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return toTarget.normalized;
        }

        return transform.forward.sqrMagnitude > MinDirectionSqrMagnitude ? transform.forward.normalized : Vector3.forward;
    }

    private Vector3 ResolveLinkedSwarmCenter()
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _linkedScouts.Count; i++)
        {
            SwarmScoutEnemyBrain3D scout = _linkedScouts[i];
            if (scout == null || !scout.IsAlive)
            {
                continue;
            }

            center += scout.transform.position;
            count++;
        }

        return count > 0 ? center / count : transform.position;
    }

    private void UpdateAlertWarmup(Entity3D target)
    {
        int survivors = CountLivingLinkedScouts();
        bool intactEnough = survivors >= Mathf.Max(1, requiredSurvivorsForAlert);
        bool swarmNearTarget = AreRequiredSurvivorsNearTarget(target);
        if (!intactEnough || !swarmNearTarget)
        {
            _nearTargetStartedAt = -1f;
            return;
        }

        if (_nearTargetStartedAt < 0f)
        {
            _nearTargetStartedAt = Time.time;
            return;
        }

        if (Time.time - _nearTargetStartedAt < alertWarmupSeconds || Time.time < _nextAlertAllowedAt)
        {
            return;
        }

        if (IsAlertCoordinator())
        {
            BroadcastTargetAlert(target);
        }

        _nextAlertAllowedAt = Time.time + Mathf.Max(0f, alertCooldown);
    }

    private int CountLivingLinkedScouts()
    {
        int count = 0;
        for (int i = 0; i < _linkedScouts.Count; i++)
        {
            if (_linkedScouts[i] != null && _linkedScouts[i].IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private bool AreRequiredSurvivorsNearTarget(Entity3D target)
    {
        if (target == null)
        {
            return false;
        }

        int nearbySurvivors = 0;
        float probeRangeSqr = alertProbeRange * alertProbeRange;
        Vector3 targetPosition = target.transform.position;
        for (int i = 0; i < _linkedScouts.Count; i++)
        {
            SwarmScoutEnemyBrain3D scout = _linkedScouts[i];
            if (scout == null || !scout.IsAlive)
            {
                continue;
            }

            if ((scout.transform.position - targetPosition).sqrMagnitude <= probeRangeSqr)
            {
                nearbySurvivors++;
            }
        }

        return nearbySurvivors >= Mathf.Max(1, requiredSurvivorsForAlert);
    }

    private bool IsAlertCoordinator()
    {
        for (int i = 0; i < _linkedScouts.Count; i++)
        {
            SwarmScoutEnemyBrain3D scout = _linkedScouts[i];
            if (scout != null && scout.IsAlive)
            {
                return scout == this;
            }
        }

        return true;
    }

    private void BroadcastTargetAlert(Entity3D target)
    {
        if (target == null || alertDuration <= 0f)
        {
            return;
        }

        EnemyTargetSensor3D[] sensors = FindObjectsByType<EnemyTargetSensor3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float broadcastRangeSqr = alertBroadcastRadius * alertBroadcastRadius;
        Vector3 targetPosition = target.transform.position;
        for (int i = 0; i < sensors.Length; i++)
        {
            EnemyTargetSensor3D sensor = sensors[i];
            if (sensor == null || sensor == targetSensor)
            {
                continue;
            }

            if ((sensor.transform.position - targetPosition).sqrMagnitude > broadcastRangeSqr)
            {
                continue;
            }

            sensor.ReceiveTargetAlert(target, alertDuration);
        }
    }

    private Vector3 ResolveSteering(Vector3 desiredDirection)
    {
        Vector3 steeringDirection = desiredDirection;
        if (useSeparation && separation != null && separation.isActiveAndEnabled)
        {
            steeringDirection = separation.ResolveSteeringDirection(steeringDirection);
        }

        if (useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled)
        {
            steeringDirection = obstacleAvoidance.ResolveSteeringDirection(steeringDirection);
        }

        return steeringDirection.sqrMagnitude > MinDirectionSqrMagnitude ? steeringDirection.normalized : desiredDirection;
    }

    private void PatrolOrKeepMoving()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        Vector3 fallback = flightController != null && flightController.MoveDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? flightController.MoveDirection
            : transform.forward;
        flightController?.SetMoveDirection(fallback.sqrMagnitude > MinDirectionSqrMagnitude ? fallback.normalized : Vector3.forward, 1f);
    }

    private bool HasBrainAuthority()
    {
        return !NetTickUtil.IsActive
            || _networkObject == null
            || !_networkObject.IsSpawned
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }
}
