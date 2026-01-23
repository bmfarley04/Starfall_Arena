using UnityEngine;

public class PlanetRotator : MonoBehaviour
{
    // The rate of rotation around the Y-axis (vertical axis)
    [Tooltip("The speed at which the planet rotates around its Y-axis.")]
    public float rotationSpeed = 10f;

    // A flag to determine if the rotation should be in the opposite direction
    [Tooltip("Check this to reverse the direction of rotation.")]
    public bool reverseDirection = false;

    // Caching the transform for efficiency (optional but good practice)
    private Transform planetTransform;

    void Start()
    {
        planetTransform = transform;
    }

    void Update()
    {
        // Determine the direction multiplier based on the checkbox
        int direction = reverseDirection ? -1 : 1;

        // The core rotation command:
        // Rotates the object by 'rotationSpeed' degrees per second around the Y-axis (up axis).
        // Time.deltaTime ensures the rotation is frame-rate independent.
        planetTransform.Rotate(Vector3.up * rotationSpeed * direction * Time.deltaTime, Space.Self);
    }
}