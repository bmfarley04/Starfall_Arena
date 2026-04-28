using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AimAssistStrength3D
{
    BarelyThere,
    Subtle,
    Light
}

[DisallowMultipleComponent]
public class AimAssist3D : MonoBehaviour
{
    [System.Serializable]
    private struct AimAssistPresetTuning3D
    {
        [Range(0f, 1f)] public float slowdownMultiplier;
        [Range(0f, 45f)] public float assistConeAngle;
        public float maxAssistRange;
        [Range(0f, 1f)] public float screenDistanceWeight;
        [Range(0f, 25f)] public float maxAngularCorrection;
    }

    [System.Serializable]
    private struct AimAssistPresetSet3D
    {
        public AimAssistPresetTuning3D barelyThere;
        public AimAssistPresetTuning3D subtle;
        public AimAssistPresetTuning3D light;
    }

    [Header("Aim Assist")]
    [SerializeField] private bool aimAssistEnabled = true;
    [SerializeField] private AimAssistStrength3D strengthPreset = AimAssistStrength3D.Subtle;

    [Header("Targeting")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Entity3D owner;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private Faction3D targetFaction = Faction3D.EnemyTeam;
    [SerializeField] private float minimumDetectionRadius = 1.25f;
    [SerializeField] private float lineOfSightPadding = 0.05f;

    [Header("Preset Tuning")]
    [SerializeField] private AimAssistPresetSet3D presets = new AimAssistPresetSet3D
    {
        barelyThere = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.92f,
            assistConeAngle = 3.5f,
            maxAssistRange = 90f,
            screenDistanceWeight = 0.85f,
            maxAngularCorrection = 1.25f
        },
        subtle = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.8f,
            assistConeAngle = 6f,
            maxAssistRange = 120f,
            screenDistanceWeight = 0.75f,
            maxAngularCorrection = 2.5f
        },
        light = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.68f,
            assistConeAngle = 8f,
            maxAssistRange = 145f,
            screenDistanceWeight = 0.65f,
            maxAngularCorrection = 4f
        }
    };

    private const string ControllerScheme = "controller";

    private struct TargetCandidate
    {
        public Entity3D entity;
        public Vector3 aimPoint;
    }

    private void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        playerInput ??= GetComponent<PlayerInput>();
        aimCamera ??= Camera.main;
        ValidateTuning();
    }

    private void OnValidate()
    {
        ValidateTuning();
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    public bool IsControllerAimAssistActive()
    {
        if (!aimAssistEnabled)
        {
            return false;
        }

        if (playerInput == null)
        {
            return Gamepad.current != null;
        }

        return string.Equals(playerInput.currentControlScheme, ControllerScheme, StringComparison.OrdinalIgnoreCase);
    }

    public float GetLookSlowdownMultiplier()
    {
        return TryGetBestTarget(out _, out _)
            ? Mathf.Clamp01(GetActivePreset().slowdownMultiplier)
            : 1f;
    }

    public bool TryGetAssistedAimDirection(Vector3 origin, Vector3 baseDirection, out Vector3 assistedDirection)
    {
        assistedDirection = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : transform.forward;
        if (!IsControllerAimAssistActive())
        {
            return false;
        }

        if (!TryGetBestTarget(origin, assistedDirection, out TargetCandidate candidate))
        {
            return false;
        }

        Vector3 toTarget = candidate.aimPoint - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 targetDirection = toTarget.normalized;
        float angleToTarget = Vector3.Angle(assistedDirection, targetDirection);
        float maxCorrection = Mathf.Max(0f, GetActivePreset().maxAngularCorrection);
        if (maxCorrection <= 0.001f)
        {
            return false;
        }

        float t = Mathf.Clamp01(maxCorrection / Mathf.Max(0.001f, angleToTarget));
        assistedDirection = Vector3.Slerp(assistedDirection, targetDirection, t).normalized;
        return true;
    }

    public bool TryGetBestTarget(out Entity3D entity, out Vector3 aimPoint)
    {
        entity = null;
        aimPoint = Vector3.zero;

        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null || !IsControllerAimAssistActive())
        {
            return false;
        }

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!TryGetBestTarget(centerRay.origin, centerRay.direction, out TargetCandidate candidate))
        {
            return false;
        }

        entity = candidate.entity;
        aimPoint = candidate.aimPoint;
        return true;
    }

    private bool TryGetBestTarget(Vector3 origin, Vector3 forward, out TargetCandidate bestCandidate)
    {
        bestCandidate = default;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        AimAssistPresetTuning3D preset = GetActivePreset();
        float maxRange = Mathf.Max(1f, preset.maxAssistRange);
        float maxAngle = Mathf.Max(0f, preset.assistConeAngle);
        float sphereRadius = Mathf.Max(minimumDetectionRadius, Mathf.Tan(maxAngle * Mathf.Deg2Rad) * maxRange);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            sphereRadius,
            forward.normalized,
            maxRange,
            targetMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Entity3D candidateEntity = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Entity3D>() : null;
            if (!IsValidTarget(candidateEntity))
            {
                continue;
            }

            Vector3 candidateAimPoint = ResolveAimPoint(candidateEntity);
            Vector3 toTarget = candidateAimPoint - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > maxRange)
            {
                continue;
            }

            Vector3 targetDirection = toTarget / distance;
            float angle = Vector3.Angle(forward, targetDirection);
            if (angle > maxAngle)
            {
                continue;
            }

            if (!HasLineOfSight(origin, candidateAimPoint, candidateEntity))
            {
                continue;
            }

            float angleScore = 1f - Mathf.Clamp01(angle / Mathf.Max(0.001f, maxAngle));
            float distanceScore = 1f - Mathf.Clamp01(distance / maxRange);
            float score = Mathf.Lerp(distanceScore, angleScore, Mathf.Clamp01(preset.screenDistanceWeight));
            score += 0.0001f * (10000f - distance);

            if (!found || score > bestScore)
            {
                bestScore = score;
                bestCandidate = new TargetCandidate
                {
                    entity = candidateEntity,
                    aimPoint = candidateAimPoint
                };
                found = true;
            }
        }

        return found;
    }

    private bool IsValidTarget(Entity3D candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        if (!candidate.gameObject.activeInHierarchy || candidate.CurrentHealth <= 0f)
        {
            return false;
        }

        if (targetFaction != Faction3D.Neutral && FactionMember3D.ResolveFaction(candidate) != targetFaction)
        {
            return false;
        }

        if (owner != null && FactionMember3D.AreAllied(owner, candidate))
        {
            return false;
        }

        return true;
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 aimPoint, Entity3D candidate)
    {
        Vector3 toTarget = aimPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance + Mathf.Max(0f, lineOfSightPadding), lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Entity3D hitEntity = hit.collider != null ? hit.collider.GetComponentInParent<Entity3D>() : null;
        return hitEntity == candidate;
    }

    private static Vector3 ResolveAimPoint(Entity3D entity)
    {
        Collider collider = entity.GetComponentInChildren<Collider>();
        return collider != null ? collider.bounds.center : entity.transform.position;
    }

    private AimAssistPresetTuning3D GetActivePreset()
    {
        return strengthPreset switch
        {
            AimAssistStrength3D.BarelyThere => presets.barelyThere,
            AimAssistStrength3D.Light => presets.light,
            _ => presets.subtle
        };
    }

    private void ValidateTuning()
    {
        presets.barelyThere = ValidatePreset(presets.barelyThere);
        presets.subtle = ValidatePreset(presets.subtle);
        presets.light = ValidatePreset(presets.light);
    }

    private static AimAssistPresetTuning3D ValidatePreset(AimAssistPresetTuning3D preset)
    {
        preset.slowdownMultiplier = Mathf.Clamp01(preset.slowdownMultiplier);
        preset.assistConeAngle = Mathf.Clamp(preset.assistConeAngle, 0f, 45f);
        preset.maxAssistRange = Mathf.Max(1f, preset.maxAssistRange);
        preset.screenDistanceWeight = Mathf.Clamp01(preset.screenDistanceWeight);
        preset.maxAngularCorrection = Mathf.Clamp(preset.maxAngularCorrection, 0f, 25f);
        return preset;
    }
}
