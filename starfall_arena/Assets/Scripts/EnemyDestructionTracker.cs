using UnityEngine;

public class EnemyDestructionTracker : MonoBehaviour
{
    public MapManagerScript mapManager;

    private void OnDestroy()
    {
        // Don't trigger on scene unload or application quit
        if (!this.gameObject.scene.isLoaded) return;
        
        if (mapManager != null)
        {
            // Pass the position of this enemy before it's destroyed
            mapManager.OnEnemyDestroyed(transform.position);
        }
        else
        {
            Debug.LogError("EnemyDestructionTracker: mapManager reference is null!", this.gameObject);
        }
    }
}