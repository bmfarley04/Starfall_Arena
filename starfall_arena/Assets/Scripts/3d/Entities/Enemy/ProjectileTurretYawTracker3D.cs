using UnityEngine;

public class ProjectileTurretYawTracker3D : MonoBehaviour
{
    [System.Serializable]
    private struct TurretBinding
    {
        [Tooltip("Yawing base transform. This pivot rotates around local Y to face the target horizontally.")]
        public Transform baseYawPivot;

        [Tooltip("If true, this base uses local X instead of local Y for horizontal tracking. Use this for side-mounted bases whose authored swivel axis is local X.")]
        public bool useBaseXRotation;

        [Tooltip("Optional child turret/barrel transform. This pivot rotates around local X to track the target vertically. Leave empty for yaw-only turrets.")]
        public Transform pitchPivot;
    }

    [Header("References")]
    [Tooltip("Optional target sensor used to find the player-team target the turrets should face. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional staggered projectile weapon used to auto-fill legacy yaw-only Turret Pivots from its configured muzzles. Leave empty if Turret Bindings or Turret Pivots are assigned manually.")]
    [SerializeField] private StaggeredProjectileWeaponEnemy3D turretWeapon;

    [Tooltip("Preferred setup for two-part turrets: base handles local Y yaw, pitch child handles local X elevation.")]
    [SerializeField] private TurretBinding[] turretBindings;

    [Tooltip("Legacy/simple yaw-only turret or muzzle transforms. Used only when Turret Bindings is empty.")]
    [SerializeField] private Transform[] turretPivots;

    [Header("Yaw Tracking")]
    [Tooltip("Degrees per second used to turn each turret base toward the target. Set to 0 for instant tracking.")]
    [SerializeField] private float yawDegreesPerSecond = 240f;

    [Tooltip("Extra local yaw offset applied after target yaw is solved. Use this if the model's base-forward axis is not local +Z.")]
    [SerializeField] private float localYawOffsetDegrees = 0f;

    [Tooltip("Maximum yaw delta allowed away from each base's starting local Y rotation. Set to 0 for unlimited travel.")]
    [SerializeField] private float maxYawFromRestDegrees = 0f;

    [Header("Pitch Tracking")]
    [Tooltip("Degrees per second used to elevate each pitch pivot toward the target. Set to 0 for instant tracking.")]
    [SerializeField] private float pitchDegreesPerSecond = 240f;

    [Tooltip("Extra local pitch offset applied after target pitch is solved. Use this if the model's barrel-forward axis is not local +Z.")]
    [SerializeField] private float localPitchOffsetDegrees = 0f;

    [Tooltip("If true, flips the local X pitch direction. Enable this if turrets pitch down when the target is above them.")]
    [SerializeField] private bool invertPitch;

    [Tooltip("Maximum pitch delta allowed away from each pitch pivot's starting local X rotation. Set to 0 for unlimited travel.")]
    [SerializeField] private float maxPitchFromRestDegrees = 60f;

    [Header("Targeting")]
    [Tooltip("Seconds between target refreshes. Visual tracking runs every frame, but target acquisition is throttled to avoid repeated scene-wide searches.")]
    [SerializeField] private float targetRefreshInterval = 0.1f;

    [Header("Idle")]
    [Tooltip("If true, turret bases and pitch pivots ease back to their authored starting rotations when no valid target is available.")]
    [SerializeField] private bool returnToRestWhenIdle = true;

    private Quaternion[] _bindingBaseRestLocalRotations;
    private Quaternion[] _bindingPitchRestLocalRotations;
    private Quaternion[] _legacyRestLocalRotations;
    private Entity3D _target;
    private float _nextTargetRefreshTime;

    private bool HasTurretBindings => turretBindings != null && turretBindings.Length > 0;

    private void Awake()
    {
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        turretWeapon ??= GetComponent<StaggeredProjectileWeaponEnemy3D>();
        ResolveLegacyTurretPivotsIfNeeded();
        CacheRestRotations();
    }

    private void OnValidate()
    {
        yawDegreesPerSecond = Mathf.Max(0f, yawDegreesPerSecond);
        maxYawFromRestDegrees = Mathf.Max(0f, maxYawFromRestDegrees);
        pitchDegreesPerSecond = Mathf.Max(0f, pitchDegreesPerSecond);
        maxPitchFromRestDegrees = Mathf.Max(0f, maxPitchFromRestDegrees);
        targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
    }

    private void OnEnable()
    {
        ResolveLegacyTurretPivotsIfNeeded();
        CacheRestRotations();
        _nextTargetRefreshTime = 0f;
    }

