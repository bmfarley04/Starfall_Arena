#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RingOfFireManager))]
public class RingOfFireManagerEditor : Editor
{
    private void OnSceneGUI()
    {
        RingOfFireManager ringOfFireManager = (RingOfFireManager)target;

        if (ringOfFireManager.config.waves == null || ringOfFireManager.config.waves.Count == 0)
            return;

        // Draw each wave's shapes
        for (int i = 0; i < ringOfFireManager.config.waves.Count; i++)
        {
            Wave wave = ringOfFireManager.config.waves[i];
            if (wave == null)
                continue;

            if (wave.shapeType == WaveShapeType.Box)
            {
                // Draw endCenterBox
                if (wave.endCenterBox != null)
                {
                    DrawWaveBox(wave.endCenterBox, Color.red, $"Wave {i + 1} - End Center");
                }

                // Draw safeBox
                if (wave.safeBox != null)
                {
                    DrawWaveBox(wave.safeBox, Color.green, $"Wave {i + 1} - Safe Zone");
                }
            }
            else // Circle
            {
                // Draw endCenterCircle
                if (wave.endCenterCircle != null)
                {
                    DrawWaveCircle(wave.endCenterCircle, Color.red, $"Wave {i + 1} - End Center");
                }

                // Draw safeCircle
                if (wave.safeCircle != null)
                {
                    DrawWaveCircle(wave.safeCircle, Color.green, $"Wave {i + 1} - Safe Zone");
                }
            }
        }
    }

    private void DrawWaveBox(WaveBox box, Color color, string label)
    {
        if (box == null)
            return;

        Handles.color = color;

        // Calculate the four corners of the box
        float halfWidth = box.width / 2f;
        float halfLength = box.length / 2f;

        Vector3 center = new Vector3(box.centerPoint.x, box.centerPoint.y, 0f);
        Vector3 topLeft = center + new Vector3(-halfWidth, halfLength, 0f);
        Vector3 topRight = center + new Vector3(halfWidth, halfLength, 0f);
        Vector3 bottomRight = center + new Vector3(halfWidth, -halfLength, 0f);
        Vector3 bottomLeft = center + new Vector3(-halfWidth, -halfLength, 0f);

        // Draw the box outline
        Handles.DrawLine(topLeft, topRight);
        Handles.DrawLine(topRight, bottomRight);
        Handles.DrawLine(bottomRight, bottomLeft);
        Handles.DrawLine(bottomLeft, topLeft);

        // Draw an X through the center for visibility
        Handles.DrawLine(topLeft, bottomRight);
        Handles.DrawLine(topRight, bottomLeft);

        // Draw a semi-transparent filled rectangle
        Color fillColor = color;
        fillColor.a = 0.1f;
        Handles.DrawSolidRectangleWithOutline(
            new Vector3[] { topLeft, topRight, bottomRight, bottomLeft },
            fillColor,
            color
        );

        // Draw label at the center
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = color;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        Handles.Label(center + Vector3.up * (halfLength + 0.5f), label, labelStyle);
    }
    
    private void DrawWaveCircle(WaveCircle circle, Color color, string label)
    {
        if (circle == null)
            return;

        Handles.color = color;

        Vector3 center = new Vector3(circle.centerPoint.x, circle.centerPoint.y, 0f);

        // Draw the circle outline
        Handles.DrawWireDisc(center, Vector3.forward, circle.radius);

        // Draw cross through center for visibility
        Handles.DrawLine(center + Vector3.left * circle.radius * 0.3f, center + Vector3.right * circle.radius * 0.3f);
        Handles.DrawLine(center + Vector3.up * circle.radius * 0.3f, center + Vector3.down * circle.radius * 0.3f);

        // Draw a semi-transparent filled disc
        Color fillColor = color;
        fillColor.a = 0.1f;
        Handles.color = fillColor;
        Handles.DrawSolidDisc(center, Vector3.forward, circle.radius);

        // Reset color for outline
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.forward, circle.radius);

        // Draw label at the center
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = color;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;
        
        Handles.Label(center + Vector3.up * (circle.radius + 0.5f), label, labelStyle);
    }
}
#endif
