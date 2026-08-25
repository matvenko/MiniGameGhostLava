using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // Mesh builders for the three board surfaces, ported from the shipped
    // GroundSurface / WallSurface / LiquidSurface components and parameterised by
    // a layer instead of by serialized fields on a scene object.
    //
    // They exist so the editor can draw a real board without touching the game's
    // components (which resolve their input through GameObject.Find in the active
    // scene, and so cannot run inside a preview scene). The maths is copied
    // verbatim - same hash, same noise, same vertex colour layout - because the
    // preview is only honest if it produces the same triangles the game will.
    public static class GhmSurfaceMeshes
    {
        private static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        // ------------------------------------------------------------------
        // Ground
        // ------------------------------------------------------------------

        // The ground's vertex lattice, exposed so anything laid on top of the
        // field - paths, decor, a debug gizmo - can land on exactly the same
        // height instead of guessing and z-fighting against it.
        public static float GroundTopY(GhmMapProfile profile) => profile.floorY + 0.5f * profile.cellSize;
        public static float GroundOriginX(GhmMapProfile profile) => profile.Origin.x - 0.5f * profile.cellSize;
        public static float GroundOriginZ(GhmMapProfile profile) => profile.Origin.z - 0.5f * profile.cellSize;
        public static float GroundStep(GhmMapProfile profile, GhmLayer layer) => profile.cellSize / Mathf.Max(1, layer.facetsPerCell);

        // Height is a pure function of the corner's lattice index, so neighbouring
        // cells agree on shared corners and the surface never cracks open. Three
        // terms: a smooth roll across the whole field (upward only, so it can
        // never eat into the water clearance), per-vertex jitter for the
        // faceting, and a dip into the water so the bank is not a flat table.
        public static float GroundHeightAt(GhmBoard board, GhmMapProfile profile, GhmLayer layer, int i, int j)
        {
            float step = GroundStep(profile, layer);
            float originX = GroundOriginX(profile);
            float originZ = GroundOriginZ(profile);
            float x = originX + i * step;
            float z = originZ + j * step;

            float topY = GroundTopY(profile);
            float minY = topY - profile.waterDrop + layer.waterClearance;

            float roll = GhmNoise.Smooth(x / layer.undulationScale, z / layer.undulationScale) * layer.undulation;
            float jitter = (GhmNoise.Hash01(i, j, 7) - 0.5f) * 2f * layer.heightJitter;
            float shore = SampleShore(board, layer, originX, originZ, profile.cellSize, x, z);
            return Mathf.Max(topY + roll + jitter - shore * layer.shoreDip, minY);
        }

        public static Vector3 GroundVertex(GhmBoard board, GhmMapProfile profile, GhmLayer layer, int i, int j)
        {
            float step = GroundStep(profile, layer);
            return new Vector3(
                GroundOriginX(profile) + i * step,
                GroundHeightAt(board, profile, layer, i, j),
                GroundOriginZ(profile) + j * step);
        }

        public static Mesh BuildGround(GhmBoard board, GhmMapProfile profile, GhmLayer layer)
        {
            var walkable = board.GroundSet();
            if (walkable.Count == 0) return null;

            float topY = GroundTopY(profile);
            float originX = GroundOriginX(profile);
            float originZ = GroundOriginZ(profile);
            int s = Mathf.Max(1, layer.facetsPerCell);

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            float ShoreAt(float worldX, float worldZ) => SampleShore(board, layer, originX, originZ, profile.cellSize, worldX, worldZ);
            Vector3 TopVert(int i, int j) => GroundVertex(board, profile, layer, i, j);

            float bottomY = topY - layer.skirtDepth;

            foreach (var cell in walkable)
            {
                int i0 = cell.x * s, j0 = cell.y * s;

                for (int a = 0; a < s; a++)
                {
                    for (int b = 0; b < s; b++)
                    {
                        int i = i0 + a, j = j0 + b;
                        AddGroundQuad(board, layer, verts, norms, cols, tris,
                            TopVert(i, j), TopVert(i, j + 1), TopVert(i + 1, j + 1), TopVert(i + 1, j),
                            Vector3.up, true, i, j, ShoreAt);
                    }
                }

                foreach (var d in Neighbours)
                {
                    if (walkable.Contains(cell + d)) continue;

                    for (int k = 0; k < s; k++)
                    {
                        int ia, ja, ib, jb;
                        if (d.x != 0)
                        {
                            int i = d.x > 0 ? i0 + s : i0;
                            ia = i; ja = j0 + k;
                            ib = i; jb = j0 + k + 1;
                        }
                        else
                        {
                            int j = d.y > 0 ? j0 + s : j0;
                            ia = i0 + k; ja = j;
                            ib = i0 + k + 1; jb = j;
                        }

                        Vector3 ta = TopVert(ia, ja);
                        Vector3 tb = TopVert(ib, jb);
                        Vector3 ba = new Vector3(ta.x, bottomY, ta.z);
                        Vector3 bb = new Vector3(tb.x, bottomY, tb.z);

                        AddGroundQuad(board, layer, verts, norms, cols, tris, ta, tb, bb, ba,
                            new Vector3(d.x, 0f, d.y), false, ia, ja, ShoreAt);
                    }
                }
            }

            return Finish("GhmGround", verts, norms, cols, tris);
        }

        private static void AddGroundQuad(GhmBoard board, GhmLayer layer,
            List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedNormal, bool isTop, int hi, int hj,
            System.Func<float, float, float> shoreAt)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
            {
                (a, b, c, d) = (d, c, b, a);
            }

            float v0 = Variation(layer, hi, hj, 11);
            float v1 = Variation(layer, hi, hj, 29);

            if (GhmNoise.Hash01(hi, hj, 41) < 0.5f)
            {
                AddGroundTri(layer, verts, norms, cols, tris, a, b, c, v0, isTop, shoreAt);
                AddGroundTri(layer, verts, norms, cols, tris, a, c, d, v1, isTop, shoreAt);
            }
            else
            {
                AddGroundTri(layer, verts, norms, cols, tris, a, b, d, v0, isTop, shoreAt);
                AddGroundTri(layer, verts, norms, cols, tris, b, c, d, v1, isTop, shoreAt);
            }
        }

        // Vertex colour layout read by LowPolyGround_URP:
        //   r = palette band, flat across the triangle
        //   g = shoreline proximity, per vertex
        //   b = per-facet brightness jitter
        //   a = 1 on top faces, 0 on the skirt walls
        private static void AddGroundTri(GhmLayer layer, List<Vector3> verts, List<Vector3> norms,
            List<Color> cols, List<int> tris, Vector3 a, Vector3 b, Vector3 c, float variation, bool isTop,
            System.Func<float, float, float> shoreAt)
        {
            int at = verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            float alpha = isTop ? 1f : 0f;
            float zone = ZoneAt(layer, (a + b + c) / 3f);

            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(n); norms.Add(n); norms.Add(n);
            cols.Add(new Color(zone, shoreAt(a.x, a.z), variation, alpha));
            cols.Add(new Color(zone, shoreAt(b.x, b.z), variation, alpha));
            cols.Add(new Color(zone, shoreAt(c.x, c.z), variation, alpha));
            tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
        }

        // Bilinear read of the board's shore field. Water cells count as half a
        // cell beyond the boundary so the gradient stays continuous right up to
        // the coastline instead of flattening on the last row of cells.
        private static float SampleShore(GhmBoard board, GhmLayer layer, float originX, float originZ, float cellSize, float worldX, float worldZ)
        {
            float cx = (worldX - originX) / cellSize - 0.5f;
            float cz = (worldZ - originZ) / cellSize - 0.5f;
            int i = Mathf.FloorToInt(cx), j = Mathf.FloorToInt(cz);
            float fx = cx - i, fz = cz - j;

            float d00 = DistAt(board, i, j), d10 = DistAt(board, i + 1, j);
            float d01 = DistAt(board, i, j + 1), d11 = DistAt(board, i + 1, j + 1);
            float d = Mathf.Lerp(Mathf.Lerp(d00, d10, fx), Mathf.Lerp(d01, d11, fx), fz);

            return Mathf.Clamp01(1f - d / Mathf.Max(layer.shoreWidth, 0.01f));
        }

        private static float DistAt(GhmBoard board, int i, int j) =>
            board.IsGround(i, j) ? board.ShoreAt(i, j) : -0.5f;

        private static float Variation(GhmLayer layer, int i, int j, int salt) =>
            Mathf.Clamp01(0.5f + (GhmNoise.Hash01(i, j, salt) - 0.5f) * layer.colorVariation);

        private static float ZoneAt(GhmLayer layer, Vector3 centre)
        {
            float n = GhmNoise.Fbm(centre.x / layer.zoneScale, centre.z / layer.zoneScale);
            n = Mathf.Clamp01((n - 0.5f) * layer.zoneContrast + 0.5f);
            int steps = Mathf.Max(1, layer.colorZones - 1);
            return Mathf.Round(n * steps) / steps;
        }

        // ------------------------------------------------------------------
        // Wall
        // ------------------------------------------------------------------

        // The border ring, as a merged shell. Cells are generated from the board
        // footprint rather than read back from cubes, so resizing the map moves
        // the rampart with it.
        public static Mesh BuildWall(GhmMapProfile profile, GhmLayer layer)
        {
            if (profile.wallMargin <= 0) return null;

            var cells = WallCells(profile, layer, out Vector3 origin, out Vector3 cellSize);
            if (cells.Count == 0) return null;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            int topLevel = int.MinValue;
            foreach (var c in cells) topLevel = Mathf.Max(topLevel, c.y);
            float topY = origin.y + topLevel * cellSize.y + cellSize.y * 0.5f;

            foreach (var c in cells)
            {
                Vector3 centre = origin + new Vector3(c.x * cellSize.x, c.y * cellSize.y, c.z * cellSize.z);

                foreach (var d in Faces)
                {
                    if (cells.Contains(c + d)) continue;

                    Vector3 n = new Vector3(d.x, d.y, d.z);
                    GetFaceAxes(d, out Vector3 u, out Vector3 v);

                    Vector3 half = new Vector3(n.x * cellSize.x, n.y * cellSize.y, n.z * cellSize.z) * 0.5f;
                    Vector3 du = new Vector3(u.x * cellSize.x, u.y * cellSize.y, u.z * cellSize.z) * 0.5f;
                    Vector3 dv = new Vector3(v.x * cellSize.x, v.y * cellSize.y, v.z * cellSize.z) * 0.5f;

                    Vector3 a = centre + half - du - dv;
                    Vector3 b = centre + half - du + dv;
                    Vector3 e = centre + half + du + dv;
                    Vector3 f = centre + half + du - dv;

                    AddWallQuad(layer, verts, norms, cols, tris,
                        Wear(layer, a, topY, origin, cellSize), Wear(layer, b, topY, origin, cellSize),
                        Wear(layer, e, topY, origin, cellSize), Wear(layer, f, topY, origin, cellSize), n, d.y > 0);
                }
            }

            return Finish("GhmWall", verts, norms, cols, tris);
        }

        // The ring the game ships with: one cell of margin around the floor, two
        // rows high, the lower row overlapping so the rampart has some thickness
        // where it meets the ground.
        public static HashSet<Vector3Int> WallCells(GhmMapProfile profile, GhmLayer layer, out Vector3 origin, out Vector3 cellSize)
        {
            int rows = Mathf.Max(1, layer != null ? layer.wallRows : 2);
            float pitchY = 0.9022552f;

            var cells = new HashSet<Vector3Int>();
            int margin = Mathf.Max(1, profile.wallMargin);

            // Row 0 is the visible crest, sitting one unit above the floor's top.
            origin = new Vector3(
                profile.CellToWorld(0, 0, false).x - margin * profile.cellSize,
                profile.floorY + 1f - (rows - 1) * pitchY,
                profile.CellToWorld(0, 0, false).z - margin * profile.cellSize);
            cellSize = new Vector3(profile.cellSize, pitchY, profile.cellSize);

            int w = profile.width + margin * 2;
            int h = profile.height + margin * 2;

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool insideFloor = x >= margin && x < margin + profile.width && z >= margin && z < margin + profile.height;
                    if (insideFloor) continue;
                    for (int y = 0; y < rows; y++) cells.Add(new Vector3Int(x, y, z));
                }
            }

            return cells;
        }

        private static readonly Vector3Int[] Faces =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        private static void GetFaceAxes(Vector3Int d, out Vector3 u, out Vector3 v)
        {
            if (d.y != 0) { u = Vector3.right; v = Vector3.forward; }
            else if (d.x != 0) { u = Vector3.forward; v = Vector3.up; }
            else { u = Vector3.right; v = Vector3.up; }
        }

        // Only vertices sitting exactly on the crest move, and only downwards, so
        // a worn top edge can never punch a hole through the wall below it.
        private static Vector3 Wear(GhmLayer layer, Vector3 p, float topY, Vector3 origin, Vector3 cell)
        {
            if (layer.crestWear <= 0f || Mathf.Abs(p.y - topY) > 0.001f) return p;

            int i = Mathf.RoundToInt((p.x - origin.x) / cell.x * 2f);
            int k = Mathf.RoundToInt((p.z - origin.z) / cell.z * 2f);

            float h = GhmNoise.Hash01(i, k, 17);
            if (h < layer.crestFlatness) return p;

            float t = (h - layer.crestFlatness) / Mathf.Max(1f - layer.crestFlatness, 0.0001f);
            p.y -= t * layer.crestWear;
            return p;
        }

        private static void AddWallQuad(GhmLayer layer, List<Vector3> verts, List<Vector3> norms,
            List<Color> cols, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 expectedNormal, bool isCrest)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
            {
                (a, b, c, d) = (d, c, b, a);
            }

            Vector3 centre = (a + b + c + d) * 0.25f;
            var col = new Color(WallZoneAt(layer, centre), 0f, WallJitter(layer, centre), isCrest ? 1f : 0f);

            AddWallTri(verts, norms, cols, tris, a, b, c, expectedNormal, col);
            AddWallTri(verts, norms, cols, tris, a, c, d, expectedNormal, col);
        }

        private static void AddWallTri(List<Vector3> verts, List<Vector3> norms, List<Color> cols,
            List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 fallbackNormal, Color col)
        {
            int at = verts.Count;
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : fallbackNormal.normalized;

            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(n); norms.Add(n); norms.Add(n);
            cols.Add(col); cols.Add(col); cols.Add(col);
            tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
        }

        private static float WallZoneAt(GhmLayer layer, Vector3 centre)
        {
            float n = GhmNoise.Fbm(centre.x / layer.zoneScale, (centre.y + centre.z) / layer.zoneScale);
            n = Mathf.Clamp01((n - 0.5f) * layer.zoneContrast + 0.5f);
            int steps = Mathf.Max(1, layer.colorZones - 1);
            return Mathf.Round(n * steps) / steps;
        }

        private static float WallJitter(GhmLayer layer, Vector3 centre)
        {
            float h = GhmNoise.Hash01(Mathf.RoundToInt(centre.x * 97f), Mathf.RoundToInt((centre.y + centre.z) * 97f), 23);
            return Mathf.Clamp01(0.5f + (h - 0.5f) * layer.colorVariation);
        }

        // ------------------------------------------------------------------
        // Liquid
        // ------------------------------------------------------------------

        // One subdivided quad over the whole footprint, parked at the top of the
        // recessed water tiles. The floor is opaque and spans that height, so it
        // punches the liquid out everywhere except the pools.
        public static Mesh BuildLiquid(GhmMapProfile profile, GhmLayer layer, out Vector3 center, out float surfaceY)
        {
            var bounds = profile.FloorBounds;
            float sizeX = bounds.size.x + layer.padding * 2f;
            float sizeZ = bounds.size.z + layer.padding * 2f;

            center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            surfaceY = profile.floorY + 0.5f * profile.cellSize - profile.waterDrop + layer.surfaceYOffset;

            int density = Mathf.Max(1, Mathf.RoundToInt(layer.verticesPerUnit));
            return BuildGridMesh(sizeX, sizeZ, Mathf.Max(1, Mathf.CeilToInt(sizeX * density)), Mathf.Max(1, Mathf.CeilToInt(sizeZ * density)));
        }

        public static Mesh BuildLiquidBed(GhmMapProfile profile, GhmLayer layer)
        {
            var bounds = profile.FloorBounds;
            return BuildGridMesh(bounds.size.x + layer.padding * 2f, bounds.size.z + layer.padding * 2f, 1, 1);
        }

        public static Mesh BuildGridMesh(float sizeX, float sizeZ, int cols, int rows)
        {
            var verts = new Vector3[(cols + 1) * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var normals = new Vector3[verts.Length];
            var tris = new int[cols * rows * 6];

            for (int z = 0; z <= rows; z++)
            {
                for (int x = 0; x <= cols; x++)
                {
                    int i = z * (cols + 1) + x;
                    float u = (float)x / cols;
                    float v = (float)z / rows;
                    verts[i] = new Vector3((u - 0.5f) * sizeX, 0f, (v - 0.5f) * sizeZ);
                    uvs[i] = new Vector2(u, v);
                    normals[i] = Vector3.up;
                }
            }

            int t = 0;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int bl = z * (cols + 1) + x;
                    int tl = bl + cols + 1;
                    tris[t++] = bl; tris[t++] = tl; tris[t++] = tl + 1;
                    tris[t++] = bl; tris[t++] = tl + 1; tris[t++] = bl + 1;
                }
            }

            var mesh = new Mesh { name = "GhmLiquidPlane" };
            if (verts.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh Finish(string name, List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris)
        {
            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
