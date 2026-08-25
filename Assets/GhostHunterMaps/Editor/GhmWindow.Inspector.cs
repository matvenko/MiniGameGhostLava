using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Bottom-right: everything about the selected layer.
    //
    // The surface layers expose the same knobs the game's own components have,
    // under the same names, because publishing writes them straight onto those
    // components - what is tuned here is literally what the game will run with.
    public partial class GhmWindow
    {
        private void DrawInspectorPanel(Rect rect)
        {
            var layer = SelectedLayer();
            GhmSkin.PanelTitle("Layer inspector", layer != null ? layer.kind.ToString() : "");

            if (layer == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("  Select a layer to edit it.", GhmSkin.Sub);
                return;
            }

            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
            GUILayout.Space(4f);

            DrawLayerCommon(layer);

            switch (layer.kind)
            {
                case GhmLayerKind.Ground: DrawGroundLayer(layer); break;
                case GhmLayerKind.Water: DrawWaterLayer(layer); break;
                case GhmLayerKind.Wall: DrawWallLayer(layer); break;
                case GhmLayerKind.Path: DrawPathLayer(layer); break;
                case GhmLayerKind.Decor: DrawDecorLayer(layer); break;
            }

            GUILayout.Space(12f);
            GUILayout.EndScrollView();
        }

        private void DrawLayerCommon(GhmLayer layer)
        {
            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    layer.name = EditorGUILayout.TextField(layer.name);
                    layer.visible = GUILayout.Toggle(layer.visible, new GUIContent("Visible"), EditorStyles.miniButton, GUILayout.Width(56f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Levels", "The layer is skipped entirely outside this range."));
                    layer.minLevel = Mathf.Max(1, EditorGUILayout.IntField(layer.minLevel));
                    layer.maxLevel = Mathf.Max(layer.minLevel, EditorGUILayout.IntField(layer.maxLevel));
                }

                if (!layer.ActiveAtLevel(_level))
                    EditorGUILayout.HelpBox($"Not active at level {_level}, so it is missing from the preview.", MessageType.None);
            }
        }

        // ------------------------------------------------------------------

        private void DrawGroundLayer(GhmLayer layer)
        {
            GhmSkin.Section("Material");
            layer.material = (Material)EditorGUILayout.ObjectField(new GUIContent("Ground material", "Written onto the scene's GroundSurface when published. A band can override it per level range."), layer.material, typeof(Material), false);
            layer.tint = EditorGUILayout.ColorField("Tint", layer.tint);
            ShowBandOverride(GhmLayerKind.Ground);

            GhmSkin.Section("Shape");
            layer.facetsPerCell = EditorGUILayout.IntSlider(new GUIContent("Facets per cell", "Quads per cell edge. More facets means a finer low-poly surface."), layer.facetsPerCell, 1, 6);
            layer.heightJitter = EditorGUILayout.Slider(new GUIContent("Height jitter", "Random per-vertex offset. Purely visual - collision still comes from the tiles."), layer.heightJitter, 0f, 0.3f);
            layer.skirtDepth = EditorGUILayout.Slider(new GUIContent("Skirt depth", "How far the shoreline wall drops. Must reach past the water bed."), layer.skirtDepth, 0.2f, 3f);

            GhmSkin.Section("Colour zones");
            layer.colorZones = EditorGUILayout.IntSlider("Zones", layer.colorZones, 2, 6);
            layer.zoneScale = EditorGUILayout.Slider("Zone scale", layer.zoneScale, 1f, 12f);
            layer.zoneContrast = EditorGUILayout.Slider(new GUIContent("Zone contrast", "Low values leave every facet in the middle band."), layer.zoneContrast, 1f, 4f);
            layer.colorVariation = EditorGUILayout.Slider("Facet variation", layer.colorVariation, 0f, 1f);

            GhmSkin.Section("Meadow");
            layer.undulation = EditorGUILayout.Slider(new GUIContent("Undulation", "Amplitude of the rolling swell over the whole field."), layer.undulation, 0f, 0.4f);
            layer.undulationScale = EditorGUILayout.Slider("Undulation scale", layer.undulationScale, 2f, 20f);
            layer.shoreWidth = EditorGUILayout.Slider(new GUIContent("Shore width", "How far inland the damp shoreline band reaches, in cells."), layer.shoreWidth, 0.5f, 5f);
            layer.shoreDip = EditorGUILayout.Slider(new GUIContent("Shore dip", "How far the ground sinks as it meets the water."), layer.shoreDip, 0f, 0.3f);
            layer.waterClearance = EditorGUILayout.Slider(new GUIContent("Water clearance", "Minimum gap kept above the water plane so it never pokes through the grass."), layer.waterClearance, 0f, 0.3f);
        }

        private void DrawWaterLayer(GhmLayer layer)
        {
            GhmSkin.Section("Material");
            layer.material = (Material)EditorGUILayout.ObjectField(new GUIContent("Liquid material", "The shared surface stretched over the whole pool."), layer.material, typeof(Material), false);
            layer.bedMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Bed material", "Opaque bottom seen through the liquid. Empty skips the bed."), layer.bedMaterial, typeof(Material), false);
            layer.tint = EditorGUILayout.ColorField("Tint", layer.tint);
            ShowBandOverride(GhmLayerKind.Water);

            GhmSkin.Section("Layout");
            layer.surfaceYOffset = EditorGUILayout.Slider(new GUIContent("Surface offset", "Fine-tunes the height relative to the top of the water tiles."), layer.surfaceYOffset, -0.4f, 0.4f);
            layer.bedDepth = EditorGUILayout.Slider(new GUIContent("Bed depth", "Drives the shader's depth fade and its intersection foam."), layer.bedDepth, 0.05f, 3f);
            layer.padding = EditorGUILayout.Slider(new GUIContent("Padding", "Extra size so the surface tucks under the surrounding wall."), layer.padding, 0f, 3f);
            layer.verticesPerUnit = EditorGUILayout.Slider(new GUIContent("Mesh density", "Wave shaders displace vertices, so a flat two-triangle quad would not ripple."), layer.verticesPerUnit, 1f, 12f);
        }

        private void DrawWallLayer(GhmLayer layer)
        {
            GhmSkin.Section("Material");
            layer.material = (Material)EditorGUILayout.ObjectField("Wall material", layer.material, typeof(Material), false);
            layer.tint = EditorGUILayout.ColorField("Tint", layer.tint);
            ShowBandOverride(GhmLayerKind.Wall);

            GhmSkin.Section("Shape");
            layer.wallRows = EditorGUILayout.IntSlider(new GUIContent("Rows", "How many cubes high the rampart stands."), layer.wallRows, 1, 4);
            layer.crestWear = EditorGUILayout.Slider(new GUIContent("Crest wear", "How far the top edge is worn down, so it is weathered rather than machined."), layer.crestWear, 0f, 0.4f);
            layer.crestFlatness = EditorGUILayout.Slider(new GUIContent("Crest flatness", "Share of the crest left level."), layer.crestFlatness, 0f, 1f);
            layer.castShadows = EditorGUILayout.Toggle("Cast shadows", layer.castShadows);

            GhmSkin.Section("Colour zones");
            layer.colorZones = EditorGUILayout.IntSlider("Zones", layer.colorZones, 2, 6);
            layer.zoneScale = EditorGUILayout.Slider("Zone scale", layer.zoneScale, 0.5f, 8f);
            layer.zoneContrast = EditorGUILayout.Slider("Zone contrast", layer.zoneContrast, 1f, 4f);
            layer.colorVariation = EditorGUILayout.Slider("Face variation", layer.colorVariation, 0f, 1f);
        }

        private void DrawPathLayer(GhmLayer layer)
        {
            var p = layer.path;

            GhmSkin.Section("Look");
            layer.texture = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Path texture", "Tiled across the whole network in world space, so it never restarts per cell."), layer.texture, typeof(Texture2D), false);
            layer.material = (Material)EditorGUILayout.ObjectField(new GUIContent("Material override", "Leave empty to use the built-in path overlay shader."), layer.material, typeof(Material), false);
            layer.tint = EditorGUILayout.ColorField("Tint", layer.tint);
            p.tiling = EditorGUILayout.Vector2Field("Tiling", p.tiling);
            p.opacity = EditorGUILayout.Slider("Opacity", p.opacity, 0f, 1f);
            p.edgeSoftness = EditorGUILayout.Slider(new GUIContent("Edge softness", "How wide the fade at the edge of the track is."), p.edgeSoftness, 0f, 1f);
            p.yOffset = EditorGUILayout.Slider(new GUIContent("Lift", "How far above the ground the skin sits. Small values only - it is depth-offset already."), p.yOffset, 0f, 0.1f);

            GhmSkin.Section("Network");
            p.anchors = EditorGUILayout.IntSlider(new GUIContent("Anchors", "Points the network is routed between. They are spread as far apart as the floor allows."), p.anchors, 0, 12);
            p.width = EditorGUILayout.Slider("Width (cells)", p.width, 0.3f, 3f);
            p.wander = EditorGUILayout.Slider(new GUIContent("Wander", "How much the route is allowed to curve away from the shortest line."), p.wander, 0f, 1f);
            p.reuse = EditorGUILayout.Slider(new GUIContent("Merge", "How strongly a new route prefers to join one already laid down, forming junctions instead of parallel tracks."), p.reuse, 0f, 1f);
            p.shoreAvoidance = EditorGUILayout.Slider(new GUIContent("Keep off the shore", "Cells from the water the route tries to stay clear of."), p.shoreAvoidance, 0f, 4f);
            p.smooth = EditorGUILayout.Toggle(new GUIContent("Round corners", "Fills the inside of every right-angle turn so the track does not read as pixel steps."), p.smooth);
            p.closeLoop = EditorGUILayout.Toggle(new GUIContent("Close the loop", "Route the last anchor back to the first."), p.closeLoop);

            if (_board != null)
                EditorGUILayout.LabelField(" ", $"{_board.pathRoute.Count} cells carry a path in this layout", GhmSkin.Sub);

            ShowBandOverride(GhmLayerKind.Path);
        }

        // ------------------------------------------------------------------

        private void DrawDecorLayer(GhmLayer layer)
        {
            GhmSkin.Section($"Rules ({layer.rules.Count})");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add rule"))
                {
                    layer.rules.Add(new GhmDecorRule { name = "Decor " + (layer.rules.Count + 1), seedSalt = layer.rules.Count + 1 });
                    _ruleIndex = layer.rules.Count - 1;
                    Regenerate();
                }

                using (new EditorGUI.DisabledScope(SelectedRule() == null))
                {
                    if (GUILayout.Button("Duplicate"))
                    {
                        var copy = layer.rules[_ruleIndex].Clone();
                        copy.name += " copy";
                        copy.seedSalt = layer.rules.Count + 1;
                        layer.rules.Insert(_ruleIndex + 1, copy);
                        _ruleIndex++;
                        Regenerate();
                    }

                    if (GUILayout.Button("Delete"))
                    {
                        layer.rules.RemoveAt(_ruleIndex);
                        _ruleIndex = Mathf.Clamp(_ruleIndex, -1, layer.rules.Count - 1);
                        Regenerate();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            for (int i = 0; i < layer.rules.Count; i++)
            {
                var rule = layer.rules[i];
                var row = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
                if (i == _ruleIndex) GhmSkin.Fill(row, new Color(GhmSkin.Accent.r, GhmSkin.Accent.g, GhmSkin.Accent.b, 0.18f));

                var toggle = new Rect(row.x + 4f, row.y + 3f, 16f, 16f);
                bool enabled = GUI.Toggle(toggle, rule.enabled, GUIContent.none);
                if (enabled != rule.enabled)
                {
                    rule.enabled = enabled;
                    Regenerate();
                }

                var icon = new Rect(row.x + 22f, row.y + 2f, 18f, 18f);
                if (rule.texture != null) EditorGUI.DrawTextureTransparent(icon, rule.texture, ScaleMode.ScaleToFit);
                else GhmSkin.Fill(icon, new Color(1f, 1f, 1f, 0.08f));

                GUI.Label(new Rect(row.x + 44f, row.y + 3f, row.width - 120f, 16f), rule.name, i == _ruleIndex ? GhmSkin.RowSelected : GhmSkin.Row);
                GUI.Label(new Rect(row.xMax - 74f, row.y + 3f, 70f, 16f), $"{rule.per100Cells:0.#}/100", GhmSkin.Sub);

                var e = Event.current;
                if (e.type == EventType.MouseDown && row.Contains(e.mousePosition) && !toggle.Contains(e.mousePosition))
                {
                    _ruleIndex = i;
                    e.Use();
                    Repaint();
                }
            }

            var selected = SelectedRule();
            if (selected == null) return;

            GUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                DrawDecorRule(selected);
            }
        }

        private void DrawDecorRule(GhmDecorRule rule)
        {
            rule.name = EditorGUILayout.TextField("Name", rule.name);
            rule.source = (GhmDecorSource)EditorGUILayout.EnumPopup(new GUIContent("Source", "A texture becomes a quad batched with every other instance of this rule; a prefab is instantiated as-is."), rule.source);

            if (rule.source == GhmDecorSource.Texture)
            {
                rule.texture = (Texture2D)EditorGUILayout.ObjectField("Texture", rule.texture, typeof(Texture2D), false);
                rule.materialOverride = (Material)EditorGUILayout.ObjectField(new GUIContent("Material override", "Leave empty to use the built-in cut-out sprite shader."), rule.materialOverride, typeof(Material), false);
                if (rule.texture == null && rule.materialOverride == null)
                    EditorGUILayout.HelpBox("No texture, so this rule places nothing. Pick one in the catalogue and press '→ Decor rule'.", MessageType.Info);
            }
            else
            {
                rule.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", rule.prefab, typeof(GameObject), false);
            }

            GhmSkin.Section("How often");
            rule.per100Cells = EditorGUILayout.Slider(new GUIContent("Per 100 cells", "Expected instances per hundred walkable cells. The band's decor density scales this per level range."), rule.per100Cells, 0f, 200f);
            rule.minSpacing = EditorGUILayout.Slider(new GUIContent("Min spacing", "Cells that must separate two instances of this rule."), rule.minSpacing, 0f, 6f);
            rule.clusterSize = EditorGUILayout.IntSlider(new GUIContent("Cluster size", "1 scatters evenly; higher values drop little clumps, which is how growing things actually look."), rule.clusterSize, 1, 8);
            rule.clusterRadius = EditorGUILayout.Slider("Cluster radius", rule.clusterRadius, 0f, 2f);

            if (_board != null)
            {
                int expected = Mathf.RoundToInt(_board.GroundCount / 100f * rule.per100Cells * BandDecorScale());
                EditorGUILayout.LabelField(" ", $"about {expected} instances on this board", GhmSkin.Sub);
            }

            GhmSkin.Section("Where");
            rule.placement = (GhmPlacement)EditorGUILayout.EnumFlagsField(new GUIContent("Placement", "Combine freely: Shore + OffPath lands only near the water, away from any track."), rule.placement);
            if ((rule.placement & GhmPlacement.Inland) != 0)
                rule.inlandMargin = EditorGUILayout.Slider(new GUIContent("Inland margin", "Cells from the water it must keep clear of."), rule.inlandMargin, 0f, 6f);
            if ((rule.placement & GhmPlacement.Shore) != 0)
                rule.shoreBand = EditorGUILayout.Slider(new GUIContent("Shore band", "Cells from the water it stays inside."), rule.shoreBand, 0f, 6f);

            GhmSkin.Section("How it looks");
            rule.stance = (GhmDecorStance)EditorGUILayout.EnumPopup(new GUIContent("Stance", "Follow camera stands the sprite up by exactly the rig's tilt, so painted art keeps its shape as the compression changes."), rule.stance);
            rule.baseScale = EditorGUILayout.Slider(new GUIContent("Size (cells)", "Size of one instance in cells."), rule.baseScale, 0.05f, 4f);
            rule.scaleRange = EditorGUILayout.Vector2Field(new GUIContent("Size variation", "Multiplied onto the size, min and max."), rule.scaleRange);
            rule.yawJitter = EditorGUILayout.Slider("Yaw jitter", rule.yawJitter, 0f, 180f);
            rule.positionJitter = EditorGUILayout.Slider(new GUIContent("Position jitter", "How far off the cell centre an instance may sit."), rule.positionJitter, 0f, 0.5f);
            rule.pivot = EditorGUILayout.Slider(new GUIContent("Pivot", "0 keeps the bottom edge on the ground when the sprite stands up; 0.5 centres it."), rule.pivot, 0f, 1f);
            rule.yOffset = EditorGUILayout.Slider("Lift", rule.yOffset, -0.2f, 1f);
            rule.tint = EditorGUILayout.ColorField("Tint", rule.tint);
            rule.tintVariation = EditorGUILayout.Slider(new GUIContent("Tint variation", "Per-instance brightness spread, so a hundred copies of one sprite do not read as one flat mass."), rule.tintVariation, 0f, 1f);

            GhmSkin.Section("When");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Levels");
                rule.minLevel = Mathf.Max(1, EditorGUILayout.IntField(rule.minLevel));
                rule.maxLevel = Mathf.Max(rule.minLevel, EditorGUILayout.IntField(rule.maxLevel));
            }
            rule.seedSalt = EditorGUILayout.IntField(new GUIContent("Seed salt", "Give two similar rules different salts or they will fight for the same cells."), rule.seedSalt);
        }

        private float BandDecorScale()
        {
            var band = _profile.BandForLevel(_level);
            return band != null ? band.decorDensityScale : 1f;
        }

        // Bands are the reason a material slot here can be quietly ignored, so
        // the inspector says so rather than letting it look broken.
        private void ShowBandOverride(GhmLayerKind kind)
        {
            var band = _profile.BandForLevel(_level);
            if (band == null) return;

            Material overrideMaterial = null;
            switch (kind)
            {
                case GhmLayerKind.Ground: overrideMaterial = band.groundMaterial; break;
                case GhmLayerKind.Water: overrideMaterial = band.waterMaterial; break;
                case GhmLayerKind.Wall: overrideMaterial = band.wallMaterial; break;
                case GhmLayerKind.Path: overrideMaterial = band.pathMaterial; break;
            }

            if (overrideMaterial == null) return;
            EditorGUILayout.HelpBox($"Band '{band.name}' overrides this with '{overrideMaterial.name}' for levels {band.minLevel}-{band.maxLevel}.", MessageType.Info);
        }
    }
}
