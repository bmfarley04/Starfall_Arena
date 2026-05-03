using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Class2Shield3D : Ability3D, IProjectileImpactHandler3D
{
    [System.Serializable]
    public struct ShieldAbilityConfig3D
    {
        [Header("Timing")]
        [Tooltip("Cooldown between uses in seconds.")]
        public float cooldown;
        [Tooltip("How long the shield stays active in seconds.")]
        public float activeDuration;

        [Header("Shield")]
        [Tooltip("ReflectShield3D used purely for the temporary shield visual.")]
        public ReflectShield3D shieldVisual;
        [Tooltip("Shield color while the ability is active.")]
        public Color shieldColor;

        [Header("Sound Effects")]
        [Tooltip("Looping sound played while the shield is active.")]
        public SoundEffect shieldLoopSound;
        [Tooltip("Sound played when a projectile is absorbed.")]
        public SoundEffect shieldHitSound;
    }

    [Header("Ability 2 - Class2 Shield 3D")]
    [SerializeField] private ShieldAbilityConfig3D shield = new ShieldAbilityConfig3D
    {
        cooldown = 4f,
        activeDuration = 1.5f,
        shieldColor = Color.cyan
    };
    [SerializeField] private AudioSource shieldLoopAudioSource;

    private Coroutine _shieldCoroutine;
    private NetCombat3D _netCombat;

    protected override void Awake()
    {
        base.Awake();
        _netCombat = GetComponent<NetCombat3D>();

        if (shieldLoopAudioSource == null)
        {
            shieldLoopAudioSource = gameObject.AddComponent<AudioSource>();
        }

        shieldLoopAudioSource.playOnAwake = false;
        shieldLoopAudioSource.loop = true;
        shieldLoopAudioSource.spatialBlend = 1f;
        shieldLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    public override bool TryUseAbility(InputValue value)
    {
        if (!value.isPressed)
        {
            return false;
        }

        if (shield.shieldVisual == null)
        {
            Debug.LogWarning("Class2Shield3D is missing its shield visual reference.", this);
            return false;
        }

        return base.TryUseAbility(value);
    }

    public override void UseAbility(InputValue value)
    {
        if (NetTickUtil.IsActive && _netCombat != null && _netCombat.IsOwner)
        {
            _netCombat.RequestClass2ShieldActivation();
            if (!_netCombat.IsServer)
            {
                ApplyNetworkShieldActivation(authoritative: false);
            }
            return;
        }

        ApplyNetworkShieldActivation(authoritative: true);
    }

    public void ApplyNetworkShieldActivation(bool authoritative)
    {
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
        }

        _shieldCoroutine = StartCoroutine(ActivateShield());
    }

    public override bool IsAbilityActive()
    {
        return shield.shieldVisual != null && shield.shieldVisual.IsActive();
    }

    protected override float GetCooldownDuration()
    {
        return shield.cooldown;
    }

    public void ApplyProfile(Class2PlayerBalanceProfile3D.Class2Stats stats)
    {
        shield.cooldown = Mathf.Max(0f, stats.shieldCooldown);
        shield.activeDuration = Mathf.Max(0f, stats.shieldActiveDuration);
    }

    public override void Die()
    {
        if (_shieldCoroutine != null)
        {
            StopCoroutine(_shieldCoroutine);
            _shieldCoroutine = null;
        }

        DeactivateShield();
    }

    bool IProjectileImpactHandler3D.TryHandleProjectileImpact(Projectile3D projectile, RaycastHit hit)
    {
        if (!IsAbilityActive() || projectile == null || entity == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(projectile.targetTag) && !entity.CompareTag(projectile.targetTag))
        {
            return false;
        }

        shield.shieldVisual.OnReflectHit(hit.point);
        shield.shieldHitSound?.PlayAtPoint(hit.point);
        GameObjectPool3D.Despawn(projectile.gameObject);
        return true;
    }

    private IEnumerator ActivateShield()
    {
        shield.shieldVisual.Activate(shield.shieldColor);
        StartShieldLoopSound();
        yield return new WaitForSeconds(shield.activeDuration);
        DeactivateShield();
        _shieldCoroutine = null;
    }

    private void DeactivateShield()
    {
        if (shield.shieldVisual != null && shield.shieldVisual.IsActive())
        {
            shield.shieldVisual.Deactivate();
        }

        StopShieldLoopSound();
    }

    private void OnDisable()
    {
        StopShieldLoopSound();
    }

    private void StartShieldLoopSound()
    {
        if (shield.shieldLoopSound == null || shieldLoopAudioSource == null)
        {
            return;
        }

        if (shieldLoopAudioSource.isPlaying && shieldLoopAudioSource.clip == shield.shieldLoopSound.clip)
        {
            return;
        }

        shield.shieldLoopSound.Play(shieldLoopAudioSource);
    }

    private void StopShieldLoopSound()
    {
        if (shieldLoopAudioSource != null && shieldLoopAudioSource.isPlaying)
        {
            shieldLoopAudioSource.Stop();
        }
    }
}
