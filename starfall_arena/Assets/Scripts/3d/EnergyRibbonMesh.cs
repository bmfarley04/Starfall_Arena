using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class EnergyRibbonMesh : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [SerializeField] private int segments = 16;
    [SerializeField] private float width = 1.0f;
    [SerializeField] private float middleWidthMultiplier = 1.8f;
    [SerializeField] private float curveAmount = 0.4f;
    [SerializeField] private Vector3 curveDirection = Vector3.up;

    private Mesh mesh;

    private void Awake()
    {
        mesh = new Mesh();
        mesh.name = "Energy Ribbon Mesh";
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void LateUpdate()
    {
        if (startPoint == null || endPoint == null)
            return;

        BuildMesh();
    }

    private void BuildMesh()
    {
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        Vector3 start = transform.InverseTransformPoint(startPoint.position);
        Vector3 end = transform.InverseTransformPoint(endPoint.position);

        Vector3 forward = (end - start).normalized;
        Vector3 side = Vector3.Cross(forward, curveDirection).normalized;

        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;

            Vector3 center = Vector3.Lerp(start, end, t);

            float curve = Mathf.Sin(t * Mathf.PI) * curveAmount;
            center += transform.InverseTransformDirection(curveDirection.normalized) * curve;

            float widthFalloff = Mathf.Sin(t * Mathf.PI);
            float currentWidth = width * Mathf.Lerp(1.0f, middleWidthMultiplier, widthFalloff);

            int vertexIndex = i * 2;

            vertices[vertexIndex] = center - side * currentWidth * 0.5f;
            vertices[vertexIndex + 1] = center + side * currentWidth * 0.5f;

            uvs[vertexIndex] = new Vector2(0.0f, t);
            uvs[vertexIndex + 1] = new Vector2(1.0f, t);

            if (i < segments)
            {
                int triangleIndex = i * 6;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 2;
                triangles[triangleIndex + 2] = vertexIndex + 1;

                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}