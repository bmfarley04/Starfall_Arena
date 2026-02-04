using System;
using Unity.VisualScripting;
using UnityEngine;

public class Augment : MonoBehaviour
{
    [System.Serializable]
    public struct AugmentInfo
    {
        public string augmentName;
        public string description;
    }
    public AugmentInfo augmentInfo;

    [System.Serializable]
    public struct AugmentSettings
    {
        [Tooltip("Number of Rounds this augment lasts")]
        public int rounds;
    }
    public AugmentSettings augmentSettings;

    protected Component augmentComponent;
    protected int roundAcquired = 0;
    protected LayerMask originalLayer;
    protected LayerMask thisPlayerLayer;
    protected Player player;
    protected virtual void Awake()
    {
        originalLayer = gameObject.layer;
        player = gameObject.GetComponent<Player>();
    }

    protected virtual void FixedUpdate()
    {

    }

    public void AcquireAugment(int roundAcquired, Component augment)
    {
        this.roundAcquired = roundAcquired;

        // Add the component type to the gameObject if it's not already present
        if (augment != null)
        {
            Type componentType = augment.GetType();
            if (gameObject.GetComponent(componentType) == null)
            {
                augmentComponent = gameObject.AddComponent(componentType);
            }
        }
    }

    public void AcquireAugment()
    {
        this.AcquireAugment(0, this);
    }

    public bool AugmentActivated(int roundAcquired, int currentRound)
    {
        return currentRound - roundAcquired < augmentSettings.rounds;
    }

    public bool AugmentActivated(bool alwaysTrue)
    {
        return alwaysTrue;
    }
}
