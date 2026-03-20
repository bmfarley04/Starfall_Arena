using UnityEngine;

/// <summary>
/// Pure-math deterministic movement simulation.
/// Contains the exact same logic as Player.FixedUpdate / Entity movement helpers,
/// but operates on plain value types (ref Vector2 velocity, ref float rotation, etc.)
/// with no Rigidbody2D or MonoBehaviour dependency.
///
/// Both the local-play path (Player.FixedUpdate) and the networked path (NetMovement)
/// should ultimately call into this class so the two paths are provably identical.
/// During reconciliation replay this lets us loop N ticks of pure arithmetic
/// and write the final result to the Rigidbody only once.
/// </summary>
public static class MovementSimulation
{
    /// <summary>
    /// Simulate a single movement tick. Mutates velocity, position, rotation,
    /// frictionTimer and anchorDragAccumulator in place.
    ///
    /// The operations are executed in the same order as Player.FixedUpdate:
    ///   1. If thrusting  → lateral damping, then thrust acceleration, reset friction timer
    ///   2. If not thrusting → friction (after delay)
    ///   3. Anchor drag (if anchored)
    ///   4. Velocity clamping (maxSpeed * slowMult)
    ///   5. Position integration (velocity * dt)
    ///   6. Rotation (MoveTowardsAngle toward look input)
    /// </summary>
    /// <param name="velocity">Current linear velocity (mutated in place).</param>
    /// <param name="position">Current world position (mutated in place).</param>
    /// <param name="rotation">Current Z rotation in degrees (mutated in place).</param>
    /// <param name="frictionTimer">Running friction delay timer (mutated in place).</param>
    /// <param name="anchorDragAccumulator">Running anchor drag accumulator (mutated in place).</param>
    /// <param name="input">The input snapshot for this tick.</param>
    /// <param name="moveCfg">Ship movement config (thrustForce, maxSpeed, rotationSpeed, lateralDamping).</param>
    /// <param name="fricCfg">Friction config (frictionDelay, frictionDeceleration).</param>
    /// <param name="inputCfg">Input config (controllerLookDeadzone).</param>
    /// <param name="mass">Rigidbody mass (used for thrust acceleration).</param>
    /// <param name="dt">Time step (seconds) for this tick.</param>
    /// <param name="slowMultiplier">Current slow effect multiplier (1 = normal).</param>
    /// <param name="anchorRotationMultiplier">Rotation speed multiplier while anchored (e.g. 3).</param>
    /// <param name="abilityRotationMultiplier">Rotation speed multiplier from active ability (1 = no modifier).</param>
    public static void SimulateTick(
        ref Vector2 velocity,
        ref Vector2 position,
        ref float rotation,
        ref float frictionTimer,
        ref float anchorDragAccumulator,
        in NetInputSnapshot input,
        in MovementConfig moveCfg,
        in FrictionConfig fricCfg,
        in InputConfig inputCfg,
        float mass,
        float dt,
        float slowMultiplier = 1f,
        float anchorRotationMultiplier = 3f,
        float abilityRotationMultiplier = 1f)
    {
        // --- Forward direction from current rotation ---
        float rotRad = (rotation + 90f) * Mathf.Deg2Rad; // +90 because Unity's "up" = +90° offset
        Vector2 forward = new Vector2(Mathf.Cos(rotRad), Mathf.Sin(rotRad));

        // ===== 1. THRUST OR FRICTION =====
        if (input.Thrust)
        {
            // Lateral damping first (same order as Player.FixedUpdate)
            ApplyLateralDamping(ref velocity, forward, moveCfg.lateralDamping);

            // Thrust acceleration
            float acceleration = (moveCfg.thrustForce * slowMultiplier) / mass;
            velocity += forward * acceleration * dt;

            frictionTimer = 0f;
        }
        else
        {
            if (input.FrictionEnabled)
            {
                frictionTimer += dt;

                if (frictionTimer >= fricCfg.frictionDelay)
                {
                    velocity = Vector2.MoveTowards(velocity, Vector2.zero, fricCfg.frictionDeceleration * dt);
                }
            }
        }

        // ===== 2. ANCHOR DRAG =====
        if (input.Anchor)
        {
            anchorDragAccumulator += 0.1f;
            float dragFactor = 1f / (1f + anchorDragAccumulator * dt);
            velocity *= dragFactor;
        }

        // ===== 3. VELOCITY CLAMPING =====
        float effectiveMaxSpeed = moveCfg.maxSpeed * slowMultiplier;
        if (velocity.magnitude > effectiveMaxSpeed)
        {
            velocity = velocity.normalized * effectiveMaxSpeed;
        }

        // NOTE: Position integration (position += velocity * dt) is NOT done here.
        // In the live path, the Rigidbody2D's physics step handles this automatically.
        // For reconciliation replay, call IntegratePosition() after each SimulateTick().

        // ===== 4. ROTATION =====
        if (input.LookInput.magnitude > inputCfg.controllerLookDeadzone)
        {
            float effectiveRotationSpeed = moveCfg.rotationSpeed * abilityRotationMultiplier;
            if (input.Anchor)
            {
                effectiveRotationSpeed *= anchorRotationMultiplier;
            }

            float targetAngle = Mathf.Atan2(input.LookInput.y, input.LookInput.x) * Mathf.Rad2Deg;
            // ROTATION_OFFSET is -90 in Entity.cs
            rotation = Mathf.MoveTowardsAngle(rotation, targetAngle + (-90f), effectiveRotationSpeed * dt);
        }
    }

    /// <summary>
    /// Manually integrate position by velocity. Call after SimulateTick() during
    /// reconciliation replay where no physics step is available.
    /// In the live path (OwnerTick / server), the Rigidbody handles this automatically.
    /// </summary>
    public static void IntegratePosition(ref Vector2 position, Vector2 velocity, float dt)
    {
        position += velocity * dt;
    }

    /// <summary>
    /// Decomposes velocity into forward and lateral components,
    /// then damps the lateral component. Identical to Entity.ApplyLateralDamping().
    /// </summary>
    public static void ApplyLateralDamping(ref Vector2 velocity, Vector2 forward, float lateralDamping)
    {
        float forwardSpeed = Vector2.Dot(velocity, forward);
        Vector2 forwardVelocity = forward * forwardSpeed;
        Vector2 lateralVelocity = velocity - forwardVelocity;

        lateralVelocity *= lateralDamping;

        velocity = forwardVelocity + lateralVelocity;
    }
}
