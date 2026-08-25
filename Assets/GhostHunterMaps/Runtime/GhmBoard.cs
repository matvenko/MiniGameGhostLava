using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // One placed decorative instance, resolved down to world space. The rule
    // index points back into the layer's rule list so the builder knows which
    // texture and material to draw it with.
    public struct GhmDecorPlacement
    {
        public int layerIndex;
        public int ruleIndex;
        public Vector3 position;
        public float yaw;
        public float scale;
        public Color tint;
    }

    // The generated map: which cells are what, plus the derived fields the
    // surfaces and the scatterer read (distance to water, path strength). Pure
    // data - it knows nothing about GameObjects, so the same board drives the
    // preview and the published scene.
    public class GhmBoard
    {
        public int width;
        public int height;
        public int level = 1;
        public GhmCell[] cells;
        public float[] shore;   // cells to the nearest water, 0.5 at the shoreline
        public float[] path;    // 0..1 path strength
        public readonly List<GhmDecorPlacement> decor = new List<GhmDecorPlacement>();
        public readonly List<Vector2Int> pathRoute = new List<Vector2Int>();

        public static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        public GhmBoard(int width, int height)
        {
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            cells = new GhmCell[this.width * this.height];
            shore = new float[this.width * this.height];
            path = new float[this.width * this.height];
        }

        public int Index(int x, int z) => z * width + x;
        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;

        public GhmCell At(int x, int z) => InBounds(x, z) ? cells[Index(x, z)] : GhmCell.Water;
        public void Set(int x, int z, GhmCell c) { if (InBounds(x, z)) cells[Index(x, z)] = c; }

        public bool IsGround(int x, int z) => At(x, z) == GhmCell.Ground;
        public bool IsGround(Vector2Int c) => IsGround(c.x, c.y);

        public float ShoreAt(int x, int z) => InBounds(x, z) ? shore[Index(x, z)] : 0f;
        public float PathAt(int x, int z) => InBounds(x, z) ? path[Index(x, z)] : 0f;

        public int GroundCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < cells.Length; i++) if (cells[i] == GhmCell.Ground) n++;
                return n;
            }
        }

        public List<Vector2Int> GroundCells()
        {
            var list = new List<Vector2Int>();
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                    if (cells[Index(x, z)] == GhmCell.Ground) list.Add(new Vector2Int(x, z));
            return list;
        }

        public HashSet<Vector2Int> GroundSet()
        {
            var set = new HashSet<Vector2Int>();
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                    if (cells[Index(x, z)] == GhmCell.Ground) set.Add(new Vector2Int(x, z));
            return set;
        }

        // Distance in cells from every floor cell to the water, the same field
        // GroundSurface builds: cells touching water sit half a cell from the
        // boundary line, which is where the shoreline shading hits full strength.
        public void RebuildShoreField()
        {
            var frontier = new Queue<Vector2Int>();
            for (int i = 0; i < shore.Length; i++) shore[i] = float.MaxValue;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (cells[Index(x, z)] != GhmCell.Ground) continue;
                    bool touchesWater = false;
                    for (int d = 0; d < Dirs.Length; d++)
                    {
                        var n = new Vector2Int(x + Dirs[d].x, z + Dirs[d].y);
                        if (!InBounds(n.x, n.y) || cells[Index(n.x, n.y)] != GhmCell.Ground) { touchesWater = true; break; }
                    }
                    if (touchesWater)
                    {
                        shore[Index(x, z)] = 0.5f;
                        frontier.Enqueue(new Vector2Int(x, z));
                    }
                }
            }

            while (frontier.Count > 0)
            {
                var c = frontier.Dequeue();
                float next = shore[Index(c.x, c.y)] + 1f;
                for (int d = 0; d < Dirs.Length; d++)
                {
                    var n = new Vector2Int(c.x + Dirs[d].x, c.y + Dirs[d].y);
                    if (!InBounds(n.x, n.y) || cells[Index(n.x, n.y)] != GhmCell.Ground) continue;
                    if (shore[Index(n.x, n.y)] <= next) continue;
                    shore[Index(n.x, n.y)] = next;
                    frontier.Enqueue(n);
                }
            }

            for (int i = 0; i < shore.Length; i++) if (shore[i] == float.MaxValue) shore[i] = 0f;
        }

        // Every floor cell reachable from every other one - the rule that keeps
        // a level completable. Reused as the accept test while carving.
        public bool IsFullyConnected()
        {
            var start = new Vector2Int(-1, -1);
            int total = 0;
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                    if (cells[Index(x, z)] == GhmCell.Ground)
                    {
                        if (start.x < 0) start = new Vector2Int(x, z);
                        total++;
                    }

            if (total == 0) return false;

            var seen = new HashSet<Vector2Int> { start };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                for (int d = 0; d < Dirs.Length; d++)
                {
                    var n = new Vector2Int(c.x + Dirs[d].x, c.y + Dirs[d].y);
                    if (!InBounds(n.x, n.y) || cells[Index(n.x, n.y)] != GhmCell.Ground || seen.Contains(n)) continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            return seen.Count == total;
        }

        // Connected components of one cell type, used by the cleanup pass to
        // find specks of floor and puddles too small to read as water.
        public List<List<Vector2Int>> Regions(GhmCell kind)
        {
            var result = new List<List<Vector2Int>>();
            var seen = new bool[cells.Length];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = Index(x, z);
                    if (seen[i] || cells[i] != kind) continue;

                    var region = new List<Vector2Int>();
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(new Vector2Int(x, z));
                    seen[i] = true;

                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        region.Add(c);
                        for (int d = 0; d < Dirs.Length; d++)
                        {
                            var n = new Vector2Int(c.x + Dirs[d].x, c.y + Dirs[d].y);
                            if (!InBounds(n.x, n.y)) continue;
                            int ni = Index(n.x, n.y);
                            if (seen[ni] || cells[ni] != kind) continue;
                            seen[ni] = true;
                            q.Enqueue(n);
                        }
                    }
                    result.Add(region);
                }
            }
            return result;
        }
    }
}
