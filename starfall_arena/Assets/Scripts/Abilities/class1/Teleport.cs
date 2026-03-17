using UnityEngine;
using UnityEngine.InputSystem;

public class Teleport : Ability
{
    [System.Serializable]
    public struct TeleportAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Delay before teleport executes (seconds)")]
        public float preTeleportDelay;
        [Tooltip("Distance to teleport in the direction player is facing")]
        public float teleportDistance;

        [Header("Animation")]
        public AnimationConfig animation;

        [Header("Visual Effects")]
        public VisualConfig visual;

        [Header("Sound Effects")]
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
            [Tooltip("Particle effects at origin and destination")]
            public GameObject[] effects;
        }
    }

    [Header("Ability 3 - Teleport")]
    public TeleportAbilityConfig teleport;


    // ===== PRIVATE STATE =====
    private float _lastTeleportTime = -999f;
    private Coroutine _teleportCoroutine;
    private bool _isTeleporting = false;
    private NetMovement _netMovement;
    private readonly System.Collections.Generic.List<Renderer> _teleportRenderers = new System.Collections.Generic.List<Renderer>();
    private readonly System.Collections.Generic.List<bool> _teleportRendererStates = new System.Collections.Generic.List<bool>();

    // ===== HUD STATE =====
    public override float GetHUDFillRatio()
    {
        if (teleport.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastTeleportTime;
        if (elapsed >= teleport.cooldown) return 0f;
        return 1f - (elapsed / teleport.cooldown);
    }
    public override bool IsOnCooldown()
    {
        return Time.time < _lastTeleportTime + teleport.cooldown;
    }

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
    }

    protected void Update()
    {

    }

    void FixedUpdate()
    {

    }
    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);
        if (Time.time < _lastTeleportTime + teleport.cooldown)
        {
            return;
        }

        if (_isTeleporting)
        {
            return;
        }

        Vector3 teleportDirection = transform.up;
        Vector3 targetWorldPosition = transform.position + teleportDirection * teleport.teleportDistance;
        targetWorldPosition.z = transform.position.z;

        _lastTeleportTime = Time.time;

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkTeleport(targetWorldPosition, authoritative: false);
            }

            _netMovement.RequestTeleport(targetWorldPosition);
            return;
        }

        ApplyNetworkTeleport(targetWorldPosition, authoritative: true);
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


        base.Die();
    }

    // ===== COROUTINES =====
    public void ApplyNetworkTeleport(Vector2 targetPosition, bool authoritative)
    {
        if (_teleportCoroutine != null)
        {
            RestoreTeleportPresentationState();
            StopCoroutine(_teleportCoroutine);
        }

        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetPosition, authoritative));
    }

    private System.Collections.IEnumerator ExecuteTeleport(Vector3 targetPosition, bool authoritative)
    {
        _isTeleporting = true;
        bool shouldHideRenderersForThisInstance = _netMovement == null || _netMovement.IsOwner || authoritative;

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

        if (teleport.preTeleportDelay > 0)
        {
            yield return new WaitForSeconds(teleport.preTeleportDelay);
        }

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

        if (shouldHideRenderersForThisInstance)
        {
            CacheTeleportRenderers();
            SetTeleportRenderersVisible(false);
        }

        Vector3 previousPosition = transform.position;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = targetPosition;
        }
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

        if (shouldHideRenderersForThisInstance)
        {
            SetTeleportRenderersVisible(true);
        }

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

    private void CacheTeleportRenderers()
    {
        _teleportRenderers.Clear();
        _teleportRendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            _teleportRenderers.Add(renderer);
            _teleportRendererStates.Add(renderer.enabled);
        }
    }

    private void SetTeleportRenderersVisible(bool isVisible)
    {
        for (int i = 0; i < _teleportRenderers.Count; i++)
        {
            Renderer renderer = _teleportRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!isVisible)
            {
                _teleportRendererStates[i] = renderer.enabled;
                renderer.enabled = false;
            }
            else
            {
                renderer.enabled = i < _teleportRendererStates.Count && _teleportRendererStates[i];
            }
        }
    }

    private void RestoreTeleportPresentationState()
    {
        SetTeleportRenderersVisible(true);
        _isTeleporting = false;
        transform.localScale = Vector3.one * teleport.animation.normalScale;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        RestoreTeleportPresentationState();
    }
}
