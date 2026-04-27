using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Class4BurstWeapon3D : ProjectileWeapon3D
{
    [System.Serializable]
    private struct BurstConfig3D
    {
        public float burstCooldown;
        public int burstCount;
        public float burstInterval;
    }

    [Header("Class4 Burst")]
    [SerializeField] private BurstConfig3D burst = new BurstConfig3D
    {
        burstCooldown = 1.5f,
        burstCount = 3,
        burstInterval = 0.08f
    };

    private bool _isBursting;
    private float _nextBurstReadyTime = float.NegativeInfinity;
    private Coroutine _burstRoutine;

    private void OnDisable()
    {
        if (_burstRoutine != null)
        {
            StopCoroutine(_burstRoutine);
            _burstRoutine = null;
        }

        _isBursting = false;
    }

    protected override void OnFireHeld()
    {
        TryStartBurst();
    }

    protected override float GetConfiguredCooldownDuration()
    {
        return Mathf.Max(0f, burst.burstCooldown + GetBurstSequenceDuration());
    }

    public override bool IsReticleSpinActive()
    {
        return IsFireHeld || _isBursting;
    }

    private void TryStartBurst()
    {
        if (_isBursting || Time.time < _nextBurstReadyTime)
        {
            return;
        }

        ProjectileWeaponConfig3D weaponConfig = WeaponConfig;
        if (weaponConfig.projectilePrefab == null || !TrySpendResource(weaponConfig.energyCost))
        {
            return;
        }

        _burstRoutine = StartCoroutine(FireBurstSequence());
    }

    private IEnumerator FireBurstSequence()
    {
        _isBursting = true;

        ProjectileWeaponConfig3D weaponConfig = WeaponConfig;
        float burstSequenceDuration = GetBurstSequenceDuration();
        _nextBurstReadyTime = Time.time + Mathf.Max(0f, burst.burstCooldown) + burstSequenceDuration;
        StartCooldown(Mathf.Max(0f, burst.burstCooldown) + burstSequenceDuration);

        int accuracyAttackId = ResolveAccuracyAttackId();
        int shotCount = Mathf.Max(1, burst.burstCount);
        float burstInterval = Mathf.Max(0f, burst.burstInterval);

        for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
        {
            ProjectileFireRequest3D request = BuildDefaultFireRequest(weaponConfig);
            request.accuracyAttackIdOverride = accuracyAttackId;
            NormalizePlayerProjectileTargeting(ref request);

            if (!FireProjectilePattern(request, weaponConfig, NetworkFireSound))
            {
                break;
            }

            if (shotIndex < shotCount - 1 && burstInterval > 0f)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        _isBursting = false;
        _burstRoutine = null;
    }

    private int ResolveAccuracyAttackId()
    {
        if (NetTickUtil.IsActive)
        {
            return NetTickUtil.CurrentTick;
        }

        PlayerCombatStats3D stats = Owner != null ? Owner.GetComponent<PlayerCombatStats3D>() : null;
        return stats != null ? stats.BeginTrackedAttack() : PlayerCombatStats3D.InvalidAttackId;
    }

    private float GetBurstSequenceDuration()
    {
        int burstCount = Mathf.Max(1, burst.burstCount);
        return Mathf.Max(0f, burst.burstInterval) * Mathf.Max(0, burstCount - 1);
    }
}
