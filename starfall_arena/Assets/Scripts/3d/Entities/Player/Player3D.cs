using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player3D : Entity3D
{
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

    public PlayerInput3D PlayerInput3D => playerInput3D;
    public PlayerCameraRig3D PlayerCameraRig3D => playerCameraRig3D;

    private AudioSource[] _audioSourcePool;
    private AudioSource _beamHitLoopSource;
    private float _lastBeamDamageTime = float.NegativeInfinity;
    private bool _anchorHeld;

    public bool IsAnchorActive => anchorConfig.enabled && _anchorHeld;

    protected override void Awake()
    {
        base.Awake();
        playerInput3D ??= GetComponent<PlayerInput3D>();
        playerCameraRig3D ??= GetComponent<PlayerCameraRig3D>();
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
    }

    private void Update()
    {
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

    public override void TakeDamage(float damage, Vector3 hitPoint, Entity3D attacker = null, DamageSource3D source = DamageSource3D.Projectile)
    {
        float previousShield = currentShield;
        float previousHealth = currentHealth;

        base.TakeDamage(damage, hitPoint, attacker, source);

        if (currentHealth >= previousHealth && currentShield >= previousShield)
        {
            return;
        }

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
        base.Die();
    }

    private void OnDisable()
    {
        _anchorHeld = false;
        ApplySplitStatePresentation();
        StopBeamHitLoop();
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
            source.spatialBlend = 0f;
            _audioSourcePool[i] = source;
        }

        _beamHitLoopSource = gameObject.AddComponent<AudioSource>();
        _beamHitLoopSource.playOnAwake = false;
        _beamHitLoopSource.loop = true;
        _beamHitLoopSource.spatialBlend = 0f;
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

    protected override float GetExternalRotationMultiplier()
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
}
