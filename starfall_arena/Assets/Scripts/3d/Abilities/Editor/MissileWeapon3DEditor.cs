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
        Editor.DrawPropertiesExcluding(serializedObject, HiddenMuzzleEffectProperties);
        serializedObject.ApplyModifiedProperties();
    }
}
