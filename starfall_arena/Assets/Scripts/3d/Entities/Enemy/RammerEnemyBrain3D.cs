using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
[RequireComponent(typeof(EnemyAIFlightController3D))]
[RequireComponent(typeof(EnemyTargetSensor3D))]
public class RammerEnemyBrain3D : MonoBehaviour
{
    [Header("Rammer Enemy")]
    [Tooltip("The Enemy3D this brain belongs to. Auto-assigned from this GameObject if left empty. Used as the attacker reference when applying ram damage.")]
    [SerializeField] private Enemy3D enemy;

    [Tooltip("AI flight motor that drives the Rigidbody. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyAIFlightController3D flightController;

    [Tooltip("Faction-aware target sensor. Auto-assigned from this GameObject if left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional spherecast obstacle avoidance. Leave empty (or disable useObstacleAvoidance) for the cheapest path.")]
    [SerializeField] private EnemyObstacleAvoidance3D obstacleAvoidance;

    [Tooltip("Optional inter-agent separation steering. Leave empty (or disable useSeparation) to skip. Recommended for any enemy that swarms a single target so multiple rammers fan out instead of stacking on the same vector.")]
    [SerializeField] private EnemySeparation3D separation;

    [Header("Think Loop")]
    [Tooltip("Seconds between AI steering decisions. Lower is more responsive but costs more CPU. Note: contact detection runs every FixedUpdate independently of this so high-closure-rate approaches do not skip the ram trigger.")]
    [SerializeField] private float thinkInterval = 0.05f;

    [Header("Ram Impact")]
    [Tooltip("Distance (meters) at which a player-team entity in front of/around the rammer counts as a contact and triggers a ram hit. IMPORTANT: this should be larger than (rammer collider radius + player collider radius + ~0.5m safety) so the hit fires before the rammer's geometry embeds in the player's. Mirrors the suicide drone's contact distance pattern.")]
    [SerializeField] private float ramDetectionDistance = 2.5f;

    [Tooltip("Damage applied to the player on a successful ram hit. The knockback is the threat - keep this small.")]
    [SerializeField] private float ramDamage = 15f;

    [Tooltip("Velocity (m/s) added to the player's existing motion in the away-from-rammer direction on hit. Routed through NetMovement3D.ApplyCombatVelocityDelta so the impulse replicates correctly across the network.")]
    [SerializeField] private float knockbackVelocity = 25f;

    [Tooltip("Optional small upward component (m/s) added on top of the away-direction knockback to give the hit a vertical jolt feel. Default 0 - 3D space combat reads weird with arbitrary up impulses.")]
    [SerializeField] private float knockbackUpwardBias = 0f;

    [Header("Disengage")]
    [Tooltip("Seconds the rammer steers away from the target after a successful ram hit before re-engaging. Lets it visibly arc out and turn around for another pass instead of grinding on the player's hull. Includes the eject window below.")]
    [SerializeField] private float disengageDuration = 1.25f;

    [Tooltip("Distance (meters) the rammer must reach during disengage before it is allowed to re-engage early. Whichever happens first - this distance or disengageDuration - ends the disengage state.")]
    [SerializeField] private float disengageDistance = 30f;

    [Tooltip("Reverse-thrust window (seconds) at the start of disengage. The rammer keeps its nose pointed at the target but is physically pulled backward at moveSpeed, so it never freezes in place inside the player's collider while rotating around. After this window the rammer transitions to the normal face-away-and-fly disengage.")]
    [SerializeField] private float ejectDuration = 0.35f;

    [Tooltip("If true, the rammer's own colliders are temporarily exempted from colliding with the rammed target's colliders for the full disengage duration. Guarantees no physical entanglement with the player after impact even in pathological cases (player charging into the rammer, multiple rammers piling in, etc.). Re-enabled when disengage ends or the rammer is disabled.")]
    [SerializeField] private bool useCollisionExemption = true;

    [Tooltip("If true, route steering through the obstacle avoidance component when one is assigned. If false or no avoidance component exists, the rammer steers straight at/away from the target.")]
    [SerializeField] private bool useObstacleAvoidance = true;

    [Tooltip("If true, route steering through the separation component when one is assigned. If false or no separation component exists, the rammer steers without inter-agent fan-out.")]
    [SerializeField] private bool useSeparation = true;

    private readonly Collider[] _overlapResults = new Collider[8];
    private readonly List<(Collider self, Collider other)> _ignoredPairs = new();

    private NetworkObject _networkObject;
    private float _nextThinkTime;
    private float _disengageEndsAt;
    private float _ejectEndsAt;
    private bool _isDisengaging;
    private bool _isEjecting;

    private void Awake()
    {
        enemy ??= GetComponent<Enemy3D>();
        flightController ??= GetComponent<EnemyAIFlightController3D>();
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        obstacleAvoidance ??= GetComponent<EnemyObstacleAvoidance3D>();
        separation ??= GetComponent<EnemySeparation3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        flightController?.ClearFlightIntent();
        EndCollisionExemption();
        _isDisengaging = false;
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
            return;
        }

        // Contact detection runs every physics tick (independently of the steering think
        // interval) so a high-closure-rate approach cannot skip past ramDetectionDistance
        // between think ticks and end up embedded in the player's collider.
        if (_isDisengaging)
        {
            return;
        }

        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            return;
        }

        Vector3 toTargetDirection = toTarget / distanceToTarget;

        if (distanceToTarget <= Mathf.Max(0.01f, ramDetectionDistance))
        {
            ApplyRamHit(target, toTargetDirection);
            return;
        }

        if (TryAcquireContact(out Entity3D contactEntity))
        {
            Vector3 awayDirection = ResolveAwayDirection(contactEntity);
            ApplyRamHit(contactEntity, -awayDirection);
        }
    }

