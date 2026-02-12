using UnityEngine;

namespace StarfallArena.UI
{
    /// <summary>
    /// ScriptableObject that defines an augment with its visual data and effect script.
    /// Create via: Right-click -> Create -> Starfall Arena -> Augment
    /// </summary>
    [CreateAssetMenu(fileName = "New Augment", menuName = "Starfall Arena/Generic Augment", order = 1)]
    public class Augment : ScriptableObject
    {
        [Header("Display Info")]
        [Tooltip("Icon displayed in augment select UI")]
        public Sprite icon;

        [Tooltip("Name of the augment")]
        public string augmentName;

        [Tooltip("Description shown in augment select UI")]
        [TextArea(3, 5)]
        public string description;

        [HideInInspector]
        public string augmentID;


        [Tooltip("Number of Rounds this augment lasts (-1 for unlimited)")]
        public int rounds = -1;


        protected int roundAcquired = 0;
        protected int currentRound = 0; // This will be updated by the manager each round, used for checking augment duration
        protected LayerMask originalLayer;
        protected LayerMask thisPlayerLayer;
        [HideInInspector] // This will be set by the player when acquiring the augment, used for any augment-specific effects that need to be attached to the player
        public GameObject playerReference; // Set by the player when acquiring the augment, used for any augment-specific effects that need to be attached to the player
        protected Player player;
        protected virtual void Awake() // This is called when the ScriptableObject is created or loaded, NOT when the game starts
        {
        }

        /// <summary>
        /// Called every fixed frame while the augment is active.
        /// This method is invoked by Entity's FixedUpdate, providing frame-rate independent execution for physics-based or time-dependent augment effects.
        /// Override this method in derived augment classes to implement continuous or periodic effects.
        /// </summary>
        public virtual void ExecuteEffects()
        {
        }

        public virtual void SetUpAugment(int roundAcquired)
        {
            this.roundAcquired = roundAcquired;
            originalLayer = playerReference.layer;
            player = playerReference.GetComponent<Player>();
        }

        public virtual void SetUpAugment()
        {
            SetUpAugment(0);
        }

        public virtual bool IsAugmentActive()
        {
            if (rounds == -1)
            {
                return true; // Always active if rounds is -1
            }
            return currentRound - roundAcquired < rounds;
        }

        public virtual void AddDamageMultiplier(float damageMultiplier)
        {
            if (IsAugmentActive() && !player.damageMultipliers.ContainsKey(augmentID))
            {
                player.damageMultipliers.Add(augmentID, damageMultiplier);
                Debug.Log("" + augmentName + " activated: Damage increased by " + damageMultiplier);
            }
        }

        public virtual void RemoveDamageMultiplier()
        {
            player.damageMultipliers.Remove(augmentID);
            Debug.Log("" + augmentName + " deactivated: Damage returned to normal.");
        }

        public virtual void OnTakeDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
        {
        }

        /// <summary>
        /// Called before the Entity actually applies damage. Allows augments to modify or cancel
        /// how incoming damage is applied (for example: ignore shield damage or ignore health damage).
        /// Default implementation does nothing.
        /// </summary>
        public virtual void OnBeforeTakeDamage(ref float damage, ref bool shieldIgnored, ref bool healthIgnored, DamageSource source = DamageSource.Projectile)
        {
        }

        public virtual void OnTakeDirectDamage(float damage, float impactForce = 0f, Vector3 hitPoint = default, DamageSource source = DamageSource.Projectile)
        {
        }

        /// <summary>
        /// Called before the Entity actually applies direct damage (bypassing shields).
        /// Allows augments to cancel or modify the incoming direct health damage.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void OnBeforeTakeDirectDamage(ref float damage, ref bool healthIgnored, DamageSource source = DamageSource.Projectile)
        {
        }

        public virtual void OnContact(Collision2D collision)
        {
        }
    }
}
