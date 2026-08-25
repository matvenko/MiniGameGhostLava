using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // The profile is edited in the map window, not in the inspector: a raw
    // inspector on it is a wall of sliders with no preview and no way to tell
    // what any of them do. So the inspector is a summary plus the way in.
    [CustomEditor(typeof(GhmMapProfile))]
    public class GhmMapProfileInspector : UnityEditor.Editor
    {
        private bool _showRaw;

        public override void OnInspectorGUI()
        {
            var profile = (GhmMapProfile)target;

            EditorGUILayout.LabelField("Ghost Hunter Maps profile", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{profile.width} x {profile.height} cells  ·  {profile.layers.Count} layers  ·  {profile.bands.Count} bands  ·  {profile.catalog.Count} textures", GhmSkin.Sub);
            EditorGUILayout.LabelField($"camera {profile.CameraPitch:0.#}° pitch at compression {profile.compression:0.00}", GhmSkin.Sub);

            GUILayout.Space(8f);
            if (GhmSkin.AccentButton("Open in Ghost Hunter Maps", "Edit this profile in the map editor", 28f))
            {
                GhmWindow.Open();
                Selection.activeObject = profile;
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Publish level 1", "Write this map into the target scene")))
                {
                    var report = GhmPublisher.Publish(profile, 1);
                    EditorUtility.DisplayDialog("Publish report", report.Summary(), "OK");
                }

                if (GUILayout.Button(new GUIContent("Save Resources copy", "So a build can load it without a scene reference")))
                    GhmPublisher.SaveResourcesCopy(profile);
            }

            GUILayout.Space(10f);
            _showRaw = EditorGUILayout.Foldout(_showRaw, "Raw fields", true);
            if (!_showRaw) return;

            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(GhmRuntimeBinder))]
    public class GhmRuntimeBinderInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var binder = (GhmRuntimeBinder)target;
            GUILayout.Space(8f);

            var board = binder.CurrentBoard;
            if (board != null)
                EditorGUILayout.LabelField($"level {board.level}  ·  {board.GroundCount} floor cells  ·  {board.decor.Count} decor  ·  {board.pathRoute.Count} path cells", GhmSkin.Sub);

            EditorGUILayout.LabelField("Outside play mode this only maintains its own paths and decor. Scene state - tiles, materials, camera - is written by publishing.", GhmSkin.Sub);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Rebuild", "Regenerate the paths and decor"))) binder.Rebuild();
                if (GUILayout.Button(new GUIContent("Apply to scene", "Also re-lay the tiles and re-aim the camera, as publishing does")))
                    binder.RebuildWithSceneState();
                if (GUILayout.Button("Clear generated")) binder.Clear();
                if (GUILayout.Button("Open editor"))
                {
                    GhmWindow.Open();
                    if (binder.Profile != null) Selection.activeObject = binder.Profile;
                }
            }
        }
    }
}
