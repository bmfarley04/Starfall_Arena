using UnityEngine;

[DisallowMultipleComponent]
public class TargetAwarenessAttackReporter3D : MonoBehaviour
{
    [Tooltip("Default seconds an offscreen attack warning stays active after this enemy fires a short projectile-style attack.")]
    [SerializeField] private float defaultPulseSeconds = 0.8f;

    [Tooltip("Minimum seconds a sustained attack remains visible after updates stop. This prevents beam/flame warnings from flickering between refreshes.")]
    [SerializeField] private float sustainedGraceSeconds = 0.12f;

    private Entity3D _lastIntendedTarget;
    private float _pulseStartTime = float.NegativeInfinity;
    private float _pulseEndTime = float.NegativeInfinity;
    private float _activeSustainedUntil = float.NegativeInfinity;

    public void ReportAttack(Entity3D intendedTarget)
    {
        ReportAttack(intendedTarget, defaultPulseSeconds);
    }

    public void ReportAttack(Entity3D intendedTarget, float pulseSeconds)
    {
        if (intendedTarget == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        _lastIntendedTarget = intendedTarget;
        _pulseStartTime = now;
        _pulseEndTime = now + Mathf.Max(0.02f, pulseSeconds);
    }

    public void ReportSustainedAttack(Entity3D intendedTarget, float activeSeconds)
    {
        if (intendedTarget == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        _lastIntendedTarget = intendedTarget;
        _pulseStartTime = now;
        _activeSustainedUntil = Mathf.Max(_activeSustainedUntil, now + Mathf.Max(sustainedGraceSeconds, activeSeconds));
        _pulseEndTime = Mathf.Max(_pulseEndTime, _activeSustainedUntil);
    }

    public void StopSustainedAttack(Entity3D intendedTarget)
    {
        if (intendedTarget != null && _lastIntendedTarget != intendedTarget)
        {
            return;
        }

        _activeSustainedUntil = Time.unscaledTime + Mathf.Max(0f, sustainedGraceSeconds);
        _pulseEndTime = Mathf.Max(_pulseEndTime, _activeSustainedUntil);
    }

    public float GetAttackPulse01(Entity3D localPlayer)
    {
        if (localPlayer == null || _lastIntendedTarget != localPlayer)
        {
            return 0f;
        }

        float now = Time.unscaledTime;
        if (now <= _activeSustainedUntil)
        {
            return 1f;
        }

        if (now >= _pulseEndTime)
        {
            return 0f;
        }

        float duration = Mathf.Max(0.02f, _pulseEndTime - _pulseStartTime);
        return 1f - Mathf.Clamp01((now - _pulseStartTime) / duration);
    }

    private void OnValidate()
    {
        defaultPulseSeconds = Mathf.Max(0.02f, defaultPulseSeconds);
        sustainedGraceSeconds = Mathf.Max(0f, sustainedGraceSeconds);
    }
}
