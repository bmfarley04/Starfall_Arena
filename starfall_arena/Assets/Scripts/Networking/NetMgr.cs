using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Network session lifecycle manager for the menu and duel flow.
/// Handles direct-IP host/client startup, session shutdown, and
/// deterministic slot assignment for the two dueling players.
/// </summary>
[DisallowMultipleComponent]
public class NetMgr : MonoBehaviour
{
    [Header("Player Prefabs")]
    [Tooltip("Prefab spawned for the first connected player.")]
    [SerializeField] private GameObject player1Prefab;
    [Tooltip("Prefab spawned for the second connected player.")]
    [SerializeField] private GameObject player2Prefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Limits")]
    [SerializeField] private int maxPlayers = 2;

    public static NetMgr Instance { get; private set; }

    public static bool IsNetworked
    {
        get
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening;
        }
    }

    public event Action<bool> OnConnectionStarted;
    public event Action<string> OnConnectionFailed;
    public event Action<ulong> OnPlayerJoined;
    public event Action<ulong> OnPlayerLeft;

    private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new Dictionary<ulong, NetworkObject>();
    private UnityTransport _transport;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetMgr] No NetworkManager.Singleton found in scene.");
            return;
        }

        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (!ValidatePlayerPrefab(player1Prefab, nameof(player1Prefab)) ||
            !ValidatePlayerPrefab(player2Prefab, nameof(player2Prefab)))
        {
            return;
        }

        NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
        NetworkManager.Singleton.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    public void StartHostForMenu()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
        {
            return;
        }

        NetworkSessionData.Instance?.BeginHostingState();

        if (!NetworkManager.Singleton.StartHost())
        {
            NotifyConnectionFailed("Failed to start host.");
            return;
        }

        OnConnectionStarted?.Invoke(true);
    }

    public void StartClientForMenu(string address)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.IsListening)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            NotifyConnectionFailed("Enter the host IP address first.");
            return;
        }

        ConfigureConnectionAddress(address);
        NetworkSessionData.Instance?.BeginJoiningState();

        if (!NetworkManager.Singleton.StartClient())
        {
            NotifyConnectionFailed("Failed to start client.");
            return;
        }

        OnConnectionStarted?.Invoke(false);
    }

    public void CancelCurrentAttempt()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        _spawnedPlayers.Clear();
        NetworkSessionData.Instance?.ResetToTitleLocal();
    }

    public void ShutdownToTitle()
    {
        CancelCurrentAttempt();
        NetworkSessionData.Instance?.ResetToTitleLocal();
    }

    public void ConfigureConnectionAddress(string address, ushort port = 7777)
    {
        if (_transport == null)
        {
            _transport = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.GetComponent<UnityTransport>()
                : null;
        }

        _transport?.SetConnectionData(address, port);
    }

    /// <summary>
    /// Spawn a player NetworkObject on the server for the requested owner.
    /// </summary>
    public static GameObject SpawnPlayerNetworked(GameObject prefab, Vector3 position, Quaternion rotation, ulong ownerClientId)
    {
        if (!IsNetworked || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[NetMgr] SpawnPlayerNetworked called but the server is not active.");
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

        netObj.SpawnAsPlayerObject(ownerClientId, true);
        return instance;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetMgr] Client connected: {clientId}");
        OnPlayerJoined?.Invoke(clientId);

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                NetworkSessionData.Instance?.SetLocalState(
                    NetworkMatchState.ConnectedReadyForShipSelect,
                    "Connected. Waiting for ship select...");
            }

            return;
        }

        if (GetConnectedPlayerCount() > maxPlayers)
        {
            NetworkManager.Singleton.DisconnectClient(clientId);
            return;
        }

        NetworkSessionData.Instance?.RegisterConnectedClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _spawnedPlayers.Remove(clientId);
        OnPlayerLeft?.Invoke(clientId);
        NetworkSessionData.Instance?.RegisterClientDisconnected(clientId);
    }

    private void NotifyConnectionFailed(string message)
    {
        Debug.LogError($"[NetMgr] {message}");
        NetworkSessionData.Instance?.SetLocalState(NetworkMatchState.Error, message);
        OnConnectionFailed?.Invoke(message);
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

    private int GetConnectedPlayerCount()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;
    }
}
