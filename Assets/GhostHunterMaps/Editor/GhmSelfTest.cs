using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GhostHunterMaps.EditorTools
{
    // Headless checks for the parts that have to be right and are invisible when
    // they are wrong: a board that is quietly split in two, a mesh that comes
    // back empty, a decor rule that scatters into the water, camera maths that
    // does not match the compression it claims to.
    //
    // Run from the menu, or in batch with
    //   -executeMethod GhostHunterMaps.EditorTools.GhmSelfTest.RunAll
    public static class GhmSelfTest
    {
        private static int _failures;
        private static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Window/Ghost Hunter Maps Self-test")]
        public static void RunFromMenu()
        {
            Run();
            EditorUtility.DisplayDialog("Ghost Hunter Maps self-test",
                _failures == 0 ? "All checks passed.\n\n" + Log : $"{_failures} check(s) failed.\n\n" + Log, "OK");
        }

        public static void RunAll()
        {
            Run();
            Debug.Log(Log.ToString());
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        // Same checks plus the ones that need the real game scene open. Separate
        // because opening a scene is not something a menu click should do behind
        // the user's back.
        public static void RunAllWithScene()
        {
            Run();
            CheckSceneBootstrap();
            Log.AppendLine(_failures == 0 ? "RESULT (with scene): all checks passed." : $"RESULT (with scene): {_failures} failed.");
            Debug.Log(Log.ToString());
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        // Reading the shipped scene back into a profile has to reproduce it
        // exactly - that is the whole promise of bootstrapping, and it is the
        // only check here that touches the real board.
        private static void CheckSceneBootstrap()
        {
            const string scenePath = "Assets/LavaScene.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Log.AppendLine("  --   " + scenePath + " not present, scene checks skipped");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            const string profilePath = "Assets/GhostHunterMaps/Profiles/LavaSceneMap.asset";
            if (AssetDatabase.LoadAssetAtPath<GhmMapProfile>(profilePath) != null) AssetDatabase.DeleteAsset(profilePath);

            var profile = GhmBootstrap.CreateFromScene(out string message);
            if (profile == null) { Fail("bootstrap from the scene: " + message); return; }

            Check(profile.width == 15 && profile.height == 10, $"bootstrap measured the board as 15x10 (got {profile.width}x{profile.height})");
            Check(Mathf.Abs(profile.floorY - (-0.08f)) < 0.01f, $"bootstrap read the floor height (got {profile.floorY:0.###})");
            Check(Mathf.Abs(profile.waterDrop - 0.18f) < 0.01f, $"bootstrap read the water recess (got {profile.waterDrop:0.###})");
            Check(profile.wallMargin == 1, $"bootstrap read a one-cell wall margin (got {profile.wallMargin})");

            var wall = profile.FirstLayer(GhmLayerKind.Wall);
            Check(wall.wallRows == 2, $"bootstrap read two wall rows (got {wall.wallRows})");

            var ground = profile.FirstLayer(GhmLayerKind.Ground);
            var water = profile.FirstLayer(GhmLayerKind.Water);
            Check(ground.material != null, "bootstrap picked up the ground material");
            Check(water.material != null, "bootstrap picked up the water material");
            Check(wall.material != null, "bootstrap picked up the wall material");
            Check(ground.facetsPerCell == 3, $"bootstrap read the scene's facet count (got {ground.facetsPerCell}, scene says 3)");

            // Every existing tile has to map onto a cell of the measured board,
            // or publishing would move the level rather than rewrite it.
            GhmTileGrid.ResolveParents(out var blocks, out var lava, out _);
            int strays = 0, tiles = 0;
            foreach (Transform t in blocks) { tiles++; if (!InBounds(profile, t)) strays++; }
            foreach (Transform t in lava) { tiles++; if (!InBounds(profile, t)) strays++; }
            Check(strays == 0, $"all {tiles} existing tiles map onto the measured grid ({strays} strays)");
            Check(tiles == profile.width * profile.height, $"the tile count matches the measured board ({tiles} tiles for {profile.width * profile.height} cells)");

            var board = GhmGenerator.Generate(profile, 1);
            Check(board.IsFullyConnected(), "a board generated from the bootstrapped profile is fully connected");

            Log.AppendLine("  ok   profile saved at " + AssetDatabase.GetAssetPath(profile));
        }

        private static bool InBounds(GhmMapProfile profile, Transform tile)
        {
            var cell = GhmTileGrid.WorldToCell(profile, tile.position);
            return cell.x >= 0 && cell.y >= 0 && cell.x < profile.width && cell.y < profile.height;
        }

        private static void Run()
        {
            _failures = 0;
            Log.Clear();

            var profile = ScriptableObject.CreateInstance<GhmMapProfile>();
            profile.EnsureDefaults();

            try
            {
                CheckCameraMaths(profile);
                CheckGridMaths(profile);
                CheckGeneration(profile);
                CheckResize(profile);
                CheckMeshes(profile);
                CheckLevelBands(profile);
            }
            catch (Exception e)
            {
                Fail("threw: " + e);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }

            Log.AppendLine(_failures == 0 ? "RESULT: all checks passed." : $"RESULT: {_failures} failed.");
        }

        // ------------------------------------------------------------------

        // Compression is defined as the factor the board is foreshortened by, so
        // it has to equal cos(tilt) exactly - otherwise the plan view and the
        // rendered view disagree about the same number.
        private static void CheckCameraMaths(GhmMapProfile profile)
        {
            profile.compression = 1f;
            profile.cameraHeight = 7f;
            Check(Mathf.Abs(profile.CameraPitch - 90f) < 0.01f, $"compression 1 gives a straight-down pitch (got {profile.CameraPitch:0.###})");
            Check(Mathf.Abs(profile.CameraOffset.z) < 0.01f, $"compression 1 gives no pull-back (got {profile.CameraOffset.z:0.###})");

            profile.compression = 0.5f;
            float expectedPitch = 90f - Mathf.Acos(0.5f) * Mathf.Rad2Deg;
            Check(Mathf.Abs(profile.CameraPitch - expectedPitch) < 0.01f, $"compression 0.5 gives a 30 degree pitch (got {profile.CameraPitch:0.###})");

            float expectedBack = profile.cameraHeight / Mathf.Tan(profile.CameraPitch * Mathf.Deg2Rad);
            Check(Mathf.Abs(profile.CameraOffset.z + expectedBack) < 0.01f, "the pull-back matches the pitch");
            Check(Mathf.Abs(Mathf.Cos(profile.TiltFromVertical * Mathf.Deg2Rad) - 0.5f) < 0.001f, "tilt is the arccos of the compression");

            profile.compression = 1f;
        }

        // The board has to stay centred as it is resized, and it has to land on
        // the same world coordinates the scene's existing tiles occupy.
        private static void CheckGridMaths(GhmMapProfile profile)
        {
            profile.width = 15;
            profile.height = 10;
            profile.boardCenter = new Vector2(1.52f, 1f);
            profile.cellSize = 1f;
            profile.floorY = -0.08f;

            var corner = profile.CellToWorld(0, 0, false);
            Check(Mathf.Abs(corner.x - (-5.48f)) < 0.001f, $"cell 0 sits at the scene's first column (got {corner.x:0.###}, expected -5.48)");
            Check(Mathf.Abs(corner.z - (-3.5f)) < 0.001f, $"cell 0 sits at the scene's first row (got {corner.z:0.###}, expected -3.5)");

            var far = profile.CellToWorld(14, 9, false);
            Check(Mathf.Abs(far.x - 8.52f) < 0.001f, $"the last column matches the scene (got {far.x:0.###}, expected 8.52)");
            Check(Mathf.Abs(far.z - 5.5f) < 0.001f, $"the last row matches the scene (got {far.z:0.###}, expected 5.5)");

            var water = profile.CellToWorld(3, 3, true);
            Check(Mathf.Abs(water.y - (profile.floorY - profile.waterDrop)) < 0.0001f, "water tiles are recessed by the drop");

            for (int z = 0; z < profile.height; z++)
            {
                for (int x = 0; x < profile.width; x++)
                {
                    var cell = GhmTileGrid.WorldToCell(profile, profile.CellToWorld(x, z, false));
                    if (cell.x == x && cell.y == z) continue;
                    Fail($"cell ({x},{z}) did not survive the world round-trip (came back as {cell})");
                    return;
                }
            }
            Pass("every cell survives the world round-trip");
        }

        // The promise every algorithm makes: a floor you can walk all of, with
        // no specks and no puddles too small to read as water.
        private static void CheckGeneration(GhmMapProfile profile)
        {
            var sizes = new[] { new Vector2Int(15, 10), new Vector2Int(8, 8), new Vector2Int(40, 24) };

            foreach (GhmAlgorithm algorithm in Enum.GetValues(typeof(GhmAlgorithm)))
            {
                foreach (var size in sizes)
                {
                    profile.width = size.x;
                    profile.height = size.y;

                    foreach (var band in profile.bands) band.algorithm = algorithm;

                    for (int level = 1; level <= 12; level += 3)
                    {
                        var board = GhmGenerator.Generate(profile, level);
                        string what = $"{algorithm} {size.x}x{size.y} L{level}";

                        if (!board.IsFullyConnected()) { Fail(what + ": the floor is split into unreachable pieces"); continue; }

                        float share = board.GroundCount / (float)(board.width * board.height);
                        if (share < profile.minWalkableShare - 0.02f) { Fail(what + $": only {share:P0} walkable, below the {profile.minWalkableShare:P0} floor"); continue; }

                        var bandForLevel = profile.BandForLevel(level);
                        int smallestPool = int.MaxValue;
                        foreach (var pool in board.Regions(GhmCell.Water)) smallestPool = Mathf.Min(smallestPool, pool.Count);
                        if (smallestPool != int.MaxValue && smallestPool < bandForLevel.minPoolSize)
                        {
                            Fail(what + $": left a {smallestPool}-cell puddle, under the {bandForLevel.minPoolSize} minimum");
                            continue;
                        }

                        foreach (var cell in board.pathRoute)
                        {
                            if (board.IsGround(cell)) continue;
                            Fail(what + $": a path runs across water at {cell}");
                            break;
                        }
                    }
                }
            }

            // Same seed, same map - the whole "preview it here, build it there"
            // arrangement rests on this.
            profile.width = 15;
            profile.height = 10;
            var a1 = GhmGenerator.Generate(profile, 4);
            var a2 = GhmGenerator.Generate(profile, 4);
            bool identical = a1.GroundCount == a2.GroundCount && a1.decor.Count == a2.decor.Count;
            for (int i = 0; identical && i < a1.cells.Length; i++) identical = a1.cells[i] == a2.cells[i];
            Check(identical, "generation is deterministic for a given seed and level");

            var b1 = GhmGenerator.Generate(profile, 5);
            bool differs = false;
            for (int i = 0; i < a1.cells.Length && !differs; i++) differs = a1.cells[i] != b1.cells[i];
            Check(differs, "a different level produces a different layout");

            Pass($"{Enum.GetValues(typeof(GhmAlgorithm)).Length} algorithms x 3 sizes x 4 levels generated cleanly");
        }

        private static void CheckResize(GhmMapProfile profile)
        {
            foreach (var size in new[] { new Vector2Int(4, 4), new Vector2Int(60, 40), new Vector2Int(15, 10) })
            {
                profile.width = size.x;
                profile.height = size.y;

                var board = GhmGenerator.Generate(profile, 1);
                Check(board.width == size.x && board.height == size.y, $"a {size.x}x{size.y} board comes back the size it was asked for");

                var bounds = profile.FloorBounds;
                Check(Mathf.Abs(bounds.center.x - profile.boardCenter.x) < 0.001f && Mathf.Abs(bounds.center.z - profile.boardCenter.y) < 0.001f,
                    $"a {size.x}x{size.y} board stays centred on the same point");

                var wallLayer = profile.FirstLayer(GhmLayerKind.Wall);
                var walls = GhmSurfaceMeshes.WallCells(profile, wallLayer, out _, out _);
                int perimeter = (size.x + profile.wallMargin * 2) * (size.y + profile.wallMargin * 2) - size.x * size.y;
                Check(walls.Count == perimeter * wallLayer.wallRows, $"the {size.x}x{size.y} wall ring has the right number of cells");
            }
        }

        private static void CheckMeshes(GhmMapProfile profile)
        {
            profile.width = 15;
            profile.height = 10;

            var ground = profile.FirstLayer(GhmLayerKind.Ground);
            var water = profile.FirstLayer(GhmLayerKind.Water);
            var wall = profile.FirstLayer(GhmLayerKind.Wall);
            var path = profile.FirstLayer(GhmLayerKind.Path);
            var decorLayer = profile.FirstLayer(GhmLayerKind.Decor);

            var texture = new Texture2D(4, 4);
            foreach (var rule in decorLayer.rules) rule.texture = texture;

            var board = GhmGenerator.Generate(profile, 1);

            var groundMesh = GhmSurfaceMeshes.BuildGround(board, profile, ground);
            Check(groundMesh != null && groundMesh.vertexCount > 0, "the ground mesh builds");

            var wallMesh = GhmSurfaceMeshes.BuildWall(profile, wall);
            Check(wallMesh != null && wallMesh.vertexCount > 0, "the wall mesh builds");

            var liquid = GhmSurfaceMeshes.BuildLiquid(profile, water, out _, out float surfaceY);
            Check(liquid != null && liquid.vertexCount > 0, "the water plane builds");
            Check(surfaceY < GhmSurfaceMeshes.GroundTopY(profile), "the water surface sits below the floor's top");

            var pathMesh = GhmOverlayMeshes.BuildPath(board, profile, ground, path);
            Check(pathMesh != null && pathMesh.vertexCount > 0, "the path overlay builds");

            // The whole point of the path skin: it has to land on the ground's
            // own lattice, not near it.
            if (groundMesh != null && pathMesh != null)
            {
                float maxGap = 0f;
                var verts = pathMesh.vertices;
                for (int i = 0; i < verts.Length; i += 7)
                {
                    float groundY = GhmOverlayMeshes.GroundHeightAtWorld(board, profile, ground, verts[i].x, verts[i].z);
                    maxGap = Mathf.Max(maxGap, Mathf.Abs(verts[i].y - groundY - path.path.yOffset - path.yOffset));
                }
                Check(maxGap < 0.02f, $"the path skin hugs the ground (worst gap {maxGap:0.####})");
            }

            int decorMeshes = 0;
            for (int li = 0; li < profile.layers.Count; li++)
            {
                if (profile.layers[li].kind != GhmLayerKind.Decor) continue;
                for (int ri = 0; ri < profile.layers[li].rules.Count; ri++)
                {
                    var mesh = GhmOverlayMeshes.BuildDecorBatch(board, profile, ground, li, ri, profile.layers[li].rules[ri]);
                    if (mesh != null && mesh.vertexCount > 0) decorMeshes++;
                }
            }
            Check(decorMeshes > 0, $"decor batches build ({decorMeshes} of {decorLayer.rules.Count} rules placed something)");

            // Placement masks are the difference between a garden and a mess.
            int offBoard = 0, inWater = 0;
            foreach (var placement in board.decor)
            {
                var cell = GhmTileGrid.WorldToCell(profile, placement.position);
                if (!board.InBounds(cell.x, cell.y)) offBoard++;
                else if (!board.IsGround(cell)) inWater++;
            }
            Check(offBoard == 0, $"no decor lands off the board (found {offBoard})");
            Check(inWater == 0, $"no decor lands in the water (found {inWater})");

            var shoreRule = decorLayer.rules.Find(r => (r.placement & GhmPlacement.Shore) != 0);
            if (shoreRule != null)
            {
                int index = decorLayer.rules.IndexOf(shoreRule);
                int strays = 0, total = 0;
                foreach (var placement in board.decor)
                {
                    if (placement.ruleIndex != index) continue;
                    total++;
                    var cell = GhmTileGrid.WorldToCell(profile, placement.position);
                    if (board.ShoreAt(cell.x, cell.y) > shoreRule.shoreBand + 1f) strays++;
                }
                Check(strays == 0, $"the shore rule stayed on the shore ({total} placed, {strays} inland)");
            }

            foreach (var mesh in new[] { groundMesh, wallMesh, liquid, pathMesh }) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void CheckLevelBands(GhmMapProfile profile)
        {
            var covered = new List<int>();
            for (int level = 1; level <= 30; level++)
            {
                var band = profile.BandForLevel(level);
                if (band == null) { Fail($"level {level} matches no band at all"); return; }
                covered.Add(level);
            }
            Check(covered.Count == 30, "every level from 1 to 30 resolves to a band");

            Check(profile.BandForLevel(1).name != profile.BandForLevel(20).name, "distant levels land in different bands");
        }

        // ------------------------------------------------------------------

        private static void Check(bool condition, string what)
        {
            if (condition) Pass(what);
            else Fail(what);
        }

        private static void Pass(string what) => Log.AppendLine("  ok   " + what);

        private static void Fail(string what)
        {
            _failures++;
            Log.AppendLine("  FAIL " + what);
            Debug.LogError("[Ghost Hunter Maps self-test] " + what);
        }
    }
}
