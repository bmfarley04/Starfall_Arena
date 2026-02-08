using Unity.VisualScripting;
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

    protected override void Awake()
    {
        base.Awake();

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
            Debug.Log($"Teleport on cooldown: {(_lastTeleportTime + teleport.cooldown - Time.time):F1}s remaining");
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

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
        }
        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetWorldPosition));
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

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bool spriteWasEnabled = false;
        if (spriteRenderer != null)
        {
            spriteWasEnabled = spriteRenderer.enabled;
            spriteRenderer.enabled = false;
        }

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
    }
}
