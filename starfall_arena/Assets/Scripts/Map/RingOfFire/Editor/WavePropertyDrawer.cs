using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(Wave))]
public class WavePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Find our properties
        SerializedProperty duration = property.FindPropertyRelative("duration");
        SerializedProperty stationaryBox = property.FindPropertyRelative("stationaryBox");
        SerializedProperty fireDamage = property.FindPropertyRelative("fireDamage");
        SerializedProperty damageTickInterval = property.FindPropertyRelative("damageTickInterval");
        SerializedProperty autoChain = property.FindPropertyRelative("autoChainWithPrevious");
        SerializedProperty endCenterBox = property.FindPropertyRelative("endCenterBox");
        SerializedProperty safeBox = property.FindPropertyRelative("safeBox");

        // Draw basic fields
        Rect fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(fieldRect, autoChain);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(fieldRect, duration);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(fieldRect, stationaryBox);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(fieldRect, fireDamage);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(fieldRect, damageTickInterval);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        // When stationaryBox is true, the safe zone stays at the previous wave's end position
        // So we don't need to show safeBox or endCenterBox since they're not used
        if (!stationaryBox.boolValue)
        {
            // Draw Boxes with conditional logic
            DrawWaveBox(ref fieldRect, safeBox, "Safe Box", autoChain.boolValue);
            
            // Only draw End Center Box if not chained
            if (!autoChain.boolValue)
            {
                DrawWaveBox(ref fieldRect, endCenterBox, "End Center Box", autoChain.boolValue);
            }
        }
        else
        {
            // Show a helpful note when stationary
            EditorGUI.HelpBox(fieldRect, "Stationary mode: Safe zone remains at previous wave's position for this wave's duration.", MessageType.Info);
            fieldRect.y += EditorGUIUtility.singleLineHeight * 2 + 5;
        }

        EditorGUI.EndProperty();
    }

    private void DrawWaveBox(ref Rect rect, SerializedProperty box, string label, bool isChained)
    {
        EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
        rect.y += EditorGUIUtility.singleLineHeight;

        SerializedProperty center = box.FindPropertyRelative("centerPoint");
        SerializedProperty width = box.FindPropertyRelative("width");
        SerializedProperty length = box.FindPropertyRelative("length");

        // Hide centerPoint if autoChain is true
        if (!isChained)
        {
            EditorGUI.PropertyField(rect, center);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
        }

        EditorGUI.PropertyField(rect, width);
        rect.y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.PropertyField(rect, length);
        rect.y += EditorGUIUtility.singleLineHeight + 5; // Extra spacing
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool autoChain = property.FindPropertyRelative("autoChainWithPrevious").boolValue;
        bool stationary = property.FindPropertyRelative("stationaryBox").boolValue;

        // Base height for the top variables (autoChain, duration, stationaryBox, fireDamage, damageTickInterval)
        float totalHeight = (EditorGUIUtility.singleLineHeight + 2) * 5;

        if (stationary)
        {
            // Add height for the help box when stationary
            totalHeight += EditorGUIUtility.singleLineHeight * 2 + 5;
        }
        else
        {
            // Add height for Safe Box (Label + Width + Length)
            totalHeight += (EditorGUIUtility.singleLineHeight + 2) * 3;

            // If not chained, add height for Safe Box centerPoint
            if (!autoChain)
            {
                totalHeight += (EditorGUIUtility.singleLineHeight + 2);
            }

            // Only add End Center Box height if not chained
            if (!autoChain)
            {
                // End Center Box (Label + CenterPoint + Width + Length)
                totalHeight += (EditorGUIUtility.singleLineHeight + 2) * 4;
            }
        }

        return totalHeight + 10;
    }
}