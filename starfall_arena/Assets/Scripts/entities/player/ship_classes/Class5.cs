using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


// ===== CLASS5 IMPLEMENTATION =====
[RequireComponent(typeof(Class5NetworkBridge))]
public class Class5 : Player, IChargeProvider
{
    internal const int ProjectileBurstCount = 4;
    internal const float ProjectileBurstSpacing = 0.06f;

    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    public new float fireCooldown = 0.5f;

    // ===== ABILITY MODIFIERS =====
    public List<GameObject> abilityChargePrefabs = new List<GameObject>();

    [Header("Charge Regeneration")]
    [Tooltip("Seconds between passive charge gains")]
    public float chargeRegenInterval = 5f;

    [Header("Charge Audio")]
    [Tooltip("Sound played when the 1st charge is gained")]
    public SoundEffect gainChargeSound1;
    [Tooltip("Sound played when the 2nd charge is gained")]
    public SoundEffect gainChargeSound2;
    [Tooltip("Sound played when the 3rd charge is gained")]
    public SoundEffect gainChargeSound3;
    [Tooltip("Sound played when the 4th charge is gained (also used in the combo)")]
    public SoundEffect gainChargeSound4;
    [Tooltip("Time between each sound when gaining the 4th charge (seconds)")]
    public float fourthChargeComboSpacing = 0.05f;
    [Tooltip("Sound played when charges are spent")]
    public SoundEffect spendChargeSound;

    // ===== ICHARGE PROVIDER =====
    public int CurrentCharges { get; private set; } = 0;
    public int MaxCharges { get; private set; } = 4;

    private float _lastChargeGainTime;
    private Coroutine _fourthChargeSoundCoroutine;
    private Coroutine _projectileSoundBurstCoroutine;
    private Class5NetworkBridge _bridge;

