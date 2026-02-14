using System.Collections.Generic;
using UnityEngine;

public enum WaveShapeType { Box, Circle }

[System.Serializable]
public class Wave
{
    [Tooltip("Time in seconds for the wave to reach the end center shape.")]
    public float duration;
    [Tooltip("Whether the wave shrinks. If enabled, the shape will stay still with the parameters from the last wave for the duration.")]
    public bool stationaryBox;
    [Tooltip("Damage per second dealt to entities outside the safe zone")]
    public float fireDamage = 5f;
    [Tooltip("How often to apply fire damage (seconds). Default: 0.5")]
    public float damageTickInterval = 0.5f;
    [Tooltip("Whether this wave will automatically chain with the previous wave. If enabled, this wave's safe shape center will be set from the previous wave's end center shape.")]
    public bool autoChainWithPrevious;
    
    [Tooltip("The type of shape for the safe zone")]
    public WaveShapeType shapeType = WaveShapeType.Box;
    
    [Tooltip("The box safe zone (used when shapeType is Box)")]
    public WaveBox safeBox;
    [Tooltip("The box that the wave will reach at the end (used when shapeType is Box)")]
    public WaveBox endCenterBox;
    
    [Tooltip("The circle safe zone (used when shapeType is Circle)")]
    public WaveCircle safeCircle;
    [Tooltip("The circle that the wave will reach at the end (used when shapeType is Circle)")]
    public WaveCircle endCenterCircle;

    public Wave(float duration, WaveBox endCenterBox, WaveBox safeBox)
    {
        this.duration = duration;
        this.endCenterBox = endCenterBox;
        this.safeBox = safeBox;
        this.fireDamage = 5f;
        this.damageTickInterval = 0.5f;
        this.stationaryBox = false;
        this.autoChainWithPrevious = false;
        this.shapeType = WaveShapeType.Box;
    }
    
    public Wave(float duration, WaveCircle endCenterCircle, WaveCircle safeCircle)
    {
        this.duration = duration;
        this.endCenterCircle = endCenterCircle;
        this.safeCircle = safeCircle;
        this.fireDamage = 5f;
        this.damageTickInterval = 0.5f;
        this.stationaryBox = false;
        this.autoChainWithPrevious = false;
        this.shapeType = WaveShapeType.Circle;
    }
    
    /// <summary>
    /// Gets the current safe zone center based on shape type
    /// </summary>
    public Vector2 GetSafeZoneCenter()
    {
        return shapeType == WaveShapeType.Box ? safeBox.centerPoint : safeCircle.centerPoint;
    }
    
    /// <summary>
    /// Gets the end center point based on shape type
    /// </summary>
    public Vector2 GetEndCenter()
    {
        return shapeType == WaveShapeType.Box ? endCenterBox.centerPoint : endCenterCircle.centerPoint;
    }
    
    /// <summary>
    /// Sets the safe zone center based on shape type
    /// </summary>
    public void SetSafeZoneCenter(Vector2 center)
    {
        if (shapeType == WaveShapeType.Box)
            safeBox.centerPoint = center;
        else
            safeCircle.centerPoint = center;
    }
    
    /// <summary>
    /// Sets the end center based on shape type
    /// </summary>
    public void SetEndCenter(Vector2 center)
    {
        if (shapeType == WaveShapeType.Box)
            endCenterBox.centerPoint = center;
        else
            endCenterCircle.centerPoint = center;
    }
}