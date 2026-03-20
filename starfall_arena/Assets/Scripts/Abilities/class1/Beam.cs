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
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _currentBeamCapacity = 0f;
        _netMovement = GetComponent<NetMovement>();

        _laserBeamSource = gameObject.AddComponent<AudioSource>();
        _laserBeamSource.playOnAwake = false;
        _laserBeamSource.loop = true;
        _laserBeamSource.spatialBlend = 0f;
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
        if (NetTickUtil.IsActive && _netMovement != null && !_netMovement.IsOwner && !_netMovement.IsServer)
        {
            return;
        }

        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            player.ApplyRecoil(recoilForceThisFrame);

            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + beam.drainRate * Time.fixedDeltaTime, beam.capacity);

            if (_currentBeamCapacity >= beam.capacity)
            {
                bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
                ApplyNetworkBeamState(false, authoritative: useNetworkPath && _netMovement.IsServer);

                if (useNetworkPath)
                {
                    _netMovement.RequestBeamState(false);
                }
            }
        }
    }
    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (value.isPressed)
        {
            //if (_isCharging)
            //{
            //    Debug.Log("Cannot fire beam while charging GigaBlast");
            //    return;
            //}

            if (_currentBeamCapacity >= beam.capacity)
            {
                return;
            }

            if (_activeBeam == null && beam.stats.prefab != null)
            {
                ApplyNetworkBeamState(true, authoritative: useNetworkPath && _netMovement.IsServer, requestedTick: NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1);

                if (useNetworkPath)
                {
                    _netMovement.RequestBeamState(true);
                }
            }
        }
        else
        {
            if (_activeBeam != null)
            {
                ApplyNetworkBeamState(false, authoritative: useNetworkPath && _netMovement.IsServer);

                if (useNetworkPath)
                {
                    _netMovement.RequestBeamState(false);
                }
            }
        }
    }

    public void ApplyNetworkBeamState(bool isFiring, bool authoritative, int requestedTick = -1)
    {
        if (isFiring)
        {
            if (_activeBeam != null || beam.stats.prefab == null)
            {
                return;
            }

            Vector3 spawnPosition = transform.position + transform.up * beam.offsetDistance;
            GameObject beamObj = Instantiate(beam.stats.prefab, spawnPosition, transform.rotation, transform);
            _activeBeam = beamObj.GetComponent<LaserBeam>();
            if (_activeBeam == null)
            {
                Destroy(beamObj);
                return;
            }

            _activeBeam.Initialize(
                player.enemyTag,
                beam.stats.damagePerSecond,
                beam.stats.maxDistance,
                beam.stats.recoilForcePerSecond,
                beam.stats.impactForce,
                player);

            bool cosmeticOnly = NetTickUtil.IsActive && !authoritative;
            _activeBeam.SetCosmeticOnly(cosmeticOnly);
            if (NetTickUtil.IsActive && authoritative && _netMovement != null)
            {
                int beamTick = requestedTick >= 0 ? requestedTick : NetTickUtil.ServerTick;
                _activeBeam.SetNetworkAuthority(_netMovement, beamTick);
            }

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

            return;
        }

        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }

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

    public override float GetRotationMultiplier()
    {
        if (_activeBeam == null) return 1f;
        return beam.rotationMultiplier;
    }

    public override void ApplyRotationMultiplier()
    {
        base.ApplyRotationMultiplier();
        player.movement.rotationSpeed *= GetRotationMultiplier();
    }

    public override bool IsAbilityActive()
    {
        return _activeBeam != null;
    }

    // ===== HUD STATE =====
    public override bool IsResourceBased() => true;
    public override float GetHUDFillRatio()
    {
        if (beam.capacity <= 0f) return 0f;
        return _currentBeamCapacity / beam.capacity;
    }
    public override bool IsOnCooldown() => false;

    public override void Die()
    {
        ApplyNetworkBeamState(false, authoritative: NetTickUtil.IsActive && _netMovement != null && _netMovement.IsServer);

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
