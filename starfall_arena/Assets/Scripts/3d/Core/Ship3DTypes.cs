using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IShipFlightInputSource
{
    Vector2 LookInput { get; }
    float ThrustInput { get; }
    bool ConsumeToggleFrictionPressed();
}

public interface IPooledObject3D
{
    void OnSpawnedFromPool();
    void OnDespawnedToPool();
}

public enum Faction3D : byte
{
    Neutral = 0,
    PlayerTeam = 1,
    EnemyTeam = 2
}

public enum ProjectileAimMode3D
{
    MuzzleForward,
    ScreenCenter
}

[System.Serializable]
public struct ShipFlightConfig3D
{
    [Header("Engine Parameters")]
    public float thrustAcceleration;
    public float maxSpeed;

    [Header("Input Response")]
    [Tooltip("How quickly raw look input converges toward the assisted steering intent.")]
    public float lookInputResponse;

    [Header("Handling Parameters")]
    public float pitchSpeed;
    public float yawSpeed;
    [Tooltip("How quickly pitch rate ramps up toward the target turn rate.")]
    public float pitchAcceleration;
    [Tooltip("How quickly pitch rate settles back down when the target turn rate changes or is released.")]
    public float pitchDeceleration;
    [Tooltip("How quickly yaw rate ramps up toward the target turn rate.")]
    public float yawAcceleration;
    [Tooltip("How quickly yaw rate settles back down when the target turn rate changes or is released.")]
    public float yawDeceleration;
    public bool invertY;
    [Range(0f, 1f)]
    public float minRotationMultiplierAtMaxSpeed;
}

[System.Serializable]
public struct ShipFlightAssistConfig3D
{
    [Header("Flight Assist (Friction)")]
    [Tooltip("How fast forward velocity bleeds off when not thrusting (units/s^2). When set to 0, passive linear assist is skipped so coasting preserves world-space momentum instead of being re-damped through the ship's local axes.")]
    public float frictionDeceleration;
    [Tooltip("Angular damping applied to rotation when friction is active.")]
    public float activeAngularDamping;

    [Header("Drift Assist")]
    [Tooltip("How aggressively sideways local drift is damped while the ship turns through space.")]
    public float lateralDriftDamping;
    [Tooltip("How aggressively local up/down drift is damped so climb/dive remains intentional instead of sloppy.")]
    public float verticalDriftDamping;
    [Tooltip("How strongly the ship's velocity aligns back toward its forward direction while thrusting.")]
    public float velocityAlignmentStrength;
}

[System.Serializable]
public struct VisualEffects3DConfig
{
    [Header("Visual Model")]
    [Tooltip("Child transform containing the ship mesh. Banking and pitch lean are applied here.")]
    public Transform visualModel;

    [Header("Banking (Roll)")]
    [Tooltip("Maximum roll angle applied to the visual model when yawing.")]
    public float maxBankAngle;
    [Tooltip("How strongly actual yaw turn rate drives the bank. Negative values invert the direction.")]
    public float bankSensitivity;
    [Tooltip("How strongly steering intent drives bank before the rigidbody has fully turned.")]
    public float steeringInputBankSensitivity;
    [Tooltip("Smoothing speed for bank interpolation.")]
    public float bankSmoothing;
    [Tooltip("How quickly bank settles back toward neutral once the target bank relaxes.")]
    public float bankReturnSmoothing;

    [Header("Pitch Lean")]
    [Tooltip("Maximum additional pitch lean applied to the visual model when pitching.")]
    public float maxPitchLeanAngle;
    [Tooltip("How strongly actual pitch turn rate drives the lean. Negative values invert the direction.")]
    public float pitchLeanSensitivity;
    [Tooltip("How strongly steering intent drives pitch lean before the rigidbody fully responds.")]
    public float steeringInputPitchSensitivity;
    [Tooltip("Smoothing speed for pitch lean interpolation.")]
    public float pitchLeanSmoothing;
    [Tooltip("How quickly pitch lean settles back toward neutral once the target lean relaxes.")]
    public float pitchLeanReturnSmoothing;

