using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ProceduralCone : MonoBehaviour
{
    public float height = 2f;
    public float radius = 1f;
    public int subdivisions = 32;

    void Start()
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[subdivisions + 1];
        int[] triangles = new int[subdivisions * 3];

        vertices[0] = new Vector3(0, height, 0); // Tip

        for (int i = 0; i < subdivisions; i++)
        {
            float angle = (float)i / subdivisions * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        for (int i = 0; i < subdivisions; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1 == subdivisions) ? 1 : i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}