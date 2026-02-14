using System;
using StarfallArena.UI;

[Serializable]
public sealed class AugmentLoadoutEntry
{
    public Augment definition;
    public int roundAcquired;
    public object persistentState;
}