    [Header("Acceleration Response")]
    [Tooltip("How strongly forward/backward linear acceleration drives pitch lean (thrust start/stop, braking).")]
    public float forwardAccelPitchSensitivity;
    [Tooltip("How strongly lateral linear acceleration drives banking (centripetal force from turning at speed).")]
    public float lateralAccelBankSensitivity;
    [Tooltip("How strongly persistent sideways drift contributes to visual bank.")]
    public float lateralDriftBankSensitivity;
    [Tooltip("How strongly persistent vertical drift contributes to pitch lean.")]
    public float verticalDriftPitchSensitivity;
}

[System.Serializable]
public struct ThrusterEffects3DConfig
{
    [Tooltip("Thruster particle systems attached to this ship.")]
    public List<ParticleSystem> thrusters;
    [Tooltip("Time to ramp thruster emission up/down in seconds.")]
    public float rampTime;
    [Tooltip("Invert each thruster's original start color while active.")]
    public bool invertColors;
}

[System.Serializable]
public struct ShipSpeedTrailLayer3DConfig
{
    [Tooltip("Material applied to the generated trail renderer.")]
    public Material material;
    [Tooltip("Minimum trail lifetime when the ship is barely fast enough to emit.")]
    public float minLifetime;
    [Tooltip("Maximum trail lifetime at top speed.")]
    public float maxLifetime;
    [Tooltip("Minimum trail width multiplier when the ship first crosses the speed threshold.")]
    public float minWidth;
    [Tooltip("Maximum trail width multiplier at top speed.")]
    public float maxWidth;
    [Tooltip("Optional custom width curve. Defaults to a short expand-then-taper profile when left empty.")]
    public AnimationCurve widthCurve;
    [Tooltip("Optional custom trail color gradient. Defaults to a cyan-white fade when left empty.")]
    public Gradient colorGradient;
    [Tooltip("Minimum vertex spacing used by the trail renderer.")]
    public float minVertexDistance;
    [Tooltip("Additional rounded geometry at trail corners.")]
    public int cornerVertices;
    [Tooltip("Additional rounded geometry at trail ends.")]
    public int endCapVertices;
    [Tooltip("How the trail texture is applied along the ribbon.")]
    public LineTextureMode textureMode;
    [Tooltip("How the trail faces the camera.")]
    public LineAlignment alignment;
}

[System.Serializable]
public struct ShipSpeedEffects3DConfig
{
    [Header("Speed VFX")]
    public ParticleSystem speedDustParticles;
    public float maxDustEmissionRate;
    [Range(0f, 1f)]
    public float dustSpeedThreshold;

    [Header("Wing Trails")]
    [Tooltip("Transforms authored near the wing tips where the speed trails should originate.")]
    public List<Transform> wingTrailSources;
    [Range(0f, 1f)]
    public float trailSpeedThreshold;
    [Tooltip("How quickly the trail intensity catches up to the current speed ratio.")]
    public float trailRampTime;
    public ShipSpeedTrailLayer3DConfig coreTrail;
    public ShipSpeedTrailLayer3DConfig softTrail;
}

[System.Serializable]
public struct PlayerCameraRigConfig3D
{
    [Header("Dynamic Camera Settings")]
    public float minZOffset;
    public float maxZOffset;
    public float minFOV;
    public float maxFOV;
    public float cameraLerpSpeed;

    [Header("Turn Composition")]
    [Tooltip("How far the camera can bias horizontally during hard yaw turns.")]
    public float horizontalTurnOffset;
    [Tooltip("How far the camera can bias vertically during hard pitch maneuvers.")]
    public float verticalTurnOffset;
    [Tooltip("How much current yaw turn rate reinforces the horizontal camera offset.")]
    public float yawRateOffsetContribution;
    [Tooltip("How much current pitch turn rate reinforces the vertical camera offset.")]
    public float pitchRateOffsetContribution;
    [Tooltip("How quickly the camera pushes into its turn-biased offset.")]
    public float turnOffsetLerpSpeed;
    [Tooltip("How quickly the camera recenters after steering relaxes.")]
    public float recenterLerpSpeed;

