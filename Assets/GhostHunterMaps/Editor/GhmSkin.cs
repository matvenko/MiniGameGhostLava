using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Shared look for the window: a small palette, a few lazily built styles, and
    // the two widgets every panel needs (a titled section and a draggable
    // splitter). Kept in one place so the four panels cannot drift apart.
    public static class GhmSkin
    {
        public static readonly Color PanelBack = new Color(0.16f, 0.17f, 0.19f);
        public static readonly Color PanelHeader = new Color(0.21f, 0.22f, 0.25f);
        public static readonly Color Divider = new Color(0.09f, 0.09f, 0.10f);
        public static readonly Color Accent = new Color(0.42f, 0.78f, 0.62f);
        public static readonly Color Warning = new Color(0.95f, 0.72f, 0.32f);
        public static readonly Color GroundSwatch = new Color(0.44f, 0.60f, 0.33f);
        public static readonly Color WaterSwatch = new Color(0.20f, 0.44f, 0.62f);
        public static readonly Color WallSwatch = new Color(0.42f, 0.40f, 0.38f);
        public static readonly Color PathSwatch = new Color(0.76f, 0.63f, 0.44f);

        private static GUIStyle _header;
        private static GUIStyle _sub;
        private static GUIStyle _panel;
        private static GUIStyle _row;
        private static GUIStyle _rowSelected;
        private static GUIStyle _tile;
        private static Texture2D _white;

        public static GUIStyle Header
        {
            get
            {
                if (_header == null)
                {
                    _header = new GUIStyle(EditorStyles.boldLabel)
                    {
                        padding = new RectOffset(8, 8, 4, 4),
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft
                    };
                    _header.normal.textColor = new Color(0.85f, 0.87f, 0.9f);
                }
                return _header;
            }
        }

        public static GUIStyle Sub
        {
            get
            {
                if (_sub == null)
                {
                    _sub = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
                    _sub.normal.textColor = new Color(0.63f, 0.66f, 0.7f);
                }
                return _sub;
            }
        }

        public static GUIStyle Panel
        {
            get
            {
                if (_panel == null) _panel = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 6, 6) };
                return _panel;
            }
        }

        public static GUIStyle Row
        {
            get
            {
                if (_row == null) _row = new GUIStyle(EditorStyles.label) { padding = new RectOffset(6, 6, 3, 3) };
                return _row;
            }
        }

        public static GUIStyle RowSelected
        {
            get
            {
                if (_rowSelected == null)
                {
                    _rowSelected = new GUIStyle(Row);
                    _rowSelected.normal.textColor = Color.white;
                    _rowSelected.fontStyle = FontStyle.Bold;
                }
                return _rowSelected;
            }
        }

        public static GUIStyle Tile
        {
            get
            {
                if (_tile == null)
                {
                    _tile = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.LowerCenter,
                        clipping = TextClipping.Clip,
                        fontSize = 9
                    };
                }
                return _tile;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                }
                return _white;
            }
        }

        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, White);
            GUI.color = previous;
        }

        public static void PanelTitle(string title, string hint = null)
        {
            var rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            Fill(rect, PanelHeader);
            GUI.Label(rect, title, Header);

            if (!string.IsNullOrEmpty(hint))
            {
                var hintRect = new Rect(rect.x, rect.y, rect.width - 8f, rect.height);
                var previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(hintRect, hint, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
                GUI.color = previous;
            }
        }

        public static void Section(string title)
        {
            GUILayout.Space(6f);
            var rect = GUILayoutUtility.GetRect(0f, 16f, GUILayout.ExpandWidth(true));
            GUI.Label(rect, title, EditorStyles.miniBoldLabel);
            var line = new Rect(rect.x, rect.yMax - 1f, rect.width, 1f);
            Fill(line, new Color(1f, 1f, 1f, 0.08f));
        }

        public static Color KindColor(GhmLayerKind kind)
        {
            switch (kind)
            {
                case GhmLayerKind.Ground: return GroundSwatch;
                case GhmLayerKind.Water: return WaterSwatch;
                case GhmLayerKind.Wall: return WallSwatch;
                case GhmLayerKind.Path: return PathSwatch;
                default: return Accent;
            }
        }

        // Drag handle between two panes. Returns the new normalised or absolute
        // position; the caller decides which, this only tracks the delta.
        public static float Splitter(Rect handle, float value, bool vertical, float min, float max, ref bool dragging, int controlId)
        {
            EditorGUIUtility.AddCursorRect(handle, vertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
            Fill(handle, Divider);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (handle.Contains(e.mousePosition) && e.button == 0)
                    {
                        dragging = true;
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (dragging && GUIUtility.hotControl == controlId)
                    {
                        value += vertical ? e.delta.x : e.delta.y;
                        value = Mathf.Clamp(value, min, max);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (dragging && GUIUtility.hotControl == controlId)
                    {
                        dragging = false;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
            return value;
        }

        public static bool ToolbarButton(string label, string tooltip, float width = 0f)
        {
            var content = new GUIContent(label, tooltip);
            return width > 0f
                ? GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(width))
                : GUILayout.Button(content, EditorStyles.toolbarButton);
        }

        public static bool AccentButton(string label, string tooltip, float height = 24f)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = Accent;
            bool clicked = GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(height));
            GUI.backgroundColor = previous;
            return clicked;
        }
    }
}
