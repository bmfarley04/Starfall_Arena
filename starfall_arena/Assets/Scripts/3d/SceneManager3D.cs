using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneManager3D : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Fallback Ships")]
    [SerializeField] private ShipData defaultPlayer1Ship;
    [SerializeField] private ShipData defaultPlayer2Ship;

    [Header("Shared UI Managers")]
    [SerializeField] private VersusScreenManager versusScreenManager;
    [SerializeField] private RoundEndScreenManager roundEndScreenManager;
    [SerializeField] private GameEndScreenManager gameEndScreenManager;

    [Header("Round UI")]
    [SerializeField] private CanvasGroup roundTextCanvasGroup;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private CanvasGroup countdownCanvasGroup;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Gameplay HUD")]
    [Tooltip("Roots for 3D gameplay HUD canvases/managers that should be hidden during round-end and game-end presentation.")]
    [SerializeField] private GameObject[] gameplayHudRoots;
    [Tooltip("Canvases that should be forced onto the 3D UI camera/sorting setup in network play.")]
    [SerializeField] private Canvas[] uiCanvases;
    [SerializeField] private Camera uiCamera;
    [SerializeField] private int baseCanvasSortingOrder = 200;

    [Header("Arena Boundary")]
    [Tooltip("Optional shrinking 3D arena boundary started and stopped with each combat round.")]
    [SerializeField] private ArenaBoundary3D arenaBoundary;

    [Header("Win Trackers")]
    [SerializeField] private StarfallArena.UI.WinTracker player1WinTracker;
    [SerializeField] private StarfallArena.UI.WinTracker player2WinTracker;

    [Header("Timing")]
    [SerializeField] private float spawnRetryIntervalSeconds = 0.1f;
    [SerializeField] private float deathToRoundEndDelay = 1.5f;
    [SerializeField] private float roundTextDisplayDuration = 1.5f;
    [SerializeField] private float countdownInterval = 1f;
    [SerializeField] private float roundEndScreenDuration = 4f;
    [SerializeField] private float textFadeDuration = 0.3f;
    [SerializeField] private float roundEndSettleDelay = 0.5f;

    [Header("Match Rules")]
    [SerializeField] private int winsRequired = 3;

    [Header("Debug")]
    [Range(0, 5)]
    [SerializeField] private int debugStartAtRound = 0;

    private int _currentRound;
    private int _player1Wins;
    private int _player2Wins;
    private float _roundStartTime;
    private float _totalGameDuration;

    private Player3D _player1;
    private Player3D _player2;
    private ShipData _player1Data;
    private ShipData _player2Data;

    private bool _roundOver;
    private int _roundWinner;
    private bool _versusScreenDone;
    private bool _useNetworkSession;
    private bool _isAuthoritativeController = true;
    private NetworkSessionData _networkSession;
    private Coroutine _networkSessionSubscriptionCoroutine;
    private int _lastRoundIntroSequenceId = -1;
    private Coroutine _activeRoundIntroCoroutine;

    private CombatStatsSnapshot _deadPlayerStatsSnapshot;
    private CombatStatsSnapshot _player1TotalStats;
    private CombatStatsSnapshot _player2TotalStats;

    private IEnumerator Start()
    {
        yield return null;
        RefreshNetworkMode();

        SubscribeNetworkSessionEvents();
        _networkSessionSubscriptionCoroutine = StartCoroutine(EnsureNetworkSessionSubscription());
        ResolveShipData();
        ConfigureNetworkUiCanvases();
        SetInitialUiState();

        if (versusScreenManager != null)
        {
            if (!versusScreenManager.gameObject.activeSelf)
            {
                versusScreenManager.gameObject.SetActive(true);
            }

            versusScreenManager.onVersusScreenComplete.AddListener(OnVersusScreenComplete);
        }

        if (_isAuthoritativeController)
        {
            StartCoroutine(GameLoop());
        }
    }

    private void OnDestroy()
    {
        if (versusScreenManager != null)
        {
            versusScreenManager.onVersusScreenComplete.RemoveListener(OnVersusScreenComplete);
        }

        UnsubscribePlayerDeath(_player1);
        UnsubscribePlayerDeath(_player2);
        if (_networkSessionSubscriptionCoroutine != null)
        {
            StopCoroutine(_networkSessionSubscriptionCoroutine);
            _networkSessionSubscriptionCoroutine = null;
        }

        UnsubscribeNetworkSessionEvents();
    }

    private void RefreshNetworkMode()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        _useNetworkSession = NetMgr.IsNetworked && networkManager != null;
        _isAuthoritativeController = !_useNetworkSession || networkManager.IsServer;
    }

    private IEnumerator EnsureNetworkSessionSubscription()
    {
        while (isActiveAndEnabled)
        {
            RefreshNetworkMode();

            if (_useNetworkSession)
            {
                NetworkSessionData session = NetworkSessionData.Instance;
                if (session != null && session != _networkSession)
                {
                    SubscribeNetworkSessionEvents();
                    ConfigureNetworkUiCanvases();
                }
            }

            yield return new WaitForSecondsRealtime(spawnRetryIntervalSeconds);
        }
    }

    private IEnumerator GameLoop()
    {
        SetupDebugStartState();

        if (debugStartAtRound <= 0)
        {
            if (versusScreenManager != null && versusScreenManager.gameObject.activeInHierarchy)
            {
                yield return new WaitUntil(() => _versusScreenDone);
            }
            else
            {
                _versusScreenDone = true;
            }
        }
        else if (versusScreenManager != null)
        {
            versusScreenManager.gameObject.SetActive(false);
        }

        while (_player1Wins < winsRequired && _player2Wins < winsRequired)
        {
            _currentRound++;
            yield return SpawnPlayers();

            StartArenaBoundaryRound();
            SetGameplayHudActive(true);
            SetPlayersMovementLocked(true);

            if (_useNetworkSession && NetworkSessionData.Instance != null)
            {
                NetworkSessionData.Instance.BroadcastRoundStartServer(_currentRound);
                yield return new WaitForSecondsRealtime(GetRoundIntroDuration());
                NetworkSessionData.Instance.MarkMatchStarted();
            }
            else
            {
                yield return ShowRoundText(_currentRound);
                yield return ShowCountdown();
            }

            ResetRoundState();
            SetPlayersMovementLocked(false);

            yield return new WaitUntil(() => _roundOver);

            StopCombatActions(_player1);
            StopCombatActions(_player2);
            SetPlayersMovementLocked(true);
            StopArenaBoundaryRound();

            yield return new WaitForSeconds(deathToRoundEndDelay);

            float roundDuration = Time.time - _roundStartTime;
            _totalGameDuration += roundDuration;
            RoundStatsSnapshot roundStats = CaptureRoundStats();

            SetGameplayHudActive(false);

            if (_useNetworkSession && NetworkSessionData.Instance != null)
            {
                NetworkSessionData.Instance.MarkRoundTransition();
                NetworkSessionData.Instance.ShowRoundEndServer(
                    _roundWinner,
                    roundDuration,
                    roundStats.Player1.damageDealt,
                    roundStats.Player2.damageDealt,
                    CalculateAccuracy(roundStats.Player1),
                    CalculateAccuracy(roundStats.Player2));
            }
            else if (roundEndScreenManager != null)
            {
                roundEndScreenManager.ShowRoundEndScreen(
                    _roundWinner,
                    roundDuration,
                    roundStats.Player1.damageDealt,
                    roundStats.Player2.damageDealt,
                    CalculateAccuracy(roundStats.Player1),
                    CalculateAccuracy(roundStats.Player2));
            }

            yield return new WaitForSecondsRealtime(roundEndScreenDuration);

            if (_useNetworkSession && NetworkSessionData.Instance != null)
            {
                NetworkSessionData.Instance.HideRoundEndServer();
            }
            else if (roundEndScreenManager != null)
            {
                roundEndScreenManager.HideRoundEndScreen();
            }

            yield return new WaitForSecondsRealtime(roundEndSettleDelay);

            ApplyRoundWin();
            DestroyPlayers();
        }

        yield return ShowGameEnd();
    }

    private void ResetRoundState()
    {
        _roundStartTime = Time.time;
        _roundOver = false;
        _roundWinner = 0;
        _deadPlayerStatsSnapshot = default;
    }

    private void ResolveShipData()
    {
        if (_useNetworkSession && NetworkSessionData.Instance != null)
        {
            _player1Data = NetworkSessionData.Instance.Player1Selection?.ShipData;
            _player2Data = NetworkSessionData.Instance.Player2Selection?.ShipData;
        }

        if (GameDataManager.Instance != null && GameDataManager.Instance.selectedShipClasses != null)
        {
            if (_player1Data == null && GameDataManager.Instance.selectedShipClasses.Count > 0)
            {
                _player1Data = GameDataManager.Instance.selectedShipClasses[0];
            }

            if (_player2Data == null && GameDataManager.Instance.selectedShipClasses.Count > 1)
            {
                _player2Data = GameDataManager.Instance.selectedShipClasses[1];
            }
        }

        _player1Data = ResolveValid3DShip(_player1Data, defaultPlayer1Ship, 1);
        _player2Data = ResolveValid3DShip(_player2Data, defaultPlayer2Ship, 2);
        GameDataManager.Instance?.SetSelectedShips(_player1Data, _player2Data);
    }

    private ShipData ResolveValid3DShip(ShipData candidate, ShipData fallback, int slot)
    {
        ShipData resolved = candidate;
        if (!IsKnown3DShip(resolved))
        {
            if (resolved != null)
            {
                Debug.LogWarning($"[SceneManager3D] Ship '{resolved.ShipId}' is not registered in the 3D roster. Falling back for player {slot}.", this);
            }

            resolved = fallback;
        }

        if (resolved != null && resolved.shipPrefab == null)
        {
            Debug.LogError($"[SceneManager3D] Ship '{resolved.ShipId}' has no 3D gameplay prefab assigned.", this);
            return null;
        }

        return resolved;
    }

    private static bool IsKnown3DShip(ShipData shipData)
    {
        if (shipData == null || GameDataManager.Instance == null)
        {
            return false;
        }

        IReadOnlyList<ShipData> knownShips = GameDataManager.Instance.Known3DShips;
        for (int i = 0; i < knownShips.Count; i++)
        {
            if (knownShips[i] == shipData)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator SpawnPlayers()
    {
        if (_useNetworkSession)
        {
            yield return SpawnPlayersNetworked();
            yield break;
        }

        _player1 = SpawnLocalPlayer(_player1Data, player1SpawnPoint, "Player1");
        _player2 = SpawnLocalPlayer(_player2Data, player2SpawnPoint, "Player2");
        PlayerHUDManager3D.RebindAllAutoManagers();
        yield return null;
    }

    private IEnumerator SpawnPlayersNetworked()
    {
        if (!_isAuthoritativeController)
        {
            yield break;
        }

        while (!AreRequiredClientsConnected())
        {
            yield return new WaitForSeconds(spawnRetryIntervalSeconds);
        }

        if (TryBindExistingNetworkPlayers())
        {
            yield return null;
            PlayerHUDManager3D.RebindAllAutoManagers();
            NetworkSessionData.Instance?.SetLocalState(NetworkMatchState.RoundTransition, $"Round {_currentRound} starting.");
            yield break;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        ulong player1OwnerId = ResolveOwnerClientIdForSlot(0);
        ulong player2OwnerId = ResolveOwnerClientIdForSlot(1);

        _player1 = SpawnNetworkPlayer(_player1Data, player1SpawnPoint, player1OwnerId, 1);
        _player2 = SpawnNetworkPlayer(_player2Data, player2SpawnPoint, player2OwnerId, 2);

        yield return null;
        PlayerHUDManager3D.RebindAllAutoManagers();
        session?.SetLocalState(NetworkMatchState.RoundTransition, $"Round {_currentRound} starting.");
    }

    private bool TryBindExistingNetworkPlayers()
    {
        if (!NetMovement3D.TryGetPlayerBySlot(1, out NetMovement3D player1Movement) ||
            !NetMovement3D.TryGetPlayerBySlot(2, out NetMovement3D player2Movement))
        {
            return false;
        }

        Player3D existingPlayer1 = player1Movement.GetComponent<Player3D>();
        Player3D existingPlayer2 = player2Movement.GetComponent<Player3D>();
        if (existingPlayer1 == null || existingPlayer2 == null)
        {
            return false;
        }

        PrepareSpawnedPlayer(existingPlayer1, 1);
        PrepareSpawnedPlayer(existingPlayer2, 2);
        return true;
    }

    private Player3D SpawnLocalPlayer(ShipData shipData, Transform spawnPoint, string playerTag)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            Debug.LogError($"[SceneManager3D] Cannot spawn {playerTag}: missing ShipData or ship prefab.", this);
            return null;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        GameObject instance = Instantiate(shipData.shipPrefab, position, rotation);
        instance.name = $"Player3D_{playerTag}_{shipData.name}";
        instance.tag = playerTag;

        Player3D player = instance.GetComponent<Player3D>();
        if (player == null)
        {
            Debug.LogError($"[SceneManager3D] Spawned {playerTag} prefab '{shipData.name}' is missing Player3D.", instance);
            Destroy(instance);
            return null;
        }

        PrepareSpawnedPlayer(player, playerTag == "Player1" ? (byte)1 : (byte)2);
        return player;
    }

    private Player3D SpawnNetworkPlayer(ShipData shipData, Transform spawnPoint, ulong ownerClientId, byte playerSlot)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            Debug.LogError($"[SceneManager3D] Cannot network-spawn player {playerSlot}: missing ShipData or ship prefab.", this);
            return null;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        GameObject instance = NetMgr.SpawnPlayerNetworked(shipData.shipPrefab, position, rotation, ownerClientId);
        if (instance == null)
        {
            Debug.LogError($"[SceneManager3D] NetMgr failed to spawn player {playerSlot} with ship '{shipData.ShipId}'.", this);
            return null;
        }

        instance.name = $"NetworkPlayer3D_{playerSlot}_{shipData.name}";
        instance.tag = playerSlot == 1 ? "Player1" : "Player2";

        Player3D player = instance.GetComponent<Player3D>();
        if (player == null)
        {
            Debug.LogError($"[SceneManager3D] Spawned network ship '{shipData.ShipId}' is missing Player3D.", instance);
            return null;
        }

        NetMovement3D movement = instance.GetComponent<NetMovement3D>();
        if (movement == null)
        {
            Debug.LogError($"[SceneManager3D] Spawned network ship '{shipData.ShipId}' is missing NetMovement3D.", instance);
        }
        else
        {
            movement.SetNetworkPlayerIndex(playerSlot);
        }

        if (instance.GetComponent<NetCombat3D>() == null)
        {
            Debug.LogWarning($"[SceneManager3D] Spawned network ship '{shipData.ShipId}' is missing NetCombat3D; combat replication will not work for this player.", instance);
        }

        PrepareSpawnedPlayer(player, playerSlot);
        return player;
    }

    private void PrepareSpawnedPlayer(Player3D player, byte playerSlot)
    {
        if (player == null)
        {
            return;
        }

        PlayerCombatStats3D stats = player.GetComponent<PlayerCombatStats3D>();
        if (stats == null)
        {
            stats = player.gameObject.AddComponent<PlayerCombatStats3D>();
        }

        stats.ResetStats();
        SubscribePlayerDeath(player);
        SetPlayerMovementLocked(player, true);

        if (playerSlot == 1)
        {
            _player1 = player;
        }
        else if (playerSlot == 2)
        {
            _player2 = player;
        }
    }

    private bool AreRequiredClientsConnected()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || NetworkManager.Singleton.ConnectedClientsIds == null)
        {
            return false;
        }

        return NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
    }

    private ulong ResolveOwnerClientIdForSlot(int slotIndex)
    {
        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            ulong sessionClientId = slotIndex == 0
                ? session.Player1Selection?.ClientId ?? ulong.MaxValue
                : session.Player2Selection?.ClientId ?? ulong.MaxValue;
            if (sessionClientId != ulong.MaxValue)
            {
                return sessionClientId;
            }
        }

        List<ulong> connectedClients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        connectedClients.Sort();
        if (slotIndex >= 0 && slotIndex < connectedClients.Count)
        {
            return connectedClients[slotIndex];
        }

        return NetworkManager.ServerClientId;
    }

    private void SubscribePlayerDeath(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        player.Died -= OnPlayerDeath;
        player.Died += OnPlayerDeath;
    }

    private void UnsubscribePlayerDeath(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        player.Died -= OnPlayerDeath;
    }

    private void OnPlayerDeath(Entity3D deadEntity)
    {
        if (_roundOver || deadEntity == null)
        {
            return;
        }

        _roundOver = true;
        PlayerCombatStats3D deadStats = deadEntity.GetComponent<PlayerCombatStats3D>();
        _deadPlayerStatsSnapshot = CopyStats(deadStats);

        if (deadEntity.CompareTag("Player1"))
        {
            _roundWinner = 2;
            UnsubscribePlayerDeath(_player1);
            _player1 = null;
        }
        else if (deadEntity.CompareTag("Player2"))
        {
            _roundWinner = 1;
            UnsubscribePlayerDeath(_player2);
            _player2 = null;
        }
    }

    private RoundStatsSnapshot CaptureRoundStats()
    {
        CombatStatsSnapshot player1Stats = _roundWinner == 2 ? _deadPlayerStatsSnapshot : CopyStats(_player1 != null ? _player1.GetComponent<PlayerCombatStats3D>() : null);
        CombatStatsSnapshot player2Stats = _roundWinner == 1 ? _deadPlayerStatsSnapshot : CopyStats(_player2 != null ? _player2.GetComponent<PlayerCombatStats3D>() : null);

        AddStats(ref _player1TotalStats, player1Stats);
        AddStats(ref _player2TotalStats, player2Stats);

        return new RoundStatsSnapshot
        {
            Player1 = player1Stats,
            Player2 = player2Stats
        };
    }

    private void ApplyRoundWin()
    {
        if (_roundWinner == 1)
        {
            _player1Wins++;
        }
        else if (_roundWinner == 2)
        {
            _player2Wins++;
        }

        UpdateWinTrackers();
        NetworkSessionData.Instance?.BroadcastWinStateServer(_player1Wins, _player2Wins);
    }

    private IEnumerator ShowGameEnd()
    {
        if (gameEndScreenManager == null)
        {
            yield break;
        }

        StopArenaBoundaryRound();
        SetGameplayHudActive(false);
        int winner = _player1Wins >= winsRequired ? 1 : 2;

        if (_useNetworkSession && NetworkSessionData.Instance != null)
        {
            NetworkSessionData.Instance.SetLocalState(NetworkMatchState.MatchComplete, "Match complete.");
            NetworkSessionData.Instance.ShowGameEndServer(
                winner,
                _player1Data,
                _player2Data,
                _totalGameDuration,
                _player1Wins,
                _player2Wins,
                _player1TotalStats.damageDealt,
                _player1TotalStats.damageTaken,
                CalculateAccuracy(_player1TotalStats),
                _player2Wins,
                _player1Wins,
                _player2TotalStats.damageDealt,
                _player2TotalStats.damageTaken,
                CalculateAccuracy(_player2TotalStats));
            yield break;
        }

        CombatStatsSnapshot winnerStats = winner == 1 ? _player1TotalStats : _player2TotalStats;
        int winnerWins = winner == 1 ? _player1Wins : _player2Wins;
        int winnerLosses = winner == 1 ? _player2Wins : _player1Wins;

        gameEndScreenManager.ShowGameEndScreen(
            winner,
            winner,
            winner == 1 ? _player1Data : _player2Data,
            _totalGameDuration,
            winnerWins,
            winnerLosses,
            winnerStats.damageDealt,
            winnerStats.damageTaken,
            CalculateAccuracy(winnerStats));
    }

    private void DestroyPlayers()
    {
        DestroyPlayer(_player1);
        DestroyPlayer(_player2);
        _player1 = null;
        _player2 = null;
        PlayerHUDManager3D.RebindAllAutoManagers();
    }

    private void DestroyPlayer(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        UnsubscribePlayerDeath(player);

        if (_useNetworkSession)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Despawn(true);
            }

            return;
        }

        Destroy(player.gameObject);
    }

    private void StopCombatActions(Player3D player)
    {
        if (player == null)
        {
            return;
        }

        Weapon3D[] weapons = player.Weapons;
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i]?.Die();
        }

        Ability3D[] abilities = player.Abilities;
        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i]?.Die();
        }
    }

    private void SetPlayersMovementLocked(bool isLocked)
    {
        SetPlayerMovementLocked(_player1, isLocked);
        SetPlayerMovementLocked(_player2, isLocked);
    }

    private void SetPlayerMovementLocked(Player3D player, bool isLocked)
    {
        if (player == null)
        {
            return;
        }

        NetMovement3D movement = player.GetComponent<NetMovement3D>();
        if (_useNetworkSession && movement != null)
        {
            movement.SetMovementLockedAuthoritative(isLocked);
            return;
        }

        if (player.PlayerInput3D != null)
        {
            player.PlayerInput3D.SetCombatInputSuppressed(isLocked);
        }

        if (!_useNetworkSession && player.PlayerInput3D != null)
        {
            player.PlayerInput3D.enabled = !isLocked;
        }
    }

    private void StartArenaBoundaryRound()
    {
        if (arenaBoundary == null)
        {
            return;
        }

        arenaBoundary.ResetBoundary();
        arenaBoundary.StartBoundary();
    }

    private void StopArenaBoundaryRound()
    {
        if (arenaBoundary == null)
        {
            return;
        }

        arenaBoundary.StopBoundary();
    }

    private void SetInitialUiState()
    {
        if (roundTextCanvasGroup != null)
        {
            roundTextCanvasGroup.alpha = 0f;
        }

        if (countdownCanvasGroup != null)
        {
            countdownCanvasGroup.alpha = 0f;
        }

        SetGameplayHudActive(false);
        UpdateWinTrackers();
    }

    private void SetGameplayHudActive(bool active)
    {
        if (gameplayHudRoots != null)
        {
            for (int i = 0; i < gameplayHudRoots.Length; i++)
            {
                if (gameplayHudRoots[i] != null)
                {
                    gameplayHudRoots[i].SetActive(active);
                }
            }
        }

        if (active)
        {
            ConfigureNetworkUiCanvases();
            PlayerHUDManager3D.RebindAllAutoManagers();
        }
    }

    private void ConfigureNetworkUiCanvases()
    {
        if (!_useNetworkSession || uiCanvases == null)
        {
            return;
        }

        Camera resolvedCamera = ResolveUiCamera();
        for (int i = 0; i < uiCanvases.Length; i++)
        {
            Canvas canvas = uiCanvases[i];
            if (canvas == null)
            {
                continue;
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = resolvedCamera;
            canvas.planeDistance = 100f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = baseCanvasSortingOrder + i;
        }
    }

    private Camera ResolveUiCamera()
    {
        if (uiCamera != null)
        {
            return uiCamera;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].name == "UICamera")
            {
                uiCamera = cameras[i];
                return uiCamera;
            }
        }

        uiCamera = Camera.main;
        return uiCamera;
    }

    private IEnumerator ShowRoundText(int roundNumber)
    {
        if (roundText == null || roundTextCanvasGroup == null)
        {
            yield break;
        }

        roundText.text = $"ROUND {roundNumber}";
        yield return FadeCanvasGroup(roundTextCanvasGroup, 0f, 1f, textFadeDuration);
        yield return new WaitForSecondsRealtime(roundTextDisplayDuration);
        yield return FadeCanvasGroup(roundTextCanvasGroup, 1f, 0f, textFadeDuration);
    }

    private IEnumerator ShowCountdown()
    {
        if (countdownText == null || countdownCanvasGroup == null)
        {
            yield break;
        }

        for (int i = 3; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            yield return FadeCanvasGroup(countdownCanvasGroup, 0f, 1f, 0.15f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, countdownInterval - 0.3f));
            yield return FadeCanvasGroup(countdownCanvasGroup, 1f, 0f, 0.15f);
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        group.alpha = to;
    }

    private float GetRoundIntroDuration()
    {
        return (textFadeDuration * 2f) + roundTextDisplayDuration + (Mathf.Max(0f, countdownInterval) * 3f);
    }

    private void OnVersusScreenComplete()
    {
        _versusScreenDone = true;
    }

    private void SubscribeNetworkSessionEvents()
    {
        NetworkSessionData session = NetworkSessionData.Instance;
        if (session == null || session == _networkSession)
        {
            return;
        }

        UnsubscribeNetworkSessionEvents();
        _networkSession = session;
        session.OnRoundStartPresentationChanged += HandleRoundStartPresentationChanged;
        session.OnRoundEndPresentationChanged += HandleRoundEndPresentationChanged;
        session.OnWinStateChanged += HandleWinStateChanged;
        session.OnGameEndPresentationChanged += HandleGameEndPresentationChanged;
    }

    private void UnsubscribeNetworkSessionEvents()
    {
        NetworkSessionData session = _networkSession;
        if (session == null)
        {
            return;
        }

        session.OnRoundStartPresentationChanged -= HandleRoundStartPresentationChanged;
        session.OnRoundEndPresentationChanged -= HandleRoundEndPresentationChanged;
        session.OnWinStateChanged -= HandleWinStateChanged;
        session.OnGameEndPresentationChanged -= HandleGameEndPresentationChanged;
        _networkSession = null;
    }

    private void HandleRoundStartPresentationChanged(NetworkRoundStartStatePayload payload)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession || payload.SequenceId <= _lastRoundIntroSequenceId)
        {
            return;
        }

        _lastRoundIntroSequenceId = payload.SequenceId;

        if (_activeRoundIntroCoroutine != null)
        {
            StopCoroutine(_activeRoundIntroCoroutine);
        }

        _activeRoundIntroCoroutine = StartCoroutine(PlayNetworkRoundIntro(payload.RoundNumber));
    }

    private IEnumerator PlayNetworkRoundIntro(int roundNumber)
    {
        if (roundTextCanvasGroup != null)
        {
            roundTextCanvasGroup.alpha = 0f;
        }

        if (countdownCanvasGroup != null)
        {
            countdownCanvasGroup.alpha = 0f;
        }

        SetGameplayHudActive(true);
        yield return ShowRoundText(roundNumber);
        yield return ShowCountdown();
        _activeRoundIntroCoroutine = null;
    }

    private void HandleRoundEndPresentationChanged(NetworkRoundEndStatePayload payload)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession)
        {
            return;
        }

        if (roundEndScreenManager == null)
        {
            Debug.LogWarning("[SceneManager3D] Round-end presentation received, but RoundEndScreenManager is not wired.", this);
            return;
        }

        if (payload.IsVisible)
        {
            SetGameplayHudActive(false);
            roundEndScreenManager.ShowRoundEndScreen(
                payload.WinningPlayer,
                payload.RoundDuration,
                payload.Player1Damage,
                payload.Player2Damage,
                payload.Player1Accuracy,
                payload.Player2Accuracy);
            return;
        }

        roundEndScreenManager.HideRoundEndScreen();
    }

    private void HandleWinStateChanged(NetworkWinStatePayload payload)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession)
        {
            return;
        }

        _player1Wins = payload.Player1Wins;
        _player2Wins = payload.Player2Wins;
        UpdateWinTrackers();
    }

    private void HandleGameEndPresentationChanged(NetworkGameEndStatePayload payload)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession)
        {
            return;
        }

        if (gameEndScreenManager == null)
        {
            Debug.LogWarning("[SceneManager3D] Game-end presentation received, but GameEndScreenManager is not wired.", this);
            return;
        }

        if (!payload.IsVisible)
        {
            gameEndScreenManager.HideGameEndScreen();
            return;
        }

        SetGameplayHudActive(false);

        NetworkSessionData session = NetworkSessionData.Instance;
        int localSlot = session != null ? session.GetLocalSlotIndex() : 0;
        bool localIsPlayer1 = localSlot != 1;

        ShipData localShip = GameDataManager.Instance != null
            ? GameDataManager.Instance.GetShipById(localIsPlayer1 ? payload.Player1ShipId.ToString() : payload.Player2ShipId.ToString())
            : null;

        gameEndScreenManager.ShowGameEndScreen(
            payload.WinningPlayer,
            localIsPlayer1 ? 1 : 2,
            localShip,
            payload.GameDuration,
            localIsPlayer1 ? payload.Player1Wins : payload.Player2Wins,
            localIsPlayer1 ? payload.Player1Losses : payload.Player2Losses,
            localIsPlayer1 ? payload.Player1DamageDealt : payload.Player2DamageDealt,
            localIsPlayer1 ? payload.Player1DamageTaken : payload.Player2DamageTaken,
            localIsPlayer1 ? payload.Player1Accuracy : payload.Player2Accuracy);
    }

    private void UpdateWinTrackers()
    {
        player1WinTracker?.SetWins(_player1Wins);
        player2WinTracker?.SetWins(_player2Wins);
    }

    private void SetupDebugStartState()
    {
        if (debugStartAtRound < 1)
        {
            return;
        }

        _currentRound = debugStartAtRound - 1;
        switch (debugStartAtRound)
        {
            case 1:
                _player1Wins = 0;
                _player2Wins = 0;
                break;
            case 2:
                _player1Wins = 1;
                _player2Wins = 0;
                break;
            case 3:
                _player1Wins = 1;
                _player2Wins = 1;
                break;
            case 4:
                _player1Wins = 2;
                _player2Wins = 1;
                break;
            case 5:
                _player1Wins = 2;
                _player2Wins = 2;
                break;
        }

        UpdateWinTrackers();
        NetworkSessionData.Instance?.BroadcastWinStateServer(_player1Wins, _player2Wins);
    }

    private static CombatStatsSnapshot CopyStats(PlayerCombatStats3D source)
    {
        return source != null
            ? new CombatStatsSnapshot
            {
                shotsFired = source.shotsFired,
                shotsHit = source.shotsHit,
                damageDealt = source.damageDealt,
                damageTaken = source.damageTaken
            }
            : default;
    }

    private static void AddStats(ref CombatStatsSnapshot target, CombatStatsSnapshot source)
    {
        target.shotsFired += source.shotsFired;
        target.shotsHit += source.shotsHit;
        target.damageDealt += source.damageDealt;
        target.damageTaken += source.damageTaken;
    }

    private static float CalculateAccuracy(CombatStatsSnapshot stats)
    {
        if (stats.shotsFired <= 0)
        {
            return 0f;
        }

        return (float)stats.shotsHit / stats.shotsFired * 100f;
    }

    private struct CombatStatsSnapshot
    {
        public int shotsFired;
        public int shotsHit;
        public float damageDealt;
        public float damageTaken;
    }

    private struct RoundStatsSnapshot
    {
        public CombatStatsSnapshot Player1;
        public CombatStatsSnapshot Player2;
    }
}
