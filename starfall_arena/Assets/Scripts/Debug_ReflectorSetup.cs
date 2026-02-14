using UnityEngine;

/// <summary>
/// Debug script to verify Reflector ability setup on ability slot 4.
/// Add this to your Class1 player GameObject temporarily to diagnose the issue.
/// </summary>
public class Debug_ReflectorSetup : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== REFLECTOR SETUP DEBUG ===");

        // Check if Player component exists
        Player player = GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError("❌ No Player component found on this GameObject!");
            return;
        }
        Debug.Log("✓ Player component found");

        // Check if ability4 is assigned
        if (player.ability4 == null)
        {
            Debug.LogError("❌ Player.ability4 is NULL! You need to assign the Reflector component to ability4 in the Inspector.");
            return;
        }
        Debug.Log($"✓ Player.ability4 is assigned: {player.ability4.GetType().Name}");

        // Check if it's a Reflector ability
        Reflector reflector = player.ability4 as Reflector;
        if (reflector == null)
        {
            Debug.LogError($"❌ Player.ability4 is not a Reflector! It's a {player.ability4.GetType().Name}");
            return;
        }
        Debug.Log("✓ Player.ability4 is a Reflector");

        // Check if ability4 is locked
        if (reflector.isLocked)
        {
            Debug.LogWarning("⚠ Ability4 is LOCKED! Call player.UnlockAbility4() or check if the ability needs to be unlocked through gameplay.");
        }
        else
        {
            Debug.Log("✓ Ability4 is not locked");
        }

        // Check if shield is assigned
        if (reflector.reflect.shield == null)
        {
            Debug.LogError("❌ Reflector.reflect.shield is NULL! You need to drag the ReflectShield GameObject into the 'Shield' field in the Reflector component's Inspector.");
            return;
        }
        Debug.Log($"✓ Reflector shield is assigned: {reflector.reflect.shield.gameObject.name}");

        // Check if shield GameObject is active
        if (!reflector.reflect.shield.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("⚠ Shield GameObject is DISABLED in hierarchy! It won't work. Enable it in the hierarchy.");
        }
        else
        {
            Debug.Log("✓ Shield GameObject is active");
        }

        // Check if shield has required components
        MeshRenderer renderer = reflector.reflect.shield.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError("❌ ReflectShield GameObject is missing a MeshRenderer component!");
        }
        else
        {
            Debug.Log("✓ Shield has MeshRenderer");
        }

        MeshFilter filter = reflector.reflect.shield.GetComponent<MeshFilter>();
        if (filter == null)
        {
            Debug.LogError("❌ ReflectShield GameObject is missing a MeshFilter component!");
        }
        else
        {
            Debug.Log("✓ Shield has MeshFilter");
        }

        // Check cooldown settings
        if (reflector.reflect.cooldown <= 0f)
        {
            Debug.LogWarning("⚠ Reflector cooldown is 0 or negative! Set it to a positive value (e.g., 5)");
        }
        else
        {
            Debug.Log($"✓ Cooldown is set to {reflector.reflect.cooldown}s");
        }

        if (reflector.reflect.activeDuration <= 0f)
        {
            Debug.LogWarning("⚠ Reflector activeDuration is 0 or negative! Set it to a positive value (e.g., 2)");
        }
        else
        {
            Debug.Log($"✓ Active duration is set to {reflector.reflect.activeDuration}s");
        }

        Debug.Log("=== SETUP CHECK COMPLETE ===");
        Debug.Log("If all checks passed, try pressing your Ability4 button (check Input Actions for the binding)");
    }

    void Update()
    {
        // Real-time monitoring - logs when you try to use the ability
        Player player = GetComponent<Player>();
        if (player != null && player.ability4 != null)
        {
            Reflector reflector = player.ability4 as Reflector;
            if (reflector != null && reflector.IsAbilityActive())
            {
                Debug.Log("🛡 REFLECTOR IS ACTIVE RIGHT NOW!");
            }
        }
    }

    // This will be called by Unity's Input System when Ability4 is pressed
    // If this message doesn't appear, your input binding is wrong
    void OnAbility4(UnityEngine.InputSystem.InputValue value)
    {
        Debug.Log("🎮 ABILITY4 INPUT RECEIVED! Button was pressed!");

        Player player = GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError("Player component is null!");
            return;
        }

        if (player.isMovementLocked)
        {
            Debug.LogWarning("Movement is locked - ability blocked");
            return;
        }

        if (player.ability4 == null)
        {
            Debug.LogError("player.ability4 is null!");
            return;
        }

        Debug.Log($"Calling TryUseAbility on {player.ability4.GetType().Name}...");
        bool success = player.ability4.TryUseAbility(value);
        Debug.Log($"TryUseAbility returned: {success}");
    }
}
