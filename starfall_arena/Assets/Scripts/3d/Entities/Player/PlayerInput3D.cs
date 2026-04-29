using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput3D : MonoBehaviour, IShipFlightInputSource
{
    [SerializeField] private ShipFlight3D shipFlight;
    [SerializeField] private Entity3D entity;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private AimAssist3D aimAssist;
    [Tooltip("Sensitivity multiplier for mouse delta when keyboard/mouse is the active control scheme.")]
    [SerializeField] private float mouseLookSensitivity = 0.02f;
    [Header("Dodge Flick")]
    [Tooltip("Horizontal left-stick magnitude that commits a dodge attempt. Keep this generous so intentional left/right inputs reach the dodge cooldown gate.")]
    [SerializeField] private float dodgeFlickThreshold = 0.45f;
    [Tooltip("Horizontal left-stick magnitude that clears the held-direction latch so the same direction can be attempted again.")]
    [SerializeField] private float dodgeFlickResetThreshold = 0.25f;
    [Tooltip("Maximum vertical left-stick magnitude allowed for a left/right dodge attempt.")]
    [SerializeField] private float dodgeFlickMaxVertical = 0.9f;
    [Tooltip("Logs left-stick flick dodge detection and rejection reasons.")]
    [SerializeField] private bool logDodgeFlickDebug = true;

    private Vector2 _lookInput;
    private Vector2 _gamepadLookInput;
    private Vector2 _moveInput;
    private float _thrustInput;
    private bool _toggleFrictionPressed;
    private bool _fireHeld;
    private bool _isCursorLocked;
    private bool _appliedFireHeld;
    private Weapon3D _activeWeaponForFire;
    private bool _combatInputSuppressed;
    private int _lastDodgeFlickAttemptDirection;
    private Player3D _player;

    private const string KeyboardMouseScheme = "key+mouse";

    public Vector2 LookInput => _lookInput;
    public Vector2 MoveInput => _moveInput;
    public float ThrustInput => _thrustInput;
    public bool IsFireHeld => _fireHeld;
    public bool IsCombatInputSuppressed => _combatInputSuppressed;

    private void Awake()
    {
        ValidateDodgeFlickConfig();
        shipFlight ??= GetComponent<ShipFlight3D>();
        entity ??= GetComponent<Entity3D>();
        playerInput ??= GetComponent<PlayerInput>();
        aimAssist ??= GetComponent<AimAssist3D>();
        _player = entity as Player3D;

        if (shipFlight != null)
        {
            shipFlight.SetInputSource(this);
        }
    }

    private void OnValidate()
    {
        ValidateDodgeFlickConfig();
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

    public void SetCombatInputSuppressed(bool suppressed)
    {
        _combatInputSuppressed = suppressed;
        if (suppressed)
        {
            _fireHeld = false;
        }

        RefreshWeaponFireState();
    }

    public void OnFreeLook(InputValue value)
    {
        _gamepadLookInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        Vector2 nextMoveInput = value.Get<Vector2>();
        TryHandleDodgeFlick(nextMoveInput);
        _moveInput = nextMoveInput;
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
        if (_combatInputSuppressed)
        {
            _fireHeld = false;
            RefreshWeaponFireState();
            return;
        }

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
        if (_combatInputSuppressed || entity == null)
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

        _lookInput = _gamepadLookInput * ResolveGamepadAimSlowdownMultiplier();
    }

    private void TryHandleDodgeFlick(Vector2 nextMoveInput)
    {
        float absX = Mathf.Abs(nextMoveInput.x);
        float resetThreshold = Mathf.Clamp01(dodgeFlickResetThreshold);
        if (absX <= resetThreshold)
        {
            if (_lastDodgeFlickAttemptDirection != 0 && logDodgeFlickDebug)
            {
                Debug.Log($"[DodgeInput3D] {name} dodge direction latch reset. input={nextMoveInput}", this);
            }

            _lastDodgeFlickAttemptDirection = 0;
            return;
        }

        if (_player == null)
        {
            LogDodgeFlickRejected("missing Player3D reference", nextMoveInput);
            return;
        }

        int direction = nextMoveInput.x >= 0f ? 1 : -1;
        if (_lastDodgeFlickAttemptDirection == direction)
        {
            return;
        }

        float threshold = Mathf.Clamp01(dodgeFlickThreshold);
        if (absX < threshold)
        {
            if (absX >= Mathf.Max(0.1f, threshold - 0.15f))
            {
                LogDodgeFlickRejected($"below horizontal threshold threshold={threshold:0.00}", nextMoveInput);
            }

            return;
        }

        if (Mathf.Abs(nextMoveInput.y) > Mathf.Clamp01(dodgeFlickMaxVertical))
        {
            LogDodgeFlickRejected($"too vertical maxVertical={dodgeFlickMaxVertical:0.00}", nextMoveInput);
            return;
        }

        _lastDodgeFlickAttemptDirection = direction;
        if (logDodgeFlickDebug)
        {
            Debug.Log($"[DodgeInput3D] {name} dodge input accepted by detector. direction={(direction > 0 ? "right" : "left")} input={nextMoveInput} previous={_moveInput}", this);
        }

        if (_player.TryDodge(direction))
        {
            return;
        }

        if (logDodgeFlickDebug)
        {
            Debug.Log($"[DodgeInput3D] {name} Player3D.TryDodge rejected after flick accepted. direction={(direction > 0 ? "right" : "left")}", this);
        }
    }

    private void LogDodgeFlickRejected(string reason, Vector2 input)
    {
        if (!logDodgeFlickDebug)
        {
            return;
        }

        Debug.Log($"[DodgeInput3D] {name} flick rejected: {reason}. input={input} currentMove={_moveInput}", this);
    }

    private void ValidateDodgeFlickConfig()
    {
        if (dodgeFlickThreshold > 0.75f)
        {
            dodgeFlickThreshold = 0.55f;
        }

        dodgeFlickThreshold = Mathf.Clamp01(dodgeFlickThreshold);
        dodgeFlickResetThreshold = Mathf.Clamp(dodgeFlickResetThreshold, 0f, dodgeFlickThreshold);
        dodgeFlickMaxVertical = Mathf.Clamp01(dodgeFlickMaxVertical);
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

    private float ResolveGamepadAimSlowdownMultiplier()
    {
        if (aimAssist == null)
        {
            return 1f;
        }

        return aimAssist.GetLookSlowdownMultiplier();
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void RefreshWeaponFireState()
    {
        Weapon3D selectedWeapon = entity != null ? entity.SelectedWeapon : null;
        bool shouldHoldFire = !_combatInputSuppressed &&
            _fireHeld &&
            (entity == null || !entity.IsPrimaryFireDisabledByAbility());

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
