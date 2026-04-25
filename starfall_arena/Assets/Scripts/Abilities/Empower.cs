using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Empower : Ability
{
    [System.Serializable]
    public struct EmpowerConfig
    {
        [Tooltip("How long Empower stays active (seconds).")]
        public float duration;
        [Tooltip("Optional sound played when Empower activates.")]
        public SoundEffect activateSound;
        [Tooltip("Optional sound played when Empower ends.")]
        public SoundEffect deactivateSound;
    }

    [System.Serializable]
    public struct EmpowerEmissionConfig
    {
        [Tooltip("Renderers on modular ship parts (wings, guns, hull, etc.) that should glow while Empower is active.")]
        public List<Renderer> parts;
        [Tooltip("Emission color held while Empower is active (typically white for a hot glow).")]
        public Color glowColor;
        [Tooltip("Emission intensity multiplier applied to the glow color.")]
        public float intensity;
        [Tooltip("Seconds to ramp the emission up when Empower activates.")]
        public float fadeInDuration;
        [Tooltip("Seconds to ramp the emission back down when Empower ends.")]
        public float fadeOutDuration;
    }

    [Header("Empower")]
    public EmpowerConfig empower;

    [Header("Empower Emission Pulse")]
    public EmpowerEmissionConfig emission = new EmpowerEmissionConfig
    {
        glowColor = Color.white,
        intensity = 3f,
        fadeInDuration = 0.25f,
        fadeOutDuration = 0.35f,
    };

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const string EmissionKeyword = "_EMISSION";

    private bool _isEmpoweredActive;
    private Coroutine _empowerRoutine;
    private MaterialPropertyBlock _propertyBlock;
    private readonly List<Color> _originalEmissionColors = new List<Color>();
    private NetMovement _netMovement;

    public bool IsEmpoweredActive => _isEmpoweredActive;

    public override bool BlocksPrimaryFire => false;

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
    }

    private bool HasNetworkPath()
    {
        return NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned;
    }

    private bool HasAuthority()
    {
        return !NetTickUtil.IsActive || (_netMovement != null && _netMovement.IsSpawned && _netMovement.IsServer);
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed) return false;
        if (isLocked || isDisabledByOtherAbility) return false;
        if (IsOnCooldown()) return false;

        if (HasNetworkPath())
        {
            if (_netMovement == null || !_netMovement.IsOwner)
            {
                return false;
            }

            StartEmpowerRoutineLocal();
            _netMovement.RequestEmpowerState(true);
            return true;
        }

        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        StartEmpowerRoutineLocal();
    }

    public void ApplyNetworkEmpowerState(bool active, bool authoritative)
    {
        if (active)
        {
            if (_isEmpoweredActive)
            {
                return;
            }
            StartEmpowerRoutineLocal();
        }
        else
        {
            StopEmpowerRoutineLocal();
        }
    }

    private void StartEmpowerRoutineLocal()
    {
        if (_empowerRoutine != null)
        {
            StopCoroutine(_empowerRoutine);
        }

        _empowerRoutine = StartCoroutine(EmpowerRoutine());
    }

    private void StopEmpowerRoutineLocal()
    {
        if (_empowerRoutine != null)
        {
            StopCoroutine(_empowerRoutine);
            _empowerRoutine = null;
        }

        if (_isEmpoweredActive)
        {
            _isEmpoweredActive = false;
            RestoreEmission();
            if (empower.deactivateSound != null)
            {
                empower.deactivateSound.Play(player.GetAvailableAudioSource());
            }
        }
    }

    public override bool IsAbilityActive()
    {
        return _isEmpoweredActive;
    }

    public override void Die()
    {
        bool wasActive = _isEmpoweredActive;
        StopEmpowerRoutineLocal();

        if (wasActive && HasNetworkPath() && HasAuthority())
        {
            _netMovement.BroadcastEmpowerState(false);
        }

        base.Die();
    }

    private System.Collections.IEnumerator EmpowerRoutine()
    {
        _isEmpoweredActive = true;

        if (empower.activateSound != null)
        {
            empower.activateSound.Play(player.GetAvailableAudioSource());
        }

        CacheOriginalEmission();

        Color targetEmission = emission.glowColor * emission.intensity;
        float fadeIn = Mathf.Max(0f, emission.fadeInDuration);
        float fadeOut = Mathf.Max(0f, emission.fadeOutDuration);
        float duration = Mathf.Max(0f, empower.duration > 0f ? empower.duration : stats.duration);
        float hold = Mathf.Max(0f, duration - fadeIn - fadeOut);

        yield return FadeEmission(targetEmission, fadeIn, true);

        if (hold > 0f)
        {
            ApplyEmission(targetEmission);
            yield return new WaitForSeconds(hold);
        }

        yield return FadeEmission(targetEmission, fadeOut, false);

        RestoreEmission();

        _isEmpoweredActive = false;

        if (empower.deactivateSound != null)
        {
            empower.deactivateSound.Play(player.GetAvailableAudioSource());
        }

        _empowerRoutine = null;

        if (HasNetworkPath() && HasAuthority())
        {
            _netMovement.BroadcastEmpowerState(false);
        }
    }

    private void CacheOriginalEmission()
    {
        _originalEmissionColors.Clear();
        if (emission.parts == null)
        {
            return;
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < emission.parts.Count; i++)
        {
            Renderer r = emission.parts[i];
            if (r == null)
            {
                _originalEmissionColors.Add(Color.black);
                continue;
            }

            Color original = r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionColorId)
                ? r.sharedMaterial.GetColor(EmissionColorId)
                : Color.black;
            _originalEmissionColors.Add(original);

            foreach (Material m in r.materials)
            {
                if (m != null)
                {
                    m.EnableKeyword(EmissionKeyword);
                }
            }
        }
    }

    private System.Collections.IEnumerator FadeEmission(Color target, float duration, bool towardTarget)
    {
        if (duration <= 0f)
        {
            ApplyPerPartBlend(target, towardTarget ? 1f : 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float blend = towardTarget ? t : 1f - t;
            ApplyPerPartBlend(target, blend);
            elapsed += Time.deltaTime;
            yield return null;
        }
        ApplyPerPartBlend(target, towardTarget ? 1f : 0f);
    }

    private void ApplyPerPartBlend(Color target, float blend)
    {
        if (emission.parts == null || _propertyBlock == null || _originalEmissionColors.Count == 0)
        {
            return;
        }

        int count = Mathf.Min(emission.parts.Count, _originalEmissionColors.Count);
        for (int i = 0; i < count; i++)
        {
            Renderer r = emission.parts[i];
            if (r == null)
            {
                continue;
            }
            Color c = Color.Lerp(_originalEmissionColors[i], target, blend);
            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    private void ApplyEmission(Color color)
    {
        if (emission.parts == null || _propertyBlock == null)
        {
            return;
        }

        for (int i = 0; i < emission.parts.Count; i++)
        {
            Renderer r = emission.parts[i];
            if (r == null)
            {
                continue;
            }
            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, color);
            r.SetPropertyBlock(_propertyBlock);
        }
    }

    private void RestoreEmission()
    {
        if (emission.parts == null || _propertyBlock == null || _originalEmissionColors.Count == 0)
        {
            return;
        }

        int count = Mathf.Min(emission.parts.Count, _originalEmissionColors.Count);
        for (int i = 0; i < count; i++)
        {
            Renderer r = emission.parts[i];
            if (r == null)
            {
                continue;
            }
            r.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, _originalEmissionColors[i]);
            r.SetPropertyBlock(_propertyBlock);
        }

        _originalEmissionColors.Clear();
    }
}
