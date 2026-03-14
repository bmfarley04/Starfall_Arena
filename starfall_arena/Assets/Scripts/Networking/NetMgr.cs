using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Network session lifecycle manager.
/// Handles host/client startup, player prefab registration,
/// and connection/disconnection callbacks.
///
/// Attach to a persistent GameObject in the scene (e.g. alongside NetworkManager).
/// </summary>
public class NetMgr : MonoBehaviour
{
    [Header("Debug Controls")]
    [Tooltip("Key to start as Host (server + local client)")]
    [SerializeField] private KeyCode _hostKey = KeyCode.H;
    [Tooltip("Key to start as Client")]
    [SerializeField] private KeyCode _clientKey = KeyCode.C;

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

        // Subscribe to connection events
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
        // Debug keyboard shortcuts for quick testing
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsListening) return; // already running

        if (Input.GetKeyDown(_hostKey))
        {
            StartHost();
        }
        if (Input.GetKeyDown(_clientKey))
        {
            StartClient();
        }
    }

    // ===== PUBLIC API =====

    public void StartHost()
    {
        Debug.Log("[NetMgr] Starting Host...");
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        Debug.Log("[NetMgr] Starting Client...");
        NetworkManager.Singleton.StartClient();
    }

    public void StartServer()
    {
        Debug.Log("[NetMgr] Starting dedicated Server...");
        NetworkManager.Singleton.StartServer();
    }

    public void Shutdown()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetMgr] Shutting down network session.");
            NetworkManager.Singleton.Shutdown();
        }
    }

    // ===== SPAWN HELPERS =====

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
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[NetMgr] Prefab '{prefab.name}' has no NetworkObject component.");
            Destroy(instance);
            return null;
        }

        netObj.SpawnAsPlayerObject(ownerClientId, destroyWithScene: true);
        return instance;
    }

    // ===== CALLBACKS =====

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NetMgr] Client connected: {clientId}");

        if (NetworkManager.Singleton.IsServer)
        {
            // Server-side: client is ready. GameSceneManager can spawn their player.
            // For now just log — actual spawn integration happens when
            // GameSceneManager is updated to call SpawnPlayerNetworked().
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NetMgr] Client disconnected: {clientId}");
    }
}
