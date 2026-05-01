using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal enemy-only separation steering. Add this component to enemy prefabs that
/// should gently fan out from nearby enemies, then route brain steering through
/// ResolveSteeringDirection before obstacle avoidance.
/// </summary>
[DisallowMultipleComponent]
public class EnemySeparation3D : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private static readonly List<EnemySeparation3D> ActiveAgents = new List<EnemySeparation3D>(64);

    [Header("Enemy Separation")]
    [Tooltip("Radius in meters where other enemies with EnemySeparation3D start pushing this enemy away. Start around 2-3x the ship width.")]
    [SerializeField] private float separationRadius = 8f;

    [Tooltip("How strongly the separation direction bends the brain's desired movement direction. 1 is a gentle fan-out; higher values make enemies prioritize spacing more.")]
    [SerializeField] private float separationStrength = 1f;

    [Tooltip("Multiplier applied to vertical separation. 1 allows full 3D fan-out; 0 keeps separation mostly horizontal.")]
    [SerializeField] private float verticalWeight = 1f;

    [Tooltip("Speed scale used by brains that are otherwise stopped but need a tiny nudge away from overlapping enemies.")]
    [SerializeField, Range(0f, 1f)] private float unstickSpeedScale = 0.2f;

    private Enemy3D _selfEnemy;

    public float UnstickSpeedScale => Mathf.Clamp01(unstickSpeedScale);

    private void Awake()
    {
        _selfEnemy = GetComponentInParent<Enemy3D>();
    }

    private void OnEnable()
    {
        if (!ActiveAgents.Contains(this))
        {
            ActiveAgents.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveAgents.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveAgents.Remove(this);
    }

    private void OnValidate()
    {
        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
        verticalWeight = Mathf.Max(0f, verticalWeight);
        unstickSpeedScale = Mathf.Clamp01(unstickSpeedScale);
    }

    public Vector3 ResolveSteeringDirection(Vector3 desiredDirection)
    {
        Vector3 desired = desiredDirection.sqrMagnitude > MinDirectionSqrMagnitude
            ? desiredDirection.normalized
            : transform.forward;

        if (!TryGetSeparationDirection(out Vector3 separationDirection))
        {
            return desired;
        }

        Vector3 steered = desired + separationDirection * separationStrength;
        return steered.sqrMagnitude > MinDirectionSqrMagnitude ? steered.normalized : desired;
    }

    public bool TryGetUnstickIntent(out Vector3 unstickDirection, out float speedScale)
    {
        speedScale = UnstickSpeedScale;
        if (speedScale <= 0f || !TryGetSeparationDirection(out unstickDirection))
        {
            unstickDirection = Vector3.zero;
            speedScale = 0f;
            return false;
        }

        return true;
    }

    public bool TryGetSeparationDirection(out Vector3 separationDirection)
    {
        separationDirection = Vector3.zero;
        if (!TryResolveSelfEnemy(out Enemy3D selfEnemy) || !IsLiveEnemy(selfEnemy))
        {
            return false;
        }

        float radius = Mathf.Max(0f, separationRadius);
        if (radius <= 0.01f)
        {
            return false;
        }

        Vector3 selfPosition = transform.position;
        float radiusSqr = radius * radius;
        for (int i = 0; i < ActiveAgents.Count; i++)
        {
            EnemySeparation3D other = ActiveAgents[i];
            if (other == null
                || other == this
                || !other.TryResolveSelfEnemy(out Enemy3D otherEnemy)
                || !IsLiveEnemy(otherEnemy))
            {
                continue;
            }

            Vector3 offset = selfPosition - other.transform.position;
            Vector3 weightedOffset = new Vector3(offset.x, offset.y * verticalWeight, offset.z);
            float weightedDistanceSqr = weightedOffset.sqrMagnitude;
            if (weightedDistanceSqr > radiusSqr)
            {
                continue;
            }

            if (weightedDistanceSqr <= MinDirectionSqrMagnitude)
            {
                weightedOffset = ResolveStableFallbackDirection(other);
                weightedDistanceSqr = weightedOffset.sqrMagnitude;
            }

            float distance = Mathf.Sqrt(weightedDistanceSqr);
            Vector3 awayDirection = weightedOffset / Mathf.Max(distance, 0.0001f);
            float falloff = 1f - Mathf.Clamp01(distance / radius);
            separationDirection += awayDirection * falloff;
        }

        if (separationDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            separationDirection = Vector3.zero;
            return false;
        }

        separationDirection.Normalize();
        return true;
    }

    private Vector3 ResolveStableFallbackDirection(EnemySeparation3D other)
    {
        int hash = GetInstanceID() ^ (other != null ? other.GetInstanceID() * 397 : 0);
        float side = (hash & 1) == 0 ? 1f : -1f;
        float lift = (hash & 2) == 0 ? 0.35f : -0.35f;
        Vector3 right = transform.right.sqrMagnitude > MinDirectionSqrMagnitude ? transform.right : Vector3.right;
        Vector3 up = transform.up.sqrMagnitude > MinDirectionSqrMagnitude ? transform.up : Vector3.up;
        Vector3 fallback = right * side + up * (lift * verticalWeight);
        return fallback.sqrMagnitude > MinDirectionSqrMagnitude ? fallback.normalized : Vector3.right * side;
    }

    private static bool IsLiveEnemy(Enemy3D enemy)
    {
        return enemy != null
            && enemy.CurrentHealth > 0f
            && enemy.gameObject.activeInHierarchy;
    }

    private bool TryResolveSelfEnemy(out Enemy3D enemy)
    {
        if (_selfEnemy == null)
        {
            _selfEnemy = GetComponentInParent<Enemy3D>();
        }

        enemy = _selfEnemy;
        return enemy != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}
