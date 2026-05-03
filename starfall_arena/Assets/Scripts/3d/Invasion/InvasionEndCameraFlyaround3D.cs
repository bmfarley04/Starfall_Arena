using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class InvasionEndCameraFlyaround3D : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Optional Cinemachine camera used only for the Invasion end screen flyaround. Assign a dedicated virtual camera when possible.")]
    [SerializeField] private CinemachineCamera virtualCamera;
    [Tooltip("Fallback real Camera to move when no Cinemachine camera is assigned. If left empty, Camera.main is used.")]
    [SerializeField] private Camera fallbackCamera;
    [Tooltip("Priority applied to the end-screen Cinemachine camera while the flyaround is active.")]
    [SerializeField] private int activePriority = 100;
    [Tooltip("Priority restored to the end-screen Cinemachine camera when the flyaround stops.")]
    [SerializeField] private int inactivePriority = 0;

    [Header("Orbit")]
    [Tooltip("Arena/world transform the end camera orbits and looks at. If empty, this component's transform is used.")]
    [SerializeField] private Transform orbitTarget;
    [Tooltip("Horizontal distance from the orbit target while the end camera is active.")]
    [Min(1f)]
    [SerializeField] private float orbitRadius = 180f;
    [Tooltip("Base world-space height above the orbit target.")]
    [SerializeField] private float orbitHeight = 55f;
    [Tooltip("Degrees per second the end camera rotates around the arena.")]
    [SerializeField] private float orbitDegreesPerSecond = 7f;
    [Tooltip("Small vertical drift amplitude applied during the orbit.")]
    [Min(0f)]
    [SerializeField] private float verticalBobAmplitude = 8f;
    [Tooltip("Cycles per second for the vertical drift.")]
    [Min(0f)]
    [SerializeField] private float verticalBobFrequency = 0.07f;
    [Tooltip("How quickly the camera position eases toward the moving orbit target.")]
    [Min(0.01f)]
    [SerializeField] private float positionSmoothing = 2.5f;
    [Tooltip("How quickly the camera rotation eases toward looking at the arena center.")]
    [Min(0.01f)]
    [SerializeField] private float rotationSmoothing = 3.5f;

    private Transform _cameraTransform;
    private bool _isActive;
    private bool _cachedCameraObjectActive;
    private int _cachedPriority;
    private float _orbitAngleDegrees;
    private float _elapsed;

    public void BeginFlyaround()
    {
        ResolveCameraTransform();
        if (_cameraTransform == null)
        {
            Debug.LogWarning("[InvasionEndCameraFlyaround3D] Cannot begin flyaround because no camera is assigned and Camera.main was not found.", this);
            return;
        }

        Transform target = ResolveOrbitTarget();
        Vector3 offset = _cameraTransform.position - target.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= 0.0001f)
        {
            offset = Vector3.back * Mathf.Max(1f, orbitRadius);
        }

        _orbitAngleDegrees = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
        _elapsed = 0f;

        if (virtualCamera != null)
        {
            _cachedPriority = virtualCamera.Priority;
            _cachedCameraObjectActive = virtualCamera.gameObject.activeSelf;
            virtualCamera.gameObject.SetActive(true);
            virtualCamera.Priority = activePriority;
        }
        else if (fallbackCamera != null)
        {
            _cachedCameraObjectActive = fallbackCamera.gameObject.activeSelf;
            fallbackCamera.gameObject.SetActive(true);
        }

        _isActive = true;
        enabled = true;
        UpdateCamera(instant: true);
    }

    public void EndFlyaround()
    {
        _isActive = false;
        enabled = false;

        if (virtualCamera != null)
        {
            virtualCamera.Priority = _cachedPriority != 0 ? _cachedPriority : inactivePriority;
            virtualCamera.gameObject.SetActive(_cachedCameraObjectActive);
        }
        else if (fallbackCamera != null)
        {
            fallbackCamera.gameObject.SetActive(_cachedCameraObjectActive);
        }
    }

    private void Awake()
    {
        ResolveCameraTransform();
        enabled = false;
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }

        UpdateCamera(instant: false);
    }

    private void UpdateCamera(bool instant)
    {
        ResolveCameraTransform();
        if (_cameraTransform == null)
        {
            return;
        }

        Transform target = ResolveOrbitTarget();
        float deltaTime = Time.unscaledDeltaTime;
        _elapsed += deltaTime;
        _orbitAngleDegrees += orbitDegreesPerSecond * deltaTime;

        float radians = _orbitAngleDegrees * Mathf.Deg2Rad;
        float heightOffset = verticalBobAmplitude > 0f && verticalBobFrequency > 0f
            ? Mathf.Sin(_elapsed * verticalBobFrequency * Mathf.PI * 2f) * verticalBobAmplitude
            : 0f;

        Vector3 desiredPosition = target.position + new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            orbitHeight + heightOffset,
            Mathf.Sin(radians) * orbitRadius);

        Vector3 lookDirection = target.position - desiredPosition;
        Quaternion desiredRotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : _cameraTransform.rotation;

        if (instant)
        {
            _cameraTransform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }

        float positionT = 1f - Mathf.Exp(-positionSmoothing * deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationSmoothing * deltaTime);
        _cameraTransform.position = Vector3.Lerp(_cameraTransform.position, desiredPosition, positionT);
        _cameraTransform.rotation = Quaternion.Slerp(_cameraTransform.rotation, desiredRotation, rotationT);
    }

    private void ResolveCameraTransform()
    {
        if (virtualCamera != null)
        {
            _cameraTransform = virtualCamera.transform;
            return;
        }

        if (fallbackCamera == null)
        {
            fallbackCamera = Camera.main;
        }

        _cameraTransform = fallbackCamera != null ? fallbackCamera.transform : null;
    }

    private Transform ResolveOrbitTarget()
    {
        return orbitTarget != null ? orbitTarget : transform;
    }
}
