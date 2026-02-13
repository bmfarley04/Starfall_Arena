using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Wave
{
    [Tooltip("Time in seconds for the wave to reach the end center box.")]
    public float duration;
    [Tooltip("Damage per second dealt to entities outside the safe zone")]
    public float fireDamage;
    [Tooltip("How often to apply fire damage (seconds). Default: 0.5")]
    public float damageTickInterval;
    [Tooltip("The box that the wave will reach at the end of its duration. (Ignored if auto-chain waves is enabled for waves after the first)")]
    public WaveBox endCenterBox;
    [Tooltip("The box that the player must be in to be safe from the wave. When auto-chain is enabled, only width/length are used (center is set from previous wave's endCenterBox).")]
    public WaveBox safeBox;
    public Wave(float duration, WaveBox endCenterBox, WaveBox safeBox)
    {
        this.duration = duration;
        this.endCenterBox = endCenterBox;
        this.safeBox = safeBox;
    }
}