using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Top-left: everything that is true of the whole map rather than of one
    // layer - how the camera looks at it, how big it is, how it is generated,
    // which assets each level range wears, and the import/export actions.
    public partial class GhmWindow
    {
        private bool _showCamera = true;
        private bool _showBoard = true;
        private bool _showGeneration = true;
        private bool _showBands = true;
        private bool _showIo = true;
        private bool _showLighting;
        private bool _showPublish;
        private int _expandedBand = -1;

        private void DrawGlobalPanel(Rect rect)
        {
            GhmSkin.PanelTitle("Global settings", _profile != null ? _profile.name : "");

            _globalScroll = GUILayout.BeginScrollView(_globalScroll);
            GUILayout.Space(4f);

            DrawCameraSection();
            DrawBoardSection();
            DrawGenerationSection();
            DrawBandsSection();
            DrawIoSection();
            DrawLightingSection();
            DrawPublishSection();

            GUILayout.Space(10f);
            GUILayout.EndScrollView();
        }

        private void DrawCameraSection()
        {
            _showCamera = Foldout(_showCamera, "Camera");
            if (!_showCamera) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                // The headline control. Compression is the factor the board is
                // foreshortened by, which is a thing you can see, rather than an
                // angle in degrees, which is a thing you have to imagine.
                _profile.compression = EditorGUILayout.Slider(
                    new GUIContent("Compression", "1.0 is straight down. Lower values tilt the rig, squashing the board along Z by exactly this factor."),
                    _profile.compression, 0.3f, 1f);

                EditorGUILayout.LabelField(" ", $"camera pitch {_profile.CameraPitch:0.#}°  ·  tilt {_profile.TiltFromVertical:0.#}° off vertical", GhmSkin.Sub);

                _profile.cameraHeight = EditorGUILayout.Slider(new GUIContent("Height", "How high the rig rides above the player."), _profile.cameraHeight, 3f, 30f);
                _profile.fieldOfView = EditorGUILayout.Slider("Field of view", _profile.fieldOfView, 20f, 90f);
                _profile.cameraYaw = EditorGUILayout.Slider(new GUIContent("Yaw", "Rotates the whole rig. Decor set to follow the camera turns with it."), _profile.cameraYaw, -180f, 180f);
                _profile.clampCameraToMap = EditorGUILayout.Toggle(new GUIContent("Clamp to map", "Stop the view from showing the empty world outside the wall."), _profile.clampCameraToMap);

                EditorGUILayout.LabelField(" ", $"follow offset {_profile.CameraOffset}", GhmSkin.Sub);
            }
        }

        private void DrawBoardSection()
        {
            _showBoard = Foldout(_showBoard, "Board size");
            if (!_showBoard) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                EditorGUI.BeginChangeCheck();
                _profile.width = EditorGUILayout.IntSlider(new GUIContent("Width (X cells)"), _profile.width, 4, 80);
                _profile.height = EditorGUILayout.IntSlider(new GUIContent("Height (Z cells)"), _profile.height, 4, 80);
                bool resized = EditorGUI.EndChangeCheck();

                _profile.wallMargin = EditorGUILayout.IntSlider(new GUIContent("Wall margin", "Cells of border wall around the floor."), _profile.wallMargin, 0, 4);
                _profile.boardCenter = EditorGUILayout.Vector2Field(new GUIContent("Centre (world X/Z)", "The board grows symmetrically around this point when it is resized."), _profile.boardCenter);

                EditorGUILayout.LabelField($"{_profile.width * _profile.height} cells  ·  footprint {_profile.FloorBounds.size.x:0.#} x {_profile.FloorBounds.size.z:0.#}", GhmSkin.Sub);

                _profile.floorY = EditorGUILayout.FloatField(new GUIContent("Floor Y", "World height of a walkable tile's centre. Matches the scene's existing board."), _profile.floorY);
                _profile.waterDrop = EditorGUILayout.FloatField(new GUIContent("Water drop", "How far a water tile is recessed below the floor."), _profile.waterDrop);
                _profile.cellSize = EditorGUILayout.FloatField(new GUIContent("Cell size", "The game's surface components assume 1.0; other values will mis-index cells."), _profile.cellSize);

                if (!Mathf.Approximately(_profile.cellSize, 1f))
                    EditorGUILayout.HelpBox("Cell size is not 1.0. The game's ground and water surfaces index cells by rounding raw world distances, so publishing at this size will misalign them.", MessageType.Warning);

                if (resized)
                    EditorGUILayout.HelpBox("Publishing will add or remove tiles and rebuild the border wall to match.", MessageType.Info);
            }
        }

        private void DrawGenerationSection()
        {
            _showGeneration = Foldout(_showGeneration, "Generation");
            if (!_showGeneration) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                _profile.seed = EditorGUILayout.IntField("Seed", _profile.seed);
                _profile.perLevelLayout = EditorGUILayout.Toggle(new GUIContent("Re-roll per level", "Off keeps one fixed layout for every level."), _profile.perLevelLayout);
                _profile.overrideRuntimeLayout = EditorGUILayout.Toggle(
                    new GUIContent("Override in game", "Lay this generator's layout over the one LevelManager shuffles for itself. Off leaves the game's own layout alone and only publishes the look."),
                    _profile.overrideRuntimeLayout);

                _profile.minIslandSize = EditorGUILayout.IntSlider(new GUIContent("Min island", "Floor islands smaller than this are flooded; bigger ones get a causeway carved to the mainland."), _profile.minIslandSize, 1, 20);
                _profile.minWalkableShare = EditorGUILayout.Slider(new GUIContent("Min walkable", "Floor is topped back up if an algorithm carves away more than this."), _profile.minWalkableShare, 0.25f, 0.95f);

                if (_board != null)
                {
                    float share = _board.GroundCount / (float)Mathf.Max(1, _board.width * _board.height);
                    EditorGUILayout.LabelField($"this layout: {share * 100f:0.#}% walkable, {(_board.IsFullyConnected() ? "fully connected" : "SPLIT")}",
                        _board.IsFullyConnected() ? GhmSkin.Sub : EditorStyles.boldLabel);
                }
            }
        }

        // The "levels 1-5 use this ground, 6-10 use that one" list.
        private void DrawBandsSection()
        {
            _showBands = Foldout(_showBands, $"Level bands ({_profile.bands.Count})");
            if (!_showBands) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                for (int i = 0; i < _profile.bands.Count; i++)
                {
                    var band = _profile.bands[i];
                    bool active = band.Covers(_level);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var swatch = GUILayoutUtility.GetRect(4f, 18f, GUILayout.Width(4f));
                        GhmSkin.Fill(swatch, active ? GhmSkin.Accent : new Color(1f, 1f, 1f, 0.12f));

                        bool expanded = _expandedBand == i;
                        if (GUILayout.Button($"{band.name}   {band.minLevel}-{band.maxLevel}", expanded ? EditorStyles.toolbarButton : EditorStyles.label))
                            _expandedBand = expanded ? -1 : i;

                        if (active) GUILayout.Label("current", GhmSkin.Sub, GUILayout.Width(50f));

                        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f)))
                        {
                            _profile.bands.RemoveAt(i);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (_expandedBand != i) continue;

                    EditorGUI.indentLevel++;
                    band.name = EditorGUILayout.TextField("Name", band.name);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        band.minLevel = Mathf.Max(1, EditorGUILayout.IntField("Levels", band.minLevel));
                        band.maxLevel = Mathf.Max(band.minLevel, EditorGUILayout.IntField(band.maxLevel));
                    }

                    band.algorithm = (GhmAlgorithm)EditorGUILayout.EnumPopup(new GUIContent("Algorithm", AlgorithmHint(band.algorithm)), band.algorithm);
                    band.waterDensity = EditorGUILayout.Slider("Water density", band.waterDensity, 0f, 0.6f);
                    band.minPoolSize = EditorGUILayout.IntSlider(new GUIContent("Min pool", "Pools smaller than this are filled back in - a one-cell puddle reads as a hole, not water."), band.minPoolSize, 1, 12);
                    band.decorDensityScale = EditorGUILayout.Slider("Decor density", band.decorDensityScale, 0f, 3f);
                    band.drawPaths = EditorGUILayout.Toggle("Draw paths", band.drawPaths);

                    GhmSkin.Section("Assets for these levels");
                    band.groundMaterial = (Material)EditorGUILayout.ObjectField("Ground", band.groundMaterial, typeof(Material), false);
                    band.waterMaterial = (Material)EditorGUILayout.ObjectField("Water", band.waterMaterial, typeof(Material), false);
                    band.wallMaterial = (Material)EditorGUILayout.ObjectField("Wall", band.wallMaterial, typeof(Material), false);
                    band.bedMaterial = (Material)EditorGUILayout.ObjectField("Water bed", band.bedMaterial, typeof(Material), false);
                    band.pathMaterial = (Material)EditorGUILayout.ObjectField("Path", band.pathMaterial, typeof(Material), false);
                    EditorGUILayout.LabelField(" ", "Empty slots fall back to the layer's own material.", GhmSkin.Sub);

                    band.groundTint = EditorGUILayout.ColorField("Ground tint", band.groundTint);
                    band.waterTint = EditorGUILayout.ColorField("Water tint", band.waterTint);
                    band.wallTint = EditorGUILayout.ColorField("Wall tint", band.wallTint);
                    EditorGUI.indentLevel--;

                    GUILayout.Space(4f);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add band"))
                    {
                        int from = _profile.bands.Count > 0 ? _profile.bands[_profile.bands.Count - 1].maxLevel + 1 : 1;
                        _profile.bands.Add(new GhmLevelBand
                        {
                            name = $"Band {_profile.bands.Count + 1}",
                            minLevel = from,
                            maxLevel = from + 4
                        });
                        _expandedBand = _profile.bands.Count - 1;
                    }

                    if (GUILayout.Button(new GUIContent("Sort", "Order the bands by their starting level")))
                        _profile.bands.Sort((a, b) => a.minLevel.CompareTo(b.minLevel));
                }

                WarnAboutBandGaps();
            }
        }

        // Silent gaps are the failure mode here: level 6 falls through to the
        // last band and the map quietly wears the wrong assets.
        private void WarnAboutBandGaps()
        {
            for (int i = 1; i < _profile.bands.Count; i++)
            {
                var previous = _profile.bands[i - 1];
                var current = _profile.bands[i];
                if (current.minLevel > previous.maxLevel + 1)
                {
                    EditorGUILayout.HelpBox($"Levels {previous.maxLevel + 1}-{current.minLevel - 1} are not covered by any band; they fall through to the last one.", MessageType.Warning);
                    return;
                }
            }
        }

        private void DrawIoSection()
        {
            _showIo = Foldout(_showIo, "Import / export");
            if (!_showIo) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                _importCategory = (GhmTextureCategory)EditorGUILayout.EnumPopup("Import as", _importCategory);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Import file…", "Copy an image into the project and add it to the catalogue")))
                        ReportImport(GhmTextureTools.ImportFiles(_profile, _importCategory));

                    if (GUILayout.Button(new GUIContent("Import folder…", "Import every image in a folder")))
                        ReportImport(GhmTextureTools.ImportFolder(_profile, _importCategory));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Export atlas…", "Pack the catalogue into one PNG plus a JSON frame manifest")))
                        ExportAtlas();

                    if (GUILayout.Button(new GUIContent("Export textures…", "Write the catalogue out as individual PNGs")))
                    {
                        int n = GhmTextureTools.ExportTextures(_profile.catalog);
                        _status = n > 0 ? $"Exported {n} textures." : "Export cancelled.";
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Generate grid", "Rebuild the board from the current settings")))
                        Regenerate();

                    if (GUILayout.Button(new GUIContent("Save Resources copy", "Write a copy of this profile under Assets/Resources so a build can load it without a scene reference")))
                    {
                        GhmPublisher.SaveResourcesCopy(_profile);
                        _status = "Resources copy saved.";
                    }
                }
            }
        }

        private void DrawLightingSection()
        {
            _showLighting = Foldout(_showLighting, "Lighting");
            if (!_showLighting) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                _profile.sunColor = EditorGUILayout.ColorField("Sun colour", _profile.sunColor);
                _profile.sunIntensity = EditorGUILayout.Slider("Sun intensity", _profile.sunIntensity, 0f, 3f);
                _profile.sunPitch = EditorGUILayout.Slider("Sun pitch", _profile.sunPitch, 0f, 90f);
                _profile.sunYaw = EditorGUILayout.Slider("Sun yaw", _profile.sunYaw, -180f, 180f);
                _profile.ambientColor = EditorGUILayout.ColorField("Ambient", _profile.ambientColor);
                EditorGUILayout.LabelField(" ", "Always used by the preview; written to the scene only if 'publish lighting' is on.", GhmSkin.Sub);
            }
        }

        private void DrawPublishSection()
        {
            _showPublish = Foldout(_showPublish, "Publish target");
            if (!_showPublish) return;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(_profile.targetScenePath);
                var picked = (SceneAsset)EditorGUILayout.ObjectField("Scene", scene, typeof(SceneAsset), false);
                if (picked != scene && picked != null) _profile.targetScenePath = AssetDatabase.GetAssetPath(picked);

                _profile.publishCamera = EditorGUILayout.Toggle("Camera", _profile.publishCamera);
                _profile.publishLighting = EditorGUILayout.Toggle("Lighting", _profile.publishLighting);
                _profile.publishPaths = EditorGUILayout.Toggle("Paths", _profile.publishPaths);
                _profile.publishDecor = EditorGUILayout.Toggle("Decor", _profile.publishDecor);
            }
        }

        // ------------------------------------------------------------------

        private void ReportImport(int count)
        {
            _status = count > 0 ? $"Imported {count} texture{(count == 1 ? "" : "s")}." : "Nothing imported.";
        }

        private void ExportAtlas()
        {
            string path = GhmTextureTools.ExportAtlas(_profile.catalog, _profile.name + "_atlas", 2, 4096);
            _status = string.IsNullOrEmpty(path) ? "Atlas export cancelled." : "Atlas written to " + path;
        }

        private static string AlgorithmHint(GhmAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case GhmAlgorithm.ShuffleConnected: return "What the game already does: flip random cells to water, keeping only the flips that leave the floor fully connected.";
                case GhmAlgorithm.Caves: return "Cellular automata. Rounded lakes with soft coastlines.";
                case GhmAlgorithm.Rivers: return "Winding channels rim to rim, plus lakes. Reads most like a labyrinth.";
                case GhmAlgorithm.Rooms: return "Rectangular water blocks on a lattice, leaving a corridor grid.";
                default: return "Noise threshold: broad water with islands in it.";
            }
        }

        private static bool Foldout(bool value, string label)
        {
            GUILayout.Space(4f);
            return EditorGUILayout.Foldout(value, label, true, EditorStyles.foldoutHeader);
        }
    }
}
