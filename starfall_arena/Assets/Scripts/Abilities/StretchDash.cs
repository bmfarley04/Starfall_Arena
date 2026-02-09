using UnityEngine;
using UnityEngine.InputSystem;

public class StretchDash : Ability
{
    [System.Serializable]
    public struct StretchDashAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Minimum hold time before dash can be released (seconds)")]
        public float minChargeTime;
        [Tooltip("Maximum hold time (seconds) - dash fires automatically at this point")]
        public float maxChargeTime;

        [Header("Stretch Animation")]
        [Tooltip("How fast the ship stretches while charging (scale units per second)")]
        public float stretchSpeed;
        [Tooltip("Maximum stretch length multiplier (e.g., 3.0 = ship becomes 3x longer)")]
        public float maxStretchMultiplier;
        [Tooltip("How fast the ship returns to normal after dash (scale units per second)")]
        public float returnSpeed;

        [Header("Dash Movement")]
        [Tooltip("Dash force multiplier based on charge time")]
        public float dashForceMultiplier;
        [Tooltip("Minimum dash force (when released at minChargeTime)")]
        public float minDashForce;
        [Tooltip("Maximum dash force (when released at maxChargeTime)")]
        public float maxDashForce;

        [Header("Movement Penalties While Charging")]
        [Tooltip("Thrust speed multiplier while charging")]
        [Range(0f, 1f)]
        public float thrustMultiplier;
        [Tooltip("Rotation speed multiplier while charging")]
        [Range(0f, 1f)]
        public float rotationMultiplier;

        [Header("Physics")]
        [Tooltip("Velocity multiplier after dash (0 = full stop, 1 = maintain speed)")]
        [Range(0f, 1f)]
        public float velocityRetention;

        [Header("Visual Effects")]
        [Tooltip("Particle effect spawned at origin on dash")]
        public GameObject dashOriginEffect;
        [Tooltip("Particle effect spawned at destination on dash")]
        public GameObject dashDestinationEffect;
        [Tooltip("Enable chromatic aberration flash on dash")]
        public bool enableChromaticFlash;
        [Tooltip("Chromatic aberration intensity on dash")]
        [Range(0f, 1f)]
        public float chromaticFlashIntensity;
        [Tooltip("Enable screen shake on dash")]
        public bool enableScreenShake;
        [Tooltip("Screen shake strength (force)")]
        public float screenShakeStrength;

