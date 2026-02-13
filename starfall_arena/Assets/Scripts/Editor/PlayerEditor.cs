using UnityEngine;
using UnityEditor;
// Heavily written with AI
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
                    Component newComp = Undo.AddComponent(player.gameObject, scriptType);
                    newComp.hideFlags = HideFlags.HideInInspector;
                    SetAbilityField(player, i, newComp as Ability);
                    EditorUtility.SetDirty(player);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"Slot {i + 1}: {script.name}", EditorStyles.miniBoldLabel);

                // Ensure the ability field is set
                if (!IsAbilityFieldSet(player, i, existingComp as Ability))
                {
                    SetAbilityField(player, i, existingComp as Ability);
                    EditorUtility.SetDirty(player);
                }

                // Embed the actual component editor
                Editor editor = CreateEditor(existingComp);
                editor.OnInspectorGUI();

                if (GUILayout.Button("Detach Component", GUILayout.Width(130)))
                {
                    SetAbilityField(player, i, null);
                    Undo.DestroyObjectImmediate(existingComp);
                    EditorUtility.SetDirty(player);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }

    private void SetAbilityField(Player player, int slotIndex, Ability ability)
    {
        switch (slotIndex)
        {
            case 0:
                player.ability1 = ability;
                break;
            case 1:
                player.ability2 = ability;
                break;
            case 2:
                player.ability3 = ability;
                break;
            case 3:
                player.ability4 = ability;
                break;
        }
    }

    private bool IsAbilityFieldSet(Player player, int slotIndex, Ability ability)
    {
        switch (slotIndex)
        {
            case 0:
                return player.ability1 == ability;
            case 1:
                return player.ability2 == ability;
            case 2:
                return player.ability3 == ability;
            case 3:
                return player.ability4 == ability;
            default:
                return false;
        }
    }
}