using UnityEngine;

public class ShipVisualTilt3D : MonoBehaviour
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private VisualEffects3DConfig visualEffects;

    private float _currentBankAngle;
    private float _currentPitchLeanAngle;
    private Quaternion _visualBaseLocalRotation = Quaternion.identity;

    private void Awake()
    {
        if (shipFlight == null)
        {
            shipFlight = GetComponent<ShipFlight3D>();
        }

        CacheBaseRotation();
    }

    private void LateUpdate()
    {
        UpdateVisualRotation();
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetVisualEffects(VisualEffects3DConfig config)
    {
        visualEffects = config;
        CacheBaseRotation();
    }

    private void CacheBaseRotation()
    {
        if (visualEffects.visualModel != null)
        {
            _visualBaseLocalRotation = visualEffects.visualModel.localRotation;
        }
        else
        {
            _visualBaseLocalRotation = Quaternion.identity;
        }
    }

    private void UpdateVisualRotation()
    {
        if (shipFlight == null || visualEffects.visualModel == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 angularVelocity = shipFlight.Rigidbody != null ? shipFlight.Rigidbody.angularVelocity : Vector3.zero;
        Vector3 linearAcceleration = shipFlight.LinearAcceleration;

        float yawAngVel = Vector3.Dot(angularVelocity, transform.up);
        float pitchAngVel = Vector3.Dot(angularVelocity, transform.right);
        float forwardAccel = Vector3.Dot(linearAcceleration, transform.forward);
        float lateralAccel = Vector3.Dot(linearAcceleration, transform.right);

        float targetBankAngle = Mathf.Clamp(
            (-yawAngVel * visualEffects.bankSensitivity) + (-lateralAccel * visualEffects.lateralAccelBankSensitivity),
            -visualEffects.maxBankAngle,
            visualEffects.maxBankAngle
        );

        float targetPitchLeanAngle = Mathf.Clamp(
            (pitchAngVel * visualEffects.pitchLeanSensitivity) + (-forwardAccel * visualEffects.forwardAccelPitchSensitivity),
            -visualEffects.maxPitchLeanAngle,
            visualEffects.maxPitchLeanAngle
        );

        _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBankAngle, Time.deltaTime * visualEffects.bankSmoothing);
        _currentPitchLeanAngle = Mathf.Lerp(_currentPitchLeanAngle, targetPitchLeanAngle, Time.deltaTime * visualEffects.pitchLeanSmoothing);

        Quaternion pitchQuat = Quaternion.AngleAxis(_currentPitchLeanAngle, Vector3.right);
        Quaternion bankQuat = Quaternion.AngleAxis(_currentBankAngle, Vector3.forward);
        visualEffects.visualModel.localRotation = _visualBaseLocalRotation * pitchQuat * bankQuat;
    }
}
