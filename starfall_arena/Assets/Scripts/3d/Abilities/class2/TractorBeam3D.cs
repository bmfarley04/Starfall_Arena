using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TractorBeam3D : Ability3D
{
    private const int MaxOverlapResults = 64;
    private static readonly Collider[] OverlapResults = new Collider[MaxOverlapResults];

    [System.Serializable]
    public struct TractorBeamAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses in seconds.")]
        public float cooldown;
        [Tooltip("How long the tractor beam stays active in seconds.")]
        public float duration;

        [Header("Area of Effect")]
        [Range(5f, 90f)]
        [Tooltip("Half-angle of the tractor cone in degrees.")]
        public float coneHalfAngle;
        [Tooltip("Maximum range of the tractor beam.")]
        public float coneRange;
        [Tooltip("Layer mask used when gathering pull targets.")]
        public LayerMask targetMask;

        [Header("Aiming")]
        [Tooltip("Optional camera used for center-screen aiming. If unset, the ability reuses the owner weapon aim camera when available.")]
        public Camera aimCamera;
        [Tooltip("Layer mask used when resolving center-screen aim hit points.")]
        public LayerMask aimCollisionMask;
        [Tooltip("Max ray distance used for center-screen aiming.")]
        public float maxAimDistance;
        [Tooltip("Minimum distance from the camera center ray used as a convergence point.")]
        public float screenCenterConvergenceDistance;
        [Range(0f, 1f)]
        [Tooltip("How strongly to blend cone direction toward the raw center-screen ray direction.")]
        public float screenCenterDirectionBlend;

        [Header("Pull Effect")]
        [Tooltip("Speed applied while pulling targets toward the ship.")]
        public float pullSpeed;
        [Tooltip("If true, targets inside the cone stop using their current velocity before the pull is applied.")]
        public bool freezeTargetMovement;
        [Tooltip("How close a target can get before the beam stops pulling it.")]
        public float stopDistance;

        [Header("Visuals")]
        [Tooltip("Optional spawn point used for the tractor pull query origin and beam facing. Keep your authored beam visuals aligned to this transform.")]
        public Transform spawnPoint;
        [Tooltip("Optional authored tractor beam root. The script only toggles this object on/off; it no longer generates visuals in code.")]
        public GameObject visualRoot;
        [Tooltip("If true, rotates the visual root to the same cone direction used by pull logic.")]
        public bool alignVisualToConeDirection;
        [Tooltip("Extra rotation offset applied after cone alignment (use this to compensate for mesh forward axis differences).")]
        public Vector3 visualRotationOffsetEuler;
        [Tooltip("If true, scales the visual root to match gameplay cone length/radius.")]
        public bool scaleVisualToCone;
        [Tooltip("Multiplier applied to the computed cone visual scale (X/Y=diameter, Z=range).")]
        public Vector3 visualConeScaleMultiplier;

        [Header("Debug")]
        [Tooltip("Draw the actual gameplay cone in Scene view when this object is selected.")]
        public bool drawGameplayConeGizmo;
        [Tooltip("Color used for the gameplay cone gizmo.")]
        public Color gameplayConeGizmoColor;

        [Header("Sound Effects")]
        [Tooltip("Looping sound played while the tractor beam is active.")]
        public SoundEffect beamLoopSound;
    }

    [Header("Ability 3 - Tractor Beam 3D")]
    [SerializeField] private TractorBeamAbilityConfig3D tractorBeam = new TractorBeamAbilityConfig3D
    {
        cooldown = 6f,
        duration = 2f,
        coneHalfAngle = 30f,
        coneRange = 15f,
        aimCollisionMask = ~0,
        maxAimDistance = 1000f,
        screenCenterConvergenceDistance = 150f,
        screenCenterDirectionBlend = 0.35f,
        pullSpeed = 20f,
        stopDistance = 1f,
        targetMask = ~0,
        alignVisualToConeDirection = true,
        visualRotationOffsetEuler = Vector3.zero,
        scaleVisualToCone = false,
        visualConeScaleMultiplier = Vector3.one,
        drawGameplayConeGizmo = true,
        gameplayConeGizmoColor = new Color(0f, 1f, 1f, 0.85f)
    };
    [SerializeField] private AudioSource tractorBeamLoopAudioSource;
    [SerializeField] private bool logInitialTargetHits = true;

    private bool _isActive;
    private bool _awaitingRelease;
    private float _activeUntilTime = -1f;
    private const float DefaultMaxAimDistance = 1000f;
    private const float DefaultScreenCenterConvergenceDistance = 150f;
    private readonly HashSet<int> _currentlyHitTargetIds = new HashSet<int>();
    private readonly HashSet<int> _hitTargetIdsThisFrame = new HashSet<int>();
    private NetCombat3D _netCombat;
    private bool _networkGameplayAuthority = true;
    private bool _hasNetworkAim;
    private Vector3 _networkAimDirection;

    protected override void Awake()
    {
        base.Awake();
        _netCombat = GetComponent<NetCombat3D>();

        if (tractorBeamLoopAudioSource == null)
        {
            tractorBeamLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        tractorBeamLoopAudioSource.playOnAwake = false;
        tractorBeamLoopAudioSource.loop = true;
        tractorBeamLoopAudioSource.spatialBlend = 1f;

        SetVisualRootActive(false);
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }

        if (Time.time >= _activeUntilTime)
        {
            DeactivateTractorBeam();
            return;
        }

        ResolveBeamAim(out Vector3 origin, out Vector3 forward);
        ApplyVisualAlignment(origin, forward);
    }

    private void FixedUpdate()
    {
        if (!_isActive)
        {
            return;
        }

        ResolveBeamAim(out Vector3 origin, out Vector3 forward);
        ApplyTractorBeamPull(origin, forward);

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned && _netCombat.IsOwner)
        {
            _netCombat.UpdateTractorBeamAim(forward);
        }
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            _awaitingRelease = false;
            return false;
        }

        if (_isActive || _awaitingRelease)
        {
            return false;
        }

        bool used = base.TryUseAbility(value);
        if (used)
        {
            _awaitingRelease = true;
        }

        return used;
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            Vector3 aimDirection = ResolveOwnerAimDirection();
            _netCombat.RequestTractorBeamState(true, aimDirection);
            if (!_netCombat.IsServer)
            {
                ApplyNetworkTractorBeamState(true, authoritative: false);
            }
            return;
        }

        ApplyNetworkTractorBeamState(true, authoritative: true);
    }

    private Vector3 ResolveOwnerAimDirection()
    {
        Vector3 origin = GetBeamOrigin();
        Vector3 forward = GetForwardDirection(origin);
        if (forward.sqrMagnitude > 0.0001f)
        {
            return forward.normalized;
        }

        Transform directionSource = tractorBeam.spawnPoint != null ? tractorBeam.spawnPoint : transform;
        return directionSource.forward.sqrMagnitude > 0.0001f
            ? directionSource.forward.normalized
            : Vector3.forward;
    }

    public override bool IsAbilityActive()
    {
        return _isActive;
    }

    protected override float GetCooldownDuration()
    {
        return tractorBeam.cooldown;
    }

    public override void Die()
    {
        _awaitingRelease = false;
        DeactivateTractorBeam();
    }

    private void ActivateTractorBeam()
    {
        _isActive = true;
        _activeUntilTime = Time.time + Mathf.Max(0.05f, tractorBeam.duration);
        SetVisualRootActive(true);
        ResolveBeamAim(out Vector3 origin, out Vector3 forward);
        ApplyVisualAlignment(origin, forward);
        StartBeamLoopSound();
    }

    public void ApplyNetworkTractorBeamState(bool isActive, bool authoritative)
    {
        _networkGameplayAuthority = authoritative;
        if (isActive)
        {
            ActivateTractorBeam();
        }
        else
        {
            DeactivateTractorBeam();
        }
    }

    public void ApplyNetworkTractorBeamAim(Vector3 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (_netCombat != null && _netCombat.IsOwner)
        {
            return;
        }

        _hasNetworkAim = true;
        _networkAimDirection = aimDirection.normalized;
    }

    private void DeactivateTractorBeam()
    {
        if (!_isActive)
        {
            return;
        }

        _isActive = false;
        _activeUntilTime = -1f;
        _currentlyHitTargetIds.Clear();
        _hitTargetIdsThisFrame.Clear();
        _hasNetworkAim = false;
        SetVisualRootActive(false);
        StopBeamLoopSound();
    }

    private void ApplyTractorBeamPull(Vector3 origin, Vector3 forward)
    {
        if (NetTickUtil.IsActive && !_networkGameplayAuthority)
        {
            return;
        }

        _hitTargetIdsThisFrame.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            Mathf.Max(0f, tractorBeam.coneRange),
            OverlapResults,
            tractorBeam.targetMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = OverlapResults[i];
            if (hitCollider == null)
            {
                continue;
            }

            Entity3D targetEntity = ResolveTargetEntity(hitCollider);
            if (targetEntity == null || targetEntity == entity)
            {
                continue;
            }

            Rigidbody targetBody = hitCollider.attachedRigidbody != null
                ? hitCollider.attachedRigidbody
                : targetEntity.GetComponent<Rigidbody>();

            if (targetBody == null)
            {
                continue;
            }

            Vector3 toTarget = targetBody.worldCenterOfMass - origin;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Max(0f, tractorBeam.stopDistance))
            {
                continue;
            }

            if (distance <= 0.0001f)
            {
                continue;
            }

            Vector3 directionToTarget = toTarget / distance;
            float angle = Vector3.Angle(forward, directionToTarget);
            if (angle > tractorBeam.coneHalfAngle)
            {
                continue;
            }

            int targetId = targetEntity.GetInstanceID();
            _hitTargetIdsThisFrame.Add(targetId);
            if (logInitialTargetHits && !_currentlyHitTargetIds.Contains(targetId))
            {
                Debug.Log($"[TractorBeam3D] Initial hit: {entity.name} -> {targetEntity.name}", this);
            }

            Vector3 pullVelocity = -directionToTarget * tractorBeam.pullSpeed;
            Vector3 previousVelocity = targetBody.linearVelocity;
            targetBody.linearVelocity = tractorBeam.freezeTargetMovement
                ? pullVelocity
                : targetBody.linearVelocity + (pullVelocity * Time.fixedDeltaTime);
            targetBody.GetComponent<NetMovement3D>()?.ApplyCombatVelocityDelta(targetBody.linearVelocity - previousVelocity);
        }

        _currentlyHitTargetIds.RemoveWhere(targetId => !_hitTargetIdsThisFrame.Contains(targetId));
        foreach (int targetId in _hitTargetIdsThisFrame)
        {
            _currentlyHitTargetIds.Add(targetId);
        }

        for (int i = 0; i < hitCount; i++)
        {
            OverlapResults[i] = null;
        }
    }

    private Entity3D ResolveTargetEntity(Collider hitCollider)
    {
        Entity3D targetEntity = hitCollider.GetComponent<Entity3D>();
        if (targetEntity != null)
        {
            return targetEntity;
        }

        if (hitCollider.attachedRigidbody != null)
        {
            targetEntity = hitCollider.attachedRigidbody.GetComponent<Entity3D>();
            if (targetEntity != null)
            {
                return targetEntity;
            }
        }

        return hitCollider.GetComponentInParent<Entity3D>();
    }

    private Vector3 GetForwardDirection()
    {
        return GetForwardDirection(GetBeamOrigin());
    }

    private Vector3 GetForwardDirection(Vector3 origin)
    {
        Transform directionSource = tractorBeam.spawnPoint != null ? tractorBeam.spawnPoint : transform;
        Vector3 fallbackForward = directionSource.forward.sqrMagnitude > 0.0001f
            ? directionSource.forward.normalized
            : Vector3.forward;

        if (TryResolveScreenCenterAim(origin, fallbackForward, out Vector3 aimDirection))
        {
            return aimDirection;
        }

        return fallbackForward;
    }

    private Vector3 GetBeamOrigin()
    {
        return tractorBeam.spawnPoint != null ? tractorBeam.spawnPoint.position : transform.position;
    }

    private void ResolveBeamAim(out Vector3 origin, out Vector3 forward)
    {
        origin = GetBeamOrigin();

        if (_hasNetworkAim && _networkAimDirection.sqrMagnitude > 0.0001f)
        {
            forward = _networkAimDirection.normalized;
            return;
        }

        forward = GetForwardDirection(origin);
    }

    private bool TryResolveScreenCenterAim(Vector3 origin, Vector3 fallbackForward, out Vector3 resolvedDirection)
    {
        resolvedDirection = fallbackForward;

        Camera resolvedCamera = ResolveAimCamera();
        if (resolvedCamera == null)
        {
            return false;
        }

        float maxAimDistance = tractorBeam.maxAimDistance > 0f
            ? tractorBeam.maxAimDistance
            : DefaultMaxAimDistance;
        float convergenceDistanceFloor = tractorBeam.screenCenterConvergenceDistance > 0f
            ? tractorBeam.screenCenterConvergenceDistance
            : DefaultScreenCenterConvergenceDistance;

        Ray centerRay = resolvedCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 centerPoint = centerRay.origin + (centerRay.direction * Mathf.Max(convergenceDistanceFloor, maxAimDistance));

        if (Physics.Raycast(centerRay, out RaycastHit hit, maxAimDistance, tractorBeam.aimCollisionMask, QueryTriggerInteraction.Ignore))
        {
            float convergenceDistance = Mathf.Max(convergenceDistanceFloor, hit.distance);
            centerPoint = centerRay.origin + (centerRay.direction * convergenceDistance);
        }

        Vector3 toAimPoint = centerPoint - origin;
        if (toAimPoint.sqrMagnitude > 0.0001f)
        {
            resolvedDirection = toAimPoint.normalized;
        }
        else if (centerRay.direction.sqrMagnitude > 0.0001f)
        {
            resolvedDirection = centerRay.direction.normalized;
        }

        if (centerRay.direction.sqrMagnitude > 0.0001f && tractorBeam.screenCenterDirectionBlend > 0f)
        {
            resolvedDirection = Vector3.Slerp(
                resolvedDirection,
                centerRay.direction.normalized,
                Mathf.Clamp01(tractorBeam.screenCenterDirectionBlend)).normalized;
        }

        return true;
    }

    private Camera ResolveAimCamera()
    {
        if (tractorBeam.aimCamera != null)
        {
            return tractorBeam.aimCamera;
        }

        if (entity != null)
        {
            Weapon3D selectedWeapon = entity.SelectedWeapon;
            if (selectedWeapon != null && selectedWeapon.AimCamera != null)
            {
                return selectedWeapon.AimCamera;
            }

            if (entity.PrimaryWeapon != null && entity.PrimaryWeapon.AimCamera != null)
            {
                return entity.PrimaryWeapon.AimCamera;
            }
        }

        return entity is Player3D ? Camera.main : null;
    }

    private void SetVisualRootActive(bool isActive)
    {
        if (tractorBeam.visualRoot != null)
        {
            tractorBeam.visualRoot.SetActive(isActive);
        }
    }

    private void ApplyVisualAlignment(Vector3 origin, Vector3 forward)
    {
        if (!tractorBeam.alignVisualToConeDirection || tractorBeam.visualRoot == null)
        {
            return;
        }

        Transform visualTransform = tractorBeam.visualRoot.transform;
        Vector3 upReference = tractorBeam.spawnPoint != null ? tractorBeam.spawnPoint.up : transform.up;
        if (upReference.sqrMagnitude <= 0.0001f || Mathf.Abs(Vector3.Dot(upReference.normalized, forward)) > 0.995f)
        {
            upReference = Vector3.up;
        }

        Quaternion alignedRotation = Quaternion.LookRotation(forward, upReference)
                                     * Quaternion.Euler(tractorBeam.visualRotationOffsetEuler);
        visualTransform.SetPositionAndRotation(origin, alignedRotation);

        if (!tractorBeam.scaleVisualToCone)
        {
            return;
        }

        float range = Mathf.Max(0.01f, tractorBeam.coneRange);
        float radius = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(tractorBeam.coneHalfAngle, 0.1f, 89f)) * range;
        Vector3 coneScale = new Vector3(radius * 2f, radius * 2f, range);
        visualTransform.localScale = Vector3.Scale(coneScale, tractorBeam.visualConeScaleMultiplier);
    }

    private void StartBeamLoopSound()
    {
        if (tractorBeam.beamLoopSound == null || tractorBeamLoopAudioSource == null)
        {
            return;
        }

        if (tractorBeamLoopAudioSource.isPlaying && tractorBeamLoopAudioSource.clip == tractorBeam.beamLoopSound.clip)
        {
            return;
        }

        tractorBeam.beamLoopSound.Play(tractorBeamLoopAudioSource);
    }

    private void StopBeamLoopSound()
    {
        if (tractorBeamLoopAudioSource != null && tractorBeamLoopAudioSource.isPlaying)
        {
            tractorBeamLoopAudioSource.Stop();
        }
    }

    private void OnDisable()
    {
        _awaitingRelease = false;
        DeactivateTractorBeam();
    }

    private void OnDrawGizmosSelected()
    {
        if (!tractorBeam.drawGameplayConeGizmo)
        {
            return;
        }

        Vector3 origin = GetBeamOrigin();
        Vector3 forward;
        if (Application.isPlaying)
        {
            forward = GetForwardDirection(origin);
        }
        else
        {
            Transform directionSource = tractorBeam.spawnPoint != null ? tractorBeam.spawnPoint : transform;
            forward = directionSource.forward.sqrMagnitude > 0.0001f ? directionSource.forward.normalized : transform.forward;
        }

        DrawConeGizmo(origin, forward);
    }

    private void DrawConeGizmo(Vector3 origin, Vector3 forward)
    {
        float range = Mathf.Max(0f, tractorBeam.coneRange);
        if (range <= 0f || forward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float halfAngleRadians = Mathf.Deg2Rad * Mathf.Clamp(tractorBeam.coneHalfAngle, 0.1f, 89f);
        float radius = Mathf.Tan(halfAngleRadians) * range;
        Vector3 forwardNorm = forward.normalized;

        Vector3 right = Vector3.Cross(forwardNorm, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(forwardNorm, Vector3.right);
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(right, forwardNorm).normalized;
        Vector3 center = origin + (forwardNorm * range);

        Color previous = Gizmos.color;
        Gizmos.color = tractorBeam.gameplayConeGizmoColor;

        const int segments = 20;
        Vector3 firstPoint = center + (right * radius);
        Vector3 previousPoint = firstPoint;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 circlePoint = center + ((Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * radius);
            Gizmos.DrawLine(previousPoint, circlePoint);
            previousPoint = circlePoint;
        }

        Gizmos.DrawLine(origin, center + (right * radius));
        Gizmos.DrawLine(origin, center - (right * radius));
        Gizmos.DrawLine(origin, center + (up * radius));
        Gizmos.DrawLine(origin, center - (up * radius));
        Gizmos.DrawLine(origin, center);

        Gizmos.color = previous;
    }
}
