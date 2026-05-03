using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player3D : Entity3D
{
    [System.Serializable]
    private struct ShieldRegenConfig3D
    {
        [Tooltip("Time in seconds without taking damage before shields start regenerating.")]
        public float regenDelay;
        [Tooltip("Amount of shield restored per second.")]
        public float regenRate;
    }

    [System.Serializable]
    private struct AnchorConfig3D
    {
        [Tooltip("If disabled, OnAnchor input is ignored for this player.")]
        public bool enabled;
        [Tooltip("Multiplies ShipFlight3D rotation speed while Anchor is held.")]
        public float rotationMultiplier;
        [Tooltip("Multiplies ShipFlight3D thrust acceleration while Anchor is held. Zero fully suppresses thrust.")]
        public float thrustMultiplier;
    }

    [System.Serializable]
    public struct PlayerDodgeConfig3D
    {
        [Tooltip("If disabled, left-stick flick dodge input is ignored for this player.")]
        public bool enabled;
        [Tooltip("Seconds before another generic flick dodge can be accepted.")]
        public float cooldown;
        [Tooltip("World distance covered by the dodge slide.")]
        public float dodgeDistance;
        [Tooltip("Seconds spent sliding sideways.")]
        public float slideDuration;
        [Tooltip("Seconds of invulnerability granted when the dodge begins.")]
        public float invulnerabilityDuration;
    }

    [System.Serializable]
    private struct Player3DAudioConfig
    {
        [Tooltip("Sound played when incoming damage is absorbed by shields.")]
        public SoundEffect shieldDamageSound;
        [Tooltip("Sound played when incoming damage reaches hull.")]
        public SoundEffect hullDamageSound;
        [Tooltip("Looping sound played while the player is being hit by a beam.")]
        public SoundEffect beamHitLoopSound;
        [Tooltip("How long the beam-hit loop stays alive after the last beam damage tick.")]
        public float beamHitLoopStopDelay;
        [Tooltip("Number of one-shot audio sources reserved for overlapping damage sounds.")]
        public int audioSourcePoolSize;
    }

    [Header("Player-Only 3D Systems")]
    [SerializeField] protected PlayerInput3D playerInput3D;
    [SerializeField] protected PlayerCameraRig3D playerCameraRig3D;
    [SerializeField] private PlayerScreenShake3D playerScreenShake3D;
    [SerializeField] private AimAssist3D aimAssist3D;
    [SerializeField] private PlayerHUDManager3D hudManager3D;
    [Tooltip("Visual-model anchor used by enemy warning spheres. Assign a child transform centered on the rendered ship, not the gameplay root.")]
    [SerializeField] private Transform warningSphereAnchor;
    [Header("Split State")]
    [SerializeField] private List<SplitStateLightningRig3D> splitStateLightningRigs = new();
    [SerializeField] private List<ShipSplitOffsetRig3D> splitStateOffsetRigs = new();
    [Header("Anchor")]
    [SerializeField] private AnchorConfig3D anchorConfig = new AnchorConfig3D
    {
        enabled = true,
        rotationMultiplier = 3f,
        thrustMultiplier = 0f
    };
    [Header("Flick Dodge")]
    [SerializeField] private PlayerDodgeConfig3D dodgeConfig = new PlayerDodgeConfig3D
    {
        enabled = true,
        cooldown = 0.85f,
        dodgeDistance = 14f,
        slideDuration = 0.18f,
        invulnerabilityDuration = 0.24f
    };
    [Tooltip("Logs generic/player dodge acceptance and rejection reasons.")]
    [SerializeField] private bool logDodgeDebug = true;
    [Header("Player 3D Audio")]
    [SerializeField] private Player3DAudioConfig audioConfig = new Player3DAudioConfig
    {
        beamHitLoopStopDelay = 0.2f,
        audioSourcePoolSize = 4
    };
    [Header("Shield Regeneration")]
    [SerializeField] private ShieldRegenConfig3D shieldRegen = new ShieldRegenConfig3D
    {
        regenDelay = 5f,
        regenRate = 20f
    };

    public static event Action<Player3D> PlayerSpawned;
    public static event Action<Player3D> PlayerDespawned;

    public event Action<float, float> HealthChanged;
    public event Action<float, float> ShieldChanged;
    public event Action<int> SelectedWeaponChanged;
    public event Action<int, Weapon3D> WeaponAvailabilityChanged;

    public PlayerInput3D PlayerInput3D => playerInput3D;
    public PlayerCameraRig3D PlayerCameraRig3D => playerCameraRig3D;
    public AimAssist3D AimAssist3D => aimAssist3D;
    public Transform WarningSphereAnchor => warningSphereAnchor;
    public float InvasionRewardProjectileHitRadiusBonus => _rewardProjectileHitRadiusBonus;

    private AudioSource[] _audioSourcePool;
    private AudioSource _beamHitLoopSource;
    private PlayerChromaticAberration3D _chromaticAberrationFx;
    private NetMovement3D _netMovement3D;
    private Coroutine _localDodgeCoroutine;
    private float _lastBeamDamageTime = float.NegativeInfinity;
    private float _lastShieldHitTime = float.NegativeInfinity;
    private float _nextShieldRegenSyncTime = float.NegativeInfinity;
    private float _lastDodgeTime = float.NegativeInfinity;
    private float _dodgeInvulnerableUntil = float.NegativeInfinity;
    private float _rewardExtraDodgeInvulnerabilitySeconds;
    private float _rewardOutgoingDamagePercent;
    private float _rewardIncomingDamageTakenPercent;
    private float _rewardIncomingDamageReductionPercent;
    private float _rewardShieldOverchargePercent;
    private float _rewardProjectileHitRadiusBonus;
    private bool _rewardPrimaryWeaponPierces;
    private int _rewardPrimaryWeaponPierceCount;
    private float _rewardPrimaryWeaponPierceDamageMultiplier = 1f;
    private float _rewardNoDamageRampDelay;
    private float _rewardNoDamageRampPercentPerSecond;
    private float _rewardNoDamageRampMaxPercent;
    private float _lastRewardDamageTakenTime = float.NegativeInfinity;
    private bool _rewardExecutionLotteryEnabled;
    private float _rewardExecutionLotteryChance;
    private float _rewardExecutionLotteryPerTargetCooldown;
    private bool _rewardShieldBreakRestoreEnabled;
    private bool _rewardShieldBreakRestoreAvailable;
    private float _rewardShieldLeechDamageFraction;
    private float _rewardHullRepairFractionOnNonBossKill;
    private float _rewardPostDodgeSpeedPercent;
    private float _rewardPostDodgeAccelerationPercent;
    private float _rewardPostDodgeBuffDurationSeconds;
    private float _rewardPostDodgeBuffUntil = float.NegativeInfinity;
    private float _rewardTargetMomentumDamagePercentPerHit;
    private float _rewardTargetMomentumMaxDamagePercent;
    private float _rewardTargetMomentumResetSeconds;
    private readonly Dictionary<Entity3D, TargetMomentumState3D> _rewardTargetMomentumByTarget = new Dictionary<Entity3D, TargetMomentumState3D>();
    private bool _anchorHeld;

    private struct TargetMomentumState3D
    {
        public int hitCount;
        public float lastHitTime;
    }

    public bool IsAnchorActive => anchorConfig.enabled && _anchorHeld;
    public bool IsDodgeInvulnerable => Time.time < _dodgeInvulnerableUntil;

    public PlayerBalanceProfile3D.CoreStats CaptureCoreStats()
    {
        return new PlayerBalanceProfile3D.CoreStats
        {
            maxHealth = maxHealth,
            maxShield = maxShield,
            shieldRegenDelay = shieldRegen.regenDelay,
            shieldRegenRate = shieldRegen.regenRate,
            anchorEnabled = anchorConfig.enabled,
            anchorRotationMultiplier = anchorConfig.rotationMultiplier,
            anchorThrustMultiplier = anchorConfig.thrustMultiplier
        };
    }

    public void ApplyProfile(PlayerBalanceProfile3D.CoreStats core)
    {
        OverrideMaxHealthAndShield(core.maxHealth, core.maxShield, refillCurrentValues: true);
        shieldRegen.regenDelay = Mathf.Max(0f, core.shieldRegenDelay);
        shieldRegen.regenRate = Mathf.Max(0f, core.shieldRegenRate);
        anchorConfig.enabled = core.anchorEnabled;
        anchorConfig.rotationMultiplier = Mathf.Max(0f, core.anchorRotationMultiplier);
        anchorConfig.thrustMultiplier = Mathf.Max(0f, core.anchorThrustMultiplier);
    }

    public void ApplyInvasionRewardRuntimeModifiers(
        float extraDodgeInvulnerabilitySeconds,
        float outgoingDamagePercent,
        float incomingDamageTakenPercent,
        float incomingDamageReductionPercent,
        float abilityCooldownReductionPercent,
        float shieldOverchargePercent,
        float noDamageRampDelay,
        float noDamageRampPercentPerSecond,
        float noDamageRampMaxPercent,
        float projectileHitRadiusBonus,
        bool primaryWeaponPierces,
        int primaryWeaponPierceCount,
        float primaryWeaponPierceDamageMultiplier,
        float aimAssistConeAngleBonus,
        float aimAssistRangeBonus,
        float aimAssistMaxCorrectionBonus,
        bool executionLotteryEnabled,
        float executionLotteryChance,
        float executionLotteryPerTargetCooldown,
        bool shieldBreakRestoreEnabled,
        float shieldLeechDamageFraction,
        float hullRepairFractionOnNonBossKill,
        float postDodgeSpeedPercent,
        float postDodgeAccelerationPercent,
        float postDodgeBuffDurationSeconds,
        float targetMomentumDamagePercentPerHit,
        float targetMomentumMaxDamagePercent,
        float targetMomentumResetSeconds)
    {
        _rewardExtraDodgeInvulnerabilitySeconds = Mathf.Max(0f, extraDodgeInvulnerabilitySeconds);
        _rewardOutgoingDamagePercent = Mathf.Max(-0.95f, outgoingDamagePercent);
        _rewardIncomingDamageTakenPercent = Mathf.Max(-0.95f, incomingDamageTakenPercent);
        _rewardIncomingDamageReductionPercent = Mathf.Clamp(incomingDamageReductionPercent, 0f, 0.85f);
        _rewardShieldOverchargePercent = Mathf.Max(0f, shieldOverchargePercent);
        _rewardNoDamageRampDelay = Mathf.Max(0f, noDamageRampDelay);
        _rewardNoDamageRampPercentPerSecond = Mathf.Max(0f, noDamageRampPercentPerSecond);
        _rewardNoDamageRampMaxPercent = Mathf.Max(0f, noDamageRampMaxPercent);
        _rewardProjectileHitRadiusBonus = Mathf.Max(0f, projectileHitRadiusBonus);
        _rewardPrimaryWeaponPierces = primaryWeaponPierces;
        _rewardPrimaryWeaponPierceCount = Mathf.Max(0, primaryWeaponPierceCount);
        _rewardPrimaryWeaponPierceDamageMultiplier = Mathf.Max(0f, primaryWeaponPierceDamageMultiplier);
        aimAssist3D?.SetRewardTuningBonus(aimAssistConeAngleBonus, aimAssistRangeBonus, aimAssistMaxCorrectionBonus);
        _rewardExecutionLotteryEnabled = executionLotteryEnabled;
        _rewardExecutionLotteryChance = Mathf.Clamp01(executionLotteryChance);
        _rewardExecutionLotteryPerTargetCooldown = Mathf.Max(0f, executionLotteryPerTargetCooldown);
        _rewardShieldBreakRestoreEnabled = shieldBreakRestoreEnabled;
        _rewardShieldLeechDamageFraction = Mathf.Max(0f, shieldLeechDamageFraction);
        _rewardHullRepairFractionOnNonBossKill = Mathf.Max(0f, hullRepairFractionOnNonBossKill);
        _rewardPostDodgeSpeedPercent = Mathf.Max(0f, postDodgeSpeedPercent);
        _rewardPostDodgeAccelerationPercent = Mathf.Max(0f, postDodgeAccelerationPercent);
        _rewardPostDodgeBuffDurationSeconds = Mathf.Max(0f, postDodgeBuffDurationSeconds);
        _rewardTargetMomentumDamagePercentPerHit = Mathf.Max(0f, targetMomentumDamagePercentPerHit);
        _rewardTargetMomentumMaxDamagePercent = Mathf.Max(0f, targetMomentumMaxDamagePercent);
        _rewardTargetMomentumResetSeconds = Mathf.Max(0f, targetMomentumResetSeconds);

        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i]?.SetExternalCooldownReduction(abilityCooldownReductionPercent);
        }
    }

    public void ResetInvasionRewardWaveLimitedEffects()
    {
        _rewardShieldBreakRestoreAvailable = _rewardShieldBreakRestoreEnabled;
        _rewardTargetMomentumByTarget.Clear();
        _rewardPostDodgeBuffUntil = float.NegativeInfinity;
    }

    public void ConfigureInvasionRewardProjectileRequest(Weapon3D sourceWeapon, ref ProjectileFireRequest3D request)
    {
        if (!_rewardPrimaryWeaponPierces || _rewardPrimaryWeaponPierceCount <= 0 || sourceWeapon == null)
        {
            return;
        }

        if (weapons == null || weapons.Length == 0 || !ReferenceEquals(weapons[0], sourceWeapon))
        {
            return;
        }

        request.canPierce = true;
        request.maxPierceCount = Mathf.Max(request.maxPierceCount, _rewardPrimaryWeaponPierceCount);
        request.pierceMultiplier = Mathf.Max(request.pierceMultiplier, _rewardPrimaryWeaponPierceDamageMultiplier);
    }

    protected override void Awake()
    {
        base.Awake();
        playerInput3D ??= GetComponent<PlayerInput3D>();
        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();
        playerScreenShake3D ??= GetComponent<PlayerScreenShake3D>();
        if (playerScreenShake3D == null)
        {
            playerScreenShake3D = gameObject.AddComponent<PlayerScreenShake3D>();
        }

        aimAssist3D ??= GetComponent<AimAssist3D>();
        _chromaticAberrationFx = GetComponent<PlayerChromaticAberration3D>();
        _netMovement3D = GetComponent<NetMovement3D>();
        InitializeAudio();

        if (playerInput3D != null && shipFlight != null)
        {
            shipFlight.SetInputSource(playerInput3D);
        }

        if (playerCameraRig3D != null && shipFlight != null)
        {
            playerCameraRig3D.SetShipFlight(shipFlight);
        }

        CacheSplitStateRigsIfNeeded();
        ApplySplitStatePresentation();
        _lastShieldHitTime = -Mathf.Max(0f, shieldRegen.regenDelay);
    }

    private void OnEnable()
    {
        SubscribeToWeaponAvailability();
        PlayerSpawned?.Invoke(this);
    }

    private void Update()
    {
        HandleShieldRegeneration(Time.deltaTime);

        if (_beamHitLoopSource == null || !_beamHitLoopSource.isPlaying)
        {
            return;
        }

        if (Time.time - _lastBeamDamageTime > Mathf.Max(0f, audioConfig.beamHitLoopStopDelay))
        {
            _beamHitLoopSource.Stop();
        }
    }

    public void OnAnchor(InputValue value)
    {
        _anchorHeld = anchorConfig.enabled && value.isPressed;
        ApplySplitStatePresentation();
    }

    public bool TryDodge(int horizontalDirection)
    {
        if (horizontalDirection == 0)
        {
            LogDodgeRejected("horizontalDirection was 0");
            return false;
        }

        if (!CanUseGenericDodge(out string rejectionReason))
        {
            LogDodgeRejected(rejectionReason);
            return false;
        }

        Vector3 worldDirection = ResolveDodgeDirection(horizontalDirection);
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            LogDodgeRejected($"resolved world direction was zero. horizontalDirection={horizontalDirection}");
            return false;
        }

        LogDodgeDebug($"try dodge accepted preflight. direction={(horizontalDirection > 0 ? "right" : "left")} worldDirection={worldDirection} networkActive={NetTickUtil.IsActive}");

        _netMovement3D ??= GetComponent<NetMovement3D>();
        if (CanUseNetworkDodgeMovement(_netMovement3D))
        {
            if (!_netMovement3D.QueuePredictedDodge(worldDirection, NetDodgeKind3D.Generic))
            {
                LogDodgeRejected("NetMovement3D.QueuePredictedDodge returned false");
                return false;
            }

            MarkGenericDodgeAccepted();
            PlayGenericDodgePresentation(worldDirection);
            LogDodgeDebug("queued generic predicted dodge");
            return true;
        }

        MarkGenericDodgeAccepted();
        PlayGenericDodgePresentation(worldDirection);
        StartLocalDodgeFallback(
            worldDirection.normalized,
            Mathf.Max(0.01f, dodgeConfig.dodgeDistance),
            Mathf.Max(0.01f, dodgeConfig.slideDuration));
        LogDodgeDebug("started local dodge fallback");
        return true;
    }

    public bool CanAcceptNetworkDodgeState()
    {
        return dodgeConfig.enabled && currentHealth > 0f;
    }

    public float GetNetworkDodgeCooldownDuration()
    {
        return Mathf.Max(0f, dodgeConfig.cooldown);
    }

    public bool TryResolveNetworkDodge(
        Vector3 worldDirection,
        Vector3 startPosition,
        float collisionRadius,
        out Vector3 dashVelocity,
        out float duration)
    {
        dashVelocity = Vector3.zero;
        duration = Mathf.Max(0.01f, dodgeConfig.slideDuration);

        if (!CanAcceptNetworkDodgeState() || worldDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 targetPosition = startPosition + (worldDirection.normalized * Mathf.Max(0.01f, dodgeConfig.dodgeDistance));
        Vector3 clampedTarget = ClampDodgePosition(targetPosition, collisionRadius);
        dashVelocity = (clampedTarget - startPosition) / duration;
        return dashVelocity.sqrMagnitude > 0.000001f;
    }

    public void MarkNetworkDodgeAccepted()
    {
        MarkGenericDodgeAccepted();
    }

    public void PlayNetworkDodgePresentation(Vector3 worldDirection)
    {
        PlayGenericDodgePresentation(worldDirection);
    }

    public void BeginDodgeInvulnerability(float duration)
    {
        float resolvedDuration = duration + _rewardExtraDodgeInvulnerabilitySeconds;
        if (resolvedDuration <= 0f)
        {
            return;
        }

        _dodgeInvulnerableUntil = Mathf.Max(_dodgeInvulnerableUntil, Time.time + resolvedDuration);
    }

    public void BeginDodgeCameraLag(float duration)
    {
        playerCameraRig3D?.BeginDodgeLag(duration);
    }

    public void BeginDodgeBarrelRoll(Vector3 worldDirection, float duration)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        int horizontalDirection = Vector3.Dot(worldDirection.normalized, transform.right) >= 0f ? 1 : -1;
        shipVisualTilt?.BeginBarrelRoll(horizontalDirection, duration);
    }

    public override void TakeDamage(
        float damage,
        Vector3 hitPoint,
        Entity3D attacker = null,
        DamageSource3D source = DamageSource3D.Projectile,
        int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        if (IsDodgeInvulnerable)
        {
            return;
        }

        _lastShieldHitTime = Time.time;
        _lastRewardDamageTakenTime = Time.time;
        float previousShield = currentShield;
        float previousHealth = currentHealth;

        base.TakeDamage(damage, hitPoint, attacker, source, accuracyAttackId);

        float shieldDamageTaken = Mathf.Max(0f, previousShield - currentShield);
        float hullDamageTaken = Mathf.Max(0f, previousHealth - currentHealth);
        float totalDamageTaken = shieldDamageTaken + hullDamageTaken;
        TryApplyRewardShieldBreakRestore(previousShield);

        if (totalDamageTaken <= 0f)
        {
            return;
        }

        _chromaticAberrationFx?.TriggerDamageFeedback(totalDamageTaken, source);
        playerScreenShake3D?.TriggerHitShake(totalDamageTaken, hullDamageTaken, source);

        if (currentHealth <= 0f)
        {
            return;
        }

        if (source == DamageSource3D.Beam)
        {
            _lastBeamDamageTime = Time.time;
            StartBeamHitLoop();
            return;
        }

        if (currentHealth < previousHealth)
        {
            audioConfig.hullDamageSound?.Play(GetAvailableAudioSource());
            return;
        }

        if (currentShield < previousShield)
        {
            audioConfig.shieldDamageSound?.Play(GetAvailableAudioSource());
            return;
        }
    }

    protected override void Die()
    {
        _anchorHeld = false;
        StopLocalDodgeFallback();
        _dodgeInvulnerableUntil = float.NegativeInfinity;
        _rewardPostDodgeBuffUntil = float.NegativeInfinity;
        _rewardTargetMomentumByTarget.Clear();
        ApplySplitStatePresentation();
        StopBeamHitLoop();
        _chromaticAberrationFx?.ClearEffect();
        base.Die();
    }

    private void OnDisable()
    {
        UnsubscribeFromWeaponAvailability();
        _anchorHeld = false;
        StopLocalDodgeFallback();
        _dodgeInvulnerableUntil = float.NegativeInfinity;
        _rewardPostDodgeBuffUntil = float.NegativeInfinity;
        _rewardTargetMomentumByTarget.Clear();
        ApplySplitStatePresentation();
        StopBeamHitLoop();
        _chromaticAberrationFx?.ClearEffect();
        PlayerDespawned?.Invoke(this);
    }

    private void InitializeAudio()
    {
        int poolSize = Mathf.Max(1, audioConfig.audioSourcePoolSize);
        _audioSourcePool = new AudioSource[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            _audioSourcePool[i] = source;
        }

        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 1f;
        _beamHitLoopSource.rolloffMode = AudioRolloffMode.Linear;
    }

    private AudioSource GetAvailableAudioSource()
    {
        if (_audioSourcePool == null || _audioSourcePool.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < _audioSourcePool.Length; i++)
        {
            if (_audioSourcePool[i] != null && !_audioSourcePool[i].isPlaying)
            {
                return _audioSourcePool[i];
            }
        }

        return _audioSourcePool[0];
    }

    private void StartBeamHitLoop()
    {
        if (audioConfig.beamHitLoopSound == null || _beamHitLoopSource == null)
        {
            return;
        }

        if (_beamHitLoopSource.isPlaying && _beamHitLoopSource.clip == audioConfig.beamHitLoopSound.clip)
        {
            return;
        }

        audioConfig.beamHitLoopSound.Play(_beamHitLoopSource);
    }

    private void StopBeamHitLoop()
    {
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }
    }

    public void BindHUD(PlayerHUDManager3D hud)
    {
        if (hud == null)
        {
            return;
        }

        hudManager3D = hud;
        if (!ReferenceEquals(hudManager3D.BoundPlayer, this))
        {
            hudManager3D.Bind(this);
        }
    }

    public override void TakeDirectDamage(
        float damage,
        Vector3 hitPoint,
        Entity3D attacker = null,
        int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        if (IsDodgeInvulnerable)
        {
            return;
        }

        _lastShieldHitTime = Time.time;
        _lastRewardDamageTakenTime = Time.time;
        float previousShield = currentShield;
        float previousHealth = currentHealth;

        base.TakeDirectDamage(damage, hitPoint, attacker, accuracyAttackId);

        float shieldDamageTaken = Mathf.Max(0f, previousShield - currentShield);
        float hullDamageTaken = Mathf.Max(0f, previousHealth - currentHealth);
        float totalDamageTaken = shieldDamageTaken + hullDamageTaken;
        if (totalDamageTaken > 0f)
        {
            playerScreenShake3D?.TriggerHitShake(totalDamageTaken, hullDamageTaken, DamageSource3D.Direct);
        }

        TryApplyRewardShieldBreakRestore(previousShield);
    }

    public void UnbindHUD(PlayerHUDManager3D hud)
    {
        if (ReferenceEquals(hudManager3D, hud))
        {
            hudManager3D = null;
        }
    }

    public void PublishHUDVignetteMessage(PlayerHUDVignetteMessage3D message)
    {
        hudManager3D?.PublishVignetteMessage(message);
    }

    protected override void OnHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    protected override void OnShieldChanged()
    {
        ShieldChanged?.Invoke(currentShield, maxShield);
    }

    protected override void OnSelectedWeaponChanged()
    {
        SelectedWeaponChanged?.Invoke(selectedWeaponIndex);
    }

    protected override void OnNetworkDamageFeedback(float previousHealth, float previousShield, NetCombatState3D state)
    {
        float shieldDamageTaken = Mathf.Max(0f, previousShield - currentShield);
        float hullDamageTaken = Mathf.Max(0f, previousHealth - currentHealth);
        float totalDamageTaken = shieldDamageTaken + hullDamageTaken;
        if (totalDamageTaken <= 0f)
        {
            return;
        }

        _lastShieldHitTime = Time.time;

        DamageSource3D source = (DamageSource3D)state.DamageSource;
        _chromaticAberrationFx?.TriggerDamageFeedback(totalDamageTaken, source);
        playerScreenShake3D?.TriggerHitShake(totalDamageTaken, hullDamageTaken, source);

        if (source == DamageSource3D.Beam)
        {
            _lastBeamDamageTime = Time.time;
            StartBeamHitLoop();
            return;
        }

        if (currentHealth < previousHealth)
        {
            audioConfig.hullDamageSound?.Play(GetAvailableAudioSource());
            return;
        }

        if (currentShield < previousShield)
        {
            audioConfig.shieldDamageSound?.Play(GetAvailableAudioSource());
        }
    }

    private void SubscribeToWeaponAvailability()
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon3D weapon = weapons[i];
            if (weapon == null)
            {
                continue;
            }

            weapon.AvailabilityChanged -= HandleWeaponAvailabilityChanged;
            weapon.AvailabilityChanged += HandleWeaponAvailabilityChanged;
        }
    }

    private void UnsubscribeFromWeaponAvailability()
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon3D weapon = weapons[i];
            if (weapon == null)
            {
                continue;
            }

            weapon.AvailabilityChanged -= HandleWeaponAvailabilityChanged;
        }
    }

    private void HandleWeaponAvailabilityChanged(Weapon3D weapon)
    {
        if (weapon == null)
        {
            return;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            if (!ReferenceEquals(weapons[i], weapon))
            {
                continue;
            }

            WeaponAvailabilityChanged?.Invoke(i, weapon);
            return;
        }
    }

    protected override float GetFlatBaseRotationMultiplier()
    {
        if (!IsAnchorActive)
        {
            return 1f;
        }

        return Mathf.Max(0f, anchorConfig.rotationMultiplier);
    }

    protected override float GetExternalThrustMultiplier()
    {
        float multiplier = Time.time < _rewardPostDodgeBuffUntil
            ? 1f + _rewardPostDodgeAccelerationPercent
            : 1f;

        if (!IsAnchorActive)
        {
            return multiplier;
        }

        return multiplier * Mathf.Max(0f, anchorConfig.thrustMultiplier);
    }

    protected override float GetExternalMaxSpeedMultiplier()
    {
        return Time.time < _rewardPostDodgeBuffUntil
            ? 1f + _rewardPostDodgeSpeedPercent
            : 1f;
    }

    private void CacheSplitStateRigsIfNeeded()
    {
        if (splitStateLightningRigs.Count == 0)
        {
            SplitStateLightningRig3D[] discoveredLightningRigs = GetComponentsInChildren<SplitStateLightningRig3D>(true);
            for (int i = 0; i < discoveredLightningRigs.Length; i++)
            {
                SplitStateLightningRig3D rig = discoveredLightningRigs[i];
                if (rig != null)
                {
                    splitStateLightningRigs.Add(rig);
                }
            }
        }

        if (splitStateOffsetRigs.Count == 0)
        {
            ShipSplitOffsetRig3D[] discoveredOffsetRigs = GetComponentsInChildren<ShipSplitOffsetRig3D>(true);
            for (int i = 0; i < discoveredOffsetRigs.Length; i++)
            {
                ShipSplitOffsetRig3D rig = discoveredOffsetRigs[i];
                if (rig != null)
                {
                    splitStateOffsetRigs.Add(rig);
                }
            }
        }
    }

    private void ApplySplitStatePresentation()
    {
        bool splitStateActive = IsAnchorActive;

        for (int i = 0; i < splitStateLightningRigs.Count; i++)
        {
            SplitStateLightningRig3D rig = splitStateLightningRigs[i];
            if (rig != null)
            {
                rig.SetSplitStateActive(splitStateActive);
            }
        }

        for (int i = 0; i < splitStateOffsetRigs.Count; i++)
        {
            ShipSplitOffsetRig3D rig = splitStateOffsetRigs[i];
            if (rig != null)
            {
                rig.SetSplitStateActive(splitStateActive);
            }
        }
    }

    private void HandleShieldRegeneration(float deltaTime)
    {
        float shieldLimit = GetCurrentShieldLimit();
        if (currentShield >= shieldLimit || shieldLimit <= 0f || shieldRegen.regenRate <= 0f)
        {
            shieldController?.SetRegeneration(false);
            return;
        }

        bool isNetworkedServerAuthority = !NetTickUtil.IsActive || (netCombat3D != null && netCombat3D.IsServer);
        if (!isNetworkedServerAuthority)
        {
            shieldController?.SetRegeneration(false);
            return;
        }

        if (Time.time < _lastShieldHitTime + Mathf.Max(0f, shieldRegen.regenDelay))
        {
            shieldController?.SetRegeneration(false);
            return;
        }

        float previousShield = currentShield;
        currentShield = Mathf.Min(shieldLimit, currentShield + (shieldRegen.regenRate * deltaTime));

        if (currentShield <= previousShield)
        {
            shieldController?.SetRegeneration(false);
            return;
        }

        OnShieldChanged();
        shieldController?.SetRegeneration(true);

        if (NetTickUtil.IsActive && netCombat3D != null && netCombat3D.IsServer)
        {
            const float regenSyncInterval = 0.1f;
            if (Time.time >= _nextShieldRegenSyncTime || Mathf.Approximately(currentShield, shieldLimit))
            {
                bool isSlowed = IsSlowed;
                netCombat3D.BroadcastCombatState(new NetCombatState3D
                {
                    Health = currentHealth,
                    Shield = currentShield,
                    HitPoint = Vector3.zero,
                    DamageSource = (int)DamageSource3D.Direct,
                    ShieldHit = false,
                    ShieldBreak = false,
                    SlowMultiplier = isSlowed ? GetSlowMultiplier() : 1f,
                    SlowRemainingTime = isSlowed ? Mathf.Max(0f, slowEndTime - Time.time) : 0f
                });

                _nextShieldRegenSyncTime = Time.time + regenSyncInterval;
            }
        }
    }

    public override float ModifyOutgoingDamage(float damage, Entity3D target, DamageSource3D source, int accuracyAttackId)
    {
        float modifiedDamage = base.ModifyOutgoingDamage(damage, target, source, accuracyAttackId);
        if (modifiedDamage <= 0f)
        {
            return 0f;
        }

        modifiedDamage *= 1f + _rewardOutgoingDamagePercent + ResolveRewardNoDamageRampPercent() + ResolveRewardTargetMomentumPercent(target);
        if (ShouldRewardExecutionLotteryKill(target))
        {
            return Mathf.Max(modifiedDamage, target.MaxHealth + target.MaxShield);
        }

        return Mathf.Max(0f, modifiedDamage);
    }

    public override void OnDamageDealtToTarget(Entity3D target, float appliedDamage, DamageSource3D source, int accuracyAttackId)
    {
        if (target == null || appliedDamage <= 0f)
        {
            return;
        }

        ApplyRewardShieldLeech(appliedDamage);
        RegisterRewardTargetMomentumHit(target);
    }

    public override void OnEnemyKilledByDamage(Enemy3D enemy, float appliedDamage, DamageSource3D source, int accuracyAttackId)
    {
        if (enemy == null || enemy.IsBossEnemy || _rewardHullRepairFractionOnNonBossKill <= 0f || maxHealth <= 0f)
        {
            return;
        }

        float repairedHealth = Mathf.Min(maxHealth, currentHealth + (maxHealth * _rewardHullRepairFractionOnNonBossKill));
        if (repairedHealth > currentHealth)
        {
            SetCurrentDurability(repairedHealth, currentShield);
        }
    }

    protected override float ModifyIncomingDamage(float damage, Entity3D attacker, DamageSource3D source, int accuracyAttackId)
    {
        float modifiedDamage = base.ModifyIncomingDamage(damage, attacker, source, accuracyAttackId);
        modifiedDamage *= 1f + _rewardIncomingDamageTakenPercent;
        modifiedDamage *= 1f - _rewardIncomingDamageReductionPercent;
        return Mathf.Max(0f, modifiedDamage);
    }

    protected override float GetCurrentShieldLimit()
    {
        return maxShield * (1f + _rewardShieldOverchargePercent);
    }

    private float ResolveRewardNoDamageRampPercent()
    {
        if (_rewardNoDamageRampMaxPercent <= 0f || _rewardNoDamageRampPercentPerSecond <= 0f)
        {
            return 0f;
        }

        float timeSinceDamage = Time.time - _lastRewardDamageTakenTime;
        if (timeSinceDamage <= _rewardNoDamageRampDelay)
        {
            return 0f;
        }

        return Mathf.Min(_rewardNoDamageRampMaxPercent, (timeSinceDamage - _rewardNoDamageRampDelay) * _rewardNoDamageRampPercentPerSecond);
    }

    private float ResolveRewardTargetMomentumPercent(Entity3D target)
    {
        if (target == null || _rewardTargetMomentumDamagePercentPerHit <= 0f || _rewardTargetMomentumMaxDamagePercent <= 0f)
        {
            return 0f;
        }

        if (!_rewardTargetMomentumByTarget.TryGetValue(target, out TargetMomentumState3D state))
        {
            return 0f;
        }

        if (_rewardTargetMomentumResetSeconds > 0f && Time.time - state.lastHitTime > _rewardTargetMomentumResetSeconds)
        {
            _rewardTargetMomentumByTarget.Remove(target);
            return 0f;
        }

        return Mathf.Min(_rewardTargetMomentumMaxDamagePercent, state.hitCount * _rewardTargetMomentumDamagePercentPerHit);
    }

    private void RegisterRewardTargetMomentumHit(Entity3D target)
    {
        if (target == null || _rewardTargetMomentumDamagePercentPerHit <= 0f || _rewardTargetMomentumMaxDamagePercent <= 0f)
        {
            return;
        }

        _rewardTargetMomentumByTarget.TryGetValue(target, out TargetMomentumState3D state);
        if (_rewardTargetMomentumResetSeconds > 0f && Time.time - state.lastHitTime > _rewardTargetMomentumResetSeconds)
        {
            state.hitCount = 0;
        }

        state.hitCount = Mathf.Max(0, state.hitCount) + 1;
        state.lastHitTime = Time.time;
        _rewardTargetMomentumByTarget[target] = state;
    }

    private void ApplyRewardShieldLeech(float appliedDamage)
    {
        if (_rewardShieldLeechDamageFraction <= 0f || appliedDamage <= 0f)
        {
            return;
        }

        float shieldLimit = GetCurrentShieldLimit();
        if (shieldLimit <= 0f || currentShield >= shieldLimit)
        {
            return;
        }

        SetCurrentDurability(currentHealth, Mathf.Min(shieldLimit, currentShield + (appliedDamage * _rewardShieldLeechDamageFraction)));
    }

    private bool ShouldRewardExecutionLotteryKill(Entity3D target)
    {
        if (!_rewardExecutionLotteryEnabled || target == null || target.CurrentHealth <= 0f)
        {
            return false;
        }

        Enemy3D enemy = target as Enemy3D;
        if (enemy == null || enemy.IsBossEnemy)
        {
            return false;
        }

        if (!enemy.CanRollRewardExecutionLottery(this, _rewardExecutionLotteryPerTargetCooldown))
        {
            return false;
        }

        return UnityEngine.Random.value < _rewardExecutionLotteryChance;
    }

    private void TryApplyRewardShieldBreakRestore(float previousShield)
    {
        if (!_rewardShieldBreakRestoreEnabled || !_rewardShieldBreakRestoreAvailable)
        {
            return;
        }

        if (previousShield <= 0f || currentShield > 0f || currentHealth <= 0f)
        {
            return;
        }

        _rewardShieldBreakRestoreAvailable = false;
        currentShield = GetCurrentShieldLimit();
        OnShieldChanged();
        shieldController?.OnHit(transform.position);
    }

    private bool CanUseGenericDodge(out string rejectionReason)
    {
        if (!dodgeConfig.enabled)
        {
            rejectionReason = "dodgeConfig.enabled is false";
            return false;
        }

        if (currentHealth <= 0f)
        {
            rejectionReason = $"player health is not positive currentHealth={currentHealth:0.00}";
            return false;
        }

        float readyTime = _lastDodgeTime + Mathf.Max(0f, dodgeConfig.cooldown);
        if (Time.time < readyTime)
        {
            rejectionReason = $"on cooldown remaining={readyTime - Time.time:0.000}s cooldown={dodgeConfig.cooldown:0.000}s";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    private void MarkGenericDodgeAccepted()
    {
        _lastDodgeTime = Time.time;
        if (_rewardPostDodgeBuffDurationSeconds > 0f && (_rewardPostDodgeSpeedPercent > 0f || _rewardPostDodgeAccelerationPercent > 0f))
        {
            _rewardPostDodgeBuffUntil = Mathf.Max(_rewardPostDodgeBuffUntil, Time.time + _rewardPostDodgeBuffDurationSeconds);
        }

        BeginDodgeInvulnerability(Mathf.Max(0f, dodgeConfig.invulnerabilityDuration));
        LogDodgeDebug($"marked dodge accepted. cooldown={dodgeConfig.cooldown:0.000}s iframes={dodgeConfig.invulnerabilityDuration:0.000}s");
    }

    private void PlayGenericDodgePresentation(Vector3 worldDirection)
    {
        BeginDodgeInvulnerability(Mathf.Max(0f, dodgeConfig.invulnerabilityDuration));
        BeginDodgeCameraLag(Mathf.Max(dodgeConfig.slideDuration, dodgeConfig.invulnerabilityDuration));
        BeginDodgeBarrelRoll(worldDirection, Mathf.Max(0.01f, dodgeConfig.slideDuration));
        RecordCombatActivity(0.25f);
        LogDodgeDebug($"played dodge presentation. worldDirection={worldDirection} slideDuration={dodgeConfig.slideDuration:0.000}s distance={dodgeConfig.dodgeDistance:0.00}");
    }

    private Vector3 ResolveDodgeDirection(int horizontalDirection)
    {
        Vector3 direction = horizontalDirection >= 0 ? transform.right : -transform.right;
        if (shipFlight != null && shipFlight.LockToWorldYPlane)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return horizontalDirection >= 0 ? Vector3.right : Vector3.left;
    }

    private void StartLocalDodgeFallback(Vector3 worldDirection, float dodgeDistance, float slideDuration)
    {
        StopLocalDodgeFallback();
        _localDodgeCoroutine = StartCoroutine(LocalDodgeSlideCoroutine(worldDirection, dodgeDistance, slideDuration));
    }

    private IEnumerator LocalDodgeSlideCoroutine(Vector3 worldDirection, float dodgeDistance, float slideDuration)
    {
        Rigidbody rb = shipFlight != null ? shipFlight.Rigidbody : null;
        if (rb == null)
        {
            _localDodgeCoroutine = null;
            yield break;
        }

        float collisionRadius = ResolveCollisionRadius();
        Vector3 startPosition = rb.position;
        Vector3 targetPosition = ClampDodgePosition(startPosition + (worldDirection.normalized * dodgeDistance), collisionRadius);
        Vector3 previousPosition = startPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = MovementSimulation3D.EaseOutCubic(t);
            Vector3 flightDelta = rb.position - previousPosition;
            startPosition += flightDelta;
            targetPosition += flightDelta;

            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            currentPosition = ClampDodgePosition(currentPosition, collisionRadius);
            rb.MovePosition(currentPosition);
            previousPosition = currentPosition;
            yield return new WaitForFixedUpdate();
        }

        Vector3 finalPosition = ClampDodgePosition(targetPosition, collisionRadius);
        rb.position = finalPosition;
        transform.position = finalPosition;
        _localDodgeCoroutine = null;
    }

    private void StopLocalDodgeFallback()
    {
        if (_localDodgeCoroutine == null)
        {
            return;
        }

        StopCoroutine(_localDodgeCoroutine);
        _localDodgeCoroutine = null;
    }

    private float ResolveCollisionRadius()
    {
        _netMovement3D ??= GetComponent<NetMovement3D>();
        if (_netMovement3D != null)
        {
            return _netMovement3D.GetCollisionRadius();
        }

        Collider collider3D = GetComponent<Collider>();
        if (collider3D != null)
        {
            Bounds bounds = collider3D.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        Collider[] childColliders = GetComponentsInChildren<Collider>();
        float radius = 0.5f;
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider child = childColliders[i];
            if (child == null)
            {
                continue;
            }

            Bounds bounds = child.bounds;
            radius = Mathf.Max(radius, bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        return radius;
    }

    private static Vector3 ClampDodgePosition(Vector3 targetPosition, float collisionRadius)
    {
        if (!ArenaBoundary3D.TryGetActive(out ArenaBoundary3D boundary) || !boundary.BlocksMovement)
        {
            return targetPosition;
        }

        return boundary.ClampPositionInside(targetPosition, collisionRadius);
    }

    private static bool CanUseNetworkDodgeMovement(NetMovement3D movement)
    {
        return movement != null && NetTickUtil.IsActive && movement.IsSpawned && movement.IsOwner;
    }

    private void LogDodgeRejected(string reason)
    {
        if (!logDodgeDebug)
        {
            return;
        }

        Debug.Log($"[Dodge3D] {name} dodge rejected: {reason}", this);
    }

    private void LogDodgeDebug(string message)
    {
        if (!logDodgeDebug)
        {
            return;
        }

        Debug.Log($"[Dodge3D] {name} {message}", this);
    }
}
