using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionSceneManager3D : MonoBehaviour
{
    private const string LivesMessageName = "StarfallArena.Invasion3D.PlayerLives";
    private const string RespawnProtectionMessageName = "StarfallArena.Invasion3D.RespawnProtection";
    private const string RewardOffersMessageName = "StarfallArena.Invasion3D.RewardOffers";
    private const string RewardChoiceMessageName = "StarfallArena.Invasion3D.RewardChoice";
    private const string RewardAppliedMessageName = "StarfallArena.Invasion3D.RewardApplied";

    [System.Serializable]
    private struct RespawnConfig3D
    {
        [Tooltip("Seconds after a death before a player with remaining lives is spawned again at the death location.")]
        public float respawnDelaySeconds;
        [Tooltip("Seconds of damage immunity granted immediately after an Invasion respawn.")]
        public float invulnerabilitySeconds;
        [Tooltip("Seconds between shield visibility pulses during respawn invulnerability. Lower values keep the shield at a higher alpha.")]
        public float shieldFlashIntervalSeconds;
    }

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
    [Tooltip("3D-only adapter that reuses the old augment-card visuals for the between-wave reward draft.")]
    [SerializeField] private InvasionRewardPhasePresenter3D rewardPhasePresenter;

    [Header("Between-Wave Rewards")]
    [Tooltip("If enabled, cleared waves trigger a between-wave reward intermission before the next wave begins.")]
    [SerializeField] private bool useBetweenWaveRewards = true;
    [Tooltip("Reward definition assets used for the Invasion stat draft. If left empty, the manager auto-loads all InvasionStatRewardDefinition3D assets found under Resources/3D/InvasionRewards.")]
    [SerializeField] private InvasionStatRewardDefinition3D[] rewardDefinitions = new InvasionStatRewardDefinition3D[0];
    [Tooltip("How many reward cards each player sees after a cleared wave.")]
    [Min(1)]
    [SerializeField] private int rewardsPerOffer = 3;
    [Tooltip("Seconds to wait after a wave is fully cleared before the reward cards are shown. This happens before players are locked into the reward intermission.")]
    [Min(0f)]
    [SerializeField] private float rewardPresentationDelaySeconds = 0.75f;
    [Tooltip("Seconds to wait after the reward intermission finishes before handing control back to the next-wave intro sequence. This is in addition to any next-wave delay authored on InvasionWaveManager3D.")]
    [Min(0f)]
    [SerializeField] private float rewardPostPresentationDelaySeconds = 0.35f;

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
    [Tooltip("If enabled, this manager controls the optional heart/life counter canvas for the local player's remaining lives.")]
    [SerializeField] private bool useLifeCounter = true;
    [Tooltip("Optional canvas group containing the heart/life counter UI.")]
    [SerializeField] private CanvasGroup lifeCounterCanvasGroup;
    [Tooltip("Text field that displays the current remaining player lives.")]
    [SerializeField] private TextMeshProUGUI lifeCounterText;
    [Tooltip("Lives assigned to each player when Invasion gameplay starts. A life is consumed on each death; players respawn only while this remains above zero after the death.")]
    [Min(0)]
    [SerializeField] private int startingPlayerLives = 3;
    [Tooltip("Format used for the life counter text. {0} is the displayed/local life count, {1} is player 1 lives, and {2} is player 2 lives.")]
    [SerializeField] private string lifeCounterFormat = "{0}";

    [Header("Respawn Rules")]
    [SerializeField] private RespawnConfig3D respawn = new RespawnConfig3D
    {
        respawnDelaySeconds = 1.5f,
        invulnerabilitySeconds = 3f,
        shieldFlashIntervalSeconds = 0.12f
    };

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
    private readonly int[] _playerLivesRemainingBySlot = new int[3];
    private readonly Coroutine[] _respawnCoroutinesBySlot = new Coroutine[3];
    private readonly Coroutine[] _respawnProtectionVisualCoroutinesBySlot = new Coroutine[3];
    private readonly InvasionPlayerRewardState3D[] _rewardStateBySlot = new InvasionPlayerRewardState3D[3];
    private readonly int[][] _pendingRewardOfferIndicesBySlot = new int[3][];
    private readonly bool[] _rewardChoiceReceivedBySlot = new bool[3];
    private readonly List<InvasionStatRewardDefinition3D> _effectiveRewardDefinitions = new List<InvasionStatRewardDefinition3D>(16);
    private int _rewardPhaseSequenceId;
    private InvasionRewardTier3D _activeRewardPhaseTier = InvasionRewardTier3D.Common;
    private bool _rewardPhaseActive;
    private bool _customNetworkMessagesRegistered;

    private IEnumerator Start()
    {
        yield return null;
        RefreshNetworkMode();
        EnsureRewardStateContainers();
        BuildEffectiveRewardDefinitionList();

        if (waveManager == null)
        {
            waveManager = FindFirstObjectByType<InvasionWaveManager3D>();
        }

        if (rewardPhasePresenter == null)
        {
            rewardPhasePresenter = FindFirstObjectByType<InvasionRewardPhasePresenter3D>(FindObjectsInactive.Include);
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
        UnsubscribePlayerDeath(_player1);
        UnsubscribePlayerDeath(_player2);
        StopRespawnCoroutines();

        if (_networkSessionSubscriptionCoroutine != null)
        {
            StopCoroutine(_networkSessionSubscriptionCoroutine);
            _networkSessionSubscriptionCoroutine = null;
        }

        UnsubscribeNetworkSessionEvents();
        UnregisterCustomNetworkMessages();
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
            BroadcastPlayerLives();
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
        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        return SpawnLocalPlayer(shipData, position, rotation, playerTag);
    }

    private Player3D SpawnLocalPlayer(ShipData shipData, Vector3 position, Quaternion rotation, string playerTag)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Cannot spawn {playerTag}: missing ShipData or ship prefab.", this);
            return null;
        }

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
        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        return SpawnNetworkPlayer(shipData, position, rotation, ownerClientId, playerSlot);
    }

    private Player3D SpawnNetworkPlayer(ShipData shipData, Vector3 position, Quaternion rotation, ulong ownerClientId, byte playerSlot)
    {
        if (shipData == null || shipData.shipPrefab == null)
        {
            Debug.LogError($"[InvasionSceneManager3D] Cannot network-spawn player {playerSlot}: missing ShipData or ship prefab.", this);
            return null;
        }

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
        SubscribePlayerDeath(player);
        EnsureRewardStateContainers();
        _rewardStateBySlot[playerSlot].CaptureBaseSnapshot(player);
        _rewardStateBySlot[playerSlot].ApplyToPlayer(player);
        if (_rewardPhaseActive)
        {
            SetPlayerIntermissionLocked(player, true);
        }

        if (playerSlot == 1)
        {
            _player1 = player;
        }
        else if (playerSlot == 2)
        {
            _player2 = player;
        }
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
        if (!_isAuthoritativeController || deadEntity == null)
        {
            return;
        }

        Player3D deadPlayer = deadEntity as Player3D;
        if (deadPlayer == null)
        {
            return;
        }

        byte playerSlot = ResolvePlayerSlot(deadPlayer);
        if (playerSlot < 1 || playerSlot > 2)
        {
            Debug.LogWarning($"[InvasionSceneManager3D] Ignoring death for '{deadPlayer.name}' because its player slot could not be resolved.", deadPlayer);
            return;
        }

        Vector3 deathPosition = deadPlayer.transform.position;
        Quaternion deathRotation = deadPlayer.transform.rotation;
        UnsubscribePlayerDeath(deadPlayer);
        SetTrackedPlayer(playerSlot, null);

        int livesAfterDeath = Mathf.Max(0, _playerLivesRemainingBySlot[playerSlot] - 1);
        _playerLivesRemainingBySlot[playerSlot] = livesAfterDeath;
        UpdateLifeCounter(ResolveDisplayedLives());
        BroadcastPlayerLives();

        if (livesAfterDeath <= 0)
        {
            Debug.Log($"[InvasionSceneManager3D] Player {playerSlot} died with no lives remaining and will not respawn.", this);
            return;
        }

        if (_respawnCoroutinesBySlot[playerSlot] != null)
        {
            StopCoroutine(_respawnCoroutinesBySlot[playerSlot]);
        }

        _respawnCoroutinesBySlot[playerSlot] = StartCoroutine(RespawnPlayerAfterDelay(playerSlot, deathPosition, deathRotation));
    }

    private IEnumerator RespawnPlayerAfterDelay(byte playerSlot, Vector3 deathPosition, Quaternion deathRotation)
    {
        float delay = Mathf.Max(0f, respawn.respawnDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        Player3D respawnedPlayer;
        if (_useNetworkSession)
        {
            ulong ownerClientId = ResolveOwnerClientIdForSlot(playerSlot - 1);
            ShipData shipData = playerSlot == 1 ? _player1Data : _player2Data;
            respawnedPlayer = SpawnNetworkPlayer(shipData, deathPosition, deathRotation, ownerClientId, playerSlot);
        }
        else
        {
            ShipData shipData = playerSlot == 1 ? _player1Data : _player2Data;
            string playerTag = playerSlot == 1 ? "Player1" : "Player2";
            respawnedPlayer = SpawnLocalPlayer(shipData, deathPosition, deathRotation, playerTag);
        }

        _respawnCoroutinesBySlot[playerSlot] = null;

        if (respawnedPlayer == null)
        {
            yield break;
        }

        ApplyRespawnProtection(respawnedPlayer, playerSlot);
        BroadcastRespawnProtection(playerSlot, Mathf.Max(0f, respawn.invulnerabilitySeconds));
        PlayerHUDManager3D.RebindAllAutoManagers();
    }

    private void ApplyRespawnProtection(Player3D player, byte playerSlot)
    {
        if (player == null)
        {
            return;
        }

        float duration = Mathf.Max(0f, respawn.invulnerabilitySeconds);
        if (duration > 0f)
        {
            player.BeginDodgeInvulnerability(duration);
        }

        StartRespawnProtectionVisual(playerSlot, duration);
    }

    private void StartRespawnProtectionVisual(byte playerSlot, float duration)
    {
        if (playerSlot < 1 || playerSlot > 2)
        {
            return;
        }

        if (_respawnProtectionVisualCoroutinesBySlot[playerSlot] != null)
        {
            StopCoroutine(_respawnProtectionVisualCoroutinesBySlot[playerSlot]);
        }

        _respawnProtectionVisualCoroutinesBySlot[playerSlot] = StartCoroutine(RespawnProtectionVisualCoroutine(playerSlot, duration));
    }

    private IEnumerator RespawnProtectionVisualCoroutine(byte playerSlot, float duration)
    {
        Player3D player = null;
        float waitForSpawnUntil = Time.realtimeSinceStartup + Mathf.Max(1f, spawnRetryIntervalSeconds * 10f);
        while (player == null && Time.realtimeSinceStartup < waitForSpawnUntil)
        {
            player = ResolveTrackedOrNetworkPlayer(playerSlot);
            if (player == null)
            {
                yield return null;
            }
        }

        if (player == null)
        {
            _respawnProtectionVisualCoroutinesBySlot[playerSlot] = null;
            yield break;
        }

        ShieldController shield = player.GetComponentInChildren<ShieldController>(true);
        float endTime = Time.time + Mathf.Max(0f, duration);
        float interval = Mathf.Max(0.03f, respawn.shieldFlashIntervalSeconds);

        while (shield != null && Time.time < endTime)
        {
            shield.OnHit(player.transform.position);
            yield return new WaitForSeconds(interval);
        }

        _respawnProtectionVisualCoroutinesBySlot[playerSlot] = null;
    }

    private Player3D ResolveTrackedOrNetworkPlayer(byte playerSlot)
    {
        Player3D trackedPlayer = playerSlot == 1 ? _player1 : _player2;
        if (trackedPlayer != null)
        {
            return trackedPlayer;
        }

        if (NetMovement3D.TryGetPlayerBySlot(playerSlot, out NetMovement3D movement))
        {
            return movement != null ? movement.GetComponent<Player3D>() : null;
        }

        return null;
    }

    private byte ResolvePlayerSlot(Player3D player)
    {
        if (player == null)
        {
            return 0;
        }

        NetMovement3D movement = player.GetComponent<NetMovement3D>();
        if (movement != null && movement.PlayerSlot > 0)
        {
            return movement.PlayerSlot;
        }

        if (player.CompareTag("Player1"))
        {
            return 1;
        }

        if (player.CompareTag("Player2"))
        {
            return 2;
        }

        return ReferenceEquals(player, _player1) ? (byte)1 : ReferenceEquals(player, _player2) ? (byte)2 : (byte)0;
    }

    private void SetTrackedPlayer(byte playerSlot, Player3D player)
    {
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

    private void EnsureRewardStateContainers()
    {
        for (int i = 1; i <= 2; i++)
        {
            _rewardStateBySlot[i] ??= new InvasionPlayerRewardState3D();
            _pendingRewardOfferIndicesBySlot[i] ??= new int[Mathf.Max(1, rewardsPerOffer)];
            for (int offerIndex = 0; offerIndex < _pendingRewardOfferIndicesBySlot[i].Length; offerIndex++)
            {
                _pendingRewardOfferIndicesBySlot[i][offerIndex] = -1;
            }
        }
    }

    private void BuildEffectiveRewardDefinitionList()
    {
        _effectiveRewardDefinitions.Clear();

        if (rewardDefinitions != null)
        {
            for (int i = 0; i < rewardDefinitions.Length; i++)
            {
                if (rewardDefinitions[i] != null)
                {
                    _effectiveRewardDefinitions.Add(rewardDefinitions[i]);
                }
            }
        }

        if (_effectiveRewardDefinitions.Count == 0)
        {
            InvasionStatRewardDefinition3D[] loadedRewards = Resources.LoadAll<InvasionStatRewardDefinition3D>("3D/InvasionRewards");
            for (int i = 0; i < loadedRewards.Length; i++)
            {
                if (loadedRewards[i] != null)
                {
                    _effectiveRewardDefinitions.Add(loadedRewards[i]);
                }
            }
        }

        if (_effectiveRewardDefinitions.Count == 0)
        {
            Debug.LogWarning("[InvasionSceneManager3D] No InvasionStatRewardDefinition3D assets were assigned and none were found in Resources/3D/InvasionRewards. Between-wave rewards will be disabled until a reward pool exists.", this);
        }
    }

    private void SetInitialUiState()
    {
        if (waveTextCanvasGroup != null)
        {
            waveTextCanvasGroup.alpha = 0f;
        }

        int clampedStartingLives = Mathf.Max(0, startingPlayerLives);
        _playerLivesRemainingBySlot[1] = clampedStartingLives;
        _playerLivesRemainingBySlot[2] = clampedStartingLives;
        _currentPlayerLives = ResolveDisplayedLives();
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
        int clampedLives = Mathf.Max(0, livesRemaining);
        _playerLivesRemainingBySlot[1] = clampedLives;
        _playerLivesRemainingBySlot[2] = clampedLives;
        UpdateLifeCounter(ResolveDisplayedLives());
        BroadcastPlayerLives();
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

    private IEnumerator PlayRewardPhaseAuthoritative(int clearedWaveNumber)
    {
        if (!useBetweenWaveRewards || _effectiveRewardDefinitions.Count == 0)
        {
            yield break;
        }

        if (rewardPresentationDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(rewardPresentationDelaySeconds);
        }

        EnsureRewardStateContainers();
        _rewardPhaseActive = true;
        _rewardPhaseSequenceId++;
        _activeRewardPhaseTier = ResolveRewardTierForWave(clearedWaveNumber);
        ResetRewardChoiceState();
        SetPlayersIntermissionLocked(true);

        BuildOfferIndicesForPlayer(1, _activeRewardPhaseTier);
        BuildOfferIndicesForPlayer(2, _activeRewardPhaseTier);

        if (_useNetworkSession)
        {
            BroadcastRewardOffers();

            int localSlot = ResolveLocalPlayerSlot();
            if (localSlot == 1 || localSlot == 2)
            {
                StartLocalRewardSelection((byte)localSlot);
            }

            yield return WaitForNetworkRewardChoices();
        }
        else
        {
            yield return PresentLocalRewardSelectionSequential(1);
            yield return PresentLocalRewardSelectionSequential(2);
        }

        _rewardPhaseActive = false;
        SetPlayersIntermissionLocked(false);

        if (rewardPostPresentationDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(rewardPostPresentationDelaySeconds);
        }
    }

    private IEnumerator PresentLocalRewardSelectionSequential(byte playerSlot)
    {
        bool selectionResolved = false;
        StartLocalRewardSelection(playerSlot, () => selectionResolved = true);
        yield return new WaitUntil(() => selectionResolved);
    }

    private void StartLocalRewardSelection(byte playerSlot, System.Action onSelectionResolved = null)
    {
        List<InvasionStatRewardDefinition3D> offers = ResolveRewardOffersForSlot(playerSlot);
        if (offers.Count == 0)
        {
            HandleLocalRewardChoice(playerSlot, 0);
            onSelectionResolved?.Invoke();
            return;
        }

        if (rewardPhasePresenter == null)
        {
            rewardPhasePresenter = FindFirstObjectByType<InvasionRewardPhasePresenter3D>(FindObjectsInactive.Include);
        }

        if (rewardPhasePresenter == null)
        {
            Debug.LogWarning("[InvasionSceneManager3D] Missing InvasionRewardPhasePresenter3D, auto-selecting the first reward.", this);
            HandleLocalRewardChoice(playerSlot, 0);
            onSelectionResolved?.Invoke();
            return;
        }

        rewardPhasePresenter.ShowOffers(playerSlot, _activeRewardPhaseTier, offers, choiceIndex =>
        {
            HandleLocalRewardChoice(playerSlot, choiceIndex);
            onSelectionResolved?.Invoke();
        });
    }

    private void HandleLocalRewardChoice(byte playerSlot, int offerChoiceIndex)
    {
        int rewardDefinitionIndex = ResolveRewardDefinitionIndexForOffer(playerSlot, offerChoiceIndex);
        if (_useNetworkSession && !_isAuthoritativeController)
        {
            SendRewardChoiceToServer(playerSlot, offerChoiceIndex);
            return;
        }

        ApplyRewardChoiceAuthoritative(playerSlot, rewardDefinitionIndex);
    }

    private void BuildOfferIndicesForPlayer(byte playerSlot, InvasionRewardTier3D rewardTier)
    {
        EnsureRewardStateContainers();
        int[] offers = _pendingRewardOfferIndicesBySlot[playerSlot];
        for (int i = 0; i < offers.Length; i++)
        {
            offers[i] = -1;
        }

        List<int> eligibleRewardIndices = new List<int>(_effectiveRewardDefinitions.Count);
        List<float> weights = new List<float>(_effectiveRewardDefinitions.Count);
        Player3D livePlayer = ResolveTrackedOrNetworkPlayer(playerSlot);
        InvasionPlayerRewardState3D rewardState = _rewardStateBySlot[playerSlot];
        if (livePlayer != null)
        {
            rewardState.CaptureBaseSnapshot(livePlayer);
        }

        for (int rewardIndex = 0; rewardIndex < _effectiveRewardDefinitions.Count; rewardIndex++)
        {
            InvasionStatRewardDefinition3D reward = _effectiveRewardDefinitions[rewardIndex];
            if (reward == null || !rewardState.CanOfferReward(reward, rewardTier, livePlayer))
            {
                continue;
            }

            float weight = Mathf.Max(0f, reward.OfferWeight);
            if (weight <= 0f)
            {
                continue;
            }

            eligibleRewardIndices.Add(rewardIndex);
            weights.Add(weight);
        }

        int offerCount = Mathf.Min(Mathf.Max(1, rewardsPerOffer), offers.Length);
        for (int offerIndex = 0; offerIndex < offerCount && eligibleRewardIndices.Count > 0; offerIndex++)
        {
            int chosenPoolIndex = PickWeightedRewardPoolIndex(weights);
            offers[offerIndex] = eligibleRewardIndices[chosenPoolIndex];
            eligibleRewardIndices.RemoveAt(chosenPoolIndex);
            weights.RemoveAt(chosenPoolIndex);
        }
    }

    private static int PickWeightedRewardPoolIndex(List<float> weights)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            totalWeight += Mathf.Max(0f, weights[i]);
        }

        if (totalWeight <= 0f)
        {
            return 0;
        }

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative)
            {
                return i;
            }
        }

        return Mathf.Max(0, weights.Count - 1);
    }

    private List<InvasionStatRewardDefinition3D> ResolveRewardOffersForSlot(byte playerSlot)
    {
        List<InvasionStatRewardDefinition3D> offers = new List<InvasionStatRewardDefinition3D>(Mathf.Max(1, rewardsPerOffer));
        int[] indices = _pendingRewardOfferIndicesBySlot[playerSlot];
        for (int i = 0; i < indices.Length; i++)
        {
            InvasionStatRewardDefinition3D reward = ResolveRewardDefinition(indices[i]);
            if (reward != null)
            {
                offers.Add(reward);
            }
        }

        return offers;
    }

    private int ResolveRewardDefinitionIndexForOffer(byte playerSlot, int offerChoiceIndex)
    {
        int[] offers = _pendingRewardOfferIndicesBySlot[playerSlot];
        if (offers == null || offers.Length == 0)
        {
            return -1;
        }

        int clampedIndex = Mathf.Clamp(offerChoiceIndex, 0, offers.Length - 1);
        int rewardDefinitionIndex = offers[clampedIndex];
        if (rewardDefinitionIndex >= 0)
        {
            return rewardDefinitionIndex;
        }

        for (int i = 0; i < offers.Length; i++)
        {
            if (offers[i] >= 0)
            {
                return offers[i];
            }
        }

        return -1;
    }

    private InvasionStatRewardDefinition3D ResolveRewardDefinition(int rewardIndex)
    {
        return rewardIndex >= 0 && rewardIndex < _effectiveRewardDefinitions.Count
            ? _effectiveRewardDefinitions[rewardIndex]
            : null;
    }

    private void ResetRewardChoiceState()
    {
        for (int i = 1; i <= 2; i++)
        {
            _rewardChoiceReceivedBySlot[i] = false;
        }
    }

    private void ApplyRewardChoiceAuthoritative(byte playerSlot, int rewardDefinitionIndex)
    {
        if (playerSlot < 1 || playerSlot > 2 || _rewardChoiceReceivedBySlot[playerSlot])
        {
            return;
        }

        InvasionStatRewardDefinition3D reward = ResolveRewardDefinition(rewardDefinitionIndex);
        if (reward == null)
        {
            Debug.LogWarning($"[InvasionSceneManager3D] Reward choice for player {playerSlot} resolved to an invalid definition index {rewardDefinitionIndex}.", this);
            _rewardChoiceReceivedBySlot[playerSlot] = true;
            return;
        }

        InvasionPlayerRewardState3D rewardState = _rewardStateBySlot[playerSlot];
        Player3D livePlayer = ResolveTrackedOrNetworkPlayer(playerSlot);
        rewardState.ApplyRewardDefinition(reward, _activeRewardPhaseTier, livePlayer);
        rewardState.ApplyToPlayer(livePlayer, reward, _activeRewardPhaseTier);

        if (reward.GrantsExtraLife(_activeRewardPhaseTier))
        {
            _playerLivesRemainingBySlot[playerSlot] = Mathf.Max(0, _playerLivesRemainingBySlot[playerSlot]) + 1;
            UpdateLifeCounter(ResolveDisplayedLives());
            BroadcastPlayerLives();
        }

        _rewardChoiceReceivedBySlot[playerSlot] = true;

        if (_useNetworkSession)
        {
            BroadcastRewardApplied(playerSlot, rewardDefinitionIndex, _activeRewardPhaseTier);
        }
    }

    private void ApplyRewardChoiceReplica(byte playerSlot, int rewardDefinitionIndex, InvasionRewardTier3D rewardTier)
    {
        if (playerSlot < 1 || playerSlot > 2)
        {
            return;
        }

        InvasionStatRewardDefinition3D reward = ResolveRewardDefinition(rewardDefinitionIndex);
        if (reward == null)
        {
            return;
        }

        InvasionPlayerRewardState3D rewardState = _rewardStateBySlot[playerSlot];
        Player3D livePlayer = ResolveTrackedOrNetworkPlayer(playerSlot);
        rewardState.ApplyRewardDefinition(reward, rewardTier, livePlayer);
        rewardState.ApplyToPlayer(livePlayer, reward, rewardTier);

        if (reward.GrantsExtraLife(rewardTier))
        {
            _playerLivesRemainingBySlot[playerSlot] = Mathf.Max(0, _playerLivesRemainingBySlot[playerSlot]) + 1;
            UpdateLifeCounter(ResolveDisplayedLives());
        }
    }

    private void SetPlayersIntermissionLocked(bool isLocked)
    {
        SetPlayerIntermissionLocked(_player1, isLocked);
        SetPlayerIntermissionLocked(_player2, isLocked);
    }

    private void SetPlayerIntermissionLocked(Player3D player, bool isLocked)
    {
        if (player == null)
        {
            return;
        }

        NetMovement3D movement = player.GetComponent<NetMovement3D>();
        if (_useNetworkSession && movement != null && _isAuthoritativeController)
        {
            movement.SetMovementLockedAuthoritative(isLocked);
        }

        if (player.PlayerInput3D != null)
        {
            player.PlayerInput3D.SetCombatInputSuppressed(isLocked);
            if (!_useNetworkSession)
            {
                player.PlayerInput3D.enabled = !isLocked;
            }
        }

        if (player.Flight != null && player.Flight.Rigidbody != null)
        {
            player.Flight.Rigidbody.linearVelocity = Vector3.zero;
            player.Flight.Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private IEnumerator WaitForNetworkRewardChoices()
    {
        while (_rewardPhaseActive)
        {
            for (byte slot = 1; slot <= 2; slot++)
            {
                if (_rewardChoiceReceivedBySlot[slot])
                {
                    continue;
                }

                if (!IsRewardChoiceClientStillConnected(slot))
                {
                    ApplyRewardChoiceAuthoritative(slot, ResolveRewardDefinitionIndexForOffer(slot, 0));
                }
            }

            if (_rewardChoiceReceivedBySlot[1] && _rewardChoiceReceivedBySlot[2])
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool IsRewardChoiceClientStillConnected(byte playerSlot)
    {
        if (!_useNetworkSession || NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClientsIds == null)
        {
            return true;
        }

        ulong ownerClientId = ResolveOwnerClientIdForSlot(playerSlot - 1);
        IReadOnlyList<ulong> connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        for (int i = 0; i < connectedClients.Count; i++)
        {
            if (connectedClients[i] == ownerClientId)
            {
                return true;
            }
        }

        return false;
    }

    private static InvasionRewardTier3D ResolveRewardTierForWave(int clearedWaveNumber)
    {
        int normalizedWaveIndex = Mathf.Max(0, clearedWaveNumber - 1) % 3;
        return normalizedWaveIndex switch
        {
            1 => InvasionRewardTier3D.Epic,
            2 => InvasionRewardTier3D.High,
            _ => InvasionRewardTier3D.Common
        };
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
        waveManager.RewardPhaseRequested -= PlayRewardPhaseAuthoritative;
        waveManager.RewardPhaseRequested += PlayRewardPhaseAuthoritative;
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
        waveManager.RewardPhaseRequested -= PlayRewardPhaseAuthoritative;
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
        RegisterCustomNetworkMessages();
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
            lifeCounterText.text = FormatLifeCounterText(_currentPlayerLives, _playerLivesRemainingBySlot[1], _playerLivesRemainingBySlot[2]);
        }
    }

    private int ResolveDisplayedLives()
    {
        int localSlot = ResolveLocalPlayerSlot();
        if (localSlot == 1 || localSlot == 2)
        {
            return _playerLivesRemainingBySlot[localSlot];
        }

        return Mathf.Min(_playerLivesRemainingBySlot[1], _playerLivesRemainingBySlot[2]);
    }

    private int ResolveLocalPlayerSlot()
    {
        if (!_useNetworkSession)
        {
            return 0;
        }

        NetworkSessionData session = NetworkSessionData.Instance;
        int localSlotIndex = session != null ? session.GetLocalSlotIndex() : -1;
        return localSlotIndex >= 0 ? localSlotIndex + 1 : 0;
    }

    private string FormatLifeCounterText(int livesRemaining, int player1Lives, int player2Lives)
    {
        string format = string.IsNullOrWhiteSpace(lifeCounterFormat) ? "{0}" : lifeCounterFormat;
        try
        {
            return string.Format(format, livesRemaining, player1Lives, player2Lives);
        }
        catch (System.FormatException)
        {
            return livesRemaining.ToString();
        }
    }

    private void RegisterCustomNetworkMessages()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || networkManager.CustomMessagingManager == null || _customNetworkMessagesRegistered)
        {
            return;
        }

        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(LivesMessageName, HandlePlayerLivesMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RespawnProtectionMessageName, HandleRespawnProtectionMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RewardOffersMessageName, HandleRewardOffersMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RewardChoiceMessageName, HandleRewardChoiceMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RewardAppliedMessageName, HandleRewardAppliedMessage);
        _customNetworkMessagesRegistered = true;
    }

    private void UnregisterCustomNetworkMessages()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_customNetworkMessagesRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
        {
            _customNetworkMessagesRegistered = false;
            return;
        }

        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(LivesMessageName);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RespawnProtectionMessageName);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RewardOffersMessageName);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RewardChoiceMessageName);
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RewardAppliedMessageName);
        _customNetworkMessagesRegistered = false;
    }

    private void BroadcastPlayerLives()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) * 2, Allocator.Temp))
        {
            writer.WriteValueSafe(_playerLivesRemainingBySlot[1]);
            writer.WriteValueSafe(_playerLivesRemainingBySlot[2]);
            networkManager.CustomMessagingManager.SendNamedMessage(LivesMessageName, networkManager.ConnectedClientsIds, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void HandlePlayerLivesMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out int player1Lives);
        reader.ReadValueSafe(out int player2Lives);
        _playerLivesRemainingBySlot[1] = Mathf.Max(0, player1Lives);
        _playerLivesRemainingBySlot[2] = Mathf.Max(0, player2Lives);
        UpdateLifeCounter(ResolveDisplayedLives());
    }

    private void BroadcastRespawnProtection(byte playerSlot, float duration)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp))
        {
            writer.WriteValueSafe(playerSlot);
            writer.WriteValueSafe(duration);
            networkManager.CustomMessagingManager.SendNamedMessage(RespawnProtectionMessageName, networkManager.ConnectedClientsIds, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void HandleRespawnProtectionMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out byte playerSlot);
        reader.ReadValueSafe(out float duration);
        StartRespawnProtectionVisual(playerSlot, Mathf.Max(0f, duration));
    }

    private void BroadcastRewardOffers()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        int offerCount = Mathf.Max(1, rewardsPerOffer);
        using (FastBufferWriter writer = new FastBufferWriter(8 + (offerCount * sizeof(int) * 2), Allocator.Temp))
        {
            writer.WriteValueSafe(_rewardPhaseSequenceId);
            writer.WriteValueSafe((int)_activeRewardPhaseTier);
            for (int slot = 1; slot <= 2; slot++)
            {
                int[] offers = _pendingRewardOfferIndicesBySlot[slot];
                for (int i = 0; i < offerCount; i++)
                {
                    int rewardIndex = offers != null && i < offers.Length ? offers[i] : -1;
                    writer.WriteValueSafe(rewardIndex);
                }
            }

            networkManager.CustomMessagingManager.SendNamedMessage(RewardOffersMessageName, networkManager.ConnectedClientsIds, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void HandleRewardOffersMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out int sequenceId);
        reader.ReadValueSafe(out int rewardTierValue);
        _rewardPhaseSequenceId = Mathf.Max(_rewardPhaseSequenceId, sequenceId);
        _activeRewardPhaseTier = SanitizeRewardTier((InvasionRewardTier3D)rewardTierValue);

        EnsureRewardStateContainers();
        ResetRewardChoiceState();
        int offerCount = Mathf.Max(1, rewardsPerOffer);
        for (int slot = 1; slot <= 2; slot++)
        {
            for (int i = 0; i < offerCount; i++)
            {
                reader.ReadValueSafe(out int rewardIndex);
                if (_pendingRewardOfferIndicesBySlot[slot] == null || _pendingRewardOfferIndicesBySlot[slot].Length != offerCount)
                {
                    _pendingRewardOfferIndicesBySlot[slot] = new int[offerCount];
                }

                _pendingRewardOfferIndicesBySlot[slot][i] = rewardIndex;
            }
        }

        _rewardPhaseActive = true;
        SetPlayersIntermissionLocked(true);

        int localSlot = ResolveLocalPlayerSlot();
        if (localSlot == 1 || localSlot == 2)
        {
            StartLocalRewardSelection((byte)localSlot);
        }
    }

    private void SendRewardChoiceToServer(byte playerSlot, int offerChoiceIndex)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp))
        {
            writer.WriteValueSafe(_rewardPhaseSequenceId);
            writer.WriteValueSafe(playerSlot);
            writer.WriteValueSafe(offerChoiceIndex);
            networkManager.CustomMessagingManager.SendNamedMessage(RewardChoiceMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void HandleRewardChoiceMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out int sequenceId);
        reader.ReadValueSafe(out byte playerSlot);
        reader.ReadValueSafe(out int offerChoiceIndex);
        if (sequenceId != _rewardPhaseSequenceId || !_rewardPhaseActive)
        {
            return;
        }

        byte senderSlot = ResolvePlayerSlotForClient(senderClientId);
        if (senderSlot != playerSlot)
        {
            Debug.LogWarning($"[InvasionSceneManager3D] Ignoring reward choice from client {senderClientId} for mismatched slot {playerSlot}.", this);
            return;
        }

        int rewardDefinitionIndex = ResolveRewardDefinitionIndexForOffer(playerSlot, offerChoiceIndex);
        ApplyRewardChoiceAuthoritative(playerSlot, rewardDefinitionIndex);
    }

    private byte ResolvePlayerSlotForClient(ulong clientId)
    {
        NetworkSessionData session = NetworkSessionData.Instance;
        if (session != null)
        {
            if (session.Player1Selection != null && session.Player1Selection.ClientId == clientId)
            {
                return 1;
            }

            if (session.Player2Selection != null && session.Player2Selection.ClientId == clientId)
            {
                return 2;
            }
        }

        return 0;
    }

    private void BroadcastRewardApplied(byte playerSlot, int rewardDefinitionIndex, InvasionRewardTier3D rewardTier)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (!_useNetworkSession || networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(20, Allocator.Temp))
        {
            writer.WriteValueSafe(_rewardPhaseSequenceId);
            writer.WriteValueSafe(playerSlot);
            writer.WriteValueSafe(rewardDefinitionIndex);
            writer.WriteValueSafe((int)rewardTier);
            networkManager.CustomMessagingManager.SendNamedMessage(RewardAppliedMessageName, networkManager.ConnectedClientsIds, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void HandleRewardAppliedMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        reader.ReadValueSafe(out int sequenceId);
        reader.ReadValueSafe(out byte playerSlot);
        reader.ReadValueSafe(out int rewardDefinitionIndex);
        reader.ReadValueSafe(out int rewardTierValue);
        if (sequenceId != _rewardPhaseSequenceId && sequenceId != _rewardPhaseSequenceId + 1)
        {
            _rewardPhaseSequenceId = Mathf.Max(_rewardPhaseSequenceId, sequenceId);
        }

        InvasionRewardTier3D rewardTier = SanitizeRewardTier((InvasionRewardTier3D)rewardTierValue);
        _activeRewardPhaseTier = rewardTier;
        EnsureRewardStateContainers();
        ApplyRewardChoiceReplica(playerSlot, rewardDefinitionIndex, rewardTier);

        if (playerSlot >= 1 && playerSlot <= 2)
        {
            _rewardChoiceReceivedBySlot[playerSlot] = true;
        }

        if (_rewardChoiceReceivedBySlot[1] && _rewardChoiceReceivedBySlot[2])
        {
            _rewardPhaseActive = false;
            SetPlayersIntermissionLocked(false);
        }
    }

    private static InvasionRewardTier3D SanitizeRewardTier(InvasionRewardTier3D tier)
    {
        return tier switch
        {
            InvasionRewardTier3D.Epic => InvasionRewardTier3D.Epic,
            InvasionRewardTier3D.High => InvasionRewardTier3D.High,
            _ => InvasionRewardTier3D.Common
        };
    }

    private void StopRespawnCoroutines()
    {
        for (int i = 1; i <= 2; i++)
        {
            if (_respawnCoroutinesBySlot[i] != null)
            {
                StopCoroutine(_respawnCoroutinesBySlot[i]);
                _respawnCoroutinesBySlot[i] = null;
            }

            if (_respawnProtectionVisualCoroutinesBySlot[i] != null)
            {
                StopCoroutine(_respawnProtectionVisualCoroutinesBySlot[i]);
                _respawnProtectionVisualCoroutinesBySlot[i] = null;
            }
        }
    }
}
