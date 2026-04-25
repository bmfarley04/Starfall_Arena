using UnityEngine;

public enum PlayerHUDVignetteChannel3D
{
    LowHealth = 0,
    Gigablast = 1
}

public readonly struct PlayerHUDVignetteMessage3D
{
    public PlayerHUDVignetteMessage3D(PlayerHUDVignetteChannel3D channel, float alpha, Color color)
    {
        Channel = channel;
        Alpha = Mathf.Clamp01(alpha);
        Color = color;
    }

    public PlayerHUDVignetteChannel3D Channel { get; }
    public float Alpha { get; }
    public Color Color { get; }
}

public interface IPlayerHUDMessageReceiver3D
{
    void ReceiveVignetteMessage(PlayerHUDVignetteMessage3D message);
}
