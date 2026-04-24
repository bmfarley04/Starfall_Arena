using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHUDManager3D : MonoBehaviour
{
    [Header("Player Binding")]
    [SerializeField] private Player3D player;
    [SerializeField] private bool autoBindToMatchingPlayer = true;
    [SerializeField] private bool preferLocalPlayer = true;
    [SerializeField] private bool fallbackToFirstMatchingPlayer = true;
    [Tooltip("How long to keep retrying local-player binding after players spawn. This covers NGO spawn order where Player3D enables before NetMovement3D enables owner input.")]
    [SerializeField] private float localPlayerBindingRetrySeconds = 3f;
    [Tooltip("Seconds between local-player binding retries.")]
    [SerializeField] private float localPlayerBindingRetryInterval = 0.1f;
    [Tooltip("Optional tag filter such as Player1 or Player2.")]
    [SerializeField] private string playerTagFilter;
    [Tooltip("Optional case-insensitive name match. Example: class1 matches 3d_class1_player(Clone).")]
    [SerializeField] private string playerNameContains;

    private Player3D _boundPlayer;
    private float _retryUntilTime;
    private float _nextRetryTime;
    private IPlayerHUDMessageReceiver3D[] _messageReceivers;

    public event Action<Player3D> BoundPlayerChanged;

    public Player3D BoundPlayer => _boundPlayer;

    public static void RebindAllAutoManagers()
    {
        PlayerHUDManager3D[] managers = FindObjectsByType<PlayerHUDManager3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            PlayerHUDManager3D manager = managers[i];
            if (manager == null || !manager.autoBindToMatchingPlayer)
            {
                continue;
            }

            manager.BeginLocalPlayerBindingRetry();
            manager.TryBindToPlayer();
        }
    }

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player3D>();
        }

        CacheMessageReceivers();
    }

    private void OnEnable()
    {
        Player3D.PlayerSpawned += HandlePlayerSpawned;
        Player3D.PlayerDespawned += HandlePlayerDespawned;
        BeginLocalPlayerBindingRetry();
        TryBindToPlayer();
    }

    private void OnDisable()
    {
        Player3D.PlayerSpawned -= HandlePlayerSpawned;
        Player3D.PlayerDespawned -= HandlePlayerDespawned;
        _retryUntilTime = 0f;
    }

    private void Update()
    {
        if (!ShouldRetryLocalPlayerBinding())
        {
            return;
        }

        if (Time.unscaledTime < _nextRetryTime)
        {
            return;
        }

        _nextRetryTime = Time.unscaledTime + Mathf.Max(0.01f, localPlayerBindingRetryInterval);
        TryBindToPlayer();

        if (_boundPlayer != null && IsPreferredLocalPlayerCandidate(_boundPlayer))
        {
            _retryUntilTime = 0f;
        }
    }

    public void Bind(Player3D targetPlayer)
    {
        if (ReferenceEquals(_boundPlayer, targetPlayer))
        {
            _boundPlayer?.BindHUD(this);
            return;
        }

        _boundPlayer?.UnbindHUD(this);
        _boundPlayer = targetPlayer;
        _boundPlayer?.BindHUD(this);
        BoundPlayerChanged?.Invoke(_boundPlayer);
    }

    public void PublishVignetteMessage(PlayerHUDVignetteMessage3D message)
    {
        if (_messageReceivers == null || _messageReceivers.Length == 0)
        {
            CacheMessageReceivers();
        }

        if (_messageReceivers == null)
        {
            return;
        }

        for (int i = 0; i < _messageReceivers.Length; i++)
        {
            _messageReceivers[i]?.ReceiveVignetteMessage(message);
        }
    }

    public bool TryBindToPlayer()
    {
        if (player != null && player.isActiveAndEnabled)
        {
            Bind(player);
            return true;
        }

        if (!autoBindToMatchingPlayer)
        {
            Bind(null);
            return false;
        }

        Player3D candidate = FindBestPlayerCandidate();
        Bind(candidate);
        return candidate != null;
    }

    private void HandlePlayerSpawned(Player3D spawnedPlayer)
    {
        if (spawnedPlayer == null)
        {
            return;
        }

        if (player != null)
        {
            if (ReferenceEquals(player, spawnedPlayer))
            {
                Bind(spawnedPlayer);
            }

            return;
        }

        if (!autoBindToMatchingPlayer || !MatchesBindingCriteria(spawnedPlayer))
        {
            return;
        }

        BeginLocalPlayerBindingRetry();
        if (_boundPlayer == null || (preferLocalPlayer && IsPreferredLocalPlayerCandidate(spawnedPlayer)))
        {
            TryBindToPlayer();
        }
    }

    private void HandlePlayerDespawned(Player3D despawnedPlayer)
    {
        if (!ReferenceEquals(_boundPlayer, despawnedPlayer))
        {
            return;
        }

        TryBindToPlayer();
    }

    private void BeginLocalPlayerBindingRetry()
    {
        if (!autoBindToMatchingPlayer || !preferLocalPlayer)
        {
            return;
        }

        float duration = Mathf.Max(0f, localPlayerBindingRetrySeconds);
        if (duration <= 0f)
        {
            return;
        }

        _retryUntilTime = Time.unscaledTime + duration;
        _nextRetryTime = Time.unscaledTime;
    }

    private bool ShouldRetryLocalPlayerBinding()
    {
        if (!autoBindToMatchingPlayer || !preferLocalPlayer || Time.unscaledTime > _retryUntilTime)
        {
            return false;
        }

        return _boundPlayer == null || !IsPreferredLocalPlayerCandidate(_boundPlayer);
    }

    private Player3D FindBestPlayerCandidate()
    {
        Player3D[] players = FindObjectsByType<Player3D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Player3D fallbackCandidate = null;
        Player3D localCandidate = null;

        for (int i = 0; i < players.Length; i++)
        {
            Player3D candidate = players[i];
            if (!MatchesBindingCriteria(candidate))
            {
                continue;
            }

            fallbackCandidate ??= candidate;

            if (!preferLocalPlayer || !IsPreferredLocalPlayerCandidate(candidate))
            {
                continue;
            }

            if (localCandidate != null && !ReferenceEquals(localCandidate, candidate))
            {
                Debug.LogWarning(
                    $"PlayerHUDManager3D found multiple local-player HUD candidates ({localCandidate.name}, {candidate.name}). " +
                    "Use the tag/name filter or an explicit player reference to remove the ambiguity.",
                    this);
            }

            localCandidate = candidate;
        }

        if (preferLocalPlayer && localCandidate != null)
        {
            return localCandidate;
        }

        return fallbackToFirstMatchingPlayer ? fallbackCandidate : null;
    }

    private bool MatchesBindingCriteria(Player3D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTagFilter) && !candidate.CompareTag(playerTagFilter))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerNameContains)
            && candidate.name.IndexOf(playerNameContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsPreferredLocalPlayerCandidate(Player3D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        PlayerInput playerInput = candidate.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            return playerInput.enabled && playerInput.isActiveAndEnabled;
        }

        return candidate.PlayerInput3D != null && candidate.PlayerInput3D.isActiveAndEnabled;
    }

    private void CacheMessageReceivers()
    {
        _messageReceivers = GetComponentsInChildren<IPlayerHUDMessageReceiver3D>(true);
    }
}
