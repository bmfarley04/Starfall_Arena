using System.Collections.Generic;
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

    [System.Serializable]
    private struct HudShakeConfig3D
    {
        [Tooltip("If enabled, active screen-space HUD canvases are offset briefly when player screen shake fires. This helps the feedback read as camera shake instead of only ship/world movement.")]
        public bool enabled;
        [Tooltip("Optional explicit HUD root to shake. Leave unset to auto-resolve active screen-space root canvases using the UI camera.")]
        public RectTransform hudRootOverride;
        [Tooltip("Only auto-shake canvases rendered by a camera with this name. Leave empty to allow any screen-space camera canvas.")]
        public string uiCameraNameFilter;
        [Tooltip("Maximum HUD offset in pixels for a strength-1 impulse.")]
        public float pixelsPerImpulseStrength;
        [Tooltip("Maximum HUD Z rotation in degrees for a strength-1 impulse.")]
        public float rotationDegreesPerImpulseStrength;
        [Tooltip("Seconds a HUD shake impulse remains active before fully fading out.")]
        public float duration;
        [Tooltip("Noise frequency used for HUD shake. Higher values feel sharper.")]
        public float frequency;
        [Tooltip("Multiplier applied to recurring speed-shake impulses so fast flight stays subtle on the HUD.")]
        public float speedImpulseMultiplier;
        [Tooltip("Maximum HUD offset in pixels after strength scaling.")]
        public float maxPixelOffset;
        [Tooltip("Maximum HUD Z rotation in degrees after strength scaling.")]
        public float maxRotationDegrees;
    }

    [Header("Impulse Source")]
    [Tooltip("Cinemachine impulse source used for all player camera shake. If unset, this component uses the source on the same GameObject.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("If enabled, a missing CinemachineImpulseSource is added at runtime so the player feedback does not silently fail.")]
    [SerializeField] private bool addMissingImpulseSourceAtRuntime = true;
    [Tooltip("If enabled, impulses choose a random direction across the camera plane instead of repeatedly pushing along the ship's local axis.")]
    [SerializeField] private bool randomizeImpulseDirectionOnCameraPlane = true;
    [Tooltip("Camera-plane horizontal weight for randomized impulse directions.")]
    [SerializeField] private float cameraPlaneHorizontalWeight = 1f;
    [Tooltip("Camera-plane vertical weight for randomized impulse directions. Keep below Horizontal Weight to avoid a visible up/down bob.")]
    [SerializeField] private float cameraPlaneVerticalWeight = 0.55f;
    [Tooltip("Local-space fallback impulse direction used when no camera is available or randomized direction is disabled. This avoids relying on the prefab's CinemachineImpulseSource Default Velocity.")]
    [SerializeField] private Vector3 localImpulseDirection = new Vector3(1f, 0.35f, 0f);

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

    [Header("HUD Shake")]
    [SerializeField] private HudShakeConfig3D hudShake = new HudShakeConfig3D
    {
        enabled = true,
        hudRootOverride = null,
        uiCameraNameFilter = "UICamera",
        pixelsPerImpulseStrength = 14f,
        rotationDegreesPerImpulseStrength = 0.65f,
        duration = 0.16f,
        frequency = 38f,
        speedImpulseMultiplier = 0.35f,
        maxPixelOffset = 20f,
        maxRotationDegrees = 1.1f
    };

    private ShipFlight3D _shipFlight;
    private NetMovement3D _netMovement;
    private NetCombat3D _netCombat;
    private readonly List<RectTransform> _hudShakeRoots = new List<RectTransform>(4);
    private readonly List<Vector2> _hudBaseAnchoredPositions = new List<Vector2>(4);
    private readonly List<Quaternion> _hudBaseLocalRotations = new List<Quaternion>(4);
    private float _nextSpeedImpulseTime;
    private float _nextHitImpulseTime;
    private float _hudShakeRemaining;
    private float _hudShakeDuration;
    private float _hudShakeStrength;
    private Vector2 _hudShakeNoiseSeed;
    private bool _hudShakeRootsResolved;
    private bool _hudShakeApplied;
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

    private void LateUpdate()
    {
        UpdateHudShake(Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        ResetHudShake();
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

        GenerateImpulse(strength, false);
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
        GenerateImpulse(strength, true);

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

    private void GenerateImpulse(float strength, bool isSpeedShake)
    {
        if (strength <= 0f || !ResolveImpulseSource())
        {
            return;
        }

        Vector3 impulseDirection = ResolveImpulseDirection();
        impulseSource.GenerateImpulseWithVelocity(impulseDirection * strength);
        TriggerHudShake(strength, isSpeedShake);
    }

    private Vector3 ResolveImpulseDirection()
    {
        if (randomizeImpulseDirectionOnCameraPlane)
        {
            Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform != null)
            {
                Vector2 randomDirection = Random.insideUnitCircle;
                if (randomDirection.sqrMagnitude <= 0.0001f)
                {
                    randomDirection = Vector2.right;
                }

                randomDirection.Normalize();
                Vector3 cameraPlaneDirection =
                    cameraTransform.right * (randomDirection.x * cameraPlaneHorizontalWeight) +
                    cameraTransform.up * (randomDirection.y * cameraPlaneVerticalWeight);

                if (cameraPlaneDirection.sqrMagnitude > 0.0001f)
                {
                    return cameraPlaneDirection.normalized;
                }
            }
        }

        return localImpulseDirection.sqrMagnitude > 0.0001f
            ? transform.TransformDirection(localImpulseDirection.normalized)
            : transform.right;
    }

    private void TriggerHudShake(float strength, bool isSpeedShake)
    {
        if (!hudShake.enabled || strength <= 0f)
        {
            return;
        }

        if (!ResolveHudShakeRoots())
        {
            return;
        }

        float multiplier = isSpeedShake ? hudShake.speedImpulseMultiplier : 1f;
        float scaledStrength = strength * Mathf.Max(0f, multiplier);
        if (scaledStrength <= 0f)
        {
            return;
        }

        _hudShakeStrength = Mathf.Max(_hudShakeStrength, scaledStrength);
        _hudShakeDuration = Mathf.Max(0.01f, hudShake.duration);
        _hudShakeRemaining = Mathf.Max(_hudShakeRemaining, _hudShakeDuration);
        _hudShakeNoiseSeed = Random.insideUnitCircle * 100f;
    }

    private void UpdateHudShake(float deltaTime)
    {
        if (!hudShake.enabled)
        {
            ResetHudShake();
            return;
        }

        if (_hudShakeRemaining <= 0f)
        {
            ResetHudShake();
            return;
        }

        if (!ResolveHudShakeRoots())
        {
            _hudShakeRemaining = 0f;
            return;
        }

        _hudShakeRemaining = Mathf.Max(0f, _hudShakeRemaining - Mathf.Max(0f, deltaTime));
        float fade = Mathf.Clamp01(_hudShakeRemaining / Mathf.Max(0.01f, _hudShakeDuration));
        float amplitude = _hudShakeStrength * fade * fade;
        float time = Time.unscaledTime * Mathf.Max(0.01f, hudShake.frequency);
        float noiseX = (Mathf.PerlinNoise(_hudShakeNoiseSeed.x, time) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(_hudShakeNoiseSeed.y + 37.1f, time + 13.7f) - 0.5f) * 2f;
        float noiseRoll = (Mathf.PerlinNoise(_hudShakeNoiseSeed.x + 71.3f, time + 29.4f) - 0.5f) * 2f;
        float pixelOffset = Mathf.Min(hudShake.maxPixelOffset, hudShake.pixelsPerImpulseStrength * amplitude);
        float rollDegrees = Mathf.Min(hudShake.maxRotationDegrees, hudShake.rotationDegreesPerImpulseStrength * amplitude) * noiseRoll;

        for (int i = _hudShakeRoots.Count - 1; i >= 0; i--)
        {
            RectTransform root = _hudShakeRoots[i];
            if (root == null)
            {
                _hudShakeRoots.RemoveAt(i);
                _hudBaseAnchoredPositions.RemoveAt(i);
                _hudBaseLocalRotations.RemoveAt(i);
                continue;
            }

            root.anchoredPosition = _hudBaseAnchoredPositions[i] + new Vector2(noiseX, noiseY) * pixelOffset;
            root.localRotation = _hudBaseLocalRotations[i] * Quaternion.Euler(0f, 0f, rollDegrees);
        }

        _hudShakeApplied = true;

        if (_hudShakeRemaining <= 0f)
        {
            ResetHudShake();
        }
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

    private bool ResolveHudShakeRoots()
    {
        if (_hudShakeRootsResolved && _hudShakeRoots.Count > 0)
        {
            return true;
        }

        ResetHudShakeRootCache();

        if (hudShake.hudRootOverride != null)
        {
            AddHudShakeRoot(hudShake.hudRootOverride);
        }
        else
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                if (!CanvasMatchesUiCameraFilter(canvas))
                {
                    continue;
                }

                AddHudShakeRoot(canvas.transform as RectTransform);
            }
        }

        _hudShakeRootsResolved = true;
        return _hudShakeRoots.Count > 0;
    }

    private bool CanvasMatchesUiCameraFilter(Canvas canvas)
    {
        if (string.IsNullOrWhiteSpace(hudShake.uiCameraNameFilter))
        {
            return true;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return true;
        }

        Camera canvasCamera = canvas.worldCamera;
        return canvasCamera != null && canvasCamera.name == hudShake.uiCameraNameFilter;
    }

    private void AddHudShakeRoot(RectTransform root)
    {
        if (root == null || _hudShakeRoots.Contains(root))
        {
            return;
        }

        _hudShakeRoots.Add(root);
        _hudBaseAnchoredPositions.Add(root.anchoredPosition);
        _hudBaseLocalRotations.Add(root.localRotation);
    }

    private void ResetHudShake()
    {
        if (_hudShakeApplied)
        {
            for (int i = _hudShakeRoots.Count - 1; i >= 0; i--)
            {
                RectTransform root = _hudShakeRoots[i];
                if (root == null)
                {
                    continue;
                }

                root.anchoredPosition = _hudBaseAnchoredPositions[i];
                root.localRotation = _hudBaseLocalRotations[i];
            }
        }

        _hudShakeRemaining = 0f;
        _hudShakeStrength = 0f;
        _hudShakeApplied = false;
    }

    private void ResetHudShakeRootCache()
    {
        ResetHudShake();
        _hudShakeRoots.Clear();
        _hudBaseAnchoredPositions.Clear();
        _hudBaseLocalRotations.Clear();
        _hudShakeRootsResolved = false;
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

        if (localImpulseDirection.sqrMagnitude <= 0.0001f)
        {
            localImpulseDirection = new Vector3(1f, 0.35f, 0f);
        }

        cameraPlaneHorizontalWeight = Mathf.Max(0f, cameraPlaneHorizontalWeight);
        cameraPlaneVerticalWeight = Mathf.Max(0f, cameraPlaneVerticalWeight);
        if (cameraPlaneHorizontalWeight <= 0.0001f && cameraPlaneVerticalWeight <= 0.0001f)
        {
            cameraPlaneHorizontalWeight = 1f;
            cameraPlaneVerticalWeight = 0.55f;
        }

        hudShake.pixelsPerImpulseStrength = Mathf.Max(0f, hudShake.pixelsPerImpulseStrength);
        hudShake.rotationDegreesPerImpulseStrength = Mathf.Max(0f, hudShake.rotationDegreesPerImpulseStrength);
        hudShake.duration = Mathf.Max(0.01f, hudShake.duration);
        hudShake.frequency = Mathf.Max(0.01f, hudShake.frequency);
        hudShake.speedImpulseMultiplier = Mathf.Max(0f, hudShake.speedImpulseMultiplier);
        hudShake.maxPixelOffset = Mathf.Max(0f, hudShake.maxPixelOffset);
        hudShake.maxRotationDegrees = Mathf.Max(0f, hudShake.maxRotationDegrees);
    }
}
