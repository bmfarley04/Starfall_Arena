#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MapManagerScript))]
public class MapManagerEditor : Editor
{
    private void OnSceneGUI()
    {
        MapManagerScript mapManager = (MapManagerScript)target;

        if (mapManager.ringOfFire.waves == null || mapManager.ringOfFire.waves.Count == 0)
            return;

        // Draw each wave's boxes
        for (int i = 0; i < mapManager.ringOfFire.waves.Count; i++)
        {
            Wave wave = mapManager.ringOfFire.waves[i];
            if (wave == null)
                continue;

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
}
#endif
