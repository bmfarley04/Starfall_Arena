using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Beam : Ability
{
    [System.Serializable]
    public struct BeamAbilityConfig
    {
        [Header("Beam Settings")]
        public BeamWeaponConfig stats;
        public float offsetDistance;
        [Tooltip("Rotation speed multiplier when beam is active (0.3 = 70% slower)")]
        public float rotationMultiplier;
        [Tooltip("Duration for beam line renderer to fade in (seconds)")]
        public float fadeInDuration;

        [Header("Beam Capacity")]
        [Tooltip("Maximum beam capacity (100 units)")]
        public float capacity;
        [Tooltip("How fast beam drains (units per second)")]
        public float drainRate;
        [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
        public float regenRate;

        [Header("Sound Effects")]
        public SoundEffect beamLoopSound;
        [Tooltip("Fade in duration for beam sound (seconds)")]
        public float soundFadeDuration;
    }

    [Header("Ability 1 - Beam Weapon")]
    public BeamAbilityConfig beam;

    // ===== PRIVATE STATE =====
    private LaserBeam _activeBeam;
    private float _currentBeamCapacity;
    private AudioSource _laserBeamSource;
    private Coroutine _beamFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _currentBeamCapacity = 0f;

        _laserBeamSource = gameObject.AddComponent<AudioSource>();
        _laserBeamSource.playOnAwake = false;
        _laserBeamSource.loop = true;
        _laserBeamSource.spatialBlend = 1f;
        _laserBeamSource.rolloffMode = AudioRolloffMode.Linear;
        _laserBeamSource.minDistance = 10f;
        _laserBeamSource.maxDistance = 50f;
        _laserBeamSource.dopplerLevel = 0f;
    }

    protected void Update()
    {
        if (_activeBeam == null && _currentBeamCapacity > 0f)
        {
            _currentBeamCapacity = Mathf.Max(_currentBeamCapacity - beam.regenRate * Time.deltaTime, 0f);
        }
    }

    void FixedUpdate()
    {
        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            player.ApplyRecoil(recoilForceThisFrame);

            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + beam.drainRate * Time.fixedDeltaTime, beam.capacity);

            if (_currentBeamCapacity >= beam.capacity)
            {
                Debug.Log("Beam capacity full! Stopping beam.");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }
    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);
        Debug.Log($"Fire Beam input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            //if (_isCharging)
            //{
            //    Debug.Log("Cannot fire beam while charging GigaBlast");
            //    return;
            //}

            if (_currentBeamCapacity >= beam.capacity)
            {
                Debug.Log("Cannot fire beam: capacity full (overheated)");
                return;
            }

            if (_activeBeam == null && beam.stats.prefab != null)
            {
                Debug.Log("Creating and starting beam");

                Vector3 spawnPosition = transform.position + transform.up * beam.offsetDistance;

                GameObject beamObj = Instantiate(beam.stats.prefab, spawnPosition, transform.rotation, transform);
                _activeBeam = beamObj.GetComponent<LaserBeam>();
                _activeBeam.Initialize(
                    player.enemyTag,
                    beam.stats.damagePerSecond,
                    beam.stats.maxDistance,
                    beam.stats.recoilForcePerSecond,
                    beam.stats.impactForce,
                    player
                );
                _activeBeam.StartFiring();

                if (beam.beamLoopSound != null && _laserBeamSource != null)
                {
                    _laserBeamSource.volume = 0f;
                    beam.beamLoopSound.Play(_laserBeamSource);

                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(beam.beamLoopSound.volume));
                }
            }
        }
        else
        {
            if (_activeBeam != null)
            {
                Debug.Log("Stopping and destroying beam");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }

    public override void ApplyRotationMultiplier()
    {
        base.ApplyRotationMultiplier();
        player.movement.rotationSpeed *= beam.rotationMultiplier;
    }

    public override bool IsAbilityActive()
    {
        return _activeBeam != null;
    }

    public override void Die()
    {
        if (_laserBeamSource != null && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }

        if (_beamFadeCoroutine != null)
        {
            StopCoroutine(_beamFadeCoroutine);
        }

        base.Die();
    }

    // ===== AUDIO =====
    private System.Collections.IEnumerator FadeBeamVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_laserBeamSource == null) yield break;

        float startVolume = _laserBeamSource.volume;
        float elapsed = 0f;

        while (elapsed < beam.soundFadeDuration)
        {
            if (_laserBeamSource == null || (!_laserBeamSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / beam.soundFadeDuration;
            _laserBeamSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        if (_laserBeamSource == null) yield break;

        _laserBeamSource.volume = targetVolume;

        if (targetVolume <= 0f && stopAfterFade && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
    }

}
