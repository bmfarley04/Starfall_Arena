using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class Empower3D : Ability3D
{
    [System.Serializable]
    public struct EmpowerConfig3D
    {
        public float cooldown;
        public float duration;
        public SoundEffect activateSound;
        public SoundEffect deactivateSound;
    }

    [System.Serializable]
    public struct EmpowerEmissionConfig3D
    {
        public List<Renderer> parts;
        public Color glowColor;
        public float intensity;
        public float fadeInDuration;
        public float fadeOutDuration;
    }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const string EmissionKeyword = "_EMISSION";

    [Header("Class4 Empower")]
    [SerializeField] private EmpowerConfig3D empower = new EmpowerConfig3D
    {
        cooldown = 32f,
        duration = 18f
    };

    [Header("Empower Emission Pulse")]
    [SerializeField] private EmpowerEmissionConfig3D emission = new EmpowerEmissionConfig3D
    {
        glowColor = Color.white,
        intensity = 3f,
        fadeInDuration = 0.25f,
        fadeOutDuration = 0.35f
    };

    private bool _isEmpoweredActive;
    private Coroutine _empowerRoutine;
    private MaterialPropertyBlock _propertyBlock;
    private readonly List<Color> _originalEmissionColors = new List<Color>();
    private NetCombat3D _netCombat;

    public bool IsEmpoweredActive => _isEmpoweredActive;

    protected override void Awake()
    {
        base.Awake();
        _netCombat = GetComponent<NetCombat3D>();
        SetInitialCooldownState(GetCooldownDuration());
    }

    protected override float GetCooldownDuration()
    {
        return empower.cooldown;
    }

    protected override float GetActiveDuration()
    {
        return empower.duration;
    }

    public void ApplyProfile(Class4PlayerBalanceProfile3D.Class4Stats stats)
    {
        empower.cooldown = Mathf.Max(0f, stats.empowerCooldown);
        empower.duration = Mathf.Max(0f, stats.empowerDuration);
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed || isLocked || isDisabledByOtherAbility || IsOnCooldown())
        {
            return false;
        }

        MarkAbilityUsed();

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            if (!_netCombat.IsServer)
            {
                StartEmpowerRoutineLocal();
            }

            _netCombat.RequestEmpowerState(true);
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

    public override bool IsAbilityActive()
    {
        return _isEmpoweredActive;
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

    public override void Die()
    {
        bool wasActive = _isEmpoweredActive;
        StopEmpowerRoutineLocal();

        if (wasActive && NetTickUtil.IsActive && _netCombat != null && _netCombat.IsServer)
        {
            _netCombat.RequestEmpowerState(false);
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

        if (!_isEmpoweredActive)
        {
            return;
        }

        _isEmpoweredActive = false;
        RestoreEmission();
        empower.deactivateSound?.PlayAtPoint(transform.position);
    }

    private IEnumerator EmpowerRoutine()
    {
        _isEmpoweredActive = true;
        empower.activateSound?.PlayAtPoint(transform.position);
        CacheOriginalEmission();

        Color targetEmission = emission.glowColor * emission.intensity;
        float fadeIn = Mathf.Max(0f, emission.fadeInDuration);
        float fadeOut = Mathf.Max(0f, emission.fadeOutDuration);
        float duration = Mathf.Max(0f, empower.duration);
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
        empower.deactivateSound?.PlayAtPoint(transform.position);
        _empowerRoutine = null;

        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsServer)
        {
            _netCombat.RequestEmpowerState(false);
        }
    }

    private void CacheOriginalEmission()
    {
        _originalEmissionColors.Clear();

        if (emission.parts == null || emission.parts.Count == 0)
        {
            emission.parts = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < emission.parts.Count; i++)
        {
            Renderer renderer = emission.parts[i];
            if (renderer == null)
            {
                _originalEmissionColors.Add(Color.black);
                continue;
            }

            Color original = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(EmissionColorId)
                ? renderer.sharedMaterial.GetColor(EmissionColorId)
                : Color.black;
            _originalEmissionColors.Add(original);

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material != null)
                {
                    material.EnableKeyword(EmissionKeyword);
                }
            }
        }
    }

    private IEnumerator FadeEmission(Color target, float duration, bool towardTarget)
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
            Renderer renderer = emission.parts[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = Color.Lerp(_originalEmissionColors[i], target, blend);
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
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
            Renderer renderer = emission.parts[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(_propertyBlock);
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
            Renderer renderer = emission.parts[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(EmissionColorId, _originalEmissionColors[i]);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        _originalEmissionColors.Clear();
    }
}
