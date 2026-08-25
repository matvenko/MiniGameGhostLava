using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Bottom-left: the texture catalogue.
    //
    // This is the shelf the rest of the editor picks from. A painted sheet comes
    // in here once, gets cut into pieces here, and from then on a decor rule or a
    // layer just points at a piece. Slicing writes real PNG assets rather than
    // sprite sub-rects, because the pieces end up on materials, and a material
    // cannot take a sprite's sub-rect.
    public partial class GhmWindow
    {
        private GhmTextureCategory _importCategory = GhmTextureCategory.Decor;
        private GhmTextureCategory _filter = GhmTextureCategory.Decor;
        private bool _filterAll = true;
        private string _search = "";
        private float _thumbSize = 64f;

        private int _sliceColumns = 4;
        private int _sliceRows = 4;
        private int _slicePadding = 0;
        private bool _sliceSkipEmpty = true;
        private int _sliceMinSize = 12;
        private float _sliceAlpha = 0.08f;
        private bool _showSliceTools;

        private void DrawCatalogPanel(Rect rect)
        {
            GhmSkin.PanelTitle("Textures", $"{_profile.catalog.Count} in catalogue");

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _filterAll = GUILayout.Toggle(_filterAll, "All", EditorStyles.toolbarButton, GUILayout.Width(34f));
                using (new EditorGUI.DisabledScope(_filterAll))
                {
                    _filter = (GhmTextureCategory)EditorGUILayout.EnumPopup(_filter, EditorStyles.toolbarPopup, GUILayout.Width(70f));
                }

                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(60f));
                GUILayout.FlexibleSpace();
                _thumbSize = GUILayout.HorizontalSlider(_thumbSize, 36f, 110f, GUILayout.Width(60f));
            }

            var visible = VisibleEntries();

            // IMGUI lays out top to bottom, so an expanding scroll view would
            // push the action strip off the panel. The strip's height is
            // predictable, so it is reserved up front instead.
            bool hasSelection = _textureIndex >= 0 && _textureIndex < _profile.catalog.Count;
            float actionsHeight = !hasSelection ? 44f : (_showSliceTools ? 250f : 116f);
            float scrollHeight = Mathf.Max(60f, rect.height - 46f - actionsHeight);

            _catalogScroll = GUILayout.BeginScrollView(_catalogScroll, GUILayout.Height(scrollHeight));
            DrawThumbnailGrid(visible, rect.width - 18f);
            GUILayout.EndScrollView();

            DrawCatalogActions(visible);
        }

        private List<GhmTextureEntry> VisibleEntries()
        {
            var list = new List<GhmTextureEntry>();
            foreach (var e in _profile.catalog)
            {
                if (!_filterAll && e.category != _filter) continue;
                if (!string.IsNullOrEmpty(_search) && e.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(e);
            }
            return list;
        }

        private void DrawThumbnailGrid(List<GhmTextureEntry> entries, float width)
        {
            if (entries.Count == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Nothing here yet. Import a painted sheet from the global panel, then slice it below.", GhmSkin.Sub);
                return;
            }

            float cell = _thumbSize + 10f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / cell));
            int rows = Mathf.CeilToInt(entries.Count / (float)columns);

            for (int r = 0; r < rows; r++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < columns; c++)
                    {
                        int index = r * columns + c;
                        if (index >= entries.Count) { GUILayout.Space(cell); continue; }
                        DrawThumbnail(entries[index], cell);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawThumbnail(GhmTextureEntry entry, float cell)
        {
            var rect = GUILayoutUtility.GetRect(cell, cell + 12f, GUILayout.Width(cell), GUILayout.Height(cell + 12f));
            int catalogIndex = _profile.catalog.IndexOf(entry);
            bool selected = catalogIndex == _textureIndex;

            var box = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.width - 4f);
            GhmSkin.Fill(box, selected ? GhmSkin.Accent * 0.6f : new Color(0f, 0f, 0f, 0.25f));

            var inner = new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f);
            if (entry.texture != null)
            {
                // Checkerboard behind the thumbnail so cut-out art with alpha is
                // readable instead of vanishing into the panel.
                EditorGUI.DrawTextureTransparent(inner, entry.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GhmSkin.Fill(inner, new Color(0.3f, 0.15f, 0.15f, 0.6f));
                GUI.Label(inner, "missing", GhmSkin.Tile);
            }

            var label = new Rect(rect.x, box.yMax, rect.width, 12f);
            GUI.Label(label, entry.name, GhmSkin.Tile);

            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                _textureIndex = catalogIndex;
                if (e.clickCount == 2 && entry.texture != null) EditorGUIUtility.PingObject(entry.texture);
                e.Use();
                Repaint();
            }
        }

        private void DrawCatalogActions(List<GhmTextureEntry> visible)
        {
            var selected = _textureIndex >= 0 && _textureIndex < _profile.catalog.Count ? _profile.catalog[_textureIndex] : null;

            using (new EditorGUILayout.VerticalScope(GhmSkin.Panel))
            {
                if (selected == null)
                {
                    GUILayout.Label("Select a texture to assign or slice it.", GhmSkin.Sub);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    selected.name = EditorGUILayout.TextField(selected.name);
                    selected.category = (GhmTextureCategory)EditorGUILayout.EnumPopup(selected.category, GUILayout.Width(80f));
                    if (GUILayout.Button(new GUIContent("×", "Remove from the catalogue. The asset itself is left alone."), GUILayout.Width(22f)))
                    {
                        _profile.catalog.RemoveAt(_textureIndex);
                        _textureIndex = -1;
                        GUIUtility.ExitGUI();
                    }
                }

                selected.tint = EditorGUILayout.ColorField("Tint", selected.tint);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var layer = SelectedLayer();
                    using (new EditorGUI.DisabledScope(layer == null))
                    {
                        if (GUILayout.Button(new GUIContent("→ Layer", "Use this texture on the selected layer")))
                        {
                            layer.texture = selected.texture;
                            layer.tiling = selected.tiling;
                            Regenerate();
                        }
                    }

                    var rule = SelectedRule();
                    using (new EditorGUI.DisabledScope(rule == null))
                    {
                        if (GUILayout.Button(new GUIContent("→ Decor rule", "Use this texture on the selected decor rule")))
                        {
                            rule.texture = selected.texture;
                            rule.source = GhmDecorSource.Texture;
                            Regenerate();
                        }
                    }

                    using (new EditorGUI.DisabledScope(SelectedLayer() == null || SelectedLayer().kind != GhmLayerKind.Decor))
                    {
                        if (GUILayout.Button(new GUIContent("+ New rule", "Add a decor rule to the selected layer using this texture")))
                            AddRuleFromTexture(selected);
                    }
                }

                _showSliceTools = EditorGUILayout.Foldout(_showSliceTools, "Slice this sheet", true);
                if (!_showSliceTools) return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    _sliceColumns = Mathf.Max(1, EditorGUILayout.IntField("Cols", _sliceColumns));
                    _sliceRows = Mathf.Max(1, EditorGUILayout.IntField("Rows", _sliceRows));
                    _slicePadding = Mathf.Max(0, EditorGUILayout.IntField("Pad", _slicePadding));
                }
                _sliceSkipEmpty = EditorGUILayout.Toggle("Skip empty cells", _sliceSkipEmpty);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Slice grid", "Cut the sheet into equal cells")))
                    {
                        int n = GhmTextureTools.SliceGrid(_profile, selected, _sliceColumns, _sliceRows, _slicePadding, _sliceSkipEmpty);
                        _status = $"Sliced {n} pieces from {selected.name}.";
                    }

                    if (GUILayout.Button(new GUIContent("Slice by alpha", "Find each painted island on the sheet and cut it out on its own")))
                    {
                        int n = GhmTextureTools.SliceByAlpha(_profile, selected, _sliceMinSize, _slicePadding, _sliceAlpha);
                        _status = $"Found {n} pieces on {selected.name}.";
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _sliceMinSize = EditorGUILayout.IntSlider(new GUIContent("Min px", "Islands smaller than this are ignored as specks."), _sliceMinSize, 2, 128);
                }
                _sliceAlpha = EditorGUILayout.Slider(new GUIContent("Alpha cut", "How opaque a pixel has to be to count as part of a piece."), _sliceAlpha, 0.01f, 0.9f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Export selection as atlas…", "Pack everything currently listed into one PNG plus a JSON manifest")))
                    {
                        string path = GhmTextureTools.ExportAtlas(visible, _profile.name + "_atlas", 2, 4096);
                        _status = string.IsNullOrEmpty(path) ? "Atlas export cancelled." : "Atlas written to " + path;
                    }
                }
            }
        }

        private void AddRuleFromTexture(GhmTextureEntry entry)
        {
            var layer = SelectedLayer();
            if (layer == null || layer.kind != GhmLayerKind.Decor) return;

            layer.rules.Add(new GhmDecorRule
            {
                name = entry.name,
                texture = entry.texture,
                source = GhmDecorSource.Texture,
                baseScale = Mathf.Max(0.05f, entry.authoredCells),
                seedSalt = layer.rules.Count + 1
            });
            _ruleIndex = layer.rules.Count - 1;
            Regenerate();
        }
    }
}
