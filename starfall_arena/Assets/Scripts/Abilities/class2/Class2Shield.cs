using UnityEngine;
using UnityEngine.InputSystem;

public class Class2Shield : Ability
{
    [System.Serializable]
    public struct ShieldAbilityConfig
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses (seconds)")]
        public float cooldown;
        [Tooltip("Shield active duration (seconds)")]
        public float activeDuration;

        [Header("Shield")]
        [Tooltip("ReflectShield component (drag from Hierarchy)")]
        public ReflectShield shield;
        [Tooltip("Color of the shield when active")]
        public Color shieldColor;

        [Header("Sound Effects")]
        [Tooltip("Shield duration sound (loops while active)")]
        public SoundEffect shieldLoopSound;
        [Tooltip("Sound when projectile hits the shield")]
        public SoundEffect shieldHitSound;
    }

    [Header("Ability 2 - Shield")]
    public ShieldAbilityConfig shieldConfig;

    private float _lastShieldTime = -999f;
    private Coroutine _shieldCoroutine;
    private AudioSource _shieldSource;
    private NetMovement _netMovement;

    protected override void Awake()
    {
        base.Awake();
        _netMovement = GetComponent<NetMovement>();
        _shieldSource = gameObject.AddComponent<AudioSource>();
        _shieldSource.playOnAwake = false;
        _shieldSource.loop = true;
        _shieldSource.spatialBlend = 0f;
    }

    public override bool TryUseAbility(InputValue value)
    {
        stats.cooldown = shieldConfig.cooldown;
        stats.duration = shieldConfig.activeDuration;

        if (!CanUseAbility())
        {
            return false;
        }

        if (shieldConfig.shield == null)
        {
            Debug.LogWarning("Shield not assigned!");
            return false;
        }

        _lastShieldTime = Time.time;
        lastUsedAbility = Time.time;
        bool useNetworkPath = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned && _netMovement.IsOwner;
        if (useNetworkPath)
        {
            if (!_netMovement.IsServer)
            {
                ApplyNetworkShieldActivation(authoritative: false);
            }

            _netMovement.RequestClass2ShieldActivation();
            return true;
        }

        ApplyNetworkShieldActivation(authoritative: true);
        return true;
    }

    public override bool IsAbilityActive()
    {
        return shieldConfig.shield != null && shieldConfig.shield.IsActive();
    }

    public override bool HasDamageMitigation()
    {
        return IsAbilityActive();
    }

    public override bool HasCollisionModification()
    {
        return IsAbilityActive();
    }

    public override void ProcessCollisionModification(Collider2D collider)
    {
        if (!IsAbilityActive())
        {
            return;
        }

        ProjectileScript projectile = collider.GetComponent<ProjectileScript>();
        if (projectile != null && projectile.targetTag == player.thisPlayerTag)
        {
            Vector3 hitPoint = collider.ClosestPoint(transform.position);
            shieldConfig.shield.OnReflectHit(hitPoint);

            if (shieldConfig.shieldHitSound != null)
            {
                shieldConfig.shieldHitSound.Play(player.GetAvailableAudioSource());
            }

            Destroy(projectile.gameObject);
        }
    }

    public override float GetHUDFillRatio()
    {
        if (shieldConfig.cooldown <= 0f) return 0f;
        float elapsed = Time.time - _lastShieldTime;
        if (elapsed >= shieldConfig.cooldown) return 0f;
        return 1f - (elapsed / shieldConfig.cooldown);
    }

    public override bool IsOnCooldown()
    {
        return Time.time < _lastShieldTime + shieldConfig.cooldown;
    }

    public override void Die()
    {
        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }

        base.Die();
    }

    private System.Collections.IEnumerator ActivateShield()
    {
        shieldConfig.shield.Activate(shieldConfig.shieldColor);

        if (shieldConfig.shieldLoopSound != null && _shieldSource != null)
        {
            shieldConfig.shieldLoopSound.Play(_shieldSource);
        }

        yield return new WaitForSeconds(shieldConfig.activeDuration);

        shieldConfig.shield.Deactivate();

        if (_shieldSource != null && _shieldSource.isPlaying)
        {
            _shieldSource.Stop();
        }
    }

    public void ApplyNetworkShieldActivation(bool authoritative)
    {
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }

        _shieldCoroutine = StartCoroutine(ActivateShield());
    }
}
