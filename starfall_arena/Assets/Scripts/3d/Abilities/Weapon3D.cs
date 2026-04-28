using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AimAssistStrength3D
{
    BarelyThere,
    Subtle,
    Light
}

[DisallowMultipleComponent]
public class AimAssist3D : MonoBehaviour
{
    [System.Serializable]
    private struct AimAssistPresetTuning3D
    {
        [Range(0f, 1f)] public float slowdownMultiplier;
        [Range(0f, 45f)] public float assistConeAngle;
        public float maxAssistRange;
        [Range(0f, 1f)] public float screenDistanceWeight;
        [Range(0f, 25f)] public float maxAngularCorrection;
    }

    [System.Serializable]
    private struct AimAssistPresetSet3D
    {
        public AimAssistPresetTuning3D barelyThere;
        public AimAssistPresetTuning3D subtle;
        public AimAssistPresetTuning3D light;
    }

    [Header("Aim Assist")]
    [SerializeField] private bool aimAssistEnabled = true;
    [SerializeField] private AimAssistStrength3D strengthPreset = AimAssistStrength3D.Subtle;

    [Header("Targeting")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Entity3D owner;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private Faction3D targetFaction = Faction3D.EnemyTeam;
    [SerializeField] private float minimumDetectionRadius = 1.25f;
    [SerializeField] private float lineOfSightPadding = 0.05f;

    [Header("Preset Tuning")]
    [SerializeField] private AimAssistPresetSet3D presets = new AimAssistPresetSet3D
    {
        barelyThere = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.92f,
            assistConeAngle = 3.5f,
            maxAssistRange = 90f,
            screenDistanceWeight = 0.85f,
            maxAngularCorrection = 1.25f
        },
        subtle = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.8f,
            assistConeAngle = 6f,
            maxAssistRange = 120f,
            screenDistanceWeight = 0.75f,
            maxAngularCorrection = 2.5f
        },
        light = new AimAssistPresetTuning3D
        {
            slowdownMultiplier = 0.68f,
            assistConeAngle = 8f,
            maxAssistRange = 145f,
            screenDistanceWeight = 0.65f,
            maxAngularCorrection = 4f
        }
    };

    private const string ControllerScheme = "controller";

    private struct TargetCandidate
    {
        public Entity3D entity;
        public Vector3 aimPoint;
        public float score;
    }

    private void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        playerInput ??= GetComponent<PlayerInput>();
        aimCamera ??= Camera.main;
        ValidateTuning();
    }

    private void OnValidate()
    {
        ValidateTuning();
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
    }

    public bool IsControllerAimAssistActive()
    {
        if (!aimAssistEnabled)
        {
            return false;
        }

        if (playerInput == null)
        {
            return Gamepad.current != null;
        }

        return string.Equals(playerInput.currentControlScheme, ControllerScheme, StringComparison.OrdinalIgnoreCase);
    }

    public float GetLookSlowdownMultiplier()
    {
        return TryGetBestTarget(out _, out _)
            ? Mathf.Clamp01(GetActivePreset().slowdownMultiplier)
            : 1f;
    }

    public bool TryGetAssistedAimDirection(Vector3 origin, Vector3 baseDirection, out Vector3 assistedDirection)
    {
        assistedDirection = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : transform.forward;
        if (!IsControllerAimAssistActive())
        {
            return false;
        }

        if (!TryGetBestTarget(origin, assistedDirection, out TargetCandidate candidate))
        {
            return false;
        }

        Vector3 toTarget = candidate.aimPoint - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 targetDirection = toTarget.normalized;
        float angleToTarget = Vector3.Angle(assistedDirection, targetDirection);
        float maxCorrection = Mathf.Max(0f, GetActivePreset().maxAngularCorrection);
        if (maxCorrection <= 0.001f)
        {
            return false;
        }

        float t = Mathf.Clamp01(maxCorrection / Mathf.Max(0.001f, angleToTarget));
        assistedDirection = Vector3.Slerp(assistedDirection, targetDirection, t).normalized;
        return true;
    }

    public bool TryGetBestTarget(out Entity3D entity, out Vector3 aimPoint)
    {
        entity = null;
        aimPoint = Vector3.zero;

        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null || !IsControllerAimAssistActive())
        {
            return false;
        }

        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!TryGetBestTarget(centerRay.origin, centerRay.direction, out TargetCandidate candidate))
        {
            return false;
        }

        entity = candidate.entity;
        aimPoint = candidate.aimPoint;
        return true;
    }

    private bool TryGetBestTarget(Vector3 origin, Vector3 forward, out TargetCandidate bestCandidate)
    {
        bestCandidate = default;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        AimAssistPresetTuning3D preset = GetActivePreset();
        float maxRange = Mathf.Max(1f, preset.maxAssistRange);
        float maxAngle = Mathf.Max(0f, preset.assistConeAngle);
        float sphereRadius = Mathf.Max(minimumDetectionRadius, Mathf.Tan(maxAngle * Mathf.Deg2Rad) * maxRange);
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            sphereRadius,
            forward.normalized,
            maxRange,
            targetMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Entity3D candidateEntity = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Entity3D>() : null;
            if (!IsValidTarget(candidateEntity))
            {
                continue;
            }

            Vector3 candidateAimPoint = ResolveAimPoint(candidateEntity);
            Vector3 toTarget = candidateAimPoint - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f || distance > maxRange)
            {
                continue;
            }

            Vector3 targetDirection = toTarget / distance;
            float angle = Vector3.Angle(forward, targetDirection);
            if (angle > maxAngle)
            {
                continue;
            }

            if (!HasLineOfSight(origin, candidateAimPoint, candidateEntity))
            {
                continue;
            }

            float angleScore = 1f - Mathf.Clamp01(angle / Mathf.Max(0.001f, maxAngle));
            float distanceScore = 1f - Mathf.Clamp01(distance / maxRange);
            float score = Mathf.Lerp(distanceScore, angleScore, Mathf.Clamp01(preset.screenDistanceWeight));
            score += 0.0001f * (10000f - distance);

            if (!found || score > bestScore)
            {
                bestScore = score;
                bestCandidate = new TargetCandidate
                {
                    entity = candidateEntity,
                    aimPoint = candidateAimPoint,
                    score = score
                };
                found = true;
            }
        }

        return found;
    }

    private bool IsValidTarget(Entity3D candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        if (!candidate.gameObject.activeInHierarchy || candidate.CurrentHealth <= 0f)
        {
            return false;
        }

        if (targetFaction != Faction3D.Neutral && FactionMember3D.ResolveFaction(candidate) != targetFaction)
        {
            return false;
        }

        if (owner != null && FactionMember3D.AreAllied(owner, candidate))
        {
            return false;
        }

        return true;
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 aimPoint, Entity3D candidate)
    {
        Vector3 toTarget = aimPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance + Mathf.Max(0f, lineOfSightPadding), lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Entity3D hitEntity = hit.collider != null ? hit.collider.GetComponentInParent<Entity3D>() : null;
        return hitEntity == candidate;
    }

    private static Vector3 ResolveAimPoint(Entity3D entity)
    {
        Collider collider = entity.GetComponentInChildren<Collider>();
        return collider != null ? collider.bounds.center : entity.transform.position;
    }

    private AimAssistPresetTuning3D GetActivePreset()
    {
        return strengthPreset switch
        {
            AimAssistStrength3D.BarelyThere => presets.barelyThere,
            AimAssistStrength3D.Light => presets.light,
            _ => presets.subtle
        };
    }

    private void ValidateTuning()
    {
        presets.barelyThere = ValidatePreset(presets.barelyThere);
        presets.subtle = ValidatePreset(presets.subtle);
        presets.light = ValidatePreset(presets.light);
    }

    private static AimAssistPresetTuning3D ValidatePreset(AimAssistPresetTuning3D preset)
    {
        preset.slowdownMultiplier = Mathf.Clamp01(preset.slowdownMultiplier);
        preset.assistConeAngle = Mathf.Clamp(preset.assistConeAngle, 0f, 45f);
        preset.maxAssistRange = Mathf.Max(1f, preset.maxAssistRange);
        preset.screenDistanceWeight = Mathf.Clamp01(preset.screenDistanceWeight);
        preset.maxAngularCorrection = Mathf.Clamp(preset.maxAngularCorrection, 0f, 25f);
        return preset;
    }
}

