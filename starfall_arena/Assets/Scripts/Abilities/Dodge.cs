using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct DodgeAbilityConfig
{
    [Header("Dodge")]
    [Tooltip("Fixed distance the ship slides during dodge")]
    public float dodgeDistance;
    [Tooltip("How long the slide takes (seconds) - lower = more teleport-like")]
    public float slideDuration;
    [Tooltip("How long the dodge stays primed waiting for stick input (seconds)")]
    public float primeWindow;

    [Header("Empowerment")]
    [Tooltip("Reference to this ship's Empower ability. If null, auto-finds on this GameObject.")]
    public Empower empowerAbility;
    [Tooltip("If true, always uses empowered cooldown (debug/testing).")]
    public bool forceEmpowered;
    [Tooltip("Cooldown when empowered (seconds) - uses stats.cooldown for base")]
    public float empoweredCooldown;

    [Header("Sound Effects")]
    public SoundEffect primeSound;
    public SoundEffect dodgeSound;
}

public class Dodge : Ability
{
    [Header("Dodge")]
    public DodgeAbilityConfig dodge;

    private bool _isPrimed;
    private bool _isSliding;
    private float _primeStartTime;
    private float _lastDodgeTime = -999f;
    private Coroutine _slideCoroutine;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        if (dodge.empowerAbility == null)
        {
            dodge.empowerAbility = GetComponent<Empower>();
        }
        _netMovement = GetComponent<NetMovement>();
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed) return;

        _isPrimed = true;
        _primeStartTime = Time.time;

        if (dodge.primeSound != null)
        {
            dodge.primeSound.Play(player.GetAvailableAudioSource());
        }
    }

    private void Update()
    {
        if (!_isPrimed) return;

        if (Time.time > _primeStartTime + dodge.primeWindow)
        {
            _isPrimed = false;
            return;
        }

        Vector2 stickInput = player.LookInput;

        if (stickInput.magnitude > player.input.controllerLookDeadzone)
        {
            ExecuteDodge(stickInput.normalized);
        }
    }

    private void ExecuteDodge(Vector2 direction)
    {
        _isPrimed = false;
        _lastDodgeTime = Time.time;

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;

        if (useNetworkPath)
        {
            // Owner predicts the slide locally for responsiveness; server runs
            // the authoritative slide and broadcasts to remote clients.
            if (!_netMovement.IsServer)
            {
                ApplyNetworkDodge(direction, authoritative: false);
            }
            _netMovement.RequestDodge(direction);
            return;
        }

        ApplyNetworkDodge(direction, authoritative: true);
    }

    public void ApplyNetworkDodge(Vector2 direction, bool authoritative)
    {
        if (dodge.dodgeSound != null)
        {
            dodge.dodgeSound.Play(player.GetAvailableAudioSource());
        }

        if (_slideCoroutine != null)
        {
            StopCoroutine(_slideCoroutine);
        }
        _slideCoroutine = StartCoroutine(SlideCoroutine(direction));
    }

    private System.Collections.IEnumerator SlideCoroutine(Vector2 direction)
    {
        _isSliding = true;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector2 savedVelocity = rb.linearVelocity;
        rb.linearVelocity = Vector2.zero;

        Vector2 startPos = rb.position;
        Vector2 endPos = startPos + direction * dodge.dodgeDistance;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, dodge.slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);

            rb.MovePosition(Vector2.Lerp(startPos, endPos, eased));

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPos);
        rb.linearVelocity = direction * savedVelocity.magnitude;

        _isSliding = false;
    }

    // Lock rotation during the slide
    public override void ApplyRotationMultiplier()
    {
        if (_isSliding)
        {
            player.movement.rotationSpeed = 0f;
        }
    }

    // Prevent thrust during the slide
    public override bool HasThrustMitigation()
    {
        return _isSliding;
    }

    private float ActiveCooldown => IsEmpoweredActive() ? dodge.empoweredCooldown : stats.cooldown;

    private bool IsEmpoweredActive()
    {
        if (dodge.forceEmpowered)
        {
            return true;
        }

        return dodge.empowerAbility != null && dodge.empowerAbility.IsEmpoweredActive;
    }

    public override bool IsAbilityActive()
    {
        return _isPrimed || _isSliding;
    }

    public override float GetHUDFillRatio()
    {
        float cd = ActiveCooldown;
        if (cd <= 0f) return 0f;
        float elapsed = Time.time - _lastDodgeTime;
        if (elapsed >= cd) return 0f;
        return 1f - (elapsed / cd);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastDodgeTime + ActiveCooldown;
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed) return false;
        if (isLocked || isDisabledByOtherAbility) return false;
        if (IsOnCooldown()) return false;

        UseAbility(value);
        return true;
    }
}
