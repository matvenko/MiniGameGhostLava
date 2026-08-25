using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // The bridge to the board the game actually plays on.
    //
    // The shipped level is a set of 1x1 cubes parented under "Blocks" and "Lava".
    // They are the colliders and the pathfinding graph; their rendering is
    // suppressed by the surface components. So publishing a map means moving
    // those cubes between the two parents, adding or removing them when the
    // board is resized, and letting the game's own surfaces rebuild themselves
    // from the result. Nothing here reaches into the game's scripts.
    public static class GhmTileGrid
    {
        public const string BlocksParent = "Blocks";
        public const string LavaParent = "Lava";
        public const string WallsParent = "Walls";
        public const string CoinsParent = "Coins";

        public class Tiles
        {
            public Transform blocks;
            public Transform lava;
            public Transform walls;
            public readonly List<Transform> created = new List<Transform>();
            public readonly List<Transform> removed = new List<Transform>();
            public int retyped;
        }

        public static bool ResolveParents(out Transform blocks, out Transform lava, out Transform walls)
        {
            var b = GameObject.Find(BlocksParent);
            var l = GameObject.Find(LavaParent);
            var w = GameObject.Find(WallsParent);
            blocks = b != null ? b.transform : null;
            lava = l != null ? l.transform : null;
            walls = w != null ? w.transform : null;
            return blocks != null && lava != null;
        }

        // Bring the cube set in line with the board: one tile per cell, in the
        // parent that matches the cell's type, at that type's height.
        //
        // Existing cubes are reused wherever possible rather than deleted and
        // respawned, so anything else in the scene that points at a tile keeps
        // pointing at a live object.
        public static Tiles Apply(GhmMapProfile profile, GhmBoard board, bool allowResize, System.Action<GameObject> onCreated = null)
        {
            var result = new Tiles();
            if (!ResolveParents(out var blocks, out var lava, out var walls)) return result;

            result.blocks = blocks;
            result.lava = lava;
            result.walls = walls;

            var pool = new List<Transform>();
            foreach (Transform t in blocks) pool.Add(t);
            foreach (Transform t in lava) pool.Add(t);
            if (pool.Count == 0) return result;

            var template = pool[0];
            var byCell = new Dictionary<Vector2Int, Transform>();
            var spare = new List<Transform>();

            foreach (var t in pool)
            {
                var cell = WorldToCell(profile, t.position);
                if (board.InBounds(cell.x, cell.y) && !byCell.ContainsKey(cell)) byCell[cell] = t;
                else spare.Add(t);
            }

            for (int z = 0; z < board.height; z++)
            {
                for (int x = 0; x < board.width; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!byCell.TryGetValue(cell, out var tile))
                    {
                        if (spare.Count > 0)
                        {
                            tile = spare[spare.Count - 1];
                            spare.RemoveAt(spare.Count - 1);
                        }
                        else
                        {
                            if (!allowResize) continue;
                            var clone = Object.Instantiate(template.gameObject, template.parent);
                            clone.name = template.name;
                            tile = clone.transform;
                            result.created.Add(tile);
                            onCreated?.Invoke(clone);
                        }
                        byCell[cell] = tile;
                    }

                    bool water = board.At(x, z) == GhmCell.Water;
                    ApplyTileType(tile, water, blocks, lava, profile.CellToWorld(x, z, water));
                    result.retyped++;
                }
            }

            // Anything the new footprint has no room for. Left for the caller to
            // destroy so the editor can route it through Undo.
            foreach (var t in spare) result.removed.Add(t);
            return result;
        }

        // The type switch the game performs when it reshuffles a level: material,
        // collider shape, hazard component, height and parent. Kept identical so
        // a published board behaves exactly like one the game generated itself.
        public static void ApplyTileType(Transform tile, bool water, Transform blocksParent, Transform lavaParent, Vector3 position)
        {
            var collider = tile.GetComponent<BoxCollider>();
            var hazard = tile.GetComponent<LavaHazard>();

            if (water)
            {
                if (collider != null)
                {
                    collider.center = new Vector3(0f, 0.05f, 0f);
                    collider.size = new Vector3(1.18f, 1.3f, 1.18f);
                    collider.isTrigger = true;
                }
                if (hazard == null) tile.gameObject.AddComponent<LavaHazard>();
                tile.position = position;
                if (tile.parent != lavaParent) tile.SetParent(lavaParent, true);
            }
            else
            {
                if (collider != null)
                {
                    collider.center = Vector3.zero;
                    collider.size = Vector3.one;
                    collider.isTrigger = false;
                }
                if (hazard != null) GhmSceneBuilder.DestroyObject(hazard);
                tile.position = position;
                if (tile.parent != blocksParent) tile.SetParent(blocksParent, true);
            }
        }

        public static Vector2Int WorldToCell(GhmMapProfile profile, Vector3 world)
        {
            Vector3 origin = profile.Origin;
            return new Vector2Int(
                Mathf.RoundToInt((world.x - origin.x) / profile.cellSize),
                Mathf.RoundToInt((world.z - origin.z) / profile.cellSize));
        }

        // Rebuild the border ring for the current footprint. Only touched when
        // the map is resized, since the wall never changes otherwise.
        public static void RebuildWalls(GhmMapProfile profile, GhmLayer wallLayer, System.Action<GameObject> onCreated = null)
        {
            if (!ResolveParents(out _, out _, out var walls) || walls == null) return;
            if (walls.childCount == 0) return;

            var template = walls.GetChild(0);
            var wanted = GhmSurfaceMeshes.WallCells(profile, wallLayer, out Vector3 origin, out Vector3 cellSize);

            var existing = new List<Transform>();
            foreach (Transform t in walls) existing.Add(t);

            int index = 0;
            foreach (var cell in wanted)
            {
                Transform tile;
                if (index < existing.Count)
                {
                    tile = existing[index];
                }
                else
                {
                    var clone = Object.Instantiate(template.gameObject, walls);
                    clone.name = template.name;
                    tile = clone.transform;
                    onCreated?.Invoke(clone);
                }
                index++;

                tile.position = origin + new Vector3(cell.x * cellSize.x, cell.y * cellSize.y, cell.z * cellSize.z);
            }

            for (int i = existing.Count - 1; i >= index; i--)
            {
                GhmSceneBuilder.DestroyObject(existing[i].gameObject);
            }
        }

        // After a layout change, anything standing where the floor used to be is
        // now standing in water. Coins would be unreachable and the player would
        // die on spawn, so everything gets nudged to the nearest floor cell.
        public static int ResettleEntities(GhmMapProfile profile, GhmBoard board)
        {
            int moved = 0;

            var coins = GameObject.Find(CoinsParent);
            if (coins != null)
            {
                foreach (Transform coin in coins.transform)
                    if (MoveToGround(profile, board, coin)) moved++;
            }

            var player = Object.FindAnyObjectByType<Sample.GhostScript>();
            if (player != null && MoveToGround(profile, board, player.transform)) moved++;

            foreach (var enemy in Object.FindObjectsByType<EnemyChaser>())
                if (MoveToGround(profile, board, enemy.transform)) moved++;

            foreach (var friendly in Object.FindObjectsByType<FriendlyGhostFlee>())
                if (MoveToGround(profile, board, friendly.transform)) moved++;

            return moved;
        }

        private static bool MoveToGround(GhmMapProfile profile, GhmBoard board, Transform t)
        {
            var cell = WorldToCell(profile, t.position);
            if (board.IsGround(cell)) return false;

            if (!TryFindNearestGround(board, cell, out var target)) return false;

            Vector3 world = profile.CellToWorld(target.x, target.y, false);
            t.position = new Vector3(world.x, t.position.y, world.z);
            return true;
        }

        private static bool TryFindNearestGround(GhmBoard board, Vector2Int from, out Vector2Int found)
        {
            found = from;
            int maxRing = Mathf.Max(board.width, board.height);

            for (int r = 0; r <= maxRing; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        var c = new Vector2Int(from.x + dx, from.y + dz);
                        if (!board.IsGround(c)) continue;
                        found = c;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