public abstract class Weapon3D : MonoBehaviour, IReticleSpinSource3D
{
    public enum AvailabilityMode3D
    {
        ResourceConsumption,
        Cooldown
    }

    [System.Serializable]
    private struct ResourceConfig3D
    {
        public float capacity;
        public float recoveryPerSecond;
    }

    protected struct AimSolution
    {
        public Vector3 point;
        public Vector3 direction;
    }

    [Header("Weapon Core")]
    [SerializeField] private AvailabilityMode3D availabilityMode = AvailabilityMode3D.ResourceConsumption;
    [SerializeField] private Color reticleFillColor = Color.white;
    [SerializeField] protected Entity3D owner;
    [SerializeField] protected ShipFlight3D shipFlight;

    [Header("Resource Availability")]
    [SerializeField] private ResourceConfig3D resource = new ResourceConfig3D
    {
        capacity = 100f,
        recoveryPerSecond = 30f
    };

    [Header("Aiming")]
    [SerializeField] private ProjectileAimMode3D aimMode = ProjectileAimMode3D.ScreenCenter;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private AimAssist3D aimAssist;
    [SerializeField] private LayerMask aimCollisionMask = ~0;
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private float screenCenterConvergenceDistance = 150f;
    [Range(0f, 1f)]
    [SerializeField] private float screenCenterDirectionBlend = 0.35f;

    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleEffectPrefab;
    [SerializeField] private float muzzleEffectLifetime = 2f;
    [SerializeField] private bool parentMuzzleEffectToMuzzle = true;
    [SerializeField] private Vector3 projectileSpawnLocalOffset = new Vector3(0f, 0f, 2f);
    [SerializeField] private Vector3 muzzleEffectLocalOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Pooling")]
    [SerializeField] private int projectilePrewarmCount = 12;
    [SerializeField] private int muzzleEffectPrewarmCount = 4;

