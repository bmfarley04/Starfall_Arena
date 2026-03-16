using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum NetworkMatchState
{
    TitleIdle = 0,
    HostingWaitingForClient = 1,
    JoiningConnecting = 2,
    ConnectedReadyForShipSelect = 3,
    ShipSelect = 4,
    LoadingGameplay = 5,
    InMatch = 6,
    RoundTransition = 7,
    AugmentPhase = 8,
    MatchComplete = 9,
    Disconnected = 10,
    Error = 11
}

public struct NetworkShipSelectionStatePayload : INetworkSerializable
{
    public ulong ClientId;
    public int SlotIndex;
    public FixedString64Bytes ShipId;
    public bool IsLockedIn;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref ShipId);
        serializer.SerializeValue(ref IsLockedIn);
    }
}

public sealed class NetworkShipSelectionState
{
    public ulong ClientId;
    public int SlotIndex;
    public string ShipId;
    public bool IsLockedIn;
    public ShipData ShipData => GameDataManager.Instance != null ? GameDataManager.Instance.GetShipById(ShipId) : null;
}

[DisallowMultipleComponent]
public class NetworkSessionData : NetworkBehaviour
{
    public static NetworkSessionData Instance { get; private set; }

    [Header("Ship Select")]
    [SerializeField] private float selectionDurationSeconds = 20f;
    [SerializeField] private string gameplaySceneName = "SampleSceneSplitScreen";

    public event Action<NetworkMatchState> OnSessionStateChanged;
    public event Action OnShipSelectionsChanged;
    public event Action<float> OnSelectionTimerChanged;
    public event Action<string> OnStatusMessageChanged;

    private readonly NetworkShipSelectionState[] _shipSelections =
    {
        new NetworkShipSelectionState { SlotIndex = 0, ClientId = ulong.MaxValue },
        new NetworkShipSelectionState { SlotIndex = 1, ClientId = ulong.MaxValue }
    };

    private NetworkMatchState _currentState = NetworkMatchState.TitleIdle;
    private float _selectionTimeRemaining;
    private string _statusMessage = string.Empty;
    private bool _gameplaySceneLoadRequested;

