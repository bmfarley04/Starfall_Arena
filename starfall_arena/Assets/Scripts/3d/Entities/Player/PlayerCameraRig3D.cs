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
        cameraLerpSpeed = 5f,
        horizontalTurnOffset = 2.5f,
        verticalTurnOffset = 1.2f,
        yawRateOffsetContribution = 0.6f,
        pitchRateOffsetContribution = 0.5f,
        turnOffsetLerpSpeed = 6f,
        recenterLerpSpeed = 2.5f,
        followPositionDampingAtRest = 0.12f,
        followPositionDampingDuringTurn = 0.45f,
        followRotationDampingAtRest = 0.1f,
        followRotationDampingDuringTurn = 0.35f,
        aimDampingAtRest = 0.1f,
        aimDampingDuringTurn = 0.25f
    };

    private CinemachineFollow _followComponent;
    private CinemachineRotateWithFollowTarget _rotateWithFollowTarget;
    private Vector3 _baseFollowOffset;
    private bool _baseFollowOffsetCaptured;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        ValidateCameraConfig();
        CacheCameraComponents();
    }

    private void OnValidate()
    {
        ValidateCameraConfig();
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

        Vector2 filteredInput = shipFlight.FilteredLookInput;
        Vector2 turnRates = shipFlight.NormalizedTurnRates;
        float yawOffsetSignal = Mathf.Clamp(filteredInput.x + (turnRates.y * cameraConfig.yawRateOffsetContribution), -1f, 1f);
        float pitchOffsetSignal = Mathf.Clamp(filteredInput.y + (turnRates.x * cameraConfig.pitchRateOffsetContribution), -1f, 1f);
        float steeringAmount = Mathf.Clamp01(Mathf.Max(Mathf.Abs(yawOffsetSignal), Mathf.Abs(pitchOffsetSignal)));

        float targetX = _baseFollowOffset.x + (yawOffsetSignal * cameraConfig.horizontalTurnOffset);
        float targetY = _baseFollowOffset.y + (pitchOffsetSignal * cameraConfig.verticalTurnOffset);
        float offsetLerpSpeed = steeringAmount > 0.05f ? cameraConfig.turnOffsetLerpSpeed : cameraConfig.recenterLerpSpeed;
        float offsetLerpFactor = 1f - Mathf.Exp(-offsetLerpSpeed * Time.deltaTime);

        Vector3 currentOffset = _followComponent.FollowOffset;
        currentOffset.x = Mathf.Lerp(currentOffset.x, targetX, offsetLerpFactor);
        currentOffset.y = Mathf.Lerp(currentOffset.y, targetY, offsetLerpFactor);
        currentOffset.z = Mathf.Lerp(currentOffset.z, targetZ, 1f - Mathf.Exp(-cameraConfig.cameraLerpSpeed * Time.deltaTime));
        _followComponent.FollowOffset = currentOffset;

        var trackerSettings = _followComponent.TrackerSettings;
        trackerSettings.PositionDamping = Vector3.one * Mathf.Lerp(cameraConfig.followPositionDampingAtRest, cameraConfig.followPositionDampingDuringTurn, steeringAmount);
        trackerSettings.RotationDamping = Vector3.one * Mathf.Lerp(cameraConfig.followRotationDampingAtRest, cameraConfig.followRotationDampingDuringTurn, steeringAmount);
        _followComponent.TrackerSettings = trackerSettings;

        if (_rotateWithFollowTarget != null)
        {
            _rotateWithFollowTarget.Damping = Mathf.Lerp(cameraConfig.aimDampingAtRest, cameraConfig.aimDampingDuringTurn, steeringAmount);
        }

        virtualCamera.Lens.FieldOfView = Mathf.Lerp(virtualCamera.Lens.FieldOfView, targetFOV, 1f - Mathf.Exp(-cameraConfig.cameraLerpSpeed * Time.deltaTime));
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

    public void BindTrackingTarget(Transform target)
    {
        if (virtualCamera == null || target == null)
        {
            return;
        }

        virtualCamera.Target.TrackingTarget = target;
    }

    public void SetCameraRigActive(bool isActive)
    {
        if (virtualCamera != null)
        {
            virtualCamera.gameObject.SetActive(isActive);
        }

        enabled = isActive;
    }

    public void SetCameraConfig(PlayerCameraRigConfig3D config)
    {
        cameraConfig = config;
        ValidateCameraConfig();
    }

    private void CacheCameraComponents()
    {
        _followComponent = virtualCamera != null ? virtualCamera.GetComponent<CinemachineFollow>() : null;
        _rotateWithFollowTarget = virtualCamera != null ? virtualCamera.GetComponent<CinemachineRotateWithFollowTarget>() : null;

        if (_followComponent != null)
        {
            _baseFollowOffset = _followComponent.FollowOffset;
            _baseFollowOffsetCaptured = true;
        }
    }

    private void ValidateCameraConfig()
    {
        if (cameraConfig.cameraLerpSpeed <= 0f)
        {
            cameraConfig.cameraLerpSpeed = 5f;
        }

        if (cameraConfig.turnOffsetLerpSpeed <= 0f)
        {
            cameraConfig.turnOffsetLerpSpeed = 6f;
        }

        if (cameraConfig.recenterLerpSpeed <= 0f)
        {
            cameraConfig.recenterLerpSpeed = 2.5f;
        }

        if (Mathf.Approximately(cameraConfig.horizontalTurnOffset, 0f))
        {
            cameraConfig.horizontalTurnOffset = 2.5f;
        }

        if (Mathf.Approximately(cameraConfig.verticalTurnOffset, 0f))
        {
            cameraConfig.verticalTurnOffset = 1.2f;
        }

        if (Mathf.Approximately(cameraConfig.yawRateOffsetContribution, 0f))
        {
            cameraConfig.yawRateOffsetContribution = 0.6f;
        }

        if (Mathf.Approximately(cameraConfig.pitchRateOffsetContribution, 0f))
        {
            cameraConfig.pitchRateOffsetContribution = 0.5f;
        }

        if (cameraConfig.followPositionDampingAtRest < 0f)
        {
            cameraConfig.followPositionDampingAtRest = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.followPositionDampingAtRest, 0f))
        {
            cameraConfig.followPositionDampingAtRest = 0.12f;
        }

        if (cameraConfig.followPositionDampingDuringTurn < 0f)
        {
            cameraConfig.followPositionDampingDuringTurn = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.followPositionDampingDuringTurn, 0f))
        {
            cameraConfig.followPositionDampingDuringTurn = 0.45f;
        }

        if (cameraConfig.followRotationDampingAtRest < 0f)
        {
            cameraConfig.followRotationDampingAtRest = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.followRotationDampingAtRest, 0f))
        {
            cameraConfig.followRotationDampingAtRest = 0.1f;
        }

        if (cameraConfig.followRotationDampingDuringTurn < 0f)
        {
            cameraConfig.followRotationDampingDuringTurn = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.followRotationDampingDuringTurn, 0f))
        {
            cameraConfig.followRotationDampingDuringTurn = 0.35f;
        }

        if (cameraConfig.aimDampingAtRest < 0f)
        {
            cameraConfig.aimDampingAtRest = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.aimDampingAtRest, 0f))
        {
            cameraConfig.aimDampingAtRest = 0.1f;
        }

        if (cameraConfig.aimDampingDuringTurn < 0f)
        {
            cameraConfig.aimDampingDuringTurn = 0f;
        }
        else if (Mathf.Approximately(cameraConfig.aimDampingDuringTurn, 0f))
        {
            cameraConfig.aimDampingDuringTurn = 0.25f;
        }

        if (cameraConfig.minFOV <= 0f)
        {
            cameraConfig.minFOV = 30f;
        }

        if (cameraConfig.maxFOV < cameraConfig.minFOV)
        {
            cameraConfig.maxFOV = cameraConfig.minFOV;
        }

        if (!_baseFollowOffsetCaptured)
        {
            _baseFollowOffset = new Vector3(0f, 0.8f, -12f);
        }
    }
}
