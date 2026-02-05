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
    public GameObject augmentedObject;

    protected int roundAcquired = 0;
    protected LayerMask originalLayer;
    protected LayerMask thisPlayerLayer;
    protected Player player;
    protected virtual void Awake()
    {
        originalLayer = augmentedObject.layer;
        player = augmentedObject.GetComponent<Player>();
    }

    protected virtual void FixedUpdate()
    {

    }

    public void AcquireAugment(int roundAcquired)
    {
        this.roundAcquired = roundAcquired;
    }

    public void AcquireAugment()
    {
        AcquireAugment(0);
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
