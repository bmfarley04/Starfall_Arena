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


    // ===== ICHARGE PROVIDER =====
    public int CurrentCharges { get; private set; } = 0;
    public int MaxCharges { get; private set; } = 4;

    /// <inheritdoc/>
    public bool TrySpendCharges(int amount)
    {
        if (CurrentCharges < amount) return false;
        LoseCharges(amount);
        return true;
    }

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        MaxCharges = abilityChargePrefabs.Count;
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

    }

    private void GainCharges(int amount)
    {
        if (CurrentCharges < MaxCharges)
        {
            CurrentCharges += amount;
            if (CurrentCharges > MaxCharges) CurrentCharges = MaxCharges;
            Debug.Log($"Gained charges: {amount}. Current charges: {CurrentCharges}/{MaxCharges}");
        }
        UpdateAbilityChargeVisuals();
    }

    private void LoseCharges(int amount)
    {
        if (CurrentCharges > 0)
        {
            CurrentCharges -= amount;
            if (CurrentCharges < 0) CurrentCharges = 0;
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
