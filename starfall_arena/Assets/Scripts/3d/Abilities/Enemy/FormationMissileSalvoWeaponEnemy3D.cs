using UnityEngine;

[DisallowMultipleComponent]
public class FormationMissileSalvoWeaponEnemy3D : MissileWeaponEnemy3D
{
    [Header("Formation Salvo")]
    [Tooltip("Total missiles spawned by one salvo. If fewer muzzles are assigned, the weapon cycles through the available muzzles.")]
    [Range(2, 16)]
    [SerializeField] private int missileCount = 8;

    [Tooltip("Cone angle the ring opens away from the direct target line before converging. A wider angle is more dramatic but needs more dodge space.")]
    [Range(0f, 85f)]
    [SerializeField] private float fanArcDegrees = 50f;

    [Tooltip("Seconds spent opening from the launch direction into the fan formation.")]
    [SerializeField] private float fanOutDuration = 0.45f;

    [Tooltip("Seconds the missiles hold the wide formation before they begin collapsing toward the target.")]
    [SerializeField] private float formationHoldDuration = 0.2f;

    [Tooltip("Seconds used for the collapse phase. Higher values give the player more time to read and dodge the convergence.")]
    [SerializeField] private float convergenceDuration = 1.1f;

    [Tooltip("Lateral spread, in meters, used as the initial convergence offset around the target. This shrinks to zero by the end of the collapse.")]
    [SerializeField] private float convergenceRadius = 42f;

    [Tooltip("Closest target distance that still uses the full fan/hold/convergence timings. Below this, timings are compressed so close targets are not missed by an overlong flourish.")]
    [SerializeField] private float fullPatternDistance = 180f;

    [Tooltip("At or below this target distance, the salvo uses the Close Range Timing Scale.")]
    [SerializeField] private float closeRangeDistance = 55f;

    [Tooltip("Multiplier applied to fan, hold, and convergence timing at close range. Lower values make the pattern collapse sooner when the player is near the boss.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float closeRangeTimingScale = 0.45f;

    [Tooltip("Maximum speed multiplier a missile may use during convergence to keep all missiles arriving together. Higher values improve synchronization but can make the collapse feel sharper.")]
    [SerializeField] private float maxConvergenceSpeedMultiplier = 2.5f;

    [Tooltip("Optional transform whose forward/up axes define the salvo's formation plane. Leave empty to use this weapon's transform.")]
    [SerializeField] private Transform formationReference;

    private Transform[] _salvoMuzzles;

    public override NetProjectileVisualType3D NetworkVisualType => NetProjectileVisualType3D.EnemyFormationMissile;

    private void OnValidate()
    {
        missileCount = Mathf.Clamp(missileCount, 2, 16);
        fanArcDegrees = Mathf.Clamp(fanArcDegrees, 0f, 85f);
        fanOutDuration = Mathf.Max(0f, fanOutDuration);
        formationHoldDuration = Mathf.Max(0f, formationHoldDuration);
        convergenceDuration = Mathf.Max(0.01f, convergenceDuration);
        convergenceRadius = Mathf.Max(0f, convergenceRadius);
        fullPatternDistance = Mathf.Max(0.01f, fullPatternDistance);
        closeRangeDistance = Mathf.Clamp(closeRangeDistance, 0f, fullPatternDistance);
        closeRangeTimingScale = Mathf.Clamp(closeRangeTimingScale, 0.1f, 1f);
        maxConvergenceSpeedMultiplier = Mathf.Max(1f, maxConvergenceSpeedMultiplier);
    }

    protected override Transform[] ResolveFiringMuzzles()
    {
        int count = Mathf.Clamp(missileCount, 2, 16);
        if (_salvoMuzzles == null || _salvoMuzzles.Length != count)
        {
            _salvoMuzzles = new Transform[count];
        }

        Transform[] configuredMuzzles = WeaponConfig.muzzles;
        for (int i = 0; i < count; i++)
        {
            Transform muzzle = transform;
            if (configuredMuzzles != null && configuredMuzzles.Length > 0)
            {
                muzzle = configuredMuzzles[i % configuredMuzzles.Length] != null
                    ? configuredMuzzles[i % configuredMuzzles.Length]
                    : transform;
            }

            _salvoMuzzles[i] = muzzle;
        }

        return _salvoMuzzles;
    }

    protected override void ConfigureFireRequest(ref NetProjectileFireRequest3D fire, int muzzleIndex, int muzzleCount, Transform muzzle, Vector3 fireDirection)
    {
        fire.UsesFormation = true;
        fire.FormationSlotIndex = muzzleIndex;
        fire.FormationSlotCount = Mathf.Max(1, muzzleCount);
        fire.FormationFanArcDegrees = fanArcDegrees;
        float timingScale = ResolveTimingScale(fire.SpawnPosition, fire.Direction);
        fire.FormationFanOutDuration = fanOutDuration * timingScale;
        fire.FormationHoldDuration = formationHoldDuration * timingScale;
        fire.FormationConvergeDuration = Mathf.Max(0.05f, convergenceDuration * timingScale);
        fire.FormationConvergenceRadius = convergenceRadius;
        fire.FormationMaxSpeedMultiplier = maxConvergenceSpeedMultiplier;
    }

    protected override void ConfigureSpawnedProjectile(Projectile3D projectile, NetProjectileFireRequest3D fire)
    {
        if (!fire.UsesFormation || projectile is not MissileProjectile3D missile)
        {
            return;
        }

        Transform reference = formationReference != null ? formationReference : transform;
        Vector3 formationForward = fire.Direction.sqrMagnitude > 0.0001f ? fire.Direction.normalized : reference.forward;
        missile.ConfigureFormationGuidance(
            fire.FormationSlotIndex,
            fire.FormationSlotCount,
            fire.FormationFanArcDegrees,
            fire.FormationFanOutDuration,
            fire.FormationHoldDuration,
            fire.FormationConvergeDuration,
            fire.FormationConvergenceRadius,
            fire.FormationMaxSpeedMultiplier,
            formationForward,
            reference.up);
    }

    private float ResolveTimingScale(Vector3 spawnPosition, Vector3 fireDirection)
    {
        float distance = ResolveTargetDistance(spawnPosition, fireDirection);
        if (distance >= fullPatternDistance)
        {
            return 1f;
        }

        if (distance <= closeRangeDistance)
        {
            return closeRangeTimingScale;
        }

        float t = Mathf.InverseLerp(closeRangeDistance, fullPatternDistance, distance);
        return Mathf.Lerp(closeRangeTimingScale, 1f, t);
    }

    private float ResolveTargetDistance(Vector3 spawnPosition, Vector3 fireDirection)
    {
        Transform target = ResolveBestFactionTarget(spawnPosition);
        if (target != null)
        {
            return Vector3.Distance(spawnPosition, target.position);
        }

        return Mathf.Max(0f, WeaponConfig.speed) * Mathf.Max(0.05f, convergenceDuration);
    }

    private Transform ResolveBestFactionTarget(Vector3 spawnPosition)
    {
        Entity3D[] entities = FindObjectsByType<Entity3D>(FindObjectsSortMode.None);
        Transform bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D candidate = entities[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (FactionMember3D.ResolveFaction(candidate) != WeaponConfig.targetFaction)
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - spawnPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestTarget = candidate.transform;
        }

        return bestTarget;
    }
}
