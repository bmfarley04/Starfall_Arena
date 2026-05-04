using UnityEngine;

public class HelixSpiralProjectileWeaponEnemy3D : EnemyProjectileWeaponBase3D
{
    [Header("Helix Pattern")]
    [Tooltip("Maximum shots the Siege Carrier fires during one helix spiral barrage.")]
    [SerializeField, Min(1)] private int shotCount = 18;

    [Tooltip("Seconds between helix shots. This also gates this component's firing cadence during the pattern.")]
    [SerializeField, Min(0.01f)] private float shotInterval = 0.08f;

    [Tooltip("Angle away from direct target aim used as the radius of the spiral cone. Larger values make a wider corkscrew.")]
    [SerializeField, Range(0f, 45f)] private float spiralConeAngle = 7f;

    [Tooltip("Degrees the spiral rotates around the base aim direction between each shot.")]
    [SerializeField] private float degreesPerShot = 24f;

    [Tooltip("If true, helix shots lead the target before applying the spiral offset.")]
    [SerializeField] private bool useLeadAim = true;

    [Tooltip("Projectile speed used for helix lead calculation. If 0, this component's configured projectile speed is used.")]
    [SerializeField, Min(0f)] private float leadProjectileSpeed;

    [Tooltip("Multiplier applied to calculated projectile travel time when leading helix shots.")]
    [SerializeField, Range(0f, 2f)] private float leadTimeScale = 1f;

    [Tooltip("Extra seconds of target-velocity lead added to each helix shot.")]
    [SerializeField, Min(0f)] private float additionalLeadSeconds = 0.02f;

    [Tooltip("Maximum total seconds of target-velocity lead allowed for one helix shot.")]
    [SerializeField, Min(0f)] private float maxLeadSeconds = 1f;

    [Tooltip("If true, each helix activation alternates clockwise/counter-clockwise so the pattern does not always carve the same path.")]
    [SerializeField] private bool reverseEveryActivation = true;

    [Header("Muzzle Sequence")]
    [Tooltip("If true, each helix shot uses one configured muzzle and advances to the next muzzle. If false, every shot fires all configured muzzles.")]
    [SerializeField] private bool fireOneMuzzlePerShot = true;

    [Tooltip("Starting muzzle index used when this component is enabled. Leave at 0 for the first configured muzzle.")]
    [SerializeField, Min(0)] private int startingMuzzleIndex;

    private readonly Transform[] _singleMuzzle = new Transform[1];
    private int _nextMuzzleIndex;
    private float _nextFireTime = float.NegativeInfinity;
    private Entity3D _shotTarget;
    private int _shotIndex;
    private int _activationIndex;
    private bool _hasPreparedShot;

    public override NetProjectileVisualType3D NetworkVisualType => NetProjectileVisualType3D.EnemyHelixProjectile;
    public int ShotCount => Mathf.Max(1, shotCount);
    public float ShotInterval => Mathf.Max(0.01f, shotInterval);

    public override bool IsFireGateReady => Time.time >= _nextFireTime && HasValidProjectilePrefab();

    private void OnEnable()
    {
        ResetMuzzleSequence();
    }

    private void OnValidate()
    {
        shotCount = Mathf.Max(1, shotCount);
        shotInterval = Mathf.Max(0.01f, shotInterval);
        spiralConeAngle = Mathf.Clamp(spiralConeAngle, 0f, 45f);
        leadProjectileSpeed = Mathf.Max(0f, leadProjectileSpeed);
        leadTimeScale = Mathf.Clamp(leadTimeScale, 0f, 2f);
        additionalLeadSeconds = Mathf.Max(0f, additionalLeadSeconds);
        maxLeadSeconds = Mathf.Max(0f, maxLeadSeconds);
        startingMuzzleIndex = Mathf.Max(0, startingMuzzleIndex);
    }

    public override bool TryConsumeFireGate()
    {
        if (Time.time < _nextFireTime || !HasValidProjectilePrefab())
        {
            return false;
        }

        _nextFireTime = Time.time + ShotInterval;
        return true;
    }

    public void ResetMuzzleSequence()
    {
        _nextMuzzleIndex = Mathf.Max(0, startingMuzzleIndex);
        _nextFireTime = float.NegativeInfinity;
        ClearPreparedShot();
    }

    public void PrepareHelixShot(Entity3D target, int shotIndex, int activationIndex)
    {
        _shotTarget = target;
        _shotIndex = Mathf.Max(0, shotIndex);
        _activationIndex = activationIndex;
        _hasPreparedShot = true;
    }

    public Vector3 ResolveHelixFireDirection(Transform origin, Entity3D target, int shotIndex, int activationIndex)
    {
        Vector3 originPosition = origin != null ? origin.position : transform.position;
        Vector3 fallbackForward = origin != null && origin.forward.sqrMagnitude > 0.0001f ? origin.forward.normalized : transform.forward;
        return ResolveHelixFireDirection(originPosition, fallbackForward, target, shotIndex, activationIndex);
    }

