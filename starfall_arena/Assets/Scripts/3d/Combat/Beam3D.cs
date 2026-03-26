using UnityEngine;
using UnityEngine.InputSystem;

public class Beam3D : Ability3D
{
    [System.Serializable]
    public struct BeamAbilityConfig3D
    {
        [Header("Beam Settings")]
        public GameObject beamPrefab;
        public string targetTag;
        public float damagePerSecond;
        public float maxDistance;
        public float recoilForcePerSecond;
        public float impactForce;
        public float offsetDistance;
        [Tooltip("Optional muzzle transform for beam origin. Falls back to entity transform if unset.")]
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
    }

    [Header("Ability 1 - Beam Weapon")]
    public BeamAbilityConfig3D beam;

    private LaserBeam3D _activeBeam;
    private float _currentBeamCapacity;

    protected override void Awake()
    {
        base.Awake();
        _currentBeamCapacity = 0f;
    }

    void Update()
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
            if (entity.Flight != null)
            {
                entity.Flight.ApplyRecoil(recoilForceThisFrame);
            }

            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + beam.drainRate * Time.fixedDeltaTime, beam.capacity);

            if (_currentBeamCapacity >= beam.capacity)
            {
                StopBeam();
            }
        }
    }

    public override void UseAbility(InputValue value)
    {
        if (value.isPressed)
        {
            if (_currentBeamCapacity >= beam.capacity)
            {
                return;
            }

            if (_activeBeam == null && beam.beamPrefab != null)
            {
                StartBeam();
            }
        }
        else
        {
            if (_activeBeam != null)
            {
                StopBeam();
            }
        }
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

        _activeBeam.Initialize(
            beam.targetTag,
            beam.damagePerSecond,
            beam.maxDistance,
            beam.recoilForcePerSecond,
            beam.impactForce,
            entity,
            beam.muzzle != null ? beam.muzzle : entity.transform,
            beam.offsetDistance);

        _activeBeam.StartFiring();
    }

    private void StopBeam()
    {
        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeam.gameObject);
            _activeBeam = null;
        }
    }

    public override float GetRotationMultiplier()
    {
        if (_activeBeam == null) return 1f;
        return beam.rotationMultiplier;
    }

    public override bool IsAbilityActive()
    {
        return _activeBeam != null;
    }

    public override bool DisablePrimaryFire()
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
        StopBeam();
    }
}
