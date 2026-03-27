using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private Entity3D entity;

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
        entity ??= GetComponent<Entity3D>();

        if (shipFlight != null)
        {
            shipFlight.SetInputSource(this);
        }
    }

    private void Update()
    {
        if (_fireHeld && primaryWeapon != null && (entity == null || !entity.IsPrimaryFireDisabledByAbility()))
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

    // ===== ABILITY INPUT =====

    public void OnAbility1(InputValue value) { TryUseAbility(0, value); }
    public void OnAbility2(InputValue value) { TryUseAbility(1, value); }
    public void OnAbility3(InputValue value) { TryUseAbility(2, value); }
    public void OnAbility4(InputValue value) { TryUseAbility(3, value); }

    private void TryUseAbility(int index, InputValue value)
    {
        if (entity == null) return;
        Ability3D ability = entity.GetAbility(index);
        ability?.TryUseAbility(value);
    }
}