    protected override void ConfigureFireRequest(ref NetProjectileFireRequest3D fire, int muzzleIndex, int muzzleCount, Transform muzzle, Vector3 fireDirection)
    {
        if (!_hasPreparedShot)
        {
            return;
        }

        Vector3 spawnOrigin = fire.SpawnPosition;
        Vector3 fallbackForward = muzzle != null && muzzle.forward.sqrMagnitude > 0.0001f ? muzzle.forward.normalized : fireDirection;
        Vector3 helixDirection = ResolveHelixFireDirection(spawnOrigin, fallbackForward, _shotTarget, _shotIndex, _activationIndex);
        fire.Direction = helixDirection;
        fire.SpawnRotation = Quaternion.LookRotation(helixDirection, ResolveStableUpVector(helixDirection));
        fire.MuzzleEffectRotation = fire.SpawnRotation;
    }

    protected override Transform[] ResolveFiringMuzzles()
    {
        if (!fireOneMuzzlePerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length == 0)
        {
            return base.ResolveFiringMuzzles();
        }

        int index = Mathf.Clamp(_nextMuzzleIndex, 0, WeaponConfig.muzzles.Length - 1);
        _singleMuzzle[0] = WeaponConfig.muzzles[index] != null ? WeaponConfig.muzzles[index] : transform;
        return _singleMuzzle;
    }

    protected override void OnLocalVolleyFired(int spawnedCount)
    {
        if (spawnedCount > 0)
        {
            AdvanceMuzzleIndex();
        }

        ClearPreparedShot();
    }

    protected override void OnNetworkVolleyBuilt(int requestCount)
    {
        if (requestCount > 0)
        {
            AdvanceMuzzleIndex();
        }

        ClearPreparedShot();
    }

    private void AdvanceMuzzleIndex()
    {
        if (!fireOneMuzzlePerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length <= 1)
        {
            return;
        }

        _nextMuzzleIndex = (_nextMuzzleIndex + 1) % WeaponConfig.muzzles.Length;
    }

    private void ClearPreparedShot()
    {
        _shotTarget = null;
        _shotIndex = 0;
        _activationIndex = 0;
        _hasPreparedShot = false;
    }

    private Vector3 ResolveHelixFireDirection(Vector3 originPosition, Vector3 fallbackForward, Entity3D target, int shotIndex, int activationIndex)
    {
        Vector3 baseDirection = ResolveDirectionToTarget(originPosition, target, fallbackForward);

        if (target != null && target.CurrentHealth > 0f && target.gameObject.activeInHierarchy)
        {
            Vector3 aimPoint = target.transform.position;
            if (useLeadAim)
            {
                Vector3 targetVelocity = ResolveTargetVelocity(target);
                float projectileSpeed = leadProjectileSpeed > 0f ? leadProjectileSpeed : WeaponConfig.speed;
                if (projectileSpeed > 0.0001f && targetVelocity.sqrMagnitude > 0.0001f)
                {
                    float travelTime = Vector3.Distance(originPosition, target.transform.position) / projectileSpeed;
                    float leadSeconds = Mathf.Clamp((travelTime * leadTimeScale) + additionalLeadSeconds, 0f, maxLeadSeconds);
                    aimPoint += targetVelocity * leadSeconds;
                }
            }

            Vector3 aimDirection = aimPoint - originPosition;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                baseDirection = aimDirection.normalized;
            }
        }

        if (spiralConeAngle <= 0.001f)
        {
            return baseDirection;
        }

        Vector3 referenceUp = Mathf.Abs(Vector3.Dot(baseDirection, Vector3.up)) > 0.95f ? transform.right : Vector3.up;
        Vector3 radialAxis = Vector3.Cross(baseDirection, referenceUp);
        if (radialAxis.sqrMagnitude <= 0.0001f)
        {
            radialAxis = Vector3.Cross(baseDirection, Vector3.right);
        }

        radialAxis.Normalize();
        float directionSign = reverseEveryActivation && (activationIndex % 2) == 0 ? -1f : 1f;
        float spinDegrees = shotIndex * degreesPerShot * directionSign;
        Vector3 rotatedRadialAxis = Quaternion.AngleAxis(spinDegrees, baseDirection) * radialAxis;
        Vector3 helixDirection = Quaternion.AngleAxis(spiralConeAngle, rotatedRadialAxis) * baseDirection;
        return helixDirection.sqrMagnitude > 0.0001f ? helixDirection.normalized : baseDirection;
    }

    private Vector3 ResolveStableUpVector(Vector3 direction)
    {
        Vector3 up = transform.up;
        if (up.sqrMagnitude <= 0.0001f || Mathf.Abs(Vector3.Dot(up.normalized, direction.normalized)) > 0.995f)
        {
            up = Vector3.up;
        }

        return up;
    }

    private static Vector3 ResolveDirectionToTarget(Vector3 originPosition, Entity3D target, Vector3 fallbackForward)
    {
        if (target == null || target.CurrentHealth <= 0f || !target.gameObject.activeInHierarchy)
        {
            return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
        }

        Vector3 direction = target.transform.position - originPosition;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallbackForward.normalized;
    }

    private static Vector3 ResolveTargetVelocity(Entity3D target)
    {
        Rigidbody rb = target != null ? target.GetComponent<Rigidbody>() : null;
        return rb != null ? rb.linearVelocity : Vector3.zero;
    }
}
