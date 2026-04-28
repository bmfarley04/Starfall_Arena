using UnityEngine;

[DisallowMultipleComponent]
public class MissileLauncherYawTracker3D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional target sensor used to find the player-team target the launchers should face. Auto-assigned from this GameObject when left empty.")]
    [SerializeField] private EnemyTargetSensor3D targetSensor;

    [Tooltip("Optional missile weapon used to auto-fill launcher pivots from its configured muzzles. Leave empty if Launcher Pivots are assigned manually.")]
    [SerializeField] private MissileWeaponEnemy3D missileWeapon;

    [Tooltip("Launcher or muzzle transforms that should yaw toward the current target. If empty and Missile Weapon is assigned, the missile weapon's configured muzzles are used.")]
    [SerializeField] private Transform[] launcherPivots;

    [Header("Yaw Tracking")]
    [Tooltip("If true, the component rotates only local Y on each launcher and preserves its authored local X/Z rotation.")]
    [SerializeField] private bool yawOnly = true;

    [Tooltip("Degrees per second used to turn each launcher toward the target. Set to 0 for instant tracking.")]
    [SerializeField] private float yawDegreesPerSecond = 180f;

    [Tooltip("Extra local yaw offset applied after target yaw is solved. Use this if the model's launcher-forward axis is not local +Z.")]
    [SerializeField] private float localYawOffsetDegrees = 0f;

    [Tooltip("Maximum yaw delta allowed away from each launcher's starting local Y rotation. Set to 0 for unlimited travel.")]
    [SerializeField] private float maxYawFromRestDegrees = 0f;

    [Tooltip("Seconds between target refreshes. Visual tracking runs every frame, but target acquisition is throttled to avoid repeated scene-wide searches.")]
    [SerializeField] private float targetRefreshInterval = 0.1f;

    [Header("Idle")]
    [Tooltip("If true, launchers ease back to their authored starting rotation when no valid target is available.")]
    [SerializeField] private bool returnToRestWhenIdle = true;

    private Quaternion[] _restLocalRotations;
    private Entity3D _target;
    private float _nextTargetRefreshTime;

    private void Awake()
    {
        targetSensor ??= GetComponent<EnemyTargetSensor3D>();
        missileWeapon ??= GetComponent<StaggeredMissileWeaponEnemy3D>();
        missileWeapon ??= GetComponent<MissileWeaponEnemy3D>();
        ResolveLauncherPivotsIfNeeded();
        CacheRestRotations();
    }

    private void OnValidate()
    {
        yawDegreesPerSecond = Mathf.Max(0f, yawDegreesPerSecond);
        maxYawFromRestDegrees = Mathf.Max(0f, maxYawFromRestDegrees);
        targetRefreshInterval = Mathf.Max(0.02f, targetRefreshInterval);
    }

    private void OnEnable()
    {
        ResolveLauncherPivotsIfNeeded();
        CacheRestRotations();
        _nextTargetRefreshTime = 0f;
    }

    private void Update()
    {
        if (launcherPivots == null || launcherPivots.Length == 0)
        {
            ResolveLauncherPivotsIfNeeded();
            CacheRestRotations();
        }

        if (launcherPivots == null || launcherPivots.Length == 0)
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

    private void ResolveLauncherPivotsIfNeeded()
    {
        if (launcherPivots != null && launcherPivots.Length > 0)
        {
            return;
        }

        if (missileWeapon == null || missileWeapon.WeaponConfig.muzzles == null)
        {
            return;
        }

        launcherPivots = missileWeapon.WeaponConfig.muzzles;
    }

    private void CacheRestRotations()
    {
        if (launcherPivots == null)
        {
            _restLocalRotations = null;
            return;
        }

        if (_restLocalRotations != null && _restLocalRotations.Length == launcherPivots.Length)
        {
            return;
        }

        _restLocalRotations = new Quaternion[launcherPivots.Length];
        for (int i = 0; i < launcherPivots.Length; i++)
        {
            _restLocalRotations[i] = launcherPivots[i] != null ? launcherPivots[i].localRotation : Quaternion.identity;
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
        for (int i = 0; i < launcherPivots.Length; i++)
        {
            Transform pivot = launcherPivots[i];
            if (pivot == null)
            {
                continue;
            }

            Quaternion desiredLocalRotation = ResolveDesiredLocalRotation(pivot, target.transform.position, i);
            pivot.localRotation = RotateToward(pivot.localRotation, desiredLocalRotation);
        }
    }

    private Quaternion ResolveDesiredLocalRotation(Transform pivot, Vector3 targetPosition, int pivotIndex)
    {
        Vector3 toTarget = targetPosition - pivot.position;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        if (!yawOnly)
        {
            Quaternion worldRotation = Quaternion.LookRotation(toTarget.normalized, transform.up);
            return pivot.parent != null
                ? Quaternion.Inverse(pivot.parent.rotation) * worldRotation
                : worldRotation;
        }

        Transform parent = pivot.parent != null ? pivot.parent : transform;
        Vector3 localDirection = parent.InverseTransformDirection(toTarget.normalized);
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude <= 0.0001f)
        {
            return pivot.localRotation;
        }

        float desiredYaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg + localYawOffsetDegrees;
        Quaternion restRotation = ResolveRestRotation(pivotIndex);
        Vector3 restEuler = restRotation.eulerAngles;
        float yaw = ResolveConstrainedYaw(restEuler.y, desiredYaw);
        return Quaternion.Euler(restEuler.x, yaw, restEuler.z);
    }

    private float ResolveConstrainedYaw(float restYaw, float desiredYaw)
    {
        if (maxYawFromRestDegrees <= 0f)
        {
            return desiredYaw;
        }

        float delta = Mathf.DeltaAngle(restYaw, desiredYaw);
        return restYaw + Mathf.Clamp(delta, -maxYawFromRestDegrees, maxYawFromRestDegrees);
    }

    private Quaternion ResolveRestRotation(int pivotIndex)
    {
        if (_restLocalRotations == null || pivotIndex < 0 || pivotIndex >= _restLocalRotations.Length)
        {
            return Quaternion.identity;
        }

        return _restLocalRotations[pivotIndex];
    }

    private void ReturnToRest()
    {
        for (int i = 0; i < launcherPivots.Length; i++)
        {
            Transform pivot = launcherPivots[i];
            if (pivot == null)
            {
                continue;
            }

            pivot.localRotation = RotateToward(pivot.localRotation, ResolveRestRotation(i));
        }
    }

    private Quaternion RotateToward(Quaternion current, Quaternion target)
    {
        if (yawDegreesPerSecond <= 0f)
        {
            return target;
        }

        return Quaternion.RotateTowards(current, target, yawDegreesPerSecond * Time.deltaTime);
    }

    private static bool IsTargetValid(Entity3D target)
    {
        return target != null
            && target.CurrentHealth > 0f
            && target.gameObject.activeInHierarchy;
    }
}
