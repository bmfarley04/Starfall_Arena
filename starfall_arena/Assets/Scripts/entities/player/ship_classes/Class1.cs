using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ===== CLASS1 IMPLEMENTATION =====
public class Class1 : Player
{
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
        var activeAbility = abilities.FirstOrDefault(a => a?.IsAbilityActive() == true);

        if (activeAbility.HasThrustMitigation()) 
        {
            return;
        }
        if (activeAbility != null)
        {
            activeAbility.ApplyThrustMultiplier();
        }

        base.FixedUpdate();
        // Restore original thrust force
        activeAbility.RestoreThrustMultiplier();
    }

    // ===== ABILITY INPUT CALLBACKS =====
    void OnAbility3(InputValue value)
    {
        ability3.TryUseAbility(value);
    }

    void OnAbility4()
    {
        ability4.TryUseAbility();
    }

    void OnAbility2()
    {
        ability2.TryUseAbility();
    }

    void OnAbility1(InputValue value)
    {
        ability1.TryUseAbility();
    }

    

    // ===== ROTATION OVERRIDES =====
    protected override void RotateWithController()
    {
        float originalRotationSpeed = movement.rotationSpeed;

        var activeAbility = abilities.FirstOrDefault(a => a?.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyRotationMultiplier();
        }

        base.RotateWithController();

        movement.rotationSpeed = originalRotationSpeed;
    }

    protected override void RotateTowardMouse()
    {
        float originalRotationSpeed = movement.rotationSpeed;

        var activeAbility = abilities.FirstOrDefault(a => a?.IsAbilityActive() == true);
        if (activeAbility != null)
        {
            activeAbility.ApplyRotationMultiplier();
        }

        base.RotateTowardMouse();

        movement.rotationSpeed = originalRotationSpeed;
    }

    // ===== OVERRIDES =====
    public override void TakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
    {
        if (abilities.Any(a => a?.HasDamageMitigation() == true))
        {
            return;
        }

        base.TakeDamage(damage, impactForce, hitPoint, source);
    }

    protected override void Die()
    {
        ability1.Die();
        ability2.Die();
        ability3.Die();
        ability4.Die();

        base.Die();
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (abilities.Any(a => a?.HasCollisionModification() == true))
        {
            foreach (var ability in abilities.Where(a => a?.HasCollisionModification() == true))
            {
                ability.ProcessCollisionModification(collider);
            }
        }
    }
}