    public NetworkMatchState CurrentState => _currentState;
    public float SelectionTimeRemaining => _selectionTimeRemaining;
    public string StatusMessage => _statusMessage;
    public string GameplaySceneName => gameplaySceneName;
    public bool IsShipSelectActive => _currentState == NetworkMatchState.ShipSelect;
    public bool HasBothPlayersConnected =>
        _shipSelections[0].ClientId != ulong.MaxValue &&
        _shipSelections[1].ClientId != ulong.MaxValue;
    public bool IsLocalPlayerLockedIn => TryGetLocalSelectionState(out NetworkShipSelectionState selection) && selection.IsLockedIn;
    public ulong LocalClientId => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
    public ulong HostClientId => NetworkManager.Singleton != null ? NetworkManager.ServerClientId : ulong.MaxValue;
    public NetworkShipSelectionState Player1Selection => _shipSelections[0];
    public NetworkShipSelectionState Player2Selection => _shipSelections[1];

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

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SyncLocalState(_currentState, _selectionTimeRemaining, _statusMessage);
    }

    private void Update()
    {
        if (!IsServer || _currentState != NetworkMatchState.ShipSelect)
        {
            return;
        }

        if (_selectionTimeRemaining > 0f)
        {
            _selectionTimeRemaining = Mathf.Max(0f, _selectionTimeRemaining - Time.unscaledDeltaTime);
            NotifyTimerChanged();
            BroadcastTimerClientRpc(_selectionTimeRemaining);
        }

        if (_selectionTimeRemaining <= 0f)
        {
            FinalizeShipSelections();
        }
    }

    public void SetLocalState(NetworkMatchState state, string statusMessage = "")
    {
        SyncLocalState(state, _selectionTimeRemaining, statusMessage);
    }

    public void BeginHostingState()
    {
        if (!IsServer)
        {
            SyncLocalState(NetworkMatchState.HostingWaitingForClient, 0f, "Hosting duel. Waiting for opponent...");
            return;
        }

        SetServerState(NetworkMatchState.HostingWaitingForClient, "Hosting duel. Waiting for opponent...");
    }

    public void BeginJoiningState()
    {
        SyncLocalState(NetworkMatchState.JoiningConnecting, 0f, "Connecting to host...");
    }

    public void RegisterConnectedClient(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        int slot = GetOrAssignSlotIndex(clientId);
        _shipSelections[slot].ClientId = clientId;
        _shipSelections[slot].SlotIndex = slot;
        _shipSelections[slot].IsLockedIn = false;
        _shipSelections[slot].ShipId = string.Empty;

        BroadcastSelections();

        if (GetConnectedPlayerCount() >= 2)
        {
            StartShipSelectServer();
        }
        else
        {
            SetServerState(NetworkMatchState.HostingWaitingForClient, "Hosting duel. Waiting for opponent...");
        }
    }

    public void RegisterClientDisconnected(ulong clientId)
    {
        ClearSlot(clientId);

        if (IsServer)
        {
            _selectionTimeRemaining = 0f;
            _gameplaySceneLoadRequested = false;
            SetServerState(NetworkMatchState.HostingWaitingForClient, "Opponent disconnected. Waiting for a new client...");
            BroadcastSelections();
        }
        else
        {
            SyncLocalState(NetworkMatchState.Disconnected, 0f, "Disconnected from host.");
        }
    }

    public void ResetToTitleLocal(string statusMessage = "")
    {
        _selectionTimeRemaining = 0f;
        _gameplaySceneLoadRequested = false;
        ClearAllSelections();
        SyncLocalState(NetworkMatchState.TitleIdle, 0f, statusMessage);
        NotifySelectionsChanged();
    }

    public bool TryGetLocalSelectionState(out NetworkShipSelectionState selection)
    {
        ulong localClientId = LocalClientId;
        foreach (NetworkShipSelectionState candidate in _shipSelections)
        {
            if (candidate.ClientId == localClientId)
            {
                selection = candidate;
                return true;
            }
        }

        selection = null;
        return false;
    }

    public int GetLocalSlotIndex()
    {
        return TryGetLocalSelectionState(out NetworkShipSelectionState selection) ? selection.SlotIndex : -1;
    }

    public bool TryGetRemoteSelectionState(out NetworkShipSelectionState selection)
    {
        ulong localClientId = LocalClientId;
        foreach (NetworkShipSelectionState candidate in _shipSelections)
        {
            if (candidate.ClientId != ulong.MaxValue && candidate.ClientId != localClientId)
            {
                selection = candidate;
                return true;
            }
        }

        selection = null;
        return false;
    }

    public void RequestShipSelection(string shipId, bool lockIn)
    {
        if (!NetMgr.IsNetworked)
        {
            return;
        }

        if (IsServer)
        {
            ApplyShipSelection(LocalClientId, shipId, lockIn);
        }
        else
        {
            SubmitShipSelectionServerRpc(shipId, lockIn);
        }
    }

    public void RequestAugmentChoice(string augmentId)
    {
        if (!NetMgr.IsNetworked)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(augmentId))
        {
            SetStatusMessageLocal($"Selected augment {augmentId}.");
        }
    }

    public void MarkMatchStarted()
    {
        if (IsServer)
        {
            SetServerState(NetworkMatchState.InMatch, "Match in progress.");
        }
        else
        {
            SyncLocalState(NetworkMatchState.InMatch, _selectionTimeRemaining, "Match in progress.");
        }
    }

    public void MarkAugmentPhase()
    {
        if (IsServer)
        {
            SetServerState(NetworkMatchState.AugmentPhase, "Augment phase.");
        }
        else
        {
            SyncLocalState(NetworkMatchState.AugmentPhase, _selectionTimeRemaining, "Augment phase.");
        }
    }

    public void MarkRoundTransition()
    {
        if (IsServer)
        {
            SetServerState(NetworkMatchState.RoundTransition, "Round transition.");
        }
        else
        {
            SyncLocalState(NetworkMatchState.RoundTransition, _selectionTimeRemaining, "Round transition.");
        }
    }

    public void SetStatusMessageLocal(string message)
    {
        _statusMessage = message ?? string.Empty;
        OnStatusMessageChanged?.Invoke(_statusMessage);
    }

    private void StartShipSelectServer()
    {
        _selectionTimeRemaining = selectionDurationSeconds;
        _gameplaySceneLoadRequested = false;

        foreach (NetworkShipSelectionState selection in _shipSelections)
        {
            if (selection.ClientId == ulong.MaxValue)
            {
                continue;
            }

            selection.IsLockedIn = false;
            selection.ShipId = string.Empty;
        }

        SetServerState(NetworkMatchState.ShipSelect, "Choose your ship.");
        BroadcastSelections();
    }

    private void ApplyShipSelection(ulong clientId, string shipId, bool lockIn)
    {
        if (_currentState != NetworkMatchState.ShipSelect)
        {
            return;
        }

        if (GameDataManager.Instance != null && GameDataManager.Instance.GetShipById(shipId) == null)
        {
            return;
        }

        int slot = GetOrAssignSlotIndex(clientId);
        NetworkShipSelectionState selection = _shipSelections[slot];
        selection.ShipId = shipId ?? string.Empty;
        selection.IsLockedIn = lockIn;
        selection.ClientId = clientId;
        BroadcastSelections();

        if (AreAllSelectionsLocked())
        {
            FinalizeShipSelections();
        }
    }

    private void FinalizeShipSelections()
    {
        if (!IsServer || _gameplaySceneLoadRequested)
        {
            return;
        }

        ShipData player1Ship = EnsureLockedSelection(_shipSelections[0]);
        ShipData player2Ship = EnsureLockedSelection(_shipSelections[1]);

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetSelectedShips(player1Ship, player2Ship);
        }

        _selectionTimeRemaining = 0f;
        _gameplaySceneLoadRequested = true;
        SetServerState(NetworkMatchState.LoadingGameplay, "Loading duel...");
        BroadcastSelections();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }

    private ShipData EnsureLockedSelection(NetworkShipSelectionState selection)
    {
        ShipData ship = selection.ShipData;
        if (ship == null && GameDataManager.Instance != null)
        {
            ship = GameDataManager.Instance.GetRandomShip();
            selection.ShipId = ship != null ? ship.ShipId : string.Empty;
        }

        selection.IsLockedIn = true;
        return ship;
    }

    private bool AreAllSelectionsLocked()
    {
        int connectedPlayers = 0;
        foreach (NetworkShipSelectionState selection in _shipSelections)
        {
            if (selection.ClientId == ulong.MaxValue)
            {
                continue;
            }

            connectedPlayers++;
            if (!selection.IsLockedIn)
            {
                return false;
            }
        }

        return connectedPlayers >= 2;
    }

    private void BroadcastSelections()
    {
        NotifySelectionsChanged();

        if (IsServer)
        {
            BroadcastSelectionsClientRpc(ToPayload(_shipSelections[0]), ToPayload(_shipSelections[1]));
        }
    }

    private void NotifySelectionsChanged()
    {
        OnShipSelectionsChanged?.Invoke();
    }

    private void NotifyTimerChanged()
    {
        OnSelectionTimerChanged?.Invoke(_selectionTimeRemaining);
    }

    private void SetServerState(NetworkMatchState state, string statusMessage)
    {
        SyncLocalState(state, _selectionTimeRemaining, statusMessage);
        BroadcastStateClientRpc((int)state, _selectionTimeRemaining, statusMessage ?? string.Empty);
    }

    private void SyncLocalState(NetworkMatchState state, float selectionTimeRemaining, string statusMessage)
    {
        _currentState = state;
        _selectionTimeRemaining = selectionTimeRemaining;
        _statusMessage = statusMessage ?? string.Empty;

        OnSessionStateChanged?.Invoke(_currentState);
        OnSelectionTimerChanged?.Invoke(_selectionTimeRemaining);
        OnStatusMessageChanged?.Invoke(_statusMessage);
    }

    private int GetConnectedPlayerCount()
    {
        int count = 0;
        foreach (NetworkShipSelectionState selection in _shipSelections)
        {
            if (selection.ClientId != ulong.MaxValue)
            {
                count++;
            }
        }

        return count;
    }

    private int GetOrAssignSlotIndex(ulong clientId)
    {
        for (int i = 0; i < _shipSelections.Length; i++)
        {
            if (_shipSelections[i].ClientId == clientId)
            {
                return i;
            }
        }

        for (int i = 0; i < _shipSelections.Length; i++)
        {
            if (_shipSelections[i].ClientId == ulong.MaxValue)
            {
                _shipSelections[i].ClientId = clientId;
                _shipSelections[i].SlotIndex = i;
                return i;
            }
        }

        return 1;
    }

    private void ClearSlot(ulong clientId)
    {
        for (int i = 0; i < _shipSelections.Length; i++)
        {
            if (_shipSelections[i].ClientId != clientId)
            {
                continue;
            }

            _shipSelections[i].ClientId = ulong.MaxValue;
            _shipSelections[i].ShipId = string.Empty;
            _shipSelections[i].IsLockedIn = false;
        }
    }

    private void ClearAllSelections()
    {
        foreach (NetworkShipSelectionState selection in _shipSelections)
        {
            selection.ClientId = ulong.MaxValue;
            selection.ShipId = string.Empty;
            selection.IsLockedIn = false;
        }
    }

    private static NetworkShipSelectionStatePayload ToPayload(NetworkShipSelectionState selection)
    {
        return new NetworkShipSelectionStatePayload
        {
            ClientId = selection.ClientId,
            SlotIndex = selection.SlotIndex,
            ShipId = selection.ShipId,
            IsLockedIn = selection.IsLockedIn
        };
    }

    private static void ApplyPayload(NetworkShipSelectionState target, NetworkShipSelectionStatePayload payload)
    {
        target.ClientId = payload.ClientId;
        target.SlotIndex = payload.SlotIndex;
        target.ShipId = payload.ShipId.ToString();
        target.IsLockedIn = payload.IsLockedIn;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitShipSelectionServerRpc(string shipId, bool lockIn, ServerRpcParams rpcParams = default)
    {
        ApplyShipSelection(rpcParams.Receive.SenderClientId, shipId, lockIn);
    }

    [ClientRpc]
    private void BroadcastStateClientRpc(int state, float selectionTimeRemaining, string statusMessage)
    {
        if (IsServer)
        {
            return;
        }

        SyncLocalState((NetworkMatchState)state, selectionTimeRemaining, statusMessage);
    }

    [ClientRpc]
    private void BroadcastSelectionsClientRpc(NetworkShipSelectionStatePayload player1, NetworkShipSelectionStatePayload player2)
    {
        if (IsServer)
        {
            return;
        }

        ApplyPayload(_shipSelections[0], player1);
        ApplyPayload(_shipSelections[1], player2);

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetSelectedShips(_shipSelections[0].ShipData, _shipSelections[1].ShipData);
        }

        NotifySelectionsChanged();
    }

    [ClientRpc]
    private void BroadcastTimerClientRpc(float selectionTimeRemaining)
    {
        if (IsServer)
        {
            return;
        }

        _selectionTimeRemaining = selectionTimeRemaining;
        NotifyTimerChanged();
    }
}
