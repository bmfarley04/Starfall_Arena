using System.Collections;
using FORGE3D;
using UnityEngine;

#pragma warning disable CS0649

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class SpawnArrivalEffect3D : MonoBehaviour
{
    [System.Serializable]
    private struct ArrivalEffectConfig3D
    {
        [Tooltip("One-shot visual prefab spawned when this object enters the scene.")]
        public GameObject effectPrefab;
        [Tooltip("Optional spawn anchor for the effect. Defaults to this object's transform.")]
        public Transform effectAnchor;
        [Tooltip("Local position offset from the effect anchor.")]
        public Vector3 localPositionOffset;
        [Tooltip("Local rotation offset from the effect anchor, in degrees.")]
        public Vector3 localEulerOffset;
        [Tooltip("Uniform scale applied to the effect before ship-scale matching.")]
        public float effectScaleMultiplier;
        [Tooltip("When enabled, the effect scale also multiplies by the largest axis of this object's current world scale.")]
        public bool multiplyByShipScale;
        [Tooltip("When enabled, this component adds TimedEffectCleanup3D to spawned effects that do not already have one.")]
        public bool autoCleanupEffect;
        [Tooltip("When enabled, child particle systems use hierarchy scaling so the effect multiplier also scales particle size.")]
        public bool forceParticleHierarchyScaling;
    }

    [System.Serializable]
    private struct ArrivalRevealConfig3D
    {
        [Tooltip("Delay before this enemy is revealed after the arrival effect starts.")]
        public float revealDelaySeconds;
        [Tooltip("When enabled, renderers are hidden until the reveal delay finishes.")]
        public bool hideVisualsUntilReveal;
        [Tooltip("Renderers hidden until reveal. Leave empty to auto-use renderers under this object.")]
        public Renderer[] visualRenderers;
        [Tooltip("When enabled, assigned colliders are disabled until the reveal delay finishes.")]
        public bool disableAssignedCollidersUntilReveal;
        [Tooltip("Colliders disabled until reveal. Assign gameplay/body colliders that should not be hittable before arrival.")]
        public Collider[] colliders;
        [Tooltip("Behaviours disabled until reveal. Assign active gameplay behaviours such as enemy brains, movement, and weapons. These behaviours are re-enabled when reveal completes.")]
        public Behaviour[] behaviours;
    }

    [Header("Arrival Effect")]
    [SerializeField] private ArrivalEffectConfig3D arrivalEffect = new ArrivalEffectConfig3D
    {
        effectScaleMultiplier = 1f,
        multiplyByShipScale = true,
        autoCleanupEffect = true,
        forceParticleHierarchyScaling = true
    };

    [Header("Reveal")]
    [SerializeField] private ArrivalRevealConfig3D reveal = new ArrivalRevealConfig3D
    {
        revealDelaySeconds = 3f,
        hideVisualsUntilReveal = true
    };

    private Renderer[] _resolvedRenderers;
    private bool[] _rendererEnabledStates;
    private bool[] _colliderEnabledStates;
    private bool[] _behaviourEnabledStates;
    private Coroutine _revealRoutine;
    private bool _hasPlayed;
    private bool _isHidden;
    private bool _hasRevealed;

    public event System.Action<SpawnArrivalEffect3D> Revealed;

    public bool HasRevealed => _hasRevealed;

    private void Awake()
    {
        CacheInitialStates();
    }

    private void OnEnable()
    {
        PlayOnceForSpawn();
    }

    private void OnDisable()
    {
        if (_revealRoutine != null)
        {
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }

        RestoreRevealTargets();
    }

    private void PlayOnceForSpawn()
    {
        if (_hasPlayed)
        {
            return;
        }

        _hasPlayed = true;
        ApplyHiddenState();
        SpawnEffect();

        float revealDelay = Mathf.Max(0f, reveal.revealDelaySeconds);
        if (revealDelay > 0f)
        {
            _revealRoutine = StartCoroutine(RevealAfterDelay(revealDelay));
        }
        else
        {
            CompleteReveal();
        }
    }

    private IEnumerator RevealAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _revealRoutine = null;
        CompleteReveal();
    }

    private void CompleteReveal()
    {
        RestoreRevealTargets();

        if (_hasRevealed)
        {
            return;
        }

        _hasRevealed = true;
        Revealed?.Invoke(this);
    }

    private void SpawnEffect()
    {
        if (arrivalEffect.effectPrefab == null)
        {
            return;
        }

        EnsureForgeTimerExists();

        Transform anchor = arrivalEffect.effectAnchor != null ? arrivalEffect.effectAnchor : transform;
        Vector3 spawnPosition = anchor.TransformPoint(arrivalEffect.localPositionOffset);
        Quaternion spawnRotation = anchor.rotation * Quaternion.Euler(arrivalEffect.localEulerOffset);
        GameObject spawnedEffect = GameObjectPool3D.Spawn(arrivalEffect.effectPrefab, spawnPosition, spawnRotation);
        if (spawnedEffect == null)
        {
            return;
        }

        float scale = Mathf.Max(0.01f, arrivalEffect.effectScaleMultiplier);
        if (arrivalEffect.multiplyByShipScale)
        {
            Vector3 shipScale = transform.lossyScale;
            scale *= Mathf.Max(shipScale.x, Mathf.Max(shipScale.y, shipScale.z));
        }

        spawnedEffect.transform.localScale = Vector3.Scale(spawnedEffect.transform.localScale, Vector3.one * scale);
        ConfigureForgeWarpJump(spawnedEffect);
        ConfigureParticleScaling(spawnedEffect);
        EnsureCleanup(spawnedEffect);
    }

    private void ConfigureForgeWarpJump(GameObject spawnedEffect)
    {
        F3DWarpJump[] warpJumps = spawnedEffect.GetComponentsInChildren<F3DWarpJump>(true);
        for (int i = 0; i < warpJumps.Length; i++)
        {
            F3DWarpJump warpJump = warpJumps[i];
            if (warpJump == null)
            {
                continue;
            }

            warpJump.DebugLoop = false;
            warpJump.SendOnSpawned = false;
        }
    }

    private void ConfigureParticleScaling(GameObject spawnedEffect)
    {
        if (!arrivalEffect.forceParticleHierarchyScaling)
        {
            return;
        }

        ParticleSystem[] particleSystems = spawnedEffect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
    }

    private void EnsureCleanup(GameObject spawnedEffect)
    {
        if (!arrivalEffect.autoCleanupEffect)
        {
            return;
        }

        TimedEffectCleanup3D cleanup = spawnedEffect.GetComponent<TimedEffectCleanup3D>();
        if (cleanup == null)
        {
            cleanup = spawnedEffect.AddComponent<TimedEffectCleanup3D>();
        }

        cleanup.BeginCleanup();
    }

    private void ApplyHiddenState()
    {
        if (_isHidden)
        {
            return;
        }

        _isHidden = true;

        if (reveal.hideVisualsUntilReveal)
        {
            for (int i = 0; i < _resolvedRenderers.Length; i++)
            {
                if (_resolvedRenderers[i] != null)
                {
                    _resolvedRenderers[i].enabled = false;
                }
            }
        }

        if (reveal.disableAssignedCollidersUntilReveal && reveal.colliders != null)
        {
            for (int i = 0; i < reveal.colliders.Length; i++)
            {
                if (reveal.colliders[i] != null)
                {
                    reveal.colliders[i].enabled = false;
                }
            }
        }

        if (reveal.behaviours != null)
        {
            for (int i = 0; i < reveal.behaviours.Length; i++)
            {
                Behaviour behaviour = reveal.behaviours[i];
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = false;
                }
            }
        }
    }

    private void RestoreRevealTargets()
    {
        if (!_isHidden)
        {
            return;
        }

        _isHidden = false;
        RestoreRenderers();
        RestoreColliders();
        RestoreBehaviours();
    }

    private void RestoreRenderers()
    {
        if (_resolvedRenderers == null || _rendererEnabledStates == null)
        {
            return;
        }

        for (int i = 0; i < _resolvedRenderers.Length && i < _rendererEnabledStates.Length; i++)
        {
            if (_resolvedRenderers[i] != null)
            {
                _resolvedRenderers[i].enabled = _rendererEnabledStates[i];
            }
        }
    }

    private void RestoreColliders()
    {
        if (reveal.colliders == null || _colliderEnabledStates == null)
        {
            return;
        }

        for (int i = 0; i < reveal.colliders.Length && i < _colliderEnabledStates.Length; i++)
        {
            if (reveal.colliders[i] != null)
            {
                reveal.colliders[i].enabled = _colliderEnabledStates[i];
            }
        }
    }

    private void RestoreBehaviours()
    {
        if (reveal.behaviours == null || _behaviourEnabledStates == null)
        {
            return;
        }

        for (int i = 0; i < reveal.behaviours.Length && i < _behaviourEnabledStates.Length; i++)
        {
            Behaviour behaviour = reveal.behaviours[i];
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = true;
            }
        }
    }

    private void CacheInitialStates()
    {
        _resolvedRenderers = ResolveRenderers();
        _rendererEnabledStates = new bool[_resolvedRenderers.Length];
        for (int i = 0; i < _resolvedRenderers.Length; i++)
        {
            _rendererEnabledStates[i] = _resolvedRenderers[i] != null && _resolvedRenderers[i].enabled;
        }

        int colliderCount = reveal.colliders != null ? reveal.colliders.Length : 0;
        _colliderEnabledStates = new bool[colliderCount];
        for (int i = 0; i < colliderCount; i++)
        {
            _colliderEnabledStates[i] = reveal.colliders[i] != null && reveal.colliders[i].enabled;
        }

        int behaviourCount = reveal.behaviours != null ? reveal.behaviours.Length : 0;
        _behaviourEnabledStates = new bool[behaviourCount];
        for (int i = 0; i < behaviourCount; i++)
        {
            Behaviour behaviour = reveal.behaviours[i];
            _behaviourEnabledStates[i] = behaviour != null && behaviour.enabled;
        }
    }

    private Renderer[] ResolveRenderers()
    {
        if (reveal.visualRenderers != null && reveal.visualRenderers.Length > 0)
        {
            return reveal.visualRenderers;
        }

        return reveal.hideVisualsUntilReveal
            ? GetComponentsInChildren<Renderer>(true)
            : System.Array.Empty<Renderer>();
    }

    private static void EnsureForgeTimerExists()
    {
        if (F3DTime.time != null)
        {
            return;
        }

        GameObject timerObject = new GameObject("F3DTime_Runtime");
        timerObject.hideFlags = HideFlags.HideAndDontSave;
        timerObject.AddComponent<F3DTime>();
    }
}

#pragma warning restore CS0649
