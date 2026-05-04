using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawnerWeapon3D : MonoBehaviour
{
    [Header("Spawn Sequence")]
    [Tooltip("Enemy prefab to spawn. For networked Invasion, this prefab must have NetworkObject and be registered with NGO.")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("How many enemies to spawn each time this spawner sequence starts.")]
    [SerializeField] private int spawnCount = 1;

    [Tooltip("Transform where spawned enemies appear. If empty, this component's transform is used.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Seconds between each spawned enemy in this sequence.")]
    [SerializeField] private float delayBetweenSpawns = 0.5f;

    [Tooltip("If true, the spawned enemy uses the spawn point's rotation. If false, it uses this spawner's rotation.")]
    [SerializeField] private bool useSpawnPointRotation = true;

    [Tooltip("If true, start the sequence when this component becomes enabled. Useful for simple test prefabs or one-shot carrier enemies.")]
    [SerializeField] private bool spawnOnEnable;

    [Header("Invasion Tracking")]
    [Tooltip("Wave manager used to spawn and track enemies. If empty, the first active InvasionWaveManager3D is found at runtime.")]
    [SerializeField] private InvasionWaveManager3D waveManager;

    [Tooltip("If true, logs missing prefab, spawn authority, or wave-manager setup warnings.")]
    [SerializeField] private bool logSetupWarnings = true;

    private Coroutine _spawnRoutine;

    public bool IsSpawning => _spawnRoutine != null;
    public int SpawnCount => Mathf.Max(0, spawnCount);

    private void OnEnable()
    {
        if (spawnOnEnable)
        {
            BeginSpawning();
        }
    }

    private void OnDisable()
    {
        if (_spawnRoutine != null)
        {
            StopCoroutine(_spawnRoutine);
            _spawnRoutine = null;
        }
    }

    public bool BeginSpawning()
    {
        if (_spawnRoutine != null)
        {
            return false;
        }

        if (!HasSpawnAuthority())
        {
            LogSetupWarning("spawn sequence ignored because this peer does not have enemy spawn authority.");
            return false;
        }

        if (enemyPrefab == null)
        {
            LogSetupWarning("spawn sequence ignored because Enemy Prefab is not assigned.");
            return false;
        }

        if (SpawnCount <= 0)
        {
            LogSetupWarning("spawn sequence ignored because Spawn Count is 0.");
            return false;
        }

        InvasionWaveManager3D manager = ResolveWaveManager();
        if (manager == null)
        {
            LogSetupWarning("spawn sequence ignored because no active InvasionWaveManager3D was found.");
            return false;
        }

        _spawnRoutine = StartCoroutine(SpawnSequence(manager));
        return true;
    }

    public void StopSpawning()
    {
        if (_spawnRoutine == null)
        {
            return;
        }

        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    private IEnumerator SpawnSequence(InvasionWaveManager3D manager)
    {
        int count = SpawnCount;
        float delay = Mathf.Max(0f, delayBetweenSpawns);

        for (int i = 0; i < count; i++)
        {
            SpawnOne(manager);

            if (i < count - 1 && delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
        }

        _spawnRoutine = null;
    }

    private void SpawnOne(InvasionWaveManager3D manager)
    {
        Transform point = spawnPoint != null ? spawnPoint : transform;
        Quaternion spawnRotation = useSpawnPointRotation && spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        manager.SpawnEnemyAt(enemyPrefab, point.position, spawnRotation);
    }

    private InvasionWaveManager3D ResolveWaveManager()
    {
        if (waveManager != null)
        {
            return waveManager;
        }

#if UNITY_2023_1_OR_NEWER
        waveManager = FindFirstObjectByType<InvasionWaveManager3D>();
#else
        waveManager = FindObjectOfType<InvasionWaveManager3D>();
#endif
        return waveManager;
    }

    private bool HasSpawnAuthority()
    {
        return !NetTickUtil.IsActive
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }

    private void LogSetupWarning(string message)
    {
        if (!logSetupWarnings)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(EnemySpawnerWeapon3D)}] {name} {message}", this);
    }
}
