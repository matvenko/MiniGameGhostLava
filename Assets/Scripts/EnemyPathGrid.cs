using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shared walkable-tile graph used by every EnemyChaser instead of each one
// separately doing an O(n^2) graph build and an O(n) linear nearest-node
// scan every frame. Built once (lazily) and reused; call Rebuild() after
// generating a new level so it doesn't keep pathing on the old map.
public class EnemyPathGrid
{
    private static EnemyPathGrid _instance;
    public static EnemyPathGrid Instance => _instance ??= new EnemyPathGrid();

    private static readonly List<Vector3> EmptyList = new List<Vector3>();

    private readonly List<Vector3> _nodes = new List<Vector3>();
    private readonly Dictionary<Vector3, List<Vector3>> _adjacency = new Dictionary<Vector3, List<Vector3>>();
    private readonly Dictionary<Vector2Int, Vector3> _cellLookup = new Dictionary<Vector2Int, Vector3>();
    private float _originX;
    private float _originZ;
    private bool _built;
    private Scene _builtForScene;

    public IReadOnlyList<Vector3> AllNodes => _nodes;

    // This is a plain static singleton, so it outlives scene loads - after a
    // restart the old level's grid would otherwise still be here, marked
    // built, describing tiles that are now lava. Tracking which scene
    // instance it was built for makes a reload rebuild it.
    public void EnsureBuilt()
    {
        if (!_built || _builtForScene != SceneManager.GetActiveScene()) Rebuild();
    }

    public void Rebuild()
    {
        _nodes.Clear();
        _adjacency.Clear();
        _cellLookup.Clear();
        _built = false;
        _builtForScene = SceneManager.GetActiveScene();

        var blocksParent = GameObject.Find("Blocks");
        if (blocksParent == null) return;

        bool first = true;
        foreach (Transform b in blocksParent.transform)
        {
            Vector3 pos = Round(b.position);
            if (first)
            {
                _originX = pos.x;
                _originZ = pos.z;
                first = false;
            }
            _nodes.Add(pos);
            _adjacency[pos] = new List<Vector3>();
        }

        foreach (var pos in _nodes)
        {
            _cellLookup[WorldToCell(pos)] = pos;
        }

        foreach (var a in _nodes)
        {
            var cellA = WorldToCell(a);
            TryLink(a, cellA + new Vector2Int(1, 0));
            TryLink(a, cellA + new Vector2Int(-1, 0));
            TryLink(a, cellA + new Vector2Int(0, 1));
            TryLink(a, cellA + new Vector2Int(0, -1));
        }

        _built = true;
    }

    private void TryLink(Vector3 from, Vector2Int neighborCell)
    {
        if (_cellLookup.TryGetValue(neighborCell, out var neighborPos))
        {
            _adjacency[from].Add(neighborPos);
        }
    }

    public List<Vector3> GetNeighbors(Vector3 node)
    {
        return _adjacency.TryGetValue(node, out var list) ? list : EmptyList;
    }

    // O(1) in the common case (worldPos is over/near a walkable tile), a
    // small 3x3-cell search when it's between tiles, and only falls back to
    // a full scan if worldPos is nowhere near the grid at all.
    public Vector3 NearestNode(Vector3 worldPos)
    {
        if (_nodes.Count == 0) return worldPos;

        var cell = WorldToCell(worldPos);
        if (_cellLookup.TryGetValue(cell, out var exact)) return exact;

        Vector3 best = default;
        float bestDistSq = float.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var c = new Vector2Int(cell.x + dx, cell.y + dz);
                if (_cellLookup.TryGetValue(c, out var candidate))
                {
                    float d = (candidate.x - worldPos.x) * (candidate.x - worldPos.x) + (candidate.z - worldPos.z) * (candidate.z - worldPos.z);
                    if (d < bestDistSq)
                    {
                        bestDistSq = d;
                        best = candidate;
                    }
                }
            }
        }
        if (bestDistSq < float.MaxValue) return best;

        foreach (var n in _nodes)
        {
            float d = (n.x - worldPos.x) * (n.x - worldPos.x) + (n.z - worldPos.z) * (n.z - worldPos.z);
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = n;
            }
        }
        return best;
    }

    private Vector2Int WorldToCell(Vector3 pos)
    {
        return new Vector2Int(Mathf.RoundToInt(pos.x - _originX), Mathf.RoundToInt(pos.z - _originZ));
    }

    private static Vector3 Round(Vector3 v)
    {
        return new Vector3(Mathf.Round(v.x * 100f) / 100f, v.y, Mathf.Round(v.z * 100f) / 100f);
    }
}
