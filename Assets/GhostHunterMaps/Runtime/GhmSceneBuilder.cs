using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // Turns a generated board into GameObjects. One builder for both consumers:
    // the editor preview asks for the whole board including the three surfaces,
    // the published scene asks only for the overlays because the game's own
    // components already draw the floor, water and wall.
    //
    // Everything it creates is flagged DontSave and parented under a single
    // root, so a rebuild is "delete the root, call this again" and nothing can
    // leak into the saved scene.
    public static class GhmSceneBuilder
    {
        public const string RootName = "GhostHunterMaps_Generated";
        public const string PathsName = "Paths";
        public const string DecorName = "Decor";
        public const string SurfacesName = "Surfaces";

        public static Shader PathShader => Shader.Find("GhostHunterMaps/PathOverlay");
        public static Shader DecorShader => Shader.Find("GhostHunterMaps/DecorSprite");

        public class Result
        {
            public GameObject root;
            public readonly List<Object> temporaries = new List<Object>();
            public int decorInstances;
            public int pathCells;
        }

        // includeSurfaces is the difference between "show me the whole board"
        // (preview) and "add what the game cannot draw on its own" (publish).
        public static Result Build(Transform parent, GhmBoard board, GhmMapProfile profile, int level,
            bool includeSurfaces, bool includePaths, bool includeDecor, HideFlags flags)
        {
            var result = new Result();
            var band = profile.BandForLevel(level);

            var root = new GameObject(RootName) { hideFlags = flags };
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            result.root = root;

            if (includeSurfaces) BuildSurfaces(root.transform, board, profile, band, level, flags, result);
            if (includePaths) BuildPaths(root.transform, board, profile, band, level, flags, result);
            if (includeDecor) BuildDecor(root.transform, board, profile, band, level, flags, result);

            return result;
        }

        private static void BuildSurfaces(Transform root, GhmBoard board, GhmMapProfile profile,
            GhmLevelBand band, int level, HideFlags flags, Result result)
        {
            var host = new GameObject(SurfacesName) { hideFlags = flags };
            host.transform.SetParent(root, false);

            var ground = FirstActive(profile, GhmLayerKind.Ground, level);
            if (ground != null)
            {
                var mesh = GhmSurfaceMeshes.BuildGround(board, profile, ground);
                if (mesh != null)
                {
                    result.temporaries.Add(mesh);
                    var go = CreateRenderer(host.transform, "Ground", mesh,
                        profile.ResolveMaterial(ground, band), flags);
                    ApplyTint(go, profile.ResolveTint(ground, band));
                    go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            var water = FirstActive(profile, GhmLayerKind.Water, level);
            if (water != null)
            {
                var mesh = GhmSurfaceMeshes.BuildLiquid(profile, water, out Vector3 centre, out float surfaceY);
                if (mesh != null)
                {
                    result.temporaries.Add(mesh);
                    var go = CreateRenderer(host.transform, "Water", mesh,
                        profile.ResolveMaterial(water, band), flags);
                    go.transform.position = new Vector3(centre.x, surfaceY, centre.z);
                    ApplyTint(go, profile.ResolveTint(water, band));

                    var bedMaterial = band != null && band.bedMaterial != null ? band.bedMaterial : water.bedMaterial;
                    if (bedMaterial != null)
                    {
                        var bed = GhmSurfaceMeshes.BuildLiquidBed(profile, water);
                        result.temporaries.Add(bed);
                        var bedGo = CreateRenderer(host.transform, "WaterBed", bed, bedMaterial, flags);
                        bedGo.transform.position = new Vector3(centre.x, surfaceY - water.bedDepth, centre.z);
                    }
                }
            }

            var wall = FirstActive(profile, GhmLayerKind.Wall, level);
            if (wall != null)
            {
                var mesh = GhmSurfaceMeshes.BuildWall(profile, wall);
                if (mesh != null)
                {
                    result.temporaries.Add(mesh);
                    var go = CreateRenderer(host.transform, "Wall", mesh,
                        profile.ResolveMaterial(wall, band), flags);
                    ApplyTint(go, profile.ResolveTint(wall, band));
                    go.GetComponent<MeshRenderer>().shadowCastingMode = wall.castShadows
                        ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private static void BuildPaths(Transform root, GhmBoard board, GhmMapProfile profile,
            GhmLevelBand band, int level, HideFlags flags, Result result)
        {
            if (band != null && !band.drawPaths) return;
            if (board.pathRoute.Count == 0) return;

            var ground = FirstActive(profile, GhmLayerKind.Ground, level);
            var host = new GameObject(PathsName) { hideFlags = flags };
            host.transform.SetParent(root, false);

            foreach (var layer in profile.LayersOfKind(GhmLayerKind.Path, level))
            {
                var mesh = GhmOverlayMeshes.BuildPath(board, profile, ground, layer);
                if (mesh == null) continue;
                result.temporaries.Add(mesh);
                result.pathCells += board.pathRoute.Count;

                var material = profile.ResolveMaterial(layer, band);
                if (material == null)
                {
                    material = new Material(PathShader) { name = "GhmPath_" + layer.name, hideFlags = HideFlags.HideAndDontSave };
                    if (layer.texture != null) material.SetTexture("_BaseMap", layer.texture);
                    material.SetColor("_BaseColor", layer.tint);
                    material.SetFloat("_Opacity", layer.path.opacity);
                    result.temporaries.Add(material);
                }

                var go = CreateRenderer(host.transform, layer.name, mesh, material, flags);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static void BuildDecor(Transform root, GhmBoard board, GhmMapProfile profile,
            GhmLevelBand band, int level, HideFlags flags, Result result)
        {
            if (board.decor.Count == 0) return;

            var ground = FirstActive(profile, GhmLayerKind.Ground, level);
            var host = new GameObject(DecorName) { hideFlags = flags };
            host.transform.SetParent(root, false);

            for (int li = 0; li < profile.layers.Count; li++)
            {
                var layer = profile.layers[li];
                if (layer.kind != GhmLayerKind.Decor || !layer.ActiveAtLevel(level)) continue;

                var layerHost = new GameObject(layer.name) { hideFlags = flags };
                layerHost.transform.SetParent(host.transform, false);

                for (int ri = 0; ri < layer.rules.Count; ri++)
                {
                    var rule = layer.rules[ri];
                    if (!rule.enabled) continue;

                    if (rule.source == GhmDecorSource.Prefab)
                    {
                        result.decorInstances += SpawnPrefabs(layerHost.transform, board, profile, ground, li, ri, rule, flags);
                        continue;
                    }

                    var mesh = GhmOverlayMeshes.BuildDecorBatch(board, profile, ground, li, ri, rule);
                    if (mesh == null) continue;
                    result.temporaries.Add(mesh);
                    result.decorInstances += mesh.vertexCount / 4;

                    var material = rule.materialOverride;
                    if (material == null)
                    {
                        material = new Material(DecorShader) { name = "GhmDecor_" + rule.name, hideFlags = HideFlags.HideAndDontSave };
                        if (rule.texture != null) material.SetTexture("_BaseMap", rule.texture);
                        result.temporaries.Add(material);
                    }

                    CreateRenderer(layerHost.transform, rule.name, mesh, material, flags);
                }
            }
        }

        private static int SpawnPrefabs(Transform parent, GhmBoard board, GhmMapProfile profile, GhmLayer ground,
            int layerIndex, int ruleIndex, GhmDecorRule rule, HideFlags flags)
        {
            int count = 0;
            foreach (var placement in board.decor)
            {
                if (placement.layerIndex != layerIndex || placement.ruleIndex != ruleIndex) continue;

                var instance = Object.Instantiate(rule.prefab, parent);
                instance.hideFlags = flags;
                Vector3 pos = placement.position;
                pos.y = GhmOverlayMeshes.GroundHeightAtWorld(board, profile, ground, pos.x, pos.z) + rule.yOffset;
                instance.transform.position = pos;
                instance.transform.rotation = Quaternion.Euler(0f, placement.yaw, 0f);
                instance.transform.localScale = Vector3.one * placement.scale;
                count++;
            }
            return count;
        }

        private static GameObject CreateRenderer(Transform parent, string name, Mesh mesh, Material material, HideFlags flags)
        {
            var go = new GameObject(name) { hideFlags = flags };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.receiveShadows = true;
            return go;
        }

        // Tint is applied through a property block rather than by instancing the
        // material: the band's tint is a preview/authoring knob, and cloning the
        // user's material here would quietly disconnect it from the asset.
        private static void ApplyTint(GameObject go, Color tint)
        {
            if (tint == Color.white) return;
            var mr = go.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            mr.SetPropertyBlock(block);
        }

        private static GhmLayer FirstActive(GhmMapProfile profile, GhmLayerKind kind, int level)
        {
            foreach (var l in profile.LayersOfKind(kind, level)) return l;
            return null;
        }

        // Meshes and materials created here are not owned by any asset, so they
        // survive as leaked objects unless they are destroyed explicitly.
        public static void Dispose(Result result)
        {
            if (result == null) return;

            if (result.root != null) DestroyObject(result.root);
            foreach (var t in result.temporaries) DestroyObject(t);
            result.temporaries.Clear();
            result.root = null;
        }

        public static void DestroyObject(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
