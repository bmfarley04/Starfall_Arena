using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAIFlightController3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;

    private Vector2 _lookInput;
    private float _thrustInput;

    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;

    private void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        shipFlight?.SetInputSource(this);
    }

    public void SetFlightIntent(Vector2 lookInput, float thrustInput)
    {
        _lookInput = Vector2.ClampMagnitude(lookInput, 1f);
        _thrustInput = Mathf.Clamp(thrustInput, -1f, 1f);
    }

    public void ClearFlightIntent()
    {
        _lookInput = Vector2.zero;
        _thrustInput = 0f;
    }

    public bool ConsumeToggleFrictionPressed()
    {
        return false;
    }
}
