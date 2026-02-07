using System.Collections;
using UnityEngine;

// ===== PREVIEW CONFIGURATION STRUCTS =====

[System.Serializable]
public struct BeamPreviewConfig
{
    [Header("Beam")]
    [Tooltip("Beam prefab to instantiate (same as gameplay beam)")]
    public GameObject beamPrefab;

    [Tooltip("Offset distance from ship center to spawn beam")]
    public float offsetDistance;

    [Tooltip("Maximum beam distance for preview")]
    public float maxDistance;

    [Header("Timing")]
    [Tooltip("How long the beam fires each loop (seconds)")]
    public float activeDuration;

    [Tooltip("Pause between beam loops (seconds)")]
    public float pauseDuration;

    [Header("Sound")]
    public SoundEffect beamLoopSound;

    [Tooltip("Fade in/out duration for beam sound (seconds)")]
    public float soundFadeDuration;
}

[System.Serializable]
public struct ReflectPreviewConfig
{
    [Header("Shield")]
    [Tooltip("ReflectShield child component on the ship model")]
    public ReflectShield shield;

    [Tooltip("Color for the reflect shield preview")]
    public Color shieldColor;

    [Header("Timing")]
    [Tooltip("How long the shield stays active each loop (seconds)")]
    public float activeDuration;

    [Tooltip("Interval between synthetic ripple hits (seconds)")]
    public float rippleInterval;

    [Tooltip("Number of ripples to fire per activation")]
    public int rippleCount;

    [Tooltip("Pause between shield loops (seconds)")]
    public float pauseDuration;

    [Header("Sound")]
    public SoundEffect shieldLoopSound;
}

[System.Serializable]
public struct TeleportPreviewConfig
{
    [Header("Animation")]
    [Tooltip("Duration of shrink animation (seconds)")]
    public float shrinkDuration;

    [Tooltip("Duration of grow animation (seconds)")]
    public float growDuration;

    [Tooltip("Target X scale when squeezed (e.g. 0.1)")]
    public float squeezeScaleX;

    [Tooltip("Target Y scale when squeezed (e.g. 2.0)")]
    public float squeezeScaleY;

    [Tooltip("Overshoot scale at arrival (e.g. 1.2)")]
    public float overshootScale;

    [Header("Timing")]
    [Tooltip("How long ship stays invisible between teleport out/in (seconds)")]
    public float invisibleDuration;

    [Tooltip("Pause after grow animation completes before looping (seconds)")]
    public float pauseDuration;

    [Header("Effects")]
    [Tooltip("Particle effect spawned at exit")]
    public GameObject exitEffectPrefab;

    [Tooltip("Particle effect spawned at arrival")]
    public GameObject arrivalEffectPrefab;

    [Header("Sound")]
    public SoundEffect exitSound;
    public SoundEffect arrivalSound;
}

[System.Serializable]
public struct GigaBlastPreviewConfig
{
    [Header("Charge Particles (children of ship)")]
    [Tooltip("Tier 1 charge particle system")]
    public ParticleSystem tier1Particles;

    [Tooltip("Tier 2 charge particle system")]
    public ParticleSystem tier2Particles;

    [Tooltip("Tier 3 charge particle system")]
    public ParticleSystem tier3Particles;

    [Tooltip("Tier 4 charge particle system")]
    public ParticleSystem tier4Particles;

    [Header("Projectile")]
    [Tooltip("Tier 4 projectile prefab to fire at end of charge")]
    public GameObject fireProjectilePrefab;

    [Tooltip("Offset distance from ship center to spawn projectile")]
    public float offsetDistance;

    [Tooltip("Projectile speed")]
    public float projectileSpeed;

    [Tooltip("Projectile lifetime (seconds)")]
    public float projectileLifetime;

    [Header("Timing")]
    [Tooltip("Duration to display each charge tier (seconds)")]
    public float tierDuration;

    [Tooltip("Pause after firing before looping (seconds)")]
    public float pauseAfterFire;

    [Header("Sound")]
    public SoundEffect chargeSound;

    [Tooltip("Fade in duration for charge sound (seconds)")]
    public float chargeSoundFadeDuration;

    public SoundEffect fireSound;
}

[System.Serializable]
public struct PreviewRotationConfig
{
    [Tooltip("Speed to slerp toward flat 2D rotation when locked")]
    public float lockSmoothing;

    [Tooltip("Speed to slerp back to free rotation when unlocked (not used directly, but available)")]
    public float unlockSmoothing;
}

// ===== CLASS1 PREVIEW CONTROLLER =====