    [Header("Follow Damping")]
    [Tooltip("Position damping while flying mostly straight.")]
    public float followPositionDampingAtRest;
    [Tooltip("Position damping during aggressive steering.")]
    public float followPositionDampingDuringTurn;
    [Tooltip("Rotation damping while flying mostly straight.")]
    public float followRotationDampingAtRest;
    [Tooltip("Rotation damping during aggressive steering.")]
    public float followRotationDampingDuringTurn;
    [Tooltip("Aim damping while flying mostly straight.")]
    public float aimDampingAtRest;
    [Tooltip("Aim damping during aggressive steering.")]
    public float aimDampingDuringTurn;
}

[System.Serializable]
public struct ProjectileWeaponConfig3D
{
    [Header("Projectile")]
    [Tooltip("Projectile prefab spawned by this weapon. The prefab must contain a Projectile3D-derived component.")]
    public GameObject projectilePrefab;
    [Tooltip("One or more muzzle transforms. When left empty, the weapon falls back to its own transform.")]
    public Transform[] muzzles;
    [Tooltip("Legacy tag fallback used by older duel projectile paths when no explicit faction is supplied.")]
    public string targetTag;
    [Tooltip("Preferred gameplay target faction for this projectile. Enemy AI weapons should usually target PlayerTeam.")]
    public Faction3D targetFaction;

    [Header("Combat")]
    [Tooltip("Seconds between shots or volleys.")]
    public float cooldown;
    [Tooltip("Projectile travel speed before inherited ship velocity is added.")]
    public float speed;
    [Tooltip("Damage dealt on hit before shields/hull split the result.")]
    public float damage;
    [Tooltip("Maximum projectile lifetime in seconds before despawn.")]
    public float lifetime;
    [Tooltip("Optional impact force passed into projectile hit handling.")]
    public float impactForce;
    [Tooltip("Optional recoil impulse applied back to the firing ship.")]
    public float recoilForce;
    [Tooltip("Overheat added per base projectile fired. This is ignored by ability-specific projectile requests for now.")]
    public float energyCost;
}

public struct ProjectileFireRequest3D
{
    public GameObject projectilePrefab;
    public Transform[] muzzles;
    public Transform spawnAnchor;
    public string targetTag;
    public Faction3D targetFaction;
    public float speed;
    public float damage;
    public float lifetime;
    public float impactForce;
    public float recoilForce;
    public float forwardOffset;
    public float verticalOffset;
    public bool canPierce;
    public float pierceMultiplier;
    public bool appliesSlow;
    public float slowMultiplier;
    public float slowDuration;
    public float slowEngineEmissionScale;
    public float projectileScaleMultiplier;
    public int accuracyAttackIdOverride;
    public System.Action<Projectile3D> onProjectileSpawned;
}

public class PooledObject3D : MonoBehaviour
{
    [SerializeField] private GameObject sourcePrefab;

    private Coroutine _scheduledDespawnRoutine;
    private ParticleSystem[] _particleSystems;
    private bool _transformBaselineCached;
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;

    public GameObject SourcePrefab
    {
        get => sourcePrefab;
        set => sourcePrefab = value;
    }

    public Vector3 InitialLocalPosition
    {
        get
        {
            CacheTransformBaselineIfNeeded();
            return _initialLocalPosition;
        }
    }

    public Quaternion InitialLocalRotation
    {
        get
        {
            CacheTransformBaselineIfNeeded();
            return _initialLocalRotation;
        }
    }

    public Vector3 InitialLocalScale
    {
        get
        {
            CacheTransformBaselineIfNeeded();
            return _initialLocalScale;
        }
    }

    public void NotifySpawned()
    {
        CacheTransformBaselineIfNeeded();
        StopScheduledDespawn();
        CacheParticleSystemsIfNeeded();
        RestartParticleSystems();
        BroadcastToPoolListeners(true);
    }

    public void NotifyDespawned()
    {
        CacheTransformBaselineIfNeeded();
        StopScheduledDespawn();
        CacheParticleSystemsIfNeeded();
        StopParticleSystems();
        BroadcastToPoolListeners(false);
    }

    public void ScheduleDespawn(float delay)
    {
        StopScheduledDespawn();

        if (delay <= 0f)
        {
            GameObjectPool3D.Despawn(gameObject);
            return;
        }

        _scheduledDespawnRoutine = StartCoroutine(DespawnAfterDelay(delay));
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObjectPool3D.Despawn(gameObject);
    }

