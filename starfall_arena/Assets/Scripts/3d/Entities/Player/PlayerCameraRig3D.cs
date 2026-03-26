using Unity.Cinemachine;
using UnityEngine;

public class PlayerCameraRig3D : MonoBehaviour
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private PlayerCameraRigConfig3D cameraConfig = new PlayerCameraRigConfig3D
    {
        minZOffset = -10f,
        maxZOffset = -16f,
        minFOV = 40f,
        maxFOV = 70f,
        cameraLerpSpeed = 5f
    };

    private CinemachineFollow _followComponent;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        CacheCameraComponents();
    }

    private void Update()
    {
        if (shipFlight == null || virtualCamera == null || _followComponent == null)
        {
            return;
        }

        float forwardSpeedPercent = shipFlight.ForwardSpeedNormalized;
        float targetZ = Mathf.Lerp(cameraConfig.minZOffset, cameraConfig.maxZOffset, forwardSpeedPercent);
        float targetFOV = Mathf.Lerp(cameraConfig.minFOV, cameraConfig.maxFOV, forwardSpeedPercent);

        Vector3 currentOffset = _followComponent.FollowOffset;
        currentOffset.z = Mathf.Lerp(currentOffset.z, targetZ, Time.deltaTime * cameraConfig.cameraLerpSpeed);
        _followComponent.FollowOffset = currentOffset;

        virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, targetFOV, Time.deltaTime * cameraConfig.cameraLerpSpeed);
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetCamera(CinemachineCamera camera)
    {
        virtualCamera = camera;
        CacheCameraComponents();
    }

    public void SetCameraConfig(PlayerCameraRigConfig3D config)
    {
        cameraConfig = config;
    }

    private void CacheCameraComponents()
    {
        _followComponent = virtualCamera != null ? virtualCamera.GetComponent<CinemachineFollow>() : null;
    }
}
