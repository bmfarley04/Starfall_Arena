using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== CLASS1 IMPLEMENTATION =====
public class Class1 : Player
{
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    [SerializeField] private float _fireCooldown = 0.5f;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        fireCooldown = _fireCooldown;
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
}
