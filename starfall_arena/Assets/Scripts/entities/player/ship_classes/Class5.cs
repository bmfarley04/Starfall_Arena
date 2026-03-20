using System.Collections;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;


// ===== CLASS5 IMPLEMENTATION =====
public class Class5 : Player, IChargeProvider
{
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

    /// <inheritdoc/>
    public bool TrySpendCharges(int amount)
    {
        if (CurrentCharges < amount) return false;
        LoseCharges(amount);
        PlaySpendChargeSound();
        return true;
    }
    /// <inheritdoc/>
    public void GainCharges(int amount)
    {
        if (amount <= 0) return;
        if (CurrentCharges < MaxCharges)
        {
            int previous = CurrentCharges;
            CurrentCharges += amount;
            if (CurrentCharges > MaxCharges) CurrentCharges = MaxCharges;
            _lastChargeGainTime = Time.time;
            if (CurrentCharges > previous)
            {
                PlayGainChargeSound();
            }
            Debug.Log($"Gained charges: {amount}. Current charges: {CurrentCharges}/{MaxCharges}");
        }
        UpdateAbilityChargeVisuals();
    }

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        MaxCharges = abilityChargePrefabs.Count;
        _lastChargeGainTime = Time.time;
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();
        HandleChargeRegen();
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

    private void LoseCharges(int amount)
    {
        if (CurrentCharges > 0)
        {
            CurrentCharges -= amount;
            if (CurrentCharges < 0) CurrentCharges = 0;
            _lastChargeGainTime = Time.time;
            Debug.Log($"Lost charges: {amount}. Current charges: {CurrentCharges}/{MaxCharges}");
        }
        UpdateAbilityChargeVisuals();
    }

    private void UpdateAbilityChargeVisuals()
    {
        for (int i = 0; i < abilityChargePrefabs.Count; i++)
        {
            abilityChargePrefabs[i].SetActive(i < CurrentCharges);
        }
    }

    private void PlayGainChargeSound()
    {
        if (CurrentCharges <= 0) return;

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
        const int burstCount = 4;
        const float burstSpacing = 0.06f;

        for (int i = 0; i < burstCount; i++)
        {
            fireSound.Play(GetAvailableAudioSource());
            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(burstSpacing);
            }
        }

        _projectileSoundBurstCoroutine = null;
    }
}
