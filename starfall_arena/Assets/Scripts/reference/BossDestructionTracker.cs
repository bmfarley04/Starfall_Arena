using UnityEngine;

/// <summary>
/// Tracks boss destruction and reports to MapManager for victory triggering.
/// Attach this to the boss prefab or it will be added automatically when spawned.
/// </summary>
public class BossDestructionTracker : MonoBehaviour
{
    public MapManagerScript mapManager;

    private void OnDestroy()
    {
        // Don't trigger on scene unload or application quit
        if (!this.gameObject.scene.isLoaded) return;
        
        if (mapManager != null)
        {
            // Pass the position of this boss before it's destroyed
            mapManager.OnBossDestroyed(transform.position);
        }
        else
        {
            Debug.LogError("BossDestructionTracker: mapManager reference is null!", this.gameObject);
        }
    }
}
