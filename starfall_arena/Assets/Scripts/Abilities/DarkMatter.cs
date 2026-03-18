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
    private const float CHARGE_SOUND_SPACING = 0.06f;

    protected override void Awake()
    {
        base.Awake();
        _chargeProvider = player as IChargeProvider;
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

        if (_chargeProvider != null)
        {
            int availableCharges = _chargeProvider.CurrentCharges;
            if (availableCharges < darkMatter.chargesRequired)
            {
                Debug.Log("❌ DarkMatter: not enough charges.");
                return false;
            }

            if (!_chargeProvider.TrySpendCharges(availableCharges))
            {
                Debug.Log("❌ DarkMatter: failed to spend charges.");
                return false;
            }

            _chargesSpentThisCast = Mathf.Max(availableCharges, 1);
        }
        else
        {
            Debug.LogWarning("DarkMatter: player does not provide charges; assuming 1 charge for duration scaling.");
            _chargesSpentThisCast = Mathf.Max(darkMatter.chargesRequired, 1);
        }

        lastUsedAbility = Time.time;
        UseAbility(value);
        return true;
    }

    public override void UseAbility(InputValue value)
    {
        base.UseAbility(value);

        // Play a use sound for each charge with slight offsets
        if (_chargeSoundCoroutine != null)
        {
            StopCoroutine(_chargeSoundCoroutine);
        }
        _chargeSoundCoroutine = StartCoroutine(PlayChargeUseSounds(_chargesSpentThisCast));

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

        float hazardDuration = Mathf.Max(0.1f, darkMatter.duration * Mathf.Max(_chargesSpentThisCast, 1));

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

            Vector3 spawnPosition = turret.position + direction * darkMatter.forwardOffset;
            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

            GameObject hazard = Instantiate(darkMatter.flamePrefab, spawnPosition, rotation);

            if (hazard.TryGetComponent<FireHazard>(out var fireHazard))
            {
                fireHazard.disableVelocityDampening = darkMatter.slowRate <= 0f;
                fireHazard.Initialize(player.enemyTag, darkMatter.damagePerSecond, hazardDuration, darkMatter.impactForce, darkMatter.slowRate);
                fireHazard.SetLoopSound(darkMatter.loopSound);
            }

            Rigidbody2D rb = hazard.GetComponent<Rigidbody2D>();
            if (rb != null && darkMatter.launchSpeed > 0f)
            {
                rb.linearVelocity = (Vector2)direction * darkMatter.launchSpeed;
            }

            var seeker = hazard.GetComponent<DarkMatterSeeker>();
            if (seeker == null)
            {
                seeker = hazard.AddComponent<DarkMatterSeeker>();
            }
            seeker.Initialize(player.enemyTag, darkMatter.launchSpeed, hazardDuration, 0.1f);
        }
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
}