    private void ThinkSteering()
    {
        Entity3D target = targetSensor != null ? targetSensor.GetTarget() : null;
        if (target == null)
        {
            flightController?.ClearFlightIntent();
            EndDisengage();
            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.0001f)
        {
            flightController?.ClearFlightIntent();
            return;
        }

        Vector3 toTargetDirection = toTarget / distanceToTarget;

        if (_isDisengaging)
        {
            UpdateDisengage(toTargetDirection, distanceToTarget);
            return;
        }

        UpdatePursuit(toTargetDirection);
    }

    private void UpdatePursuit(Vector3 toTargetDirection)
    {
        Vector3 steeringDirection = ResolveSteering(toTargetDirection);
        flightController?.SetMoveDirection(steeringDirection, 1f);
    }

    private void UpdateDisengage(Vector3 toTargetDirection, float distanceToTarget)
    {
        if (Time.time >= _disengageEndsAt || distanceToTarget >= Mathf.Max(0f, disengageDistance))
        {
            EndDisengage();
            return;
        }

        Vector3 awayDirection = -toTargetDirection;

        if (_isEjecting && Time.time < _ejectEndsAt)
        {
            // Reverse-thrust phase: keep nose pointed at the target so the ship doesn't
            // have to rotate (which would zero its velocity in EnemyAIFlightController3D
            // until it finished turning around). moveBackward=true makes the controller
            // accelerate the rigidbody along -forward at full moveSpeed from frame 1, so
            // the rammer immediately retreats from the player's collider rather than
            // freezing inside it. Skip separation/obstacle drift here on purpose: any
            // sideways shift to moveDirection would re-open the velocity gate (the
            // facing-vs-move angle check) and freeze the ship again.
            flightController?.SetFlightIntent(awayDirection, toTargetDirection, 1f, moveBackward: true);
            return;
        }

        // Eject window has expired - transition to normal face-and-fly disengage. Now we
        // are clear of the player's geometry, so separation/obstacle avoidance can drift
        // the move direction without risking another freeze.
        if (_isEjecting)
        {
            _isEjecting = false;
        }

        Vector3 steeringDirection = ResolveSteering(awayDirection);
        flightController?.SetMoveDirection(steeringDirection, 1f);
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

    private bool TryAcquireContact(out Entity3D contactEntity)
    {
        contactEntity = null;
        float radius = Mathf.Max(0.01f, ramDetectionDistance);
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Entity3D candidate = ResolveEntity(_overlapResults[i]);
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

        // ResolveAwayDirection returns the rammer-to-target vector (the direction the
        // rammer is travelling on impact). The existing knockback math pushes the target
        // along this vector - i.e. away from the contact point - which is what we want.
        Vector3 toTargetDirectionResolved = ResolveAwayDirection(target);
        if (toTargetDirectionResolved.sqrMagnitude <= 0.0001f)
        {
            toTargetDirectionResolved = toTargetDirection.sqrMagnitude > 0.0001f ? toTargetDirection.normalized : transform.forward;
        }

        Vector3 knockback = toTargetDirectionResolved * Mathf.Max(0f, knockbackVelocity);
        if (knockbackUpwardBias > 0f)
        {
            knockback += Vector3.up * knockbackUpwardBias;
        }

        ApplyKnockbackToTarget(target, knockback);
        target.TakeDamage(ramDamage, transform.position, enemy, DamageSource3D.Direct);

        BeginCollisionExemption(target);

        _isDisengaging = true;
        _disengageEndsAt = Time.time + Mathf.Max(0f, disengageDuration);
        _isEjecting = ejectDuration > 0f;
        _ejectEndsAt = Time.time + Mathf.Max(0f, ejectDuration);

        // Drive the reverse-thrust intent immediately so the rigidbody starts retreating
        // on the next FixedUpdate even if the steering think tick hasn't fired yet.
        // moveDirection = away from target; facingDirection = toward target (no rotation
        // needed); moveBackward = true so the controller pulls the rigidbody along
        // -forward at full moveSpeed.
        if (_isEjecting && flightController != null)
        {
            Vector3 awayFromTarget = -toTargetDirectionResolved;
            flightController.SetFlightIntent(awayFromTarget, toTargetDirectionResolved, 1f, moveBackward: true);
        }
    }

    private void EndDisengage()
    {
        _isDisengaging = false;
        _isEjecting = false;
        EndCollisionExemption();
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
            // Either side may have been destroyed (round end, target despawn, rammer
            // destroyed mid-disengage). Unity's Object lifetime check is null-safe on the
            // == operator; skip pairs where either collider is gone.
            if (pair.self == null || pair.other == null)
            {
                continue;
            }

            Physics.IgnoreCollision(pair.self, pair.other, false);
        }

        _ignoredPairs.Clear();
    }

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

    private Vector3 ResolveAwayDirection(Entity3D target)
    {
        Vector3 away = target.transform.position - transform.position;
        return away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward;
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, ramDetectionDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, disengageDistance);
    }
}
