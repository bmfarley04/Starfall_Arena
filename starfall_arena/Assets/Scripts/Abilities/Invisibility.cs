using Unity.VisualScripting;
using UnityEngine;

public class Invisibility : Ability
{
    protected override void Awake()
    {
        base.Awake();
    }

    void FixedUpdate()
    {
        
    }

    public override void UseAbility()
    {
        base.UseAbility();
        if (gameObject.CompareTag("Player1"))
        {
            gameObject.layer = LayerMask.NameToLayer("Background1");
        }
        else if (gameObject.CompareTag("Player2"))
        {
            gameObject.layer = LayerMask.NameToLayer("Background2");
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer("Invisible");
        }
        SetAllChildrenLayer(gameObject.layer);
        Invoke("BecomeVisible", stats.duration);
    }

    void BecomeVisible()
    {
        gameObject.layer = originalLayer;
        SetAllChildrenLayer(originalLayer);
    }

    void SetAllChildrenLayer(int layer)
    {
        foreach (Transform child in transform)
        {
            child.gameObject.layer = layer;
            SetAllChildrenLayerRecursive(child, layer);
        }
    }
    void SetAllChildrenLayerRecursive(Transform parent, int layer)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.layer = layer;
            SetAllChildrenLayerRecursive(child, layer);
        }
    }
}
