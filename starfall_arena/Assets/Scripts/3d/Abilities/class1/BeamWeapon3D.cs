using UnityEngine;

public class BeamWeapon3D : Weapon3D, IBeamWeaponNetwork3D
{
    [System.Serializable]
    public struct BeamWeaponConfig3D
    {
        [Header("Beam Settings")]
        public GameObject beamPrefab;
        public string targetTag;
        public Faction3D targetFaction;
        public float damagePerSecond;
        public float maxDistance;
        public float recoilForcePerSecond;
        public float impactForce;
        public float offsetDistance;
        [Tooltip("Vertical offset from the muzzle anchor along its local up axis (positive = up).")]
        public float verticalOffset;
        [Tooltip("Optional muzzle transform for beam origin. Falls back to the entity transform if unset.")]
        public Transform muzzle;
        [Tooltip("Optional transform whose +Z/forward axis defines beam aim and cast direction. Use this when the muzzle is positioned correctly but its local forward points away from the intended shot lane.")]
        public Transform directionReference;
        [Tooltip("Rotation speed multiplier when beam is active (0.3 = 70% slower)")]
        public float rotationMultiplier;
        [Tooltip("How long the rotation penalty should linger after the beam stops, to prevent instant pivot-and-refire behavior.")]
        public float postFireRotationPenaltyDuration;

        [Header("Beam Capacity")]
        [Tooltip("Maximum beam capacity (100 units)")]
        public float capacity;
        [Tooltip("How fast beam drains (units per second)")]
        public float drainRate;
        [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
        public float regenRate;
        [Tooltip("Minimum remaining beam energy required before the weapon is allowed to start firing again.")]
        public float minimumStartEnergy;

        [Header("Sound Effects")]
        [Tooltip("Looping sound played while the beam is firing.")]
        public SoundEffect fireLoopSound;
    }

    [Header("Weapon 2 - Beam")]
    [SerializeField] private BeamWeaponConfig3D beam;
    [SerializeField] private AudioSource beamLoopAudioSource;

    private IBeamRuntime3D _activeBeam;
    private MonoBehaviour _activeBeamComponent;
    private NetCombat3D _netCombat;
    private bool _activeBeamAuthoritative = true;
    private Vector3 _pendingNetworkAim;
    private bool _hasPendingNetworkAim;
    private int _activeBeamAttackId = PlayerCombatStats3D.InvalidAttackId;
    private float _rotationPenaltyUntilTime = float.NegativeInfinity;

    private bool UsesBeamCapacity => beam.capacity > 0f && beam.drainRate > 0f;
    public bool IsBeamActive => _activeBeam != null;

