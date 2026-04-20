using UnityEngine;
using UnityEngine.InputSystem;

public class ConvergeBeam : Ability
{
    [System.Serializable]
    public struct ConvergeBeamAbilityConfig
    {
        [Header("Beam Settings")]
        public BeamWeaponConfig stats;
        [Tooltip("Distance ahead of ship where all beams converge")]
        public float convergenceDistance;
        [Tooltip("Rotation speed multiplier when beams are active (0.3 = 70% slower)")]
        public float rotationMultiplier;

        [Header("Hardpoints")]
        [Tooltip("Cannon positions on ship model.")]
        public Transform[] hardpoints;

        [Header("Empowerment")]
        [Tooltip("Reference to this ship's Empower ability. If null, auto-finds on this GameObject.")]
        public Empower empowerAbility;
        [Tooltip("If true, always uses empowered beam count (debug/testing).")]
        public bool forceEmpowered;
        [Tooltip("Number of beams in base mode.")]
        public int baseBeamCount;
        [Tooltip("Number of beams in empowered mode.")]
        public int empoweredBeamCount;

        [Header("Beam Capacity")]
        [Tooltip("Maximum beam capacity (100 units)")]
        public float capacity;
        [Tooltip("How fast beam drains (units per second)")]
        public float drainRate;
        [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
        public float regenRate;

        [Header("Sound Effects")]
        public SoundEffect beamLoopSound;
        [Tooltip("Fade in/out duration for beam sound (seconds)")]
        public float soundFadeDuration;
    }

    [Header("Converge Beam Weapon")]
    public ConvergeBeamAbilityConfig convergeBeam;

    // ===== PRIVATE STATE =====
    private LaserBeam[] _activeBeams;
    private float _currentBeamCapacity;
    private AudioSource _laserBeamSource;
    private Coroutine _beamFadeCoroutine;
    private bool _isFiring;
    private bool _lastEmpoweredState;

    protected override void Awake()
    {
        base.Awake();
        _currentBeamCapacity = 0f;
        if (convergeBeam.empowerAbility == null)
        {
            convergeBeam.empowerAbility = GetComponent<Empower>();
        }

        _laserBeamSource = gameObject.AddComponent<AudioSource>();
        _laserBeamSource.playOnAwake = false;
        _laserBeamSource.loop = true;
        _laserBeamSource.spatialBlend = 0f;
        _lastEmpoweredState = IsEmpoweredActive();
    }

    protected void Update()
    {
        if (_isFiring)
        {
            bool empoweredNow = IsEmpoweredActive();
            if (empoweredNow != _lastEmpoweredState)
            {
                // Rebuild beam set so count switches immediately when Empower toggles.
                DestroyAllBeams();
                SpawnAllBeams();
            }

            UpdateBeamConvergence();
        }
        else if (_currentBeamCapacity > 0f)
        {
            _currentBeamCapacity = Mathf.Max(_currentBeamCapacity - convergeBeam.regenRate * Time.deltaTime, 0f);
        }
    }

    void FixedUpdate()
    {
        if (!_isFiring || _activeBeams == null)
            return;

        // Sum recoil from all active beams
        float totalRecoil = 0f;
        foreach (var beam in _activeBeams)
        {
            if (beam != null)
                totalRecoil += beam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
        }
        player.ApplyRecoil(totalRecoil);

        // Drain capacity
        _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + convergeBeam.drainRate * Time.fixedDeltaTime, convergeBeam.capacity);

        if (_currentBeamCapacity >= convergeBeam.capacity)
        {
            Debug.Log("Converge beam capacity full! Stopping beams.");
            DestroyAllBeams();
            FadeOutSound();
        }
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);
        Debug.Log($"Converge Beam input received - isPressed: {value.isPressed}");