/// <summary>
/// Standalone preview controller for Class1 ship in the ship select screen.
/// Lives on the ship model prefab, independent of disabled Entity/Player/Class1 scripts.
/// Exposes StartPreview(int)/StopPreview() API for ShipSelectManager to call on hover changes.
/// </summary>
public class Class1PreviewController : MonoBehaviour
{
    [Header("Ability Previews")]
    [SerializeField] private BeamPreviewConfig beamPreview;
    [SerializeField] private ReflectPreviewConfig reflectPreview;
    [SerializeField] private TeleportPreviewConfig teleportPreview;
    [SerializeField] private GigaBlastPreviewConfig gigaBlastPreview;

    [Header("Rotation")]
    [SerializeField] private PreviewRotationConfig rotation;

    // Public state for ShipSelectManager to read
    public bool IsRotationLocked { get; private set; }

    // Private state
    private Coroutine _activePreviewCoroutine;
    private int _currentAbilityIndex = -1;
    private AudioSource _loopAudioSource;
    private AudioSource _oneShotAudioSource;

    // Cleanup tracking
    private GameObject _activeBeamInstance;
    private bool _renderersHidden;
    private Vector3 _originalScale;
    private Renderer[] _cachedRenderers;
    private Coroutine _soundFadeCoroutine;

    private void Awake()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>();
        _originalScale = transform.localScale;

        // Create 2 AudioSources for menu (2D audio, spatialBlend=0)
        _loopAudioSource = gameObject.AddComponent<AudioSource>();
        _loopAudioSource.playOnAwake = false;
        _loopAudioSource.loop = true;
        _loopAudioSource.spatialBlend = 0f;

