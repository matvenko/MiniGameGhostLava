using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GhostHunterMaps.EditorTools
{
    // The one button. Takes the profile that was just previewed and makes the
    // real game scene look like it.
    //
    // The rule this whole tool was written under is that the game's scripts are
    // not edited. So publishing works entirely through things they already
    // expose: it moves the tiles they read, writes their serialized settings
    // through SerializedObject, calls their public rebuild methods, and adds the
    // paths and decor - which they have no notion of - as a separate object.
    //
    // Nothing here is silent. Every change is inside one undo group and the
    // caller gets back a report of exactly what was touched.
    public static class GhmPublisher
    {
        public class Report
        {
            public bool success;
            public readonly List<string> steps = new List<string>();
            public readonly List<string> warnings = new List<string>();

            public string Summary()
            {
                var sb = new StringBuilder();
                foreach (var s in steps) sb.AppendLine("· " + s);
                if (warnings.Count > 0)
                {
                    sb.AppendLine();
                    foreach (var w in warnings) sb.AppendLine("! " + w);
                }
                return sb.ToString().TrimEnd();
            }
        }

        public static Report Publish(GhmMapProfile profile, int level)
        {
            var report = new Report();
            if (profile == null)
            {
                report.warnings.Add("No profile to publish.");
                return report;
            }

            Validate(profile, report);

            if (!EnsureTargetScene(profile, report)) return report;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Publish Ghost Hunter map");

            var board = GhmGenerator.Generate(profile, level);

            EnsureMaterialAssets(profile, report);
            ApplyTiles(profile, board, report);
            ApplySurfaceSettings(profile, level, report);
            ApplyCamera(profile, report);
            if (profile.publishLighting) ApplyLighting(profile, report);
            var binder = EnsureBinder(profile, level, report);

            // The surfaces read the tiles, so they can only rebuild once the
            // tiles are final - and the binder's overlays sit on the ground, so
            // they come last of all.
            RefreshGameSurfaces(report);
            if (binder != null) binder.Rebuild();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Undo.CollapseUndoOperations(undoGroup);

            report.success = true;
            report.steps.Add("Scene marked dirty - save it to keep the published board.");
            return report;
        }

        // ------------------------------------------------------------------

        private static void Validate(GhmMapProfile profile, Report report)
        {
            // The game's surfaces index cells with RoundToInt on raw world
            // distances, which silently assumes a pitch of exactly one unit.
            if (!Mathf.Approximately(profile.cellSize, 1f))
                report.warnings.Add($"Cell size is {profile.cellSize:0.###}. The game's surface components assume 1.0 and will mis-index cells.");

            if (profile.FirstLayer(GhmLayerKind.Ground) == null)
                report.warnings.Add("No Ground layer - the floor will keep whatever material the scene already had.");

            if (profile.bands.Count == 0)
                report.warnings.Add("No level bands defined, so every level will look the same.");

            if (profile.width * profile.height > 2500)
                report.warnings.Add($"{profile.width}x{profile.height} is a big board; generation and rebuilds will be slow.");
        }

        private static bool EnsureTargetScene(GhmMapProfile profile, Report report)
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == profile.targetScenePath) return true;

            if (string.IsNullOrEmpty(profile.targetScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(profile.targetScenePath) == null)
            {
                report.warnings.Add($"Target scene '{profile.targetScenePath}' does not exist.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.warnings.Add("Publish cancelled - the open scene was not saved.");
                return false;
            }

            EditorSceneManager.OpenScene(profile.targetScenePath, OpenSceneMode.Single);
            report.steps.Add($"Opened {profile.targetScenePath}.");
            return true;
        }

        private static void ApplyTiles(GhmMapProfile profile, GhmBoard board, Report report)
        {
            if (!GhmTileGrid.ResolveParents(out var blocks, out var lava, out var walls))
            {
                report.warnings.Add("Could not find the 'Blocks' and 'Lava' parents - the board was not written.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(blocks.gameObject, "Publish board");
            Undo.RegisterFullObjectHierarchyUndo(lava.gameObject, "Publish board");
            if (walls != null) Undo.RegisterFullObjectHierarchyUndo(walls.gameObject, "Publish board");

            var tiles = GhmTileGrid.Apply(profile, board, allowResize: true,
                onCreated: go => Undo.RegisterCreatedObjectUndo(go, "Publish board"));

            foreach (var extra in tiles.removed) Undo.DestroyObjectImmediate(extra.gameObject);

            report.steps.Add($"Board {profile.width}x{profile.height}: {tiles.retyped} tiles set" +
                             $"{(tiles.created.Count > 0 ? $", {tiles.created.Count} added" : "")}" +
                             $"{(tiles.removed.Count > 0 ? $", {tiles.removed.Count} removed" : "")}.");

            var wallLayer = profile.FirstLayer(GhmLayerKind.Wall);
            GhmTileGrid.RebuildWalls(profile, wallLayer, go => Undo.RegisterCreatedObjectUndo(go, "Publish board"));
            report.steps.Add("Border wall rebuilt for the current footprint.");
        }

        // Writes the authored look onto the game's own components. They keep
        // owning the meshes; this only changes the numbers they build them from,
        // which is why the published board matches the preview exactly.
        private static void ApplySurfaceSettings(GhmMapProfile profile, int level, Report report)
        {
            var band = profile.BandForLevel(level);

            var ground = profile.FirstLayer(GhmLayerKind.Ground);
            if (ground != null && GroundSurface.Instance != null)
            {
                var so = new SerializedObject(GroundSurface.Instance);
                SetObject(so, "groundMaterial", profile.ResolveMaterial(ground, band));
                SetInt(so, "facetsPerCell", ground.facetsPerCell);
                SetFloat(so, "heightJitter", ground.heightJitter);
                SetFloat(so, "skirtDepth", ground.skirtDepth);
                SetFloat(so, "colorVariation", ground.colorVariation);
                SetInt(so, "colorZones", ground.colorZones);
                SetFloat(so, "zoneScale", ground.zoneScale);
                SetFloat(so, "zoneContrast", ground.zoneContrast);
                SetFloat(so, "undulation", ground.undulation);
                SetFloat(so, "undulationScale", ground.undulationScale);
                SetFloat(so, "shoreWidth", ground.shoreWidth);
                SetFloat(so, "shoreDip", ground.shoreDip);
                SetFloat(so, "waterClearance", ground.waterClearance);
                so.ApplyModifiedProperties();
                report.steps.Add("Ground surface settings written.");
            }

            var water = profile.FirstLayer(GhmLayerKind.Water);
            if (water != null && LiquidSurface.Instance != null)
            {
                var so = new SerializedObject(LiquidSurface.Instance);
                SetObject(so, "liquidMaterial", profile.ResolveMaterial(water, band));
                SetObject(so, "bedMaterial", band != null && band.bedMaterial != null ? band.bedMaterial : water.bedMaterial);
                SetFloat(so, "surfaceYOffset", water.surfaceYOffset);
                SetFloat(so, "bedDepth", water.bedDepth);
                SetFloat(so, "padding", water.padding);
                SetFloat(so, "verticesPerUnit", water.verticesPerUnit);
                so.ApplyModifiedProperties();
                report.steps.Add("Water surface settings written.");
            }

            var wall = profile.FirstLayer(GhmLayerKind.Wall);
            if (wall != null && WallSurface.Instance != null)
            {
                var so = new SerializedObject(WallSurface.Instance);
                SetObject(so, "wallMaterial", profile.ResolveMaterial(wall, band));
                SetFloat(so, "crestWear", wall.crestWear);
                SetFloat(so, "crestFlatness", wall.crestFlatness);
                SetInt(so, "colorZones", wall.colorZones);
                SetFloat(so, "zoneScale", wall.zoneScale);
                SetFloat(so, "zoneContrast", wall.zoneContrast);
                SetFloat(so, "colorVariation", wall.colorVariation);
                SetBool(so, "castShadows", wall.castShadows);
                so.ApplyModifiedProperties();
                report.steps.Add("Wall surface settings written.");
            }
        }

        // The rig is an offset plus a rotation, so the compression slider has to
        // land in two places: the camera's own transform and the follow script's
        // serialized offset.
        private static void ApplyCamera(GhmMapProfile profile, Report report)
        {
            if (!profile.publishCamera) return;

            var cam = Camera.main;
            if (cam == null)
            {
                report.warnings.Add("No camera tagged MainCamera - camera settings were skipped.");
                return;
            }

            Undo.RecordObject(cam.transform, "Publish camera");
            Undo.RecordObject(cam, "Publish camera");
            cam.transform.rotation = profile.CameraRotation;
            cam.fieldOfView = profile.fieldOfView;

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null)
            {
                report.warnings.Add("Main camera has no CameraFollow - only its rotation was set.");
                return;
            }

            var bounds = profile.FloorBounds;
            var so = new SerializedObject(follow);
            SetVector(so, "offset", profile.CameraOffset);
            SetBool(so, "clampToMap", profile.clampCameraToMap);
            SetFloat(so, "mapMinX", bounds.min.x);
            SetFloat(so, "mapMaxX", bounds.max.x);
            SetFloat(so, "mapMinZ", bounds.min.z);
            SetFloat(so, "mapMaxZ", bounds.max.z);
            so.ApplyModifiedProperties();

            report.steps.Add($"Camera set to {profile.CameraPitch:0.#}° pitch (compression {profile.compression:0.00}), offset {profile.CameraOffset}.");
        }

        private static void ApplyLighting(GhmMapProfile profile, Report report)
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                sun = light;
                break;
            }

            if (sun == null)
            {
                report.warnings.Add("No directional light found - lighting was skipped.");
                return;
            }

            Undo.RecordObject(sun, "Publish lighting");
            Undo.RecordObject(sun.transform, "Publish lighting");
            sun.color = profile.sunColor;
            sun.intensity = profile.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(profile.sunPitch, profile.sunYaw, 0f);

            RenderSettings.ambientLight = profile.ambientColor;
            report.steps.Add("Sun and ambient written.");
        }

        private static GhmRuntimeBinder EnsureBinder(GhmMapProfile profile, int level, Report report)
        {
            var existing = Object.FindAnyObjectByType<GhmRuntimeBinder>();
            if (existing == null)
            {
                var host = new GameObject(GhmRuntimeBinder.HostName);
                Undo.RegisterCreatedObjectUndo(host, "Publish board");
                existing = Undo.AddComponent<GhmRuntimeBinder>(host);
                report.steps.Add($"Added the '{GhmRuntimeBinder.HostName}' runtime binder to the scene.");
            }

            Undo.RecordObject(existing, "Publish board");
            existing.Profile = profile;
            existing.EditorLevel = level;

            var so = new SerializedObject(existing);
            SetObject(so, "profile", profile);
            SetInt(so, "editorLevel", level);
            so.ApplyModifiedProperties();

            report.steps.Add($"Binder pointed at '{profile.name}', showing level {level}.");
            return existing;
        }

        private static void RefreshGameSurfaces(Report report)
        {
            if (LiquidSurface.Instance != null)
            {
                LiquidSurface.Instance.Build();
                LiquidSurface.Instance.Refresh();
            }
            if (GroundSurface.Instance != null) GroundSurface.Instance.Rebuild();
            if (WallSurface.Instance != null) WallSurface.Instance.Rebuild();
            report.steps.Add("Ground, water and wall surfaces rebuilt.");
        }

        // ------------------------------------------------------------------

        // Paths and decor fall back to materials created on the fly from the two
        // built-in shaders. That is fine in the editor and useless in a build:
        // a material made with Shader.Find at runtime finds nothing, because
        // nothing references the shader, so it never gets included.
        //
        // Publishing is therefore the moment those fallbacks become real assets.
        // They are written once and reused afterwards, so tweaking one by hand
        // survives the next publish.
        private static void EnsureMaterialAssets(GhmMapProfile profile, Report report)
        {
            string folder = GhmTextureTools.EnsureFolder("Assets/GhostHunterMaps/Materials");
            int created = 0;

            foreach (var layer in profile.layers)
            {
                if (layer.kind == GhmLayerKind.Path && layer.material == null)
                {
                    layer.material = GetOrCreateMaterial(folder, "GhmPath_" + GhmTextureTools.SafeName(layer.name),
                        GhmSceneBuilder.PathShader, material =>
                        {
                            if (layer.texture != null) material.SetTexture("_BaseMap", layer.texture);
                            material.SetColor("_BaseColor", layer.tint);
                            material.SetFloat("_Opacity", layer.path.opacity);
                        }, ref created);
                }

                if (layer.kind != GhmLayerKind.Decor) continue;

                foreach (var rule in layer.rules)
                {
                    if (rule.source != GhmDecorSource.Texture || rule.materialOverride != null || rule.texture == null) continue;

                    rule.materialOverride = GetOrCreateMaterial(folder, "GhmDecor_" + GhmTextureTools.SafeName(rule.name),
                        GhmSceneBuilder.DecorShader, material => material.SetTexture("_BaseMap", rule.texture), ref created);
                }
            }

            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                report.steps.Add($"Created {created} material asset{(created == 1 ? "" : "s")} under {folder} so the build can find the shaders.");
            }
        }

        private static Material GetOrCreateMaterial(string folder, string name, Shader shader, System.Action<Material> configure, ref int created)
        {
            if (shader == null) return null;

            string path = folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                configure(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var material = new Material(shader) { name = name };
            configure(material);
            AssetDatabase.CreateAsset(material, path);
            created++;
            return material;
        }

        // ------------------------------------------------------------------

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        private static void SetVector(SerializedObject so, string name, Vector3 value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.vector3Value = value;
        }

        private static void SetObject(SerializedObject so, string name, Object value)
        {
            if (value == null) return;
            var p = so.FindProperty(name);
            if (p != null) p.objectReferenceValue = value;
        }

        // ------------------------------------------------------------------

        // Optional convenience: a copy under Resources so a build can find the
        // profile even if the scene reference is ever lost.
        public static void SaveResourcesCopy(GhmMapProfile profile)
        {
            if (profile == null) return;

            string folder = GhmTextureTools.EnsureFolder("Assets/Resources/" + GhmMapProfile.ResourcesFolder);
            string path = folder + "/" + GhmMapProfile.DefaultResourceName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<GhmMapProfile>(path);
            if (existing == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(profile), path);
            }
            else
            {
                EditorUtility.CopySerialized(profile, existing);
                EditorUtility.SetDirty(existing);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
