using StarfallArena.UI;
using UnityEngine;

[CreateAssetMenu(fileName = "Flyers", menuName = "Starfall Arena/Augments/Flyers", order = 2)]
public class Flyers : Augment
{
    [Header("AutoCast")]
    [Tooltip("Seconds between automatic flyer respawn cycles")]
    public float autocastInterval = 4f;

    [Header("Flyer Setup")]
    [Tooltip("How many flyers orbit the player")]
    public int flyerCount = 1;

    [Tooltip("Optional visual prefab for each flyer")]
    public GameObject flyerPrefab;

    [Tooltip("Orbit radius around the player")]
    public float orbitRadius = 1.4f;

    [Tooltip("Orbit angular speed in degrees per second")]
    public float orbitSpeed = 140f;

    [Tooltip("Distance at which a flyer launches toward the opponent")]
    public float engageRange = 9f;

    [Tooltip("Homing speed when a flyer launches")]
    public float homingSpeed = 13f;

    [Tooltip("Maximum seconds a launched flyer can home before timing out")]
    public float homingDuration = 1.2f;

    [Tooltip("Contact damage dealt by a launched flyer")]
    public float hitDamage = 6f;

    [Tooltip("Impact force dealt by a launched flyer")]
    public float impactForce = 0f;

    [Tooltip("Distance threshold used to register a hit")]
    public float hitRadius = 0.6f;

    public override IAugmentRuntime CreateRuntime()
    {
        return new FlyersRuntime(this);
    }
}