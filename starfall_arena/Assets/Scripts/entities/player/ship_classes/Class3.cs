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

    // ===== PRIVATE STATE =====
    private string exampleVar;

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

    // ===== ABILITY INPUT CALLBACKS =====
    void OnPrimary(InputValue value)
    {
        Debug.Log("Primary Fire Activated");
    }

    void OnAbility1(InputValue value)
    {
        Debug.Log("Ability 1 Activated");
    }
    void OnAbility2(InputValue value)
    {
        Debug.Log("Ability 2 Activated");
    }
    void OnAbility3(InputValue value)
    {
        Debug.Log("Ability 3 Activated");
    }
    void OnAbility4(InputValue value)
    {
        Debug.Log("Ability 4 Activated");
        invisibilityScript.TryUseAbility();
    }

    // ===== ABILITY 1 =====
    // ===== ABILITY 2 =====
    // ===== ABILITY 3 =====
    // ===== ABILITY 4 =====

    // ===== AUDIO =====

    // ===== OVERRIDES =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        base.Die();
    }

    protected override Vector3 GetFireDirection(Transform turret)
    {
        return turret.up;
    }
}
