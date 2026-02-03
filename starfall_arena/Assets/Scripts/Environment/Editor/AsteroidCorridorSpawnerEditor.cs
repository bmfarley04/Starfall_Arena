#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AsteroidCorridorSpawner))]
public class AsteroidCorridorSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        AsteroidCorridorSpawner spawner = (AsteroidCorridorSpawner)target;

        EditorGUI.BeginChangeCheck();

        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            // Changes detected - regeneration happens via OnValidate
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Regenerate Asteroids", GUILayout.Height(30)))
        {
            spawner.RegenerateAsteroids();
        }

        if (GUILayout.Button("Randomize Seed", GUILayout.Height(30)))
        {
            Undo.RecordObject(spawner, "Randomize Seed");
            SerializedProperty seedProp = serializedObject.FindProperty("seed");
            seedProp.intValue = Random.Range(0, 999999);
            serializedObject.ApplyModifiedProperties();
            spawner.RegenerateAsteroids();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Pattern: Belt Arc\n\n" +
            "• Asteroids form a curved belt in 3D space\n" +
            "• Camera views a portion of this belt\n" +
            "• Arc angles control which section is visible\n" +
            "• Perspective camera naturally handles depth sizing\n\n" +
            "Tip: Position spawner and adjust Belt Center so the arc\n" +
            "curves around/in front of your camera view.\n\n" +
            "Once satisfied, unparent the 'SpawnedAsteroids' child object.",
            MessageType.Info
        );
    }
}
#endif
