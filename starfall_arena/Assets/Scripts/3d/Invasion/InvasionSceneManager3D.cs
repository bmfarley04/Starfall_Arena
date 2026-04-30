using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionSceneManager3D : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Spawn point used for player slot 1.")]
    [SerializeField] private Transform player1SpawnPoint;
    [Tooltip("Spawn point used for player slot 2.")]
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Fallback Ships")]
    [Tooltip("Fallback 3D ship for player slot 1 when no network or GameData selection is available.")]
    [SerializeField] private ShipData defaultPlayer1Ship;
    [Tooltip("Fallback 3D ship for player slot 2 when no network or GameData selection is available.")]
    [SerializeField] private ShipData defaultPlayer2Ship;

    [Header("Invasion Systems")]
    [Tooltip("Wave manager that owns configured enemy waves, enemy spawning, and alive-enemy tracking.")]
    [SerializeField] private InvasionWaveManager3D waveManager;
    [Tooltip("Arena boundary to reset and start once when the Invasion session begins.")]
    [SerializeField] private ArenaBoundary3D arenaBoundary;

    [Header("Wave UI")]
    [Tooltip("Canvas group for the reused round text canvas. In Invasion this displays WAVE text only.")]
    [SerializeField] private CanvasGroup waveTextCanvasGroup;
    [Tooltip("Text field displayed as WAVE 1, WAVE 2, and so on.")]
    [SerializeField] private TextMeshProUGUI waveText;

    [Header("Enemy Counter UI")]
    [Tooltip("If enabled, this manager controls the optional enemy counter canvas from the wave manager's alive enemy count.")]
    [SerializeField] private bool useEnemyCounter = true;
    [Tooltip("Optional canvas/root containing the enemy counter icon and text.")]
    [SerializeField] private GameObject enemyCounterCanvas;
    [Tooltip("Text field that displays how many tracked enemies are currently alive.")]
    [SerializeField] private TextMeshProUGUI enemyCounterText;
    [Tooltip("Format used for the enemy counter text. {0} is replaced by the alive enemy count.")]
    [SerializeField] private string enemyCounterFormat = "{0}";
    [Tooltip("If enabled, the enemy counter canvas is hidden whenever the alive enemy count is zero.")]
    [SerializeField] private bool hideEnemyCounterWhenZero = false;

    [Header("Life Counter UI")]
    [Tooltip("If enabled, this manager controls the optional heart/life counter canvas. This is display-only until the Invasion lives/respawn rules are implemented.")]
    [SerializeField] private bool useLifeCounter = true;
    [Tooltip("Optional canvas group containing the heart/life counter UI.")]
    [SerializeField] private CanvasGroup lifeCounterCanvasGroup;
    [Tooltip("Text field that displays the current remaining player lives.")]
    [SerializeField] private TextMeshProUGUI lifeCounterText;
    [Tooltip("Initial life count shown when Invasion gameplay starts. Gameplay life-loss rules are planned separately.")]
    [Min(0)]
    [SerializeField] private int startingPlayerLives = 3;
    [Tooltip("Format used for the life counter text. {0} is replaced by the remaining life count.")]
    [SerializeField] private string lifeCounterFormat = "{0}";

    [Header("Gameplay HUD")]
    [Tooltip("HUD roots that should be active during Invasion gameplay: health, vignette, crosshair, weapon container, ability container, FPS/ping, and enemy tracker.")]
    [SerializeField] private GameObject[] gameplayHudRoots;
    [Tooltip("Canvases that should be forced onto the 3D UI camera/sorting setup in network play.")]
    [SerializeField] private Canvas[] uiCanvases;
    [Tooltip("Optional UI camera. If unset, the manager looks for a camera named UICamera, then falls back to Camera.main.")]
    [SerializeField] private Camera uiCamera;
    [Tooltip("Sorting order applied to the first configured UI canvas. Later canvases increment from this value.")]
    [SerializeField] private int baseCanvasSortingOrder = 200;

    [Header("Timing")]
    [Tooltip("Seconds between retries while waiting for network players or session data.")]
    [SerializeField] private float spawnRetryIntervalSeconds = 0.1f;
    [Tooltip("How long WAVE text remains fully visible before it fades out.")]
    [SerializeField] private float waveTextDisplayDuration = 1.5f;
    [Tooltip("Fade duration used when showing or hiding WAVE text.")]
    [SerializeField] private float textFadeDuration = 0.3f;

    private Player3D _player1;
    private Player3D _player2;
    private ShipData _player1Data;
    private ShipData _player2Data;

    private bool _useNetworkSession;
    private bool _isAuthoritativeController = true;
    private bool _wavesStarted;
    private bool _gameplayHudActive;
    private int _currentAliveEnemyCount;
    private int _currentPlayerLives;
    private NetworkSessionData _networkSession;
    private Coroutine _networkSessionSubscriptionCoroutine;
    private Coroutine _activeWaveIntroCoroutine;
    private int _lastWaveIntroSequenceId = -1;

    private IEnumerator Start()
    {
        yield return null;
        RefreshNetworkMode();

        if (waveManager == null)
        {
            waveManager = FindFirstObjectByType<InvasionWaveManager3D>();
        }

        ResolveShipData();
        ConfigureNetworkUiCanvases();
        SetInitialUiState();

        SubscribeWaveManagerEvents();
        SubscribeNetworkSessionEvents();
        _networkSessionSubscriptionCoroutine = StartCoroutine(EnsureNetworkSessionSubscription());

        if (_isAuthoritativeController)
        {
            StartCoroutine(GameLoop());
        }
    }

    private void OnDestroy()
    {
        UnsubscribeWaveManagerEvents();

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
        yield return SpawnPlayers();
        SetGameplayHudActive(true);
        StartArenaBoundary();

        if (_useNetworkSession && NetworkSessionData.Instance != null)
        {
            NetworkSessionData.Instance.MarkMatchStarted();
        }

        if (waveManager == null)
        {
            Debug.LogWarning("[InvasionSceneManager3D] Cannot start Invasion waves because no InvasionWaveManager3D is assigned.", this);
            yield break;
        }

        _wavesStarted = true;
        waveManager.StartWaves();
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
                Debug.LogWarning($"[InvasionSceneManager3D] Ship '{resolved.ShipId}' is not registered in the 3D roster. Falling back for player {slot}.", this);
            }

            resolved = fallback;
        }

        if (resolved != null && resolved.shipPrefab == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Ship '{resolved.ShipId}' has no 3D gameplay prefab assigned.", this);
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
            yield break;
        }

        ulong player1OwnerId = ResolveOwnerClientIdForSlot(0);
        ulong player2OwnerId = ResolveOwnerClientIdForSlot(1);

        _player1 = SpawnNetworkPlayer(_player1Data, player1SpawnPoint, player1OwnerId, 1);
        _player2 = SpawnNetworkPlayer(_player2Data, player2SpawnPoint, player2OwnerId, 2);

        yield return null;
        PlayerHUDManager3D.RebindAllAutoManagers();
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
            Debug.LogError($"[InvasionSceneManager3D] Cannot spawn {playerTag}: missing ShipData or ship prefab.", this);
            return null;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        GameObject instance = Instantiate(shipData.shipPrefab, position, rotation);
        instance.name = $"InvasionPlayer3D_{playerTag}_{shipData.name}";
        instance.tag = playerTag;

        Player3D player = instance.GetComponent<Player3D>();
        if (player == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Spawned {playerTag} prefab '{shipData.name}' is missing Player3D.", instance);
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
            Debug.LogError($"[InvasionSceneManager3D] Cannot network-spawn player {playerSlot}: missing ShipData or ship prefab.", this);
            return null;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        GameObject instance = NetMgr.SpawnPlayerNetworked(shipData.shipPrefab, position, rotation, ownerClientId);
        if (instance == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] NetMgr failed to spawn player {playerSlot} with ship '{shipData.ShipId}'.", this);
            return null;
        }

        instance.name = $"NetworkInvasionPlayer3D_{playerSlot}_{shipData.name}";
        instance.tag = playerSlot == 1 ? "Player1" : "Player2";

        Player3D player = instance.GetComponent<Player3D>();
        if (player == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Spawned network ship '{shipData.ShipId}' is missing Player3D.", instance);
            return null;
        }

        NetMovement3D movement = instance.GetComponent<NetMovement3D>();
        if (movement == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Spawned network ship '{shipData.ShipId}' is missing NetMovement3D.", instance);
        }
        else
        {
            movement.SetNetworkPlayerIndex(playerSlot);
        }

        if (instance.GetComponent<NetCombat3D>() == null)
        {
            Debug.LogWarning($"[InvasionSceneManager3D] Spawned network ship '{shipData.ShipId}' is missing NetCombat3D; combat replication will not work for this player.", instance);
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

    private void StartArenaBoundary()
    {
        if (arenaBoundary == null)
        {
            return;
        }

        arenaBoundary.ResetBoundary();
        arenaBoundary.StartBoundary();
    }

    private void SetInitialUiState()
    {
        if (waveTextCanvasGroup != null)
        {
            waveTextCanvasGroup.alpha = 0f;
        }

        _currentPlayerLives = Mathf.Max(0, startingPlayerLives);
        UpdateEnemyCounter(0);
        UpdateLifeCounter(_currentPlayerLives);
        SetGameplayHudActive(false);
    }

    private void SetGameplayHudActive(bool active)
    {
        _gameplayHudActive = active;

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

        UpdateEnemyCounter(_currentAliveEnemyCount);
        UpdateLifeCounter(_currentPlayerLives);
    }

    public void SetPlayerLivesRemaining(int livesRemaining)
    {
        UpdateLifeCounter(livesRemaining);
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

    private IEnumerator PlayWaveIntroAuthoritative(int waveNumber)
    {
        if (_useNetworkSession && NetworkSessionData.Instance != null)
        {
            NetworkSessionData.Instance.BroadcastWaveStartServer(waveNumber);
            SetGameplayHudActive(true);
            yield return ShowWaveText(waveNumber);
            NetworkSessionData.Instance.MarkMatchStarted();
            yield break;
        }

        SetGameplayHudActive(true);
        yield return ShowWaveText(waveNumber);
    }

    private IEnumerator ShowWaveText(int waveNumber)
    {
        if (waveText == null || waveTextCanvasGroup == null)
        {
            yield break;
        }

        waveText.text = $"WAVE {waveNumber}";
        yield return FadeCanvasGroup(waveTextCanvasGroup, 0f, 1f, textFadeDuration);
        yield return new WaitForSecondsRealtime(waveTextDisplayDuration);
        yield return FadeCanvasGroup(waveTextCanvasGroup, 1f, 0f, textFadeDuration);
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

    private float GetWaveIntroDuration()
    {
        return (textFadeDuration * 2f) + waveTextDisplayDuration;
    }

    private void SubscribeWaveManagerEvents()
    {
        if (waveManager == null)
        {
            return;
        }

        waveManager.WaveIntroRequested -= PlayWaveIntroAuthoritative;
        waveManager.WaveIntroRequested += PlayWaveIntroAuthoritative;
        waveManager.AllWavesCleared -= HandleAllWavesCleared;
        waveManager.AllWavesCleared += HandleAllWavesCleared;
        waveManager.AliveEnemyCountChanged -= HandleAliveEnemyCountChanged;
        waveManager.AliveEnemyCountChanged += HandleAliveEnemyCountChanged;
        SyncAliveEnemyCountFromWaveManager();
    }

    private void UnsubscribeWaveManagerEvents()
    {
        if (waveManager == null)
        {
            return;
        }

        waveManager.WaveIntroRequested -= PlayWaveIntroAuthoritative;
        waveManager.AllWavesCleared -= HandleAllWavesCleared;
        waveManager.AliveEnemyCountChanged -= HandleAliveEnemyCountChanged;
    }

    private void HandleAllWavesCleared()
    {
        if (!_wavesStarted)
        {
            return;
        }

        Debug.Log("[InvasionSceneManager3D] All configured Invasion waves are cleared. Game-end flow is planned later, so gameplay remains active.", this);
    }

    private void HandleAliveEnemyCountChanged(int aliveEnemyCount)
    {
        if (_useNetworkSession && NetworkSessionData.Instance != null)
        {
            NetworkSessionData.Instance.BroadcastInvasionEnemyCountServer(aliveEnemyCount);
            return;
        }

        UpdateEnemyCounter(aliveEnemyCount);
    }

    private void SyncAliveEnemyCountFromWaveManager()
    {
        if (waveManager == null)
        {
            return;
        }

        // If the wave manager already spawned enemies before this scene manager
        // finished subscribing, the first AliveEnemyCountChanged events were missed.
        // Pull the current tracked total now so initial wave spawns, single enemies,
        // and already-active bosses immediately correct the HUD/session count.
        HandleAliveEnemyCountChanged(waveManager.AliveEnemyCount);
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
        session.OnSessionStateChanged += HandleNetworkSessionStateChanged;
        session.OnWaveStartPresentationChanged += HandleWaveStartPresentationChanged;
        session.OnInvasionEnemyCountChanged += HandleInvasionEnemyCountChanged;
        HandleNetworkSessionStateChanged(session.CurrentState);

        // Late-joining scene managers can miss the first wave-start presentation
        // RPC if the host already broadcast it while this scene was still loading.
        // Recover the current intro while the session is still in RoundTransition.
        if (session.CurrentState == NetworkMatchState.RoundTransition
            && session.TryGetLastWaveStartPresentation(out NetworkWaveStartStatePayload wavePayload))
        {
            HandleWaveStartPresentationChanged(wavePayload);
        }
    }

    private void UnsubscribeNetworkSessionEvents()
    {
        NetworkSessionData session = _networkSession;
        if (session == null)
        {
            return;
        }

        session.OnSessionStateChanged -= HandleNetworkSessionStateChanged;
        session.OnWaveStartPresentationChanged -= HandleWaveStartPresentationChanged;
        session.OnInvasionEnemyCountChanged -= HandleInvasionEnemyCountChanged;
        _networkSession = null;
    }

    private void HandleNetworkSessionStateChanged(NetworkMatchState state)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession)
        {
            return;
        }

        // Invasion clients previously relied on the one-shot wave-start presentation
        // event to enable gameplay HUD. If that event fired before the scene manager
        // subscribed, the client could stay stuck with every gameplay HUD root hidden.
        // Recover from the replicated session state as well so late subscribers still
        // show HUD once the match has entered its live gameplay phases.
        if (state == NetworkMatchState.RoundTransition || state == NetworkMatchState.InMatch)
        {
            SetGameplayHudActive(true);
        }
    }

    private void HandleWaveStartPresentationChanged(NetworkWaveStartStatePayload payload)
    {
        RefreshNetworkMode();
        if (!_useNetworkSession || payload.SequenceId <= _lastWaveIntroSequenceId)
        {
            return;
        }

        // The host now plays the wave intro directly in PlayWaveIntroAuthoritative().
        // Ignore the echoed local presentation event here so the host does not run the
        // same intro a second time through the replicated client-facing path.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            _lastWaveIntroSequenceId = payload.SequenceId;
            return;
        }

        _lastWaveIntroSequenceId = payload.SequenceId;

        if (_activeWaveIntroCoroutine != null)
        {
            StopCoroutine(_activeWaveIntroCoroutine);
        }

        _activeWaveIntroCoroutine = StartCoroutine(PlayNetworkWaveIntro(payload.WaveNumber));
    }

    private IEnumerator PlayNetworkWaveIntro(int waveNumber)
    {
        if (waveTextCanvasGroup != null)
        {
            waveTextCanvasGroup.alpha = 0f;
        }

        SetGameplayHudActive(true);
        yield return ShowWaveText(waveNumber);
        _activeWaveIntroCoroutine = null;
    }

    private void HandleInvasionEnemyCountChanged(NetworkInvasionEnemyCountStatePayload payload)
    {
        UpdateEnemyCounter(payload.AliveEnemyCount);
    }

    private void UpdateEnemyCounter(int aliveEnemyCount)
    {
        _currentAliveEnemyCount = Mathf.Max(0, aliveEnemyCount);

        if (!useEnemyCounter)
        {
            if (enemyCounterCanvas != null)
            {
                enemyCounterCanvas.SetActive(false);
            }

            return;
        }

        if (enemyCounterCanvas != null)
        {
            bool shouldShowCounter = _gameplayHudActive && (!hideEnemyCounterWhenZero || _currentAliveEnemyCount > 0);
            enemyCounterCanvas.SetActive(shouldShowCounter);
        }

        if (enemyCounterText != null)
        {
            enemyCounterText.text = FormatEnemyCounterText(_currentAliveEnemyCount);
        }
    }

    private string FormatEnemyCounterText(int aliveEnemyCount)
    {
        string format = string.IsNullOrWhiteSpace(enemyCounterFormat) ? "{0}" : enemyCounterFormat;
        try
        {
            return string.Format(format, aliveEnemyCount);
        }
        catch (System.FormatException)
        {
            return aliveEnemyCount.ToString();
        }
    }

    private void UpdateLifeCounter(int livesRemaining)
    {
        _currentPlayerLives = Mathf.Max(0, livesRemaining);

        if (!useLifeCounter)
        {
            if (lifeCounterCanvasGroup != null)
            {
                lifeCounterCanvasGroup.alpha = 0f;
                lifeCounterCanvasGroup.interactable = false;
                lifeCounterCanvasGroup.blocksRaycasts = false;
            }

            return;
        }

        if (lifeCounterCanvasGroup != null)
        {
            lifeCounterCanvasGroup.alpha = _gameplayHudActive ? 1f : 0f;
            lifeCounterCanvasGroup.interactable = false;
            lifeCounterCanvasGroup.blocksRaycasts = false;
        }

        if (lifeCounterText != null)
        {
            lifeCounterText.text = FormatLifeCounterText(_currentPlayerLives);
        }
    }

    private string FormatLifeCounterText(int livesRemaining)
    {
        string format = string.IsNullOrWhiteSpace(lifeCounterFormat) ? "{0}" : lifeCounterFormat;
        try
        {
            return string.Format(format, livesRemaining);
        }
        catch (System.FormatException)
        {
            return livesRemaining.ToString();
        }
    }
}
