using UnityEngine;
using UnityEngine.InputSystem;

public class FaerieShift : Ability
{
    [System.Serializable]
    public struct FaerieShiftConfig
    {
        [Header("Scale")]
        [Tooltip("Scale multiplier when ability is active (0.1 = 10% size)")]
        public float scaleMultiplier;

        [Header("Movement")]
        [Tooltip("Movement speed multiplier when ability is active (3.0 = 3x faster)")]
        public float speedMultiplier;
        [Tooltip("Rotation speed multiplier when ability is active (10.0 = 10x faster)")]
        public float rotationMultiplier;

        [Header("Vulnerability")]
        [Tooltip("Damage multiplier when ability is active (10.0 = 10x damage taken)")]
        public float takeDamageMultiplier;

        [Header("Sound Effects")]
        [Tooltip("Sound played when activating Faerie Shift")]
        public SoundEffect activateSound;
        [Tooltip("Sound played when deactivating Faerie Shift")]
        public SoundEffect deactivateSound;
    }

    public FaerieShiftConfig config;

    // ===== PRIVATE STATE =====
    private Vector3 _originalScale;
    private bool _isActive = false;

    protected override void Awake()
    {
        base.Awake();
        
        // Store original scale
        if (player != null)
        {
            _originalScale = player.transform.localScale;
        }
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        // Toggle on press
        if (value.isPressed)
        {
            ActivateShift();
        }
        else
        {
            DeactivateShift();
        }
    }

    private void ActivateShift()
    {
        if (player == null) return;

        _isActive = true;

        // Apply scale reduction
        player.transform.localScale = _originalScale * config.scaleMultiplier;

        // Play activation sound
        if (config.activateSound != null)
        {
            config.activateSound.Play(player.GetAvailableAudioSource());
        }

        // Disable other abilities while active
        DisableOtherAbilities(true);

        Debug.Log($"Faerie Shift activated! Scale: {config.scaleMultiplier}x, Speed: {config.speedMultiplier}x, Rotation: {config.rotationMultiplier}x, Damage: {config.takeDamageMultiplier}x");
    }

    private void DeactivateShift()
    {
        if (player == null) return;

        _isActive = false;

        // Restore original scale
        player.transform.localScale = _originalScale;

        // Play deactivation sound
        if (config.deactivateSound != null)
        {
            config.deactivateSound.Play(player.GetAvailableAudioSource());
        }

        // Re-enable other abilities
        DisableOtherAbilities(false);

        Debug.Log("Faerie Shift deactivated!");
    }

    public override bool IsAbilityActive()
    {
        return _isActive;
    }

    public override void ApplyThrustMultiplier()
    {
        if (_isActive)
        {
            player.movement.thrustForce *= config.speedMultiplier;
        }
    }

    public override bool DisablePrimaryFire()
    {
        return _isActive;
    }

    public override void RestoreThrustMultiplier()
    {
        if (_isActive)
        {
            player.movement.thrustForce /= config.speedMultiplier;
        }
    }

    public override void ApplyRotationMultiplier()
    {
        if (_isActive)
        {
            player.movement.rotationSpeed *= config.rotationMultiplier;
        }
    }

    public override void ApplyTakeDamageMultiplier(ref float damage)
    {
        base.ApplyTakeDamageMultiplier(ref damage);
        if (_isActive)
        {
            damage *= config.takeDamageMultiplier;
        }
    }

    public override void Die()
    {
        base.Die();

        // Reset scale if player dies while shifted
        if (_isActive && player != null)
        {
            player.transform.localScale = _originalScale;
            _isActive = false;
        }
    }
}