    protected override void Awake()
    {
        base.Awake();
        _netCombat ??= GetComponent<NetCombat3D>();
        if (beamLoopAudioSource == null)
        {
            beamLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        beamLoopAudioSource.playOnAwake = false;
        beamLoopAudioSource.loop = true;
        beamLoopAudioSource.spatialBlend = 1f;
        beamLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
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

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned && _netCombat.IsOwner)
        {
            _netCombat.UpdateBeamAim(ResolveOwnerAimDirection());
        }

        float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * deltaTime;
        if (_activeBeamAuthoritative && Owner != null && Owner.Flight != null)
        {
            Owner.Flight.ApplyRecoil(recoilForceThisFrame);
            _netCombat?.ApplyCombatVelocityDelta(-transform.forward * recoilForceThisFrame);
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

    public bool CanStartBeamNow()
    {
        if (beam.beamPrefab == null || _activeBeam != null)
        {
            return false;
        }

        if (!UsesBeamCapacity)
        {
            return true;
        }

        float remainingEnergy = GetRemainingBeamEnergy();
        float minimumStartEnergy = Mathf.Clamp(beam.minimumStartEnergy, 0f, Mathf.Max(0f, beam.capacity));
        float firstFrameCost = Mathf.Max(beam.drainRate * Time.fixedDeltaTime, 0f);
        float requiredEnergy = Mathf.Max(minimumStartEnergy, firstFrameCost);
        return remainingEnergy + 0.001f >= requiredEnergy;
    }

    public float GetRemainingBeamEnergy()
    {
        if (!UsesBeamCapacity)
        {
            return 0f;
        }

        return Mathf.Max(0f, beam.capacity - CurrentResourceUsage);
    }

    public Vector3 GetBeamForwardDirection()
    {
        Transform directionSource = ResolveBeamDirectionSource();
        if (directionSource != null && directionSource.forward.sqrMagnitude > 0.0001f)
        {
            return directionSource.forward.normalized;
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
    }

    public Vector3 GetBeamOrigin(Vector3 aimDirection)
    {
        Transform muzzle = beam.muzzle != null ? beam.muzzle : Owner != null ? Owner.transform : transform;
        Vector3 normalizedDirection = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection.normalized
            : GetBeamForwardDirection();

        if (muzzle == null)
        {
            return transform.position + (normalizedDirection * beam.offsetDistance) + (transform.up * beam.verticalOffset);
        }

        return muzzle.position + (normalizedDirection * beam.offsetDistance) + (muzzle.up * beam.verticalOffset);
    }

    private Vector3 ResolveOwnerAimDirection()
    {
        Camera cam = AimCamera;
        if (cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (centerRay.direction.sqrMagnitude > 0.0001f)
            {
                return centerRay.direction.normalized;
            }
        }

        Transform directionSource = ResolveBeamDirectionSource();
        if (directionSource != null && directionSource.forward.sqrMagnitude > 0.0001f)
        {
            return directionSource.forward.normalized;
        }

        return transform.forward;
    }

    protected override void OnFirePressed()
    {
        if (!CanStartBeamNow())
        {
            return;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            Vector3 aimDirection = ResolveOwnerAimDirection();
            _netCombat.RequestBeamState(true, aimDirection);
            if (!_netCombat.IsServer)
            {
                StartBeam(authoritative: false, PlayerCombatStats3D.InvalidAttackId);
            }
            return;
        }

        int attackId = Owner != null
            ? Owner.GetComponent<PlayerCombatStats3D>()?.BeginTrackedAttack() ?? PlayerCombatStats3D.InvalidAttackId
            : PlayerCombatStats3D.InvalidAttackId;
        StartBeam(authoritative: true, attackId);
    }

    protected override void OnFireReleased()
    {
        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            _netCombat.RequestBeamState(false, ResolveOwnerAimDirection());
        }

        StopBeam();
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        StopBeam();
    }

    public override float GetRotationMultiplier()
    {
        bool shouldApplyPenalty = _activeBeam != null || Time.time < _rotationPenaltyUntilTime;
        return shouldApplyPenalty ? beam.rotationMultiplier : 1f;
    }

    public override bool IsReticleSpinActive()
    {
        return _activeBeam != null;
    }

    public override void Die()
    {
        StopBeam();
    }

    public void ApplyNetworkBeamState(bool isFiring, bool authoritative)
    {
        if (isFiring)
        {
            StartBeam(authoritative, PlayerCombatStats3D.InvalidAttackId);
        }
        else
        {
            StopBeam();
        }
    }

    public void ApplyNetworkBeamAim(Vector3 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _pendingNetworkAim = aimDirection;
        _hasPendingNetworkAim = true;

        if (_activeBeam != null)
        {
            _activeBeam.SetNetworkAim(aimDirection);
        }
    }

    public void ApplyNetworkBeamState(bool isFiring, bool authoritative, int accuracyAttackId)
    {
        if (isFiring)
        {
            StartBeam(authoritative, accuracyAttackId);
        }
        else
        {
            StopBeam();
        }
    }

    private void StartBeam(bool authoritative, int accuracyAttackId)
    {
        if (_activeBeam == null && !CanStartBeamNow())
        {
            return;
        }

        if (_activeBeam != null)
        {
            _activeBeamAuthoritative = authoritative;
            if (accuracyAttackId != PlayerCombatStats3D.InvalidAttackId)
            {
                _activeBeamAttackId = accuracyAttackId;
                _activeBeam.SetAccuracyAttackId(accuracyAttackId);
            }
            return;
        }

        GameObject beamObj = Instantiate(beam.beamPrefab, Vector3.zero, Quaternion.identity);
        _activeBeam = null;
        _activeBeamComponent = null;
        MonoBehaviour[] beamBehaviours = beamObj.GetComponents<MonoBehaviour>();
        for (int i = 0; i < beamBehaviours.Length; i++)
        {
            if (beamBehaviours[i] is IBeamRuntime3D runtime)
            {
                _activeBeam = runtime;
                _activeBeamComponent = beamBehaviours[i];
                break;
            }
        }

        if (_activeBeam == null || _activeBeamComponent == null)
        {
            Destroy(beamObj);
            return;
        }

        Transform muzzle = beam.muzzle != null ? beam.muzzle : Owner != null ? Owner.transform : transform;
        string resolvedTargetTag = beam.targetTag;
        if (authoritative
            && beam.targetFaction == Faction3D.Neutral
            && NetTickUtil.IsActive
            && _netCombat != null
            && _netCombat.IsSpawned)
        {
            string enemyTag = _netCombat.GetEnemyTag();
            if (!string.IsNullOrEmpty(enemyTag))
            {
                resolvedTargetTag = enemyTag;
            }
        }
        _activeBeam.Initialize(
            resolvedTargetTag,
            beam.targetFaction,
            beam.damagePerSecond,
            beam.maxDistance,
            beam.recoilForcePerSecond,
            beam.impactForce,
            Owner,
            muzzle,
            beam.offsetDistance,
            beam.verticalOffset,
            AimCamera);
        if (_activeBeam is IBeamDirectionSource3D directionSourceConsumer)
        {
            directionSourceConsumer.SetBeamDirectionSource(ResolveBeamDirectionSource());
        }

        _activeBeamAuthoritative = authoritative;
        _activeBeamAttackId = authoritative ? accuracyAttackId : PlayerCombatStats3D.InvalidAttackId;
        if (authoritative)
        {
            Owner?.GetComponent<PlayerCombatStats3D>()?.RecordTrackedAttackFired(_activeBeamAttackId);
        }

        _activeBeam.SetCosmeticOnly(!authoritative);
        _activeBeam.SetNetworkAuthority(authoritative ? _netCombat : null);
        _activeBeam.SetAccuracyAttackId(_activeBeamAttackId);
        if (_hasPendingNetworkAim)
        {
            _activeBeam.SetNetworkAim(_pendingNetworkAim);
        }
        _activeBeam.StartFiring();
        Owner?.RecordCombatActivity();
        StartBeamLoopSound();
    }

    private void StopBeam()
    {
        if (_activeBeam != null)
        {
            _activeBeam.StopFiring();
            Destroy(_activeBeamComponent.gameObject);
            _activeBeam = null;
            _activeBeamComponent = null;
        }

        if (beam.postFireRotationPenaltyDuration > 0f)
        {
            _rotationPenaltyUntilTime = Time.time + beam.postFireRotationPenaltyDuration;
        }
        else
        {
            _rotationPenaltyUntilTime = float.NegativeInfinity;
        }

        _activeBeamAuthoritative = true;
        _activeBeamAttackId = PlayerCombatStats3D.InvalidAttackId;
        _hasPendingNetworkAim = false;
        Owner?.RecordCombatActivity();
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

    private Transform ResolveBeamDirectionSource()
    {
        if (beam.directionReference != null)
        {
            return beam.directionReference;
        }

        if (beam.muzzle != null)
        {
            return beam.muzzle;
        }

        return Owner != null ? Owner.transform : transform;
    }
}
