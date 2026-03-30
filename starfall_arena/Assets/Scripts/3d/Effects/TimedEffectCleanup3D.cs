using System.Collections;
using UnityEngine;

public enum TimedEffectCleanupAction3D
{
    Auto,
    Destroy,
    Deactivate,
    DespawnToPool
}

[DisallowMultipleComponent]
public class TimedEffectCleanup3D : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("When enabled, the cleanup delay is derived from the child particle systems.")]
    [SerializeField] private bool autoCalculateLifetimeFromParticles = true;
    [Tooltip("Fallback cleanup delay when no valid particle lifetime can be derived.")]
    [SerializeField] private float fallbackLifetime = 2f;
    [Tooltip("Extra time added after the calculated or fallback lifetime.")]
    [SerializeField] private float extraLifetime = 0.1f;

    [Header("Cleanup")]
    [Tooltip("Auto destroys normal effects and despawns pooled effects.")]
    [SerializeField] private TimedEffectCleanupAction3D cleanupAction = TimedEffectCleanupAction3D.Auto;
    [Tooltip("Automatically start the cleanup timer whenever the object is enabled.")]
    [SerializeField] private bool startOnEnable = true;

    private Coroutine _cleanupRoutine;
    private ParticleSystem[] _particleSystems;

    private void OnEnable()
    {
        if (startOnEnable)
        {
            BeginCleanup();
        }
    }

    private void OnDisable()
    {
        StopCleanupRoutine();
    }

    public void BeginCleanup()
    {
        StopCleanupRoutine();
        float cleanupDelay = ResolveCleanupDelay();
        _cleanupRoutine = StartCoroutine(CleanupAfterDelay(cleanupDelay));
    }

    private IEnumerator CleanupAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        CleanupNow();
    }

    private float ResolveCleanupDelay()
    {
        float resolvedLifetime = autoCalculateLifetimeFromParticles
            ? ResolveParticleLifetime()
            : Mathf.Max(0f, fallbackLifetime);

        if (resolvedLifetime <= 0f)
        {
            resolvedLifetime = Mathf.Max(0f, fallbackLifetime);
        }

        return Mathf.Max(0f, resolvedLifetime + extraLifetime);
    }

    private float ResolveParticleLifetime()
    {
        CacheParticleSystemsIfNeeded();
        if (_particleSystems == null || _particleSystems.Length == 0)
        {
            return 0f;
        }

        float maxLifetime = 0f;
        for (int i = 0; i < _particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = _particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            if (main.loop)
            {
                return 0f;
            }

            float lifetime = GetMax(main.startDelay) + main.duration + GetMax(main.startLifetime);
            if (lifetime > maxLifetime)
            {
                maxLifetime = lifetime;
            }
        }

        return maxLifetime;
    }

    private float GetMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => GetCurveEndValue(curve.curve, curve.constantMax),
            ParticleSystemCurveMode.TwoCurves => GetCurveEndValue(curve.curveMax, curve.constantMax),
            _ => curve.constantMax
        };
    }

    private float GetCurveEndValue(AnimationCurve curve, float fallback)
    {
        if (curve == null || curve.length == 0)
        {
            return fallback;
        }

        return curve.keys[curve.length - 1].value;
    }

    private void CleanupNow()
    {
        _cleanupRoutine = null;

        switch (cleanupAction)
        {
            case TimedEffectCleanupAction3D.Destroy:
                Destroy(gameObject);
                return;

            case TimedEffectCleanupAction3D.Deactivate:
                gameObject.SetActive(false);
                return;

            case TimedEffectCleanupAction3D.DespawnToPool:
                GameObjectPool3D.Despawn(gameObject);
                return;

            default:
                if (GetComponent<PooledObject3D>() != null)
                {
                    GameObjectPool3D.Despawn(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
        }
    }

    private void CacheParticleSystemsIfNeeded()
    {
        if (_particleSystems == null)
        {
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void StopCleanupRoutine()
    {
        if (_cleanupRoutine != null)
        {
            StopCoroutine(_cleanupRoutine);
            _cleanupRoutine = null;
        }
    }
}
