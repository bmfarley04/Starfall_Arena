using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Teleport3D : Ability3D
{
    private const float MinDirectionSqrMagnitude = 0.0001f;

    [System.Serializable]
    public struct TeleportAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds).")]
        public float cooldown;
        [Tooltip("Delay before teleport executes (seconds).")]
        public float preTeleportDelay;
        [Tooltip("Distance to teleport in the ship's planar forward direction.")]
        public float teleportDistance;

        [Header("Animation")]
        public AnimationConfig animation;

        [Header("Visual Effects")]
        public VisualConfig visual;

        [Header("Sound Effects")]
        [Tooltip("Teleport exit sound played at the origin.")]
        public SoundEffect exitSound;
        [Tooltip("Teleport arrival sound played at the destination.")]
        public SoundEffect arrivalSound;

        [System.Serializable]
        public struct AnimationConfig
        {
            [Tooltip("Shrink duration at origin (seconds).")]
            public float shrinkDuration;
            [Tooltip("Grow duration at destination (seconds).")]
            public float growDuration;
            [Tooltip("Target X scale at origin.")]
            public float originScaleX;
            [Tooltip("Target Y scale at origin.")]
            public float originScaleY;
            [Tooltip("Overshoot scale at destination.")]
            public float destinationOvershootScale;
            [Tooltip("Normal scale multiplier after teleport.")]
            public float normalScale;
        }

        [System.Serializable]
        public struct VisualConfig
        {
            [Tooltip("Enable screen shake on teleport.")]
            public bool enableScreenShake;
            [Tooltip("Screen shake strength.")]
            public float screenShakeStrength;
            [Tooltip("Optional origin and destination effects.")]
            public GameObject[] effects;
        }
    }

    [Header("Ability 3 - Teleport 3D")]
    [SerializeField] private TeleportAbilityConfig3D teleport;
    [SerializeField] private AudioSource audioSource;

    private Coroutine _teleportCoroutine;
    private bool _isTeleporting;
    private Rigidbody _rigidbody;
    private CinemachineImpulseSource _impulseSource;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<bool> _rendererStates = new List<bool>();
    private readonly List<Collider> _colliders = new List<Collider>();
    private readonly List<bool> _colliderStates = new List<bool>();
    private Vector3 _cachedScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        _rigidbody = GetComponent<Rigidbody>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        audioSource ??= GetComponent<AudioSource>();
    }

    public override void UseAbility(InputValue value)
    {
        if (_isTeleporting)
        {
            return;
        }

        Vector3 targetPosition = transform.position + ResolveTeleportDirection() * teleport.teleportDistance;

        if (_teleportCoroutine != null)
        {
            RestoreTeleportPresentationState();
            StopCoroutine(_teleportCoroutine);
        }

        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetPosition));
    }

    public override bool IsAbilityActive()
    {
        return _isTeleporting;
    }

    protected override float GetCooldownDuration()
    {
        return teleport.cooldown;
    }

    public override void Die()
    {
        if (_teleportCoroutine != null)
        {
            RestoreTeleportPresentationState();
            StopCoroutine(_teleportCoroutine);
            _teleportCoroutine = null;
        }
    }

    private IEnumerator ExecuteTeleport(Vector3 targetPosition)
    {
        _isTeleporting = true;

        Vector3 originalScale = transform.localScale;
        _cachedScale = originalScale;
        Vector3 normalScale = originalScale * teleport.animation.normalScale;
        Vector3 originSqueezeScale = new Vector3(
            originalScale.x * teleport.animation.originScaleX,
            originalScale.y * teleport.animation.originScaleY,
            originalScale.z);
        Vector3 destinationPopScale = originalScale * teleport.animation.destinationOvershootScale;

        CacheColliderStates();
        SetCollidersEnabled(false);

        if (teleport.preTeleportDelay > 0f)
        {
            yield return new WaitForSeconds(teleport.preTeleportDelay);
        }

        yield return AnimateScale(originalScale, originSqueezeScale, teleport.animation.shrinkDuration);
        SpawnEffect(0, transform.position);
        PlaySound(teleport.exitSound);

        CacheRenderers();
        SetRenderersVisible(false);

        Vector3 previousPosition = transform.position;
        if (_rigidbody != null)
        {
            _rigidbody.position = targetPosition;
        }
        transform.position = targetPosition;

        NotifyWarp(previousPosition, targetPosition);

        if (teleport.visual.enableScreenShake && _impulseSource != null)
        {
            _impulseSource.GenerateImpulse(teleport.visual.screenShakeStrength);
        }

        SpawnEffect(1, transform.position);
        PlaySound(teleport.arrivalSound);

        SetRenderersVisible(true);
        transform.localScale = destinationPopScale;
        yield return AnimateScale(destinationPopScale, normalScale, teleport.animation.growDuration);

        SetCollidersEnabled(true);
        _isTeleporting = false;
        _teleportCoroutine = null;
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        transform.localScale = to;
    }

    private void NotifyWarp(Vector3 previousPosition, Vector3 targetPosition)
    {
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        Vector3 warpDelta = targetPosition - previousPosition;
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera cameraInstance = cameras[i];
            if (cameraInstance != null && cameraInstance.Target.TrackingTarget == transform)
            {
                cameraInstance.OnTargetObjectWarped(transform, warpDelta);
            }
        }
    }

    private Vector3 ResolveTeleportDirection()
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (planarForward.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return planarForward.normalized;
        }

        if (transform.forward.sqrMagnitude > MinDirectionSqrMagnitude)
        {
            return transform.forward.normalized;
        }

        return Vector3.forward;
    }

    private void SpawnEffect(int index, Vector3 position)
    {
        if (teleport.visual.effects == null || index < 0 || index >= teleport.visual.effects.Length)
        {
            return;
        }

        GameObject effectPrefab = teleport.visual.effects[index];
        if (effectPrefab == null)
        {
            return;
        }

        Instantiate(effectPrefab, position, Quaternion.identity);
    }

    private void PlaySound(SoundEffect soundEffect)
    {
        if (soundEffect == null)
        {
            return;
        }

        if (audioSource != null)
        {
            soundEffect.Play(audioSource);
            return;
        }

        soundEffect.PlayAtPoint(transform.position);
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        _rendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            _renderers.Add(renderer);
            _rendererStates.Add(renderer.enabled);
        }
    }

    private void SetRenderersVisible(bool isVisible)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!isVisible)
            {
                _rendererStates[i] = renderer.enabled;
                renderer.enabled = false;
            }
            else
            {
                renderer.enabled = i < _rendererStates.Count && _rendererStates[i];
            }
        }
    }

    private void CacheColliderStates()
    {
        _colliders.Clear();
        _colliderStates.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            _colliders.Add(collider);
            _colliderStates.Add(collider.enabled);
        }
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        for (int i = 0; i < _colliders.Count; i++)
        {
            Collider collider = _colliders[i];
            if (collider == null)
            {
                continue;
            }

            collider.enabled = isEnabled && i < _colliderStates.Count && _colliderStates[i];
        }
    }

    private void RestoreTeleportPresentationState()
    {
        if (!_isTeleporting && _teleportCoroutine == null)
        {
            return;
        }

        SetRenderersVisible(true);
        SetCollidersEnabled(true);
        transform.localScale = _cachedScale * teleport.animation.normalScale;
        _isTeleporting = false;
    }

    private void OnDisable()
    {
        RestoreTeleportPresentationState();
    }
}
