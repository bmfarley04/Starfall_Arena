using UnityEngine;

[DisallowMultipleComponent]
public class StaggeredMissileWeaponEnemy3D : MissileWeaponEnemy3D
{
    [Header("Staggered Launch")]
    [Tooltip("If true, each successful fire uses only one configured muzzle, then advances to the next muzzle for the next shot. If false, all muzzles fire together like the base enemy missile weapon.")]
    [SerializeField] private bool fireOneLauncherPerShot = true;

    [Tooltip("If true, the launcher index wraps back to the first muzzle after the last one. If false, it clamps to the last muzzle until reset or re-enabled.")]
    [SerializeField] private bool loopLauncherSequence = true;

    [Tooltip("Starting launcher index used when this component is enabled. Leave at 0 for the first configured muzzle.")]
    [SerializeField] private int startingLauncherIndex;

    [Tooltip("Seconds between missiles within one rack activation. The inherited weapon cooldown still controls how often a new rack sequence can begin.")]
    [SerializeField] private float launcherStaggerInterval = 0.75f;

    private readonly Transform[] _singleMuzzle = new Transform[1];
    private int _nextLauncherIndex;
    private int _remainingLaunchesInSequence;
    private float _nextSequenceShotTime = float.NegativeInfinity;

    private void OnEnable()
    {
        ResetLauncherSequence();
    }

    private void OnValidate()
    {
        startingLauncherIndex = Mathf.Max(0, startingLauncherIndex);
        launcherStaggerInterval = Mathf.Max(0f, launcherStaggerInterval);
    }

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

    public void ResetLauncherSequence()
    {
        _nextLauncherIndex = Mathf.Max(0, startingLauncherIndex);
        _remainingLaunchesInSequence = 0;
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
        if (!fireOneLauncherPerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length == 0)
        {
            return base.ResolveFiringMuzzles();
        }

        int index = Mathf.Clamp(_nextLauncherIndex, 0, WeaponConfig.muzzles.Length - 1);
        _singleMuzzle[0] = WeaponConfig.muzzles[index] != null ? WeaponConfig.muzzles[index] : transform;
        return _singleMuzzle;
    }

    protected override void OnLocalVolleyFired(int spawnedCount)
    {
        if (spawnedCount > 0)
        {
            AdvanceLauncherIndex();
            CompleteSequenceShot();
        }
    }

    protected override void OnNetworkVolleyBuilt(int requestCount)
    {
        if (requestCount > 0)
        {
            AdvanceLauncherIndex();
            CompleteSequenceShot();
        }
    }

    private void AdvanceLauncherIndex()
    {
        if (!fireOneLauncherPerShot || WeaponConfig.muzzles == null || WeaponConfig.muzzles.Length <= 1)
        {
            return;
        }

        int next = _nextLauncherIndex + 1;
        if (next >= WeaponConfig.muzzles.Length)
        {
            next = loopLauncherSequence ? 0 : WeaponConfig.muzzles.Length - 1;
        }

        _nextLauncherIndex = next;
    }

    private bool ShouldUseStaggeredSequence()
    {
        return fireOneLauncherPerShot && WeaponConfig.muzzles != null && WeaponConfig.muzzles.Length > 1;
    }

    private bool IsSequenceActive => _remainingLaunchesInSequence > 0;

    private void BeginSequence()
    {
        _remainingLaunchesInSequence = WeaponConfig.muzzles.Length;
        _nextSequenceShotTime = Time.time;
    }

    private void CompleteSequenceShot()
    {
        if (!IsSequenceActive)
        {
            return;
        }

        _remainingLaunchesInSequence--;
        if (_remainingLaunchesInSequence <= 0)
        {
            _remainingLaunchesInSequence = 0;
            _nextSequenceShotTime = float.NegativeInfinity;
            return;
        }

        _nextSequenceShotTime = Time.time + launcherStaggerInterval;
    }
}
