using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Chrono Step � Hold to plant a waypoint at the press location.
/// Release (or let the hold timeout) to teleport back to that waypoint.
/// On Class5 / any ship with IChargeProvider the ability costs one charge
/// and has no cooldown; all other ships use a standard cooldown.
/// </summary>
public class ChronoStep : Ability
{
    [System.Serializable]
    public struct TeleportAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds) � ignored on charge-based ships")]
        public float cooldown;
        [Tooltip("Maximum hold duration before the waypoint auto-releases (seconds)")]
        public float maxHoldDuration;

        [Header("Animation")]
        public AnimationConfig animation;

        [Header("Visual Effects")]
        public VisualConfig visual;

        [Header("Sound Effects")]
        [Tooltip("Sound played when the waypoint is planted")]
        public SoundEffect waypointSound;
        [Tooltip("Teleport exit sound (at origin)")]
        public SoundEffect exitSound;
        [Tooltip("Teleport arrival sound (at destination)")]
        public SoundEffect arrivalSound;

        [System.Serializable]
        public struct AnimationConfig
        {
            [Tooltip("Shrink duration at origin (seconds)")]
            public float shrinkDuration;
            [Tooltip("Grow duration at destination (seconds)")]
            public float growDuration;
            [Tooltip("Target X scale at origin (squeeze width, e.g. 0.1)")]
            public float originScaleX;
            [Tooltip("Target Y scale at origin (stretch height, e.g. 2.0)")]
            public float originScaleY;
            [Tooltip("Overshoot scale at destination (pop effect, e.g. 1.2)")]
            public float destinationOvershootScale;
            [Tooltip("Normal scale (usually 1.0)")]
            public float normalScale;
        }

        [System.Serializable]
        public struct VisualConfig
        {
            [Tooltip("Enable chromatic aberration flash on teleport")]
            public bool enableChromaticFlash;
            [Tooltip("Chromatic aberration intensity on teleport")]
            [Range(0f, 1f)]
            public float chromaticFlashIntensity;
            [Tooltip("Enable screen shake on teleport")]
            public bool enableScreenShake;
            [Tooltip("Screen shake strength (force)")]
            public float screenShakeStrength;
            [Tooltip("Waypoint marker prefab (spawned at press location, destroyed on teleport)")]
            public GameObject waypointMarkerPrefab;
            [Tooltip("Particle effects at origin and destination")]
            public GameObject[] effects;
        }
    }

    [Header("Ability - Chrono Step")]
    public TeleportAbilityConfig teleport;

    // ===== PRIVATE STATE =====
    private float _lastTeleportTime = -999f;
    private Coroutine _teleportCoroutine;
    private bool _isTeleporting = false;

    // Waypoint state
    private bool _isHolding = false;
    private float _holdStartTime;
    private Vector3 _waypoint;
    private GameObject _waypointMarkerInstance;

    // Charge-based or cooldown-based, resolved once in Awake
    private IChargeProvider _chargeProvider;
    private NetMovement _netMovement;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
        _netMovement = GetComponent<NetMovement>();
    }

    // ===== UPDATE � no hold timeout in toggle mode =====
    protected void Update()
    {
    }

    // ===== ABILITY BASE OVERRIDES =====
    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed) return false;
        if (_isTeleporting) return false;

        // Planting path requires resources; teleporting path does not.
        if (!_isHolding)
        {
            if (_chargeProvider != null)
            {
                if (!_chargeProvider.TrySpendCharges(1))
                {
                    Debug.Log("ChronoStep: no charges available");
                    return false;
                }
            }
            else
            {
                if (Time.time < _lastTeleportTime + teleport.cooldown)
                {
                    Debug.Log($"ChronoStep on cooldown: {(_lastTeleportTime + teleport.cooldown - Time.time):F1}s remaining");
                    return false;
                }
            }
        }

        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed) return;

        if (!_isHolding)
        {
            PlantWaypoint();
        }
        else
        {
            CommitTeleport();
        }
    }

    public override bool IsAbilityActive()
    {
        return _isTeleporting;
    }

    public override bool HasThrustMitigation()
    {
        return _isTeleporting;
    }

    public override void Die()
    {
        CancelWaypoint();
        base.Die();
    }

    // ===== HUD STATE =====
    public override bool IsResourceBased()
    {
        return _chargeProvider != null;
    }

    public override float GetHUDFillRatio()
    {
        if (_chargeProvider != null)
        {
            if (_chargeProvider.MaxCharges <= 0) return 0f;
            return 1f - ((float)_chargeProvider.CurrentCharges / _chargeProvider.MaxCharges);
        }

        if (teleport.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastTeleportTime;
        if (elapsed >= teleport.cooldown) return 0f;
        return 1f - (elapsed / teleport.cooldown);
    }

    public override bool IsOnCooldown()
    {
        if (_chargeProvider != null) return false;
        return Time.time < _lastTeleportTime + teleport.cooldown;
    }

    // ===== WAYPOINT LOGIC =====
    private void PlantWaypoint()
    {
        Vector3 waypoint = transform.position;
        waypoint.z = transform.position.z;

        NetChronoStepState state = new NetChronoStepState
        {
            Action = NetChronoStepAction.Plant,
            Waypoint = new Vector2(waypoint.x, waypoint.y)
        };

        if (ShouldUseNetworkPath())
        {
            // Apply immediately for responsiveness on the owning client.
            if (!_netMovement.IsServer)
            {
                ApplyNetworkChronoStepState(state, authoritative: false);
            }

            _netMovement.RequestChronoStepState(state);
            return;
        }

        ApplyNetworkChronoStepState(state, authoritative: true);
    }

    private void CommitTeleport()
    {
        if (!_isHolding) return;

        Vector3 target = _waypoint;
        NetChronoStepState state = new NetChronoStepState
        {
            Action = NetChronoStepAction.Teleport,
            Waypoint = new Vector2(target.x, target.y)
        };

        if (ShouldUseNetworkPath())
        {
            // Apply immediately on the owning client for responsiveness.
            if (!_netMovement.IsServer)
            {
                ApplyNetworkChronoStepState(state, authoritative: false);
            }

            _netMovement.RequestChronoStepState(state);
            return;
        }

        ApplyNetworkChronoStepState(state, authoritative: true);
    }

    private void CancelWaypoint()
    {
        _isHolding = false;
        ClearWaypointMarker();

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
            _teleportCoroutine = null;
        }

        _isTeleporting = false;
    }

    private void ClearWaypointMarker()
    {
        if (_waypointMarkerInstance != null)
        {
            Destroy(_waypointMarkerInstance);
            _waypointMarkerInstance = null;
        }
    }

    private void OnDisable()
    {
        // Ensure round transitions or disables clear lingering waypoint markers/state
        CancelWaypoint();
    }

    private bool ShouldUseNetworkPath()
    {
        return NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
    }

    public void ApplyNetworkChronoStepState(NetChronoStepState state, bool authoritative)
    {
        switch (state.Action)
        {
            case NetChronoStepAction.Plant:
                ClearWaypointMarker();
                _waypoint = new Vector3(state.Waypoint.x, state.Waypoint.y, transform.position.z);
                _isHolding = true;
                _holdStartTime = Time.time;

                if (teleport.visual.waypointMarkerPrefab != null)
                {
                    _waypointMarkerInstance = Instantiate(teleport.visual.waypointMarkerPrefab, _waypoint, Quaternion.identity);
                }

                if (teleport.waypointSound != null)
                {
                    teleport.waypointSound.Play(player.GetAvailableAudioSource());
                }

                Debug.Log($"ChronoStep: waypoint planted at {_waypoint}");
                break;

            case NetChronoStepAction.Teleport:
                if (!_isHolding)
                {
                    return;
                }

                _isHolding = false;
                ClearWaypointMarker();

                if (_teleportCoroutine != null)
                {
                    StopCoroutine(_teleportCoroutine);
                }

                Vector3 targetPosition = new Vector3(state.Waypoint.x, state.Waypoint.y, transform.position.z);
                _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetPosition));

                if (_chargeProvider == null)
                {
                    _lastTeleportTime = Time.time;
                }
                break;
        }
    }

    // ===== TELEPORT COROUTINE =====
    private System.Collections.IEnumerator ExecuteTeleport(Vector3 targetPosition)
    {
        _isTeleporting = true;

        Vector3 originalScale = transform.localScale;
        Vector3 normalScale = originalScale * teleport.animation.normalScale;

        Vector3 originSqueezeScale = new Vector3(
            originalScale.x * teleport.animation.originScaleX,
            originalScale.y * teleport.animation.originScaleY,
            originalScale.z
        );

        Vector3 destinationPopScale = originalScale * teleport.animation.destinationOvershootScale;

        Collider2D playerCollider = GetComponent<Collider2D>();
        bool colliderWasEnabled = false;
        if (playerCollider != null)
        {
            colliderWasEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        // Shrink at origin
        float elapsed = 0f;
        while (elapsed < teleport.animation.shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleport.animation.shrinkDuration;
            transform.localScale = Vector3.Lerp(originalScale, originSqueezeScale, t);
            yield return null;
        }
        transform.localScale = originSqueezeScale;

        if (teleport.visual.effects != null && teleport.visual.effects.Length > 0 &&
            teleport.visual.effects[0] != null)
        {
            Instantiate(teleport.visual.effects[0], transform.position, Quaternion.identity);
        }

        if (teleport.exitSound != null)
        {
            teleport.exitSound.Play(player.GetAvailableAudioSource());
        }

        // Hide sprite during warp only for the local owner to avoid invisibility on the host
        bool shouldHideSprite = _netMovement == null || _netMovement.IsOwner;
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bool spriteWasEnabled = false;
        if (spriteRenderer != null && shouldHideSprite)
        {
            spriteWasEnabled = spriteRenderer.enabled;
            spriteRenderer.enabled = false;
        }

        // Warp
        Vector3 previousPosition = transform.position;
        transform.position = targetPosition;

        var cinemachineCameras = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cinemachineCameras)
        {
            if (cam.Target.TrackingTarget == transform)
            {
                cam.OnTargetObjectWarped(transform, targetPosition - previousPosition);
            }
        }

        if (teleport.visual.enableChromaticFlash)
        {
            player.SetChromaticAberrationIntensity(player.GetChromaticAberrationIntensity() + teleport.visual.chromaticFlashIntensity);
        }

        if (teleport.visual.enableScreenShake)
        {
            var impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse(teleport.visual.screenShakeStrength);
            }
        }

        if (teleport.visual.effects != null && teleport.visual.effects.Length > 1 &&
            teleport.visual.effects[1] != null)
        {
            Instantiate(teleport.visual.effects[1], transform.position, Quaternion.identity);
        }

        if (teleport.arrivalSound != null)
        {
            teleport.arrivalSound.Play(player.GetAvailableAudioSource());
        }

        if (spriteRenderer != null && spriteWasEnabled)
        {
            spriteRenderer.enabled = true;
        }

        // Grow at destination
        elapsed = 0f;
        while (elapsed < teleport.animation.growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleport.animation.growDuration;
            transform.localScale = Vector3.Lerp(destinationPopScale, normalScale, t);
            yield return null;
        }
        transform.localScale = normalScale;

        if (playerCollider != null && colliderWasEnabled)
        {
            playerCollider.enabled = true;
        }

        _isTeleporting = false;
        _teleportCoroutine = null;
    }
}