    private void Update()
    {
        if (!HasTurretBindings && (turretPivots == null || turretPivots.Length == 0))
        {
            ResolveLegacyTurretPivotsIfNeeded();
            CacheRestRotations();
        }

        if (!HasTurretBindings && (turretPivots == null || turretPivots.Length == 0))
        {
            return;
        }

        RefreshTargetIfNeeded();

        if (IsTargetValid(_target))
        {
            TrackTarget(_target);
            return;
        }

        if (returnToRestWhenIdle)
        {
            ReturnToRest();
        }
    }

    private void ResolveLegacyTurretPivotsIfNeeded()
    {
        if (HasTurretBindings || turretPivots != null && turretPivots.Length > 0)
        {
            return;
        }

        if (turretWeapon == null || turretWeapon.WeaponConfig.muzzles == null)
        {
            return;
        }

        turretPivots = turretWeapon.WeaponConfig.muzzles;
    }

    private void CacheRestRotations()
    {
        if (HasTurretBindings)
        {
            if (_bindingBaseRestLocalRotations == null || _bindingBaseRestLocalRotations.Length != turretBindings.Length)
            {
                _bindingBaseRestLocalRotations = new Quaternion[turretBindings.Length];
                _bindingPitchRestLocalRotations = new Quaternion[turretBindings.Length];
                for (int i = 0; i < turretBindings.Length; i++)
                {
                    _bindingBaseRestLocalRotations[i] = turretBindings[i].baseYawPivot != null
                        ? turretBindings[i].baseYawPivot.localRotation
                        : Quaternion.identity;
                    _bindingPitchRestLocalRotations[i] = turretBindings[i].pitchPivot != null
                        ? turretBindings[i].pitchPivot.localRotation
                        : Quaternion.identity;
                }
            }

            return;
        }

        if (turretPivots == null)
        {
            _legacyRestLocalRotations = null;
            return;
        }

        if (_legacyRestLocalRotations != null && _legacyRestLocalRotations.Length == turretPivots.Length)
        {
            return;
        }

        _legacyRestLocalRotations = new Quaternion[turretPivots.Length];
        for (int i = 0; i < turretPivots.Length; i++)
        {
            _legacyRestLocalRotations[i] = turretPivots[i] != null ? turretPivots[i].localRotation : Quaternion.identity;
        }
    }

    private void RefreshTargetIfNeeded()
    {
        if (Time.time < _nextTargetRefreshTime)
        {
            return;
        }

        _nextTargetRefreshTime = Time.time + targetRefreshInterval;
        _target = targetSensor != null ? targetSensor.GetTarget() : null;
    }

    private void TrackTarget(Entity3D target)
    {
        if (HasTurretBindings)
        {
            TrackBoundTurrets(target.transform.position);
            return;
        }

        TrackLegacyYawOnlyTurrets(target.transform.position);
    }

    private void TrackBoundTurrets(Vector3 targetPosition)
    {
        for (int i = 0; i < turretBindings.Length; i++)
        {
            Transform basePivot = turretBindings[i].baseYawPivot;
            if (basePivot == null)
            {
                continue;
            }

            Quaternion desiredBaseRotation = ResolveDesiredBaseRotation(basePivot, targetPosition, ResolveBindingBaseRestRotation(i), turretBindings[i].useBaseXRotation);
            basePivot.localRotation = RotateToward(basePivot.localRotation, desiredBaseRotation, yawDegreesPerSecond);

            Transform pitchPivot = turretBindings[i].pitchPivot;
            if (pitchPivot == null)
            {
                continue;
            }

            Quaternion desiredPitchRotation = ResolveDesiredPitchRotation(pitchPivot, targetPosition, ResolveBindingPitchRestRotation(i));
            pitchPivot.localRotation = RotateToward(pitchPivot.localRotation, desiredPitchRotation, pitchDegreesPerSecond);
        }
    }

    private void TrackLegacyYawOnlyTurrets(Vector3 targetPosition)
    {
        for (int i = 0; i < turretPivots.Length; i++)
        {
            Transform pivot = turretPivots[i];
            if (pivot == null)
            {
                continue;
            }

            Quaternion desiredLocalRotation = ResolveDesiredYawRotation(pivot, targetPosition, ResolveLegacyRestRotation(i));
            pivot.localRotation = RotateToward(pivot.localRotation, desiredLocalRotation, yawDegreesPerSecond);
        }
    }

    private Quaternion ResolveDesiredBaseRotation(Transform pivot, Vector3 targetPosition, Quaternion restRotation, bool useBaseXRotation)
    {
        return useBaseXRotation
            ? ResolveDesiredBaseXRotation(pivot, targetPosition, restRotation)
            : ResolveDesiredYawRotation(pivot, targetPosition, restRotation);
    }

