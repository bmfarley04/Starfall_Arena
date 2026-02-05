using System;
using UnityEngine;

public class Augment : MonoBehaviour
{
    [System.Serializable]
    public struct AugmentInfo
    {
        [Tooltip("Unique identifier for the augment. Should be the name of the class.")]
        public string augmentID; // Unique identifier for the augment. Should be the name of the class.
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
