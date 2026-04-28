using System;
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
    [SerializeField] private AimAssist3D aimAssist3D;
    [SerializeField] private PlayerHUDManager3D hudManager3D;
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

    private AudioSource[] _audioSourcePool;
    private AudioSource _beamHitLoopSource;
    private PlayerChromaticAberration3D _chromaticAberrationFx;
    private float _lastBeamDamageTime = float.NegativeInfinity;
    private float _lastShieldHitTime = float.NegativeInfinity;
    private float _nextShieldRegenSyncTime = float.NegativeInfinity;
    private bool _anchorHeld;

    public bool IsAnchorActive => anchorConfig.enabled && _anchorHeld;

    public void ApplyProfile(PlayerBalanceProfile3D.CoreStats core)
    {
        OverrideMaxHealthAndShield(core.maxHealth, core.maxShield, refillCurrentValues: true);
        shieldRegen.regenDelay = Mathf.Max(0f, core.shieldRegenDelay);
        shieldRegen.regenRate = Mathf.Max(0f, core.shieldRegenRate);
        anchorConfig.enabled = core.anchorEnabled;
        anchorConfig.rotationMultiplier = Mathf.Max(0f, core.anchorRotationMultiplier);
        anchorConfig.thrustMultiplier = Mathf.Max(0f, core.anchorThrustMultiplier);
    }

    protected override void Awake()
    {
        base.Awake();
        playerInput3D ??= GetComponent<PlayerInput3D>();
        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();
        aimAssist3D ??= GetComponent<AimAssist3D>();
        _chromaticAberrationFx = GetComponent<PlayerChromaticAberration3D>();
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

    public override void TakeDamage(
        float damage,
        Vector3 hitPoint,
        Entity3D attacker = null,
        DamageSource3D source = DamageSource3D.Projectile,
        int accuracyAttackId = PlayerCombatStats3D.InvalidAttackId)
    {
        _lastShieldHitTime = Time.time;
        float previousShield = currentShield;
        float previousHealth = currentHealth;

        base.TakeDamage(damage, hitPoint, attacker, source, accuracyAttackId);

        float shieldDamageTaken = Mathf.Max(0f, previousShield - currentShield);
        float hullDamageTaken = Mathf.Max(0f, previousHealth - currentHealth);
        float totalDamageTaken = shieldDamageTaken + hullDamageTaken;

        if (totalDamageTaken <= 0f)
        {
            return;
        }

        _chromaticAberrationFx?.TriggerDamageFeedback(totalDamageTaken, source);

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
        ApplySplitStatePresentation();
        StopBeamHitLoop();
        _chromaticAberrationFx?.ClearEffect();
        base.Die();
    }

    private void OnDisable()
    {
        UnsubscribeFromWeaponAvailability();
        _anchorHeld = false;
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
        _lastShieldHitTime = Time.time;
        base.TakeDirectDamage(damage, hitPoint, attacker, accuracyAttackId);
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
        if (!IsAnchorActive)
        {
            return 1f;
        }

        return Mathf.Max(0f, anchorConfig.thrustMultiplier);
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
        if (currentShield >= maxShield || maxShield <= 0f || shieldRegen.regenRate <= 0f)
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
        currentShield = Mathf.Min(maxShield, currentShield + (shieldRegen.regenRate * deltaTime));

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
            if (Time.time >= _nextShieldRegenSyncTime || Mathf.Approximately(currentShield, maxShield))
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
}
