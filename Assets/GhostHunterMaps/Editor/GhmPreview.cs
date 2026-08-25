using System;
using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // The middle of the window: the generated map, either as the real thing or as
    // a plan.
    //
    // Scene mode builds the board into an off-screen preview scene with the same
    // mesh code and the same materials the published level uses, so what is on
    // screen here is what the game will show. Plan mode draws the grid flat and
    // fast - it is the view you actually read a labyrinth in, and it is where the
    // compression setting is most obvious, because the plan is squashed by
    // exactly the factor the tilted camera would squash it by.
    public class GhmPreview : IDisposable
    {
        public enum Mode { Scene, Plan }

        public Mode mode = Mode.Scene;
        public bool showGrid = true;
        public bool showDecor = true;
        public bool showPaths = true;
        public float zoom = 1f;
        public float yawOffset = 0f;
        public Vector2 pan = Vector2.zero;

        private PreviewRenderUtility _pru;
        private GhmSceneBuilder.Result _result;
        private int _builtHash = -1;

        public string Status { get; private set; } = string.Empty;

        private void EnsureUtility(GhmMapProfile profile)
        {
            if (_pru != null) return;

            _pru = new PreviewRenderUtility();
            _pru.camera.clearFlags = CameraClearFlags.SolidColor;
            _pru.camera.backgroundColor = new Color(0.11f, 0.12f, 0.14f, 1f);
            _pru.camera.nearClipPlane = 0.05f;
            _pru.camera.farClipPlane = 500f;
            _pru.camera.cameraType = CameraType.Preview;

            if (_pru.lights.Length > 0)
            {
                _pru.lights[0].type = LightType.Directional;
                _pru.lights[0].intensity = profile != null ? profile.sunIntensity : 1.1f;
                _pru.lights[0].color = profile != null ? profile.sunColor : Color.white;
                _pru.lights[0].shadows = LightShadows.Soft;
            }
            if (_pru.lights.Length > 1)
            {
                _pru.lights[1].intensity = 0.35f;
                _pru.lights[1].color = new Color(0.6f, 0.68f, 0.85f);
            }
        }

        // Rebuilding is expensive enough that it must not happen every repaint;
        // the hash is what the window changed since the last build.
        public void Invalidate() => _builtHash = -1;

        public void Rebuild(GhmBoard board, GhmMapProfile profile, int level, int hash)
        {
            if (hash == _builtHash) return;
            _builtHash = hash;

            EnsureUtility(profile);
            GhmSceneBuilder.Dispose(_result);
            _result = null;

            if (board == null || profile == null) return;

            _result = GhmSceneBuilder.Build(null, board, profile, level,
                includeSurfaces: true,
                includePaths: showPaths,
                includeDecor: showDecor,
                flags: HideFlags.HideAndDontSave);

            if (_result.root != null) _pru.AddSingleGO(_result.root);

            Status = $"{board.GroundCount} floor / {board.width * board.height - board.GroundCount} water cells" +
                     $"  ·  {_result.decorInstances} decor  ·  {board.pathRoute.Count} path cells";
        }

        public void Draw(Rect rect, GhmBoard board, GhmMapProfile profile)
        {
            // Navigation runs on every event; drawing only on Repaint, because
            // BeginPreview throws outside it.
            HandleNavigation(rect);

            if (mode == Mode.Plan || _pru == null)
            {
                DrawPlan(rect, board, profile);
                if (mode == Mode.Plan) return;
            }

            if (board == null || profile == null || _result == null || _result.root == null) return;
            if (Event.current.type != EventType.Repaint) return;

            FrameCamera(profile, board, rect);

            if (_pru.lights.Length > 0)
            {
                _pru.lights[0].transform.rotation = Quaternion.Euler(profile.sunPitch, profile.sunYaw, 0f);
                _pru.lights[0].color = profile.sunColor;
                _pru.lights[0].intensity = profile.sunIntensity;
            }
            _pru.ambientColor = profile.ambientColor;

            _pru.BeginPreview(rect, GUIStyle.none);
            _pru.Render(true, false);
            var texture = _pru.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        // The rig the game will use, pulled back far enough to hold the whole
        // board. Its angle is the profile's, so tilting for the preview and
        // tilting for the game are the same control.
        private void FrameCamera(GhmMapProfile profile, GhmBoard board, Rect rect)
        {
            var bounds = profile.FloorBounds;
            float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f + 2f;
            float vertical = radius / Mathf.Tan(profile.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = Mathf.Max(0.2f, rect.width / Mathf.Max(1f, rect.height));
            if (aspect < 1f) vertical /= aspect;

            float distance = Mathf.Max(2f, vertical / Mathf.Max(0.2f, zoom));
            var rotation = Quaternion.Euler(profile.CameraPitch, profile.cameraYaw + yawOffset, 0f);
            Vector3 focus = bounds.center + rotation * new Vector3(pan.x, 0f, pan.y);

            _pru.camera.fieldOfView = profile.fieldOfView;
            _pru.camera.transform.rotation = rotation;
            _pru.camera.transform.position = focus - rotation * Vector3.forward * distance;
        }

        private void HandleNavigation(Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (1f - e.delta.y * 0.03f), 0.25f, 4f);
                e.Use();
                GUI.changed = true;
            }
            else if (e.type == EventType.MouseDrag && e.button == 0)
            {
                yawOffset += e.delta.x * 0.4f;
                e.Use();
                GUI.changed = true;
            }
            else if (e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 1 && e.alt)))
            {
                pan -= new Vector2(e.delta.x, -e.delta.y) * 0.03f;
                e.Use();
                GUI.changed = true;
            }
        }

        // ------------------------------------------------------------------
        // Plan view
        // ------------------------------------------------------------------

        private void DrawPlan(Rect rect, GhmBoard board, GhmMapProfile profile)
        {
            GhmSkin.Fill(rect, new Color(0.10f, 0.11f, 0.13f));
            if (board == null || profile == null) return;

            // Cells are squashed vertically by the compression factor: this is
            // literally what the tilted camera does to the board, so the plan and
            // the rendered view agree about the shape you are designing.
            float squash = Mathf.Clamp(profile.compression, 0.3f, 1f);
            int margin = Mathf.Max(1, profile.wallMargin);
            float cols = board.width + margin * 2f;
            float rows = (board.height + margin * 2f) * squash;

            float cell = Mathf.Min((rect.width - 24f) / cols, (rect.height - 24f) / rows) * zoom;
            cell = Mathf.Max(2f, cell);

            float totalW = cols * cell;
            float totalH = rows * cell;
            float ox = rect.x + (rect.width - totalW) * 0.5f;
            float oy = rect.y + (rect.height - totalH) * 0.5f;

            Rect CellRect(float x, float z)
            {
                // Z grows away from the camera, so the plan is drawn bottom-up to
                // match what the rendered view shows.
                float top = oy + (rows - (z + margin + 1f) * squash) * cell;
                return new Rect(ox + (x + margin) * cell, top, cell, cell * squash);
            }

            GhmSkin.Fill(new Rect(ox, oy, totalW, totalH), GhmSkin.WallSwatch * 0.55f);

            for (int z = 0; z < board.height; z++)
            {
                for (int x = 0; x < board.width; x++)
                {
                    var r = CellRect(x, z);
                    bool ground = board.IsGround(x, z);
                    Color color;

                    if (ground)
                    {
                        float shore = Mathf.Clamp01(board.ShoreAt(x, z) / 3f);
                        color = Color.Lerp(new Color(0.55f, 0.63f, 0.36f), GhmSkin.GroundSwatch * 0.85f, shore);
                    }
                    else
                    {
                        color = GhmSkin.WaterSwatch;
                    }

                    GhmSkin.Fill(r, color);

                    if (showPaths && ground)
                    {
                        float p = board.PathAt(x, z);
                        if (p > 0.01f) GhmSkin.Fill(r, new Color(GhmSkin.PathSwatch.r, GhmSkin.PathSwatch.g, GhmSkin.PathSwatch.b, p * 0.9f));
                    }

                    if (showGrid && cell > 6f)
                    {
                        GhmSkin.Fill(new Rect(r.x, r.y, r.width, 1f), new Color(0f, 0f, 0f, 0.12f));
                        GhmSkin.Fill(new Rect(r.x, r.y, 1f, r.height), new Color(0f, 0f, 0f, 0.12f));
                    }
                }
            }

            if (showDecor)
            {
                float dot = Mathf.Max(2f, cell * 0.22f);
                foreach (var d in board.decor)
                {
                    var cellPos = GhmTileGrid.WorldToCell(profile, d.position);
                    float fx = (d.position.x - profile.Origin.x) / profile.cellSize;
                    float fz = (d.position.z - profile.Origin.z) / profile.cellSize;
                    if (!board.InBounds(cellPos.x, cellPos.y)) continue;

                    var r = CellRect(fx, fz);
                    var centre = new Rect(r.x + r.width * 0.5f - dot * 0.5f, r.y + r.height * 0.5f - dot * 0.5f, dot, dot);
                    GhmSkin.Fill(centre, DecorColor(d.layerIndex, d.ruleIndex));
                }
            }
        }

        private static Color DecorColor(int layerIndex, int ruleIndex)
        {
            float h = ((layerIndex * 7 + ruleIndex * 3) % 12) / 12f;
            return Color.HSVToRGB(h, 0.55f, 1f);
        }

        public void Dispose()
        {
            GhmSceneBuilder.Dispose(_result);
            _result = null;
            if (_pru != null)
            {
                _pru.Cleanup();
                _pru = null;
            }
        }
    }
}
