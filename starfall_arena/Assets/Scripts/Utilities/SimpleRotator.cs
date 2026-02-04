using UnityEngine;

/// <summary>
/// Simple multipurpose rotation script. Enable rotation on any combination of axes
/// with independent speeds. Useful for asteroids (3D tumbling) and lens flare effects (2D spin).
/// </summary>
public class SimpleRotator : MonoBehaviour
{
    [Header("X Axis")]
    [SerializeField] private bool rotateX;
    [SerializeField] private float xSpeed = 10f;

    [Header("Y Axis")]
    [SerializeField] private bool rotateY;
    [SerializeField] private float ySpeed = 10f;

    [Header("Z Axis")]
    [SerializeField] private bool rotateZ;
    [SerializeField] private float zSpeed = 10f;

    [Header("Settings")]
    [Tooltip("Use unscaled time (ignores Time.timeScale)")]
    [SerializeField] private bool useUnscaledTime;

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        Vector3 rotation = Vector3.zero;

        if (rotateX) rotation.x = xSpeed * deltaTime;
        if (rotateY) rotation.y = ySpeed * deltaTime;
        if (rotateZ) rotation.z = zSpeed * deltaTime;

        transform.Rotate(rotation, Space.Self);
    }
}
