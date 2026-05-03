using UnityEngine;

/// <summary>
/// Keeps a distant background object almost fixed while the camera or player moves.
/// </summary>
public class DistantPlanetParallax : MonoBehaviour
{
    [Header("Tracking")]
    [Tooltip("Object to track for parallax. Use the gameplay camera or player. If empty, the script uses Camera.main.")]
    [SerializeField] private Transform parallaxTarget;

    [Header("Parallax")]
    [Tooltip("How much of the target movement this planet follows. 0 is fully static, 0.01-0.05 is very distant, 1 follows exactly.")]
    [Range(0f, 1f)]
    [SerializeField] private float movementMultiplier = 0.02f;

    [Tooltip("When enabled, horizontal target movement affects the planet position.")]
    [SerializeField] private bool useHorizontalParallax = true;

    [Tooltip("When enabled, vertical target movement affects the planet position.")]
    [SerializeField] private bool useVerticalParallax = true;

    private Vector3 startingPosition;
    private Vector3 targetStartingPosition;

    private void Awake()
    {
        if (parallaxTarget == null && Camera.main != null)
        {
            parallaxTarget = Camera.main.transform;
        }

        startingPosition = transform.position;

        if (parallaxTarget != null)
        {
            targetStartingPosition = parallaxTarget.position;
        }
    }

    private void LateUpdate()
    {
        if (parallaxTarget == null)
        {
            return;
        }

        Vector3 targetDelta = parallaxTarget.position - targetStartingPosition;

        if (!useHorizontalParallax)
        {
            targetDelta.x = 0f;
        }

        if (!useVerticalParallax)
        {
            targetDelta.y = 0f;
        }

        targetDelta.z = 0f;
        transform.position = startingPosition + targetDelta * movementMultiplier;
    }
}
