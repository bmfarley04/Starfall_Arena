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

public struct NetworkRoundEndStatePayload : INetworkSerializable
{
    public bool IsVisible;
    public int WinningPlayer;
    public float RoundDuration;
    public float Player1Damage;
    public float Player2Damage;
    public float Player1Accuracy;
    public float Player2Accuracy;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsVisible);
        serializer.SerializeValue(ref WinningPlayer);
        serializer.SerializeValue(ref RoundDuration);
        serializer.SerializeValue(ref Player1Damage);
        serializer.SerializeValue(ref Player2Damage);
        serializer.SerializeValue(ref Player1Accuracy);
        serializer.SerializeValue(ref Player2Accuracy);
    }
}

public struct NetworkRoundStartStatePayload : INetworkSerializable
{
    public int SequenceId;
    public int RoundNumber;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SequenceId);
        serializer.SerializeValue(ref RoundNumber);
    }
}

public struct NetworkWaveStartStatePayload : INetworkSerializable
{
    public int SequenceId;
    public int WaveNumber;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SequenceId);
        serializer.SerializeValue(ref WaveNumber);
    }
}

public struct NetworkInvasionEnemyCountStatePayload : INetworkSerializable
{
    public int AliveEnemyCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AliveEnemyCount);
    }
}

public struct NetworkWinStatePayload : INetworkSerializable
{
    public int Player1Wins;
    public int Player2Wins;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Player1Wins);
        serializer.SerializeValue(ref Player2Wins);
    }
}

public struct NetworkAugmentSelectionStatePayload : INetworkSerializable
{
    public bool IsVisible;
    public int Tier;
    public int Player1OptionCount;
    public FixedString64Bytes Player1Option1;
    public FixedString64Bytes Player1Option2;
    public FixedString64Bytes Player1Option3;
    public int Player2OptionCount;
    public FixedString64Bytes Player2Option1;
    public FixedString64Bytes Player2Option2;
    public FixedString64Bytes Player2Option3;
    public bool Player1LockedIn;
    public bool Player2LockedIn;
    public FixedString64Bytes Player1ChosenAugmentId;
    public FixedString64Bytes Player2ChosenAugmentId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsVisible);
        serializer.SerializeValue(ref Tier);
        serializer.SerializeValue(ref Player1OptionCount);
        serializer.SerializeValue(ref Player1Option1);
        serializer.SerializeValue(ref Player1Option2);
        serializer.SerializeValue(ref Player1Option3);
        serializer.SerializeValue(ref Player2OptionCount);
        serializer.SerializeValue(ref Player2Option1);
        serializer.SerializeValue(ref Player2Option2);
        serializer.SerializeValue(ref Player2Option3);
        serializer.SerializeValue(ref Player1LockedIn);
        serializer.SerializeValue(ref Player2LockedIn);
        serializer.SerializeValue(ref Player1ChosenAugmentId);
        serializer.SerializeValue(ref Player2ChosenAugmentId);
    }
}

