using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class InvasionWaveManager3D : MonoBehaviour
{
    private enum FormationPreset3D
    {
        Line = 0,
        Wedge = 1,
        Ring = 2,
        Grid = 3
    }

    [Serializable]
    private class SubWaveEnemyEntry3D
    {
        [Tooltip("Enemy prefab assigned to the next available formation slots for this sub-wave. In networked Invasion this prefab must have NetworkObject and be registered with NGO.")]
        public GameObject enemyPrefab;

        [Tooltip("How many copies of this enemy prefab are assigned into the generated formation slots for this sub-wave.")]
        [Min(0)]
        public int count = 1;
    }

    [Serializable]
    private class FormationConfig3D
    {
        [Tooltip("Preset slot layout generated around the sub-wave's center spawn point.")]
        public FormationPreset3D preset = FormationPreset3D.Line;

        [Tooltip("World-space distance between neighboring formation slots. Ring formations also use this to derive their radius.")]
        [Min(0.01f)]
        public float slotSpacing = 20f;

        [Tooltip("Column count used only by Grid formations. Values below 1 are treated as 1 column.")]
        [Min(1)]
        public int gridColumns = 3;
    }

    [Serializable]
    private class SubWaveConfig3D
    {
        [Tooltip("Optional label used only to keep the Inspector readable while authoring several sub-waves inside one wave.")]
        public string subWaveName = "";

        [Tooltip("Center point for this sub-wave's generated formation. Its position defines the formation center and its rotation defines the slot orientation and enemy facing. If left empty, the wave manager transform is used.")]
        public Transform centerSpawnPoint;

        [Tooltip("Generated formation settings for this sub-wave.")]
        public FormationConfig3D formation = new FormationConfig3D();

        [Tooltip("Enemy entries assigned sequentially into the generated formation slots. Example: 2 scouts then 3 shooters fills the first 2 slots with scouts and the next 3 with shooters.")]
        public SubWaveEnemyEntry3D[] enemies = Array.Empty<SubWaveEnemyEntry3D>();

        [Tooltip("Seconds between each enemy spawned in this sub-wave burst. Set to 0 to spawn the whole formation on the same frame.")]
        [Min(0f)]
        public float spawnBurstIntervalSeconds = 0f;

        [Tooltip("Seconds to wait after this sub-wave finishes spawning before the next sub-wave starts, even if earlier enemies are still alive.")]
        [Min(0f)]
        public float delayBeforeNextSubWaveSeconds = 0f;
    }

    [Serializable]
    private class BossWaveConfig3D
    {
        [Tooltip("Optional boss prefab spawned after this wave's normal timed sub-waves complete. In networked Invasion this prefab must have NetworkObject and be registered with NGO.")]
        public GameObject bossPrefab;

        [Tooltip("Spawn point used for the optional boss. Its position defines the boss spawn location and its rotation defines initial facing. If left empty, the wave manager transform is used.")]
        public Transform bossSpawnPoint;

        [Tooltip("Seconds to wait after the final normal sub-wave finishes before spawning the boss.")]
        [Min(0f)]
        public float delayBeforeBossSeconds = 0f;
    }

    [Serializable]
    private class WaveConfig3D
    {
        [Tooltip("Optional label used only to keep the Inspector readable while authoring several waves.")]
        public string waveName = "";

        [Tooltip("Timed sub-waves for this wave. They spawn in order and advance by authored delay rather than waiting for previous enemies to die.")]
        public SubWaveConfig3D[] subWaves = Array.Empty<SubWaveConfig3D>();

        [Tooltip("If enabled, this wave spawns its separate boss block after the normal timed sub-waves complete.")]
        public bool enableBoss = false;

        [Tooltip("Optional separate boss settings for this wave. Bosses remain prefab-authored for their own presentation and behavior.")]
        public BossWaveConfig3D boss = new BossWaveConfig3D();
    }

    [Header("Waves")]
    [Tooltip("Finite Invasion waves. Each wave runs its sub-waves in order, optionally spawns one separate boss, then waits until every tracked enemy from that wave and any tracked child spawns are dead before advancing.")]
    [SerializeField] private WaveConfig3D[] waves = Array.Empty<WaveConfig3D>();
    [Tooltip("If enabled, this manager starts waves as soon as it is enabled. Networked Invasion scenes should usually leave this off so InvasionSceneManager3D can spawn players and show WAVE text first.")]
    [SerializeField] private bool startOnEnable = true;
    [Tooltip("Seconds to wait after a wave is fully cleared before requesting the next wave intro.")]
    [FormerlySerializedAs("timeBetweenWaves")]
    [Min(0f)]
    [SerializeField] private float waveEndDelaySeconds = 3f;
    [Tooltip("Seconds to wait after the WAVE text/intro finishes before this wave starts spawning. This also applies to the first wave.")]
    [Min(0f)]
    [SerializeField] private float waveStartDelaySeconds = 0f;

    private readonly List<Enemy3D> _aliveEnemies = new List<Enemy3D>();
    private readonly List<Vector3> _formationOffsets = new List<Vector3>(16);
    private Coroutine _waveRoutine;

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
        int waveCount = WaveCount;
        for (int waveIndex = 0; waveIndex < waveCount; waveIndex++)
        {
            int waveNumber = waveIndex + 1;
            yield return RunWaveIntro(waveNumber);
            if (waveStartDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(waveStartDelaySeconds);
            }

            WaveStarted?.Invoke(waveNumber);
            yield return RunWaveSequence(waves[waveIndex]);
            yield return new WaitUntil(() => _aliveEnemies.Count == 0);
            WaveCleared?.Invoke(waveNumber);

            if (waveIndex < waveCount - 1 && waveEndDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(waveEndDelaySeconds);
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

    private IEnumerator RunWaveSequence(WaveConfig3D wave)
    {
        if (wave == null)
        {
            yield break;
        }

        if (wave.subWaves != null)
        {
            for (int i = 0; i < wave.subWaves.Length; i++)
            {
                yield return SpawnSubWave(wave.subWaves[i]);
            }
        }

        if (wave.enableBoss)
        {
            yield return SpawnWaveBoss(wave.boss);
        }
    }

    private IEnumerator SpawnSubWave(SubWaveConfig3D subWave)
    {
        if (subWave == null)
        {
            yield break;
        }

        int totalEnemyCount = GetTotalEnemyCount(subWave.enemies);
        if (totalEnemyCount <= 0)
        {
            if (subWave.delayBeforeNextSubWaveSeconds > 0f)
            {
                yield return new WaitForSeconds(subWave.delayBeforeNextSubWaveSeconds);
            }

            yield break;
        }

        Transform centerPoint = ResolveCenterPoint(subWave.centerSpawnPoint);
        Quaternion spawnRotation = centerPoint.rotation;
        Vector3 spawnOrigin = centerPoint.position;
        BuildFormationOffsets(totalEnemyCount, subWave.formation);

        float burstIntervalSeconds = Mathf.Max(0f, subWave.spawnBurstIntervalSeconds);
        int slotIndex = 0;

        if (subWave.enemies != null)
        {
            for (int entryIndex = 0; entryIndex < subWave.enemies.Length; entryIndex++)
            {
                SubWaveEnemyEntry3D entry = subWave.enemies[entryIndex];
                if (entry == null)
                {
                    continue;
                }

                int entryCount = Mathf.Max(0, entry.count);
                for (int memberIndex = 0; memberIndex < entryCount; memberIndex++)
                {
                    Vector3 localOffset = slotIndex < _formationOffsets.Count ? _formationOffsets[slotIndex] : Vector3.zero;
                    Vector3 spawnPosition = spawnOrigin + (spawnRotation * localOffset);
                    SpawnEnemyAt(entry.enemyPrefab, spawnPosition, spawnRotation);
                    slotIndex++;

                    bool hasMoreBurstMembers = slotIndex < totalEnemyCount;
                    if (hasMoreBurstMembers && burstIntervalSeconds > 0f)
                    {
                        yield return new WaitForSeconds(burstIntervalSeconds);
                    }
                }
            }
        }

        if (subWave.delayBeforeNextSubWaveSeconds > 0f)
        {
            yield return new WaitForSeconds(subWave.delayBeforeNextSubWaveSeconds);
        }
    }

    private IEnumerator SpawnWaveBoss(BossWaveConfig3D bossConfig)
    {
        if (bossConfig == null)
        {
            yield break;
        }

        if (bossConfig.delayBeforeBossSeconds > 0f)
        {
            yield return new WaitForSeconds(bossConfig.delayBeforeBossSeconds);
        }

        Transform bossSpawnPoint = ResolveCenterPoint(bossConfig.bossSpawnPoint);
        SpawnEnemyAt(bossConfig.bossPrefab, bossSpawnPoint.position, bossSpawnPoint.rotation);
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

    private Transform ResolveCenterPoint(Transform configuredPoint)
    {
        return configuredPoint != null ? configuredPoint : transform;
    }

    private int GetTotalEnemyCount(SubWaveEnemyEntry3D[] entries)
    {
        if (entries == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null)
            {
                continue;
            }

            total += Mathf.Max(0, entries[i].count);
        }

        return total;
    }

    private void BuildFormationOffsets(int totalEnemyCount, FormationConfig3D formation)
    {
        _formationOffsets.Clear();
        if (totalEnemyCount <= 0)
        {
            return;
        }

        FormationConfig3D resolvedFormation = formation ?? new FormationConfig3D();
        float spacing = Mathf.Max(0.01f, resolvedFormation.slotSpacing);

        switch (resolvedFormation.preset)
        {
            case FormationPreset3D.Wedge:
                BuildWedgeOffsets(totalEnemyCount, spacing);
                break;
            case FormationPreset3D.Ring:
                BuildRingOffsets(totalEnemyCount, spacing);
                break;
            case FormationPreset3D.Grid:
                BuildGridOffsets(totalEnemyCount, spacing, resolvedFormation.gridColumns);
                break;
            case FormationPreset3D.Line:
            default:
                BuildLineOffsets(totalEnemyCount, spacing);
                break;
        }
    }

    private void BuildLineOffsets(int totalEnemyCount, float spacing)
    {
        float halfSpan = (totalEnemyCount - 1) * 0.5f;
        for (int i = 0; i < totalEnemyCount; i++)
        {
            float x = (i - halfSpan) * spacing;
            _formationOffsets.Add(new Vector3(x, 0f, 0f));
        }
    }

    private void BuildWedgeOffsets(int totalEnemyCount, float spacing)
    {
        _formationOffsets.Add(Vector3.zero);
        if (totalEnemyCount == 1)
        {
            return;
        }

        int row = 1;
        while (_formationOffsets.Count < totalEnemyCount)
        {
            _formationOffsets.Add(new Vector3(-row * spacing, 0f, -row * spacing));
            if (_formationOffsets.Count >= totalEnemyCount)
            {
                break;
            }

            _formationOffsets.Add(new Vector3(row * spacing, 0f, -row * spacing));
            row++;
        }
    }

    private void BuildRingOffsets(int totalEnemyCount, float spacing)
    {
        if (totalEnemyCount == 1)
        {
            _formationOffsets.Add(Vector3.zero);
            return;
        }

        float radius = spacing / (2f * Mathf.Sin(Mathf.PI / totalEnemyCount));
        for (int i = 0; i < totalEnemyCount; i++)
        {
            float angle = (Mathf.PI * 2f * i) / totalEnemyCount;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            _formationOffsets.Add(new Vector3(x, 0f, z));
        }
    }

    private void BuildGridOffsets(int totalEnemyCount, float spacing, int gridColumns)
    {
        int columns = Mathf.Max(1, gridColumns);
        int rows = Mathf.CeilToInt(totalEnemyCount / (float)columns);
        float halfColumns = (columns - 1) * 0.5f;
        float halfRows = (rows - 1) * 0.5f;

        for (int i = 0; i < totalEnemyCount; i++)
        {
            int row = i / columns;
            int column = i % columns;
            float x = (column - halfColumns) * spacing;
            float z = (halfRows - row) * spacing;
            _formationOffsets.Add(new Vector3(x, 0f, z));
        }
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
