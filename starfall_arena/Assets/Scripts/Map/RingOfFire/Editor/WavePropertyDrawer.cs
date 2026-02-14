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
        SerializedProperty shapeType = property.FindPropertyRelative("shapeType");
        SerializedProperty endCenterBox = property.FindPropertyRelative("endCenterBox");
        SerializedProperty safeBox = property.FindPropertyRelative("safeBox");
        SerializedProperty endCenterCircle = property.FindPropertyRelative("endCenterCircle");
        SerializedProperty safeCircle = property.FindPropertyRelative("safeCircle");

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
        
        EditorGUI.PropertyField(fieldRect, shapeType);
        fieldRect.y += EditorGUIUtility.singleLineHeight + 2;

        // When stationaryBox is true, the safe zone stays at the previous wave's end position
        // So we don't need to show safeBox or endCenterBox since they're not used
        if (!stationaryBox.boolValue)
        {
            WaveShapeType currentShapeType = (WaveShapeType)shapeType.enumValueIndex;
            
            if (currentShapeType == WaveShapeType.Box)
            {
                // Draw Boxes with conditional logic
                DrawWaveBox(ref fieldRect, safeBox, "Safe Box", autoChain.boolValue);
                
                // Only draw End Center Box if not chained
                if (!autoChain.boolValue)
                {
                    DrawWaveBox(ref fieldRect, endCenterBox, "End Center Box", autoChain.boolValue);
                }
            }
            else // Circle
            {
                // Draw Circles with conditional logic
                DrawWaveCircle(ref fieldRect, safeCircle, "Safe Circle", autoChain.boolValue);
                
                // Only draw End Center Circle if not chained
                if (!autoChain.boolValue)
                {
                    DrawWaveCircle(ref fieldRect, endCenterCircle, "End Center Circle", autoChain.boolValue);
                }
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
    
    private void DrawWaveCircle(ref Rect rect, SerializedProperty circle, string label, bool isChained)
    {
        EditorGUI.LabelField(rect, label, EditorStyles.boldLabel);
        rect.y += EditorGUIUtility.singleLineHeight;

        SerializedProperty center = circle.FindPropertyRelative("centerPoint");
        SerializedProperty radius = circle.FindPropertyRelative("radius");

        // Hide centerPoint if autoChain is true
        if (!isChained)
        {
            EditorGUI.PropertyField(rect, center);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
        }

        EditorGUI.PropertyField(rect, radius);
        rect.y += EditorGUIUtility.singleLineHeight + 5; // Extra spacing
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool autoChain = property.FindPropertyRelative("autoChainWithPrevious").boolValue;
        bool stationary = property.FindPropertyRelative("stationaryBox").boolValue;
        WaveShapeType shapeType = (WaveShapeType)property.FindPropertyRelative("shapeType").enumValueIndex;

        // Base height for the top variables (autoChain, duration, stationaryBox, fireDamage, damageTickInterval, shapeType)
        float totalHeight = (EditorGUIUtility.singleLineHeight + 2) * 6;

        if (stationary)
        {
            // Add height for the help box when stationary
            totalHeight += EditorGUIUtility.singleLineHeight * 2 + 5;
        }
        else
        {
            if (shapeType == WaveShapeType.Box)
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
            else // Circle
            {
                // Add height for Safe Circle (Label + Radius)
                totalHeight += (EditorGUIUtility.singleLineHeight + 2) * 2;

                // If not chained, add height for Safe Circle centerPoint
                if (!autoChain)
                {
                    totalHeight += (EditorGUIUtility.singleLineHeight + 2);
                }

                // Only add End Center Circle height if not chained
                if (!autoChain)
                {
                    // End Center Circle (Label + CenterPoint + Radius)
                    totalHeight += (EditorGUIUtility.singleLineHeight + 2) * 3;
                }
            }
        }

        return totalHeight + 10;
    }
}