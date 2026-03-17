using System;
using StarfallArena.UI;

[Serializable]
public sealed class AugmentLoadoutEntry
{
    public Augment definition;
    public int roundAcquired;
    public object persistentState;
}

[Serializable]
public sealed class NetworkAugmentLoadoutEntry
{
    public string augmentId;
    public int roundAcquired;
    public byte stateFlags;
}
