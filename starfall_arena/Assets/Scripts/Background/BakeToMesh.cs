using UnityEngine;

public class ParticleBaker : MonoBehaviour
{
    [ContextMenu("Bake Stars to Mesh")]
    public void BakeToMesh()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        ParticleSystemRenderer psr = GetComponent<ParticleSystemRenderer>();
        Mesh mesh = new Mesh();
        
        psr.BakeMesh(mesh);

        // This creates a new object in the hierarchy so you can see the result immediately
        GameObject bakedObject = new GameObject("Baked_Star_Layer");
        bakedObject.AddComponent<MeshFilter>().mesh = mesh;
        bakedObject.AddComponent<MeshRenderer>().material = psr.sharedMaterial;
        
        Debug.Log("Stars baked to new GameObject!");

        // This requires 'using UnityEditor;' at the top
        // Note: This only works in the Editor, not in a build.
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.CreateAsset(mesh, "Assets/close_StarLayer_Baked.asset");
        UnityEditor.AssetDatabase.SaveAssets();
        #endif
    }
}