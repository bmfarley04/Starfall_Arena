using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Production session lifecycle manager for menu-driven LAN networking.
/// Owns transport startup/shutdown and connection callbacks.
/// Gameplay prefabs are selected by ShipData/GameSceneManager, not here.
/// </summary>
[DisallowMultipleComponent]
public class NetMgr : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private ushort defaultPort = 7777;
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private float clientConnectionTimeoutSeconds = 8f;

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

    private UnityTransport _transport;
    private bool _isConfigured;
    private bool _awaitingClientConnect;
    private float _clientConnectStartedAt;

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
        if (_transport == null)
        {
            Debug.LogError("[NetMgr] UnityTransport component is missing from the NetworkManager.");
            return;
        }

        _isConfigured = true;

        // Gameplay player objects are spawned manually from GameSceneManager.
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

    private void Update()
    {
        if (!_awaitingClientConnect || NetworkManager.Singleton == null)
        {
            return;
        }

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            ClearClientConnectTimeout();
            return;
        }

        if (Time.unscaledTime - _clientConnectStartedAt < clientConnectionTimeoutSeconds)
        {
            return;
        }

        ClearClientConnectTimeout();
        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        NotifyConnectionFailed($"Failed to connect to host at {_transport.ConnectionData.Address}:{defaultPort}.");
    }

    public bool StartHostForMenu()
    {
        if (NetworkManager.Singleton == null)
        {
            NotifyConnectionFailed("NetworkManager is missing from the scene.");
            return false;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            NotifyConnectionFailed("A network session is already active.");
            return false;
        }

        if (!EnsureConfigured())
        {
            return false;
        }

        ConfigureConnectionAddress("0.0.0.0", defaultPort);

        if (!NetworkManager.Singleton.StartHost())
        {
            NotifyConnectionFailed("Failed to start host.");
            return false;
        }

        ClearClientConnectTimeout();
        NetworkSessionData.Instance?.BeginHostingState();
        OnConnectionStarted?.Invoke(true);
        return true;
    }

    public bool StartClientForMenu(string address)
    {
        if (NetworkManager.Singleton == null)
        {
            NotifyConnectionFailed("NetworkManager is missing from the scene.");
            return false;
        }

        if (_awaitingClientConnect)
        {
            return false;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            NotifyConnectionFailed("A network session is already active.");
            return false;
        }

        if (!EnsureConfigured())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            NotifyConnectionFailed("Enter the host IP address first.");
            return false;
        }

        ConfigureConnectionAddress(address.Trim(), defaultPort);
        NetworkSessionData.Instance?.BeginJoiningState();

        if (!NetworkManager.Singleton.StartClient())
        {
            NotifyConnectionFailed("Failed to start client.");
            return false;
        }

        _awaitingClientConnect = true;
        _clientConnectStartedAt = Time.unscaledTime;
        OnConnectionStarted?.Invoke(false);
        return true;
    }

    public void CancelCurrentAttempt()
    {
        ClearClientConnectTimeout();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        NetworkSessionData.Instance?.ResetToTitleLocal();
    }

    public void ShutdownToTitle()
    {
        CancelCurrentAttempt();
        NetworkSessionData.Instance?.ResetToTitleLocal();
    }

    public void ConfigureConnectionAddress(string address, ushort port)
    {
        if (_transport == null && NetworkManager.Singleton != null)
        {
            _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }

        _transport?.SetConnectionData(address, port);
    }

    public static GameObject SpawnPlayerNetworked(GameObject prefab, Vector3 position, Quaternion rotation, ulong ownerClientId)
    {
        if (!IsNetworked || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogError("[NetMgr] SpawnPlayerNetworked called but the server is not active.");
            return null;
        }

        if (prefab == null)
        {
            Debug.LogError("[NetMgr] SpawnPlayerNetworked received a null prefab.");
            return null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"[NetMgr] Prefab '{prefab.name}' has no NetworkObject component.");
            Destroy(instance);
            return null;
        }

        networkObject.SpawnAsPlayerObject(ownerClientId, true);
        return instance;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetMgr] Client connected: {clientId}");
        OnPlayerJoined?.Invoke(clientId);

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                ClearClientConnectTimeout();
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
        if (NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsServer &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            bool wasAwaitingConnection = _awaitingClientConnect;
            ClearClientConnectTimeout();

            if (wasAwaitingConnection)
            {
                NotifyConnectionFailed("Disconnected before joining the host. Check the IP address and make sure the host is waiting for an opponent.");
                return;
            }
        }

        OnPlayerLeft?.Invoke(clientId);
        NetworkSessionData.Instance?.RegisterClientDisconnected(clientId);
    }

    private void NotifyConnectionFailed(string message)
    {
        Debug.LogError($"[NetMgr] {message}");
        ClearClientConnectTimeout();
        NetworkSessionData.Instance?.SetLocalState(NetworkMatchState.Error, message);
        OnConnectionFailed?.Invoke(message);
    }

    private bool EnsureConfigured()
    {
        if (_isConfigured && _transport != null)
        {
            return true;
        }

        if (NetworkManager.Singleton == null)
        {
            NotifyConnectionFailed("NetworkManager is missing from the scene.");
            return false;
        }

        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (_transport == null)
        {
            NotifyConnectionFailed("UnityTransport is missing from the NetworkManager.");
            return false;
        }

        _isConfigured = true;
        return true;
    }

    private int GetConnectedPlayerCount()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClientsIds != null
            ? NetworkManager.Singleton.ConnectedClientsIds.Count
            : 0;
    }

    private void ClearClientConnectTimeout()
    {
        _awaitingClientConnect = false;
        _clientConnectStartedAt = 0f;
    }
}
