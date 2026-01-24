using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


// ===== CLASSSTARTER IMPLEMENTATION =====
public class ClassStarter : Player
{
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

    void Ability1(InputValue value)
    {
        Debug.Log("Ability 1 Activated");
    }
    void Ability2(InputValue value)
    {
        Debug.Log("Ability 2 Activated");
    }
    void Ability3(InputValue value)
    {
        Debug.Log("Ability 3 Activated");
    }
    void Ability4(InputValue value)
    {
        Debug.Log("Ability 4 Activated");
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
