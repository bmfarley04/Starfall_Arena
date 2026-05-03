using UnityEngine;

/// <summary>
/// Keeps a distant 3D background object moving only slightly relative to a gameplay origin.
/// </summary>
public class DistantPlanetParallax3D : MonoBehaviour
{
    [Header("Tracking")]
    [Tooltip("Transform whose translation drives parallax. Prefer the player/ship for chase cameras that rotate or lag. If empty, the script uses Camera.main.")]
    [SerializeField] private Transform parallaxOrigin;

    [Header("Translation Follow")]
    [Tooltip("How much the object follows the parallax origin's translation. 0 is fixed in world space, 1 keeps the same world offset from the origin.")]
    [Range(0f, 1f)]
    [SerializeField] private float translationFollowAmount = 0.98f;

    [Tooltip("When enabled, origin movement on the world X axis affects the object position.")]
    [SerializeField] private bool useHorizontalParallax = true;

    [Tooltip("When enabled, origin movement on the world Y axis affects the object position.")]
    [SerializeField] private bool useVerticalParallax = true;

    [Tooltip("When enabled, origin movement on the world Z axis affects the object position. Usually keep this enabled for 3D background planets.")]
    [SerializeField] private bool useDepthParallax = true;

    private Vector3 startingWorldPosition;
    private Vector3 startingOriginPosition;

    private void Awake()
    {
        if (parallaxOrigin == null && Camera.main != null)
        {
            parallaxOrigin = Camera.main.transform;
        }

        startingWorldPosition = transform.position;

        if (parallaxOrigin != null)
        {
            startingOriginPosition = parallaxOrigin.position;
        }
    }

    private void LateUpdate()
    {
        if (parallaxOrigin == null)
        {
            return;
        }

        Vector3 originDelta = parallaxOrigin.position - startingOriginPosition;

        if (!useHorizontalParallax)
        {
            originDelta.x = 0f;
        }

        if (!useVerticalParallax)
        {
            originDelta.y = 0f;
        }

        if (!useDepthParallax)
        {
            originDelta.z = 0f;
        }

        transform.position = startingWorldPosition + originDelta * translationFollowAmount;
    }
}
