using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


// ===== CLASS3 IMPLEMENTATION =====
public class Class3 : Player
{
    float originalRotationSpeed;
    bool isAnchored = false;
    // ===== ABILITY CONFIGURATION STRUCTS =====
    [System.Serializable]
    public struct AbilitiesConfigStarter
    {
        [Header("Ability 1")]
        public Ability1Stats ability1Stats;
        [System.Serializable]
        public struct Ability1Stats
        {
            [Tooltip("Cooldown time between uses (seconds)")]
            public float cooldown;
            [Tooltip("Duration of ability effect (seconds)")]
            public float duration;
        }
        [Header("Ability 2")]
        public Ability2Stats ability2Stats;
        [System.Serializable]
        public struct Ability2Stats
        {
            [Tooltip("Cooldown time between uses (seconds)")]
            public float cooldown;
            [Tooltip("Duration of ability effect (seconds)")]
            public float duration;
        }
        [Header("Ability 3")]
        public Ability3Stats ability3Stats;
        [System.Serializable]
        public struct Ability3Stats
        {
            [Tooltip("Cooldown time between uses (seconds)")]
            public float cooldown;
            [Tooltip("Duration of ability effect (seconds)")]
            public float duration;
        }
        [Header("Ability 4")]
        public Ability4Stats ability4Stats;
        [System.Serializable]
        public struct Ability4Stats
        {
            [Tooltip("Cooldown time between uses (seconds)")]
            public float cooldown;
            [Tooltip("Duration of ability effect (seconds)")]
            public float duration;
        }
    }

    // ===== PRIMARY WEAPON =====
    [Header("Primary Weapon Settings")]
    [Tooltip("Cooldown between normal fire shots (seconds)")]
    public new float fireCooldown = 0.5f;

    // ===== ABILITIES =====
    [Header("Abilities")]
    public AbilitiesConfigStarter abilities;

    // ===== PRIVATE STATE =====
    private string exampleVar;

    // ===== INITIALIZATION =====
    protected override void Awake()
    {
        base.Awake();
        originalRotationSpeed = movement.rotationSpeed;
    }

    // ===== UPDATE LOOP =====
    protected override void Update()
    {
        base.Update();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (isAnchored)
        {
            _rb.linearDamping+=.1f;
        }
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
    }

    // Anchor
    void OnAnchor(InputValue value)
    {
        if (value.isPressed)
        {
            thrusters.invertColors = true;
            Debug.Log("Anchor Activated: Rotate " + movement.rotationSpeed);
            movement.rotationSpeed*=3;
            isAnchored = true;
        }
        else
        {
            thrusters.invertColors = false;
            isAnchored = false;
            _rb.linearDamping = 0f;
            movement.rotationSpeed = originalRotationSpeed;
            Debug.Log("Anchor Deactivated: Rotate "+originalRotationSpeed);
        }
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
}
