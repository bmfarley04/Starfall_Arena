using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Player), true)]
public class PlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Player player = (Player)target;

        // Draw the script reference and the 4 slots
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Active Ability Components", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Manage your 4 active slots below.", MessageType.None);

        if (player.abilitySlots == null) return;

        for (int i = 0; i < player.abilitySlots.Length; i++)
        {
            MonoScript script = player.abilitySlots[i];

            if (script == null)
            {
                EditorGUILayout.HelpBox($"Slot {i + 1}: Empty", MessageType.None);
                continue;
            }

            System.Type scriptType = script.GetClass();

            // Safety check: Ensure the script is a MonoBehaviour
            if (scriptType == null || !scriptType.IsSubclassOf(typeof(MonoBehaviour)))
            {
                EditorGUILayout.HelpBox($"Slot {i + 1}: {script.name} is not a MonoBehaviour.", MessageType.Warning);
                continue;
            }

            // Draw a nice box for each ability
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            Component existingComp = player.GetComponent(scriptType);

            if (existingComp == null)
            {
                EditorGUILayout.LabelField($"Slot {i + 1}: {script.name} (Not Attached)");
                if (GUILayout.Button("Attach to Player"))
                {
                    Undo.AddComponent(player.gameObject, scriptType);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"Slot {i + 1}: {script.name}", EditorStyles.miniBoldLabel);

                // Embed the actual component editor
                Editor editor = CreateEditor(existingComp);
                editor.OnInspectorGUI();

                if (GUILayout.Button("Detach Component", GUILayout.Width(130)))
                {
                    Undo.DestroyObjectImmediate(existingComp);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}