using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private ProjectileWeapon3D primaryWeapon;
    [SerializeField] private Entity3D entity;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private float mouseLookSensitivity = 0.02f;

    private Vector2 _lookInput;
    private Vector2 _gamepadLookInput;
    private float _thrustInput;
    private bool _toggleFrictionPressed;
    private bool _fireHeld;
    private bool _isCursorLocked;

    private const string KeyboardMouseScheme = "key+mouse";

    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;
    public bool IsFireHeld => _fireHeld;

    private void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
        primaryWeapon ??= GetComponent<ProjectileWeapon3D>();
        entity ??= GetComponent<Entity3D>();
        playerInput ??= GetComponent<PlayerInput>();

        if (shipFlight != null)
        {
            shipFlight.SetInputSource(this);
        }
    }

    private void Update()
    {
        UpdateLookInput();
        UpdateCursorLockState();

        if (_fireHeld && primaryWeapon != null && (entity == null || !entity.IsPrimaryFireDisabledByAbility()))
        {
            primaryWeapon.TryFire();
        }
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
        _isCursorLocked = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SetCursorLocked(false);
            _isCursorLocked = false;
            return;
        }

        UpdateCursorLockState();
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
        _gamepadLookInput = value.Get<Vector2>();
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

    private void UpdateLookInput()
    {
        if (IsKeyboardMouseSchemeActive() && Mouse.current != null)
        {
            _lookInput = Mouse.current.delta.ReadValue() * mouseLookSensitivity;
            return;
        }

        _lookInput = _gamepadLookInput;
    }

    private void UpdateCursorLockState()
    {
        bool shouldLockCursor = IsKeyboardMouseSchemeActive() && Application.isFocused;
        if (_isCursorLocked == shouldLockCursor)
        {
            return;
        }

        SetCursorLocked(shouldLockCursor);
        _isCursorLocked = shouldLockCursor;
    }

    private bool IsKeyboardMouseSchemeActive()
    {
        if (playerInput == null)
        {
            return Keyboard.current != null && Mouse.current != null;
        }

        return string.Equals(playerInput.currentControlScheme, KeyboardMouseScheme, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