    private Quaternion ResolveDesiredYawRotation(Transform pivot, Vector3 targetPosition, Quaternion restRotation)
    {
        Vector3 toTarget = targetPosition - pivot.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        Transform parent = pivot.parent != null ? pivot.parent : transform;
        Vector3 localDirection = parent.InverseTransformDirection(toTarget.normalized);
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        float desiredYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg + localYawOffsetDegrees;
        Vector3 restEuler = restRotation.eulerAngles;
        float yaw = ResolveConstrainedAngle(restEuler.y, desiredYaw, maxYawFromRestDegrees);
        return Quaternion.Euler(restEuler.x, yaw, restEuler.z);
    }

    private Quaternion ResolveDesiredBaseXRotation(Transform pivot, Vector3 targetPosition, Quaternion restRotation)
    {
        Vector3 toTarget = targetPosition - pivot.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        Transform parent = pivot.parent != null ? pivot.parent : transform;
        Vector3 localDirection = parent.InverseTransformDirection(toTarget.normalized);
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        float desiredBaseX = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg + localYawOffsetDegrees;
        Vector3 restEuler = restRotation.eulerAngles;
        float baseX = ResolveConstrainedAngle(restEuler.x, desiredBaseX, maxYawFromRestDegrees);
        return Quaternion.Euler(baseX, restEuler.y, restEuler.z);
    }

    private Quaternion ResolveDesiredPitchRotation(Transform pivot, Vector3 targetPosition, Quaternion restRotation)
    {
        Vector3 toTarget = targetPosition - pivot.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        Transform parent = pivot.parent != null ? pivot.parent : transform;
        Vector3 localDirection = parent.InverseTransformDirection(toTarget.normalized);
        Vector2 pitchPlane = new Vector2(localDirection.z, localDirection.y);
        if (pitchPlane.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        float pitchSign = invertPitch ? 1f : -1f;
        float desiredPitch = Mathf.Atan2(localDirection.y, Mathf.Abs(localDirection.z)) * Mathf.Rad2Deg * pitchSign
            + localPitchOffsetDegrees;
        Vector3 restEuler = restRotation.eulerAngles;
        float pitch = ResolveConstrainedAngle(restEuler.x, desiredPitch, maxPitchFromRestDegrees);
        return Quaternion.Euler(pitch, restEuler.y, restEuler.z);
    }

    private static float ResolveConstrainedAngle(float restAngle, float desiredAngle, float maxDeltaFromRest)
    {
        if (maxDeltaFromRest <= 0f)
        {
            return desiredAngle;
        }

        float delta = Mathf.DeltaAngle(restAngle, desiredAngle);
        return restAngle + Mathf.Clamp(delta, -maxDeltaFromRest, maxDeltaFromRest);
    }

    private Quaternion ResolveBindingBaseRestRotation(int index)
    {
        if (_bindingBaseRestLocalRotations == null || index < 0 || index >= _bindingBaseRestLocalRotations.Length)
        {
            return Quaternion.identity;
        }

        return _bindingBaseRestLocalRotations[index];
    }

    private Quaternion ResolveBindingPitchRestRotation(int index)
    {
        if (_bindingPitchRestLocalRotations == null || index < 0 || index >= _bindingPitchRestLocalRotations.Length)
        {
            return Quaternion.identity;
        }

        return _bindingPitchRestLocalRotations[index];
    }

    private Quaternion ResolveLegacyRestRotation(int index)
    {
        if (_legacyRestLocalRotations == null || index < 0 || index >= _legacyRestLocalRotations.Length)
        {
            return Quaternion.identity;
        }

        return _legacyRestLocalRotations[index];
    }

    private void ReturnToRest()
    {
        if (HasTurretBindings)
        {
            for (int i = 0; i < turretBindings.Length; i++)
            {
                Transform basePivot = turretBindings[i].baseYawPivot;
                if (basePivot != null)
                {
                    basePivot.localRotation = RotateToward(basePivot.localRotation, ResolveBindingBaseRestRotation(i), yawDegreesPerSecond);
                }

                Transform pitchPivot = turretBindings[i].pitchPivot;
                if (pitchPivot != null)
                {
                    pitchPivot.localRotation = RotateToward(pitchPivot.localRotation, ResolveBindingPitchRestRotation(i), pitchDegreesPerSecond);
                }
            }

            return;
        }

        for (int i = 0; i < turretPivots.Length; i++)
        {
            Transform pivot = turretPivots[i];
            if (pivot == null)
            {
                continue;
            }

            pivot.localRotation = RotateToward(pivot.localRotation, ResolveLegacyRestRotation(i), yawDegreesPerSecond);
        }
    }

    private static Quaternion RotateToward(Quaternion current, Quaternion target, float degreesPerSecond)
    {
        if (degreesPerSecond <= 0f)
        {
            return target;
        }

        return Quaternion.RotateTowards(current, target, degreesPerSecond * Time.deltaTime);
    }

    private static bool IsTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }
}