    private bool _isFireHeld;
    private float _currentResourceUsage;
    private float _cooldownReadyTime = float.NegativeInfinity;
    private float _lastReticleSpinPulseTime = float.NegativeInfinity;
    private float _lastAvailabilityChangedReadyRatio = float.NaN;
    private bool _lastAvailabilityChangedOnCooldown;
    private bool _hasAvailabilitySnapshot;
    private NetCombat3D _netCombat;

    public event Action<Weapon3D> AvailabilityChanged;

    public AvailabilityMode3D AvailabilityMode => availabilityMode;
    public Entity3D Owner => owner;
    public ShipFlight3D ShipFlight => shipFlight;
    public Color ReticleFillColor => reticleFillColor;
    public Camera AimCamera => aimCamera;
    protected AimAssist3D AimAssist => aimAssist;
    public bool IsFireHeld => _isFireHeld;
    public float CurrentResourceUsage => _currentResourceUsage;
    public float ResourceCapacity => Mathf.Max(0f, GetConfiguredResourceCapacity());
    public float AvailableResourceRatio => GetAvailableResourceRatio();
    public float CooldownRemaining => UsesCooldownAvailability ? Mathf.Max(0f, _cooldownReadyTime - Time.time) : 0f;
    public float CooldownDuration => Mathf.Max(0f, GetConfiguredCooldownDuration());
    public float CooldownReadyRatio => GetCooldownReadyRatio();
    public bool UsesResourceAvailability => availabilityMode == AvailabilityMode3D.ResourceConsumption;
    public bool UsesCooldownAvailability => availabilityMode == AvailabilityMode3D.Cooldown;
    protected virtual bool SupportsMuzzleEffects => true;

    protected virtual void Awake()
    {
        owner ??= GetComponent<Entity3D>();
        shipFlight ??= GetComponent<ShipFlight3D>();
        _netCombat ??= GetComponent<NetCombat3D>();
        aimAssist ??= GetComponent<AimAssist3D>();
        aimCamera ??= Camera.main;

        foreach (GameObject projectilePrefab in GetPrewarmProjectilePrefabs())
        {
            if (projectilePrefab != null)
            {
                GameObjectPool3D.Prewarm(projectilePrefab, projectilePrewarmCount);
            }
        }

        if (SupportsMuzzleEffects && muzzleEffectPrefab != null)
        {
            GameObjectPool3D.Prewarm(muzzleEffectPrefab, muzzleEffectPrewarmCount);
        }

        CacheAvailabilitySnapshot();
    }

    private void Update()
    {
        RecoverResource(Time.deltaTime);

        if (_isFireHeld)
        {
            OnFireHeld();
        }

        OnWeaponUpdated(Time.deltaTime);
        RaiseAvailabilityChangedIfNeeded();
    }

    private void FixedUpdate()
    {
        OnWeaponFixedUpdated(Time.fixedDeltaTime);
    }

    public virtual void SetFireHeld(bool isHeld)
    {
        if (_isFireHeld == isHeld)
        {
            return;
        }

        _isFireHeld = isHeld;
        if (_isFireHeld)
        {
            OnFirePressed();
        }
        else
        {
            OnFireReleased();
        }
    }

    public virtual void OnSelected()
    {
    }

    public virtual void OnDeselected()
    {
        SetFireHeld(false);
    }

    public virtual void Die()
    {
    }

    public virtual float GetReticleFillRatio()
    {
        if (UsesResourceAvailability)
        {
            float capacity = ResourceCapacity;
            return capacity > 0f ? Mathf.Clamp01(_currentResourceUsage / capacity) : 0f;
        }

        float cooldown = CooldownDuration;
        if (cooldown <= 0f || !UsesCooldownAvailability)
        {
            return 0f;
        }

        return Mathf.Clamp01(CooldownRemaining / cooldown);
    }

    public virtual bool IsReticleSpinActive()
    {
        return false;
    }

    public float GetReticleSpinPulseTime()
    {
        return _lastReticleSpinPulseTime;
    }

    public virtual float GetRotationMultiplier()
    {
        return 1f;
    }

    public virtual float GetThrustMultiplier()
    {
        return 1f;
    }

