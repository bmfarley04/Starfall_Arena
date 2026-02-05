using UnityEngine;

public class BlazeOfGlory : Augment
{
    public GameObject bogEffect;
    public float damageMultiplier = 1.5f;
    public float healthThreshold = 0.25f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Awake()
    {
        base.Awake();
        augmentInfo = new AugmentInfo
        {
            augmentID = "BlazeOfGlory",
            augmentName = "Blaze of Glory",
            description = "Deal 50% more damage when below 25% health."
        };
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (player != null)
        {
            if (AugmentActivated(true)) // Always active augment. Could be changed to round-based if desired, here for consistency among augments.
            {
                Debug.Log("Blaze of Glory FixedUpdate: Checking health status.");
                bool shouldBeActive = player.CurrentHealth / player.maxHealth < healthThreshold;
                if (bogEffect != null)
                {
                    bogEffect.SetActive(shouldBeActive);
                }
                // Only update if state changed (avoids spamming logs)
                if (shouldBeActive && !player.damageMultipliers.ContainsKey(augmentInfo.augmentID))
                {
                    player.damageMultipliers.Add(augmentInfo.augmentID, damageMultiplier);
                    Debug.Log("Blaze of Glory activated: Damage increased.");
                }
                else if (!shouldBeActive && player.damageMultipliers.ContainsKey(augmentInfo.augmentID))
                {
                    player.damageMultipliers.Remove(augmentInfo.augmentID);
                    Debug.Log("Blaze of Glory deactivated: Damage returned to normal.");
                }
            }
        }
    }
}
