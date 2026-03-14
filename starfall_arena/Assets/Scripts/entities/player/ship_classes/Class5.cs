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

    // ===== ICHARGE PROVIDER =====
    public int CurrentCharges { get; private set; } = 0;
    public int MaxCharges { get; private set; } = 4;

    private float _lastChargeGainTime;

    /// <inheritdoc/>
    public bool TrySpendCharges(int amount)
    {
        if (CurrentCharges < amount) return false;
        LoseCharges(amount);
        return true;
    }
    /// <inheritdoc/>
    public void GainCharges(int amount)
    {
        if (amount <= 0) return;
        if (CurrentCharges < MaxCharges)
        {
            CurrentCharges += amount;
            if (CurrentCharges > MaxCharges) CurrentCharges = MaxCharges;
            _lastChargeGainTime = Time.time;
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

    protected override Vector3 GetFireDirection(Transform turret)
    {
        return turret.up;
    }
}
