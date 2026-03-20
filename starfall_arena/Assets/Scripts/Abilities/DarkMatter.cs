using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections;

public class DarkMatter : Ability
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
        [Tooltip("Multiplier controlling how quickly the dark matter slows during its lifetime (1 = stop by end, <1 slower, >1 faster)")]
        public float slowRate;

        [Header("Charges")]
        [Tooltip("Minimum number of charges required to cast Dark Matter")]
        public int chargesRequired;

        [Header("Audio")]
        public SoundEffect fireSound;
        [Tooltip("Looping sound played from each dark matter hazard during its lifetime")]
        public SoundEffect loopSound;
    }

    [Header("Ability - Dark Matter")]
    [FormerlySerializedAs("flameWave")]
    public FlameWaveConfig darkMatter;

    private IChargeProvider _chargeProvider;
    private int _chargesSpentThisCast = 1;
    private Coroutine _chargeSoundCoroutine;
    private NetMovement _netMovement;
    private bool _chargesSpentLocally;
    private const float CHARGE_SOUND_SPACING = 0.06f;

    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
        _netMovement = GetComponent<NetMovement>();
        if (darkMatter.chargesRequired <= 0)
        {
            darkMatter.chargesRequired = 1;
        }
        if (darkMatter.slowRate <= 0f)
        {
            darkMatter.slowRate = 1f;
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

        _chargesSpentLocally = false;

        if (!TryConsumeCharges(out _chargesSpentThisCast, spendNow: true))
        {
            return false;
        }

        _chargesSpentLocally = true;
        lastUsedAbility = Time.time;
        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        if (_chargeSoundCoroutine != null)
        {
            StopCoroutine(_chargeSoundCoroutine);
        }
        _chargeSoundCoroutine = StartCoroutine(PlayChargeUseSounds(_chargesSpentThisCast));

        bool netActive = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned;
        bool isOwner = netActive && _netMovement.IsOwner;
        bool isServer = netActive && _netMovement.IsServer;

        if (netActive && isOwner)
        {
            ApplyNetworkDarkMatterCast(_chargesSpentThisCast, authoritative: isServer, chargesAlreadySpent: _chargesSpentLocally);

            if (!isServer)
            {
                _netMovement.RequestDarkMatterCast(_chargesSpentThisCast);
            }
            return;
        }

        ApplyNetworkDarkMatterCast(_chargesSpentThisCast, authoritative: true, chargesAlreadySpent: _chargesSpentLocally);
    }

    private IEnumerator PlayChargeUseSounds(int charges)
    {
        if (darkMatter.fireSound == null || player == null) yield break;

        for (int i = 0; i < charges; i++)
        {
            darkMatter.fireSound.Play(player.GetAvailableAudioSource());
            if (i < charges - 1)
            {
                yield return new WaitForSeconds(CHARGE_SOUND_SPACING);
            }
        }
    }

    public override bool IsAbilityActive()
    {
        return false;
    }

    public void ApplyNetworkDarkMatterCast(int requestedCharges, bool authoritative, bool chargesAlreadySpent = false)
    {
        if (darkMatter.flamePrefab == null)
        {
            Debug.LogWarning("DarkMatter: flamePrefab not assigned.");
            return;
        }

        if (player == null || player.turrets == null || player.turrets.Length == 0)
        {
            Debug.LogWarning("DarkMatter: no turrets available to spawn dark matter.");
            return;
        }

        int chargesToUse = Mathf.Max(1, Mathf.Max(requestedCharges, darkMatter.chargesRequired));

        if (authoritative && _chargeProvider != null && !chargesAlreadySpent)
        {
            int availableCharges = _chargeProvider.CurrentCharges;
            if (availableCharges < darkMatter.chargesRequired)
            {
                return;
            }

            chargesToUse = Mathf.Clamp(chargesToUse, darkMatter.chargesRequired, availableCharges);
            if (!_chargeProvider.TrySpendCharges(chargesToUse))
            {
                return;
            }
        }

        _chargesSpentThisCast = chargesToUse;

        float hazardDuration = Mathf.Max(0.1f, darkMatter.duration * Mathf.Max(chargesToUse, 1));

        foreach (Transform turret in player.turrets)
        {
            if (turret == null) continue;

            Transform target = FindNearestEnemy(turret.position);
            Vector3 direction = target != null ? (target.position - turret.position) : turret.up;
            if (direction == Vector3.zero)
            {
                direction = transform.up;
            }
            direction = direction.normalized;

            NetDarkMatterHazardSpawnData spawnData = new NetDarkMatterHazardSpawnData
            {
                SpawnPosition = turret.position + direction * darkMatter.forwardOffset,
                Direction = direction,
                DamagePerSecond = darkMatter.damagePerSecond,
                Lifetime = hazardDuration,
                ImpactForce = darkMatter.impactForce,
                SlowRate = darkMatter.slowRate,
                LaunchSpeed = darkMatter.launchSpeed,
                DisableDampening = darkMatter.slowRate <= 0f,
            };

            SpawnDarkMatterHazard(spawnData, authoritative);

            if (NetTickUtil.IsActive && _netMovement != null && _netMovement.IsServer)
            {
                _netMovement.BroadcastDarkMatterHazardSpawn(spawnData);
            }
        }
    }

    public void SpawnRemoteHazard(NetDarkMatterHazardSpawnData spawnData)
    {
        SpawnDarkMatterHazard(spawnData, authoritative: false);
    }

    private void SpawnDarkMatterHazard(NetDarkMatterHazardSpawnData spawnData, bool authoritative)
    {
        Vector3 direction3 = spawnData.Direction;
        if (direction3 == Vector3.zero)
        {
            direction3 = transform.up;
        }
        direction3 = direction3.normalized;

        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction3);
        GameObject hazard = Instantiate(darkMatter.flamePrefab, spawnData.SpawnPosition, rotation);

        if (hazard.TryGetComponent<FireHazard>(out var fireHazard))
        {
            fireHazard.disableVelocityDampening = spawnData.DisableDampening;
            fireHazard.Initialize(player.enemyTag, spawnData.DamagePerSecond, spawnData.Lifetime, spawnData.ImpactForce, spawnData.SlowRate);
            fireHazard.SetLoopSound(darkMatter.loopSound);

            if (NetTickUtil.IsActive)
            {
                fireHazard.SetCosmeticOnly(!authoritative);
            }
        }

        Rigidbody2D rb = hazard.GetComponent<Rigidbody2D>();
        if (rb != null && spawnData.LaunchSpeed > 0f)
        {
            rb.linearVelocity = (Vector2)direction3 * spawnData.LaunchSpeed;
        }

        var seeker = hazard.GetComponent<DarkMatterSeeker>();
        if (seeker == null)
        {
            seeker = hazard.AddComponent<DarkMatterSeeker>();
        }
        seeker.Initialize(player.enemyTag, spawnData.LaunchSpeed, spawnData.Lifetime, 0.1f);
    }

    private Transform FindNearestEnemy(Vector3 origin)
    {
        if (player == null || string.IsNullOrEmpty(player.enemyTag))
        {
            return null;
        }

        GameObject[] potentialTargets;
        try
        {
            potentialTargets = GameObject.FindGameObjectsWithTag(player.enemyTag);
        }
        catch (UnityException)
        {
            return null;
        }

        Transform nearest = null;
        float nearestSqr = float.MaxValue;

        foreach (GameObject obj in potentialTargets)
        {
            if (obj == null || !obj.activeInHierarchy) continue;

            float sqr = (obj.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = obj.transform;
            }
        }

        return nearest;
    }

    private bool TryConsumeCharges(out int chargesSpent, bool spendNow)
    {
        chargesSpent = Mathf.Max(darkMatter.chargesRequired, 1);

        if (_chargeProvider == null)
        {
            Debug.LogWarning("DarkMatter: player does not provide charges; assuming minimum for duration scaling.");
            return true;
        }

        int availableCharges = _chargeProvider.CurrentCharges;
        if (availableCharges < darkMatter.chargesRequired)
        {
            Debug.Log("❌ DarkMatter: not enough charges.");
            return false;
        }

        chargesSpent = Mathf.Max(1, Mathf.Max(availableCharges, darkMatter.chargesRequired));

        if (spendNow && !_chargeProvider.TrySpendCharges(chargesSpent))
        {
            Debug.Log("❌ DarkMatter: failed to spend charges.");
            return false;
        }

        return true;
    }
}