    public virtual Ray GetAimRay()
    {
        if (aimMode == ProjectileAimMode3D.ScreenCenter)
        {
            return GetScreenCenterAimRay();
        }

        return new Ray(transform.position, transform.forward);
    }

    public Ray GetScreenCenterAimRay()
    {
        Camera resolvedCamera = aimCamera != null ? aimCamera : Camera.main;
        if (resolvedCamera == null)
        {
            return new Ray(transform.position, transform.forward);
        }

        return resolvedCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    public void SetOwner(Entity3D newOwner)
    {
        owner = newOwner;
    }

    public void SetShipFlight(ShipFlight3D flight)
    {
        shipFlight = flight;
    }

    public void SetAimCamera(Camera camera)
    {
        aimCamera = camera;
        aimAssist?.SetAimCamera(camera);
    }

    protected virtual Vector3 ResolveOwnerAimDirection()
    {
        Ray aimRay = GetAimRay();
        Vector3 direction = aimRay.direction.sqrMagnitude > 0.0001f ? aimRay.direction.normalized : transform.forward;
        if (aimAssist != null)
        {
            aimAssist.TryGetAssistedAimDirection(aimRay.origin, direction, out direction);
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    protected void SetAvailabilityMode(AvailabilityMode3D mode)
    {
        if (availabilityMode == mode)
        {
            return;
        }

        availabilityMode = mode;
        if (availabilityMode != AvailabilityMode3D.ResourceConsumption)
        {
            _currentResourceUsage = 0f;
        }

        if (availabilityMode != AvailabilityMode3D.Cooldown)
        {
            _cooldownReadyTime = float.NegativeInfinity;
        }

        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected virtual IEnumerable<GameObject> GetPrewarmProjectilePrefabs()
    {
        yield break;
    }

    protected virtual float GetConfiguredCooldownDuration()
    {
        return 0f;
    }

    protected virtual float GetConfiguredResourceCapacity()
    {
        return resource.capacity;
    }

    protected virtual float GetConfiguredResourceRecoveryPerSecond()
    {
        return resource.recoveryPerSecond;
    }

    protected virtual bool ShouldRecoverResource()
    {
        return true;
    }

    protected virtual void OnFirePressed()
    {
    }

    protected virtual void OnFireHeld()
    {
    }

    protected virtual void OnFireReleased()
    {
    }

    protected virtual void OnWeaponUpdated(float deltaTime)
    {
    }

    protected virtual void OnWeaponFixedUpdated(float deltaTime)
    {
    }

    protected bool IsOnCooldown()
    {
        return UsesCooldownAvailability && Time.time < _cooldownReadyTime;
    }

    protected void StartCooldown(float duration = -1f)
    {
        float resolvedDuration = duration >= 0f ? duration : GetConfiguredCooldownDuration();
        _cooldownReadyTime = Time.time + Mathf.Max(0f, resolvedDuration);
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected bool CanSpendResource(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return true;
        }

        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            return false;
        }

        float clampedAmount = Mathf.Max(0f, amount);
        return _currentResourceUsage + clampedAmount <= capacity + 0.001f;
    }

    protected bool TrySpendResource(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return true;
        }

        float clampedAmount = Mathf.Max(0f, amount);
        if (!CanSpendResource(clampedAmount))
        {
            return false;
        }

        if (clampedAmount <= 0f)
        {
            return true;
        }

        SetResourceUsage(_currentResourceUsage + clampedAmount);
        return true;
    }

    protected void AddResourceUsage(float amount)
    {
        if (!UsesResourceAvailability)
        {
            return;
        }

        SetResourceUsage(_currentResourceUsage + Mathf.Max(0f, amount));
    }

    protected void SetResourceUsage(float amount)
    {
        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            _currentResourceUsage = 0f;
            RaiseAvailabilityChangedIfNeeded(force: true);
            return;
        }

        _currentResourceUsage = Mathf.Clamp(amount, 0f, capacity);
        RaiseAvailabilityChangedIfNeeded(force: true);
    }

    protected void RecordReticleSpinPulse()
    {
        _lastReticleSpinPulseTime = Time.time;
    }

    protected void NormalizePlayerProjectileTargeting(ref ProjectileFireRequest3D request)
    {
        if (owner is not Player3D)
        {
            return;
        }

        bool hasEnemyTeamTargets = SceneHasFactionTargets(Faction3D.EnemyTeam);
        bool hasDuelOpponentTag = TryResolveOpponentPlayerTag(out string opponentPlayerTag);

        if (request.targetFaction == Faction3D.EnemyTeam)
        {
            if (!hasEnemyTeamTargets && hasDuelOpponentTag)
            {
                request.targetFaction = Faction3D.Neutral;
                request.targetTag = opponentPlayerTag;
            }
            else if (hasEnemyTeamTargets && string.IsNullOrEmpty(request.targetTag))
            {
                request.targetTag = "Enemy";
            }

            return;
        }

        if (request.targetFaction != Faction3D.Neutral)
        {
            return;
        }

        bool usesGenericEnemyTag = string.IsNullOrEmpty(request.targetTag) || request.targetTag == "Enemy";
        if (!usesGenericEnemyTag)
        {
            return;
        }

        if (hasEnemyTeamTargets)
        {
            request.targetFaction = Faction3D.EnemyTeam;
            request.targetTag = "Enemy";
            return;
        }

        if (hasDuelOpponentTag)
        {
            request.targetTag = opponentPlayerTag;
        }
    }

    protected ProjectileFireRequest3D BuildDefaultFireRequest(ProjectileWeaponConfig3D weaponConfig)
    {
        return new ProjectileFireRequest3D
        {
            projectilePrefab = weaponConfig.projectilePrefab,
            muzzles = weaponConfig.muzzles,
            spawnAnchor = null,
            targetTag = weaponConfig.targetTag,
            targetFaction = weaponConfig.targetFaction,
            speed = weaponConfig.speed,
            damage = weaponConfig.damage,
            lifetime = weaponConfig.lifetime,
            impactForce = weaponConfig.impactForce,
            recoilForce = weaponConfig.recoilForce,
            forwardOffset = 0f,
            verticalOffset = 0f,
            projectileScaleMultiplier = 1f,
            accuracyAttackIdOverride = PlayerCombatStats3D.InvalidAttackId
        };
    }

    protected bool FireProjectilePattern(ProjectileFireRequest3D request, ProjectileWeaponConfig3D fallbackConfig, SoundEffect fireSound = null)
    {
        if (request.projectilePrefab == null)
        {
            return false;
        }

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsSpawned)
        {
            return _netCombat.TryFireProjectilePattern(this, request, fallbackConfig, fireSound);
        }

        return FireProjectilePatternLocal(request, fallbackConfig, fireSound, cosmeticOnly: false, networkAuthority: null, visualType: NetProjectileVisualType3D.Primary);
    }

