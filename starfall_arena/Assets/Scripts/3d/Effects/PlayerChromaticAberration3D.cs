using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player3D))]
public class PlayerChromaticAberration3D : MonoBehaviour
{
    [System.Serializable]
    private struct ChromaticDamageConfig3D
    {
        [Tooltip("Master toggle for the local chromatic-aberration hit response.")]
        public bool enabled;
        [Tooltip("Optional explicit volume reference. Recommended when the scene has multiple volumes.")]
        public Volume volume;
        [Tooltip("If no explicit volume is assigned, search the scene once for the first volume profile that contains Chromatic Aberration.")]
        public bool autoFindVolume;
        [Tooltip("Upper cap for extra chromatic intensity added by this controller.")]
        public float maxAddedIntensity;
        [Tooltip("How much extra chromatic intensity to add per point of real damage taken.")]
        public float intensityPerDamage;
        [Tooltip("Extra multiplier applied to non-beam hits so projectile impacts can punch harder than beam ticks.")]
        public float projectileMultiplier;
        [Tooltip("Beam hits inside this window are treated as one continuous stream and accumulate intensity together.")]
        public float beamAccumulationWindow;
        [Tooltip("How quickly the added intensity decays back toward zero after incoming hits stop.")]
        public float fadeSpeed;
    }

    [Header("Chromatic Aberration")]
    [SerializeField] private Player3D player;
    [SerializeField] private ChromaticDamageConfig3D chromaticConfig = new ChromaticDamageConfig3D
    {
        enabled = true,
        autoFindVolume = true,
        maxAddedIntensity = 0.7f,
        intensityPerDamage = 0.03f,
        projectileMultiplier = 1.35f,
        beamAccumulationWindow = 0.2f,
        fadeSpeed = 1.5f
    };

    private ChromaticAberration _chromaticAberration;
    private Coroutine _fadeCoroutine;
    private float _baselineIntensity;
    private bool _baselineOverrideState;
    private float _currentAddedIntensity;
    private float _beamAccumulator;
    private float _lastDamageTime = float.NegativeInfinity;
    private bool _warnedMissingVolume;
    private bool _warnedMissingOverride;

    private void Awake()
    {
        player ??= GetComponent<Player3D>();
        TryResolveChromaticAberration();
        ResetEffectState();
    }

    private void OnEnable()
    {
        TryResolveChromaticAberration();
        ResetEffectState();
    }

    private void OnDisable()
    {
        StopFadeCoroutine();
        RestoreBaseline();
        _currentAddedIntensity = 0f;
        _beamAccumulator = 0f;
    }

    public void TriggerDamageFeedback(float appliedDamage, DamageSource3D source, float intensityMultiplier = 1f)
    {
        if (!chromaticConfig.enabled || appliedDamage <= 0f || intensityMultiplier <= 0f)
        {
            return;
        }

        if (!TryResolveChromaticAberration())
        {
            return;
        }

        float scaledDamage = Mathf.Max(0f, appliedDamage) * Mathf.Max(0f, intensityMultiplier);
        bool isBeam = source == DamageSource3D.Beam;
        float sourceMultiplier = isBeam ? 1f : Mathf.Max(0f, chromaticConfig.projectileMultiplier);
        float addedIntensity = scaledDamage * Mathf.Max(0f, chromaticConfig.intensityPerDamage) * sourceMultiplier;

        if (isBeam)
        {
            if (Time.time - _lastDamageTime <= Mathf.Max(0f, chromaticConfig.beamAccumulationWindow))
            {
                _beamAccumulator += addedIntensity;
            }
            else
            {
                _beamAccumulator = addedIntensity;
            }

            _currentAddedIntensity = Mathf.Max(
                _currentAddedIntensity,
                Mathf.Min(_beamAccumulator, Mathf.Max(0f, chromaticConfig.maxAddedIntensity)));
        }
        else
        {
            _beamAccumulator = 0f;
            _currentAddedIntensity = Mathf.Max(
                _currentAddedIntensity,
                Mathf.Min(addedIntensity, Mathf.Max(0f, chromaticConfig.maxAddedIntensity)));
        }

        _lastDamageTime = Time.time;
        ApplyCurrentIntensity();
        RestartFadeCoroutine();
    }

