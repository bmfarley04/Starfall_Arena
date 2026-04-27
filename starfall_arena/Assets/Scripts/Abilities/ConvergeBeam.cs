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
    private bool _activeAuthoritative;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _currentBeamCapacity = 0f;
        if (convergeBeam.empowerAbility == null)
        {
            convergeBeam.empowerAbility = GetComponent<Empower>();
        }
        _netMovement = GetComponent<NetMovement>();

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
                // Rebuild locally so the count switches the moment Empower toggles.
                // Empower state is itself networked, so each peer (server, owner,
                // remote client) observes the toggle and rebuilds independently.
                bool wasAuthoritative = _activeAuthoritative;
                DestroyBeamObjects();
                SpawnAllBeams(wasAuthoritative, NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1);
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
        // Remote, non-authoritative copies don't drain capacity or apply recoil.
        if (NetTickUtil.IsActive && _netMovement != null && !_netMovement.IsOwner && !_netMovement.IsServer)
        {
            return;
        }

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
            StopFiringRouted();
        }
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;

        if (value.isPressed)
        {
            if (_currentBeamCapacity >= convergeBeam.capacity)
            {
                return;
            }

            if (!_isFiring && convergeBeam.stats.prefab != null && convergeBeam.hardpoints != null && convergeBeam.hardpoints.Length > 0)
            {
                ApplyNetworkConvergeBeamState(true, authoritative: !NetTickUtil.IsActive || (useNetworkPath && _netMovement.IsServer), requestedTick: NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1);

                if (useNetworkPath)
                {
                    _netMovement.RequestConvergeBeamState(true);
                }
            }
        }
        else
        {
            if (_isFiring)
            {
                StopFiringRouted();
            }
        }
    }

    private void StopFiringRouted()
    {
        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        ApplyNetworkConvergeBeamState(false, authoritative: !NetTickUtil.IsActive || (useNetworkPath && _netMovement.IsServer));

        if (useNetworkPath)
        {
            _netMovement.RequestConvergeBeamState(false);
        }
    }

    public void ApplyNetworkConvergeBeamState(bool isFiring, bool authoritative, int requestedTick = -1)
    {
        if (isFiring)
        {
            if (_isFiring || convergeBeam.stats.prefab == null || convergeBeam.hardpoints == null || convergeBeam.hardpoints.Length == 0)
            {
                return;
            }

            SpawnAllBeams(authoritative, requestedTick);
            FadeInSound();
        }
        else
        {
            if (!_isFiring)
            {
                return;
            }

            DestroyBeamObjects();
            _isFiring = false;
            _activeAuthoritative = false;
            FadeOutSound();
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

    private void SpawnAllBeams(bool authoritative, int requestedTick)
    {
        _lastEmpoweredState = IsEmpoweredActive();
        int count = GetActiveHardpointCount();
        _activeBeams = new LaserBeam[count];

        bool cosmeticOnly = NetTickUtil.IsActive && !authoritative;

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

            beam.SetCosmeticOnly(cosmeticOnly);
            if (NetTickUtil.IsActive && authoritative && _netMovement != null)
            {
                int beamTick = requestedTick >= 0 ? requestedTick : NetTickUtil.ServerTick;
                beam.SetNetworkAuthority(_netMovement, beamTick);
            }

            beam.StartFiring();
            _activeBeams[i] = beam;
        }

        _isFiring = true;
        _activeAuthoritative = authoritative;
        UpdateBeamConvergence();
    }

    private void UpdateBeamConvergence()
    {
        if (_activeBeams == null) return;

        Vector2 aimDirection = ResolveCurrentAimDirection();
        Vector3 convergencePoint = transform.position + (Vector3)(aimDirection * convergeBeam.convergenceDistance);

        for (int i = 0; i < _activeBeams.Length; i++)
        {
            if (_activeBeams[i] == null) continue;

            Transform hardpoint = convergeBeam.hardpoints[i];
            if (hardpoint == null) continue;

            Vector3 direction = convergencePoint - hardpoint.position;
            _activeBeams[i].transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
        }
    }

    private Vector2 ResolveCurrentAimDirection()
    {
        if (player != null)
        {
            Vector2 liveLookInput = player.LookInput;
            if (liveLookInput.sqrMagnitude > 0.0001f)
            {
                return liveLookInput.normalized;
            }
        }

        if (NetTickUtil.IsActive && _netMovement != null)
        {
            Vector2 networkLookInput = _netMovement.GetLatestLookInput();
            if (networkLookInput.sqrMagnitude > 0.0001f)
            {
                return networkLookInput.normalized;
            }
        }

        return transform.up;
    }

    private void DestroyBeamObjects()
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
    public override float GetRotationMultiplier()
    {
        if (!_isFiring) return 1f;
        return convergeBeam.rotationMultiplier;
    }

    public override void ApplyRotationMultiplier()
    {
        base.ApplyRotationMultiplier();
        player.movement.rotationSpeed *= GetRotationMultiplier();
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
        if (_isFiring)
        {
            StopFiringRouted();
        }
        else
        {
            DestroyBeamObjects();
        }

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