public struct NetworkGameEndStatePayload : INetworkSerializable
{
    public bool IsVisible;
    public int WinningPlayer;
    public FixedString64Bytes Player1ShipId;
    public FixedString64Bytes Player2ShipId;
    public float GameDuration;
    public int Player1Wins;
    public int Player1Losses;
    public float Player1DamageDealt;
    public float Player1DamageTaken;
    public float Player1Accuracy;
    public int Player2Wins;
    public int Player2Losses;
    public float Player2DamageDealt;
    public float Player2DamageTaken;
    public float Player2Accuracy;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsVisible);
        serializer.SerializeValue(ref WinningPlayer);
        serializer.SerializeValue(ref Player1ShipId);
        serializer.SerializeValue(ref Player2ShipId);
        serializer.SerializeValue(ref GameDuration);
        serializer.SerializeValue(ref Player1Wins);
        serializer.SerializeValue(ref Player1Losses);
        serializer.SerializeValue(ref Player1DamageDealt);
        serializer.SerializeValue(ref Player1DamageTaken);
        serializer.SerializeValue(ref Player1Accuracy);
        serializer.SerializeValue(ref Player2Wins);
        serializer.SerializeValue(ref Player2Losses);
        serializer.SerializeValue(ref Player2DamageDealt);
        serializer.SerializeValue(ref Player2DamageTaken);
        serializer.SerializeValue(ref Player2Accuracy);
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
    public event Action<int> OnSelectedMapIndexChanged;
    public event Action<NetworkRoundStartStatePayload> OnRoundStartPresentationChanged;
    public event Action<NetworkWaveStartStatePayload> OnWaveStartPresentationChanged;
    public event Action<NetworkInvasionEnemyCountStatePayload> OnInvasionEnemyCountChanged;
    public event Action<NetworkRoundEndStatePayload> OnRoundEndPresentationChanged;
    public event Action<NetworkWinStatePayload> OnWinStateChanged;
    public event Action<NetworkAugmentSelectionStatePayload> OnAugmentSelectionPresentationChanged;
    public event Action<NetworkGameEndStatePayload> OnGameEndPresentationChanged;

    private readonly NetworkShipSelectionState[] _shipSelections =
    {
        new NetworkShipSelectionState { SlotIndex = 0, ClientId = ulong.MaxValue },
        new NetworkShipSelectionState { SlotIndex = 1, ClientId = ulong.MaxValue }
    };

    private NetworkMatchState _currentState = NetworkMatchState.TitleIdle;
    private float _selectionTimeRemaining;
    private string _statusMessage = string.Empty;
    private bool _gameplaySceneLoadRequested;
    private int _selectedMapIndex = -1;
    private readonly string[][] _augmentOptionIds = { new string[3], new string[3] };
    private readonly int[] _augmentOptionCounts = new int[2];
    private readonly string[] _augmentChosenIds = new string[2];
    private readonly bool[] _augmentChoicesLocked = new bool[2];
    private bool _augmentPhaseActive;
    private int _augmentTier = -1;
    private int _roundStartSequenceId = 0;
    private int _waveStartSequenceId = 0;
    private NetworkWaveStartStatePayload _lastWaveStartPresentation;

    public NetworkMatchState CurrentState => _currentState;
    public float SelectionTimeRemaining => _selectionTimeRemaining;
    public string StatusMessage => _statusMessage;
    public string GameplaySceneName => gameplaySceneName;
    public bool IsShipSelectActive => _currentState == NetworkMatchState.ShipSelect;
    public bool HasBothPlayersConnected =>
        _shipSelections[0].ClientId != ulong.MaxValue &&
        _shipSelections[1].ClientId != ulong.MaxValue;
    public bool IsLocalPlayerLockedIn => TryGetLocalSelectionState(out NetworkShipSelectionState selection) && selection.IsLockedIn;
    public int SelectedMapIndex => _selectedMapIndex;
    public bool AreAugmentSelectionsResolved => !_augmentPhaseActive &&
        !string.IsNullOrWhiteSpace(_augmentChosenIds[0]) &&
        !string.IsNullOrWhiteSpace(_augmentChosenIds[1]);
    public ulong LocalClientId => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
    public ulong HostClientId => NetworkManager.Singleton != null ? NetworkManager.ServerClientId : ulong.MaxValue;
    public NetworkShipSelectionState Player1Selection => _shipSelections[0];
    public NetworkShipSelectionState Player2Selection => _shipSelections[1];

    public bool TryGetLastWaveStartPresentation(out NetworkWaveStartStatePayload payload)
    {
        payload = _lastWaveStartPresentation;
        return payload.SequenceId > 0;
    }

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
        if (!IsServer || (_currentState != NetworkMatchState.ShipSelect && _currentState != NetworkMatchState.AugmentPhase))
        {
            return;
        }

        if (_currentState == NetworkMatchState.ShipSelect && AreAllSelectionsLocked())
        {
            FinalizeShipSelections();
            return;
        }

        if (_selectionTimeRemaining > 0f)
        {
            _selectionTimeRemaining = Mathf.Max(0f, _selectionTimeRemaining - Time.unscaledDeltaTime);
            NotifyTimerChanged();
            if (CanSendSessionRpcs())
            {
                BroadcastTimerClientRpc(_selectionTimeRemaining);
            }
        }

        if (_selectionTimeRemaining <= 0f)
        {
            if (_currentState == NetworkMatchState.ShipSelect)
            {
                FinalizeShipSelections();
            }
            else if (_currentState == NetworkMatchState.AugmentPhase)
            {
                FinalizeAugmentSelections();
            }
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

        if (CanSendSessionRpcs())
        {
            BroadcastGameplaySceneNameClientRpc(gameplaySceneName);
        }

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
        _selectedMapIndex = -1;
        _augmentPhaseActive = false;
        _augmentTier = -1;
        ClearAllSelections();
        ClearAugmentSelectionData();
        SyncLocalState(NetworkMatchState.TitleIdle, 0f, statusMessage);
        NotifySelectionsChanged();
        OnSelectedMapIndexChanged?.Invoke(_selectedMapIndex);
    }

    public void SetGameplaySceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        ApplyGameplaySceneName(sceneName);

        if (CanSendSessionRpcs())
        {
            BroadcastGameplaySceneNameClientRpc(gameplaySceneName);
        }
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

        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return;
        }

        if (IsServer)
        {
            ApplyAugmentChoice(LocalClientId, augmentId);
        }
        else
        {
            SubmitAugmentChoiceServerRpc(augmentId);
        }
    }

    public void StartAugmentSelectionServer(int tier, string[] player1OptionIds, string[] player2OptionIds, float selectionDuration)
    {
        if (!IsServer)
        {
            return;
        }

        _augmentTier = tier;
        _augmentPhaseActive = true;
        _selectionTimeRemaining = Mathf.Max(0f, selectionDuration);

        CopyAugmentOptions(0, player1OptionIds);
        CopyAugmentOptions(1, player2OptionIds);

        for (int i = 0; i < _augmentChosenIds.Length; i++)
        {
            _augmentChosenIds[i] = string.Empty;
            _augmentChoicesLocked[i] = false;
        }

        SetServerState(NetworkMatchState.AugmentPhase, "Choose your augment.");
        BroadcastAugmentPresentation();
    }

    public string GetResolvedAugmentIdForSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _augmentChosenIds.Length)
        {
            return string.Empty;
        }

        return _augmentChosenIds[slotIndex];
    }

    public void ClearResolvedAugmentSelectionsServer()
    {
        if (!IsServer)
        {
            return;
        }

        _augmentPhaseActive = false;
        _augmentTier = -1;
        _selectionTimeRemaining = 0f;
        ClearAugmentSelectionData();
        BroadcastAugmentPresentation();
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

    public void SetSelectedMapIndexServer(int mapIndex)
    {
        if (!IsServer)
        {
            return;
        }

        ApplySelectedMapIndexLocal(mapIndex);
        if (CanSendSessionRpcs())
        {
            BroadcastSelectedMapClientRpc(mapIndex);
        }
    }

    public void ShowRoundEndServer(int winningPlayer, float roundDuration, float player1Damage, float player2Damage, float player1Accuracy, float player2Accuracy)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkRoundEndStatePayload payload = new NetworkRoundEndStatePayload
        {
            IsVisible = true,
            WinningPlayer = winningPlayer,
            RoundDuration = roundDuration,
            Player1Damage = player1Damage,
            Player2Damage = player2Damage,
            Player1Accuracy = player1Accuracy,
            Player2Accuracy = player2Accuracy
        };

        OnRoundEndPresentationChanged?.Invoke(payload);

        if (CanSendSessionRpcs())
        {
            BroadcastRoundEndClientRpc(payload);
        }
    }

    public void BroadcastRoundStartServer(int roundNumber)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkRoundStartStatePayload payload = new NetworkRoundStartStatePayload
        {
            SequenceId = ++_roundStartSequenceId,
            RoundNumber = roundNumber
        };

        SetServerState(NetworkMatchState.RoundTransition, $"Round {roundNumber} starting.");
        OnRoundStartPresentationChanged?.Invoke(payload);

        if (CanSendSessionRpcs())
        {
            BroadcastRoundStartClientRpc(payload);
        }
    }

    public void BroadcastWaveStartServer(int waveNumber)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkWaveStartStatePayload payload = new NetworkWaveStartStatePayload
        {
            SequenceId = ++_waveStartSequenceId,
            WaveNumber = waveNumber
        };

        _lastWaveStartPresentation = payload;
        SetServerState(NetworkMatchState.RoundTransition, $"Wave {waveNumber} starting.");
        OnWaveStartPresentationChanged?.Invoke(payload);

        if (CanSendSessionRpcs())
        {
            BroadcastWaveStartClientRpc(payload);
        }
    }

    public void BroadcastInvasionEnemyCountServer(int aliveEnemyCount)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkInvasionEnemyCountStatePayload payload = new NetworkInvasionEnemyCountStatePayload
        {
            AliveEnemyCount = Mathf.Max(0, aliveEnemyCount)
        };

        OnInvasionEnemyCountChanged?.Invoke(payload);

        if (CanSendSessionRpcs())
        {
            BroadcastInvasionEnemyCountClientRpc(payload);
        }
    }

    public void HideRoundEndServer()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkRoundEndStatePayload payload = new NetworkRoundEndStatePayload { IsVisible = false };
        OnRoundEndPresentationChanged?.Invoke(payload);
        if (CanSendSessionRpcs())
        {
            BroadcastRoundEndClientRpc(payload);
        }
    }

    public void BroadcastWinStateServer(int player1Wins, int player2Wins)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkWinStatePayload payload = new NetworkWinStatePayload
        {
            Player1Wins = Mathf.Max(0, player1Wins),
            Player2Wins = Mathf.Max(0, player2Wins)
        };

        OnWinStateChanged?.Invoke(payload);
        if (CanSendSessionRpcs())
        {
            BroadcastWinStateClientRpc(payload);
        }
    }

    public void ShowGameEndServer(
        int winningPlayer,
        ShipData player1Ship,
        ShipData player2Ship,
        float gameDuration,
        int player1Wins,
        int player1Losses,
        float player1DamageDealt,
        float player1DamageTaken,
        float player1Accuracy,
        int player2Wins,
        int player2Losses,
        float player2DamageDealt,
        float player2DamageTaken,
        float player2Accuracy)
    {
        if (!IsServer)
        {
            return;
        }

        NetworkGameEndStatePayload payload = new NetworkGameEndStatePayload
        {
            IsVisible = true,
            WinningPlayer = winningPlayer,
            Player1ShipId = player1Ship != null ? player1Ship.ShipId : string.Empty,
            Player2ShipId = player2Ship != null ? player2Ship.ShipId : string.Empty,
            GameDuration = gameDuration,
            Player1Wins = player1Wins,
            Player1Losses = player1Losses,
            Player1DamageDealt = player1DamageDealt,
            Player1DamageTaken = player1DamageTaken,
            Player1Accuracy = player1Accuracy,
            Player2Wins = player2Wins,
            Player2Losses = player2Losses,
            Player2DamageDealt = player2DamageDealt,
            Player2DamageTaken = player2DamageTaken,
            Player2Accuracy = player2Accuracy
        };

        OnGameEndPresentationChanged?.Invoke(payload);
        if (CanSendSessionRpcs())
        {
            BroadcastGameEndClientRpc(payload);
        }
    }

    public void HideGameEndServer()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkGameEndStatePayload payload = new NetworkGameEndStatePayload { IsVisible = false };
        OnGameEndPresentationChanged?.Invoke(payload);
        if (CanSendSessionRpcs())
        {
            BroadcastGameEndClientRpc(payload);
        }
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

        // Ship-select navigation should remain local-only until the player commits.
        // Guard non-lock updates here so accidental preview submissions cannot
        // synchronize host/client browsing state.
        if (!lockIn)
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
        TryAdvanceShipSelectServer();
    }

    private void ApplyAugmentChoice(ulong clientId, string augmentId)
    {
        if (_currentState != NetworkMatchState.AugmentPhase || !_augmentPhaseActive)
        {
            return;
        }

        int slot = GetOrAssignSlotIndex(clientId);
        if (!IsAugmentChoiceValidForSlot(slot, augmentId))
        {
            return;
        }

        _augmentChosenIds[slot] = augmentId;
        _augmentChoicesLocked[slot] = true;
        BroadcastAugmentPresentation();

        if (_augmentChoicesLocked[0] && _augmentChoicesLocked[1])
        {
            FinalizeAugmentSelections();
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

        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            _gameplaySceneLoadRequested = false;
            SetServerState(
                NetworkMatchState.Error,
                $"Gameplay scene '{gameplaySceneName}' is not in Build Settings.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }

    private void FinalizeAugmentSelections()
    {
        if (!IsServer || !_augmentPhaseActive)
        {
            return;
        }

        for (int slot = 0; slot < _augmentChosenIds.Length; slot++)
        {
            if (_augmentChoicesLocked[slot] && !string.IsNullOrWhiteSpace(_augmentChosenIds[slot]))
            {
                continue;
            }

            _augmentChosenIds[slot] = GetRandomAugmentOptionId(slot);
            _augmentChoicesLocked[slot] = !string.IsNullOrWhiteSpace(_augmentChosenIds[slot]);
        }

        _selectionTimeRemaining = 0f;
        _augmentPhaseActive = false;
        BroadcastAugmentPresentation();
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

    private void TryAdvanceShipSelectServer()
    {
        if (!IsServer || _currentState != NetworkMatchState.ShipSelect)
        {
            return;
        }

        if (AreAllSelectionsLocked())
        {
            FinalizeShipSelections();
        }
    }

    private void CopyAugmentOptions(int slot, string[] optionIds)
    {
        if (slot < 0 || slot >= _augmentOptionIds.Length)
        {
            return;
        }

        _augmentOptionCounts[slot] = Mathf.Clamp(optionIds != null ? optionIds.Length : 0, 0, _augmentOptionIds[slot].Length);
        for (int i = 0; i < _augmentOptionIds[slot].Length; i++)
        {
            _augmentOptionIds[slot][i] = i < _augmentOptionCounts[slot]
                ? optionIds[i] ?? string.Empty
                : string.Empty;
        }
    }

    private string GetRandomAugmentOptionId(int slot)
    {
        if (slot < 0 || slot >= _augmentOptionIds.Length || _augmentOptionCounts[slot] <= 0)
        {
            return string.Empty;
        }

        int randomIndex = UnityEngine.Random.Range(0, _augmentOptionCounts[slot]);
        return _augmentOptionIds[slot][randomIndex] ?? string.Empty;
    }

    private bool IsAugmentChoiceValidForSlot(int slot, string augmentId)
    {
        if (slot < 0 || slot >= _augmentOptionIds.Length || string.IsNullOrWhiteSpace(augmentId))
        {
            return false;
        }

        for (int i = 0; i < _augmentOptionCounts[slot]; i++)
        {
            if (_augmentOptionIds[slot][i] == augmentId)
            {
                return true;
            }
        }

        return false;
    }

    private void BroadcastSelections()
    {
        NotifySelectionsChanged();

        if (CanSendSessionRpcs())
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
        if (CanSendSessionRpcs())
        {
            BroadcastStateClientRpc((int)state, _selectionTimeRemaining, statusMessage ?? string.Empty);
        }
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

    private void ClearAugmentSelectionData()
    {
        for (int slot = 0; slot < _augmentOptionIds.Length; slot++)
        {
            _augmentOptionCounts[slot] = 0;
            _augmentChosenIds[slot] = string.Empty;
            _augmentChoicesLocked[slot] = false;

            for (int optionIndex = 0; optionIndex < _augmentOptionIds[slot].Length; optionIndex++)
            {
                _augmentOptionIds[slot][optionIndex] = string.Empty;
            }
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

    private NetworkAugmentSelectionStatePayload CreateAugmentPayload(bool isVisible)
    {
        return new NetworkAugmentSelectionStatePayload
        {
            IsVisible = isVisible,
            Tier = _augmentTier,
            Player1OptionCount = _augmentOptionCounts[0],
            Player1Option1 = _augmentOptionIds[0][0],
            Player1Option2 = _augmentOptionIds[0][1],
            Player1Option3 = _augmentOptionIds[0][2],
            Player2OptionCount = _augmentOptionCounts[1],
            Player2Option1 = _augmentOptionIds[1][0],
            Player2Option2 = _augmentOptionIds[1][1],
            Player2Option3 = _augmentOptionIds[1][2],
            Player1LockedIn = _augmentChoicesLocked[0],
            Player2LockedIn = _augmentChoicesLocked[1],
            Player1ChosenAugmentId = _augmentChosenIds[0],
            Player2ChosenAugmentId = _augmentChosenIds[1]
        };
    }

    private bool CanSendSessionRpcs()
    {
        return IsServer &&
               IsSpawned &&
               NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening;
    }

    private void ApplySelectedMapIndexLocal(int mapIndex)
    {
        _selectedMapIndex = mapIndex;
        OnSelectedMapIndexChanged?.Invoke(_selectedMapIndex);
    }

    private void ApplyGameplaySceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        gameplaySceneName = sceneName.Trim();
    }

    private void BroadcastAugmentPresentation()
    {
        NetworkAugmentSelectionStatePayload payload = CreateAugmentPayload(_augmentPhaseActive);
        OnAugmentSelectionPresentationChanged?.Invoke(payload);

        if (CanSendSessionRpcs())
        {
            BroadcastAugmentSelectionClientRpc(payload);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitShipSelectionServerRpc(string shipId, bool lockIn, ServerRpcParams rpcParams = default)
    {
        ApplyShipSelection(rpcParams.Receive.SenderClientId, shipId, lockIn);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitAugmentChoiceServerRpc(string augmentId, ServerRpcParams rpcParams = default)
    {
        ApplyAugmentChoice(rpcParams.Receive.SenderClientId, augmentId);
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

    [ClientRpc]
    private void BroadcastSelectedMapClientRpc(int mapIndex)
    {
        if (IsServer)
        {
            return;
        }

        ApplySelectedMapIndexLocal(mapIndex);
    }

    [ClientRpc]
    private void BroadcastRoundStartClientRpc(NetworkRoundStartStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnRoundStartPresentationChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastWaveStartClientRpc(NetworkWaveStartStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnWaveStartPresentationChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastInvasionEnemyCountClientRpc(NetworkInvasionEnemyCountStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnInvasionEnemyCountChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastRoundEndClientRpc(NetworkRoundEndStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnRoundEndPresentationChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastWinStateClientRpc(NetworkWinStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnWinStateChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastAugmentSelectionClientRpc(NetworkAugmentSelectionStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        _augmentTier = payload.Tier;
        _augmentPhaseActive = payload.IsVisible;
        _augmentOptionCounts[0] = payload.Player1OptionCount;
        _augmentOptionIds[0][0] = payload.Player1Option1.ToString();
        _augmentOptionIds[0][1] = payload.Player1Option2.ToString();
        _augmentOptionIds[0][2] = payload.Player1Option3.ToString();
        _augmentOptionCounts[1] = payload.Player2OptionCount;
        _augmentOptionIds[1][0] = payload.Player2Option1.ToString();
        _augmentOptionIds[1][1] = payload.Player2Option2.ToString();
        _augmentOptionIds[1][2] = payload.Player2Option3.ToString();
        _augmentChoicesLocked[0] = payload.Player1LockedIn;
        _augmentChoicesLocked[1] = payload.Player2LockedIn;
        _augmentChosenIds[0] = payload.Player1ChosenAugmentId.ToString();
        _augmentChosenIds[1] = payload.Player2ChosenAugmentId.ToString();
        OnAugmentSelectionPresentationChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastGameEndClientRpc(NetworkGameEndStatePayload payload)
    {
        if (IsServer)
        {
            return;
        }

        OnGameEndPresentationChanged?.Invoke(payload);
    }

    [ClientRpc]
    private void BroadcastGameplaySceneNameClientRpc(string sceneName)
    {
        if (IsServer)
        {
            return;
        }

        ApplyGameplaySceneName(sceneName);
    }
}
