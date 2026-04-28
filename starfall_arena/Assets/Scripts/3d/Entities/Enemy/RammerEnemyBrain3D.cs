using System.Collections.Generic;
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
        Stalk,
        WindUp,
        Charge,
        Disengage
    }

    [Header("Rammer Enemy")]
    [Tooltip("The Enemy3D this brain belongs to. Auto-assigned from this GameObject if left empty. Used as the attacker reference when applying ram damage.")]
    [SerializeField] private Enemy3D enemy;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional inter-agent separation steering. Leave empty (or disable useSeparation) to skip. Recommended for swarms so multiple rammers fan out instead of stacking on the same vector.")]
    [SerializeField] private EnemySeparation3D separation;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI steering decisions. Lower is more responsive but costs more CPU. Note: contact detection runs every FixedUpdate independently of this so high-closure-rate approaches do not skip the ram trigger.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Stalk Behavior")]
    [Tooltip("Preferred standoff distance (meters) during stalking. The rammer closes at full speed when farther than this and slows to stalkSpeedScale when within this range, hovering near the player while waiting for the next charge to be ready.")]
    [SerializeField] private float stalkDistance = 30f;

    [Tooltip("Speed scale (0-1) used when within stalkDistance. A value below 1 makes the rammer drift instead of barreling in, so the player can read that it is winding up between charges.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float stalkSpeedScale = 0.55f;

    [Header("Charge Behavior")]
    [Tooltip("Distance (meters) at which the rammer commits to a charge. Must be smaller than stalkDistance for the windup-then-charge cadence to read clearly.")]
    [SerializeField] private float chargeStartDistance = 22f;

    [Tooltip("Telegraph duration (seconds) the rammer spends locking onto the player before launching a charge. Gives the player a window to react. Set to 0 to launch instantly (not recommended - removes the read).")]
    [SerializeField] private float windUpDuration = 0.4f;

    [Tooltip("Maximum committed flight time (seconds) for one charge before the rammer gives up and disengages without a hit. Sized to cover the chargeStartDistance plus chargeOvershootDistance at moveSpeed.")]
    [SerializeField] private float chargeMaxDuration = 1.5f;

    [Tooltip("Distance (meters) past the wind-up-locked vector's predicted contact point at which the rammer auto-ends the charge. Ensures the rammer doesn't fly arbitrarily far when it whiffs.")]
    [SerializeField] private float chargeOvershootDistance = 8f;

    [Tooltip("Cooldown (seconds) after a charge ends (hit OR miss) before the rammer can wind up another charge. Forces visible breathing room between attacks.")]
    [SerializeField] private float chargeCooldown = 1.5f;

    [Header("Ram Impact")]
    [Tooltip("Layers searched by ram contact detection. Should include the player ship's collider layer. Leave at Everything (-1) for backward-compatible behavior, but a narrow mask is more reliable in cluttered scenes (the OverlapSphere buffer cannot be filled with irrelevant colliders).")]
    [SerializeField] private LayerMask contactDetectionMask = ~0;

    [Tooltip("Distance (meters) at which a player-team entity in front of/around the rammer counts as a contact and triggers a ram hit. IMPORTANT: this should be larger than (rammer collider radius + player collider radius + ~0.5m safety) so the hit fires before the rammer's geometry embeds in the player's. The detection sweep also extends along the rammer's actual movement vector each FixedUpdate to catch tunneling.")]
    [SerializeField] private float ramDetectionDistance = 3f;

    [Tooltip("Damage applied to the player on a successful ram hit. The knockback is the threat - keep this small.")]
    [SerializeField] private float ramDamage = 15f;

    [Tooltip("Velocity (m/s) added to the player's existing motion in the away-from-rammer direction on hit. Routed through NetMovement3D.ApplyCombatVelocityDelta so the impulse replicates correctly across the network. Default sized to feel like 'sent reeling' on a charge connect.")]
    [SerializeField] private float knockbackVelocity = 60f;

    [Tooltip("Optional small upward component (m/s) added on top of the away-direction knockback to give the hit a vertical jolt feel. Default 0 - 3D space combat reads weird with arbitrary up impulses.")]
    [SerializeField] private float knockbackUpwardBias = 0f;

    [Header("Disengage")]
    [Tooltip("Seconds the rammer steers away from the target after a charge ends (hit or miss) before re-engaging. Includes the eject window below.")]
    [SerializeField] private float disengageDuration = 1.25f;

    [Tooltip("Distance (meters) the rammer must reach during disengage before it is allowed to re-engage early. Whichever happens first - this distance or disengageDuration - ends the disengage state.")]
    [SerializeField] private float disengageDistance = 30f;

    [Tooltip("Reverse-thrust window (seconds) at the start of a HIT-disengage. The rammer keeps its nose pointed at the target but is physically pulled backward at moveSpeed, so it never freezes in place inside the player's collider while rotating around. Misses skip the eject and go straight to forward disengage.")]
    [SerializeField] private float ejectDuration = 0.35f;

    [Tooltip("If true, the rammer's own colliders are temporarily exempted from colliding with the rammed target's colliders for the full disengage duration after a HIT. Guarantees no physical entanglement with the player after impact even in pathological cases. Re-enabled when disengage ends or the rammer is disabled.")]
    [SerializeField] private bool useCollisionExemption = true;

    [Header("Steering Composition")]
    [Tooltip("If true, route stalk and post-eject disengage steering through the obstacle avoidance component when one is assigned. Charge steering ignores this flag on purpose - the locked charge vector should not be perturbed.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    [Tooltip("If true, route stalk and post-eject disengage steering through the separation component when one is assigned. Charge steering ignores this flag on purpose - the locked charge vector should not be perturbed.")]
    [SerializeField] private bool useSeparation = true;

    private readonly Collider[] _overlapResults = new Collider[16];
    private readonly RaycastHit[] _sweepResults = new RaycastHit[16];
    private readonly List<(Collider self, Collider other)> _ignoredPairs = new();

    private NetworkObject _networkObject;
    private RammerState _state = RammerState.Stalk;
    private float _nextThinkTime;
    private float _stateEndsAt;
    private float _ejectEndsAt;
    private float _nextChargeReadyAt;
    private bool _isEjecting;
    private Vector3 _chargeDirection;
    private Vector3 _chargeStartPosition;
    private float _chargeTargetDistanceAtStart;
    private Vector3 _previousFixedUpdatePosition;

    private void Awake()
    {
        enemy ??= GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        _networkObject = GetComponent<NetworkObject>();
        _previousFixedUpdatePosition = transform.position;
    }

    private void OnEnable()
    {
        _previousFixedUpdatePosition = transform.position;
        _state = RammerState.Stalk;
        _isEjecting = false;
        _nextChargeReadyAt = Time.time;
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        EndCollisionExemption();
        _state = RammerState.Stalk;
        _isEjecting = false;
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
        ThinkSteering();
    }

    private void FixedUpdate()
    {
        if (!HasBrainAuthority())
        {
            _previousFixedUpdatePosition = transform.position;
            return;
        }

        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;

        // Contact detection runs every physics tick, independent of the steering think
        // interval, and uses a swept SphereCast from the previous fixed-update position to
        // the current one PLUS an OverlapSphere fallback so even high-closure-rate
        // approaches and off-center colliders cannot skip past the ram trigger. We only
        // run it in states where a hit is meaningful - WindUp is a stationary telegraph
        // and Disengage is collision-exempted by design.
        bool wantsHitCheck = target != null && (_state == RammerState.Stalk || _state == RammerState.Charge);
        if (wantsHitCheck && RunRamHitCheck(target))
        {
            _previousFixedUpdatePosition = transform.position;
            return;
        }

        switch (_state)
        {
            case RammerState.Stalk:
                MaybeStartWindUp(target);
                break;
            case RammerState.WindUp:
                if (target == null)
                {
                    _state = RammerState.Stalk;
                    break;
                }
                if (Time.time >= _stateEndsAt)
                {
                    BeginCharge(target);
                }
                break;
            case RammerState.Charge:
                if (Time.time >= _stateEndsAt || HasOvershotChargeTarget())
                {
                    EndChargeMiss();
                }
                break;
            case RammerState.Disengage:
                if (Time.time >= _stateEndsAt || HasReachedDisengageDistance(target))
                {
                    EndDisengage();
                }
                break;
        }

        _previousFixedUpdatePosition = transform.position;
    }

    // ------------ Steering (think tick) ------------

    private void ThinkSteering()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;

        switch (_state)
        {
            case RammerState.Stalk:
                UpdateStalkSteering(target);
                break;
            case RammerState.WindUp:
                UpdateWindUpSteering(target);
                break;
            case RammerState.Charge:
                UpdateChargeSteering();
                break;
            case RammerState.Disengage:
                UpdateDisengageSteering(target);
                break;
        }
    }

    private void UpdateStalkSteering(Entity3D target)
    {
        if (target == null)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 desired = toTarget / distance;
        Vector3 steered = ResolveSteering(desired);
        // Close at full speed when far; drift at stalkSpeedScale when within stalkDistance
        // so the player has visible breathing room between charges.
        float speedScale = distance > stalkDistance ? 1f : Mathf.Clamp01(stalkSpeedScale);
        flightController?.SetMoveDirection(steered, speedScale);
    }

    private void UpdateWindUpSteering(Entity3D target)
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

        // Lock onto the target's current direction without committing to translation yet.
        // Pass a tiny speedScale so the rammer creeps forward while aiming - looks alive
        // without closing distance fast enough to make the wind-up window meaningless.
        Vector3 desired = toTarget.normalized;
        flightController?.SetFlightIntent(desired, desired, 0.05f, moveBackward: false);
    }

    private void UpdateChargeSteering()
    {
        if (_chargeDirection.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        // CRITICAL: the charge vector is locked at wind-up end. Do NOT re-target the
        // player here - that would let the rammer track the player mid-charge and remove
        // the dodge window. Also do NOT route through separation/obstacle avoidance: any
        // sideways drift would re-open the flight controller's facing-vs-move angle gate
        // and zero the velocity (the same freeze-bug documented in 3D_BUGS.md).
        flightController?.SetMoveDirection(_chargeDirection, 1f);
    }

    private void UpdateDisengageSteering(Entity3D target)
    {
        // Eject phase: reverse-thrust away while keeping nose pointed at target. The
        // controller pulls the rigidbody along -forward at full moveSpeed from frame 1
        // (no rotation needed -> no velocity-zero freeze).
        if (_isEjecting && Time.time < _ejectEndsAt)
        {
            Vector3 awayFromTarget;
            Vector3 facingTowardTarget;
            if (target != null)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    facingTowardTarget = toTarget.normalized;
                    awayFromTarget = -facingTowardTarget;
                    flightController?.SetFlightIntent(awayFromTarget, facingTowardTarget, 1f, moveBackward: true);
                    return;
                }
            }

            // Target lost mid-eject: fall back to the locked charge direction's reverse.
            if (_chargeDirection.sqrMagnitude > 0.0001f)
            {
                facingTowardTarget = _chargeDirection;
                awayFromTarget = -_chargeDirection;
                flightController?.SetFlightIntent(awayFromTarget, facingTowardTarget, 1f, moveBackward: true);
                return;
            }
        }

        if (_isEjecting)
        {
            _isEjecting = false;
        }

        // Forward disengage: face away and fly away. Now we are clear of the player's
        // geometry, so separation/obstacle avoidance can drift the move direction safely.
        Vector3 awayDirection;
        if (target != null)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            awayDirection = toTarget.sqrMagnitude > 0.0001f ? -toTarget.normalized : -_chargeDirection;
        }
        else
        {
            awayDirection = _chargeDirection.sqrMagnitude > 0.0001f ? -_chargeDirection : transform.forward;
        }

        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 steered = ResolveSteering(awayDirection);
        flightController?.SetMoveDirection(steered, 1f);
    }

    private Vector3 ResolveSteering(Vector3 desiredDirection)
    {
        Vector3 result = desiredDirection;

        if (useSeparation && separation != null && separation.isActiveAndEnabled)
        {
            result = separation.ResolveSteeringDirection(result);
        }

        if (useObstacleAvoidance && obstacleAvoidance != null && obstacleAvoidance.isActiveAndEnabled)
        {
            result = obstacleAvoidance.ResolveSteeringDirection(result);
        }

        return result;
    }

    // ------------ State transitions ------------

    private void MaybeStartWindUp(Entity3D target)
    {
        if (target == null)
        {
            return;
        }

        if (Time.time < _nextChargeReadyAt)
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
    }

    private void BeginCharge(Entity3D target)
    {
        if (target == null)
        {
            _state = RammerState.Stalk;
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            _state = RammerState.Stalk;
            return;
        }

        _chargeDirection = toTarget.normalized;
        _chargeStartPosition = transform.position;
        _chargeTargetDistanceAtStart = toTarget.magnitude;
        _state = RammerState.Charge;
        _stateEndsAt = Time.time + Mathf.Max(0.1f, chargeMaxDuration);
    }

    private bool HasOvershotChargeTarget()
    {
        if (_chargeDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        // Distance traveled along the locked charge vector since BeginCharge.
        Vector3 progress = transform.position - _chargeStartPosition;
        float distanceAlongCharge = Vector3.Dot(progress, _chargeDirection);
        float overshootThreshold = _chargeTargetDistanceAtStart + Mathf.Max(0f, chargeOvershootDistance);
        return distanceAlongCharge >= overshootThreshold;
    }

    private void EndChargeMiss()
    {
        // Miss: short forward disengage to swing around for the next pass. No eject (we
        // never made contact, so there is no entanglement risk) and no collision
        // exemption (no specific target to exempt against).
        _state = RammerState.Disengage;
        _isEjecting = false;
        _stateEndsAt = Time.time + Mathf.Max(0f, disengageDuration);
        _nextChargeReadyAt = Time.time + Mathf.Max(0f, chargeCooldown);
    }

    private void EndDisengage()
    {
        _state = RammerState.Stalk;
        _isEjecting = false;
        EndCollisionExemption();
    }

    private bool HasReachedDisengageDistance(Entity3D target)
    {
        if (target == null)
        {
            return true;
        }

        return Vector3.Distance(transform.position, target.transform.position) >= Mathf.Max(0f, disengageDistance);
    }

    // ------------ Hit detection ------------

    private bool RunRamHitCheck(Entity3D target)
    {
        Vector3 currentPos = transform.position;
        float radius = Mathf.Max(0.01f, ramDetectionDistance);

        // 1. Cheap distance check against the resolved target's transform. Catches the
        //    common case where the player ship's pivot is near the visible center of mass.
        Vector3 toTarget = target.transform.position - currentPos;
        float distance = toTarget.magnitude;
        if (distance <= radius)
        {
            Vector3 dir = distance > 0.0001f ? toTarget / distance : transform.forward;
            ApplyRamHit(target, dir);
            return true;
        }

        // 2. OverlapSphere on the contact mask. Catches off-center colliders the
        //    transform-distance check missed (compound colliders, child collider on the
        //    ship hull, etc.). LayerMask filtering means the buffer cannot be wasted on
        //    irrelevant environment colliders.
        if (TryAcquireContactMasked(out Entity3D contactEntity))
        {
            Vector3 contactDir = (contactEntity.transform.position - currentPos);
            contactDir = contactDir.sqrMagnitude > 0.0001f ? contactDir.normalized : transform.forward;
            ApplyRamHit(contactEntity, contactDir);
            return true;
        }

        // 3. Swept SphereCast from the previous fixed-update position to the current one.
        //    Catches tunneling: at high closure rates, the rammer may pass the player's
        //    collider entirely between physics ticks. The cast has the contact radius and
        //    spans the actual movement this tick, so anything we crossed gets hit.
        Vector3 movement = currentPos - _previousFixedUpdatePosition;
        float movedDistance = movement.magnitude;
        if (movedDistance > 0.0001f)
        {
            Vector3 moveDir = movement / movedDistance;
            int hits = Physics.SphereCastNonAlloc(
                _previousFixedUpdatePosition,
                radius,
                moveDir,
                _sweepResults,
                movedDistance,
                contactDetectionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _sweepResults[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                Entity3D candidate = ResolveEntity(hitCollider);
                if (!IsTargetEntity(candidate))
                {
                    continue;
                }

                Vector3 contactDir = (candidate.transform.position - currentPos);
                contactDir = contactDir.sqrMagnitude > 0.0001f ? contactDir.normalized : moveDir;
                ApplyRamHit(candidate, contactDir);
                return true;
            }
        }

        return false;
    }

    private bool TryAcquireContactMasked(out Entity3D contactEntity)
    {
        contactEntity = null;
        float radius = Mathf.Max(0.01f, ramDetectionDistance);
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            _overlapResults,
            contactDetectionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider c = _overlapResults[i];
            if (c == null || c.transform.IsChildOf(transform))
            {
                continue;
            }

            Entity3D candidate = ResolveEntity(c);
            if (IsTargetEntity(candidate))
            {
                contactEntity = candidate;
                return true;
            }
        }

        return false;
    }

    private void ApplyRamHit(Entity3D target, Vector3 toTargetDirection)
    {
        if (target == null || enemy == null)
        {
            return;
        }

        Vector3 knockDir = toTargetDirection;
        if (knockDir.sqrMagnitude <= 0.0001f)
        {
            Vector3 fallback = target.transform.position - transform.position;
            knockDir = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : transform.forward;
        }

        Vector3 knockback = knockDir * Mathf.Max(0f, knockbackVelocity);
        if (knockbackUpwardBias > 0f)
        {
            knockback += Vector3.up * knockbackUpwardBias;
        }

        ApplyKnockbackToTarget(target, knockback);
        target.TakeDamage(ramDamage, transform.position, enemy, DamageSource3D.Direct);

        BeginCollisionExemption(target);

        // Hit-disengage: eject + collision exemption. The eject reverses the rammer at
        // full moveSpeed for ejectDuration without rotating, so the rammer's geometry
        // pulls clear of the player even when the controller would otherwise zero
        // velocity during the disengage rotation.
        _state = RammerState.Disengage;
        _isEjecting = ejectDuration > 0f;
        _ejectEndsAt = Time.time + Mathf.Max(0f, ejectDuration);
        _stateEndsAt = Time.time + Mathf.Max(0f, disengageDuration);
        _nextChargeReadyAt = Time.time + Mathf.Max(0f, chargeCooldown);

        // Drive reverse-thrust intent immediately so the rigidbody starts retreating on
        // the next FixedUpdate even if the steering think tick hasn't fired yet.
        if (_isEjecting && flightController != null)
        {
            Vector3 awayFromTarget = -knockDir;
            flightController.SetFlightIntent(awayFromTarget, knockDir, 1f, moveBackward: true);
        }
    }

    private void BeginCollisionExemption(Entity3D target)
    {
        if (!useCollisionExemption || target == null)
        {
            return;
        }

        // Stale pairs from a previous (interrupted) disengage should never persist into a
        // new one - belt-and-suspenders cleanup before adding new pairs.
        EndCollisionExemption();

        Collider[] selfColliders = GetComponentsInChildren<Collider>(includeInactive: false);
        Collider[] otherColliders = target.GetComponentsInChildren<Collider>(includeInactive: false);
        if (selfColliders == null || otherColliders == null)
        {
            return;
        }

        for (int i = 0; i < selfColliders.Length; i++)
        {
            Collider selfCol = selfColliders[i];
            if (selfCol == null || selfCol.isTrigger)
            {
                continue;
            }

            for (int j = 0; j < otherColliders.Length; j++)
            {
                Collider otherCol = otherColliders[j];
                if (otherCol == null || otherCol == selfCol || otherCol.isTrigger)
                {
                    continue;
                }

                if (otherCol.transform.IsChildOf(transform))
                {
                    continue;
                }

                Physics.IgnoreCollision(selfCol, otherCol, true);
                _ignoredPairs.Add((selfCol, otherCol));
            }
        }
    }

    private void EndCollisionExemption()
    {
        if (_ignoredPairs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _ignoredPairs.Count; i++)
        {
            var pair = _ignoredPairs[i];
            if (pair.self == null || pair.other == null)
            {
                continue;
            }

            Physics.IgnoreCollision(pair.self, pair.other, false);
        }

        _ignoredPairs.Clear();
    }

    // ------------ Helpers ------------

    private static void ApplyKnockbackToTarget(Entity3D target, Vector3 knockback)
    {
        if (knockback.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        NetMovement3D netMovement = target.GetComponent<NetMovement3D>();
        if (netMovement != null)
        {
            netMovement.ApplyCombatVelocityDelta(knockback);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity += knockback;
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
        // Red = ram contact radius, Cyan = disengage break-off distance.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, ramDetectionDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, disengageDistance);

        // Yellow = charge engagement distance, white = stalk standoff distance.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chargeStartDistance);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, stalkDistance);

        // Show the locked charge vector while charging, for in-editor sanity.
        if (Application.isPlaying && _state == RammerState.Charge && _chargeDirection.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(_chargeStartPosition, _chargeDirection * (_chargeTargetDistanceAtStart + chargeOvershootDistance));
        }
    }
}
