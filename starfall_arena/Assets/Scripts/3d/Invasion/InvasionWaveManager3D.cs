using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionWaveManager3D : MonoBehaviour
{
    [System.Serializable]
    private class WaveEnemyEntry3D
    {
        public GameObject enemyPrefab = null;
        public int count = 0;
        public float spawnDelay = 0f;

        public WaveEnemyEntry3D(GameObject enemyPrefab, int count, float spawnDelay)
        {
            this.enemyPrefab = enemyPrefab;
            this.count = count;
            this.spawnDelay = spawnDelay;
        }
    }

    [System.Serializable]
    private class WaveConfig3D
    {
        public string waveName = "";
        public WaveEnemyEntry3D[] enemies = new WaveEnemyEntry3D[0];

        public WaveConfig3D(string waveName, WaveEnemyEntry3D[] enemies)
        {
            this.waveName = waveName;
            this.enemies = enemies;
        }
    }

    [Header("Waves")]
    [Tooltip("Finite Invasion waves. Each wave spawns its entries in order, then waits until every tracked enemy from that wave and any tracked child spawns are dead before advancing.")]
    [SerializeField] private WaveConfig3D[] waves = new WaveConfig3D[0];
    [Tooltip("Authored enemy spawn points used in round-robin order. If empty, enemies spawn at this manager's transform.")]
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [Tooltip("If enabled, this manager starts waves as soon as it is enabled. Networked Invasion scenes should usually leave this off so InvasionSceneManager3D can spawn players and show WAVE text first.")]
    [SerializeField] private bool startOnEnable = true;
    [Tooltip("Seconds to wait after a wave is fully cleared before requesting the next wave intro.")]
    [SerializeField] private float timeBetweenWaves = 3f;

    private readonly List<Enemy3D> _aliveEnemies = new List<Enemy3D>();
    private Coroutine _waveRoutine;
    private int _spawnPointIndex;

    public event Func<int, IEnumerator> WaveIntroRequested;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action AllWavesCleared;
    public event Action<int> AliveEnemyCountChanged;

    public int AliveEnemyCount => _aliveEnemies.Count;
    public int WaveCount => waves != null ? waves.Length : 0;
    public bool IsRunning => _waveRoutine != null;

    private void OnEnable()
    {
        if (startOnEnable)
        {
            StartWaves();
        }
    }

    private void OnDisable()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }

        ClearTrackedEnemies();
    }

    public void StartWaves()
    {
        if (_waveRoutine != null || !HasSpawnAuthority())
        {
            return;
        }

        _waveRoutine = StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            int waveNumber = waveIndex + 1;
            yield return RunWaveIntro(waveNumber);
            WaveStarted?.Invoke(waveNumber);
            yield return SpawnWave(waves[waveIndex]);
            yield return new WaitUntil(() => _aliveEnemies.Count == 0);
            WaveCleared?.Invoke(waveNumber);

            if (waveIndex < waves.Length - 1 && timeBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        _waveRoutine = null;
        AllWavesCleared?.Invoke();
    }

    private IEnumerator RunWaveIntro(int waveNumber)
    {
        Func<int, IEnumerator> introHandler = WaveIntroRequested;
        if (introHandler == null)
        {
            yield break;
        }

        Delegate[] handlers = introHandler.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is Func<int, IEnumerator> handler)
            {
                yield return handler(waveNumber);
            }
        }
    }

    private IEnumerator SpawnWave(WaveConfig3D wave)
    {
        if (wave.enemies == null)
        {
            yield break;
        }

        for (int i = 0; i < wave.enemies.Length; i++)
        {
            WaveEnemyEntry3D entry = wave.enemies[i];
            int count = Mathf.Max(0, entry.count);
            for (int enemyIndex = 0; enemyIndex < count; enemyIndex++)
            {
                SpawnEnemy(entry.enemyPrefab);
                if (entry.spawnDelay > 0f)
                {
                    yield return new WaitForSeconds(entry.spawnDelay);
                }
            }
        }
    }

    public Enemy3D SpawnEnemyAt(GameObject enemyPrefab, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        return SpawnEnemyAt(enemyPrefab, spawnPosition, spawnRotation, null);
    }

    public Enemy3D SpawnEnemyAt(GameObject enemyPrefab, Vector3 spawnPosition, Quaternion spawnRotation, Action<GameObject> configureBeforeNetworkSpawn)
    {
        if (!HasSpawnAuthority())
        {
            return null;
        }

        if (enemyPrefab == null)
        {
            Debug.LogWarning("[InvasionWaveManager3D] Enemy spawn skipped because enemyPrefab is missing.", this);
            return null;
        }

        GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
        configureBeforeNetworkSpawn?.Invoke(enemyObject);

        if (NetTickUtil.IsActive)
        {
            NetworkObject networkObject = enemyObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[InvasionWaveManager3D] Networked enemy prefab '{enemyPrefab.name}' is missing NetworkObject.", enemyObject);
                Destroy(enemyObject);
                return null;
            }

            networkObject.Spawn(true);
        }

        Enemy3D enemy = enemyObject.GetComponent<Enemy3D>();
        if (enemy == null)
        {
            Debug.LogError($"[InvasionWaveManager3D] Enemy prefab '{enemyPrefab.name}' is missing Enemy3D.", enemyObject);
            Destroy(enemyObject);
            return null;
        }

        TrackEnemy(enemy);
        return enemy;
    }

    private Enemy3D SpawnEnemy(GameObject enemyPrefab)
    {
        Transform spawnPoint = ResolveSpawnPoint();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        return SpawnEnemyAt(enemyPrefab, spawnPosition, spawnRotation);
    }

    private Transform ResolveSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        Transform spawnPoint = spawnPoints[_spawnPointIndex % spawnPoints.Length];
        _spawnPointIndex++;
        return spawnPoint;
    }

    private void TrackEnemy(Enemy3D enemy)
    {
        if (enemy == null || _aliveEnemies.Contains(enemy))
        {
            return;
        }

        _aliveEnemies.Add(enemy);
        enemy.Died += HandleEnemyDied;
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private void HandleEnemyDied(Entity3D entity)
    {
        Enemy3D enemy = entity as Enemy3D;
        if (enemy == null)
        {
            return;
        }

        enemy.Died -= HandleEnemyDied;
        _aliveEnemies.Remove(enemy);
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private void ClearTrackedEnemies()
    {
        for (int i = 0; i < _aliveEnemies.Count; i++)
        {
            if (_aliveEnemies[i] != null)
            {
                _aliveEnemies[i].Died -= HandleEnemyDied;
            }
        }

        _aliveEnemies.Clear();
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private bool HasSpawnAuthority()
    {
        return !NetTickUtil.IsActive
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }
}
