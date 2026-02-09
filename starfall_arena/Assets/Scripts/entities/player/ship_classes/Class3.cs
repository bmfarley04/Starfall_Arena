using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;


// ===== CLASS3 IMPLEMENTATION =====
public class Class3 : Player
{
    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    public new float fireCooldown = 0.5f;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
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

    protected override Vector3 GetFireDirection(Transform turret)
    {
        return turret.up;
    }
}
