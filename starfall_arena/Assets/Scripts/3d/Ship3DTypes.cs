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

    [Header("Handling Parameters")]
    public float pitchSpeed;
    public float yawSpeed;
    public bool invertY;
    [Range(0f, 1f)]
    public float minRotationMultiplierAtMaxSpeed;
}

[System.Serializable]
public struct ShipFlightAssistConfig3D
{
    [Header("Flight Assist (Friction)")]
    [Tooltip("How fast velocity bleeds off when not thrusting (units/s^2). Does not affect max speed while thrusting.")]
    public float frictionDeceleration;
    [Tooltip("Angular damping applied to rotation when friction is active.")]
    public float activeAngularDamping;
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
    [Tooltip("How strongly yaw angular velocity drives the bank. Negative values invert the direction.")]
    public float bankSensitivity;
    [Tooltip("Smoothing speed for bank interpolation.")]
    public float bankSmoothing;

    [Header("Pitch Lean")]
    [Tooltip("Maximum additional pitch lean applied to the visual model when pitching.")]
    public float maxPitchLeanAngle;
    [Tooltip("How strongly pitch angular velocity drives the lean. Negative values invert the direction.")]
    public float pitchLeanSensitivity;
    [Tooltip("Smoothing speed for pitch lean interpolation.")]
    public float pitchLeanSmoothing;

    [Header("Acceleration Response")]
    [Tooltip("How strongly forward/backward linear acceleration drives pitch lean (thrust start/stop, braking).")]
    public float forwardAccelPitchSensitivity;
    [Tooltip("How strongly lateral linear acceleration drives banking (centripetal force from turning at speed).")]
    public float lateralAccelBankSensitivity;
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
public struct ShipSpeedEffects3DConfig
{
    [Header("Speed VFX")]
    public ParticleSystem speedDustParticles;
    public float maxDustEmissionRate;
    [Range(0f, 1f)]
    public float dustSpeedThreshold;
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
}

[System.Serializable]
public struct ProjectileWeaponConfig3D
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform[] muzzles;
    public string targetTag;

    [Header("Combat")]
    public float cooldown;
    public float speed;
    public float damage;
    public float lifetime;
    public float impactForce;
    public float recoilForce;
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
