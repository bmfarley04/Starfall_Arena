using UnityEditor;

[CustomEditor(typeof(GuidedMissileWeapon3D))]
public class GuidedMissileWeapon3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MissileWeaponInspectorUtility.DrawWithoutMuzzleEffects(serializedObject);
    }
}

[CustomEditor(typeof(MissileWeaponEnemy3D), true)]
public class MissileWeaponEnemy3DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MissileWeaponInspectorUtility.DrawWithoutMuzzleEffects(serializedObject);
    }
}

internal static class MissileWeaponInspectorUtility
{
    private static readonly string[] HiddenMuzzleEffectProperties =
    {
        "m_Script",
        "muzzleEffectPrefab",
        "muzzleEffectLifetime",
        "parentMuzzleEffectToMuzzle",
        "muzzleEffectLocalOffset",
        "muzzleEffectPrewarmCount"
    };

    public static void DrawWithoutMuzzleEffects(SerializedObject serializedObject)
    {
        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (ShouldHide(property.name))
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, includeChildren: true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool ShouldHide(string propertyName)
    {
        for (int i = 0; i < HiddenMuzzleEffectProperties.Length; i++)
        {
            if (HiddenMuzzleEffectProperties[i] == propertyName)
            {
                return true;
            }
        }

        return false;
    }
}
