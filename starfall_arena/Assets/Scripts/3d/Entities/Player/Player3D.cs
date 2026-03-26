using UnityEngine;

public class Player3D : Entity3D
{
    [Header("Player-Only 3D Systems")]
    [SerializeField] protected PlayerInput3D playerInput3D;
    [SerializeField] protected PlayerCameraRig3D playerCameraRig3D;

    public PlayerInput3D PlayerInput3D => playerInput3D;
    public PlayerCameraRig3D PlayerCameraRig3D => playerCameraRig3D;

    protected override void Awake()
    {
        base.Awake();
        playerInput3D ??= GetComponent<PlayerInput3D>();
        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();

        if (playerInput3D != null && shipFlight != null)
        {
            shipFlight.SetInputSource(playerInput3D);
        }

        if (playerCameraRig3D != null && shipFlight != null)
        {
            playerCameraRig3D.SetShipFlight(shipFlight);
        }
    }
}
