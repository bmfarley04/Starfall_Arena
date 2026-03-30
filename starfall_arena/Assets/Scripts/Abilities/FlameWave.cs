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
    private NetMovement _netMovement;
    private bool _chargesSpentLocally;
    private int _chargesSpentThisCast = 1;

    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
        _netMovement = GetComponent<NetMovement>();
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

        _chargesSpentLocally = false;
        _chargesSpentThisCast = Mathf.Max(flameWave.chargesRequired, 1);

        bool netActive = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned;
        bool isOwner = netActive && _netMovement.IsOwner;

        if (_chargeProvider != null)
        {
            if (!netActive || isOwner)
            {
                if (!_chargeProvider.TrySpendCharges(_chargesSpentThisCast))
                {
                    Debug.Log("❌ FlameWave: not enough charges.");
                    return false;
                }

                _chargesSpentLocally = true;
            }
            else
            {
                Debug.LogWarning("FlameWave: non-owner tried to cast while networking is active.");
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

        bool netActive = NetTickUtil.IsActive && _netMovement != null && _netMovement.IsSpawned;
        bool isOwner = netActive && _netMovement.IsOwner;
        bool isServer = netActive && _netMovement.IsServer;

        if (netActive && isOwner)
        {
            ApplyNetworkFlameWaveCast(_chargesSpentThisCast, authoritative: isServer, chargesAlreadySpent: _chargesSpentLocally);

            if (!isServer)
            {
                _netMovement.RequestFlameWaveCast(_chargesSpentThisCast);
            }

            return;
        }

        ApplyNetworkFlameWaveCast(_chargesSpentThisCast, authoritative: true, chargesAlreadySpent: _chargesSpentLocally);
    }

    public override bool IsAbilityActive()
    {
        return false;
    }

    public void ApplyNetworkFlameWaveCast(int requestedCharges, bool authoritative, bool chargesAlreadySpent)
    {
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

        int chargesToUse = Mathf.Max(1, Mathf.Max(requestedCharges, flameWave.chargesRequired));

        if (authoritative && _chargeProvider != null && !chargesAlreadySpent)
        {
            int availableCharges = _chargeProvider.CurrentCharges;
            if (availableCharges < flameWave.chargesRequired)
            {
                Debug.Log("❌ FlameWave: not enough charges on server.");
                return;
            }

            chargesToUse = Mathf.Clamp(chargesToUse, flameWave.chargesRequired, availableCharges);
            if (!_chargeProvider.TrySpendCharges(chargesToUse))
            {
                Debug.Log("❌ FlameWave: failed to spend charges on server.");
                return;
            }
        }

        _chargesSpentThisCast = chargesToUse;
        float hazardDuration = Mathf.Max(0.1f, flameWave.duration);

        foreach (Transform turret in player.turrets)
        {
            if (turret == null)
            {
                continue;
            }

            Vector3 direction = turret.up;
            if (direction == Vector3.zero)
            {
                direction = transform.up;
            }
            direction = direction.normalized;

            NetFlameWaveHazardSpawnData spawnData = new NetFlameWaveHazardSpawnData
            {
                SpawnPosition = turret.position + direction * flameWave.forwardOffset,
                Direction = direction,
                DamagePerSecond = flameWave.damagePerSecond,
                Lifetime = hazardDuration,
                ImpactForce = flameWave.impactForce,
                SlowRate = flameWave.slowRate,
                LaunchSpeed = flameWave.launchSpeed,
                DisableDampening = flameWave.slowRate <= 0f,
            };

            SpawnFlameHazard(spawnData, authoritative);

            if (NetTickUtil.IsActive && _netMovement != null && _netMovement.IsServer)
            {
                _netMovement.BroadcastFlameWaveHazardSpawn(spawnData);
            }
        }

        if (flameWave.fireSound != null)
        {
            flameWave.fireSound.Play(player.GetAvailableAudioSource());
        }
    }

    public void SpawnRemoteHazard(NetFlameWaveHazardSpawnData spawnData)
    {
        SpawnFlameHazard(spawnData, authoritative: false);
    }

    private void SpawnFlameHazard(NetFlameWaveHazardSpawnData spawnData, bool authoritative)
    {
        Vector3 direction = spawnData.Direction;
        if (direction == Vector3.zero)
        {
            direction = transform.up;
        }
        direction = direction.normalized;

        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);
        GameObject hazard = Instantiate(flameWave.flamePrefab, spawnData.SpawnPosition, rotation);

        if (hazard.TryGetComponent<FireHazard>(out var fireHazard))
        {
            fireHazard.disableVelocityDampening = spawnData.DisableDampening;
            fireHazard.Initialize(player.enemyTag, spawnData.DamagePerSecond, spawnData.Lifetime, spawnData.ImpactForce, spawnData.SlowRate);
            fireHazard.SetLoopSound(flameWave.loopSound);

            if (NetTickUtil.IsActive)
            {
                fireHazard.SetCosmeticOnly(!authoritative);
            }
        }

        Rigidbody2D rb = hazard.GetComponent<Rigidbody2D>();
        if (rb != null && spawnData.LaunchSpeed > 0f)
        {
            rb.linearVelocity = (Vector2)direction * spawnData.LaunchSpeed;
        }
    }
}