    internal bool FireProjectilePatternLocal(
        ProjectileFireRequest3D request,
        ProjectileWeaponConfig3D fallbackConfig,
        SoundEffect fireSound,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        NetProjectileVisualType3D visualType)
    {
        if (request.projectilePrefab == null)
        {
            return false;
        }

        Transform[] muzzles = ResolveFiringMuzzles(request, fallbackConfig);
        PlayerCombatStats3D stats = !cosmeticOnly && owner != null ? owner.GetComponent<PlayerCombatStats3D>() : null;
        int accuracyAttackId = request.accuracyAttackIdOverride != PlayerCombatStats3D.InvalidAttackId
            ? request.accuracyAttackIdOverride
            : stats != null
                ? stats.BeginTrackedAttack()
                : PlayerCombatStats3D.InvalidAttackId;

        string resolvedTargetTag = !string.IsNullOrEmpty(request.targetTag)
            ? request.targetTag
            : fallbackConfig.targetTag;

        AimSolution aim = ResolveAimSolution();
        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            SpawnMuzzleEffect(spawnMuzzle);
            SpawnProjectile(spawnMuzzle, aim, request, resolvedTargetTag, cosmeticOnly, networkAuthority, visualType, accuracyAttackId);
        }

        if (!cosmeticOnly && shipFlight != null && request.recoilForce > 0f)
        {
            shipFlight.ApplyRecoil(request.recoilForce);
            networkAuthority?.ApplyCombatVelocityDelta(-transform.forward * request.recoilForce);
        }

