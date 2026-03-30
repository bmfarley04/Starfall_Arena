using UnityEngine;

public class BeamWeapon3D : Weapon3D
{
    [System.Serializable]
    public struct BeamWeaponConfig3D
    {
        [Header("Beam Settings")]
        public GameObject beamPrefab;
        public string targetTag;
        public float damagePerSecond;
        public float maxDistance;
        public float recoilForcePerSecond;
        public float impactForce;
        public float offsetDistance;
        [Tooltip("Vertical offset from the muzzle anchor along its local up axis (positive = up).")]
        public float verticalOffset;
        [Tooltip("Optional muzzle transform for beam origin. Falls back to the entity transform if unset.")]
        public Transform muzzle;
        [Tooltip("Rotation speed multiplier when beam is active (0.3 = 70% slower)")]
        public float rotationMultiplier;

        [Header("Beam Capacity")]
        [Tooltip("Maximum beam capacity (100 units)")]
        public float capacity;
        [Tooltip("How fast beam drains (units per second)")]
        public float drainRate;
        [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
        public float regenRate;

        [Header("Sound Effects")]
        [Tooltip("Looping sound played while the beam is firing.")]
        public SoundEffect fireLoopSound;
    }

    [Header("Weapon 2 - Beam")]
    [SerializeField] private BeamWeaponConfig3D beam;
    [SerializeField] private AudioSource beamLoopAudioSource;

    private LaserBeam3D _activeBeam;

    private bool UsesBeamCapacity => beam.capacity > 0f && beam.drainRate > 0f;

    protected override void Awake()
    {
        base.Awake();
        if (beamLoopAudioSource == null)
        {
            beamLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        beamLoopAudioSource.playOnAwake = false;
        beamLoopAudioSource.loop = true;
        beamLoopAudioSource.spatialBlend = 0f;
    }

    protected override float GetConfiguredResourceCapacity()
    {
        return beam.capacity;
    }

    protected override float GetConfiguredResourceRecoveryPerSecond()
    {
        return beam.regenRate;
    }

    protected override bool ShouldRecoverResource()
    {
        return _activeBeam == null;
    }

    protected override void OnWeaponFixedUpdated(float deltaTime)
    {
        if (_activeBeam == null)
        {
            return;
        }

        float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * deltaTime;
        if (Owner != null && Owner.Flight != null)
        {
            Owner.Flight.ApplyRecoil(recoilForceThisFrame);
        }

        if (!UsesBeamCapacity)
        {
            return;
        }

        AddResourceUsage(beam.drainRate * deltaTime);
        if (CurrentResourceUsage >= Mathf.Max(0f, beam.capacity) - 0.001f)
        {
            StopBeam();
        }
    }

    protected override void OnFirePressed()
    {
        if (_activeBeam != null || beam.beamPrefab == null)
        {
            return;
        }

        if (UsesBeamCapacity && !CanSpendResource(beam.drainRate * Time.fixedDeltaTime))
        {
            return;
        }

        StartBeam();
    }

    protected override void OnFireReleased()
    {
        StopBeam();
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        StopBeam();
    }

    public override float GetRotationMultiplier()
    {
        return _activeBeam != null ? beam.rotationMultiplier : 1f;
    }

    public override bool IsReticleSpinActive()
    {
        return _activeBeam != null;
    }

    public override void Die()
    {
        StopBeam();
    }

    private void StartBeam()
    {
        GameObject beamObj = Instantiate(beam.beamPrefab, Vector3.zero, Quaternion.identity);
        _activeBeam = beamObj.GetComponent<LaserBeam3D>();
        if (_activeBeam == null)
        {
            Destroy(beamObj);
            return;
        }

        Transform muzzle = beam.muzzle != null ? beam.muzzle : Owner != null ? Owner.transform : transform;
        _activeBeam.Initialize(
            beam.targetTag,
            beam.damagePerSecond,
            beam.maxDistance,
            beam.recoilForcePerSecond,
            beam.impactForce,
            Owner,
            muzzle,
            beam.offsetDistance,
            beam.verticalOffset);

        _activeBeam.StartFiring();
        StartBeamLoopSound();
    }

    private void StopBeam()
    {
        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }

        StopBeamLoopSound();
    }

    private void OnDisable()
    {
        StopBeam();
    }

    private void StartBeamLoopSound()
    {
        if (beam.fireLoopSound == null || beamLoopAudioSource == null)
        {
            return;
        }

        if (beamLoopAudioSource.isPlaying && beamLoopAudioSource.clip == beam.fireLoopSound.clip)
        {
            return;
        }

        beamLoopAudioSource.loop = true;
        beam.fireLoopSound.Play(beamLoopAudioSource);
    }

    private void StopBeamLoopSound()
    {
        if (beamLoopAudioSource == null || !beamLoopAudioSource.isPlaying)
        {
            return;
        }

        beamLoopAudioSource.Stop();
    }
}
