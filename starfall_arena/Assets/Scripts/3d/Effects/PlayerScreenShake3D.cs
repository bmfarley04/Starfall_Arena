using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerScreenShake3D : MonoBehaviour
{
    [System.Serializable]
    private struct SpeedShakeConfig3D
    {
        [Tooltip("If disabled, high-speed flight will not create recurring camera shake impulses.")]
        public bool enabled;
        [Tooltip("Ship speed, as a fraction of max speed, where speed shake begins.")]
        [Range(0f, 1f)] public float startSpeedNormalized;
        [Tooltip("Ship speed, as a fraction of max speed, that reaches the strongest configured speed shake.")]
        [Range(0f, 1f)] public float fullShakeSpeedNormalized;
        [Tooltip("Impulse strength when the ship first crosses Start Speed Normalized.")]
        public float minImpulseStrength;
        [Tooltip("Impulse strength when the ship reaches Full Shake Speed Normalized.")]
        public float maxImpulseStrength;
        [Tooltip("Seconds between speed shake impulses at Start Speed Normalized.")]
        public float intervalAtStartSpeed;
        [Tooltip("Seconds between speed shake impulses at Full Shake Speed Normalized.")]
        public float intervalAtFullSpeed;
    }

    [System.Serializable]
    private struct HitShakeConfig3D
    {
        [Tooltip("If disabled, taking damage will not create camera shake impulses.")]
        public bool enabled;
        [Tooltip("Smallest confirmed damage amount that can trigger hit shake.")]
        public float minDamage;
        [Tooltip("Damage amount that maps to the strongest configured hit shake.")]
        public float damageForMaxShake;
        [Tooltip("Impulse strength used at Min Damage.")]
        public float minImpulseStrength;
        [Tooltip("Impulse strength used at Damage For Max Shake.")]
        public float maxImpulseStrength;
        [Tooltip("Multiplier applied when the hit reaches hull health instead of only shields.")]
        public float hullDamageMultiplier;
        [Tooltip("Multiplier applied to beam damage ticks so sustained beams do not overwhelm the camera.")]
        public float beamDamageMultiplier;
        [Tooltip("Multiplier applied to direct damage such as arena or scripted damage.")]
        public float directDamageMultiplier;
        [Tooltip("Minimum seconds between hit shake impulses. Prevents beam ticks and rapid multi-hit bursts from stacking too hard.")]
        public float minImpulseInterval;
    }

    [Header("Impulse Source")]
    [Tooltip("Cinemachine impulse source used for all player camera shake. If unset, this component uses the source on the same GameObject.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("If enabled, a missing CinemachineImpulseSource is added at runtime so the player feedback does not silently fail.")]
    [SerializeField] private bool addMissingImpulseSourceAtRuntime = true;

    [Header("Speed Shake")]
    [SerializeField] private SpeedShakeConfig3D speedShake = new SpeedShakeConfig3D
    {
        enabled = true,
        startSpeedNormalized = 0.72f,
        fullShakeSpeedNormalized = 1f,
        minImpulseStrength = 0.04f,
        maxImpulseStrength = 0.16f,
        intervalAtStartSpeed = 0.32f,
        intervalAtFullSpeed = 0.12f
    };

    [Header("Hit Shake")]
    [SerializeField] private HitShakeConfig3D hitShake = new HitShakeConfig3D
    {
        enabled = true,
        minDamage = 1f,
        damageForMaxShake = 35f,
        minImpulseStrength = 0.25f,
        maxImpulseStrength = 1.2f,
        hullDamageMultiplier = 1.25f,
        beamDamageMultiplier = 0.45f,
        directDamageMultiplier = 0.75f,
        minImpulseInterval = 0.08f
    };

    private ShipFlight3D _shipFlight;
    private NetMovement3D _netMovement;
    private NetCombat3D _netCombat;
    private float _nextSpeedImpulseTime;
    private float _nextHitImpulseTime;
    private bool _warnedMissingImpulseSource;

    private void Awake()
    {
        _shipFlight = GetComponent<ShipFlight3D>();
        _netMovement = GetComponent<NetMovement3D>();
        _netCombat = GetComponent<NetCombat3D>();
        ResolveImpulseSource();
        ValidateConfig();
    }

    private void OnValidate()
    {
        ValidateConfig();
    }

    private void Update()
    {
        UpdateSpeedShake();
    }

    public void TriggerHitShake(float totalDamageTaken, float hullDamageTaken, DamageSource3D damageSource)
    {
        if (!hitShake.enabled || totalDamageTaken < hitShake.minDamage || Time.time < _nextHitImpulseTime)
        {
            return;
        }

        if (!ShouldPlayLocalShake())
        {
            return;
        }

        float damageRange = Mathf.Max(0.01f, hitShake.damageForMaxShake - hitShake.minDamage);
        float damage01 = Mathf.Clamp01((totalDamageTaken - hitShake.minDamage) / damageRange);
        float strength = Mathf.Lerp(hitShake.minImpulseStrength, hitShake.maxImpulseStrength, damage01);

        if (hullDamageTaken > 0f)
        {
            strength *= Mathf.Max(0f, hitShake.hullDamageMultiplier);
        }

        if (damageSource == DamageSource3D.Beam)
        {
            strength *= Mathf.Max(0f, hitShake.beamDamageMultiplier);
        }
        else if (damageSource == DamageSource3D.Direct)
        {
            strength *= Mathf.Max(0f, hitShake.directDamageMultiplier);
        }

        GenerateImpulse(strength);
        _nextHitImpulseTime = Time.time + Mathf.Max(0f, hitShake.minImpulseInterval);
    }

    private void UpdateSpeedShake()
    {
        if (!speedShake.enabled || _shipFlight == null || Time.time < _nextSpeedImpulseTime)
        {
            return;
        }

        if (!ShouldPlayLocalShake())
        {
            return;
        }

        float speed01 = ResolveSpeedNormalized();
        if (speed01 < speedShake.startSpeedNormalized)
        {
            return;
        }

        float range = Mathf.Max(0.01f, speedShake.fullShakeSpeedNormalized - speedShake.startSpeedNormalized);
        float shake01 = Mathf.Clamp01((speed01 - speedShake.startSpeedNormalized) / range);
        float strength = Mathf.Lerp(speedShake.minImpulseStrength, speedShake.maxImpulseStrength, shake01);
        GenerateImpulse(strength);

        float interval = Mathf.Lerp(speedShake.intervalAtStartSpeed, speedShake.intervalAtFullSpeed, shake01);
        _nextSpeedImpulseTime = Time.time + Mathf.Max(0.01f, interval);
    }

    private float ResolveSpeedNormalized()
    {
        if (_shipFlight == null)
        {
            return 0f;
        }

        Rigidbody rb = _shipFlight.Rigidbody;
        float maxSpeed = _shipFlight.FlightConfig.maxSpeed;
        if (rb == null || maxSpeed <= 0f)
        {
            return _shipFlight.ForwardSpeedNormalized;
        }

        return Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
    }

    private bool ShouldPlayLocalShake()
    {
        if (!NetTickUtil.IsActive)
        {
            return true;
        }

        if (_netMovement != null && _netMovement.IsSpawned)
        {
            return _netMovement.IsOwner;
        }

        return _netCombat != null && _netCombat.IsSpawned && _netCombat.IsOwner;
    }

    private void GenerateImpulse(float strength)
    {
        if (strength <= 0f || !ResolveImpulseSource())
        {
            return;
        }

        impulseSource.GenerateImpulse(strength);
    }

    private bool ResolveImpulseSource()
    {
        if (impulseSource != null)
        {
            return true;
        }

        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null && addMissingImpulseSourceAtRuntime && Application.isPlaying)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        if (impulseSource == null && !_warnedMissingImpulseSource && Application.isPlaying)
        {
            Debug.LogWarning("[PlayerScreenShake3D] Screen shake needs a CinemachineImpulseSource on the player prefab.", this);
            _warnedMissingImpulseSource = true;
        }

        return impulseSource != null;
    }

    private void ValidateConfig()
    {
        speedShake.startSpeedNormalized = Mathf.Clamp01(speedShake.startSpeedNormalized);
        speedShake.fullShakeSpeedNormalized = Mathf.Clamp01(Mathf.Max(speedShake.startSpeedNormalized + 0.01f, speedShake.fullShakeSpeedNormalized));
        speedShake.minImpulseStrength = Mathf.Max(0f, speedShake.minImpulseStrength);
        speedShake.maxImpulseStrength = Mathf.Max(speedShake.minImpulseStrength, speedShake.maxImpulseStrength);
        speedShake.intervalAtStartSpeed = Mathf.Max(0.01f, speedShake.intervalAtStartSpeed);
        speedShake.intervalAtFullSpeed = Mathf.Max(0.01f, speedShake.intervalAtFullSpeed);

        hitShake.minDamage = Mathf.Max(0f, hitShake.minDamage);
        hitShake.damageForMaxShake = Mathf.Max(hitShake.minDamage + 0.01f, hitShake.damageForMaxShake);
        hitShake.minImpulseStrength = Mathf.Max(0f, hitShake.minImpulseStrength);
        hitShake.maxImpulseStrength = Mathf.Max(hitShake.minImpulseStrength, hitShake.maxImpulseStrength);
        hitShake.hullDamageMultiplier = Mathf.Max(0f, hitShake.hullDamageMultiplier);
        hitShake.beamDamageMultiplier = Mathf.Max(0f, hitShake.beamDamageMultiplier);
        hitShake.directDamageMultiplier = Mathf.Max(0f, hitShake.directDamageMultiplier);
        hitShake.minImpulseInterval = Mathf.Max(0f, hitShake.minImpulseInterval);
    }
}
