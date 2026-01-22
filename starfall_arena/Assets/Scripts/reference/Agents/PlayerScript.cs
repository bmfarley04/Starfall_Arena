using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerScript : ShipBase
{
    public float beamOffsetDistance = 1f;

    [Header("Player UI")]
    public Image healthBarFillImage;
    public Image shieldBarFillImage;

    [Header("Shield Regeneration")]
    [Tooltip("Time in seconds without taking damage before shields start regenerating")]
    public float shieldRegenDelay = 3f;
    [Tooltip("Amount of shield restored per second")]
    public float shieldRegenRate = 10f;

    [Header("Controller Settings")]
    [Tooltip("Deadzone threshold for controller look input (0-1)")]
    [Range(0f, 0.5f)]
    public float controllerLookDeadzone = 0.1f;
    [Tooltip("Minimum mouse movement (pixels) to detect mouse input")]
    public float mouseMovementThreshold = 5f;

    [Header("Friction Settings")]
    [Tooltip("how long (seconds) after thrust ends before friction starts")]
    public float frictionDelay = 0.25f;
    [Tooltip("how fast velocity is reduced (units per second) once friction is active")]
    public float frictionDeceleration = 6f;
    [Tooltip("if true, prints friction debug logs")]
    public bool frictionDebug = false;

    [Header("Combat Settings")]
    [Tooltip("rotation speed multiplier when beam is active (0.3 = 70% slower)")]
    public float beamRotationMultiplier = 0.3f;
    [Tooltip("cooldown between normal fire shots (seconds)")]
    public float fireCooldown = 0.5f;
    [Tooltip("Duration for beam line renderer to fade in (seconds)")]
    public float beamFadeInDuration = 0.3f;

    [Header("Beam Capacity")]
    public Image beamCapacityBarFillImage;
    [Tooltip("Sub-icon image (alpha scales based on capacity)")]
    public Image beamSubIcon;

    [Header("Ability Icon Alpha")]
    [Tooltip("Alpha value when ability is on cooldown (0-255)")]
    [Range(0, 255)]
    public int cooldownAlpha = 45;
    [Tooltip("Alpha value when ability is ready (0-255)")]
    [Range(0, 255)]
    public int readyAlpha = 255;
    [Tooltip("Maximum beam capacity (100 units)")]
    public float beamCapacity = 100f;
    [Tooltip("How fast beam drains (units per second)")]
    public float beamCapacityDrainRate = 20f;
    [Tooltip("How fast beam capacity regenerates when not firing (units per second)")]
    public float beamCapacityRegenRate = 5f;

    [Header("Visual Feedback")]
    [Tooltip("enable/disable chromatic aberration on taking damage")]
    public bool enableChromaticAberration = true;
    [Tooltip("max chromatic aberration intensity")]
    public float maxChromaticIntensity = 1f;
    [Tooltip("intensity increase per damage point")]
    public float chromaticIntensityPerDamage = 0.05f;
    [Tooltip("how fast chromatic aberration fades (units per second)")]
    public float _projectileMultiplier = 2f;
    public float chromaticFadeSpeed = 2f;
    [Tooltip("time window to detect beam hits (seconds)")]
    public float beamDetectionWindow = 0.2f;

    [Header("Sound Effects")]
    [Header("Movement Sounds")]
    [Tooltip("Engine rumble sound (loops while boosting)")]
    public AudioClip engineRumbleSound;
    [Tooltip("Fade in/out duration for engine rumble (seconds)")]
    public float engineRumbleFadeDuration = 0.2f;
    [Tooltip("Fade in duration for beam sound (seconds)")]
    public float beamSoundFadeDuration = 0.1f;
    [Tooltip("Fade in duration for GigaBlast charge sound (seconds)")]
    public float gigaBlastChargeSoundFadeDuration = 0.1f;

    [Header("Weapon Sounds")]
    [Tooltip("Basic projectile fire sound")]
    public AudioClip projectileFireSound;
    [Tooltip("Laser beam sound (loops while beam is active)")]
    public AudioClip laserBeamSound;

    [Header("Ability Sounds - Teleport")]
    [Tooltip("Teleport exit sound (at origin)")]
    public AudioClip teleportExitSound;
    [Tooltip("Teleport arrival sound (at destination)")]
    public AudioClip teleportArrivalSound;

    [Header("Ability Sounds - GigaBlast")]
    [Tooltip("Charging sound (plays during charge, stops on release)")]
    public AudioClip gigaBlastChargeSound;
    [Tooltip("Tier 1 fire sound")]
    public AudioClip gigaBlastTier1FireSound;
    [Tooltip("Tier 2 fire sound")]
    public AudioClip gigaBlastTier2FireSound;
    [Tooltip("Tier 3 fire sound")]
    public AudioClip gigaBlastTier3FireSound;
    [Tooltip("Tier 4 fire sound")]
    public AudioClip gigaBlastTier4FireSound;

    [Header("Ability Sounds - Reflect")]
    [Tooltip("Reflect shield duration sound (loops while active)")]
    public AudioClip reflectShieldLoopSound;
    [Tooltip("Bullet reflection impact sound")]
    public AudioClip bulletReflectionSound;

    [Header("Damage Sounds")]
    [Tooltip("Shield damage sound")]
    public AudioClip shieldDamageSound;
    [Tooltip("Hull damage sound")]
    public AudioClip hullDamageSound;
    [Tooltip("Beam hit loop sound (loops while taking beam damage)")]
    public AudioClip beamHitLoopSound;

    [Header("Audio System")]
    [Tooltip("Number of AudioSources in the pool for overlapping sounds")]
    public int audioSourcePoolSize = 10;

    [System.Serializable]
    public struct AudioVolumeConfig
    {
        [Header("Movement Sounds")]
        [Range(0f, 3f)]
        [Tooltip("Volume for engine rumble sound")]
        public float engineRumbleVolume;

        [Header("Weapon Sounds")]
        [Range(0f, 3f)]
        [Tooltip("Volume for projectile fire sound")]
        public float projectileFireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for laser beam sound")]
        public float laserBeamVolume;

        [Header("Ability Sounds - Teleport")]
        [Range(0f, 3f)]
        [Tooltip("Volume for teleport exit sound")]
        public float teleportExitVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for teleport arrival sound")]
        public float teleportArrivalVolume;

        [Header("Ability Sounds - GigaBlast")]
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast charge sound")]
        public float gigaBlastChargeVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 1 fire sound")]
        public float gigaBlastTier1FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 2 fire sound")]
        public float gigaBlastTier2FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 3 fire sound")]
        public float gigaBlastTier3FireVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for GigaBlast Tier 4 fire sound")]
        public float gigaBlastTier4FireVolume;

        [Header("Ability Sounds - Reflect")]
        [Range(0f, 3f)]
        [Tooltip("Volume for reflect shield loop sound")]
        public float reflectShieldLoopVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for bullet reflection impact sound")]
        public float bulletReflectionVolume;

        [Header("Damage Sounds")]
        [Range(0f, 3f)]
        [Tooltip("Volume for shield damage sound")]
        public float shieldDamageVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for hull damage sound")]
        public float hullDamageVolume;
        [Range(0f, 3f)]
        [Tooltip("Volume for beam hit loop sound")]
        public float beamHitLoopVolume;

        [Header("GigaBlast Sound Pitch")]
        [Range(0.5f, 2f)]
        [Tooltip("Pitch multiplier for GigaBlast Tier 3 charge sound")]
        public float gigaBlastTier3ChargePitch;
        [Range(0.5f, 2f)]
        [Tooltip("Pitch multiplier for GigaBlast Tier 4 charge sound")]
        public float gigaBlastTier4ChargePitch;
    }

    [Header("Audio Volume Controls")]
    public AudioVolumeConfig audioVolume = new AudioVolumeConfig
    {
        engineRumbleVolume = 0.7f,
        projectileFireVolume = 0.7f,
        laserBeamVolume = 0.7f,
        teleportExitVolume = 0.7f,
        teleportArrivalVolume = 0.7f,
        gigaBlastChargeVolume = 0.7f,
        gigaBlastTier1FireVolume = 0.7f,
        gigaBlastTier2FireVolume = 0.7f,
        gigaBlastTier3FireVolume = 0.7f,
        gigaBlastTier4FireVolume = 0.7f,
        reflectShieldLoopVolume = 0.7f,
        bulletReflectionVolume = 0.7f,
        shieldDamageVolume = 0.7f,
        hullDamageVolume = 0.7f,
        beamHitLoopVolume = 0.7f,
        gigaBlastTier3ChargePitch = 1f,
        gigaBlastTier4ChargePitch = 1f
    };

    [System.Serializable]
    public struct ReflectAbilityConfig
    {
        [Header("UI - Cooldown Animation")]
        [Tooltip("The dark overlay image with ImageType=Filled")]
        public Image cooldownOverlay;

        [Tooltip("Sub-icon image (alpha 45 on cooldown, 255 when ready)")]
        public Image subIcon;

        [Header("Cooldown & Duration")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;

        [Tooltip("Shield active duration (seconds)")]
        public float activeDuration;

        [Header("Visual Settings")]
        [Tooltip("ReflectShield component on player (drag from Hierarchy)")]
        public ReflectShield shield;

        [Tooltip("Color of reflected projectiles")]
        public Color reflectedProjectileColor;

        [Header("Damage Multiplier")]
        [Tooltip("Damage multiplier for reflected projectiles (1.0 = same damage, 2.0 = double damage)")]
        [Range(0.5f, 5f)]
        public float reflectedProjectileDamageMultiplier;
    }

    [Header("Reflect Ability")]
    public ReflectAbilityConfig reflectAbility;

    private float _lastReflectTime = -999f;
    private Coroutine _reflectCoroutine;

    [System.Serializable]
    public struct TeleportAbilityConfig
    {
        [Header("UI - Cooldown Animation")]
        [Tooltip("The dark overlay image with ImageType=Filled")]
        public Image cooldownOverlay;

        [Tooltip("Sub-icon image (alpha 45 on cooldown, 255 when ready)")]
        public Image subIcon;

        [Header("Cooldown & Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;

        [Tooltip("Delay before teleport executes (seconds)")]
        public float preTeleportDelay;

        [Header("Scale Animation")]
        [Tooltip("Shrink duration at origin (seconds)")]
        public float shrinkDuration;

        [Tooltip("Grow duration at destination (seconds)")]
        public float growDuration;

        [Tooltip("Target X scale at origin (squeeze width, e.g. 0.1)")]
        public float originScaleX;

        [Tooltip("Target Y scale at origin (stretch height, e.g. 2.0)")]
        public float originScaleY;

        [Tooltip("Overshoot scale at destination (pop effect, e.g. 1.2)")]
        public float destinationOvershootScale;

        [Tooltip("Normal scale (usually 1.0)")]
        public float normalScale;

        [Header("Physics")]
        [Tooltip("Reset velocity after teleport")]
        public bool resetVelocity;

        [Tooltip("Preserve momentum but reduce magnitude")]
        public bool dampVelocity;

        [Tooltip("Velocity multiplier if dampVelocity true")]
        [Range(0f, 1f)]
        public float velocityDampFactor;

        [Header("Visual Effects")]
        [Tooltip("Enable chromatic aberration flash on teleport")]
        public bool enableChromaticFlash;

        [Tooltip("Chromatic aberration intensity on teleport")]
        [Range(0f, 1f)]
        public float chromaticFlashIntensity;

        [Tooltip("Enable screen shake on teleport")]
        public bool enableScreenShake;

        [Tooltip("Screen shake strength (force)")]
        public float screenShakeStrength;

        [Tooltip("Optional particle effect at origin (leave null for none)")]
        public GameObject teleportOriginEffect;

        [Tooltip("Optional particle effect at destination (leave null for none)")]
        public GameObject teleportDestinationEffect;
    }

    [Header("Teleport Ability")]
    public TeleportAbilityConfig teleportAbility;

    private float _lastTeleportTime = -999f;
    private Coroutine _teleportCoroutine;
    private bool _isTeleporting = false;

    [System.Serializable]
    public struct GigaBlastAbilityConfig
    {
        [Header("UI - Cooldown Animation")]
        [Tooltip("The dark overlay image with ImageType=Filled")]
        public Image cooldownOverlay;

        [Tooltip("Sub-icon image (alpha 45 on cooldown, 255 when ready)")]
        public Image subIcon;

        [Header("Cooldown & Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;

        [Tooltip("Minimum charge time before shot can be released (seconds)")]
        public float minChargeTime;

        [Tooltip("Maximum charge time cap (seconds)")]
        public float maxChargeTime;

        [Header("Charge Tier Thresholds")]
        [Tooltip("Time to reach Tier 1 (seconds)")]
        public float tier1Time;

        [Tooltip("Time to reach Tier 2 (seconds)")]
        public float tier2Time;

        [Tooltip("Time to reach Tier 3 (seconds)")]
        public float tier3Time;

        [Tooltip("Time to reach Tier 4 (seconds)")]
        public float tier4Time;

        [Header("Particle Colors per Tier")]
        [Tooltip("Particle color at Tier 1 (0.5-1s)")]
        public Color tier1Color;

        [Tooltip("Particle color at Tier 2 (1-2s)")]
        public Color tier2Color;

        [Tooltip("Particle color at Tier 3 (2-3s)")]
        public Color tier3Color;

        [Tooltip("Particle color at Tier 4 (3s+)")]
        public Color tier4Color;

        [Header("Movement Penalties per Tier")]
        [Tooltip("Thrust speed multiplier for Tier 1")]
        [Range(0f, 1f)]
        public float tier1ThrustMultiplier;

        [Tooltip("Rotation speed multiplier for Tier 1")]
        [Range(0f, 1f)]
        public float tier1RotationMultiplier;

        [Tooltip("Thrust speed multiplier for Tier 2")]
        [Range(0f, 1f)]
        public float tier2ThrustMultiplier;

        [Tooltip("Rotation speed multiplier for Tier 2")]
        [Range(0f, 1f)]
        public float tier2RotationMultiplier;

        [Tooltip("Thrust speed multiplier for Tier 3")]
        [Range(0f, 1f)]
        public float tier3ThrustMultiplier;

        [Tooltip("Rotation speed multiplier for Tier 3")]
        [Range(0f, 1f)]
        public float tier3RotationMultiplier;

        [Tooltip("Thrust speed multiplier for Tier 4")]
        [Range(0f, 1f)]
        public float tier4ThrustMultiplier;

        [Tooltip("Rotation speed multiplier for Tier 4")]
        [Range(0f, 1f)]
        public float tier4RotationMultiplier;

        [Header("Projectile Scaling - Tier Multipliers")]
        [Tooltip("Tier 1 speed multiplier (base projectile speed * this)")]
        public float tier1SpeedMultiplier;

        [Tooltip("Tier 2 speed multiplier")]
        public float tier2SpeedMultiplier;

        [Tooltip("Tier 3 speed multiplier")]
        public float tier3SpeedMultiplier;

        [Tooltip("Tier 4 speed multiplier")]
        public float tier4SpeedMultiplier;

        [Tooltip("Tier 1 damage multiplier (base projectile damage * this)")]
        public float tier1DamageMultiplier;

        [Tooltip("Tier 2 damage multiplier")]
        public float tier2DamageMultiplier;

        [Tooltip("Tier 3 damage multiplier")]
        public float tier3DamageMultiplier;

        [Tooltip("Tier 4 damage multiplier")]
        public float tier4DamageMultiplier;

        [Tooltip("Tier 1 recoil force multiplier (base recoil * this)")]
        public float tier1RecoilMultiplier;

        [Tooltip("Tier 2 recoil force multiplier")]
        public float tier2RecoilMultiplier;

        [Tooltip("Tier 3 recoil force multiplier")]
        public float tier3RecoilMultiplier;

        [Tooltip("Tier 4 recoil force multiplier")]
        public float tier4RecoilMultiplier;

        [Tooltip("Tier 1 impact force multiplier (base impact * this)")]
        public float tier1ImpactMultiplier;

        [Tooltip("Tier 2 impact force multiplier")]
        public float tier2ImpactMultiplier;

        [Tooltip("Tier 3 impact force multiplier")]
        public float tier3ImpactMultiplier;

        [Tooltip("Tier 4 impact force multiplier")]
        public float tier4ImpactMultiplier;

        [Header("Pierce Behavior")]
        [Tooltip("Damage multiplier for Tier 3 pierce (0 = no pierce)")]
        public float tier3PierceMultiplier;

        [Tooltip("Damage multiplier for Tier 4 pierce (0 = no pierce)")]
        public float tier4PierceMultiplier;

        [Header("Visual Effects")]
        [Tooltip("Tier 1 charged projectile prefab (0.5-1s)")]
        public GameObject tier1ProjectilePrefab;

        [Tooltip("Tier 2 charged projectile prefab (1-2s)")]
        public GameObject tier2ProjectilePrefab;

        [Tooltip("Tier 3 charged projectile prefab (2-3s)")]
        public GameObject tier3ProjectilePrefab;

        [Tooltip("Tier 4 charged projectile prefab (3s+)")]
        public GameObject tier4ProjectilePrefab;

        [Tooltip("Tier 1 particle system at ship tip (0.5-1s)")]
        public ParticleSystem tier1ParticleEffect;

        [Tooltip("Tier 2 particle system at ship tip (1-2s)")]
        public ParticleSystem tier2ParticleEffect;

        [Tooltip("Tier 3 particle system at ship tip (2-3s)")]
        public ParticleSystem tier3ParticleEffect;

        [Tooltip("Tier 4 particle system at ship tip (3s+)")]
        public ParticleSystem tier4ParticleEffect;

        [Tooltip("Projectile lifetime (seconds)")]
        public float projectileLifetime;
    }

    [Header("GigaBlast Ability")]
    public GigaBlastAbilityConfig gigaBlastAbility;

    private float _lastGigaBlastTime = -999f;
    private bool _isCharging = false;
    private float _chargeStartTime = 0f;
    private int _currentChargeTier = 0;

    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private bool _frictionEnabled = false;
    private Vector2 _lookInput;
    private bool _usingControllerLook = false;
    private float _lastControllerLookTime = 0f;
    private Vector2 _lastMousePosition;
    private float _lastMouseMoveTime = 0f;

    private LaserBeam _activeBeam;
    private float _lastFireTime = -999f;
    private float _currentBeamCapacity;
    private bool _isFiring = false;

    // internal friction state
    private float _frictionTimer = 0f;

    // reference to map manager
    SceneManagerScript sceneManager;

    // chromatic aberration runtime state
    private ChromaticAberration _chromaticAberration;
    private Coroutine _chromaticFadeCoroutine;
    private float _currentChromaticIntensity = 0f;
    private float _lastDamageTime;

    // NEW variable strictly for Shield Regeneration logic
    private float _lastShieldHitTime;

    private float _damageAccumulator;

    // screen shake
    private Unity.Cinemachine.CinemachineImpulseSource _impulseSource;

    // Audio system
    private AudioSource[] _audioSourcePool;
    private AudioSource _engineRumbleSource;
    private AudioSource _laserBeamSource;
    private AudioSource _reflectShieldSource;
    private AudioSource _gigaBlastChargeSource;
    private AudioSource _beamHitLoopSource;
    private Coroutine _engineFadeCoroutine;
    private Coroutine _beamFadeCoroutine;
    private Coroutine _gigaBlastChargeFadeCoroutine;

    protected override void Awake()
    {
        base.Awake();

        // Initialize the new shield timer to a negative value so shields can regen immediately at start
        _lastShieldHitTime = -shieldRegenDelay;

        // Initialize beam capacity to empty (fills when beam is used)
        _currentBeamCapacity = 0f;

        // Initialize mouse position to current position to avoid initial snap
        _lastMousePosition = Mouse.current.position.ReadValue();
        _lastMouseMoveTime = Time.time;

        if (sceneManager == null)
        {
            sceneManager = FindObjectOfType<SceneManagerScript>();
            if (sceneManager == null)
            {
                Debug.LogError("sceneManagerScript not found in scene!", this);
            }
        }

        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput != null)
        {
            _moveAction = _playerInput.actions["Move"];
        }

        // Cache impulse source for screen shake
        _impulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
        if (_impulseSource == null)
        {
            Debug.LogWarning("CinemachineImpulseSource not found on player. Screen shake effects will be disabled.", this);
        }

        // Find the global volume and get chromatic aberration
        var volume = FindObjectOfType<Volume>();
        if (volume != null && volume.profile.TryGet(out ChromaticAberration ca))
        {
            _chromaticAberration = ca;
        }
        else
        {
            Debug.LogWarning("ChromaticAberration not found in Volume profile. Visual feedback will be disabled.", this);
        }

        UpdateHealthBar();
        UpdateShieldBar();
        UpdateBeamCapacityBar();

        // Initialize audio system
        InitializeAudioSystem();
    }

    private void InitializeAudioSystem()
    {
        // Create audio source pool for one-shot sounds
        _audioSourcePool = new AudioSource[audioSourcePoolSize];
        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            _audioSourcePool[i] = gameObject.AddComponent<AudioSource>();
            _audioSourcePool[i].playOnAwake = false;
            _audioSourcePool[i].spatialBlend = 1f; // Full 3D sound
            _audioSourcePool[i].rolloffMode = AudioRolloffMode.Linear;
            _audioSourcePool[i].minDistance = 10f; // Full volume within this distance
            _audioSourcePool[i].maxDistance = 50f; // Silent beyond this distance
            _audioSourcePool[i].dopplerLevel = 0f; // Disable doppler effect
        }

        // Create dedicated audio sources for looping sounds
        _engineRumbleSource = gameObject.AddComponent<AudioSource>();
        _engineRumbleSource.playOnAwake = false;
        _engineRumbleSource.loop = true;
        _engineRumbleSource.volume = 0f;
        _engineRumbleSource.spatialBlend = 1f; // Full 3D sound
        _engineRumbleSource.rolloffMode = AudioRolloffMode.Linear;
        _engineRumbleSource.minDistance = 10f;
        _engineRumbleSource.maxDistance = 50f;
        _engineRumbleSource.dopplerLevel = 0f;

        _laserBeamSource = gameObject.AddComponent<AudioSource>();
        _laserBeamSource.playOnAwake = false;
        _laserBeamSource.loop = true;
        _laserBeamSource.spatialBlend = 1f; // Full 3D sound
        _laserBeamSource.rolloffMode = AudioRolloffMode.Linear;
        _laserBeamSource.minDistance = 10f;
        _laserBeamSource.maxDistance = 50f;
        _laserBeamSource.dopplerLevel = 0f;

        _reflectShieldSource = gameObject.AddComponent<AudioSource>();
        _reflectShieldSource.playOnAwake = false;
        _reflectShieldSource.loop = true;
        _reflectShieldSource.spatialBlend = 1f; // Full 3D sound
        _reflectShieldSource.rolloffMode = AudioRolloffMode.Linear;
        _reflectShieldSource.minDistance = 10f;
        _reflectShieldSource.maxDistance = 50f;
        _reflectShieldSource.dopplerLevel = 0f;

        _gigaBlastChargeSource = gameObject.AddComponent<AudioSource>();
        _gigaBlastChargeSource.playOnAwake = false;
        _gigaBlastChargeSource.loop = true;
        _gigaBlastChargeSource.spatialBlend = 1f; // Full 3D sound
        _gigaBlastChargeSource.rolloffMode = AudioRolloffMode.Linear;
        _gigaBlastChargeSource.minDistance = 10f;
        _gigaBlastChargeSource.maxDistance = 50f;
        _gigaBlastChargeSource.dopplerLevel = 0f;

        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 1f; // Full 3D sound
        _beamHitLoopSource.rolloffMode = AudioRolloffMode.Linear;
        _beamHitLoopSource.minDistance = 10f;
        _beamHitLoopSource.maxDistance = 50f;
        _beamHitLoopSource.dopplerLevel = 0f;
    }

    private AudioSource GetAvailableAudioSource()
    {
        // Find first non-playing audio source
        foreach (var source in _audioSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        // If all busy, reuse the first one
        return _audioSourcePool[0];
    }

    private void PlayOneShotSound(AudioClip clip, float volume = 1f, AudioClipType clipType = AudioClipType.Default, float pitch = 1f)
    {
        if (clip == null) return;

        float volumeMultiplier = GetVolumeMultiplier(clipType);
        AudioSource source = GetAvailableAudioSource();
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * volumeMultiplier);
    }

    private enum AudioClipType
    {
        Default,
        ProjectileFire,
        LaserBeam,
        TeleportExit,
        TeleportArrival,
        GigaBlastCharge,
        GigaBlastTier1Fire,
        GigaBlastTier2Fire,
        GigaBlastTier3Fire,
        GigaBlastTier4Fire,
        ReflectShieldLoop,
        BulletReflection,
        ShieldDamage,
        HullDamage,
        BeamHitLoop
    }

    private float GetVolumeMultiplier(AudioClipType clipType)
    {
        return clipType switch
        {
            AudioClipType.ProjectileFire => audioVolume.projectileFireVolume,
            AudioClipType.LaserBeam => audioVolume.laserBeamVolume,
            AudioClipType.TeleportExit => audioVolume.teleportExitVolume,
            AudioClipType.TeleportArrival => audioVolume.teleportArrivalVolume,
            AudioClipType.GigaBlastCharge => audioVolume.gigaBlastChargeVolume,
            AudioClipType.GigaBlastTier1Fire => audioVolume.gigaBlastTier1FireVolume,
            AudioClipType.GigaBlastTier2Fire => audioVolume.gigaBlastTier2FireVolume,
            AudioClipType.GigaBlastTier3Fire => audioVolume.gigaBlastTier3FireVolume,
            AudioClipType.GigaBlastTier4Fire => audioVolume.gigaBlastTier4FireVolume,
            AudioClipType.ReflectShieldLoop => audioVolume.reflectShieldLoopVolume,
            AudioClipType.BulletReflection => audioVolume.bulletReflectionVolume,
            AudioClipType.ShieldDamage => audioVolume.shieldDamageVolume,
            AudioClipType.HullDamage => audioVolume.hullDamageVolume,
            AudioClipType.BeamHitLoop => audioVolume.beamHitLoopVolume,
            _ => 1f
        };
    }

    private void StartEngineRumble()
    {
        if (engineRumbleSound == null || _engineRumbleSource == null) return;

        if (!_engineRumbleSource.isPlaying)
        {
            _engineRumbleSource.clip = engineRumbleSound;
            _engineRumbleSource.volume = 0f;
            _engineRumbleSource.Play();
        }

        // Fade in
        if (_engineFadeCoroutine != null)
        {
            StopCoroutine(_engineFadeCoroutine);
        }
        _engineFadeCoroutine = StartCoroutine(FadeEngineVolume(audioVolume.engineRumbleVolume));
    }

    private void StopEngineRumble()
    {
        if (_engineRumbleSource == null || !_engineRumbleSource.isPlaying) return;

        // Fade out
        if (_engineFadeCoroutine != null)
        {
            StopCoroutine(_engineFadeCoroutine);
        }
        _engineFadeCoroutine = StartCoroutine(FadeEngineVolume(0f));
    }

    private System.Collections.IEnumerator FadeEngineVolume(float targetVolume)
    {
        float startVolume = _engineRumbleSource.volume;
        float elapsed = 0f;

        while (elapsed < engineRumbleFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / engineRumbleFadeDuration;
            _engineRumbleSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        _engineRumbleSource.volume = targetVolume;

        // Stop playing if faded to zero
        if (targetVolume <= 0f)
        {
            _engineRumbleSource.Stop();
        }
    }

    private System.Collections.IEnumerator FadeBeamVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_laserBeamSource == null) yield break;

        float startVolume = _laserBeamSource.volume;
        float elapsed = 0f;

        while (elapsed < beamSoundFadeDuration)
        {
            // Safety check: stop fade if source becomes null or stops playing unexpectedly
            if (_laserBeamSource == null || (!_laserBeamSource.isPlaying && targetVolume > 0f))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / beamSoundFadeDuration;
            _laserBeamSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        // Final safety check before setting volume and stopping
        if (_laserBeamSource == null) yield break;

        _laserBeamSource.volume = targetVolume;

        // Stop playing if faded to zero and stopAfterFade is true
        if (targetVolume <= 0f && stopAfterFade && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
    }

    private System.Collections.IEnumerator FadeGigaBlastChargeVolume(float targetVolume, bool stopAfterFade = false)
    {
        if (_gigaBlastChargeSource == null) yield break;

        float startVolume = _gigaBlastChargeSource.volume;
        float elapsed = 0f;

        while (elapsed < gigaBlastChargeSoundFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / gigaBlastChargeSoundFadeDuration;
            _gigaBlastChargeSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        _gigaBlastChargeSource.volume = targetVolume;

        // Stop playing if faded to zero and stopAfterFade is true
        if (targetVolume <= 0f && stopAfterFade)
        {
            _gigaBlastChargeSource.Stop();
        }
    }

    void OnMove()
    {
        // input callback kept for compatibility with PlayerInput actions.
        // we read the move action state directly in FixedUpdate.
    }

    void OnLook(InputValue value)
    {
        // Store the look input from controller (Vector2 from left stick)
        _lookInput = value.Get<Vector2>();

        // Track if controller is being actively used
        if (_lookInput.magnitude > controllerLookDeadzone)
        {
            _usingControllerLook = true;
            _lastControllerLookTime = Time.time;
        }
    }

    void OnToggleFriction()
    {
        _frictionEnabled = !_frictionEnabled;
        // reset timer when toggling to avoid an immediate application
        _frictionTimer = 0f;
        Debug.Log($"friction: {(_frictionEnabled ? "ON" : "OFF")}");
    }

    protected override void Update()
    {
        base.Update();
        HandleRotation();

        // Calculate and update Cooldown UI animations (Clock Wipe)
        UpdateCooldownUI();

        // Check for shield regeneration every frame
        HandleShieldRegeneration();

        // Stop beam hit loop sound if not taking beam damage
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            float timeSinceLastHit = Time.time - _lastDamageTime;
            if (timeSinceLastHit > beamDetectionWindow)
            {
                _beamHitLoopSource.Stop();
            }
        }

        // Handle continuous firing
        if (_isFiring)
        {
            TryFireProjectile();
        }

        // Handle beam capacity drain when beam is not active (capacity decreases over time)
        if (_activeBeam == null && _currentBeamCapacity > 0f)
        {
            _currentBeamCapacity = Mathf.Max(_currentBeamCapacity - beamCapacityRegenRate * Time.deltaTime, 0f);
            UpdateBeamCapacityBar();
        }

        // Handle GigaBlast charging particle system switching
        if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int newTier = GetChargeTier(chargeTime);

            // If tier changed, switch particle systems
            if (newTier != _currentChargeTier)
            {
                StopCurrentChargeParticle();
                _currentChargeTier = newTier;
                PlayChargeParticleForTier(newTier);
            }
        }
    }

    // --- COOLDOWN UI LOGIC ---
    void UpdateCooldownUI()
    {
        float minAlpha = cooldownAlpha / 255f;
        float maxAlpha = readyAlpha / 255f;

        // REFLECT UI
        if (reflectAbility.cooldownOverlay != null)
        {
            float timeSinceUse = Time.time - _lastReflectTime;
            // 1.0 means fully cooling down (dark), 0.0 means ready (transparent)
            float fraction = 1f - (timeSinceUse / reflectAbility.cooldown);
            reflectAbility.cooldownOverlay.fillAmount = Mathf.Clamp01(fraction);

            // Sub-icon alpha
            if (reflectAbility.subIcon != null)
            {
                bool isReady = timeSinceUse >= reflectAbility.cooldown;
                Color c = reflectAbility.subIcon.color;
                c.a = isReady ? maxAlpha : minAlpha;
                reflectAbility.subIcon.color = c;
            }
        }

        // TELEPORT UI
        if (teleportAbility.cooldownOverlay != null)
        {
            float timeSinceUse = Time.time - _lastTeleportTime;
            float fraction = 1f - (timeSinceUse / teleportAbility.cooldown);
            teleportAbility.cooldownOverlay.fillAmount = Mathf.Clamp01(fraction);

            // Sub-icon alpha
            if (teleportAbility.subIcon != null)
            {
                bool isReady = timeSinceUse >= teleportAbility.cooldown;
                Color c = teleportAbility.subIcon.color;
                c.a = isReady ? maxAlpha : minAlpha;
                teleportAbility.subIcon.color = c;
            }
        }

        // GIGABLAST UI
        if (gigaBlastAbility.cooldownOverlay != null)
        {
            float timeSinceUse = Time.time - _lastGigaBlastTime;
            float fraction = 1f - (timeSinceUse / gigaBlastAbility.cooldown);
            gigaBlastAbility.cooldownOverlay.fillAmount = Mathf.Clamp01(fraction);

            // Sub-icon alpha
            if (gigaBlastAbility.subIcon != null)
            {
                bool isReady = timeSinceUse >= gigaBlastAbility.cooldown;
                Color c = gigaBlastAbility.subIcon.color;
                c.a = isReady ? maxAlpha : minAlpha;
                gigaBlastAbility.subIcon.color = c;
            }
        }

        // BEAM SUB-ICON - scales alpha based on capacity (inverted: full alpha when empty, low alpha when full/overheated)
        if (beamSubIcon != null)
        {
            float capacityFraction = _currentBeamCapacity / beamCapacity;
            // Invert: maxAlpha when capacity is 0 (ready), minAlpha when capacity is full (overheated)
            float alpha = Mathf.Lerp(maxAlpha, minAlpha, capacityFraction);
            Color c = beamSubIcon.color;
            c.a = alpha;
            beamSubIcon.color = c;
        }
    }

    // --- NEW REGENERATION LOGIC START ---
    private void HandleShieldRegeneration()
    {
        // 1. If shields are full (or invalid), turn OFF visuals and exit
        if (currentShield >= maxShield || maxShield <= 0)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        // 2. If waiting on cooldown, turn OFF visuals and exit
        if (Time.time < _lastShieldHitTime + shieldRegenDelay)
        {
            if (shieldController != null) shieldController.SetRegeneration(false);
            return;
        }

        // 3. Regenerate
        currentShield += shieldRegenRate * Time.deltaTime;

        // 4. Clamp to max
        if (currentShield > maxShield)
        {
            currentShield = maxShield;
        }

        // 5. If we recovered from 0 shields, re-enable the flag
        if (currentShield > 0 && !hasShield)
        {
            hasShield = true;
        }

        // 6. Update UI and turn ON visuals
        UpdateShieldBar();
        if (shieldController != null) shieldController.SetRegeneration(true);
    }
    // --- NEW REGENERATION LOGIC END ---

    // --- OVERRIDE TAKE DAMAGE START ---
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        // Player is immune to damage during reflect ability
        if (reflectAbility.shield != null && reflectAbility.shield.IsActive())
        {
            return;
        }

        // Store shield value before damage
        float previousShield = currentShield;

        // Update variable for shield logic
        _lastShieldHitTime = Time.time;

        base.TakeDamage(damage, impactForce, hitPoint, source);

        // Handle beam damage sound (looping)
        if (source == DamageSource.LaserBeam)
        {
            // Start beam hit loop sound if not already playing
            if (beamHitLoopSound != null && _beamHitLoopSource != null && !_beamHitLoopSource.isPlaying)
            {
                _beamHitLoopSource.clip = beamHitLoopSound;
                _beamHitLoopSource.volume = audioVolume.beamHitLoopVolume;
                _beamHitLoopSource.Play();
            }
        }
        else
        {
            // Play one-shot damage sound based on whether shield or hull was hit
            if (previousShield > 0f)
            {
                // Shield was hit
                PlayOneShotSound(shieldDamageSound, 1f, AudioClipType.ShieldDamage);
            }
            else
            {
                // Hull was hit (no shield)
                PlayOneShotSound(hullDamageSound, 1f, AudioClipType.HullDamage);
            }
        }

        if (enableChromaticAberration && _chromaticAberration != null)
        {
            HandleChromaticAberration(impactForce);
        }
    }
    // --- OVERRIDE TAKE DAMAGE END ---

    protected override void FixedUpdate()
    {
        // Disable physics during teleport
        if (_isTeleporting)
        {
            return;
        }

        base.FixedUpdate();

        // if there is no action mapping (defensive), don't crash
        bool movePressed = _moveAction != null && _moveAction.IsPressed();

        // Disable movement when beam is active, but allow reduced movement while charging GigaBlast
        if (movePressed && _activeBeam == null)
        {
            // thrust input active
            _isThrusting = true;
            Vector2 thrustDirection = transform.up;

            // Apply movement penalty if charging GigaBlast
            float thrustMultiplier = 1f;
            if (_isCharging)
            {
                float chargeTime = Time.time - _chargeStartTime;
                int tier = GetChargeTier(chargeTime);
                thrustMultiplier = tier switch
                {
                    1 => gigaBlastAbility.tier1ThrustMultiplier,
                    2 => gigaBlastAbility.tier2ThrustMultiplier,
                    3 => gigaBlastAbility.tier3ThrustMultiplier,
                    4 => gigaBlastAbility.tier4ThrustMultiplier,
                    _ => 1f
                };
            }

            _rb.AddForce(thrustDirection * thrustForce * thrustMultiplier);
            ApplyLateralDamping();

            // reset friction timer while actively thrusting
            _frictionTimer = 0f;

            // Start engine rumble sound
            StartEngineRumble();
        }
        else
        {
            // not thrusting
            _isThrusting = false;

            if (_frictionEnabled)
            {
                // accumulate time since thrust ended
                _frictionTimer += Time.fixedDeltaTime;

                if (_frictionTimer >= frictionDelay)
                {
                    ApplyFriction();
                }
                else if (frictionDebug)
                {
                    Debug.Log($"friction waiting: {_frictionTimer:F2}/{frictionDelay:F2}");
                }
            }

            // Stop engine rumble sound
            StopEngineRumble();
        }

        ClampVelocity();

        // Apply continuous recoil while beam is active and fill capacity
        if (_activeBeam != null)
        {
            float recoilForceThisFrame = _activeBeam.GetRecoilForcePerSecond() * Time.fixedDeltaTime;
            ApplyRecoil(recoilForceThisFrame);

            // Fill beam capacity (bar increases while firing)
            _currentBeamCapacity = Mathf.Min(_currentBeamCapacity + beamCapacityDrainRate * Time.fixedDeltaTime, beamCapacity);
            UpdateBeamCapacityBar();

            // Stop beam if capacity is full (overheated)
            if (_currentBeamCapacity >= beamCapacity)
            {
                Debug.Log("Beam capacity full! Stopping beam.");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                // Stop laser beam sound with fade out
                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    // Stop any existing fade coroutine
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                // Safety: ensure sound is stopped even if not currently playing
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }

    void ApplyFriction()
    {
        // move velocity toward zero at a fixed rate (frictionDeceleration units per second)
        // this produces smooth, frame-rate-independent deceleration
        Vector2 currentVel = _rb.linearVelocity;
        Vector2 newVel = Vector2.MoveTowards(currentVel, Vector2.zero, frictionDeceleration * Time.fixedDeltaTime);
        _rb.linearVelocity = newVel;

        if (frictionDebug)
        {
            Debug.Log($"applying friction: vel {currentVel.magnitude:F2} -> {newVel.magnitude:F2}");
        }
    }

    void OnFire(InputValue value)
    {
        // Set firing state based on input value (0 = not firing, >0 = firing)
        _isFiring = value.Get<float>() > 0f;
    }

    void TryFireProjectile()
    {
        // Cannot fire when game is paused
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        // Cannot fire normal weapon when beam is active or charging GigaBlast
        if (_activeBeam != null || _isCharging)
            return;

        // Check if projectile weapon is configured
        if (projectileWeapon.prefab == null)
            return;

        // Check cooldown
        if (Time.time < _lastFireTime + fireCooldown)
            return;

        foreach (var turret in turrets)
        {
            GameObject projectile = Instantiate(projectileWeapon.prefab, turret.transform.position, transform.rotation);

            if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
            {
                projectileScript.targetTag = "Enemy";
                projectileScript.Initialize(
                    transform.up,
                    Vector2.zero,
                    projectileWeapon.speed,
                    projectileWeapon.damage,
                    projectileWeapon.lifetime,
                    projectileWeapon.impactForce,
                    this
                );
            }
        }
        ApplyRecoil(projectileWeapon.recoilForce);

        // Play projectile fire sound
        PlayOneShotSound(projectileFireSound, 1f, AudioClipType.ProjectileFire);

        // Update last fire time
        _lastFireTime = Time.time;
    }

    void OnFireBeam(InputValue value)
    {
        Debug.Log($"Fire Beam input received - isPressed: {value.isPressed}");

        // Block starting beam when game is paused, but always allow release
        if (sceneManager != null && sceneManager.IsPaused() && value.isPressed)
            return;

        if (value.isPressed)
        {
            // Block if charging GigaBlast
            if (_isCharging)
            {
                Debug.Log("Cannot fire beam while charging GigaBlast");
                return;
            }

            // Block if capacity is full (overheated)
            if (_currentBeamCapacity >= beamCapacity)
            {
                Debug.Log("Cannot fire beam: capacity full (overheated)");
                return;
            }

            // Only create beam if we don't have one already and beam weapon is configured
            if (_activeBeam == null && beamWeapon.prefab != null)
            {
                Debug.Log("Creating and starting beam");

                // Spawn 1 unit forward from the ship (in the direction it's facing)
                Vector3 spawnPosition = transform.position + transform.up * beamOffsetDistance;

                GameObject beamObj = Instantiate(beamWeapon.prefab, spawnPosition, transform.rotation, transform);
                _activeBeam = beamObj.GetComponent<LaserBeam>();
                _activeBeam.Initialize(
                    "Enemy",
                    beamWeapon.damagePerSecond,
                    beamWeapon.maxDistance,
                    beamWeapon.recoilForcePerSecond,
                    beamWeapon.impactForce,
                    this
                );
                _activeBeam.StartFiring();

                // Start laser beam sound with fade in
                if (laserBeamSound != null && _laserBeamSource != null)
                {
                    _laserBeamSource.clip = laserBeamSound;
                    _laserBeamSource.volume = 0f;
                    _laserBeamSource.Play();

                    // Stop existing fade coroutine and start new one
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(audioVolume.laserBeamVolume));
                }
            }
        }
        else
        {
            // Stop and destroy the existing beam
            if (_activeBeam != null)
            {
                Debug.Log("Stopping and destroying beam");
                _activeBeam.StopFiring();
                Destroy(_activeBeam.gameObject);
                _activeBeam = null;

                // Stop laser beam sound with fade out
                if (_laserBeamSource != null && _laserBeamSource.isPlaying)
                {
                    // Stop any existing fade coroutine
                    if (_beamFadeCoroutine != null)
                    {
                        StopCoroutine(_beamFadeCoroutine);
                    }
                    _beamFadeCoroutine = StartCoroutine(FadeBeamVolume(0f, stopAfterFade: true));
                }
                // Safety: ensure sound is stopped even if not currently playing
                else if (_laserBeamSource != null && !_laserBeamSource.isPlaying)
                {
                    _laserBeamSource.Stop();
                }
            }
        }
    }

    void OnReflect()
    {
        // Block when game is paused
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        // Check cooldown
        if (Time.time < _lastReflectTime + reflectAbility.cooldown)
        {
            Debug.Log($"Reflect on cooldown: {(_lastReflectTime + reflectAbility.cooldown - Time.time):F1}s remaining");
            return;
        }

        // Check if shield is assigned
        if (reflectAbility.shield == null)
        {
            Debug.LogWarning("Reflect shield not assigned!");
            return;
        }

        // Activate reflect shield
        _lastReflectTime = Time.time;

        if (_reflectCoroutine != null)
        {
            StopCoroutine(_reflectCoroutine);
        }
        _reflectCoroutine = StartCoroutine(ActivateReflectShield());
    }

    private System.Collections.IEnumerator ActivateReflectShield()
    {
        // Activate shield
        reflectAbility.shield.Activate(reflectAbility.reflectedProjectileColor);

        // Start reflect shield loop sound
        if (reflectShieldLoopSound != null && _reflectShieldSource != null)
        {
            _reflectShieldSource.clip = reflectShieldLoopSound;
            _reflectShieldSource.volume = audioVolume.reflectShieldLoopVolume;
            _reflectShieldSource.Play();
        }

        // Wait for duration
        yield return new WaitForSeconds(reflectAbility.activeDuration);

        // Deactivate shield
        reflectAbility.shield.Deactivate();

        // Stop reflect shield loop sound
        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
    }

    void OnTeleport()
    {
        // Block when game is paused
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        // Check cooldown
        if (Time.time < _lastTeleportTime + teleportAbility.cooldown)
        {
            Debug.Log($"Teleport on cooldown: {(_lastTeleportTime + teleportAbility.cooldown - Time.time):F1}s remaining");
            return;
        }

        // Check if already teleporting
        if (_isTeleporting)
        {
            return;
        }

        // Get mouse position in world space
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 targetWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        targetWorldPosition.z = transform.position.z; // Maintain Z depth

        // Start teleport
        _lastTeleportTime = Time.time;

        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
        }
        _teleportCoroutine = StartCoroutine(ExecuteTeleport(targetWorldPosition));
    }

    private System.Collections.IEnumerator ExecuteTeleport(Vector3 targetPosition)
    {
        _isTeleporting = true;

        // Store original scale
        Vector3 originalScale = transform.localScale;
        Vector3 normalScale = originalScale * teleportAbility.normalScale;

        // Calculate squeeze scale at origin (narrow width, tall height)
        Vector3 originSqueezeScale = new Vector3(
            originalScale.x * teleportAbility.originScaleX,
            originalScale.y * teleportAbility.originScaleY,
            originalScale.z
        );

        // Calculate overshoot scale at destination (uniform pop)
        Vector3 destinationPopScale = originalScale * teleportAbility.destinationOvershootScale;

        // Disable collider during teleport
        Collider2D playerCollider = GetComponent<Collider2D>();
        bool colliderWasEnabled = false;
        if (playerCollider != null)
        {
            colliderWasEnabled = playerCollider.enabled;
            playerCollider.enabled = false;
        }

        // Phase 1: Pre-teleport delay
        if (teleportAbility.preTeleportDelay > 0)
        {
            yield return new WaitForSeconds(teleportAbility.preTeleportDelay);
        }

        // Phase 2: Squeeze at origin (compress to narrow vertical line)
        float elapsed = 0f;
        while (elapsed < teleportAbility.shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleportAbility.shrinkDuration;
            transform.localScale = Vector3.Lerp(originalScale, originSqueezeScale, t);
            yield return null;
        }
        transform.localScale = originSqueezeScale;

        // Spawn origin particle effect
        if (teleportAbility.teleportOriginEffect != null)
        {
            Instantiate(teleportAbility.teleportOriginEffect, transform.position, Quaternion.identity);
        }

        // Play teleport exit sound
        PlayOneShotSound(teleportExitSound, 1f, AudioClipType.TeleportExit);

        // Disable sprite renderer during teleport
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        bool spriteWasEnabled = false;
        if (spriteRenderer != null)
        {
            spriteWasEnabled = spriteRenderer.enabled;
            spriteRenderer.enabled = false;
        }

        // Phase 3: Instant teleport
        Vector3 previousPosition = transform.position;
        transform.position = targetPosition;

        // Notify Cinemachine that player has warped to prevent camera from smoothly following
        // Find all Cinemachine cameras and notify them of the warp
        var cinemachineCameras = FindObjectsOfType<Unity.Cinemachine.CinemachineCamera>();
        foreach (var cam in cinemachineCameras)
        {
            if (cam.Target.TrackingTarget == transform)
            {
                cam.OnTargetObjectWarped(transform, targetPosition - previousPosition);
            }
        }

        // Handle velocity
        if (teleportAbility.resetVelocity)
        {
            _rb.linearVelocity = Vector2.zero;
        }
        else if (teleportAbility.dampVelocity)
        {
            _rb.linearVelocity *= teleportAbility.velocityDampFactor;
        }

        // Chromatic aberration flash
        if (teleportAbility.enableChromaticFlash && _chromaticAberration != null)
        {
            _currentChromaticIntensity = Mathf.Min(_currentChromaticIntensity + teleportAbility.chromaticFlashIntensity, maxChromaticIntensity);
            _chromaticAberration.intensity.value = _currentChromaticIntensity;

            if (_chromaticFadeCoroutine != null)
            {
                StopCoroutine(_chromaticFadeCoroutine);
            }
            _chromaticFadeCoroutine = StartCoroutine(FadeChromaticAberration());
        }

        // Screen shake
        if (teleportAbility.enableScreenShake && _impulseSource != null)
        {
            _impulseSource.GenerateImpulse(teleportAbility.screenShakeStrength);
        }

        // Spawn destination particle effect
        if (teleportAbility.teleportDestinationEffect != null)
        {
            Instantiate(teleportAbility.teleportDestinationEffect, transform.position, Quaternion.identity);
        }

        // Play teleport arrival sound
        PlayOneShotSound(teleportArrivalSound, 1f, AudioClipType.TeleportArrival);

        // Re-enable sprite renderer
        if (spriteRenderer != null && spriteWasEnabled)
        {
            spriteRenderer.enabled = true;
        }

        // Phase 4: Pop at destination (overshoot then settle to normal)
        elapsed = 0f;
        while (elapsed < teleportAbility.growDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / teleportAbility.growDuration;
            transform.localScale = Vector3.Lerp(destinationPopScale, normalScale, t);
            yield return null;
        }
        transform.localScale = normalScale;

        // Re-enable collider
        if (playerCollider != null && colliderWasEnabled)
        {
            playerCollider.enabled = true;
        }

        _isTeleporting = false;
    }

    void OnGigaBlast(InputValue value)
    {
        Debug.Log($"GigaBlast input received - isPressed: {value.isPressed}");

        // Block starting charge when game is paused, but always allow release
        if (sceneManager != null && sceneManager.IsPaused() && value.isPressed)
            return;

        if (value.isPressed)
        {
            // Start charging
            if (!_isCharging && Time.time >= _lastGigaBlastTime + gigaBlastAbility.cooldown)
            {
                // Block if beam is active or other abilities are active
                if (_activeBeam != null || _isTeleporting ||
                    (reflectAbility.shield != null && reflectAbility.shield.IsActive()))
                {
                    Debug.Log("Cannot charge GigaBlast: other abilities active");
                    return;
                }

                _isCharging = true;
                _chargeStartTime = Time.time;
                _currentChargeTier = 1; // Start at tier 1

                // Start tier 1 particle effect
                PlayChargeParticleForTier(1);

                // Start charging sound with fade in
                if (gigaBlastChargeSound != null && _gigaBlastChargeSource != null)
                {
                    _gigaBlastChargeSource.clip = gigaBlastChargeSound;
                    _gigaBlastChargeSource.volume = 0f;
                    _gigaBlastChargeSource.Play();

                    // Stop existing fade coroutine and start new one
                    if (_gigaBlastChargeFadeCoroutine != null)
                    {
                        StopCoroutine(_gigaBlastChargeFadeCoroutine);
                    }
                    _gigaBlastChargeFadeCoroutine = StartCoroutine(FadeGigaBlastChargeVolume(audioVolume.gigaBlastChargeVolume));
                }

                Debug.Log("GigaBlast charging started");
            }
        }
        else
        {
            // Release charging
            if (_isCharging)
            {
                float chargeTime = Time.time - _chargeStartTime;

                // Only fire if minimum charge time met
                if (chargeTime >= gigaBlastAbility.minChargeTime)
                {
                    FireChargedShot(chargeTime);
                    _lastGigaBlastTime = Time.time;
                }
                else
                {
                    Debug.Log($"GigaBlast released too early: {chargeTime:F2}s < {gigaBlastAbility.minChargeTime:F2}s");
                }

                // Stop charging and all particle effects
                _isCharging = false;
                _currentChargeTier = 0;
                StopAllChargeParticles();

                // Stop charging sound with fade out
                if (_gigaBlastChargeSource != null && _gigaBlastChargeSource.isPlaying)
                {
                    if (_gigaBlastChargeFadeCoroutine != null)
                    {
                        StopCoroutine(_gigaBlastChargeFadeCoroutine);
                    }
                    _gigaBlastChargeFadeCoroutine = StartCoroutine(FadeGigaBlastChargeVolume(0f, stopAfterFade: true));
                }
            }
        }
    }

    private void PlayChargeParticleForTier(int tier)
    {
        ParticleSystem particleToPlay = tier switch
        {
            1 => gigaBlastAbility.tier1ParticleEffect,
            2 => gigaBlastAbility.tier2ParticleEffect,
            3 => gigaBlastAbility.tier3ParticleEffect,
            4 => gigaBlastAbility.tier4ParticleEffect,
            _ => null
        };

        if (particleToPlay != null)
        {
            Debug.Log($"Playing Tier {tier} particle effect: {particleToPlay.name}");
            particleToPlay.Play();
        }
        else
        {
            Debug.LogWarning($"Tier {tier} particle effect is null!");
        }
    }

    private void StopCurrentChargeParticle()
    {
        ParticleSystem particleToStop = _currentChargeTier switch
        {
            1 => gigaBlastAbility.tier1ParticleEffect,
            2 => gigaBlastAbility.tier2ParticleEffect,
            3 => gigaBlastAbility.tier3ParticleEffect,
            4 => gigaBlastAbility.tier4ParticleEffect,
            _ => null
        };

        if (particleToStop != null)
        {
            Debug.Log($"Stopping Tier {_currentChargeTier} particle effect: {particleToStop.name}");
            particleToStop.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void StopAllChargeParticles()
    {
        Debug.Log("Stopping all GigaBlast charge particles");
        if (gigaBlastAbility.tier1ParticleEffect != null) gigaBlastAbility.tier1ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier2ParticleEffect != null) gigaBlastAbility.tier2ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier3ParticleEffect != null) gigaBlastAbility.tier3ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (gigaBlastAbility.tier4ParticleEffect != null) gigaBlastAbility.tier4ParticleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private int GetChargeTier(float chargeTime)
    {
        if (chargeTime >= gigaBlastAbility.tier4Time) return 4;
        if (chargeTime >= gigaBlastAbility.tier3Time) return 3;
        if (chargeTime >= gigaBlastAbility.tier2Time) return 2;
        if (chargeTime >= gigaBlastAbility.tier1Time) return 1;
        return 1; // Default to tier 1 if below all thresholds
    }

    private void FireChargedShot(float chargeTime)
    {
        // Clamp charge time to max
        chargeTime = Mathf.Min(chargeTime, gigaBlastAbility.maxChargeTime);

        // Get charge tier
        int tier = GetChargeTier(chargeTime);

        // Get base projectile stats
        float baseSpeed = projectileWeapon.speed;
        float baseDamage = projectileWeapon.damage;
        float baseRecoil = projectileWeapon.recoilForce;
        float baseImpact = projectileWeapon.impactForce;

        // Get tier-based multipliers
        float speedMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1SpeedMultiplier,
            2 => gigaBlastAbility.tier2SpeedMultiplier,
            3 => gigaBlastAbility.tier3SpeedMultiplier,
            4 => gigaBlastAbility.tier4SpeedMultiplier,
            _ => 1f
        };

        float damageMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1DamageMultiplier,
            2 => gigaBlastAbility.tier2DamageMultiplier,
            3 => gigaBlastAbility.tier3DamageMultiplier,
            4 => gigaBlastAbility.tier4DamageMultiplier,
            _ => 1f
        };

        float recoilMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1RecoilMultiplier,
            2 => gigaBlastAbility.tier2RecoilMultiplier,
            3 => gigaBlastAbility.tier3RecoilMultiplier,
            4 => gigaBlastAbility.tier4RecoilMultiplier,
            _ => 1f
        };

        float impactMultiplier = tier switch
        {
            1 => gigaBlastAbility.tier1ImpactMultiplier,
            2 => gigaBlastAbility.tier2ImpactMultiplier,
            3 => gigaBlastAbility.tier3ImpactMultiplier,
            4 => gigaBlastAbility.tier4ImpactMultiplier,
            _ => 1f
        };

        // Calculate final stats
        float finalSpeed = baseSpeed * speedMultiplier;
        float finalDamage = baseDamage * damageMultiplier;
        float finalRecoil = baseRecoil * recoilMultiplier;
        float finalImpact = baseImpact * impactMultiplier;

        // Get tier-specific projectile prefab
        GameObject projectilePrefab = tier switch
        {
            1 => gigaBlastAbility.tier1ProjectilePrefab,
            2 => gigaBlastAbility.tier2ProjectilePrefab,
            3 => gigaBlastAbility.tier3ProjectilePrefab,
            4 => gigaBlastAbility.tier4ProjectilePrefab,
            _ => null
        };

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"GigaBlast Tier {tier} projectile prefab not assigned!");
            return;
        }

        // Spawn projectile at ship tip (similar to beam offset)
        Vector3 spawnPosition = transform.position + transform.up * beamOffsetDistance;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, transform.rotation);

        // Initialize projectile - NO modifications to scale, color, or trail
        if (projectile.TryGetComponent<ProjectileScript>(out var projectileScript))
        {
            projectileScript.targetTag = "Enemy";
            projectileScript.Initialize(
                transform.up,
                Vector2.zero,
                finalSpeed,
                finalDamage,
                gigaBlastAbility.projectileLifetime,
                finalImpact,
                this
            );

            // Set pierce behavior based on tier
            if (tier >= 3)
            {
                float pierceMultiplier = (tier == 3) ? gigaBlastAbility.tier3PierceMultiplier : gigaBlastAbility.tier4PierceMultiplier;
                projectileScript.EnablePiercing(pierceMultiplier);
            }
        }

        // Apply recoil
        ApplyRecoil(finalRecoil);

        // Play tier-specific fire sound
        AudioClip fireSound = tier switch
        {
            1 => gigaBlastTier1FireSound,
            2 => gigaBlastTier2FireSound,
            3 => gigaBlastTier3FireSound,
            4 => gigaBlastTier4FireSound,
            _ => null
        };
        AudioClipType fireSoundType = tier switch
        {
            1 => AudioClipType.GigaBlastTier1Fire,
            2 => AudioClipType.GigaBlastTier2Fire,
            3 => AudioClipType.GigaBlastTier3Fire,
            4 => AudioClipType.GigaBlastTier4Fire,
            _ => AudioClipType.Default
        };
        float fireSoundPitch = tier switch
        {
            3 => audioVolume.gigaBlastTier3ChargePitch,
            4 => audioVolume.gigaBlastTier4ChargePitch,
            _ => 1f
        };
        PlayOneShotSound(fireSound, 1f, fireSoundType, fireSoundPitch);

        Debug.Log($"GigaBlast fired! Tier: {tier}, Charge: {chargeTime:F2}s, Damage: {finalDamage:F1}, Speed: {finalSpeed:F1}");
    }

    void OnPause()
    {
        if (sceneManager != null)
        {
            sceneManager.TogglePause();
        }
    }

    void HandleRotation()
    {
        // Skip rotation while paused
        if (sceneManager != null && sceneManager.IsPaused())
            return;

        // Detect mouse movement
        Vector2 currentMousePosition = Mouse.current.position.ReadValue();
        if (Vector2.Distance(currentMousePosition, _lastMousePosition) > mouseMovementThreshold)
        {
            _lastMousePosition = currentMousePosition;
            _lastMouseMoveTime = Time.time;
        }

        // Check which input was used most recently
        bool controllerUsedRecently = _lookInput.magnitude > controllerLookDeadzone;
        bool mouseUsedMoreRecently = _lastMouseMoveTime > _lastControllerLookTime;

        if (controllerUsedRecently)
        {
            // Actively using controller - rotate with stick
            RotateWithController();
        }
        else if (mouseUsedMoreRecently)
        {
            // Mouse was moved more recently than controller - use mouse rotation
            RotateTowardMouse();
        }
        // Else: neither input is active - maintain current rotation
    }

    void RotateWithController()
    {
        // Calculate target angle from look input
        float targetAngle = Mathf.Atan2(_lookInput.y, _lookInput.x) * Mathf.Rad2Deg;
        float currentAngle = transform.eulerAngles.z;

        // Apply rotation speed penalty when beam is active or charging GigaBlast
        float effectiveRotationSpeed = rotationSpeed;
        if (_activeBeam != null)
        {
            effectiveRotationSpeed *= beamRotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => gigaBlastAbility.tier1RotationMultiplier,
                2 => gigaBlastAbility.tier2RotationMultiplier,
                3 => gigaBlastAbility.tier3RotationMultiplier,
                4 => gigaBlastAbility.tier4RotationMultiplier,
                _ => 1f
            };
            effectiveRotationSpeed *= rotationMultiplier;
        }

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, effectiveRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    void RotateTowardMouse()
    {
        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        Vector2 direction = mouseWorldPosition - transform.position;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float currentAngle = transform.eulerAngles.z;

        // Apply rotation speed penalty when beam is active or charging GigaBlast
        float effectiveRotationSpeed = rotationSpeed;
        if (_activeBeam != null)
        {
            effectiveRotationSpeed *= beamRotationMultiplier;
        }
        else if (_isCharging)
        {
            float chargeTime = Time.time - _chargeStartTime;
            int tier = GetChargeTier(chargeTime);
            float rotationMultiplier = tier switch
            {
                1 => gigaBlastAbility.tier1RotationMultiplier,
                2 => gigaBlastAbility.tier2RotationMultiplier,
                3 => gigaBlastAbility.tier3RotationMultiplier,
                4 => gigaBlastAbility.tier4RotationMultiplier,
                _ => 1f
            };
            effectiveRotationSpeed *= rotationMultiplier;
        }

        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle + ROTATION_OFFSET, effectiveRotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    protected override void OnHealthChanged()
    {
        UpdateHealthBar();
    }

    protected override void OnShieldChanged()
    {
        UpdateShieldBar();
    }

    protected override void Die()
    {
        // Stop all looping audio sources before destruction
        if (_laserBeamSource != null && _laserBeamSource.isPlaying)
        {
            _laserBeamSource.Stop();
        }
        if (_engineRumbleSource != null && _engineRumbleSource.isPlaying)
        {
            _engineRumbleSource.Stop();
        }
        if (_reflectShieldSource != null && _reflectShieldSource.isPlaying)
        {
            _reflectShieldSource.Stop();
        }
        if (_gigaBlastChargeSource != null && _gigaBlastChargeSource.isPlaying)
        {
            _gigaBlastChargeSource.Stop();
        }
        if (_beamHitLoopSource != null && _beamHitLoopSource.isPlaying)
        {
            _beamHitLoopSource.Stop();
        }

        // Stop any active fade coroutines
        if (_beamFadeCoroutine != null)
        {
            StopCoroutine(_beamFadeCoroutine);
        }
        if (_engineFadeCoroutine != null)
        {
            StopCoroutine(_engineFadeCoroutine);
        }
        if (_gigaBlastChargeFadeCoroutine != null)
        {
            StopCoroutine(_gigaBlastChargeFadeCoroutine);
        }

        if (sceneManager != null)
        {
            // Play player explosion sound through SceneManager (handles audio when player is destroyed)
            sceneManager.PlayPlayerExplosionSound();
            sceneManager.OnPlayerDestroyed();
        }
        else
        {
            Debug.LogError("sceneManagerScript reference missing! Cannot notify of player destruction.", this);
        }
        // deactivate instead of destroy?
        // gameObject.SetActive(false);

        base.Die();
    }

    private void UpdateHealthBar()
    {
        if (healthBarFillImage != null)
        {
            healthBarFillImage.fillAmount = currentHealth / maxHealth;
            // Removed debug log to avoid spam
            // Debug.Log($"Health bar updated: {currentHealth}/{maxHealth} = {healthBarFillImage.fillAmount}");
        }
        else
        {
            Debug.LogWarning("healthBarFillImage is not assigned in the Inspector!", this);
        }
    }

    private void UpdateShieldBar()
    {
        if (shieldBarFillImage != null)
        {
            shieldBarFillImage.fillAmount = currentShield / maxShield;
        }
        else
        {
            Debug.LogWarning("shieldBarFillImage is not assigned in the Inspector!", this);
        }
    }

    private void UpdateBeamCapacityBar()
    {
        if (beamCapacityBarFillImage != null)
        {
            beamCapacityBarFillImage.fillAmount = _currentBeamCapacity / beamCapacity;
        }
        else if (beamCapacityBarFillImage == null && beamCapacity > 0)
        {
            // Debug.LogWarning("beamCapacityBarFillImage is not assigned in the Inspector!", this);
        }
    }

    private void HandleChromaticAberration(float impactForce)
    {
        float timeSinceLastHit = Time.time - _lastDamageTime;

        // Detect if this is a beam (continuous damage) or projectile (spike)
        bool isBeamHit = timeSinceLastHit < beamDetectionWindow;

        if (isBeamHit)
        {
            // Beam: accumulate intensity over time
            _damageAccumulator += impactForce;
            float targetIntensity = Mathf.Min(_damageAccumulator * chromaticIntensityPerDamage, maxChromaticIntensity);
            _currentChromaticIntensity = Mathf.Lerp(_currentChromaticIntensity, targetIntensity, Time.deltaTime * 5f);
        }
        else
        {
            // Projectile: instant spike
            _damageAccumulator = impactForce;
            _projectileMultiplier = 2f; // Assuming a default multiplier value
            _currentChromaticIntensity = Mathf.Min(impactForce * chromaticIntensityPerDamage * _projectileMultiplier, maxChromaticIntensity);
        }

        _chromaticAberration.intensity.value = _currentChromaticIntensity;
        _lastDamageTime = Time.time;

        // Start/restart fade coroutine
        if (_chromaticFadeCoroutine != null)
        {
            StopCoroutine(_chromaticFadeCoroutine);
        }
        _chromaticFadeCoroutine = StartCoroutine(FadeChromaticAberration());
    }

    private System.Collections.IEnumerator FadeChromaticAberration()
    {
        // Wait a frame to let damage accumulate
        yield return null;

        // Check if still taking damage (beam still connected)
        while (Time.time - _lastDamageTime < beamDetectionWindow)
        {
            yield return null;
        }

        // Beam disconnected or projectile hit - fade out
        while (_currentChromaticIntensity > 0.01f)
        {
            _damageAccumulator = Mathf.Max(0f, _damageAccumulator - chromaticFadeSpeed * Time.deltaTime);
            _currentChromaticIntensity = Mathf.Max(0f, _currentChromaticIntensity - chromaticFadeSpeed * Time.deltaTime);

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = _currentChromaticIntensity;
            }

            yield return null;
        }

        // Ensure it's fully cleared
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = 0f;
        }
        _currentChromaticIntensity = 0f;
        _damageAccumulator = 0f;
    }

    /// <summary>
    /// Allows external systems (like glitch effects) to directly set chromatic aberration intensity.
    /// Set to 0 to disable and let normal fade take over.
    /// </summary>
    public void SetChromaticAberrationIntensity(float intensity)
    {
        if (_chromaticAberration != null)
        {
            _chromaticAberration.intensity.value = Mathf.Clamp(intensity, 0f, maxChromaticIntensity * 2f);
        }
    }

    /// <summary>
    /// Gets the current chromatic aberration intensity.
    /// </summary>
    public float GetChromaticAberrationIntensity()
    {
        if (_chromaticAberration != null)
        {
            return _chromaticAberration.intensity.value;
        }
        return 0f;
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        // Check if reflect shield is active and handle projectile reflection
        if (reflectAbility.shield != null && reflectAbility.shield.IsActive())
        {
            ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
            if (projectile != null && projectile.targetTag == "Player")
            {
                Vector3 hitPoint = collider.ClosestPoint(transform.position);
                reflectAbility.shield.OnReflectHit(hitPoint);
                reflectAbility.shield.ReflectProjectile(projectile);

                // Mark projectile as reflected (used by boss to apply reduced damage)
                projectile.MarkAsReflected();

                // Apply damage multiplier to reflected projectile
                projectile.ApplyDamageMultiplier(reflectAbility.reflectedProjectileDamageMultiplier);

                // Play bullet reflection sound
                PlayOneShotSound(bulletReflectionSound, 1f, AudioClipType.BulletReflection);
            }
        }
    }
}