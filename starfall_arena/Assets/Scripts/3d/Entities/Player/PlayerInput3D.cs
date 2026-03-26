using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ProjectileWeapon3D primaryWeapon;

    private Vector2 _lookInput;
    private float _thrustInput;
    private bool _toggleFrictionPressed;
    private bool _fireHeld;

    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;

    private void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();

        if (shipFlight != null)
        {
            shipFlight.SetInputSource(this);
        }
    }

    private void Update()
    {
        if (_fireHeld && primaryWeapon != null)
        {
            primaryWeapon.TryFire();
        }
    }

    public bool ConsumeToggleFrictionPressed()
    {
        bool wasPressed = _toggleFrictionPressed;
        _toggleFrictionPressed = false;
        return wasPressed;
    }

    public void SetPrimaryWeapon(ProjectileWeapon3D weapon)
    {
        primaryWeapon = weapon;
    }

    public void OnFreeLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
    }

    public void OnThrust(InputValue value)
    {
        _thrustInput = value.Get<float>();
    }

    public void OnToggleFriction(InputValue value)
    {
        if (value.isPressed)
        {
            _toggleFrictionPressed = true;
        }
    }

    public void OnFire(InputValue value)
    {
        _fireHeld = value.Get<float>() > 0f;
    }
}