    public void AddChromaticImpulse(float addedIntensity)
    {
        if (!chromaticConfig.enabled || addedIntensity <= 0f)
        {
            return;
        }

        if (!TryResolveChromaticAberration())
        {
            return;
        }

        _beamAccumulator = 0f;
        _lastDamageTime = Time.time;
        _currentAddedIntensity = Mathf.Clamp(
            _currentAddedIntensity + addedIntensity,
            0f,
            Mathf.Max(0f, chromaticConfig.maxAddedIntensity));
        ApplyCurrentIntensity();
        RestartFadeCoroutine();
    }

    public float GetCurrentAddedIntensity()
    {
        return _currentAddedIntensity;
    }

    public void ClearEffect()
    {
        StopFadeCoroutine();
        ResetEffectState();
    }

    private bool TryResolveChromaticAberration()
    {
        if (_chromaticAberration != null)
        {
            return true;
        }

        Volume volume = chromaticConfig.volume;

        if (volume == null && chromaticConfig.autoFindVolume)
        {
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                Volume candidate = volumes[i];
                if (candidate == null)
                {
                    continue;
                }

                VolumeProfile profile = candidate.profile;
                if (profile != null && profile.TryGet(out ChromaticAberration candidateChromatic))
                {
                    chromaticConfig.volume = candidate;
                    volume = candidate;
                    _chromaticAberration = candidateChromatic;
                    CacheBaseline();
                    return true;
                }
            }
        }

        if (volume == null)
        {
            WarnMissingVolume("PlayerChromaticAberration3D could not find a Volume. Assign the local gameplay volume explicitly or enable auto-find for scenes that keep a single chromatic-capable volume.");
            return false;
        }

        VolumeProfile resolvedProfile = volume.profile;
        if (resolvedProfile == null || !resolvedProfile.TryGet(out _chromaticAberration))
        {
            WarnMissingVolume("PlayerChromaticAberration3D found a Volume but its runtime profile does not contain a Chromatic Aberration override.");
            return false;
        }

        CacheBaseline();
        return true;
    }

    private void CacheBaseline()
    {
        if (_chromaticAberration == null)
        {
            return;
        }

        _baselineIntensity = _chromaticAberration.intensity.value;
        _baselineOverrideState = _chromaticAberration.intensity.overrideState;
    }

    private void ResetEffectState()
    {
        _currentAddedIntensity = 0f;
        _beamAccumulator = 0f;
        ApplyCurrentIntensity();
    }

    private void ApplyCurrentIntensity()
    {
        if (_chromaticAberration == null)
        {
            return;
        }

        if (!_chromaticAberration.intensity.overrideState && !_warnedMissingOverride)
        {
            _warnedMissingOverride = true;
            Debug.LogWarning(
                "PlayerChromaticAberration3D enabled Chromatic Aberration intensity override at runtime because the assigned Volume override was not marked active. Keep that override enabled in the profile to make the setup explicit.",
                this);
        }

        _chromaticAberration.intensity.overrideState = true;
        _chromaticAberration.intensity.value = Mathf.Clamp01(_baselineIntensity + _currentAddedIntensity);
    }

    private void RestoreBaseline()
    {
        if (_chromaticAberration == null)
        {
            return;
        }

        _chromaticAberration.intensity.value = _baselineIntensity;
        _chromaticAberration.intensity.overrideState = _baselineOverrideState;
    }

    private void RestartFadeCoroutine()
    {
        StopFadeCoroutine();
        _fadeCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private void StopFadeCoroutine()
    {
        if (_fadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        while (Time.time - _lastDamageTime < Mathf.Max(0f, chromaticConfig.beamAccumulationWindow))
        {
            yield return null;
        }

        float fadeSpeed = Mathf.Max(0f, chromaticConfig.fadeSpeed);
        if (fadeSpeed <= 0f)
        {
            yield break;
        }

        while (_currentAddedIntensity > 0.0001f)
        {
            _currentAddedIntensity = Mathf.MoveTowards(_currentAddedIntensity, 0f, fadeSpeed * Time.deltaTime);
            _beamAccumulator = Mathf.MoveTowards(_beamAccumulator, 0f, fadeSpeed * Time.deltaTime);
            ApplyCurrentIntensity();
            yield return null;
        }

        _currentAddedIntensity = 0f;
        _beamAccumulator = 0f;
        ApplyCurrentIntensity();
        _fadeCoroutine = null;
    }

    private void WarnMissingVolume(string message)
    {
        if (_warnedMissingVolume)
        {
            return;
        }

        _warnedMissingVolume = true;
        Debug.LogWarning(message, this);
    }
}
