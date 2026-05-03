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

        [Tooltip("How much of the formation's secondary axis is also applied as local Y offset. Set to 0 to keep the formation flat. Higher values make wedges, rings, and grids gain more vertical variation around the center point.")]
        public float yBias = 0f;
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
    private readonly List<Enemy3D> _pendingRevealEnemies = new List<Enemy3D>();
    private readonly List<Vector3> _formationOffsets = new List<Vector3>(16);
    private float _activeFormationYBias;
    private Coroutine _waveRoutine;
    private int _authoredEnemyCount;
    private int _defeatedEnemyCount;
    private bool _loggedZeroAuthoredEnemyCount;

    public event Func<int, IEnumerator> WaveIntroRequested;
    public event Func<int, IEnumerator> RewardPhaseRequested;
    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;
    public event Action AllWavesCleared;
    public event Action<int> AliveEnemyCountChanged;
    public event Action<int, int, float> EnemyDefeatProgressChanged;

    public int AliveEnemyCount => _aliveEnemies.Count;
    public int WaveCount => waves != null ? waves.Length : 0;
    public bool IsRunning => _waveRoutine != null;
    public int AuthoredEnemyCount => IsRunning ? _authoredEnemyCount : CalculateAuthoredEnemyCount();
    public int DefeatedEnemyCount => _defeatedEnemyCount;
    public float EnemyDefeatProgress01 => CalculateEnemyDefeatProgress01(_defeatedEnemyCount, AuthoredEnemyCount);

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

        ResetEnemyDefeatProgress();
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
            yield return new WaitUntil(() => !HasOutstandingTrackedEnemies());
            WaveCleared?.Invoke(waveNumber);

            if (waveIndex < waveCount - 1)
            {
                yield return RunRewardPhase(waveNumber);
            }

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

    private IEnumerator RunRewardPhase(int clearedWaveNumber)
    {
        Func<int, IEnumerator> rewardHandler = RewardPhaseRequested;
        if (rewardHandler == null)
        {
            yield break;
        }

        Delegate[] handlers = rewardHandler.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is Func<int, IEnumerator> handler)
            {
                yield return handler(clearedWaveNumber);
            }
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

        RegisterSpawnedEnemy(enemy);
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

    private int CalculateAuthoredEnemyCount()
    {
        if (waves == null)
        {
            return 0;
        }

        int total = 0;
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            WaveConfig3D wave = waves[waveIndex];
            if (wave == null)
            {
                continue;
            }

            if (wave.subWaves != null)
            {
                for (int subWaveIndex = 0; subWaveIndex < wave.subWaves.Length; subWaveIndex++)
                {
                    SubWaveConfig3D subWave = wave.subWaves[subWaveIndex];
                    if (subWave == null)
                    {
                        continue;
                    }

                    total += GetTotalEnemyCount(subWave.enemies);
                }
            }

            if (wave.enableBoss && wave.boss != null && wave.boss.bossPrefab != null)
            {
                total++;
            }
        }

        return total;
    }

    private void ResetEnemyDefeatProgress()
    {
        _authoredEnemyCount = CalculateAuthoredEnemyCount();
        _defeatedEnemyCount = 0;

        if (_authoredEnemyCount <= 0 && !_loggedZeroAuthoredEnemyCount)
        {
            Debug.LogWarning("[InvasionWaveManager3D] Enemy defeat progress will stay at 0 because no authored enemies were found in the configured waves.", this);
            _loggedZeroAuthoredEnemyCount = true;
        }

        PublishEnemyDefeatProgress();
    }

    private void RegisterEnemyDefeated()
    {
        int total = AuthoredEnemyCount;
        if (total <= 0)
        {
            PublishEnemyDefeatProgress();
            return;
        }

        _defeatedEnemyCount = Mathf.Min(_defeatedEnemyCount + 1, total);
        PublishEnemyDefeatProgress();
    }

    private void PublishEnemyDefeatProgress()
    {
        int total = AuthoredEnemyCount;
        EnemyDefeatProgressChanged?.Invoke(_defeatedEnemyCount, total, CalculateEnemyDefeatProgress01(_defeatedEnemyCount, total));
    }

    private static float CalculateEnemyDefeatProgress01(int defeated, int total)
    {
        if (total <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(defeated / (float)total);
    }

    private void BuildFormationOffsets(int totalEnemyCount, FormationConfig3D formation)
    {
        _formationOffsets.Clear();
        if (totalEnemyCount <= 0)
        {
            return;
        }

        FormationConfig3D resolvedFormation = formation ?? new FormationConfig3D();
        _activeFormationYBias = resolvedFormation.yBias;
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
            float axisA = (i - halfSpan) * spacing;
            _formationOffsets.Add(CreatePlaneOffset(axisA, 0f));
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
            _formationOffsets.Add(CreatePlaneOffset(-row * spacing, -row * spacing));
            if (_formationOffsets.Count >= totalEnemyCount)
            {
                break;
            }

            _formationOffsets.Add(CreatePlaneOffset(row * spacing, -row * spacing));
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
            float axisA = Mathf.Cos(angle) * radius;
            float axisB = Mathf.Sin(angle) * radius;
            _formationOffsets.Add(CreatePlaneOffset(axisA, axisB));
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
            float axisA = (column - halfColumns) * spacing;
            float axisB = (halfRows - row) * spacing;
            _formationOffsets.Add(CreatePlaneOffset(axisA, axisB));
        }
    }

    private Vector3 CreatePlaneOffset(float axisA, float axisB)
    {
        return new Vector3(axisA, axisB * _activeFormationYBias, axisB);
    }

    private void RegisterSpawnedEnemy(Enemy3D enemy)
    {
        if (enemy == null)
        {
            return;
        }

        SpawnArrivalEffect3D arrivalEffect = enemy.GetComponent<SpawnArrivalEffect3D>();
        if (arrivalEffect != null && !arrivalEffect.HasRevealed)
        {
            QueuePendingRevealEnemy(enemy, arrivalEffect);
            return;
        }

        TrackAliveEnemy(enemy);
    }

    private void QueuePendingRevealEnemy(Enemy3D enemy, SpawnArrivalEffect3D arrivalEffect)
    {
        if (enemy == null || arrivalEffect == null)
        {
            return;
        }

        if (_pendingRevealEnemies.Contains(enemy) || _aliveEnemies.Contains(enemy))
        {
            return;
        }

        _pendingRevealEnemies.Add(enemy);
        enemy.Died -= HandlePendingEnemyDied;
        enemy.Died += HandlePendingEnemyDied;
        arrivalEffect.Revealed -= HandlePendingEnemyRevealed;
        arrivalEffect.Revealed += HandlePendingEnemyRevealed;
    }

    private void TrackAliveEnemy(Enemy3D enemy)
    {
        if (enemy == null || _aliveEnemies.Contains(enemy))
        {
            return;
        }

        _aliveEnemies.Add(enemy);
        enemy.Died += HandleEnemyDied;
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private void HandlePendingEnemyRevealed(SpawnArrivalEffect3D arrivalEffect)
    {
        if (arrivalEffect == null)
        {
            return;
        }

        Enemy3D enemy = arrivalEffect.GetComponent<Enemy3D>();
        PromotePendingEnemyToAlive(enemy);
    }

    private void HandlePendingEnemyDied(Entity3D entity)
    {
        Enemy3D enemy = entity as Enemy3D;
        if (enemy == null)
        {
            return;
        }

        RemovePendingRevealEnemy(enemy);
        RegisterEnemyDefeated();
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
        RegisterEnemyDefeated();
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private void PromotePendingEnemyToAlive(Enemy3D enemy)
    {
        if (enemy == null)
        {
            return;
        }

        RemovePendingRevealEnemy(enemy);
        TrackAliveEnemy(enemy);
    }

    private void RemovePendingRevealEnemy(Enemy3D enemy)
    {
        if (enemy == null)
        {
            return;
        }

        SpawnArrivalEffect3D arrivalEffect = enemy.GetComponent<SpawnArrivalEffect3D>();
        if (arrivalEffect != null)
        {
            arrivalEffect.Revealed -= HandlePendingEnemyRevealed;
        }

        enemy.Died -= HandlePendingEnemyDied;
        _pendingRevealEnemies.Remove(enemy);
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

        for (int i = 0; i < _pendingRevealEnemies.Count; i++)
        {
            Enemy3D pendingEnemy = _pendingRevealEnemies[i];
            if (pendingEnemy == null)
            {
                continue;
            }

            pendingEnemy.Died -= HandlePendingEnemyDied;
            SpawnArrivalEffect3D arrivalEffect = pendingEnemy.GetComponent<SpawnArrivalEffect3D>();
            if (arrivalEffect != null)
            {
                arrivalEffect.Revealed -= HandlePendingEnemyRevealed;
            }
        }

        _aliveEnemies.Clear();
        _pendingRevealEnemies.Clear();
        AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
    }

    private bool HasOutstandingTrackedEnemies()
    {
        CleanupNullTrackedEnemies();
        return _aliveEnemies.Count > 0 || _pendingRevealEnemies.Count > 0;
    }

    private void CleanupNullTrackedEnemies()
    {
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (_aliveEnemies[i] == null)
            {
                _aliveEnemies.RemoveAt(i);
                AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
            }
        }

        for (int i = _pendingRevealEnemies.Count - 1; i >= 0; i--)
        {
            if (_pendingRevealEnemies[i] == null)
            {
                _pendingRevealEnemies.RemoveAt(i);
            }
        }
    }

    private bool HasSpawnAuthority()
    {
        return !NetTickUtil.IsActive
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }
}
