using UnityEngine;

/// <summary>
/// Handles parallax scrolling for a single background layer.
/// Uses FixedUpdate to match physics timing and prevent jitter.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax Settings")]
    [Tooltip("0 = static (doesn't move), 1 = moves with camera. Use 0.1-0.3 for distant layers")]
    [SerializeField] private float parallaxFactor = 0.2f;

    [Tooltip("Reference to main camera. Will auto-find if not set")]
    [SerializeField] private Camera mainCamera;

    [Header("Infinite Scrolling (Optional)")]
    [Tooltip("Enable infinite horizontal wrapping for this layer")]
    [SerializeField] private bool enableWrapping = false;

    private Vector3 previousCameraPosition;
    private float spriteWidth;

    void Start()
    {
        // Auto-find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Store initial camera position
        if (mainCamera != null)
        {
            previousCameraPosition = mainCamera.transform.position;
        }

        // Calculate sprite width for wrapping (if applicable)
        if (enableWrapping)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                spriteWidth = sr.bounds.size.x;
            }
        }
    }

    void FixedUpdate()
    {
        if (mainCamera == null) return;

        // Calculate camera movement delta
        Vector3 cameraDelta = mainCamera.transform.position - previousCameraPosition;

        // Apply parallax movement based on delta
        transform.position += new Vector3(
            cameraDelta.x * parallaxFactor,
            cameraDelta.y * parallaxFactor,
            0f
        );

        // Update previous camera position
        previousCameraPosition = mainCamera.transform.position;

        // Optional: Infinite wrapping for horizontal scrolling
        if (enableWrapping && spriteWidth > 0)
        {
            float distanceFromCamera = transform.position.x - mainCamera.transform.position.x;

            if (distanceFromCamera > spriteWidth)
            {
                transform.position = new Vector3(
                    transform.position.x - spriteWidth * 2f,
                    transform.position.y,
                    transform.position.z
                );
            }
            else if (distanceFromCamera < -spriteWidth)
            {
                transform.position = new Vector3(
                    transform.position.x + spriteWidth * 2f,
                    transform.position.y,
                    transform.position.z
                );
            }
        }
    }
}