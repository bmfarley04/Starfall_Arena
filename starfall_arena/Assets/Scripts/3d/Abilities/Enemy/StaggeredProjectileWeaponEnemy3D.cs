using UnityEngine;

public class StaggeredProjectileWeaponEnemy3D : EnemyProjectileWeaponBase3D
{
    [Header("Staggered Launch")]
    [Tooltip("If true, each rack activation fires one configured muzzle at a time. If false, all muzzles fire together like the base enemy projectile weapon.")]
    [SerializeField] private bool fireOneTurretPerShot = true;

    [Tooltip("If true, the turret index wraps back to the first muzzle after the last one. If false, it clamps to the last muzzle until reset or re-enabled.")]
    [SerializeField] private bool loopTurretSequence = true;

    [Tooltip("If true, each staggered laser bolt picks a random configured turret instead of using the sequential turret index.")]
    [SerializeField] private bool randomizeTurretSelection;

    [Tooltip("Starting turret index used when this component is enabled. Leave at 0 for the first configured muzzle.")]
    [SerializeField] private int startingTurretIndex;

    [Tooltip("Seconds between laser bolts within one turret-rack activation. The inherited weapon cooldown still controls how often a new rack sequence can begin.")]
    [SerializeField] private float turretStaggerInterval = 0.2f;

    private readonly Transform[] _singleMuzzle = new Transform[1];
    private int _nextTurretIndex;
    private int _remainingShotsInSequence;
    private float _nextSequenceShotTime = float.NegativeInfinity;

    public override NetProjectileVisualType3D NetworkVisualType => NetProjectileVisualType3D.EnemySecondaryProjectile;

    public override bool IsFireGateReady
    {
        get
        {
            if (!ShouldUseStaggeredSequence())
            {
                return base.IsFireGateReady;
            }

            if (IsSequenceActive)
            {
                return Time.time >= _nextSequenceShotTime && HasValidProjectilePrefab();
            }

            return base.IsFireGateReady;
        }
    }

    private void OnEnable()
    {
        ResetTurretSequence();
    }

    private void OnValidate()
    {
        startingTurretIndex = Mathf.Max(0, startingTurretIndex);
        turretStaggerInterval = Mathf.Max(0f, turretStaggerInterval);
    }

    public void ResetTurretSequence()
    {
        _nextTurretIndex = Mathf.Max(0, startingTurretIndex);
        _remainingShotsInSequence = 0;
        _nextSequenceShotTime = float.NegativeInfinity;
    }

    public override bool TryConsumeFireGate()
    {
        if (!ShouldUseStaggeredSequence())
        {
            return base.TryConsumeFireGate();
        }

        if (!IsSequenceActive)
        {
            if (!base.TryConsumeFireGate())
            {
                return false;
            }

            BeginSequence();
        }

        return Time.time >= _nextSequenceShotTime && HasValidProjectilePrefab();
    }

    protected override Transform[] ResolveFiringMuzzles()
    {
        if (!fireOneTurretPerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length == 0)
        {
            return base.ResolveFiringMuzzles();
        }

        int index = ResolveTurretIndex();
        _singleMuzzle[0] = WeaponConfig.muzzles[index] != null ? WeaponConfig.muzzles[index] : transform;
        return _singleMuzzle;
    }

    protected override void OnLocalVolleyFired(int spawnedCount)
    {
        if (spawnedCount <= 0)
        {
            return;
        }

        AdvanceTurretIndex();
        CompleteSequenceShot();
    }

    protected override void OnNetworkVolleyBuilt(int requestCount)
    {
        if (requestCount <= 0)
        {
            return;
        }

        AdvanceTurretIndex();
        CompleteSequenceShot();
    }

    private void AdvanceTurretIndex()
    {
        if (!fireOneTurretPerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length <= 1)
        {
            return;
        }

        if (randomizeTurretSelection)
        {
            return;
        }

        int next = _nextTurretIndex + 1;
        if (next >= WeaponConfig.muzzles.Length)
        {
            next = loopTurretSequence ? 0 : WeaponConfig.muzzles.Length - 1;
        }

        _nextTurretIndex = next;
    }

    private int ResolveTurretIndex()
    {
        if (WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length == 0)
        {
            return 0;
        }

        if (randomizeTurretSelection)
        {
            return Random.Range(0, WeaponConfig.muzzles.Length);
        }

        return Mathf.Clamp(_nextTurretIndex, 0, WeaponConfig.muzzles.Length - 1);
    }

    private bool ShouldUseStaggeredSequence()
    {
        return fireOneTurretPerShot && WeaponConfig.muzzles != null && WeaponConfig.muzzles.Length > 1;
    }

    private bool IsSequenceActive => _remainingShotsInSequence > 0;

    private void BeginSequence()
    {
        _remainingShotsInSequence = WeaponConfig.muzzles.Length;
        _nextSequenceShotTime = Time.time;
    }

    private void CompleteSequenceShot()
    {
        if (!IsSequenceActive)
        {
            return;
        }

        _remainingShotsInSequence--;
        if (_remainingShotsInSequence <= 0)
        {
            _remainingShotsInSequence = 0;
            _nextSequenceShotTime = float.NegativeInfinity;
            return;
        }

        _nextSequenceShotTime = Time.time + turretStaggerInterval;
    }
}
