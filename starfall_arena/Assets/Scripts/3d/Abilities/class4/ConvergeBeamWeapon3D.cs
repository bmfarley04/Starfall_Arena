using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConvergeBeamWeapon3D : Weapon3D, IBeamWeaponNetwork3D
{
    [System.Serializable]
    public struct ConvergeBeamConfig3D
    {
        [Header("Beam Settings")]
        public GameObject beamPrefab;
        public string targetTag;
        public float damagePerSecond;
        public float maxDistance;
        public float recoilForcePerSecond;
        public float impactForce;
        public float offsetDistance;
        public float verticalOffset;
        public float rotationMultiplier;

        [Header("Hardpoints")]
        public Transform[] hardpoints;
        public Vector3[] fallbackLocalOffsets;

        [Header("Empowerment")]
        public Empower3D empowerAbility;
        public bool forceEmpowered;
        public int baseBeamCount;
        public int empoweredBeamCount;

        [Header("Aiming")]
        public LayerMask aimCollisionMask;
        public float maxAimDistance;
        public float fallbackAimDistance;
        [Range(0f, 1f)]
        public float screenCenterDirectionBlend;

        [Header("Beam Capacity")]
        public float capacity;
        public float drainRate;
        public float regenRate;

        [Header("Sound Effects")]
        public SoundEffect fireLoopSound;
    }

    [Header("Class4 Converge Beam")]
    [SerializeField] private ConvergeBeamConfig3D convergeBeam = new ConvergeBeamConfig3D
    {
        damagePerSecond = 8f,
        maxDistance = 60f,
        recoilForcePerSecond = 1f,
        impactForce = 2f,
        rotationMultiplier = 0.2f,
        baseBeamCount = 2,
        empoweredBeamCount = 4,
        aimCollisionMask = ~0,
        maxAimDistance = 1000f,
        fallbackAimDistance = 150f,
        screenCenterDirectionBlend = 0.35f,
        capacity = 100f,
        drainRate = 25f,
        regenRate = 4f
    };
    [SerializeField] private AudioSource beamLoopAudioSource;

    private LaserBeam3D[] _activeBeams;
    private Transform[] _activeHardpoints = System.Array.Empty<Transform>();
    private readonly List<GameObject> _runtimeFallbackHardpoints = new List<GameObject>();
    private NetCombat3D _netCombat;
    private bool _activeBeamAuthoritative = true;
    private Vector3 _pendingNetworkAimDirection;
    private bool _hasPendingNetworkAim;
    private int _activeBeamAttackId = PlayerCombatStats3D.InvalidAttackId;
    private bool _lastEmpoweredState;

    private bool UsesBeamCapacity => convergeBeam.capacity > 0f && convergeBeam.drainRate > 0f;
    private bool IsBeamActive => _activeBeams != null && _activeBeams.Length > 0;

    protected override void Awake()
    {
        base.Awake();
        _netCombat ??= GetComponent<NetCombat3D>();
        if (convergeBeam.empowerAbility == null)
        {
            convergeBeam.empowerAbility = GetComponent<Empower3D>();
        }

        if (beamLoopAudioSource == null)
        {
            beamLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        beamLoopAudioSource.playOnAwake = false;
        beamLoopAudioSource.loop = true;
        beamLoopAudioSource.spatialBlend = 1f;
        beamLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
        _lastEmpoweredState = IsEmpoweredActive();
    }

    protected override float GetConfiguredResourceCapacity()
    {
        return convergeBeam.capacity;
    }

    protected override float GetConfiguredResourceRecoveryPerSecond()
    {
        return convergeBeam.regenRate;
    }

    protected override bool ShouldRecoverResource()
    {
        return !IsBeamActive;
    }

    protected override void OnFirePressed()
    {
        if (IsBeamActive || convergeBeam.beamPrefab == null)
        {
            return;
        }

        if (UsesBeamCapacity && !CanSpendResource(convergeBeam.drainRate * Time.fixedDeltaTime))
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

    protected override void OnWeaponUpdated(float deltaTime)
    {
        if (!IsBeamActive)
        {
            return;
        }

        bool empoweredState = IsEmpoweredActive();
        if (empoweredState != _lastEmpoweredState)
        {
            RebuildActiveBeams();
        }

        UpdateBeamAimDirections();
    }

    protected override void OnWeaponFixedUpdated(float deltaTime)
    {
        if (!IsBeamActive)
        {
            return;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned && _netCombat.IsOwner)
        {
            _netCombat.UpdateBeamAim(ResolveOwnerAimDirection());
        }

        float recoilForceThisFrame = Mathf.Max(0f, convergeBeam.recoilForcePerSecond) * Mathf.Max(0, _activeBeams.Length) * deltaTime;
        if (_activeBeamAuthoritative && Owner != null && Owner.Flight != null && recoilForceThisFrame > 0f)
        {
            Owner.Flight.ApplyRecoil(recoilForceThisFrame);
            _netCombat?.ApplyCombatVelocityDelta(-transform.forward * recoilForceThisFrame);
        }

        if (!UsesBeamCapacity)
        {
            return;
        }

        AddResourceUsage(convergeBeam.drainRate * deltaTime);
        if (CurrentResourceUsage >= Mathf.Max(0f, convergeBeam.capacity) - 0.001f)
        {
            StopBeam();
        }
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        StopBeam();
    }

    public override void Die()
    {
        StopBeam();
    }

    public override float GetRotationMultiplier()
    {
        return IsBeamActive ? convergeBeam.rotationMultiplier : 1f;
    }

    public override bool IsReticleSpinActive()
    {
        return IsBeamActive;
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

    public void ApplyNetworkBeamAim(Vector3 aimDirection)
    {
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _pendingNetworkAimDirection = aimDirection.normalized;
        _hasPendingNetworkAim = true;
        UpdateBeamAimDirections();
    }

    private void StartBeam(bool authoritative, int accuracyAttackId)
    {
        _activeBeamAuthoritative = authoritative;
        _activeBeamAttackId = authoritative ? accuracyAttackId : PlayerCombatStats3D.InvalidAttackId;
        _lastEmpoweredState = IsEmpoweredActive();

        if (IsBeamActive)
        {
            RebuildActiveBeams();
            return;
        }

        BuildActiveBeams();
        if (_activeBeamAuthoritative)
        {
            Owner?.GetComponent<PlayerCombatStats3D>()?.RecordTrackedAttackFired(_activeBeamAttackId);
        }

        StartBeamLoopSound();
    }

    private void RebuildActiveBeams()
    {
        bool wasActive = IsBeamActive;
        bool authoritative = _activeBeamAuthoritative;
        int accuracyAttackId = _activeBeamAttackId;
        DestroyBeamObjects();
        if (wasActive)
        {
            _activeBeamAuthoritative = authoritative;
            _activeBeamAttackId = accuracyAttackId;
            _lastEmpoweredState = IsEmpoweredActive();
            BuildActiveBeams();
        }
    }

    private void BuildActiveBeams()
    {
        _activeHardpoints = ResolveActiveHardpoints();
        if (_activeHardpoints.Length == 0 || convergeBeam.beamPrefab == null)
        {
            _activeBeams = null;
            return;
        }

        _activeBeams = new LaserBeam3D[_activeHardpoints.Length];
        string resolvedTargetTag = convergeBeam.targetTag;
        if (_activeBeamAuthoritative && NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned)
        {
            string enemyTag = _netCombat.GetEnemyTag();
            if (!string.IsNullOrEmpty(enemyTag))
            {
                resolvedTargetTag = enemyTag;
            }
        }

        for (int i = 0; i < _activeHardpoints.Length; i++)
        {
            Transform hardpoint = _activeHardpoints[i];
            if (hardpoint == null)
            {
                continue;
            }

            GameObject beamObject = Instantiate(convergeBeam.beamPrefab, Vector3.zero, Quaternion.identity);
            LaserBeam3D beam = beamObject.GetComponent<LaserBeam3D>();
            if (beam == null)
            {
                Destroy(beamObject);
                continue;
            }

            beam.Initialize(
                resolvedTargetTag,
                convergeBeam.damagePerSecond,
                convergeBeam.maxDistance,
                convergeBeam.recoilForcePerSecond,
                convergeBeam.impactForce,
                Owner,
                hardpoint,
                convergeBeam.offsetDistance,
                convergeBeam.verticalOffset,
                null);

            beam.SetCosmeticOnly(!_activeBeamAuthoritative);
            beam.SetNetworkAuthority(_activeBeamAuthoritative ? _netCombat : null);
            beam.SetAccuracyAttackId(_activeBeamAttackId);
            beam.StartFiring();
            _activeBeams[i] = beam;
        }

        UpdateBeamAimDirections();
    }

    private void StopBeam()
    {
        DestroyBeamObjects();
        _activeBeamAuthoritative = true;
        _activeBeamAttackId = PlayerCombatStats3D.InvalidAttackId;
        _hasPendingNetworkAim = false;
        StopBeamLoopSound();
    }

    private void DestroyBeamObjects()
    {
        if (_activeBeams != null)
        {
            for (int i = 0; i < _activeBeams.Length; i++)
            {
                if (_activeBeams[i] == null)
                {
                    continue;
                }

                _activeBeams[i].StopFiring();
                Destroy(_activeBeams[i].gameObject);
            }
        }

        _activeBeams = null;
        _activeHardpoints = System.Array.Empty<Transform>();
        ClearRuntimeFallbackHardpoints();
    }

    private Transform[] ResolveActiveHardpoints()
    {
        int desiredCount = Mathf.Max(1, IsEmpoweredActive() ? convergeBeam.empoweredBeamCount : convergeBeam.baseBeamCount);
        if (convergeBeam.hardpoints != null && convergeBeam.hardpoints.Length > 0)
        {
            int count = Mathf.Min(desiredCount, convergeBeam.hardpoints.Length);
            Transform[] result = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = convergeBeam.hardpoints[i];
            }

            return result;
        }

        ClearRuntimeFallbackHardpoints();
        Vector3[] fallbackOffsets = convergeBeam.fallbackLocalOffsets;
        if (fallbackOffsets == null || fallbackOffsets.Length == 0)
        {
            fallbackOffsets = new[]
            {
                new Vector3(-0.85f, 0f, 4.5f),
                new Vector3(0.85f, 0f, 4.5f),
                new Vector3(-1.7f, 0f, 4f),
                new Vector3(1.7f, 0f, 4f)
            };
        }

        int fallbackCount = Mathf.Min(desiredCount, fallbackOffsets.Length);
        Transform[] runtimeHardpoints = new Transform[fallbackCount];
        for (int i = 0; i < fallbackCount; i++)
        {
            GameObject hardpoint = new GameObject($"ConvergeBeamHardpoint_{i + 1}");
            hardpoint.transform.SetParent(transform, false);
            hardpoint.transform.localPosition = fallbackOffsets[i];
            runtimeHardpoints[i] = hardpoint.transform;
            _runtimeFallbackHardpoints.Add(hardpoint);
        }

        return runtimeHardpoints;
    }

    private void UpdateBeamAimDirections()
    {
        if (!IsBeamActive || _activeHardpoints == null)
        {
            return;
        }

        Vector3 aimPoint = ResolveAimPoint();
        Vector3 fallbackDirection = ResolveOwnerAimDirection();

        for (int i = 0; i < _activeBeams.Length; i++)
        {
            LaserBeam3D beam = _activeBeams[i];
            Transform hardpoint = i < _activeHardpoints.Length ? _activeHardpoints[i] : null;
            if (beam == null || hardpoint == null)
            {
                continue;
            }

            Vector3 direction = aimPoint - hardpoint.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackDirection;
            }

            beam.SetNetworkAim(direction.normalized);
        }
    }

    private Vector3 ResolveOwnerAimDirection()
    {
        if (ShouldUseReplicatedAim())
        {
            return _pendingNetworkAimDirection;
        }

        Camera cam = AimCamera;
        if (cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (centerRay.direction.sqrMagnitude > 0.0001f)
            {
                return centerRay.direction.normalized;
            }
        }

        return transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
    }

    private Vector3 ResolveAimPoint()
    {
        Vector3 origin = transform.position;

        if (ShouldUseReplicatedAim())
        {
            return origin + (_pendingNetworkAimDirection * Mathf.Max(1f, convergeBeam.fallbackAimDistance));
        }

        Camera cam = AimCamera;
        if (cam == null)
        {
            return origin + (ResolveOwnerAimDirection() * Mathf.Max(1f, convergeBeam.fallbackAimDistance));
        }

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxAimDistance = convergeBeam.maxAimDistance > 0f ? convergeBeam.maxAimDistance : 1000f;
        float fallbackAimDistance = convergeBeam.fallbackAimDistance > 0f ? convergeBeam.fallbackAimDistance : 150f;
        Vector3 rayPoint = centerRay.origin + (centerRay.direction * Mathf.Max(fallbackAimDistance, maxAimDistance));

        if (Physics.Raycast(centerRay, out RaycastHit hit, maxAimDistance, convergeBeam.aimCollisionMask, QueryTriggerInteraction.Ignore))
        {
            rayPoint = hit.point;
        }

        return rayPoint;
    }

    private bool ShouldUseReplicatedAim()
    {
        return _hasPendingNetworkAim
            && NetTickUtil.IsActive
            && _netCombat != null
            && _netCombat.IsSpawned
            && !_netCombat.IsOwner;
    }

    private bool IsEmpoweredActive()
    {
        if (convergeBeam.forceEmpowered)
        {
            return true;
        }

        return convergeBeam.empowerAbility != null && convergeBeam.empowerAbility.IsEmpoweredActive;
    }

    private void ClearRuntimeFallbackHardpoints()
    {
        for (int i = 0; i < _runtimeFallbackHardpoints.Count; i++)
        {
            if (_runtimeFallbackHardpoints[i] != null)
            {
                Destroy(_runtimeFallbackHardpoints[i]);
            }
        }

        _runtimeFallbackHardpoints.Clear();
    }

    private void OnDisable()
    {
        StopBeam();
    }

    private void StartBeamLoopSound()
    {
        if (convergeBeam.fireLoopSound == null || beamLoopAudioSource == null)
        {
            return;
        }

        if (beamLoopAudioSource.isPlaying && beamLoopAudioSource.clip == convergeBeam.fireLoopSound.clip)
        {
            return;
        }

        beamLoopAudioSource.loop = true;
        convergeBeam.fireLoopSound.Play(beamLoopAudioSource);
    }

    private void StopBeamLoopSound()
    {
        if (beamLoopAudioSource != null && beamLoopAudioSource.isPlaying)
        {
            beamLoopAudioSource.Stop();
        }
    }
}
