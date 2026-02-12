using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Wave
{
    public float duration;
    public WaveBox endCenterBox;
    public WaveBox safeBox;
    public Wave(float duration, WaveBox endCenterBox, WaveBox safeBox)
    {
        this.duration = duration;
        this.endCenterBox = endCenterBox;
        this.safeBox = safeBox;
    }
}