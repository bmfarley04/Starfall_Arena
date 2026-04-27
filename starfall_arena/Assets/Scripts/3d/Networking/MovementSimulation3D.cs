using UnityEngine;

public static class MovementSimulation3D
{
    private const float MinSpeedSqrMagnitude = 0.0001f;

    public static void SimulateTick(
        ref MovementState3D state,
        in NetInputSnapshot3D input,
        in ShipFlightConfig3D flight,
        in ShipFlightAssistConfig3D assist,
        bool lockToWorldYPlane,
        float lockedWorldY,
        float dt)
    {
        FilterLookInput(ref state, input.LookInput, flight.lookInputResponse, dt);
        HandleRotation(ref state, input, flight, dt);
        HandleVelocity(ref state, input, flight, assist, dt);
        IntegratePosition(ref state, dt);

        if (lockToWorldYPlane)
        {
            state.Position.y = lockedWorldY;
            state.Velocity.y = 0f;
        }
    }

    public static Vector2 GetNormalizedTurnRates(in MovementState3D state, in ShipFlightConfig3D flight)
    {
        return new Vector2(
            NormalizeTurnRate(state.TurnRates.x, flight.pitchSpeed),
            NormalizeTurnRate(state.TurnRates.y, flight.yawSpeed));
    }

    private static void FilterLookInput(ref MovementState3D state, Vector2 lookInput, float response, float dt)
    {
        float safeResponse = Mathf.Max(0.01f, response);
        float lerpFactor = 1f - Mathf.Exp(-safeResponse * dt);
        state.FilteredLookInput = Vector2.Lerp(state.FilteredLookInput, Vector2.ClampMagnitude(lookInput, 1f), lerpFactor);
    }

    private static void HandleRotation(ref MovementState3D state, in NetInputSnapshot3D input, in ShipFlightConfig3D flight, float dt)
    {
        float speedPercent = flight.maxSpeed > 0f ? Mathf.Clamp01(state.Velocity.magnitude / flight.maxSpeed) : 0f;
        float speedRotationMultiplier = Mathf.Lerp(1f, flight.minRotationMultiplierAtMaxSpeed, speedPercent);

        Vector2 steeringInput = GetEffectiveSteeringInput(state.FilteredLookInput, flight.invertY);
        Vector2 targetTurnRates = new Vector2(
            steeringInput.y * flight.pitchSpeed * input.BaseRotationMultiplier * speedRotationMultiplier * input.AbilityRotationMultiplier,
            steeringInput.x * flight.yawSpeed * input.BaseRotationMultiplier * speedRotationMultiplier * input.AbilityRotationMultiplier);

        state.TurnRates.x = MoveTurnRate(state.TurnRates.x, targetTurnRates.x, flight.pitchAcceleration, flight.pitchDeceleration, dt);
        state.TurnRates.y = MoveTurnRate(state.TurnRates.y, targetTurnRates.y, flight.yawAcceleration, flight.yawDeceleration, dt);

        Quaternion localDelta = Quaternion.Euler(
            state.TurnRates.x * Mathf.Rad2Deg * dt,
            state.TurnRates.y * Mathf.Rad2Deg * dt,
            0f);
        state.Rotation = state.Rotation * localDelta;
    }

    private static void HandleVelocity(
        ref MovementState3D state,
        in NetInputSnapshot3D input,
        in ShipFlightConfig3D flight,
        in ShipFlightAssistConfig3D assist,
        float dt)
    {
        HandleThrustAndAssist(ref state, input, flight, assist, dt);
    }

    private static void HandleThrustAndAssist(
        ref MovementState3D state,
        in NetInputSnapshot3D input,
        in ShipFlightConfig3D flight,
        in ShipFlightAssistConfig3D assist,
        float dt)
    {
        float effectiveThrustInput = input.ThrustMultiplier > 0f ? Mathf.Max(0f, input.ThrustInput) : 0f;
        bool passiveLinearAssistEnabled = input.FrictionEnabled && assist.frictionDeceleration > 0f;
        Quaternion inverseRotation = Quaternion.Inverse(state.Rotation);
        Vector3 localVelocity = inverseRotation * state.Velocity;

        if (effectiveThrustInput > 0.05f)
        {
            localVelocity.z += effectiveThrustInput * flight.thrustAcceleration * input.ThrustMultiplier * input.SlowMultiplier * dt;
        }
        else if (passiveLinearAssistEnabled)
        {
            localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0f, assist.frictionDeceleration * dt);
        }

