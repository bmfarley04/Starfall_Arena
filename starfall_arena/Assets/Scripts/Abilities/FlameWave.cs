using UnityEngine;
using UnityEngine.InputSystem;

public class FlameWave : Ability
{
    [System.Serializable]
    public struct FlameWaveConfig
    {
        [Header("Flame Pocket")]
        [Tooltip("Fire hazard prefab to spawn (should include FireHazard component)")]
        public GameObject flamePrefab;
        [Tooltip("Damage per second dealt by the flame pocket")]
        public float damagePerSecond;
        [Tooltip("Lifetime of the flame pocket (seconds)")]
        public float duration;
        [Tooltip("Impact force applied when dealing damage")]
        public float impactForce;
        [Tooltip("Forward offset from each turret to spawn the flame pocket")]
        public float forwardOffset;
        [Tooltip("Initial launch speed for the flame pocket (0 = stationary)")]
        public float launchSpeed;
        [Tooltip("Multiplier controlling how quickly the flame pocket slows during its lifetime (1 = stop by end, <1 slower, >1 faster)")]
        public float slowRate;

        [Header("Charges")]
        [Tooltip("Number of charges required to cast Flame Wave")]
        public int chargesRequired;

        [Header("Audio")]
        public SoundEffect fireSound;
        [Tooltip("Looping sound played from each flame hazard during its lifetime")]
        public SoundEffect loopSound;
    }

    [Header("Ability - Flame Wave")]
    public FlameWaveConfig flameWave;

    private IChargeProvider _chargeProvider;

    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
        if (flameWave.chargesRequired <= 0)
        {
            flameWave.chargesRequired = 1;
        }
        if (flameWave.slowRate <= 0f)
        {
            flameWave.slowRate = 1f;
        }
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return false;
        }

        if (!CanUseAbility())
        {
            return false;
        }

        if (_chargeProvider != null)
        {
            if (!_chargeProvider.TrySpendCharges(flameWave.chargesRequired))
            {
                Debug.Log("❌ FlameWave: not enough charges.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning("FlameWave: player does not provide charges; ability will not consume any.");
        }

        lastUsedAbility = Time.time;
        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        if (flameWave.flamePrefab == null)
        {
            Debug.LogWarning("FlameWave: flamePrefab not assigned.");
            return;
        }

        if (player == null || player.turrets == null || player.turrets.Length == 0)
        {
            Debug.LogWarning("FlameWave: no turrets available to spawn flames.");
            return;
        }

        foreach (Transform turret in player.turrets)
        {
            if (turret == null) continue;

            Vector3 direction = turret.up;
            if (direction == Vector3.zero)
            {
                direction = transform.up;
            }
            direction = direction.normalized;

            Vector3 spawnPosition = turret.position + direction * flameWave.forwardOffset;
            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

            GameObject hazard = Instantiate(flameWave.flamePrefab, spawnPosition, rotation);

            if (hazard.TryGetComponent<FireHazard>(out var fireHazard))
            {
                fireHazard.Initialize(player.enemyTag, flameWave.damagePerSecond, flameWave.duration, flameWave.impactForce, flameWave.slowRate);
                fireHazard.SetLoopSound(flameWave.loopSound);
            }

            Rigidbody2D rb = hazard.GetComponent<Rigidbody2D>();
            if (rb != null && flameWave.launchSpeed > 0f)
            {
                rb.linearVelocity = (Vector2)direction * flameWave.launchSpeed;
            }
        }

        if (flameWave.fireSound != null)
        {
            flameWave.fireSound.Play(player.GetAvailableAudioSource());
        }
    }

    public override bool IsAbilityActive()
    {
        return false;
    }
}
