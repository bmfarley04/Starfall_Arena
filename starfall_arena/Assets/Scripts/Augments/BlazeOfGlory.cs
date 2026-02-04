using UnityEngine;

public class BlazeOfGlory : Augment
{
    private const string DAMAGE_MULTIPLIER_SOURCE = "BlazeOfGlory";
    private bool _isActive = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Awake()
    {
        base.Awake();
        augmentInfo = new AugmentInfo
        {
            augmentName = "Blaze of Glory",
            description = "Deal 50% more damage when below 15% health."
        };
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (player != null)
        {
            if (AugmentActivated(true))
            {
                bool shouldBeActive = player.CurrentHealth / player.maxHealth < 0.15f;
                
                // Only update if state changed (avoids spamming logs)
                if (shouldBeActive && !_isActive)
                {
                    player.AddDamageMultiplier(DAMAGE_MULTIPLIER_SOURCE, 1.5f);
                    _isActive = true;
                }
                else if (!shouldBeActive && _isActive)
                {
                    player.RemoveDamageMultiplier(DAMAGE_MULTIPLIER_SOURCE);
                    _isActive = false;
                }
            }
            else if (_isActive)
            {
                // Augment was deactivated, remove bonus
                player.RemoveDamageMultiplier(DAMAGE_MULTIPLIER_SOURCE);
                _isActive = false;
            }
        }
    }
}
