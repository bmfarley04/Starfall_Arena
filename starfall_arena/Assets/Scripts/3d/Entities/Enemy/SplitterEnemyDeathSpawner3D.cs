using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy3D))]
public class SplitterEnemyDeathSpawner3D : MonoBehaviour
{
    [Header("Splitter Death Spawn")]
    [Tooltip("Smaller enemy prefab spawned when this enemy dies. Use a faster, lower-health prefab without this component for the one-level Splitter behavior.")]
    [SerializeField] private GameObject splitEnemyPrefab;

    [Tooltip("Number of smaller enemies spawned when this enemy dies.")]
    [SerializeField] private int splitCount = 2;

    [Tooltip("Distance from the dying enemy where each smaller enemy appears.")]
    [SerializeField] private float splitSpawnRadius = 4f;

    [Tooltip("Random vertical offset range applied to each split spawn so children do not stack exactly in the same flight lane.")]
    [SerializeField] private float verticalSpawnJitter = 1.5f;

    [Tooltip("Fallback wave manager used to spawn and track child enemies. If empty, the first active InvasionWaveManager3D is found at runtime.")]
    [SerializeField] private InvasionWaveManager3D waveManager;

    [Tooltip("If true, this component logs missing prefab or wave-manager setup instead of failing silently.")]
    [SerializeField] private bool logSetupWarnings = true;

    [Tooltip("If true, disables this same death-spawn component on spawned children so the Splitter only has one split level even if the child prefab includes the component.")]
    [SerializeField] private bool preventChildSplitting = true;

    private Enemy3D _enemy;
    private NetworkObject _networkObject;
    private bool _hasSplit;

    private void Awake()
    {
        _enemy = GetComponent<Enemy3D>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void OnEnable()
    {
        if (_enemy != null)
        {
            _enemy.Died -= HandleEnemyDied;
            _enemy.Died += HandleEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (_enemy != null)
        {
            _enemy.Died -= HandleEnemyDied;
        }
    }

    private void HandleEnemyDied(Entity3D deadEntity)
    {
        if (_hasSplit || !HasSpawnAuthority())
        {
            return;
        }

        _hasSplit = true;

        if (splitEnemyPrefab == null)
        {
            LogSetupWarning("cannot split because Split Enemy Prefab is not assigned.");
            return;
        }

        InvasionWaveManager3D manager = ResolveWaveManager();
        if (manager == null)
        {
            LogSetupWarning("cannot split because no active InvasionWaveManager3D was found.");
            return;
        }

        int childCount = Mathf.Max(0, splitCount);
        for (int i = 0; i < childCount; i++)
        {
            Vector3 spawnPosition = ResolveChildSpawnPosition(i, childCount);
            Vector3 childForward = ResolveChildForward(spawnPosition);
            Quaternion spawnRotation = Quaternion.LookRotation(childForward, ResolveChildUp(childForward));
            Enemy3D child = manager.SpawnEnemyAt(splitEnemyPrefab, spawnPosition, spawnRotation);
            DisableChildSplitSpawnerIfNeeded(child);
        }
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

    private Vector3 ResolveChildSpawnPosition(int index, int childCount)
    {
        float radius = Mathf.Max(0f, splitSpawnRadius);
        if (childCount <= 1 || radius <= 0f)
        {
            return transform.position;
        }

        float angle = (Mathf.PI * 2f * index) / childCount;
        Vector3 lateralOffset = (transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle)) * radius;
        float verticalOffset = verticalSpawnJitter > 0f ? Random.Range(-verticalSpawnJitter, verticalSpawnJitter) : 0f;
        return transform.position + lateralOffset + (transform.up * verticalOffset);
    }

    private Vector3 ResolveChildForward(Vector3 spawnPosition)
    {
        Vector3 awayFromParent = spawnPosition - transform.position;
        if (awayFromParent.sqrMagnitude > 0.0001f)
        {
            return awayFromParent.normalized;
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;
    }

    private Vector3 ResolveChildUp(Vector3 childForward)
    {
        Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up : Vector3.up;
        if (Mathf.Abs(Vector3.Dot(childForward.normalized, up.normalized)) > 0.98f)
        {
            up = Vector3.up;
        }

        if (Mathf.Abs(Vector3.Dot(childForward.normalized, up.normalized)) > 0.98f)
        {
            up = Vector3.right;
        }

        return up;
    }

    private void DisableChildSplitSpawnerIfNeeded(Enemy3D child)
    {
        if (!preventChildSplitting || child == null)
        {
            return;
        }

        SplitterEnemyDeathSpawner3D childSplitter = child.GetComponent<SplitterEnemyDeathSpawner3D>();
        if (childSplitter != null)
        {
            childSplitter.enabled = false;
        }
    }

    private bool HasSpawnAuthority()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_networkObject == null)
        {
            return NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        }

        return !_networkObject.IsSpawned
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsServer;
    }

    private void LogSetupWarning(string message)
    {
        if (!logSetupWarnings)
        {
            return;
        }

        Debug.LogWarning($"[{nameof(SplitterEnemyDeathSpawner3D)}] {name} {message}", this);
    }
}