        if (passiveLinearAssistEnabled)
        {
            localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, assist.lateralDriftDamping * dt);
            localVelocity.y = Mathf.MoveTowards(localVelocity.y, 0f, assist.verticalDriftDamping * dt);
        }

        Vector3 worldVelocity = state.Rotation * localVelocity;
        worldVelocity = ApplyVelocityAlignment(worldVelocity, effectiveThrustInput, state, flight, assist, passiveLinearAssistEnabled, dt);

        float effectiveMaxSpeed = Mathf.Max(0f, flight.maxSpeed * input.SlowMultiplier);
        if (effectiveMaxSpeed > 0f && worldVelocity.magnitude > effectiveMaxSpeed)
        {
            worldVelocity = worldVelocity.normalized * effectiveMaxSpeed;
        }

        state.Velocity = worldVelocity;
    }

    private static Vector3 ApplyVelocityAlignment(
        Vector3 worldVelocity,
        float effectiveThrustInput,
        in MovementState3D state,
        in ShipFlightConfig3D flight,
        in ShipFlightAssistConfig3D assist,
        bool passiveLinearAssistEnabled,
        float dt)
    {
        if (!passiveLinearAssistEnabled || assist.velocityAlignmentStrength <= 0f || effectiveThrustInput <= 0.05f || worldVelocity.sqrMagnitude <= MinSpeedSqrMagnitude)
        {
            return worldVelocity;
        }

        Vector2 normalizedTurnRates = GetNormalizedTurnRates(state, flight);
        float turnInfluence = Mathf.Clamp01(Mathf.Max(Mathf.Abs(normalizedTurnRates.x), Mathf.Abs(normalizedTurnRates.y)));
        float alignmentStrength = assist.velocityAlignmentStrength * effectiveThrustInput * (0.5f + (0.5f * turnInfluence));
        float lerpFactor = 1f - Mathf.Exp(-alignmentStrength * dt);
        Vector3 alignedDirection = Vector3.Slerp(worldVelocity.normalized, state.Rotation * Vector3.forward, lerpFactor).normalized;
        return alignedDirection * worldVelocity.magnitude;
    }

    private static void IntegratePosition(ref MovementState3D state, float dt)
    {
        state.Position += state.Velocity * dt;

        if (state.DodgeRemainingTime <= 0f)
        {
            state.DodgeVelocity = Vector3.zero;
            state.DodgeExitVelocity = Vector3.zero;
            return;
        }

        float dodgeStep = Mathf.Min(dt, state.DodgeRemainingTime);
        state.Position += state.DodgeVelocity * dodgeStep;
        state.DodgeRemainingTime = Mathf.Max(0f, state.DodgeRemainingTime - dt);

        if (state.DodgeRemainingTime <= 0f)
        {
            state.DodgeVelocity = Vector3.zero;
            state.DodgeExitVelocity = Vector3.zero;
        }
    }

    private static Vector2 GetEffectiveSteeringInput(Vector2 filteredLookInput, bool invertY)
    {
        return new Vector2(filteredLookInput.x, filteredLookInput.y * (invertY ? -1f : 1f));
    }

    private static float MoveTurnRate(float current, float target, float acceleration, float deceleration, float dt)
    {
        float step = DetermineTurnRateStep(current, target, acceleration, deceleration) * dt;
        return Mathf.MoveTowards(current, target, step);
    }

    private static float DetermineTurnRateStep(float current, float target, float acceleration, float deceleration)
    {
        bool acceleratingIntoSameDirection = Mathf.Abs(target) > Mathf.Abs(current) && Mathf.Sign(target) == Mathf.Sign(current);
        return Mathf.Max(0.01f, acceleratingIntoSameDirection ? acceleration : deceleration);
    }

    private static float NormalizeTurnRate(float turnRate, float maxTurnRate)
    {
        if (maxTurnRate <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp(turnRate / maxTurnRate, -1f, 1f);
    }
}