    /// <inheritdoc/>
    public bool TrySpendCharges(int amount)
    {
        if (amount <= 0) return true;
        if (CurrentCharges < amount) return false;

        int applied = ApplyChargeDelta(-amount, playAudio: true, broadcast: true);
        return applied < 0;
    }
    /// <inheritdoc/>
    public void GainCharges(int amount)
    {
        if (amount <= 0) return;
        ApplyChargeDelta(amount, playAudio: true, broadcast: true);
    }

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        _bridge = GetComponent<Class5NetworkBridge>();
        if (abilityChargePrefabs != null && abilityChargePrefabs.Count > 0)
        {
            MaxCharges = abilityChargePrefabs.Count;
        }
        _lastChargeGainTime = Time.time;
        UpdateAbilityChargeVisuals();
    }

    protected override void Start()
    {
        base.Start();

        // Push initial charge state to clients once the NetworkObject is spawned.
        if (ShouldHandleChargesLocally())
        {
            ApplyChargeDelta(0, playAudio: false, broadcast: true, forceBroadcast: true);
        }
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();

        if (ShouldHandleChargesLocally())
        {
            HandleChargeRegen();
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

    }

    private void HandleChargeRegen()
    {
        if (CurrentCharges >= MaxCharges) return;
        if (Time.time >= _lastChargeGainTime + chargeRegenInterval)
        {
            GainCharges(1);
        }
    }

    /// <summary>
    /// Called by NetMovement on the server when the Player component is disabled
    /// (client-owned characters) so passive charge regen still runs.
    /// </summary>
    public void ServerTickCharges()
    {
        if (ShouldHandleChargesLocally())
        {
            HandleChargeRegen();
        }
    }

    private void LoseCharges(int amount)
    {
        ApplyChargeDelta(-Mathf.Abs(amount), playAudio: true, broadcast: true);
    }

    private void UpdateAbilityChargeVisuals()
    {
        for (int i = 0; i < abilityChargePrefabs.Count; i++)
        {
            if (abilityChargePrefabs[i] != null)
            {
                abilityChargePrefabs[i].SetActive(i < CurrentCharges);
            }
        }
    }

    private void PlayGainChargeSound()
    {
        if (CurrentCharges <= 0) return;

        if (!isActiveAndEnabled && _bridge != null)
        {
            _bridge?.PlayChargeAudioForProxy(
                CurrentCharges,
                gainChargeSound1,
                gainChargeSound2,
                gainChargeSound3,
                gainChargeSound4,
                fourthChargeComboSpacing,
                isSpend: false);
            return;
        }

        if (_fourthChargeSoundCoroutine != null)
        {
            StopCoroutine(_fourthChargeSoundCoroutine);
            _fourthChargeSoundCoroutine = null;
        }

        int chargeIndex = Mathf.Min(CurrentCharges, MaxCharges);
        SoundEffect effect = GetChargeSound(chargeIndex);

        if (chargeIndex >= 4)
        {
            _fourthChargeSoundCoroutine = StartCoroutine(PlayFourthChargeCombo());
        }
        else
        {
            PlaySoundEffect(effect);
        }
    }

    private SoundEffect GetChargeSound(int chargeIndex)
    {
        switch (chargeIndex)
        {
            case 1: return gainChargeSound1;
            case 2: return gainChargeSound2;
            case 3: return gainChargeSound3;
            default: return gainChargeSound4 != null ? gainChargeSound4 : gainChargeSound1;
        }
    }

    private IEnumerator PlayFourthChargeCombo()
    {
        SoundEffect[] sounds =
        {
            GetChargeSound(1),
            GetChargeSound(2),
            GetChargeSound(3),
            GetChargeSound(4)
        };

        float comboPitch = GetComboPitch(sounds);

        for (int i = 0; i < sounds.Length; i++)
        {
            PlaySoundEffect(sounds[i], comboPitch);
            if (i < sounds.Length - 1)
            {
                yield return new WaitForSeconds(fourthChargeComboSpacing);
            }
        }

        _fourthChargeSoundCoroutine = null;
    }

    private float GetComboPitch(SoundEffect[] sounds)
    {
        for (int i = 0; i < sounds.Length; i++)
        {
            SoundEffect effect = sounds[i];
            if (effect == null) continue;
            return UnityEngine.Random.Range(effect.minPitch, effect.maxPitch);
        }
        return 1f;
    }

    private void PlaySoundEffect(SoundEffect effect, float? forcedPitch = null)
    {
        if (effect == null) return;

        AudioSource source = GetAvailableAudioSource();
        if (source == null) return;

        source.clip = effect.clip;
        source.volume = effect.volume;
        source.pitch = forcedPitch ?? UnityEngine.Random.Range(effect.minPitch, effect.maxPitch);
        source.Play();
    }

    private void PlaySpendChargeSound()
    {
        if (spendChargeSound != null)
        {
            if (!isActiveAndEnabled && _bridge != null)
            {
                _bridge?.PlayChargeAudioForProxy(
                    CurrentCharges,
                    gainChargeSound1,
                    gainChargeSound2,
                    gainChargeSound3,
                    gainChargeSound4,
                    fourthChargeComboSpacing,
                    isSpend: true,
                    spendChargeSound);
                return;
            }

            PlaySoundEffect(spendChargeSound);
        }
    }

    protected override Vector3 GetFireDirection(Transform turret)
    {
        return turret.up;
    }

    protected override void TryFireProjectile()
    {
        if (isMovementLocked) return;

        // Suppress the base class's single-shot audio so we can play a 4-shot burst instead.
        SoundEffect originalFireSound = projectileFireSound;
        projectileFireSound = null;

        float previousFireTime = _lastFireTime;
        base.TryFireProjectile();
        projectileFireSound = originalFireSound;

        if (originalFireSound == null) return;
        if (Mathf.Approximately(_lastFireTime, previousFireTime)) return; // base did not fire

        if (_projectileSoundBurstCoroutine != null)
        {
            StopCoroutine(_projectileSoundBurstCoroutine);
        }
        _projectileSoundBurstCoroutine = StartCoroutine(PlayProjectileFireSoundBurst(originalFireSound));
    }

    private IEnumerator PlayProjectileFireSoundBurst(SoundEffect fireSound)
    {
        for (int i = 0; i < ProjectileBurstCount; i++)
        {
            fireSound.Play(GetAvailableAudioSource());
            if (i < ProjectileBurstCount - 1)
            {
                yield return new WaitForSeconds(ProjectileBurstSpacing);
            }
        }

        _projectileSoundBurstCoroutine = null;
    }

    private bool ShouldHandleChargesLocally()
    {
        // Offline/local play OR this peer is authoritative server/host.
        if (!NetTickUtil.IsActive) return true;
        if (_bridge == null || !_bridge.IsSpawned) return true;
        return _bridge.IsServer;
    }


    private int ApplyChargeDelta(int delta, bool playAudio, bool broadcast, bool forceBroadcast = false)
    {
        if (delta == 0) return 0;

        int previous = CurrentCharges;
        if (delta < 0 && CurrentCharges < -delta)
        {
            return 0;
        }

        CurrentCharges = Mathf.Clamp(CurrentCharges + delta, 0, MaxCharges);
        _lastChargeGainTime = Time.time;

        int appliedDelta = CurrentCharges - previous;
        if (playAudio && appliedDelta != 0)
        {
            if (appliedDelta > 0)
            {
                PlayGainChargeSound();
            }
            else
            {
                PlaySpendChargeSound();
            }
        }

        UpdateAbilityChargeVisuals();

        bool shouldBroadcast = broadcast && _bridge != null && _bridge.IsSpawned && _bridge.IsServer && NetTickUtil.IsActive;
        if (shouldBroadcast && (appliedDelta != 0 || forceBroadcast))
        {
            _bridge.BroadcastChargeState(CurrentCharges, appliedDelta, playAudio || forceBroadcast);
        }

        Debug.Log($"Charges delta={delta} applied={appliedDelta}. Current: {CurrentCharges}/{MaxCharges}");
        return appliedDelta;
    }

    /// <summary>
    /// Authoritative network callback from NetMovement to sync charge count and audio.
    /// </summary>
    public void ApplyNetworkChargeState(int currentCharges, int delta, bool playAudio)
    {
        int previous = CurrentCharges;
        CurrentCharges = Mathf.Clamp(currentCharges, 0, MaxCharges);
        _lastChargeGainTime = Time.time;

        int effectiveDelta = delta != 0 ? delta : CurrentCharges - previous;

        if (playAudio && effectiveDelta != 0)
        {
            if (effectiveDelta > 0)
            {
                PlayGainChargeSound();
            }
            else
            {
                PlaySpendChargeSound();
            }
        }

        UpdateAbilityChargeVisuals();
    }

}