        if (value.isPressed)
        {
            if (_currentBeamCapacity >= convergeBeam.capacity)
            {
                Debug.Log("Cannot fire converge beam: capacity full (overheated)");
                return;
            }

            if (!_isFiring && convergeBeam.stats.prefab != null && convergeBeam.hardpoints != null && convergeBeam.hardpoints.Length > 0)
            {
                Debug.Log($"Creating {GetActiveHardpointCount()} converging beams (empowered: {IsEmpoweredActive()})");
                SpawnAllBeams();
                FadeInSound();
            }
        }
        else
        {
            if (_isFiring)
            {
                Debug.Log("Stopping all converging beams");
                DestroyAllBeams();
                FadeOutSound();
            }
        }
    }

    private int GetActiveHardpointCount()
    {
        if (convergeBeam.hardpoints == null) return 0;
        int desired = IsEmpoweredActive() ? convergeBeam.empoweredBeamCount : convergeBeam.baseBeamCount;
        if (desired <= 0)
        {
            desired = IsEmpoweredActive() ? 4 : 2;
        }

        return Mathf.Min(desired, convergeBeam.hardpoints.Length);
    }

    private void SpawnAllBeams()
    {
        _lastEmpoweredState = IsEmpoweredActive();
        int count = GetActiveHardpointCount();
        _activeBeams = new LaserBeam[count];

        for (int i = 0; i < count; i++)
        {
            Transform hardpoint = convergeBeam.hardpoints[i];
            if (hardpoint == null) continue;

            GameObject beamObj = Instantiate(convergeBeam.stats.prefab, hardpoint.position, hardpoint.rotation, hardpoint);
            LaserBeam beam = beamObj.GetComponent<LaserBeam>();
            beam.Initialize(
                player.enemyTag,
                convergeBeam.stats.damagePerSecond,
                convergeBeam.stats.maxDistance,
                convergeBeam.stats.recoilForcePerSecond,
                convergeBeam.stats.impactForce,
                player
            );
            beam.StartFiring();
            _activeBeams[i] = beam;
        }

        _isFiring = true;
        UpdateBeamConvergence();
    }

    private void UpdateBeamConvergence()
    {
        if (_activeBeams == null) return;

        Vector3 convergencePoint = transform.position + transform.up * convergeBeam.convergenceDistance;

        for (int i = 0; i < _activeBeams.Length; i++)
        {
            if (_activeBeams[i] == null) continue;

            Transform hardpoint = convergeBeam.hardpoints[i];
            if (hardpoint == null) continue;

            Vector3 direction = convergencePoint - hardpoint.position;
            _activeBeams[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
        }
    }

    private void DestroyAllBeams()
    {
        if (_activeBeams != null)
        {
            foreach (var beam in _activeBeams)
            {
                if (beam != null)
                {
                    beam.StopFiring();
                    Destroy(beam.gameObject);
                }
            }
            _activeBeams = null;
        }
        _isFiring = false;
    }

    private bool IsEmpoweredActive()
    {
        if (convergeBeam.forceEmpowered)
        {
            return true;
        }

        return convergeBeam.empowerAbility != null && convergeBeam.empowerAbility.IsEmpoweredActive;
    }

    // ===== ROTATION =====
    public override void ApplyRotationMultiplier()
    {
        base.ApplyRotationMultiplier();
        player.movement.rotationSpeed *= convergeBeam.rotationMultiplier;
    }

    public override bool IsAbilityActive()
    {
        return _isFiring;
    }

    // ===== HUD STATE =====
    public override bool IsResourceBased() => true;
    public override float GetHUDFillRatio()
    {
        if (convergeBeam.capacity <= 0f) return 0f;
        return _currentBeamCapacity / convergeBeam.capacity;
    }
    public override bool IsOnCooldown() => false;

    // ===== CLEANUP =====
    public override void Die()
    {
        DestroyAllBeams();

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
    private void FadeInSound()
    {
        if (convergeBeam.beamLoopSound != null && _laserBeamSource != null)
        {
            _laserBeamSource.volume = 0f;
            convergeBeam.beamLoopSound.Play(_laserBeamSource);

            if (_beamFadeCoroutine != null)
                StopCoroutine(_beamFadeCoroutine);
            _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(convergeBeam.beamLoopSound.volume));
        }
    }

    private void FadeOutSound()
    {
        if (_laserBeamSource != null && _laserBeamSource.isPlaying)
        {
            if (_beamFadeCoroutine != null)
                StopCoroutine(_beamFadeCoroutine);
            _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
        }
        else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
    }

    private System.Collections.IEnumerator FadeBeamVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_laserBeamSource == null) yield break;

        float startVolume = _laserBeamSource.volume;
        float elapsed = 0f;

        while (elapsed < convergeBeam.soundFadeDuration)
        {
            if (_laserBeamSource == null || (!_laserBeamSource.isPlaying && targetVolume > 0f))
                yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / convergeBeam.soundFadeDuration;
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
