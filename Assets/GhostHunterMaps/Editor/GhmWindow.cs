using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Ghost Hunter Maps - the map editor.
    //
    // Four docked panels around a live preview, in the arrangement that was
    // asked for: global settings top-left, the texture catalogue below them,
    // the layer stack top-right, the selected layer's inspector below that.
    //
    // The window owns no map data of its own. Everything lives in the profile
    // asset, the board is regenerated from it on demand, and publishing writes
    // that same profile into the game scene - so there is exactly one source of
    // truth and no way for the preview and the game to disagree.
    public partial class GhmWindow : EditorWindow
    {
        private const string LastProfileKey = "GhostHunterMaps.LastProfile";

        private GhmMapProfile _profile;
        private GhmBoard _board;
        private GhmPreview _preview;

        private int _level = 1;
        private int _layerIndex;
        private int _ruleIndex = -1;
        private int _textureIndex = -1;

        private Vector2 _globalScroll, _catalogScroll, _layersScroll, _inspectorScroll;
        private float _leftWidth = 330f;
        private float _rightWidth = 340f;
        private float _leftSplit = 0.55f;
        private float _rightSplit = 0.38f;
        private bool _dragLeft, _dragRight, _dragLeftRow, _dragRightRow;

        private bool _autoRegenerate = true;
        private int _boardVersion;
        private string _status = "";
        private double _lastGenerateMs;

        [MenuItem("Window/Ghost Hunter Maps %#m")]
        public static void Open()
        {
            var window = GetWindow<GhmWindow>();
            window.titleContent = new GUIContent("Ghost Hunter Maps");
            window.minSize = new Vector2(1040f, 620f);
            window.Show();
        }

        [MenuItem("Assets/Create/Ghost Hunter Maps/Map Profile (with defaults)", priority = 0)]
        public static void CreateProfileAsset()
        {
            var profile = CreateInstance<GhmMapProfile>();
            profile.EnsureDefaults();

            string folder = GhmTextureTools.EnsureFolder("Assets/GhostHunterMaps/Profiles");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/GhostMapProfile.asset");
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        void OnEnable()
        {
            _preview = new GhmPreview();

            if (_profile == null)
            {
                string path = EditorPrefs.GetString(LastProfileKey, "");
                if (!string.IsNullOrEmpty(path)) _profile = AssetDatabase.LoadAssetAtPath<GhmMapProfile>(path);
            }

            Regenerate();
        }

        void OnDisable()
        {
            _preview?.Dispose();
            _preview = null;
        }

        void OnGUI()
        {
            DrawToolbar();

            if (_profile == null)
            {
                DrawEmptyState();
                return;
            }

            float top = EditorStyles.toolbar.fixedHeight;
            var body = new Rect(0f, top, position.width, position.height - top);

            _leftWidth = Mathf.Clamp(_leftWidth, 240f, Mathf.Max(260f, body.width * 0.4f));
            _rightWidth = Mathf.Clamp(_rightWidth, 250f, Mathf.Max(270f, body.width * 0.4f));

            float centreX = body.x + _leftWidth + 4f;
            float centreWidth = Mathf.Max(120f, body.width - _leftWidth - _rightWidth - 8f);

            var leftColumn = new Rect(body.x, body.y, _leftWidth, body.height);
            var centreColumn = new Rect(centreX, body.y, centreWidth, body.height);
            var rightColumn = new Rect(centreX + centreWidth + 4f, body.y, _rightWidth, body.height);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(_profile, "Ghost Hunter Maps");

            DrawLeftColumn(leftColumn);
            DrawCentre(centreColumn);
            DrawRightColumn(rightColumn);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_profile);
                if (_autoRegenerate) Regenerate();
                else _preview?.Invalidate();
            }

            DrawSplitters(body, centreX, centreWidth);
        }

        // ------------------------------------------------------------------
        // Chrome
        // ------------------------------------------------------------------

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            var picked = (GhmMapProfile)EditorGUILayout.ObjectField(_profile, typeof(GhmMapProfile), false, GUILayout.Width(190f));
            if (EditorGUI.EndChangeCheck()) SetProfile(picked);

            if (GhmSkin.ToolbarButton("New", "Create a new map profile asset", 42f))
            {
                CreateProfileAsset();
                SetProfile(Selection.activeObject as GhmMapProfile);
            }

            using (new EditorGUI.DisabledScope(_profile == null))
            {
                if (GhmSkin.ToolbarButton("Save", "Write the profile asset to disk", 46f))
                {
                    EditorUtility.SetDirty(_profile);
                    AssetDatabase.SaveAssets();
                    _status = "Profile saved.";
                }

                GUILayout.Space(12f);
                GUILayout.Label("Level", EditorStyles.miniLabel, GUILayout.Width(36f));

                if (GhmSkin.ToolbarButton("◀", "Previous level", 24f)) SetLevel(_level - 1);
                int typed = EditorGUILayout.IntField(_level, EditorStyles.toolbarTextField, GUILayout.Width(38f));
                if (typed != _level) SetLevel(typed);
                if (GhmSkin.ToolbarButton("▶", "Next level", 24f)) SetLevel(_level + 1);

                if (GhmSkin.ToolbarButton("Generate next level", "Advance one level and roll its layout, decor and paths", 130f))
                    SetLevel(_level + 1);

                GUILayout.Space(12f);
                GUILayout.Label("Seed", EditorStyles.miniLabel, GUILayout.Width(32f));
                EditorGUI.BeginChangeCheck();
                int seed = EditorGUILayout.IntField(_profile != null ? _profile.seed : 0, EditorStyles.toolbarTextField, GUILayout.Width(80f));
                if (EditorGUI.EndChangeCheck() && _profile != null)
                {
                    _profile.seed = seed;
                    EditorUtility.SetDirty(_profile);
                    Regenerate();
                }
                if (GhmSkin.ToolbarButton("↻", "Roll a new seed", 24f) && _profile != null)
                {
                    _profile.seed = Random.Range(1, int.MaxValue);
                    EditorUtility.SetDirty(_profile);
                    Regenerate();
                }
            }

            GUILayout.FlexibleSpace();

            _autoRegenerate = GUILayout.Toggle(_autoRegenerate, new GUIContent("Auto", "Regenerate the board whenever a setting changes"), EditorStyles.toolbarButton, GUILayout.Width(44f));
            if (GhmSkin.ToolbarButton("Generate grid", "Rebuild the board from the current settings", 92f)) Regenerate();

            using (new EditorGUI.DisabledScope(_profile == null))
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = GhmSkin.Accent;
                if (GUILayout.Button(new GUIContent("Publish to game", "Write this map into the game scene"), EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    Publish();
                GUI.backgroundColor = previous;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawEmptyState()
        {
            var rect = new Rect(0f, EditorStyles.toolbar.fixedHeight, position.width, position.height - EditorStyles.toolbar.fixedHeight);
            GhmSkin.Fill(rect, GhmSkin.PanelBack);

            GUILayout.BeginArea(new Rect(rect.x + rect.width * 0.5f - 200f, rect.y + 80f, 400f, 260f));
            GUILayout.Label("Ghost Hunter Maps", EditorStyles.largeLabel);
            GUILayout.Label("A map profile holds the board size, the camera framing, the layer stack, "
                          + "the texture catalogue and the per-level bands.\n\n"
                          + "The best way in is to read the board that is already in the open scene: "
                          + "every setting then starts at the value the game is actually using, and "
                          + "each change from there is a deliberate step away from a known-good board.", GhmSkin.Sub);
            GUILayout.Space(14f);

            if (GhmSkin.AccentButton("Read the open scene into a profile", "Measures the board already in the scene - size, heights, materials, camera - so publishing straight away would change nothing", 28f))
            {
                var fromScene = GhmBootstrap.CreateFromScene(out string message);
                if (fromScene != null) SetProfile(fromScene);
                _status = message;
                EditorUtility.DisplayDialog("Ghost Hunter Maps", message, "OK");
            }

            GUILayout.Space(6f);
            if (GUILayout.Button(new GUIContent("Create an empty profile", "Starts from the tool's own defaults instead"), GUILayout.Height(24f)))
            {
                CreateProfileAsset();
                SetProfile(Selection.activeObject as GhmMapProfile);
            }
            GUILayout.EndArea();
        }

        private void DrawLeftColumn(Rect column)
        {
            float topHeight = Mathf.Round(column.height * _leftSplit) - 2f;
            var top = new Rect(column.x, column.y, column.width, topHeight);
            var bottom = new Rect(column.x, column.y + topHeight + 4f, column.width, column.height - topHeight - 4f);

            GhmSkin.Fill(top, GhmSkin.PanelBack);
            GUILayout.BeginArea(top);
            DrawGlobalPanel(top);
            GUILayout.EndArea();

            GhmSkin.Fill(bottom, GhmSkin.PanelBack);
            GUILayout.BeginArea(bottom);
            DrawCatalogPanel(bottom);
            GUILayout.EndArea();
        }

        private void DrawRightColumn(Rect column)
        {
            float topHeight = Mathf.Round(column.height * _rightSplit) - 2f;
            var top = new Rect(column.x, column.y, column.width, topHeight);
            var bottom = new Rect(column.x, column.y + topHeight + 4f, column.width, column.height - topHeight - 4f);

            GhmSkin.Fill(top, GhmSkin.PanelBack);
            GUILayout.BeginArea(top);
            DrawLayersPanel(top);
            GUILayout.EndArea();

            GhmSkin.Fill(bottom, GhmSkin.PanelBack);
            GUILayout.BeginArea(bottom);
            DrawInspectorPanel(bottom);
            GUILayout.EndArea();
        }

        private void DrawCentre(Rect column)
        {
            GhmSkin.Fill(column, new Color(0.10f, 0.11f, 0.13f));

            var bar = new Rect(column.x, column.y, column.width, 22f);
            GUILayout.BeginArea(bar);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            _preview.mode = (GhmPreview.Mode)GUILayout.Toolbar((int)_preview.mode,
                new[] { "Scene", "Plan" }, EditorStyles.toolbarButton, GUILayout.Width(110f));

            GUILayout.Space(8f);
            _preview.showPaths = GUILayout.Toggle(_preview.showPaths, "Paths", EditorStyles.toolbarButton, GUILayout.Width(50f));
            _preview.showDecor = GUILayout.Toggle(_preview.showDecor, "Decor", EditorStyles.toolbarButton, GUILayout.Width(52f));
            _preview.showGrid = GUILayout.Toggle(_preview.showGrid, "Grid", EditorStyles.toolbarButton, GUILayout.Width(44f));

            if (GhmSkin.ToolbarButton("Reset view", "Recentre and unzoom the preview", 74f))
            {
                _preview.zoom = 1f;
                _preview.yawOffset = 0f;
                _preview.pan = Vector2.zero;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_preview.Status, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            var footer = new Rect(column.x, column.yMax - 52f, column.width, 52f);
            var view = new Rect(column.x, column.y + 22f, column.width, column.height - 22f - footer.height);

            _preview.Rebuild(_board, _profile, _level, PreviewHash());
            _preview.Draw(view, _board, _profile);

            DrawFooter(footer);
        }

        private void DrawFooter(Rect footer)
        {
            GhmSkin.Fill(footer, GhmSkin.PanelBack);
            GUILayout.BeginArea(new Rect(footer.x + 8f, footer.y + 6f, footer.width - 16f, footer.height - 10f));
            GUILayout.BeginHorizontal();

            var band = _profile.BandForLevel(_level);
            GUILayout.BeginVertical();
            GUILayout.Label($"Level {_level}  ·  band <b>{(band != null ? band.name : "none")}</b>  ·  {_profile.width}x{_profile.height} cells", GhmSkin.Sub);
            GUILayout.Label(string.IsNullOrEmpty(_status) ? $"generated in {_lastGenerateMs:0.#} ms" : _status, GhmSkin.Sub);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Generate next level", "Advance a level and roll its layout"), GUILayout.Width(150f), GUILayout.Height(30f)))
                SetLevel(_level + 1);

            if (GhmSkin.AccentButton("Publish to game", "Write this board, its materials and its camera into the game scene", 30f))
                Publish();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSplitters(Rect body, float centreX, float centreWidth)
        {
            var leftHandle = new Rect(body.x + _leftWidth, body.y, 4f, body.height);
            _leftWidth = GhmSkin.Splitter(leftHandle, _leftWidth, true, 240f, body.width * 0.4f, ref _dragLeft, 6001);

            var rightHandle = new Rect(centreX + centreWidth, body.y, 4f, body.height);
            float rightEdge = GhmSkin.Splitter(rightHandle, body.width - _rightWidth, true, body.width * 0.35f, body.width - 250f, ref _dragRight, 6002);
            _rightWidth = Mathf.Clamp(body.width - rightEdge, 250f, body.width * 0.4f);

            var leftRow = new Rect(body.x, body.y + body.height * _leftSplit - 2f, _leftWidth, 4f);
            float leftY = GhmSkin.Splitter(leftRow, body.height * _leftSplit, false, body.height * 0.18f, body.height * 0.82f, ref _dragLeftRow, 6003);
            _leftSplit = Mathf.Clamp01(leftY / Mathf.Max(1f, body.height));

            var rightRow = new Rect(centreX + centreWidth + 4f, body.y + body.height * _rightSplit - 2f, _rightWidth, 4f);
            float rightY = GhmSkin.Splitter(rightRow, body.height * _rightSplit, false, body.height * 0.15f, body.height * 0.85f, ref _dragRightRow, 6004);
            _rightSplit = Mathf.Clamp01(rightY / Mathf.Max(1f, body.height));

            if (_dragLeft || _dragRight || _dragLeftRow || _dragRightRow) Repaint();
        }

        // ------------------------------------------------------------------
        // State
        // ------------------------------------------------------------------

        private void SetProfile(GhmMapProfile profile)
        {
            _profile = profile;
            if (_profile != null)
            {
                _profile.EnsureDefaults();
                EditorPrefs.SetString(LastProfileKey, AssetDatabase.GetAssetPath(_profile));
                _level = Mathf.Max(1, _profile.previewLevel);
            }
            _layerIndex = 0;
            _ruleIndex = -1;
            _textureIndex = -1;
            Regenerate();
        }

        private void SetLevel(int level)
        {
            _level = Mathf.Max(1, level);
            if (_profile != null)
            {
                _profile.previewLevel = _level;
                EditorUtility.SetDirty(_profile);
            }
            Regenerate();
        }

        private void Regenerate()
        {
            if (_profile == null)
            {
                _board = null;
                return;
            }

            _profile.EnsureDefaults();

            double start = EditorApplication.timeSinceStartup;
            _board = GhmGenerator.Generate(_profile, _level);
            _lastGenerateMs = (EditorApplication.timeSinceStartup - start) * 1000.0;

            _boardVersion++;
            _status = "";
            _preview?.Invalidate();
            Repaint();
        }

        // What the preview was last built from. Anything that changes the
        // picture has to be in here or the preview would go stale.
        private int PreviewHash()
        {
            unchecked
            {
                int hash = _boardVersion * 397;
                hash = hash * 31 + _level;
                hash = hash * 31 + (_preview.showDecor ? 1 : 0);
                hash = hash * 31 + (_preview.showPaths ? 2 : 0);
                return hash;
            }
        }

        private void Publish()
        {
            if (_profile == null) return;

            if (!EditorUtility.DisplayDialog("Publish to the game scene",
                    $"This writes the level {_level} board into '{_profile.targetScenePath}':\n\n"
                    + $"· {_profile.width}x{_profile.height} tiles re-typed (and added or removed if the size changed)\n"
                    + "· the border wall rebuilt to match\n"
                    + "· ground, water and wall materials and settings written onto the scene's surface components\n"
                    + "· camera angle and clamp bounds set\n"
                    + "· paths and decor added under a 'GhostHunterMaps' object\n\n"
                    + "The scene is modified but not saved, and it is all one undo step.",
                    "Publish", "Cancel"))
                return;

            var report = GhmPublisher.Publish(_profile, _level);
            _status = report.success ? "Published to " + _profile.targetScenePath : "Publish did not complete.";

            if (report.success) Debug.Log("[Ghost Hunter Maps] Published:\n" + report.Summary(), _profile);
            else Debug.LogWarning("[Ghost Hunter Maps] Publish stopped:\n" + report.Summary(), _profile);

            if (report.warnings.Count > 0)
                EditorUtility.DisplayDialog("Publish report", report.Summary(), "OK");
        }
    }
}
