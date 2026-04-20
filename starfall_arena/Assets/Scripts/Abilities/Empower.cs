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

    [Header("Empower")]
    public EmpowerConfig empower;

    private bool _isEmpoweredActive;
    private Coroutine _empowerRoutine;

    public bool IsEmpoweredActive => _isEmpoweredActive;

    public override void UseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        if (_empowerRoutine != null)
        {
            StopCoroutine(_empowerRoutine);
        }

        _empowerRoutine = StartCoroutine(EmpowerRoutine());
    }

    public override bool IsAbilityActive()
    {
        return _isEmpoweredActive;
    }

    public override void Die()
    {
        if (_empowerRoutine != null)
        {
            StopCoroutine(_empowerRoutine);
            _empowerRoutine = null;
        }

        if (_isEmpoweredActive)
        {
            _isEmpoweredActive = false;
            if (empower.deactivateSound != null)
            {
                empower.deactivateSound.Play(player.GetAvailableAudioSource());
            }
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

        float duration = Mathf.Max(0f, empower.duration > 0f ? empower.duration : stats.duration);
        yield return new WaitForSeconds(duration);

        _isEmpoweredActive = false;

        if (empower.deactivateSound != null)
        {
            empower.deactivateSound.Play(player.GetAvailableAudioSource());
        }

        _empowerRoutine = null;
    }
}