        [Header("Sound Effects")]
        [Tooltip("Charge loop sound (plays while charging)")]
        public SoundEffect chargeLoopSound;
        [Tooltip("Fade in duration for charge sound (seconds)")]
        public float chargeSoundFadeDuration;
        [Tooltip("Dash release sound (at release)")]
        public SoundEffect dashSound;
    }

    [Header("Ability 3 - Stretch Dash")]
    public StretchDashAbilityConfig stretchDash;

    // ===== PRIVATE STATE =====
    private float _lastDashTime = -999f;
    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private float _currentStretchMultiplier = 1f;
    private Vector3 _originalScale;
    private Vector3 _chargeDirection;
    private bool _isDashing = false;
    private Coroutine _dashCoroutine;
    private AudioSource _chargeLoopSource;
    private Coroutine _chargeFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _originalScale = transform.localScale;

        // Create dedicated audio source for charge loop
        _chargeLoopSource = gameObject.AddComponent<AudioSource>();
        _chargeLoopSource.playOnAwake = false;
        _chargeLoopSource.loop = true;
        _chargeLoopSource.spatialBlend = 1f;
        _chargeLoopSource.rolloffMode = AudioRolloffMode.Linear;
        _chargeLoopSource.minDistance = 10f;
        _chargeLoopSource.maxDistance = 50f;
        _chargeLoopSource.dopplerLevel = 0f;
    }

    protected void Update()
    {
        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;

            // Stretch the ship while charging (stretches from back, fixed at front)
            float targetStretch = Mathf.Lerp(1f, stretchDash.maxStretchMultiplier, 
                chargeTime / stretchDash.maxChargeTime);
            _currentStretchMultiplier = Mathf.MoveTowards(_currentStretchMultiplier, targetStretch, 
                stretchDash.stretchSpeed * Time.deltaTime);

            // Apply stretch along the ship's forward direction (transform.up)
            // The ship stretches backwards while the front stays in place
            Vector3 stretchScale = _originalScale;
            stretchScale.y *= _currentStretchMultiplier;
            transform.localScale = stretchScale;

            // Auto-release at max charge time
            if (chargeTime >= stretchDash.maxChargeTime)
            {
                ReleaseDash();
            }
        }
        else if (_isDashing)
        {
            // Return to normal scale after dash
            if (_currentStretchMultiplier > 1.01f)
            {
                _currentStretchMultiplier = Mathf.MoveTowards(_currentStretchMultiplier, 1f, 
                    stretchDash.returnSpeed * Time.deltaTime);

                Vector3 stretchScale = _originalScale;
                stretchScale.y *= _currentStretchMultiplier;
                transform.localScale = stretchScale;
            }
            else
            {
                transform.localScale = _originalScale;
                _isDashing = false;
            }
        }
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        if (value.isPressed)
        {
            // Start charging
            if (!_isCharging && !_isDashing && Time.time >= _lastDashTime + stretchDash.cooldown)
            {
                _isCharging = true;
                _chargeStartTime = Time.time;
                _chargeDirection = transform.up;
                _currentStretchMultiplier = 1f;

                // Start charge sound with fade in
                if (stretchDash.chargeLoopSound != null && _chargeLoopSource != null)
                {
                    _chargeLoopSource.volume = 0f;
                    stretchDash.chargeLoopSound.Play(_chargeLoopSource);

                    if (_chargeFadeCoroutine != null)
                    {
                        StopCoroutine(_chargeFadeCoroutine);
                    }
                    _chargeFadeCoroutine = StartCoroutine(FadeChargeVolume(stretchDash.chargeLoopSound.volume));
                }

                Debug.Log("Stretch Dash charging started");
            }
        }
        else
        {
            // Release dash
            if (_isCharging)
            {
                ReleaseDash();
            }
        }
    }

    private void ReleaseDash()
    {
        float chargeTime = Time.time - _chargeStartTime;

        // Only dash if minimum charge time met
        if (chargeTime >= stretchDash.minChargeTime)
        {
            ExecuteDash(chargeTime);
            _lastDashTime = Time.time;
        }
        else
        {
            Debug.Log($"Stretch Dash released too early: {chargeTime:F2}s < {stretchDash.minChargeTime:F2}s");
        }

        _isCharging = false;

        // Stop charge sound with fade out
        if (_chargeLoopSource != null && _chargeLoopSource.isPlaying)
        {
            if (_chargeFadeCoroutine != null)
            {
                StopCoroutine(_chargeFadeCoroutine);
            }
            _chargeFadeCoroutine = StartCoroutine(FadeChargeVolume(0f, stopAfterFade: true));
        }
    }

    private void ExecuteDash(float chargeTime)
    {
        _isDashing = true;

        // Calculate dash force based on charge time
        float chargeRatio = Mathf.Clamp01(chargeTime / stretchDash.maxChargeTime);
        float dashForce = Mathf.Lerp(stretchDash.minDashForce, stretchDash.maxDashForce, chargeRatio);

        // Apply dash force in the direction ship was facing when charge started
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Reduce current velocity
            rb.linearVelocity *= stretchDash.velocityRetention;
            // Apply dash impulse
            rb.AddForce(_chargeDirection * dashForce, ForceMode2D.Impulse);
        }

        // Visual effects at origin
        if (stretchDash.dashOriginEffect != null)
        {
            Instantiate(stretchDash.dashOriginEffect, transform.position, Quaternion.identity);
        }

        // Dash sound
        if (stretchDash.dashSound != null)
        {
            stretchDash.dashSound.Play(player.GetAvailableAudioSource());
        }

        // Chromatic aberration flash
        if (stretchDash.enableChromaticFlash)
        {
            player.SetChromaticAberrationIntensity(
                player.GetChromaticAberrationIntensity() + stretchDash.chromaticFlashIntensity);
        }

        // Screen shake
        if (stretchDash.enableScreenShake)
        {
            var impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(stretchDash.screenShakeStrength);
            }
        }

        Debug.Log($"Stretch Dash executed! Charge: {chargeTime:F2}s, Force: {dashForce:F1}");
    }

    public override bool IsAbilityActive()
    {
        return _isCharging || _isDashing;
    }

    public override bool HasThrustMitigation()
    {
        return _isCharging || _isDashing;
    }

    public override void ApplyThrustMultiplier()
    {
        base.ApplyThrustMultiplier();
        if (_isCharging)
        {
            player.movement.thrustForce *= stretchDash.thrustMultiplier;
        }
    }

    public override void RestoreThrustMultiplier()
    {
        base.RestoreThrustMultiplier();
        // Thrust force is already restored by player before this is called
    }

    public override void ApplyRotationMultiplier()
    {
        base.ApplyRotationMultiplier();
        if (_isCharging)
        {
            player.movement.rotationSpeed *= stretchDash.rotationMultiplier;
        }
    }

    public override void Die()
    {
        // Stop charge sound if playing
        if (_chargeLoopSource != null && _chargeLoopSource.isPlaying)
        {
            _chargeLoopSource.Stop();
        }

        // Stop any active fade coroutine
        if (_chargeFadeCoroutine != null)
        {
            StopCoroutine(_chargeFadeCoroutine);
        }

        // Reset scale to original
        if (_isCharging || _isDashing)
        {
            transform.localScale = _originalScale;
        }

        base.Die();
    }

    private System.Collections.IEnumerator FadeChargeVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_chargeLoopSource == null) yield break;

        float startVolume = _chargeLoopSource.volume;
        float elapsed = 0f;

        while (elapsed < stretchDash.chargeSoundFadeDuration)
        {
            if (_chargeLoopSource == null || (!_chargeLoopSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / stretchDash.chargeSoundFadeDuration;
            _chargeLoopSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_chargeLoopSource == null) yield break;

        _chargeLoopSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && _chargeLoopSource.isPlaying)
        {
            _chargeLoopSource.Stop();
        }
    }
}
