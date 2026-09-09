using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WardenAppearance))]
public sealed class WardenAppearanceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var appearance = (WardenAppearance)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview skins", EditorStyles.boldLabel);
        if (appearance.availableSkins != null)
            foreach (var skin in appearance.availableSkins)
                if (skin != null && GUILayout.Button(skin.displayName))
                {
                    Undo.RecordObject(appearance, "Change Warden skin");
                    appearance.SetSkin(skin);
                    EditorUtility.SetDirty(appearance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(appearance);
                }
        EditorGUILayout.LabelField("Preview eye shapes", EditorStyles.boldLabel);
        if (appearance.availableEyes != null)
            foreach (var eyes in appearance.availableEyes)
                if (eyes != null && GUILayout.Button(eyes.styleId))
                {
                    Undo.RecordObject(appearance, "Change Warden eyes");
                    appearance.SetEyes(eyes);
                    EditorUtility.SetDirty(appearance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(appearance);
                }
    }
}
