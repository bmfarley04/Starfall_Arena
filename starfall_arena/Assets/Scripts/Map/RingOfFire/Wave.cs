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
    [Tooltip("Whether this wave will automatically chain with the previous wave. If enabled, this wave's safe box center will be set from the previous wave's end center box (width/length are still used from this wave's safe box). Additionally, this wave's end center box settings will be set based on it's last save box.")]
    public bool autoChainWithPrevious; 
    [Tooltip("The box that the wave will reach at the end of its duration.")]
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