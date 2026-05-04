using System.IO;
using UnityEditor;
using UnityEngine;

public static class PulsarPrefabBuilder3D
{
    private const string ShaderDirectory = "Assets/Shaders/3d";
    private const string MaterialDirectory = "Assets/Materials/3d";
    private const string PrefabDirectory = "Assets/Prefabs/3d_effects";

    private const string CoreMaterialPath = MaterialDirectory + "/Pulsar_Core.mat";
    private const string NorthJetMaterialPath = MaterialDirectory + "/Pulsar_Jet_North.mat";
    private const string SouthJetMaterialPath = MaterialDirectory + "/Pulsar_Jet_South.mat";
    private const string JetNoisePath = MaterialDirectory + "/Pulsar_JetNoise.asset";
    private const string PrefabPath = PrefabDirectory + "/Pulsar3D.prefab";

    [MenuItem("Starfall/3D/Create Pulsar Prefab")]
    public static void CreatePulsarAssets()
    {
        EnsureDirectory(ShaderDirectory);
        EnsureDirectory(MaterialDirectory);
        EnsureDirectory(PrefabDirectory);

        AssetDatabase.Refresh();

        Texture2D noiseTexture = CreateOrUpdateNoiseTexture();
        Material coreMaterial = CreateOrUpdateCoreMaterial();
        Material northJetMaterial = CreateOrUpdateJetMaterial(NorthJetMaterialPath, noiseTexture, 1f);
        Material southJetMaterial = CreateOrUpdateJetMaterial(SouthJetMaterialPath, noiseTexture, -1f);

        GameObject root = new GameObject("Pulsar3D");
        try
        {
            PulsarVisual3D pulsarVisual = root.AddComponent<PulsarVisual3D>();

            const float coreRadius = 12f;
            const float jetLength = 120f;
            const float jetRadius = 8f;
            const float jetCenterOffset = coreRadius + jetLength * 0.5f;

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * coreRadius * 2f;
            RemoveCollider(core);
            MeshRenderer coreRenderer = core.GetComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = coreMaterial;
            ConfigureRenderer(coreRenderer);

            GameObject northJet = CreateJet("NorthJet", root.transform, new Vector3(0f, jetCenterOffset, 0f), jetRadius, jetLength, northJetMaterial);
            GameObject southJet = CreateJet("SouthJet", root.transform, new Vector3(0f, -jetCenterOffset, 0f), jetRadius, jetLength, southJetMaterial);

            SerializedObject serializedVisual = new SerializedObject(pulsarVisual);
            serializedVisual.FindProperty("rotationRoot").objectReferenceValue = root.transform;
            serializedVisual.FindProperty("localRotationAxis").vector3Value = Vector3.up;
            serializedVisual.FindProperty("rotationDegreesPerSecond").floatValue = 18f;
            serializedVisual.FindProperty("coreRenderer").objectReferenceValue = coreRenderer;
            serializedVisual.FindProperty("northJetRenderer").objectReferenceValue = northJet.GetComponent<MeshRenderer>();
            serializedVisual.FindProperty("southJetRenderer").objectReferenceValue = southJet.GetComponent<MeshRenderer>();
            serializedVisual.FindProperty("basePulseIntensity").floatValue = 1f;
            serializedVisual.FindProperty("helperPulseAmplitude").floatValue = 0.12f;
            serializedVisual.FindProperty("helperPulseFrequency").floatValue = 0.22f;
            serializedVisual.FindProperty("northJetTransform").objectReferenceValue = northJet.transform;
            serializedVisual.FindProperty("southJetTransform").objectReferenceValue = southJet.transform;
            serializedVisual.FindProperty("gameplayCoreRadius").floatValue = coreRadius;
            serializedVisual.FindProperty("gameplayJetLength").floatValue = jetLength;
            serializedVisual.FindProperty("gameplayJetRadius").floatValue = jetRadius;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Pulsar prefab created at {PrefabPath}");
    }

    private static Texture2D CreateOrUpdateNoiseTexture()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(JetNoisePath);
        if (texture == null)
        {
            texture = new Texture2D(128, 128, TextureFormat.RGBA32, false, true)
            {
                name = "Pulsar_JetNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            AssetDatabase.CreateAsset(texture, JetNoisePath);
        }

        const int size = 128;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float value = FractalNoise(u * 8f, v * 8f);
                value = Mathf.SmoothStep(0.2f, 0.95f, value);
                pixels[y * size + x] = new Color(value, value, value, value);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Material CreateOrUpdateCoreMaterial()
    {
        Shader shader = Shader.Find("Starfall/3D/Pulsar/CoreSurface");
        Material material = LoadOrCreateMaterial(CoreMaterialPath, shader);
        material.SetColor("_CoreColor", new Color(2.4f, 2.8f, 5.8f, 1f));
        material.SetColor("_HotBandColor", new Color(9.2f, 8.5f, 6.0f, 1f));
        material.SetColor("_MagneticRimColor", new Color(1.1f, 5.4f, 10f, 1f));
        material.SetFloat("_Brightness", 7.5f);
        material.SetFloat("_NoiseScale", 15f);
        material.SetFloat("_BandFrequency", 12f);
        material.SetFloat("_BandSharpness", 6f);
        material.SetFloat("_SurfaceFlowSpeed", 0.28f);
        material.SetFloat("_PulseStrength", 0.35f);
        material.SetFloat("_PulseSpeed", 0.7f);
        material.SetFloat("_RimPower", 2.8f);
        material.SetFloat("_RimStrength", 2.4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateJetMaterial(string materialPath, Texture2D noiseTexture, float outwardSign)
    {
        Shader shader = Shader.Find("Starfall/3D/Pulsar/Jet");
        Material material = LoadOrCreateMaterial(materialPath, shader);
        material.SetColor("_JetColor", new Color(0.45f, 3.0f, 10f, 1f));
        material.SetColor("_HotCoreColor", new Color(8.0f, 9.0f, 10f, 1f));
        material.SetFloat("_Brightness", 5.5f);
        material.SetFloat("_Alpha", 0.7f);
        material.SetTexture("_NoiseTex", noiseTexture);
        material.SetFloat("_TextureInfluence", 0.35f);
        material.SetFloat("_NoiseScale", 9f);
        material.SetFloat("_NoiseStrength", 1.25f);
        material.SetFloat("_ScrollSpeed", 1.8f);
        material.SetFloat("_OutwardSign", outwardSign);
        material.SetFloat("_FresnelPower", 2.25f);
        material.SetFloat("_FresnelStrength", 2.5f);
        material.SetFloat("_InnerFill", 0.12f);
        material.SetFloat("_LengthFadePower", 0.7f);
        material.SetFloat("_BaseFadeDistance", 0.03f);
        material.SetFloat("_TipFadeDistance", 0.18f);
        material.SetFloat("_VertexJitterStrength", 0.16f);
        material.SetFloat("_JitterScale", 6f);
        material.SetFloat("_JitterSpeed", 1.2f);
        material.SetFloat("_PulseStrength", 0.28f);
        material.SetFloat("_PulseSpeed", 0.7f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateJet(string name, Transform parent, Vector3 localPosition, float radius, float length, Material material)
    {
        GameObject jet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        jet.name = name;
        jet.transform.SetParent(parent, false);
        jet.transform.localPosition = localPosition;
        jet.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);

        MeshRenderer renderer = jet.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer);

        RemoveCollider(jet);
        CapsuleCollider trigger = jet.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.direction = 1;
        trigger.radius = 0.5f;
        trigger.height = 2f;

        return jet;
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        if (shader == null)
        {
            Debug.LogWarning($"Pulsar shader for {path} was not found. The material will stay on its current shader until Unity imports the shader asset.");
        }

        return material;
    }

    private static void ConfigureRenderer(MeshRenderer renderer)
    {
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static void EnsureDirectory(string assetDirectory)
    {
        string absolutePath = Path.Combine(Application.dataPath, assetDirectory.Substring("Assets/".Length));
        if (!Directory.Exists(absolutePath))
        {
            Directory.CreateDirectory(absolutePath);
        }
    }

    private static float FractalNoise(float x, float y)
    {
        float value = 0f;
        float amplitude = 0.5f;
        float frequency = 1f;

        for (int octave = 0; octave < 4; octave++)
        {
            value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            frequency *= 2f;
            amplitude *= 0.5f;
        }

        return Mathf.Clamp01(value);
    }
}
