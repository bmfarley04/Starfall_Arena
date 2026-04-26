using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[DisallowMultipleComponent]
public class Dodge3D : Ability3D
{
    [System.Serializable]
    public struct DodgeConfig3D
    {
        [Header("Dodge")]
        public float cooldown;
        public float dodgeDistance;
        public float slideDuration;
        public float primeWindow;
        public float lookDeadzone;

        [Header("Empowerment")]
        public Empower3D empowerAbility;
        public bool forceEmpowered;
        public float empoweredCooldown;

        [Header("Sound Effects")]
        public SoundEffect primeSound;
        public SoundEffect dodgeSound;
    }

    [Header("Class4 Dodge")]
    [SerializeField] private DodgeConfig3D dodge = new DodgeConfig3D
    {
        cooldown = 3f,
        dodgeDistance = 8f,
        slideDuration = 0.2f,
        primeWindow = 1.5f,
        lookDeadzone = 0.35f,
        empoweredCooldown = 1f
    };

    private bool _isPrimed;
    private bool _isDodging;
    private float _primeStartTime;
    private float _dodgeEndTime = float.NegativeInfinity;
    private NetCombat3D _netCombat;
    private Player3D _player;
    private Coroutine _localDodgeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _netCombat = GetComponent<NetCombat3D>();
        _player = entity as Player3D;
        if (dodge.empowerAbility == null)
        {
            dodge.empowerAbility = GetComponent<Empower3D>();
        }

        SetInitialCooldownState(GetCooldownDuration());
    }

    protected override void Update()
    {
        base.Update();

        if (_isDodging && Time.time >= _dodgeEndTime)
        {
            _isDodging = false;
        }

        if (!_isPrimed)
        {
            return;
        }

        if (Time.time > _primeStartTime + Mathf.Max(0f, dodge.primeWindow))
        {
            _isPrimed = false;
            return;
        }

        Vector2 lookInput = _player != null && _player.PlayerInput3D != null
            ? _player.PlayerInput3D.LookInput
            : Vector2.zero;

        if (lookInput.magnitude <= Mathf.Clamp01(dodge.lookDeadzone))
        {
            return;
        }

        ExecuteDodge(ResolveCardinalDirection(lookInput));
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed || isLocked || isDisabledByOtherAbility || IsOnCooldown())
        {
            return false;
        }

        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        _isPrimed = true;
        _primeStartTime = Time.time;
        dodge.primeSound?.PlayAtPoint(transform.position);
    }

    public override bool IsAbilityActive()
    {
        return _isPrimed || _isDodging;
    }

    public override float GetRotationMultiplier()
    {
        return _isDodging ? 0f : 1f;
    }

    public override float GetThrustMultiplier()
    {
        return _isDodging ? 0f : 1f;
    }

    protected override float GetCooldownDuration()
    {
        return IsEmpoweredActive() ? dodge.empoweredCooldown : dodge.cooldown;
    }

    public void ApplyNetworkDodge(Vector3 worldDirection, bool authoritative)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        StartDodge(worldDirection.normalized, authoritative);
    }

    public override void Die()
    {
        _isPrimed = false;
        _isDodging = false;
        _dodgeEndTime = float.NegativeInfinity;
        if (_localDodgeCoroutine != null)
        {
            StopCoroutine(_localDodgeCoroutine);
            _localDodgeCoroutine = null;
        }
    }

    private void ExecuteDodge(Vector3 worldDirection)
    {
        _isPrimed = false;
        MarkAbilityUsed();

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            if (!_netCombat.IsServer)
            {
                StartDodge(worldDirection, authoritative: true);
            }

            _netCombat.RequestClass4Dodge(worldDirection);
            return;
        }

        StartDodge(worldDirection, authoritative: true);
    }

    private void StartDodge(Vector3 worldDirection, bool authoritative)
    {
        dodge.dodgeSound?.PlayAtPoint(transform.position);

        _isDodging = true;
        _dodgeEndTime = Time.time + Mathf.Max(0.01f, dodge.slideDuration);

        if (!authoritative)
        {
            return;
        }

        NetMovement3D movement = GetComponent<NetMovement3D>();
        if (entity == null || entity.Flight == null)
        {
            return;
        }

        Vector3 normalizedDirection = worldDirection.normalized;
        float dodgeDistance = Mathf.Max(0.01f, dodge.dodgeDistance);
        float slideDuration = Mathf.Max(0.01f, dodge.slideDuration);

        if (CanUseNetworkDodgeMovement(movement))
        {
            movement.ApplyCombatDodgeSlide(normalizedDirection, dodgeDistance, slideDuration);
            return;
        }

        StartLocalDodgeFallback(normalizedDirection, dodgeDistance, slideDuration);
    }

    private Vector3 ResolveCardinalDirection(Vector2 lookInput)
    {
        Vector3 direction;
        if (Mathf.Abs(lookInput.x) >= Mathf.Abs(lookInput.y))
        {
            direction = lookInput.x >= 0f ? transform.right : -transform.right;
        }
        else
        {
            direction = lookInput.y >= 0f ? transform.forward : -transform.forward;
        }

        if (entity != null && entity.Flight != null && entity.Flight.LockToWorldYPlane)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    private bool IsEmpoweredActive()
    {
        if (dodge.forceEmpowered)
        {
            return true;
        }

        return dodge.empowerAbility != null && dodge.empowerAbility.IsEmpoweredActive;
    }

    private static bool CanUseNetworkDodgeMovement(NetMovement3D movement)
    {
        return movement != null && NetTickUtil.IsActive && movement.IsSpawned;
    }

    private void StartLocalDodgeFallback(Vector3 worldDirection, float dodgeDistance, float slideDuration)
    {
        if (_localDodgeCoroutine != null)
        {
            StopCoroutine(_localDodgeCoroutine);
        }

        _localDodgeCoroutine = StartCoroutine(LocalDodgeSlideCoroutine(worldDirection, dodgeDistance, slideDuration));
    }

    private IEnumerator LocalDodgeSlideCoroutine(Vector3 worldDirection, float dodgeDistance, float slideDuration)
    {
        Rigidbody rb = entity != null && entity.Flight != null ? entity.Flight.Rigidbody : null;
        if (rb == null)
        {
            _localDodgeCoroutine = null;
            yield break;
        }

        Vector3 startPosition = rb.position;
        Vector3 endPosition = startPosition + (worldDirection * dodgeDistance);
        Vector3 carriedVelocity = rb.linearVelocity;
        float elapsed = 0f;

        rb.linearVelocity = Vector3.zero;

        while (elapsed < slideDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = 1f - ((1f - t) * (1f - t));
            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, eased);
            rb.MovePosition(nextPosition);
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPosition);
        rb.linearVelocity = carriedVelocity;
        _localDodgeCoroutine = null;
    }
}
