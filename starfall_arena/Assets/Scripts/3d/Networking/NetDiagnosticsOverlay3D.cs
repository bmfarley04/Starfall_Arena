using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class NetDiagnosticsOverlay3D : MonoBehaviour
{
    [Header("Visibility")]
    [Tooltip("If enabled, the diagnostics overlay is visible as soon as this component starts.")]
    [SerializeField] private bool showOnStart;

    [Tooltip("Keyboard key that toggles the overlay during local playtests.")]
    [SerializeField] private Key toggleKey = Key.F8;

    [Header("Refresh")]
    [Tooltip("Seconds between scene scans for networked 3D movement components. Higher values reduce debug overhead.")]
    [Min(0.1f)] [SerializeField] private float refreshIntervalSeconds = 0.5f;

    [Tooltip("Maximum number of enemy movement rows shown before the overlay summarizes the rest.")]
    [Min(1)] [SerializeField] private int maxEnemyRows = 8;

    private NetMovement3D[] _players = System.Array.Empty<NetMovement3D>();
    private NetEnemyMovement3D[] _enemies = System.Array.Empty<NetEnemyMovement3D>();
    private float _nextRefreshTime;
    private bool _visible;
    private Vector2 _scroll;

    private void Awake()
    {
        _visible = showOnStart;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            _visible = !_visible;
        }

        if (!_visible || Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshIntervalSeconds);
        _players = FindObjectsByType<NetMovement3D>(FindObjectsSortMode.None);
        _enemies = FindObjectsByType<NetEnemyMovement3D>(FindObjectsSortMode.None);
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        const float width = 520f;
        const float height = 420f;
        GUILayout.BeginArea(new Rect(12f, 12f, width, height), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label("3D Network Diagnostics");
        GUILayout.Label(BuildSessionLine());
        GUILayout.Space(6f);

        GUILayout.Label("Players");
        if (_players == null || _players.Length == 0)
        {
            GUILayout.Label("  none");
        }
        else
        {
            for (int i = 0; i < _players.Length; i++)
            {
                NetMovement3D player = _players[i];
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                GUILayout.Label(FormatPlayerLine(player));
            }
        }

        GUILayout.Space(6f);
        GUILayout.Label("Enemies");
        if (_enemies == null || _enemies.Length == 0)
        {
            GUILayout.Label("  none");
        }
        else
        {
            int shown = 0;
            int totalStarves = 0;
            int totalHardSnaps = 0;
            int totalExtrapolated = 0;

            for (int i = 0; i < _enemies.Length; i++)
            {
                NetEnemyMovement3D enemy = _enemies[i];
                if (enemy == null || !enemy.IsSpawned)
                {
                    continue;
                }

                NetInterpolationDiagnostics3D diagnostics = enemy.InterpolationDiagnostics;
                totalStarves += diagnostics.BufferStarvationEvents;
                totalHardSnaps += diagnostics.HardSnaps;
                totalExtrapolated += diagnostics.ExtrapolatedFrames;

                if (shown < maxEnemyRows)
                {
                    GUILayout.Label(FormatEnemyLine(enemy, diagnostics));
                    shown++;
                }
            }

            if (_enemies.Length > shown)
            {
                GUILayout.Label($"  +{_enemies.Length - shown} more | starve={totalStarves} extrap={totalExtrapolated} snaps={totalHardSnaps}");
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static string BuildSessionLine()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return "session inactive";
        }

        return $"tick={NetTickUtil.CurrentTick} serverTick={NetTickUtil.ServerTick} rate={NetTickUtil.TickRate}hz";
    }

    private static string FormatPlayerLine(NetMovement3D player)
    {
        NetInterpolationDiagnostics3D diagnostics = player.InterpolationDiagnostics;
        return $"  {player.name} slot={player.PlayerSlot} owner={player.IsOwner} server={player.IsServer} " +
            $"buf={diagnostics.CurrentBufferDepth} delay={diagnostics.CurrentDelayMs:0}ms " +
            $"recv={diagnostics.ReceivedSnapshots} starve={diagnostics.BufferStarvationEvents} " +
            $"extrap={diagnostics.ExtrapolatedFrames} snaps={diagnostics.HardSnaps} " +
            $"corr={player.OwnerCorrectionCount} rate={player.OwnerCorrectionsPerSecond:0.#}/s " +
            $"latest={player.LatestOwnerCorrectionDistance:0.###} largest={player.LargestRecentOwnerCorrectionDistance:0.###} " +
            $"avg={player.AverageOwnerCorrectionDistance:0.###} cause={player.LatestOwnerCorrectionLikelyCause} side={player.LatestOwnerCorrectionSideEffect}";
    }

    private static string FormatEnemyLine(NetEnemyMovement3D enemy, NetInterpolationDiagnostics3D diagnostics)
    {
        return $"  {enemy.name} buf={diagnostics.CurrentBufferDepth} delay={diagnostics.CurrentDelayMs:0}ms " +
            $"recv={diagnostics.ReceivedSnapshots} starve={diagnostics.BufferStarvationEvents} " +
            $"extrap={diagnostics.ExtrapolatedFrames} snaps={diagnostics.HardSnaps}";
    }
}