        fireSound?.PlayAtPoint(transform.position);
        RecordReticleSpinPulse();
        owner?.RecordCombatActivity();
        return true;
    }

    internal void BuildNetworkProjectileRequests(
        ProjectileFireRequest3D request,
        ProjectileWeaponConfig3D fallbackConfig,
        NetProjectileVisualType3D visualType,
        int tick,
        List<NetProjectileFireRequest3D> output)
    {
        if (output == null || request.projectilePrefab == null)
        {
            return;
        }

        Transform[] muzzles = ResolveFiringMuzzles(request, fallbackConfig);
        AimSolution aim = ResolveAimSolution();
        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        int accuracyAttackId = request.accuracyAttackIdOverride != PlayerCombatStats3D.InvalidAttackId
            ? request.accuracyAttackIdOverride
            : NetTickUtil.IsActive ? tick : PlayerCombatStats3D.InvalidAttackId;

        for (int i = 0; i < muzzles.Length; i++)
        {
            Transform spawnMuzzle = muzzles[i] != null ? muzzles[i] : transform;
            Vector3 spawnPosition = ResolveProjectileSpawnPosition(spawnMuzzle, request);
            Vector3 fireDirection = ResolveFireDirection(spawnMuzzle, spawnPosition, aim);
            Quaternion spawnRotation = Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection));

            output.Add(new NetProjectileFireRequest3D
            {
                Tick = tick,
                SpawnPosition = spawnPosition,
                SpawnRotation = spawnRotation,
                MuzzleEffectPosition = spawnMuzzle.TransformPoint(muzzleEffectLocalOffset),
                MuzzleEffectRotation = spawnMuzzle.rotation,
                Direction = fireDirection,
                InheritedVelocity = inheritedVelocity,
                Speed = request.speed,
                Damage = request.damage,
                Lifetime = request.lifetime,
                ImpactForce = request.impactForce,
                RecoilForce = request.recoilForce,
                ApplyRecoil = request.recoilForce > 0f,
                CanPierce = request.canPierce,
                PierceMultiplier = request.pierceMultiplier,
                AppliesSlow = request.appliesSlow,
                SlowMultiplier = request.slowMultiplier,
                SlowDuration = request.slowDuration,
                SlowEngineEmissionScale = request.slowEngineEmissionScale,
                ProjectileScaleMultiplier = request.projectileScaleMultiplier > 0f ? request.projectileScaleMultiplier : 1f,
                TargetFaction = request.targetFaction,
                VisualType = visualType,
                AccuracyAttackId = accuracyAttackId
            });
        }
    }

    internal void SpawnNetworkProjectile(
        GameObject projectilePrefab,
        in NetProjectileFireRequest3D fire,
        string targetTag,
        Faction3D targetFaction,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        bool playMuzzleEffect,
        bool serverAuthoritativeGameplay = false)
    {
        if (projectilePrefab == null)
        {
            return;
        }

        if (playMuzzleEffect && SupportsMuzzleEffects)
        {
            SpawnMuzzleEffect(fire.MuzzleEffectPosition, fire.MuzzleEffectRotation);
        }

        GameObject projectileObject = GameObjectPool3D.Spawn(projectilePrefab, fire.SpawnPosition, fire.SpawnRotation);
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        projectile.targetTag = targetTag;
        projectile.TargetFaction = targetFaction != Faction3D.Neutral ? targetFaction : fire.TargetFaction;
        projectile.SetCosmeticOnly(cosmeticOnly);
        projectile.SetNetworkAuthority(networkAuthority, fire.Tick);
        projectile.SetServerAuthoritativeGameplay(serverAuthoritativeGameplay);
        projectile.SetNetworkVisualType(fire.VisualType);
        projectile.Initialize(
            fire.Direction,
            fire.InheritedVelocity,
            fire.Speed,
            fire.Damage,
            fire.Lifetime,
            fire.ImpactForce,
            owner,
            fire.AccuracyAttackId);
        ApplyProjectileScale(projectileObject.transform, fire.ProjectileScaleMultiplier);
        projectile.SetProjectileScaleMultiplier(fire.ProjectileScaleMultiplier);

        if (!cosmeticOnly)
        {
            owner?.GetComponent<PlayerCombatStats3D>()?.RecordTrackedAttackFired(fire.AccuracyAttackId);
        }

        if (fire.CanPierce)
        {
            if (projectile is GigaBlastProjectile3D gigaBlastProjectile)
            {
                gigaBlastProjectile.EnablePiercing(fire.PierceMultiplier);
            }
        }

        if (fire.AppliesSlow)
        {
            projectile.EnableSlow(fire.SlowMultiplier, fire.SlowDuration, fire.SlowEngineEmissionScale);
        }
    }

    private Transform[] ResolveFiringMuzzles(ProjectileFireRequest3D request, ProjectileWeaponConfig3D fallbackConfig)
    {
        if (request.muzzles != null && request.muzzles.Length > 0)
        {
            return request.muzzles;
        }

        if (request.spawnAnchor != null)
        {
            return new[] { request.spawnAnchor };
        }

        if (fallbackConfig.muzzles != null && fallbackConfig.muzzles.Length > 0)
        {
            return fallbackConfig.muzzles;
        }

        return new[] { transform };
    }

    private void RecoverResource(float deltaTime)
    {
        if (!UsesResourceAvailability || deltaTime <= 0f || _currentResourceUsage <= 0f || !ShouldRecoverResource())
        {
            return;
        }

        float recoveryRate = Mathf.Max(0f, GetConfiguredResourceRecoveryPerSecond());
        if (recoveryRate <= 0f)
        {
            return;
        }

        SetResourceUsage(_currentResourceUsage - (recoveryRate * deltaTime));
    }

    private void SpawnProjectile(
        Transform muzzle,
        AimSolution aim,
        ProjectileFireRequest3D request,
        string targetTag,
        bool cosmeticOnly,
        NetCombat3D networkAuthority,
        NetProjectileVisualType3D visualType,
        int accuracyAttackId)
    {
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(muzzle, request);
        Vector3 fireDirection = ResolveFireDirection(muzzle, spawnPosition, aim);
        GameObject projectileObject = GameObjectPool3D.Spawn(request.projectilePrefab, spawnPosition, Quaternion.LookRotation(fireDirection, ResolveUpVector(fireDirection)));
        if (!projectileObject.TryGetComponent(out Projectile3D projectile))
        {
            Debug.LogWarning($"Projectile prefab {request.projectilePrefab.name} is missing Projectile3D.", projectileObject);
            return;
        }

        Vector3 inheritedVelocity = shipFlight != null ? shipFlight.LinearVelocity : Vector3.zero;
        projectile.targetTag = targetTag;
        projectile.TargetFaction = request.targetFaction;
        projectile.SetCosmeticOnly(cosmeticOnly);
        projectile.SetNetworkAuthority(networkAuthority, NetTickUtil.IsActive ? NetTickUtil.CurrentTick : -1);
        projectile.SetServerAuthoritativeGameplay(false);
        projectile.SetNetworkVisualType(visualType);
        projectile.Initialize(
            fireDirection,
            inheritedVelocity,
            request.speed,
            request.damage,
            request.lifetime,
            request.impactForce,
            owner,
            accuracyAttackId
        );
        ApplyProjectileScale(projectileObject.transform, request.projectileScaleMultiplier);
        projectile.SetProjectileScaleMultiplier(request.projectileScaleMultiplier);

        if (request.canPierce)
        {
            if (projectile is GigaBlastProjectile3D gigaBlastProjectile)
            {
                gigaBlastProjectile.EnablePiercing(request.pierceMultiplier);
            }
        }

        if (request.appliesSlow)
        {
            projectile.EnableSlow(request.slowMultiplier, request.slowDuration, request.slowEngineEmissionScale);
        }

        request.onProjectileSpawned?.Invoke(projectile);
    }

    private static void ApplyProjectileScale(Transform projectileTransform, float scaleMultiplier)
    {
        if (projectileTransform == null)
        {
            return;
        }

        float safeScaleMultiplier = scaleMultiplier > 0f ? scaleMultiplier : 1f;
        if (Mathf.Abs(safeScaleMultiplier - 1f) <= 0.0001f)
        {
            return;
        }

        projectileTransform.localScale *= safeScaleMultiplier;
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform muzzle, ProjectileFireRequest3D request)
    {
        if (request.spawnAnchor != null)
        {
            return request.spawnAnchor.position
                + (request.spawnAnchor.forward * request.forwardOffset)
                + (request.spawnAnchor.up * request.verticalOffset);
        }

        return muzzle.position
            + (transform.right * projectileSpawnLocalOffset.x)
            + (transform.up * projectileSpawnLocalOffset.y)
            + (transform.forward * projectileSpawnLocalOffset.z);
    }

    private AimSolution ResolveAimSolution()
    {
        if (aimMode != ProjectileAimMode3D.ScreenCenter || aimCamera == null)
        {
            Vector3 fallbackDirection = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
            if (aimAssist != null)
            {
                aimAssist.TryGetAssistedAimDirection(transform.position, fallbackDirection, out fallbackDirection);
            }

            return new AimSolution
            {
                point = transform.position + (fallbackDirection * maxAimDistance),
                direction = fallbackDirection
            };
        }

        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 assistedDirection = centerRay.direction.sqrMagnitude > 0.0001f ? centerRay.direction.normalized : transform.forward;
        if (aimAssist != null)
        {
            aimAssist.TryGetAssistedAimDirection(centerRay.origin, assistedDirection, out assistedDirection);
        }

        if (Physics.Raycast(centerRay.origin, assistedDirection, out RaycastHit hit, maxAimDistance, aimCollisionMask, QueryTriggerInteraction.Ignore))
        {
            float convergenceDistance = Mathf.Max(screenCenterConvergenceDistance, hit.distance);
            return new AimSolution
            {
                point = centerRay.origin + (assistedDirection * convergenceDistance),
                direction = assistedDirection
            };
        }

        return new AimSolution
        {
            point = centerRay.origin + (assistedDirection * Mathf.Max(screenCenterConvergenceDistance, maxAimDistance)),
            direction = assistedDirection
        };
    }

    private Vector3 ResolveFireDirection(Transform muzzle, Vector3 spawnPosition, AimSolution aim)
    {
        if (aimMode == ProjectileAimMode3D.MuzzleForward)
        {
            return muzzle.forward;
        }

        Vector3 direction = aim.point - spawnPosition;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return aim.direction.sqrMagnitude > 0.0001f ? aim.direction.normalized : transform.forward;
        }

        Vector3 resolvedDirection = direction.normalized;
        if (aimMode == ProjectileAimMode3D.ScreenCenter && aim.direction.sqrMagnitude > 0.0001f && screenCenterDirectionBlend > 0f)
        {
            resolvedDirection = Vector3.Slerp(resolvedDirection, aim.direction.normalized, Mathf.Clamp01(screenCenterDirectionBlend)).normalized;
        }

        return resolvedDirection;
    }

    private void SpawnMuzzleEffect(Transform muzzle)
    {
        if (!SupportsMuzzleEffects || muzzleEffectPrefab == null)
        {
            return;
        }

        Transform parent = parentMuzzleEffectToMuzzle ? muzzle : null;
        Vector3 spawnPosition = muzzle.TransformPoint(muzzleEffectLocalOffset);
        GameObject effectObject = GameObjectPool3D.Spawn(muzzleEffectPrefab, spawnPosition, muzzle.rotation, parent);
        PooledObject3D pooled = effectObject != null ? effectObject.GetComponent<PooledObject3D>() : null;
        if (pooled != null)
        {
            pooled.ScheduleDespawn(muzzleEffectLifetime);
        }
    }

    private void SpawnMuzzleEffect(Vector3 position, Quaternion rotation)
    {
        if (!SupportsMuzzleEffects || muzzleEffectPrefab == null)
        {
            return;
        }

        GameObject effectObject = GameObjectPool3D.Spawn(muzzleEffectPrefab, position, rotation);
        PooledObject3D pooled = effectObject != null ? effectObject.GetComponent<PooledObject3D>() : null;
        if (pooled != null)
        {
            pooled.ScheduleDespawn(muzzleEffectLifetime);
        }
    }

    private Vector3 ResolveUpVector(Vector3 direction)
    {
        Vector3 up = transform.up;
        if (up.sqrMagnitude <= 0.0001f || Mathf.Abs(Vector3.Dot(up.normalized, direction.normalized)) > 0.995f)
        {
            up = Vector3.up;
        }

        return up;
    }

    private float GetAvailableResourceRatio()
    {
        float capacity = ResourceCapacity;
        if (capacity <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - (_currentResourceUsage / capacity));
    }

    private float GetCooldownReadyRatio()
    {
        if (!UsesCooldownAvailability)
        {
            return 1f;
        }

        float cooldown = CooldownDuration;
        if (cooldown <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(1f - (CooldownRemaining / cooldown));
    }

    private void CacheAvailabilitySnapshot()
    {
        _lastAvailabilityChangedReadyRatio = UsesCooldownAvailability ? CooldownReadyRatio : GetAvailableResourceRatio();
        _lastAvailabilityChangedOnCooldown = UsesCooldownAvailability && IsOnCooldown();
        _hasAvailabilitySnapshot = true;
    }

    private void RaiseAvailabilityChangedIfNeeded(bool force = false)
    {
        float currentReadyRatio = UsesCooldownAvailability ? CooldownReadyRatio : GetAvailableResourceRatio();
        bool isOnCooldown = UsesCooldownAvailability && IsOnCooldown();

        if (!force && _hasAvailabilitySnapshot)
        {
            bool ratioUnchanged = Mathf.Abs(currentReadyRatio - _lastAvailabilityChangedReadyRatio) <= 0.0001f;
            bool cooldownStateUnchanged = isOnCooldown == _lastAvailabilityChangedOnCooldown;
            if (ratioUnchanged && cooldownStateUnchanged)
            {
                return;
            }
        }

        _lastAvailabilityChangedReadyRatio = currentReadyRatio;
        _lastAvailabilityChangedOnCooldown = isOnCooldown;
        _hasAvailabilitySnapshot = true;
        AvailabilityChanged?.Invoke(this);
    }

    private bool TryResolveOpponentPlayerTag(out string opponentPlayerTag)
    {
        opponentPlayerTag = null;

        NetMovement3D movement = owner != null ? owner.GetComponent<NetMovement3D>() : null;
        byte playerSlot = movement != null ? movement.PlayerSlot : (byte)0;
        if (playerSlot == 1)
        {
            opponentPlayerTag = "Player2";
            return true;
        }

        if (playerSlot == 2)
        {
            opponentPlayerTag = "Player1";
            return true;
        }

        if (owner != null && owner.CompareTag("Player1"))
        {
            opponentPlayerTag = "Player2";
            return true;
        }

        if (owner != null && owner.CompareTag("Player2"))
        {
            opponentPlayerTag = "Player1";
            return true;
        }

        return false;
    }

    private static bool SceneHasFactionTargets(Faction3D targetFaction)
    {
        if (targetFaction == Faction3D.Neutral)
        {
            return false;
        }

        Entity3D[] entities = FindObjectsByType<Entity3D>(FindObjectsSortMode.None);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity3D entity = entities[i];
            if (entity == null || !entity.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (FactionMember3D.ResolveFaction(entity) == targetFaction)
            {
                return true;
            }
        }

        return false;
    }
}