        _oneShotAudioSource = gameObject.AddComponent<AudioSource>();
        _oneShotAudioSource.playOnAwake = false;
        _oneShotAudioSource.loop = false;
        _oneShotAudioSource.spatialBlend = 0f;
    }

    private void LateUpdate()
    {
        if (IsRotationLocked)
        {
            // Slerp toward flat 2D view: X=0, Y=0, preserve current Z
            float currentZ = transform.rotation.eulerAngles.z;
            Quaternion flatTarget = Quaternion.Euler(0f, 0f, currentZ);
            transform.rotation = Quaternion.Slerp(transform.rotation, flatTarget, rotation.lockSmoothing * Time.unscaledDeltaTime);
        }
    }

    // ===== PUBLIC API =====

    /// <summary>
    /// Start previewing a specific ability. Stops any active preview first.
    /// </summary>
    /// <param name="abilityIndex">0-3 for abilities 1-4</param>
    public void StartPreview(int abilityIndex)
    {
        // If already previewing this ability, do nothing
        if (_currentAbilityIndex == abilityIndex && _activePreviewCoroutine != null)
            return;

        // Stop any active preview
        StopPreviewInternal();

        _currentAbilityIndex = abilityIndex;
        IsRotationLocked = true;

        // Cache original scale in case it was changed
        _originalScale = transform.localScale;

        _activePreviewCoroutine = abilityIndex switch
        {
            0 => StartCoroutine(BeamPreviewLoop()),
            1 => StartCoroutine(ReflectPreviewLoop()),
            2 => StartCoroutine(TeleportPreviewLoop()),
            3 => StartCoroutine(GigaBlastPreviewLoop()),
            _ => null
        };
    }

    /// <summary>
    /// Stop any active preview and unlock rotation.
    /// </summary>
    public void StopPreview()
    {
        StopPreviewInternal();
        IsRotationLocked = false;
    }

    private void StopPreviewInternal()
    {
        if (_activePreviewCoroutine != null)
        {
            StopCoroutine(_activePreviewCoroutine);
            _activePreviewCoroutine = null;
        }

        if (_soundFadeCoroutine != null)
        {
            StopCoroutine(_soundFadeCoroutine);
            _soundFadeCoroutine = null;
        }

        CleanupCurrentPreview();
        _currentAbilityIndex = -1;
    }

    // ===== CLEANUP =====

    /// <summary>
    /// Synchronous cleanup that handles any mid-animation state for all abilities.
    /// </summary>
    private void CleanupCurrentPreview()
    {
        // Beam cleanup
        if (_activeBeamInstance != null)
        {
            LaserBeam beam = _activeBeamInstance.GetComponent<LaserBeam>();
            if (beam != null)
                beam.StopFiring();
            Destroy(_activeBeamInstance);
            _activeBeamInstance = null;
        }

        // Reflect cleanup
        if (reflectPreview.shield != null)
        {
            if (reflectPreview.shield.IsActive())
                reflectPreview.shield.Deactivate();
            reflectPreview.shield.enabled = false;
        }

        // GigaBlast cleanup - stop all charge particles
        StopAllChargeParticles();

        // Restore renderers if hidden
        if (_renderersHidden)
        {
            SetRenderersVisible(true);
            _renderersHidden = false;
        }

        // Restore original scale
        transform.localScale = _originalScale;

        // Stop both audio sources
        if (_loopAudioSource != null)
        {
            _loopAudioSource.Stop();
            _loopAudioSource.volume = 0f;
        }

        if (_oneShotAudioSource != null)
        {
            _oneShotAudioSource.Stop();
        }
    }

    // ===== BEAM PREVIEW =====

    private IEnumerator BeamPreviewLoop()
    {
        while (true)
        {
            // Instantiate beam as child
            if (beamPreview.beamPrefab != null)
            {
                Vector3 spawnPos = transform.position + transform.up * beamPreview.offsetDistance;
                _activeBeamInstance = Instantiate(beamPreview.beamPrefab, spawnPos, transform.rotation, transform);

                LaserBeam beam = _activeBeamInstance.GetComponent<LaserBeam>();
                if (beam != null)
                {
                    // Initialize with no damage, no recoil, no shooter (preview only)
                    beam.Initialize("", 0f, beamPreview.maxDistance, 0f, 0f, null);
                    beam.StartFiring();
                }

                // Fade in loop sound
                if (beamPreview.beamLoopSound != null && _loopAudioSource != null)
                {
                    _loopAudioSource.volume = 0f;
                    beamPreview.beamLoopSound.Play(_loopAudioSource);
                    _soundFadeCoroutine = StartCoroutine(FadeAudioSource(_loopAudioSource, beamPreview.beamLoopSound.volume, beamPreview.soundFadeDuration));
                }
            }

            // Wait active duration
            yield return new WaitForSecondsRealtime(beamPreview.activeDuration);

            // Stop beam
            if (_activeBeamInstance != null)
            {
                LaserBeam beam = _activeBeamInstance.GetComponent<LaserBeam>();
                if (beam != null)
                    beam.StopFiring();

                // Fade out sound
                if (_loopAudioSource != null && _loopAudioSource.isPlaying)
                {
                    if (_soundFadeCoroutine != null)
                        StopCoroutine(_soundFadeCoroutine);
                    _soundFadeCoroutine = StartCoroutine(FadeAudioSource(_loopAudioSource, 0f, beamPreview.soundFadeDuration, stopAfterFade: true));
                }

                Destroy(_activeBeamInstance);
                _activeBeamInstance = null;
            }

            // Pause
            yield return new WaitForSecondsRealtime(beamPreview.pauseDuration);
        }
    }

    // ===== REFLECT PREVIEW =====

    private IEnumerator ReflectPreviewLoop()
    {
        while (true)
        {
            if (reflectPreview.shield != null)
            {
                // Enable the component so its Update() runs fade animations
                reflectPreview.shield.enabled = true;
                reflectPreview.shield.Activate(reflectPreview.shieldColor);

                // Play shield loop sound
                if (reflectPreview.shieldLoopSound != null && _loopAudioSource != null)
                {
                    reflectPreview.shieldLoopSound.Play(_loopAudioSource);
                }

                // Fire synthetic ripples at intervals
                float elapsed = 0f;
                int ripplesFired = 0;
                while (elapsed < reflectPreview.activeDuration)
                {
                    if (ripplesFired < reflectPreview.rippleCount &&
                        elapsed >= ripplesFired * reflectPreview.rippleInterval)
                    {
                        // Random hit point on the shield surface
                        Vector3 randomOffset = new Vector3(
                            Random.Range(-0.5f, 0.5f),
                            Random.Range(-0.5f, 0.5f),
                            0f
                        );
                        Vector3 hitPoint = reflectPreview.shield.transform.position + randomOffset;
                        reflectPreview.shield.OnReflectHit(hitPoint);
                        ripplesFired++;
                    }

                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }

                // Deactivate shield
                reflectPreview.shield.Deactivate();

                if (_loopAudioSource != null && _loopAudioSource.isPlaying)
                {
                    _loopAudioSource.Stop();
                }
            }

            // Pause
            yield return new WaitForSecondsRealtime(reflectPreview.pauseDuration);
        }
    }

    // ===== TELEPORT PREVIEW =====

    private IEnumerator TeleportPreviewLoop()
    {
        while (true)
        {
            Vector3 normalScale = _originalScale;
            Vector3 squeezeScale = new Vector3(
                _originalScale.x * teleportPreview.squeezeScaleX,
                _originalScale.y * teleportPreview.squeezeScaleY,
                _originalScale.z
            );
            Vector3 overshootScale = _originalScale * teleportPreview.overshootScale;

            // Shrink animation
            float elapsed = 0f;
            while (elapsed < teleportPreview.shrinkDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / teleportPreview.shrinkDuration);
                transform.localScale = Vector3.Lerp(normalScale, squeezeScale, t);
                yield return null;
            }
            transform.localScale = squeezeScale;

            // Play exit sound and spawn exit effect
            if (teleportPreview.exitSound != null && _oneShotAudioSource != null)
                teleportPreview.exitSound.Play(_oneShotAudioSource);

            if (teleportPreview.exitEffectPrefab != null)
                Instantiate(teleportPreview.exitEffectPrefab, transform.position, Quaternion.identity);

            // Hide renderers (no position change - stays at display position)
            SetRenderersVisible(false);
            _renderersHidden = true;

            // Invisible duration
            yield return new WaitForSecondsRealtime(teleportPreview.invisibleDuration);

            // Show renderers, spawn arrival effect
            SetRenderersVisible(true);
            _renderersHidden = false;

            if (teleportPreview.arrivalEffectPrefab != null)
                Instantiate(teleportPreview.arrivalEffectPrefab, transform.position, Quaternion.identity);

            if (teleportPreview.arrivalSound != null && _oneShotAudioSource != null)
                teleportPreview.arrivalSound.Play(_oneShotAudioSource);

            // Grow animation (overshoot to normal)
            elapsed = 0f;
            while (elapsed < teleportPreview.growDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / teleportPreview.growDuration);
                transform.localScale = Vector3.Lerp(overshootScale, normalScale, t);
                yield return null;
            }
            transform.localScale = normalScale;

            // Pause
            yield return new WaitForSecondsRealtime(teleportPreview.pauseDuration);
        }
    }

    // ===== GIGABLAST PREVIEW =====

    private IEnumerator GigaBlastPreviewLoop()
    {
        while (true)
        {
            // Fade in charge sound
            if (gigaBlastPreview.chargeSound != null && _loopAudioSource != null)
            {
                _loopAudioSource.volume = 0f;
                gigaBlastPreview.chargeSound.Play(_loopAudioSource);
                _soundFadeCoroutine = StartCoroutine(FadeAudioSource(_loopAudioSource, gigaBlastPreview.chargeSound.volume, gigaBlastPreview.chargeSoundFadeDuration));
            }

            // Cycle through tiers 1-4
            ParticleSystem[] tierParticles = new[]
            {
                gigaBlastPreview.tier1Particles,
                gigaBlastPreview.tier2Particles,
                gigaBlastPreview.tier3Particles,
                gigaBlastPreview.tier4Particles
            };

            for (int tier = 0; tier < 4; tier++)
            {
                // Stop previous tier
                if (tier > 0 && tierParticles[tier - 1] != null)
                    tierParticles[tier - 1].Stop(true, ParticleSystemStopBehavior.StopEmitting);

                // Start current tier
                if (tierParticles[tier] != null)
                    tierParticles[tier].Play();

                yield return new WaitForSecondsRealtime(gigaBlastPreview.tierDuration);
            }

            // Stop all particles
            StopAllChargeParticles();

            // Stop charge sound
            if (_soundFadeCoroutine != null)
            {
                StopCoroutine(_soundFadeCoroutine);
                _soundFadeCoroutine = null;
            }
            if (_loopAudioSource != null)
            {
                _loopAudioSource.Stop();
                _loopAudioSource.volume = 0f;
            }

            // Fire tier 4 projectile
            if (gigaBlastPreview.fireProjectilePrefab != null)
            {
                Vector3 spawnPos = transform.position + transform.up * gigaBlastPreview.offsetDistance;
                GameObject projectile = Instantiate(gigaBlastPreview.fireProjectilePrefab, spawnPos, transform.rotation);

                if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
                {
                    projectileScript.Initialize(
                        transform.up,
                        Vector2.zero,
                        gigaBlastPreview.projectileSpeed,
                        0f, // No damage in preview
                        gigaBlastPreview.projectileLifetime,
                        0f, // No impact force
                        null // No shooter
                    );
                }
            }

            // Play fire sound
            if (gigaBlastPreview.fireSound != null && _oneShotAudioSource != null)
                gigaBlastPreview.fireSound.Play(_oneShotAudioSource);

            // Pause after fire
            yield return new WaitForSecondsRealtime(gigaBlastPreview.pauseAfterFire);
        }
    }

    // ===== HELPERS =====

    private void StopAllChargeParticles()
    {
        if (gigaBlastPreview.tier1Particles != null)
            gigaBlastPreview.tier1Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (gigaBlastPreview.tier2Particles != null)
            gigaBlastPreview.tier2Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (gigaBlastPreview.tier3Particles != null)
            gigaBlastPreview.tier3Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (gigaBlastPreview.tier4Particles != null)
            gigaBlastPreview.tier4Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (_cachedRenderers == null) return;

        foreach (var renderer in _cachedRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private IEnumerator FadeAudioSource(AudioSource source, float targetVolume, float duration, bool stopAfterFade = false)
    {
        if (source == null) yield break;

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (source == null) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (source == null) yield break;

        source.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && source.isPlaying)
            source.Stop();
    }

    private void OnDisable()
    {
        // Ensure clean state when ship model is deactivated
        StopPreviewInternal();
        IsRotationLocked = false;
    }
}
