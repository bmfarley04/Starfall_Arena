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
    [Tooltip("Optional tag filter such as Player1 or Player2.")]
    [SerializeField] private string playerTagFilter;
    [Tooltip("Optional case-insensitive name match. Example: class1 matches 3d_class1_player(Clone).")]
    [SerializeField] private string playerNameContains;

    private Player3D _boundPlayer;

    public event Action<Player3D> BoundPlayerChanged;

    public Player3D BoundPlayer => _boundPlayer;

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponentInParent<Player3D>();
        }
    }

    private void OnEnable()
    {
        Player3D.PlayerSpawned += HandlePlayerSpawned;
        Player3D.PlayerDespawned += HandlePlayerDespawned;
        TryBindToPlayer();
    }

    private void OnDisable()
    {
        Player3D.PlayerSpawned -= HandlePlayerSpawned;
        Player3D.PlayerDespawned -= HandlePlayerDespawned;
    }

    public void Bind(Player3D targetPlayer)
    {
        if (ReferenceEquals(_boundPlayer, targetPlayer))
        {
            return;
        }

        _boundPlayer = targetPlayer;
        BoundPlayerChanged?.Invoke(_boundPlayer);
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
}
