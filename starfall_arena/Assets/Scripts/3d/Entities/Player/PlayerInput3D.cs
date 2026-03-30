using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private Entity3D entity;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private float mouseLookSensitivity = 0.02f;

    private Vector2 _lookInput;
    private Vector2 _gamepadLookInput;
    private float _thrustInput;
    private bool _toggleFrictionPressed;
    private bool _fireHeld;
    private bool _isCursorLocked;
    private bool _appliedFireHeld;
    private Weapon3D _activeWeaponForFire;

    private const string KeyboardMouseScheme = "key+mouse";

    public Vector2 LookInput => _lookInput;
    public float ThrustInput => _thrustInput;
    public bool IsFireHeld => _fireHeld;

    private void Awake()
    {
        shipFlight ??= GetComponent<ShipFlight3D>();
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
        RefreshWeaponFireState();
    }

    private void OnDisable()
    {
        _activeWeaponForFire?.OnDeselected();
        _activeWeaponForFire = null;
        _appliedFireHeld = false;
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
        RefreshWeaponFireState();
    }

    public void OnAbility1(InputValue value) { TryUseAbility(0, value); }
    public void OnAbility2(InputValue value) { TryUseAbility(1, value); }
    public void OnAbility3(InputValue value) { }
    public void OnAbility4(InputValue value) { }
    public void OnWeapon1(InputValue value) { TrySelectWeapon(0, value); }
    public void OnWeapon2(InputValue value) { TrySelectWeapon(1, value); }
    public void OnWeapon3(InputValue value) { TrySelectWeapon(2, value); }

    private void TryUseAbility(int index, InputValue value)
    {
        if (entity == null)
        {
            return;
        }

        Ability3D ability = entity.GetAbility(index);
        ability?.TryUseAbility(value);
    }

    private void TrySelectWeapon(int index, InputValue value)
    {
        if (entity == null || !value.isPressed)
        {
            return;
        }

        if (entity.SelectWeapon(index))
        {
            RefreshWeaponFireState();
        }
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

    private void RefreshWeaponFireState()
    {
        Weapon3D selectedWeapon = entity != null ? entity.SelectedWeapon : null;
        bool shouldHoldFire = _fireHeld && (entity == null || !entity.IsPrimaryFireDisabledByAbility());

        if (_activeWeaponForFire != selectedWeapon)
        {
            _activeWeaponForFire?.OnDeselected();
            _activeWeaponForFire = selectedWeapon;
            _activeWeaponForFire?.OnSelected();
            _appliedFireHeld = false;
        }

        if (_activeWeaponForFire == null)
        {
            _appliedFireHeld = false;
            return;
        }

        if (_appliedFireHeld == shouldHoldFire)
        {
            return;
        }

        _activeWeaponForFire.SetFireHeld(shouldHoldFire);
        _appliedFireHeld = shouldHoldFire;
    }
}
