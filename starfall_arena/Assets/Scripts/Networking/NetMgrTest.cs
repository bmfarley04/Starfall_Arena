using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network session lifecycle manager.
/// Handles host/client startup, manual player spawning,
/// and connection/disconnection callbacks.
///
/// Attach to a persistent GameObject in the scene (e.g. alongside NetworkManager).
/// </summary>
public class NetMgrTest : MonoBehaviour
{
    [Header("Debug Controls")]
    [Tooltip("Key to start as Host (server + local client)")]
    [SerializeField] private KeyCode _hostKey = KeyCode.H;
    [Tooltip("Key to start as Client")]
    [SerializeField] private KeyCode _clientKey = KeyCode.C;

    [Header("Player Prefabs")]
    [Tooltip("Prefab spawned for the first connected player.")]
    [SerializeField] private GameObject _player1Prefab;
    [Tooltip("Prefab spawned for the second connected player and any later joins.")]
    [SerializeField] private GameObject _player2Prefab;

    [Header("Spawn Points")]
    [Tooltip("Optional spawn point for player 1. Falls back to world origin if left empty.")]
    [SerializeField] private Transform _player1SpawnPoint;
    [Tooltip("Optional spawn point for player 2. Falls back to world origin if left empty.")]
    [SerializeField] private Transform _player2SpawnPoint;

    private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new();

    /// <summary>True when a networked session is active.</summary>
    public static bool IsNetworked
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening;
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetMgr] No NetworkManager.Singleton found in scene.");
            return;
        }

        if (!ValidatePlayerPrefab(_player1Prefab, nameof(_player1Prefab)) ||
            !ValidatePlayerPrefab(_player2Prefab, nameof(_player2Prefab)))
        {
            return;
        }

        // We manually spawn the correct prefab per client when they connect.
        NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
        NetworkManager.Singleton.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening) return;

        if (Input.GetKeyDown(_hostKey))
        {
            StartHost();
        }

        if (Input.GetKeyDown(_clientKey))
        {
            StartClient();
        }
    }

    public void StartHost()
    {
        Debug.Log("[NetMgr] Starting Host...");
        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("[NetMgr] Failed to start Host.");
            return;
        }

        // Make host assignment deterministic: the host is always player 1.
        SpawnAssignedPlayer(NetworkManager.Singleton.LocalClientId);
    }

    public void StartClient()
    {
        Debug.Log("[NetMgr] Starting Client...");
        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError("[NetMgr] Failed to start Client.");
        }
    }

    public void StartServer()
    {
        Debug.Log("[NetMgr] Starting dedicated Server...");
        if (!NetworkManager.Singleton.StartServer())
        {
            Debug.LogError("[NetMgr] Failed to start dedicated Server.");
        }
    }

    public void Shutdown()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetMgr] Shutting down network session.");
            NetworkManager.Singleton.Shutdown();
        }
    }

    /// <summary>
    /// Spawn a player NetworkObject on the server. Call from GameSceneManager
    /// instead of Instantiate() when a networked session is active.
    /// The prefab must be registered in NetworkManager's NetworkPrefabs list.
    /// </summary>
    public static GameObject SpawnPlayerNetworked(GameObject prefab, Vector3 position, Quaternion rotation, ulong ownerClientId)
    {
        if (!IsNetworked || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[NetMgr] SpawnPlayerNetworked called but not running as server.");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[NetMgr] Prefab '{prefab.name}' has no NetworkObject component.");
            Destroy(instance);
            return null;
        }

        netObj.SpawnAsPlayerObject(ownerClientId, destroyWithScene: true);
        return instance;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetMgr] Client connected: {clientId}");

        if (!NetworkManager.Singleton.IsServer)
        {
            return;
        }

        SpawnAssignedPlayer(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _spawnedPlayers.Remove(clientId);
        Debug.Log($"[NetMgr] Client disconnected: {clientId}");
    }

    private void SpawnAssignedPlayer(ulong clientId)
    {
        if (_spawnedPlayers.TryGetValue(clientId, out NetworkObject existingPlayer) && existingPlayer != null)
        {
            return;
        }

        bool usePlayer1Slot = ShouldUsePlayer1Slot(clientId);
        GameObject selectedPrefab = usePlayer1Slot ? _player1Prefab : _player2Prefab;

        if (!usePlayer1Slot && _spawnedPlayers.Count >= 2)
        {
            Debug.LogWarning($"[NetMgr] More than two players attempted to join. Reusing '{_player2Prefab.name}' for client {clientId}.");
        }

        Transform spawnPoint = usePlayer1Slot ? _player1SpawnPoint : _player2SpawnPoint;
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject instance = Instantiate(selectedPrefab, spawnPosition, spawnRotation);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        _spawnedPlayers[clientId] = netObj;

        Debug.Log($"[NetMgr] Spawned '{selectedPrefab.name}' for client {clientId} at {spawnPosition}.");
    }

    private bool ShouldUsePlayer1Slot(ulong clientId)
    {
        if (NetworkManager.Singleton.IsHost && clientId == NetworkManager.ServerClientId)
        {
            return true;
        }

        return _spawnedPlayers.Count == 0;
    }

    private bool ValidatePlayerPrefab(GameObject prefab, string fieldName)
    {
        if (prefab == null)
        {
            Debug.LogError($"[NetMgr] {fieldName} is not assigned.");
            return false;
        }

        if (prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[NetMgr] Prefab '{prefab.name}' assigned to {fieldName} has no NetworkObject component.");
            return false;
        }

        return true;
    }
}