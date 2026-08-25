using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Top-right: the layer stack.
    //
    // Order is meaningful only within a kind - the first visible Ground layer is
    // the one the floor is built from, and so on - which is why the list shows
    // which entry is actually driving each surface rather than pretending every
    // layer composites.
    public partial class GhmWindow
    {
        private void DrawLayersPanel(Rect rect)
        {
            GhmSkin.PanelTitle("Layers", $"{_profile.layers.Count} total");

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GhmSkin.ToolbarButton("+ Add", "Add a layer", 52f)) ShowAddLayerMenu();

                using (new EditorGUI.DisabledScope(SelectedLayer() == null))
                {
                    if (GhmSkin.ToolbarButton("Duplicate", "Copy the selected layer", 68f)) DuplicateLayer();
                    if (GhmSkin.ToolbarButton("▲", "Move up", 24f)) MoveLayer(-1);
                    if (GhmSkin.ToolbarButton("▼", "Move down", 24f)) MoveLayer(1);
                    if (GhmSkin.ToolbarButton("×", "Delete the selected layer", 24f)) DeleteLayer();
                }

                GUILayout.FlexibleSpace();
            }

            _layersScroll = GUILayout.BeginScrollView(_layersScroll);

            for (int i = 0; i < _profile.layers.Count; i++) DrawLayerRow(i);

            GUILayout.Space(6f);
            GUILayout.EndScrollView();
        }

        private void DrawLayerRow(int index)
        {
            var layer = _profile.layers[index];
            bool selected = index == _layerIndex;
            bool driving = IsDrivingLayer(layer, index);

            var row = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            if (selected) GhmSkin.Fill(row, new Color(GhmSkin.Accent.r, GhmSkin.Accent.g, GhmSkin.Accent.b, 0.18f));
            else if (index % 2 == 1) GhmSkin.Fill(row, new Color(1f, 1f, 1f, 0.02f));

            var swatch = new Rect(row.x + 4f, row.y + 4f, 4f, row.height - 8f);
            GhmSkin.Fill(swatch, GhmSkin.KindColor(layer.kind));

            var eye = new Rect(row.x + 12f, row.y + 4f, 16f, 16f);
            bool visible = GUI.Toggle(eye, layer.visible, GUIContent.none);
            if (visible != layer.visible)
            {
                layer.visible = visible;
                EditorUtility.SetDirty(_profile);
                Regenerate();
            }

            var nameRect = new Rect(row.x + 32f, row.y + 3f, row.width - 150f, 18f);
            GUI.Label(nameRect, layer.name, selected ? GhmSkin.RowSelected : GhmSkin.Row);

            var kindRect = new Rect(row.xMax - 116f, row.y + 4f, 60f, 16f);
            GUI.Label(kindRect, layer.kind.ToString(), GhmSkin.Sub);

            var badge = new Rect(row.xMax - 54f, row.y + 4f, 50f, 16f);
            if (layer.kind == GhmLayerKind.Decor) GUI.Label(badge, $"{layer.rules.Count} rules", GhmSkin.Sub);
            else if (layer.minLevel > 1 || layer.maxLevel < 999) GUI.Label(badge, $"L{layer.minLevel}-{layer.maxLevel}", GhmSkin.Sub);
            else if (driving) GUI.Label(badge, "active", GhmSkin.Sub);

            if (!layer.ActiveAtLevel(_level))
            {
                GhmSkin.Fill(row, new Color(0f, 0f, 0f, 0.35f));
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && row.Contains(e.mousePosition) && !eye.Contains(e.mousePosition))
            {
                _layerIndex = index;
                _ruleIndex = layer.kind == GhmLayerKind.Decor && layer.rules.Count > 0 ? 0 : -1;
                e.Use();
                Repaint();
            }
        }

        // Only the first active layer of a kind actually builds its surface. The
        // rest are kept as alternatives to switch to, so it has to be visible
        // which one is live.
        private bool IsDrivingLayer(GhmLayer layer, int index)
        {
            if (layer.kind == GhmLayerKind.Decor || layer.kind == GhmLayerKind.Path) return layer.ActiveAtLevel(_level);
            foreach (var candidate in _profile.LayersOfKind(layer.kind, _level)) return candidate == layer;
            return false;
        }

        private void ShowAddLayerMenu()
        {
            var menu = new GenericMenu();
            foreach (GhmLayerKind kind in System.Enum.GetValues(typeof(GhmLayerKind)))
            {
                var captured = kind;
                menu.AddItem(new GUIContent(kind.ToString()), false, () =>
                {
                    Undo.RecordObject(_profile, "Add layer");
                    _profile.layers.Add(GhmLayer.Create(captured, captured + " " + (_profile.layers.Count + 1)));
                    _layerIndex = _profile.layers.Count - 1;
                    EditorUtility.SetDirty(_profile);
                    Regenerate();
                });
            }
            menu.ShowAsContext();
        }

        private void DuplicateLayer()
        {
            var layer = SelectedLayer();
            if (layer == null) return;

            Undo.RecordObject(_profile, "Duplicate layer");
            var copy = JsonUtility.FromJson<GhmLayer>(JsonUtility.ToJson(layer));
            copy.id = System.Guid.NewGuid().ToString("N");
            copy.name = layer.name + " copy";
            _profile.layers.Insert(_layerIndex + 1, copy);
            _layerIndex++;
            EditorUtility.SetDirty(_profile);
            Regenerate();
        }

        private void MoveLayer(int delta)
        {
            int target = _layerIndex + delta;
            if (target < 0 || target >= _profile.layers.Count) return;

            Undo.RecordObject(_profile, "Reorder layers");
            var layer = _profile.layers[_layerIndex];
            _profile.layers.RemoveAt(_layerIndex);
            _profile.layers.Insert(target, layer);
            _layerIndex = target;
            EditorUtility.SetDirty(_profile);
            Regenerate();
        }

        private void DeleteLayer()
        {
            var layer = SelectedLayer();
            if (layer == null) return;
            if (!EditorUtility.DisplayDialog("Delete layer", $"Delete '{layer.name}'?", "Delete", "Cancel")) return;

            Undo.RecordObject(_profile, "Delete layer");
            _profile.layers.RemoveAt(_layerIndex);
            _layerIndex = Mathf.Clamp(_layerIndex, 0, Mathf.Max(0, _profile.layers.Count - 1));
            _ruleIndex = -1;
            EditorUtility.SetDirty(_profile);
            Regenerate();
        }

        private GhmLayer SelectedLayer() =>
            _profile != null && _layerIndex >= 0 && _layerIndex < _profile.layers.Count ? _profile.layers[_layerIndex] : null;

        private GhmDecorRule SelectedRule()
        {
            var layer = SelectedLayer();
            if (layer == null || layer.kind != GhmLayerKind.Decor) return null;
            return _ruleIndex >= 0 && _ruleIndex < layer.rules.Count ? layer.rules[_ruleIndex] : null;
        }
    }
}
