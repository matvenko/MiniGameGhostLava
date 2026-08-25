using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Reads the board that is already in the scene back into a profile.
    //
    // Starting from an empty profile means the first publish would overwrite a
    // hand-tuned level with defaults. Starting from the scene means the opposite:
    // publishing immediately after bootstrapping changes nothing, and every
    // setting in the editor begins at the value the game is actually using. From
    // there each change is a deliberate step away from a known-good board.
    public static class GhmBootstrap
    {
        [MenuItem("Window/Ghost Hunter Maps: profile from open scene")]
        public static void CreateFromSceneMenu()
        {
            var profile = CreateFromScene(out string message);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Ghost Hunter Maps", message, "OK");
                return;
            }

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            GhmWindow.Open();
            EditorUtility.DisplayDialog("Ghost Hunter Maps", message, "OK");
        }

        public static GhmMapProfile CreateFromScene(out string message)
        {
            if (!GhmTileGrid.ResolveParents(out var blocks, out var lava, out var walls))
            {
                message = "This scene has no 'Blocks' and 'Lava' parents, so there is no board to read.";
                return null;
            }

            var profile = ScriptableObject.CreateInstance<GhmMapProfile>();
            profile.EnsureDefaults();

            int waterCells = ReadBoard(profile, blocks, lava);
            if (waterCells < 0)
            {
                Object.DestroyImmediate(profile);
                message = "The 'Blocks' and 'Lava' parents are empty.";
                return null;
            }

            ReadWalls(profile, walls);
            ReadSurfaces(profile);
            ReadCamera(profile);
            ReadLighting(profile);

            float density = waterCells / (float)Mathf.Max(1, profile.width * profile.height);
            for (int i = 0; i < profile.bands.Count; i++)
                profile.bands[i].waterDensity = Mathf.Clamp(density * (1f + 0.12f * i), 0f, 0.6f);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            profile.targetScenePath = scene.path;
            profile.name = scene.name + "Map";

            string folder = GhmTextureTools.EnsureFolder("Assets/GhostHunterMaps/Profiles");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + profile.name + ".asset");
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();

            message = $"Read the board out of '{scene.name}':\n\n"
                    + $"· {profile.width} x {profile.height} cells, {waterCells} of them water ({density:P0})\n"
                    + $"· floor at Y {profile.floorY:0.###}, water recessed {profile.waterDrop:0.###}\n"
                    + $"· wall margin {profile.wallMargin}, {profile.FirstLayer(GhmLayerKind.Wall).wallRows} rows\n"
                    + $"· camera compression {profile.compression:0.00} ({profile.CameraPitch:0.#}° pitch)\n\n"
                    + $"Saved as {path}. Publishing it now would leave the scene as it is.";
            return profile;
        }

        // ------------------------------------------------------------------

        // The footprint is measured from where the tiles actually are, so the
        // profile lands on the same world coordinates rather than near them.
        private static int ReadBoard(GhmMapProfile profile, Transform blocks, Transform lava)
        {
            var floor = new List<Transform>();
            var water = new List<Transform>();
            foreach (Transform t in blocks) floor.Add(t);
            foreach (Transform t in lava) water.Add(t);
            if (floor.Count + water.Count == 0) return -1;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var t in floor) Accumulate(t, ref minX, ref maxX, ref minZ, ref maxZ);
            foreach (var t in water) Accumulate(t, ref minX, ref maxX, ref minZ, ref maxZ);

            profile.cellSize = 1f;
            profile.width = Mathf.RoundToInt(maxX - minX) + 1;
            profile.height = Mathf.RoundToInt(maxZ - minZ) + 1;
            profile.boardCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);

            profile.floorY = floor.Count > 0 ? floor[0].position.y : 0f;
            profile.waterDrop = water.Count > 0 ? profile.floorY - water[0].position.y : 0.18f;

            return water.Count;
        }

        private static void Accumulate(Transform t, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            minX = Mathf.Min(minX, t.position.x);
            maxX = Mathf.Max(maxX, t.position.x);
            minZ = Mathf.Min(minZ, t.position.z);
            maxZ = Mathf.Max(maxZ, t.position.z);
        }

        private static void ReadWalls(GhmMapProfile profile, Transform walls)
        {
            if (walls == null || walls.childCount == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            var levels = new HashSet<int>();
            foreach (Transform t in walls)
            {
                Accumulate(t, ref minX, ref maxX, ref minZ, ref maxZ);
                levels.Add(Mathf.RoundToInt(t.position.y * 100f));
            }

            int spanX = Mathf.RoundToInt(maxX - minX) + 1;
            profile.wallMargin = Mathf.Clamp((spanX - profile.width) / 2, 0, 4);

            var wall = profile.FirstLayer(GhmLayerKind.Wall);
            if (wall != null) wall.wallRows = Mathf.Clamp(levels.Count, 1, 4);
        }

        // The reverse of what the publisher writes, so a bootstrap-then-publish
        // round trip is a no-op.
        private static void ReadSurfaces(GhmMapProfile profile)
        {
            var ground = profile.FirstLayer(GhmLayerKind.Ground);
            if (ground != null && GroundSurface.Instance != null)
            {
                var so = new SerializedObject(GroundSurface.Instance);
                ground.material = GetObject(so, "groundMaterial") as Material;
                ground.facetsPerCell = GetInt(so, "facetsPerCell", ground.facetsPerCell);
                ground.heightJitter = GetFloat(so, "heightJitter", ground.heightJitter);
                ground.skirtDepth = GetFloat(so, "skirtDepth", ground.skirtDepth);
                ground.colorVariation = GetFloat(so, "colorVariation", ground.colorVariation);
                ground.colorZones = GetInt(so, "colorZones", ground.colorZones);
                ground.zoneScale = GetFloat(so, "zoneScale", ground.zoneScale);
                ground.zoneContrast = GetFloat(so, "zoneContrast", ground.zoneContrast);
                ground.undulation = GetFloat(so, "undulation", ground.undulation);
                ground.undulationScale = GetFloat(so, "undulationScale", ground.undulationScale);
                ground.shoreWidth = GetFloat(so, "shoreWidth", ground.shoreWidth);
                ground.shoreDip = GetFloat(so, "shoreDip", ground.shoreDip);
                ground.waterClearance = GetFloat(so, "waterClearance", ground.waterClearance);
            }

            var water = profile.FirstLayer(GhmLayerKind.Water);
            if (water != null && LiquidSurface.Instance != null)
            {
                var so = new SerializedObject(LiquidSurface.Instance);
                water.material = GetObject(so, "liquidMaterial") as Material;
                water.bedMaterial = GetObject(so, "bedMaterial") as Material;
                water.surfaceYOffset = GetFloat(so, "surfaceYOffset", water.surfaceYOffset);
                water.bedDepth = GetFloat(so, "bedDepth", water.bedDepth);
                water.padding = GetFloat(so, "padding", water.padding);
                water.verticesPerUnit = GetFloat(so, "verticesPerUnit", water.verticesPerUnit);
            }

            var wall = profile.FirstLayer(GhmLayerKind.Wall);
            if (wall != null && WallSurface.Instance != null)
            {
                var so = new SerializedObject(WallSurface.Instance);
                wall.material = GetObject(so, "wallMaterial") as Material;
                wall.crestWear = GetFloat(so, "crestWear", wall.crestWear);
                wall.crestFlatness = GetFloat(so, "crestFlatness", wall.crestFlatness);
                wall.colorZones = GetInt(so, "colorZones", wall.colorZones);
                wall.zoneScale = GetFloat(so, "zoneScale", wall.zoneScale);
                wall.zoneContrast = GetFloat(so, "zoneContrast", wall.zoneContrast);
                wall.colorVariation = GetFloat(so, "colorVariation", wall.colorVariation);
                wall.castShadows = GetBool(so, "castShadows", wall.castShadows);
            }
        }

        // compression is sin(pitch): a camera looking straight down (pitch 90)
        // foreshortens nothing, which is a compression of 1.
        private static void ReadCamera(GhmMapProfile profile)
        {
            var cam = Camera.main;
            if (cam == null) return;

            profile.fieldOfView = cam.fieldOfView;

            float pitch = cam.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            profile.compression = Mathf.Clamp(Mathf.Sin(Mathf.Abs(pitch) * Mathf.Deg2Rad), 0.3f, 1f);

            float yaw = cam.transform.eulerAngles.y;
            profile.cameraYaw = yaw > 180f ? yaw - 360f : yaw;

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) return;

            var so = new SerializedObject(follow);
            var offset = so.FindProperty("offset");
            if (offset != null) profile.cameraHeight = Mathf.Clamp(offset.vector3Value.y, 3f, 30f);
            profile.clampCameraToMap = GetBool(so, "clampToMap", profile.clampCameraToMap);
        }

        private static void ReadLighting(GhmMapProfile profile)
        {
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                profile.sunColor = light.color;
                profile.sunIntensity = light.intensity;

                var euler = light.transform.eulerAngles;
                profile.sunPitch = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, 0f, 90f);
                profile.sunYaw = euler.y > 180f ? euler.y - 360f : euler.y;
                break;
            }
            profile.ambientColor = RenderSettings.ambientLight;
        }

        // ------------------------------------------------------------------

        private static float GetFloat(SerializedObject so, string name, float fallback)
        {
            var p = so.FindProperty(name);
            return p != null ? p.floatValue : fallback;
        }

        private static int GetInt(SerializedObject so, string name, int fallback)
        {
            var p = so.FindProperty(name);
            return p != null ? p.intValue : fallback;
        }

        private static bool GetBool(SerializedObject so, string name, bool fallback)
        {
            var p = so.FindProperty(name);
            return p != null ? p.boolValue : fallback;
        }

        private static Object GetObject(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            return p != null ? p.objectReferenceValue : null;
        }
    }
}
