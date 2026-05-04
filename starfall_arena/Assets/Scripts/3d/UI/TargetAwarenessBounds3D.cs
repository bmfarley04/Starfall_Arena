using UnityEngine;

[DisallowMultipleComponent]
public class TargetAwarenessBounds3D : MonoBehaviour
{
    [Header("Local Bounds Override")]
    [Tooltip("Local-space center of the target-awareness bracket bounds. Use this to align the HUD with the visible ship body when automatic renderer bounds are not enough.")]
    [SerializeField] private Vector3 localCenter = Vector3.zero;

    [Tooltip("Local-space size of the target-awareness bracket bounds. Keep this close to the visible ship silhouette, not weapon trails or temporary VFX.")]
    [SerializeField] private Vector3 localSize = Vector3.one;

    [Header("Gizmos")]
    [Tooltip("If enabled, selected Scene view gizmos show the authored local bounds box used by TargetAwarenessHUD3D.")]
    [SerializeField] private bool drawSelectedGizmo = true;

    [Tooltip("Color used for the selected Scene view bounds gizmo.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.25f, 0.15f, 0.8f);

    public Vector3 LocalCenter => localCenter;
    public Vector3 LocalSize => new Vector3(
        Mathf.Max(0.01f, Mathf.Abs(localSize.x)),
        Mathf.Max(0.01f, Mathf.Abs(localSize.y)),
        Mathf.Max(0.01f, Mathf.Abs(localSize.z)));

    public void GetWorldCorners(Vector3[] corners)
    {
        if (corners == null || corners.Length < 8)
        {
            return;
        }

        FillLocalBoxCorners(localCenter, LocalSize, transform, corners);
    }

    public static void FillLocalBoxCorners(Vector3 center, Vector3 size, Transform sourceTransform, Vector3[] corners)
    {
        if (corners == null || corners.Length < 8 || sourceTransform == null)
        {
            return;
        }

        Vector3 half = new Vector3(
            Mathf.Max(0.005f, Mathf.Abs(size.x) * 0.5f),
            Mathf.Max(0.005f, Mathf.Abs(size.y) * 0.5f),
            Mathf.Max(0.005f, Mathf.Abs(size.z) * 0.5f));

        corners[0] = sourceTransform.TransformPoint(center + new Vector3(-half.x, -half.y, -half.z));
        corners[1] = sourceTransform.TransformPoint(center + new Vector3(-half.x, -half.y, half.z));
        corners[2] = sourceTransform.TransformPoint(center + new Vector3(-half.x, half.y, -half.z));
        corners[3] = sourceTransform.TransformPoint(center + new Vector3(-half.x, half.y, half.z));
        corners[4] = sourceTransform.TransformPoint(center + new Vector3(half.x, -half.y, -half.z));
        corners[5] = sourceTransform.TransformPoint(center + new Vector3(half.x, -half.y, half.z));
        corners[6] = sourceTransform.TransformPoint(center + new Vector3(half.x, half.y, -half.z));
        corners[7] = sourceTransform.TransformPoint(center + new Vector3(half.x, half.y, half.z));
    }

    private void OnValidate()
    {
        localSize = LocalSize;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSelectedGizmo)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(localCenter, LocalSize);
        Gizmos.matrix = previousMatrix;
    }
}
