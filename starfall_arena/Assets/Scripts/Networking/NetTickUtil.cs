using Unity.Netcode;

/// <summary>
/// Static convenience wrapper around NGO's built-in NetworkTickSystem.
/// Avoids repeating the verbose accessor chain throughout networking code.
/// This is NOT a NetworkBehaviour — just a plain static utility class.
/// </summary>
public static class NetTickUtil
{
    /// <summary>
    /// The current network tick from NGO's tick system.
    /// Only valid while a NetworkManager session is active.
    /// </summary>
    public static int CurrentTick
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return -1;
            return nm.NetworkTickSystem.LocalTime.Tick;
        }
    }

    /// <summary>
    /// The server tick (as seen by this peer). On the server this equals CurrentTick.
    /// On clients this is the latest acknowledged server tick.
    /// </summary>
    public static int ServerTick
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return -1;
            return nm.NetworkTickSystem.ServerTime.Tick;
        }
    }

    /// <summary>
    /// Seconds per tick (1 / tick rate). Use instead of Time.fixedDeltaTime
    /// for networked simulation to stay in sync with the tick clock.
    /// </summary>
    public static float TickInterval
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return 0f;
            return 1f / nm.NetworkTickSystem.LocalTime.TickRate;
        }
    }

    /// <summary>
    /// The tick rate (ticks per second) configured on the NetworkManager.
    /// </summary>
    public static uint TickRate
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return 0;
            return nm.NetworkTickSystem.LocalTime.TickRate;
        }
    }

    /// <summary>
    /// True when a NetworkManager session is active and ticking.
    /// </summary>
    public static bool IsActive
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening;
        }
    }
}
