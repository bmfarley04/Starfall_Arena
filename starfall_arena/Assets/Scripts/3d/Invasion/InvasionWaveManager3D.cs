using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private WaveConfig3D[] waves = new WaveConfig3D[0];
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [SerializeField] private bool startOnEnable = true;
    [SerializeField] private float timeBetweenWaves = 3f;

    private readonly List<Enemy3D> _aliveEnemies = new List<Enemy3D>();
    private Coroutine _waveRoutine;
    private int _spawnPointIndex;

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
            yield return SpawnWave(waves[waveIndex]);
            yield return new WaitUntil(() => _aliveEnemies.Count == 0);

            if (waveIndex < waves.Length - 1 && timeBetweenWaves > 0f)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        _waveRoutine = null;
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

    private void SpawnEnemy(GameObject enemyPrefab)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[InvasionWaveManager3D] Wave entry skipped because enemyPrefab is missing.", this);
            return;
        }

        Transform spawnPoint = ResolveSpawnPoint();
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        GameObject enemyObject = Instantiate(enemyPrefab, spawnPosition, spawnRotation);

        if (NetTickUtil.IsActive)
        {
            NetworkObject networkObject = enemyObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[InvasionWaveManager3D] Networked enemy prefab '{enemyPrefab.name}' is missing NetworkObject.", enemyObject);
                Destroy(enemyObject);
                return;
            }

            networkObject.Spawn(true);
        }

        Enemy3D enemy = enemyObject.GetComponent<Enemy3D>();
        if (enemy == null)
        {
            Debug.LogError($"[InvasionWaveManager3D] Enemy prefab '{enemyPrefab.name}' is missing Enemy3D.", enemyObject);
            Destroy(enemyObject);
            return;
        }

        TrackEnemy(enemy);
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
    }

    private bool HasSpawnAuthority()
    {
        return !NetTickUtil.IsActive
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }
}
