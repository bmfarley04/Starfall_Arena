using UnityEngine;

/// <summary>
/// Optional final steering polish for enemies that already compute a desired
/// world-space movement direction. This does not own speed, facing, or tactics.
/// </summary>
[DisallowMultipleComponent]
public class EnemySteeringSmoother3D : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;

    [Header("Steering Smoothing")]
    [Tooltip("Seconds used to bend toward a new steering direction. Lower values react faster; higher values make broader arcs.")]
    [SerializeField] private float turnSmoothTime = 0.16f;

    [Tooltip("Seconds used when the target direction is already close to the current smoothed direction. Higher values make small corrections settle more gently.")]
    [SerializeField] private float releaseSmoothTime = 0.24f;

    [Tooltip("Maximum degrees per second the smoothed steering direction may rotate. This is a safety cap, not the enemy body's turn speed. Set to 0 to disable the cap.")]
    [SerializeField] private float maxTurnDegreesPerSecond = 360f;

    [Header("Debug")]
    [Tooltip("Draw raw desired steering in gray and smoothed steering in magenta when this component is selected.")]
    [SerializeField] private bool drawGizmos = true;

    private Vector3 _rawDesiredDirection;
    private Vector3 _smoothedDirection;
    private float _lastSampleTime = -1f;
    private bool _hasSmoothedDirection;

    public Vector3 ResolveSteeringDirection(Vector3 desiredDirection)
    {
        Vector3 target = desiredDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? desiredDirection.normalized
            : transform.forward;

        if (target.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            ResetSmoothing();
            return Vector3.forward;
        }

        _rawDesiredDirection = target;

        float now = Time.time;
        float deltaTime = _lastSampleTime >= 0f ? Mathf.Max(0f, now - _lastSampleTime) : 0f;
        _lastSampleTime = now;

        if (!_hasSmoothedDirection)
        {
            _smoothedDirection = target;
            _hasSmoothedDirection = true;
            return target;
        }

        if (deltaTime <= 0f)
        {
            return _smoothedDirection;
        }

        float angle = Vector3.Angle(_smoothedDirection, target);
        float smoothTime = angle <= 10f ? releaseSmoothTime : turnSmoothTime;
        if (smoothTime <= 0f)
        {
            _smoothedDirection = target;
            return target;
        }

        float smoothingBlend = smoothTime <= 0f
            ? 1f
            : 1f - Mathf.Exp(-deltaTime / Mathf.Max(0.0001f, smoothTime));
        Vector3 blended = Vector3.Slerp(_smoothedDirection, target, smoothingBlend);
        float maxStep = maxTurnDegreesPerSecond * deltaTime;
        Vector3 capped = maxTurnDegreesPerSecond > 0f
            ? Vector3.RotateTowards(_smoothedDirection, blended, maxStep * Mathf.Deg2Rad, 0f)
            : blended;

        _smoothedDirection = capped.sqrMagnitude > MinDirectionSqrMagnitude
            ? capped.normalized
            : target;
        return _smoothedDirection;
    }

    private void OnDisable()
    {
        ResetSmoothing();
    }

    private void OnValidate()
    {
        turnSmoothTime = Mathf.Max(0f, turnSmoothTime);
        releaseSmoothTime = Mathf.Max(0f, releaseSmoothTime);
        maxTurnDegreesPerSecond = Mathf.Max(0f, maxTurnDegreesPerSecond);
    }

    private void ResetSmoothing()
    {
        _rawDesiredDirection = Vector3.zero;
        _smoothedDirection = Vector3.zero;
        _lastSampleTime = -1f;
        _hasSmoothedDirection = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !_hasSmoothedDirection)
        {
            return;
        }

        Vector3 origin = transform.position;
        float length = 8f;
        Gizmos.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        Gizmos.DrawRay(origin, _rawDesiredDirection * length);

        Gizmos.color = new Color(1f, 0.2f, 0.9f, 1f);
        Gizmos.DrawRay(origin, _smoothedDirection * length);
    }
}