    private void StopScheduledDespawn()
    {
        if (_scheduledDespawnRoutine != null)
        {
            StopCoroutine(_scheduledDespawnRoutine);
            _scheduledDespawnRoutine = null;
        }
    }

    private void BroadcastToPoolListeners(bool spawned)
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPooledObject3D pooledListener)
            {
                if (spawned)
                {
                    pooledListener.OnSpawnedFromPool();
                }
                else
                {
                    pooledListener.OnDespawnedToPool();
                }
            }
        }
    }

    private void CacheParticleSystemsIfNeeded()
    {
        if (_particleSystems == null)
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void CacheTransformBaselineIfNeeded()
    {
        if (_transformBaselineCached)
        {
            return;
        }

        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialLocalScale = transform.localScale;
        _transformBaselineCached = true;
    }

    private void RestartParticleSystems()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = _particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void StopParticleSystems()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = _particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}

public static class GameObjectPool3D
{
    private static readonly Dictionary<GameObject, Queue<PooledObject3D>> Pools = new();
    private static readonly Dictionary<GameObject, Transform> PoolRoots = new();
    private static Transform _globalRoot;

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        Queue<PooledObject3D> pool = GetOrCreatePool(prefab);
        while (pool.Count < count)
        {
            PooledObject3D pooled = CreateInstance(prefab);
            pooled.gameObject.SetActive(false);
            pool.Enqueue(pooled);
        }
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        Queue<PooledObject3D> pool = GetOrCreatePool(prefab);
        PooledObject3D pooled = null;

        while (pool.Count > 0 && pooled == null)
        {
            pooled = pool.Dequeue();
        }

        if (pooled == null)
        {
            pooled = CreateInstance(prefab);
        }

        Transform transform = pooled.transform;
        transform.SetParent(parent, false);
        transform.localScale = pooled.InitialLocalScale;
        transform.SetPositionAndRotation(position, rotation);
        pooled.gameObject.SetActive(true);
        pooled.NotifySpawned();
        return pooled.gameObject;
    }

    public static void Despawn(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledObject3D pooled = instance.GetComponent<PooledObject3D>();
        if (pooled == null || pooled.SourcePrefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        Queue<PooledObject3D> pool = GetOrCreatePool(pooled.SourcePrefab);
        pooled.NotifyDespawned();
        Transform transform = pooled.transform;
        transform.SetParent(GetOrCreatePoolRoot(pooled.SourcePrefab), false);
        transform.localPosition = pooled.InitialLocalPosition;
        transform.localRotation = pooled.InitialLocalRotation;
        transform.localScale = pooled.InitialLocalScale;
        instance.SetActive(false);
        pool.Enqueue(pooled);
    }

    private static Queue<PooledObject3D> GetOrCreatePool(GameObject prefab)
    {
        if (!Pools.TryGetValue(prefab, out Queue<PooledObject3D> pool))
        {
            pool = new Queue<PooledObject3D>();
            Pools[prefab] = pool;
        }

        return pool;
    }

    private static Transform GetOrCreatePoolRoot(GameObject prefab)
    {
        if (_globalRoot == null)
        {
            GameObject root = new GameObject("GameObjectPool3D");
            root.hideFlags = HideFlags.HideInHierarchy;
            _globalRoot = root.transform;
        }

        if (!PoolRoots.TryGetValue(prefab, out Transform poolRoot) || poolRoot == null)
        {
            GameObject root = new GameObject($"{prefab.name}_Pool");
            root.hideFlags = HideFlags.HideInHierarchy;
            root.transform.SetParent(_globalRoot, false);
            poolRoot = root.transform;
            PoolRoots[prefab] = poolRoot;
        }

        return poolRoot;
    }

    private static PooledObject3D CreateInstance(GameObject prefab)
    {
        GameObject instance = Object.Instantiate(prefab, GetOrCreatePoolRoot(prefab));
        PooledObject3D pooled = instance.GetComponent<PooledObject3D>();
        if (pooled == null)
        {
            pooled = instance.AddComponent<PooledObject3D>();
        }

        pooled.SourcePrefab = prefab;
        return pooled;
    }
}
