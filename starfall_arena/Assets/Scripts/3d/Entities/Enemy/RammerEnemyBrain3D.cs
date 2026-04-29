using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class RammerEnemyBrain3D : MonoBehaviour
{
    private enum RammerState
    {
        Approach,
        WindUp,
        Charge,
        Recover
    }

    [Header("Rammer Enemy")]
    [Tooltip("The Enemy3D this brain belongs to. Auto-assigned from this GameObject if left empty. Used as the attacker reference when applying ram damage.")]
    [SerializeField] private Enemy3D enemy;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional patrol fallback used when no player-team target is inside detection range.")]
    [SerializeField] private EnemyPatrol3D patrol;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI steering decisions. Lower is more responsive but costs more CPU.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Approach")]
    [Tooltip("Distance (meters) where the rammer is close enough to start its charge wind-up.")]
    [SerializeField] private float chargeStartDistance = 28f;

    [Header("Charge")]
    [Tooltip("Telegraph duration (seconds) the rammer spends facing the target before locking its charge direction.")]
    [SerializeField] private float windUpDuration = 0.35f;

    [Tooltip("Maximum committed flight time (seconds) for one charge before the rammer gives up and recovers.")]
    [SerializeField] private float chargeMaxDuration = 1.4f;

    [Tooltip("Distance (meters) past the locked target point where the charge ends if it did not hit.")]
    [SerializeField] private float chargeOvershootDistance = 10f;

    [Tooltip("Cooldown (seconds) after a charge ends before the rammer may start another wind-up.")]
    [SerializeField] private float chargeCooldown = 1.25f;

    [Header("Ram Impact")]
    [Tooltip("Damage applied to the player on a successful ram hit. The knockback is the main threat, so this should usually stay modest.")]
    [SerializeField] private float ramDamage = 15f;

    [Tooltip("Velocity (m/s) added to the target in the locked charge direction. Routed through NetMovement3D.ApplyCombatVelocityDelta when present so the result participates in network reconciliation.")]
    [SerializeField] private float hitVelocity = 70f;

    [Tooltip("Optional small upward velocity (m/s) added to the ram hit. Keep at 0 unless the authored enemy needs a deliberate vertical pop.")]
    [SerializeField] private float hitUpwardVelocity = 0f;

    [Header("Recover")]
    [Tooltip("Seconds the rammer keeps flying past the target after a hit or miss before returning to approach.")]
    [SerializeField] private float recoverDuration = 0.75f;

    private NetworkObject _networkObject;
    private RammerState _state = RammerState.Approach;
    private float _nextThinkTime;
    private float _stateEndsAt;
    private float _nextChargeReadyAt;
    private Vector3 _lockedChargeDirection;
    private Vector3 _chargeStartPosition;
    private float _lockedTargetDistance;
    private bool _hitThisCharge;

    private void Awake()
    {
        enemy ??= GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        patrol ??= GetComponent<EnemyPatrol3D>() ?? gameObject.AddComponent<EnemyPatrol3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnEnable()
    {
        _state = RammerState.Approach;
        _nextChargeReadyAt = Time.time;
        _lockedChargeDirection = Vector3.zero;
        _hitThisCharge = false;
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        _state = RammerState.Approach;
        _lockedChargeDirection = Vector3.zero;
        _hitThisCharge = false;
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

    private void FixedUpdate()
    {
        if (!HasBrainAuthority())
        {
            return;
        }

        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        switch (_state)
        {
            case RammerState.Approach:
                TryStartWindUp(target);
                break;
            case RammerState.WindUp:
                if (target == null)
                {
                    EnterApproach();
                    break;
                }

                if (Time.time >= _stateEndsAt)
                {
                    BeginCharge(target);
                }
                break;
            case RammerState.Charge:
                if (Time.time >= _stateEndsAt || HasPassedLockedTargetPoint())
                {
                    BeginRecover();
                }
                break;
            case RammerState.Recover:
                if (Time.time >= _stateEndsAt)
                {
                    EnterApproach();
                }
                break;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!HasBrainAuthority() || _state != RammerState.Charge || _hitThisCharge)
        {
            return;
        }

        Entity3D hitEntity = ResolveEntity(collision.collider);
        if (IsTargetEntity(hitEntity))
        {
            ApplyRamHit(hitEntity);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasBrainAuthority() || _state != RammerState.Charge || _hitThisCharge)
        {
            return;
        }

        Entity3D hitEntity = ResolveEntity(other);
        if (IsTargetEntity(hitEntity))
        {
            ApplyRamHit(hitEntity);
        }
    }

    private void Think()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        switch (_state)
        {
            case RammerState.Approach:
                UpdateApproach(target);
                break;
            case RammerState.WindUp:
                UpdateWindUp(target);
                break;
            case RammerState.Charge:
            case RammerState.Recover:
                UpdateLockedChargeFlight();
                break;
        }
    }

    private void UpdateApproach(Entity3D target)
    {
        if (target == null)
        {
            PatrolOrClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        flightController?.SetMoveDirection(toTarget.normalized, 1f);
    }

    private void UpdateWindUp(Entity3D target)
    {
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

        flightController?.SetFacingDirection(toTarget.normalized);
    }

    private void UpdateLockedChargeFlight()
    {
        if (_lockedChargeDirection.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        flightController?.SetMoveDirection(_lockedChargeDirection, 1f);
    }

    private void PatrolOrClearFlightIntent()
    {
        if (patrol != null && patrol.isActiveAndEnabled && patrol.TryUpdatePatrolIntent())
        {
            return;
        }

        flightController?.ClearFlightIntent();
    }

    private void TryStartWindUp(Entity3D target)
    {
        if (target == null || Time.time < _nextChargeReadyAt)
        {
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.magnitude > Mathf.Max(0.01f, chargeStartDistance))
        {
            return;
        }

        _state = RammerState.WindUp;
        _stateEndsAt = Time.time + Mathf.Max(0f, windUpDuration);
        flightController?.SetFacingDirection(toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward);
    }

    private void BeginCharge(Entity3D target)
    {
        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            EnterApproach();
            return;
        }

        _lockedChargeDirection = toTarget.normalized;
        _chargeStartPosition = transform.position;
        _lockedTargetDistance = toTarget.magnitude;
        _hitThisCharge = false;
        _state = RammerState.Charge;
        _stateEndsAt = Time.time + Mathf.Max(0.1f, chargeMaxDuration);
        UpdateLockedChargeFlight();
    }

    private bool HasPassedLockedTargetPoint()
    {
        if (_lockedChargeDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 progress = transform.position - _chargeStartPosition;
        float distanceAlongCharge = Vector3.Dot(progress, _lockedChargeDirection);
        return distanceAlongCharge >= _lockedTargetDistance + Mathf.Max(0f, chargeOvershootDistance);
    }

    private void ApplyRamHit(Entity3D target)
    {
        if (target == null || enemy == null)
        {
            return;
        }

        _hitThisCharge = true;
        Vector3 hitVelocityDelta = _lockedChargeDirection.sqrMagnitude > 0.0001f
            ? _lockedChargeDirection.normalized * Mathf.Max(0f, hitVelocity)
            : transform.forward * Mathf.Max(0f, hitVelocity);

        if (hitUpwardVelocity > 0f)
        {
            hitVelocityDelta += Vector3.up * hitUpwardVelocity;
        }

        ApplyVelocityToTarget(target, hitVelocityDelta);
        target.TakeDamage(ramDamage, transform.position, enemy, DamageSource3D.Direct);
        BeginRecover();
    }

    private void BeginRecover()
    {
        _state = RammerState.Recover;
        _stateEndsAt = Time.time + Mathf.Max(0f, recoverDuration);
        _nextChargeReadyAt = Time.time + Mathf.Max(0f, chargeCooldown);
        UpdateLockedChargeFlight();
    }

    private void EnterApproach()
    {
        _state = RammerState.Approach;
        _lockedChargeDirection = Vector3.zero;
        _hitThisCharge = false;
    }

    private static void ApplyVelocityToTarget(Entity3D target, Vector3 velocityDelta)
    {
        if (velocityDelta.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        NetMovement3D netMovement = target.GetComponent<NetMovement3D>();
        if (netMovement != null)
        {
            netMovement.ApplyCombatVelocityDelta(velocityDelta);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity += velocityDelta;
        }
    }

    private static bool IsTargetEntity(Entity3D candidate)
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chargeStartDistance);

        if (Application.isPlaying && (_state == RammerState.Charge || _state == RammerState.Recover) && _lockedChargeDirection.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(_chargeStartPosition, _lockedChargeDirection * (_lockedTargetDistance + chargeOvershootDistance));
        }
    }
}
