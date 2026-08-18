using System.Collections.Generic;
using UnityEngine;

// Builds the walkable floor as ONE merged, flat-shaded mesh instead of 110
// separate cubes.
//
// Making the texture continuous (see the triplanar shader) was only half the
// problem: the board still read as individual blocks because every cube brought
// its own silhouette, edge and shading break. The only way to remove those is to
// stop drawing cubes. So this walks the grid, emits a single top surface over
// every walkable cell, and drops a skirt wall only where the floor actually
// meets water. Interior cell borders simply do not exist in the mesh.
//
// The cubes stay in the scene as colliders - only their rendering is suppressed,
// through the non-serialized Renderer.forceRenderingOff, so nothing leaks into
// the saved scene.
[ExecuteAlways]
public class GroundSurface : MonoBehaviour
{
    public static GroundSurface Instance { get; private set; }

    [Header("Material")]
    [SerializeField] private Material groundMaterial;

    [Header("Shape")]
    [Tooltip("Quads per cell edge. Higher means more facets in the low-poly surface.")]
    [SerializeField, Range(1, 6)] private int facetsPerCell = 2;
    [Tooltip("Random height offset per surface vertex. Purely visual - collision still comes from the cubes.")]
    [SerializeField] private float heightJitter = 0.035f;
    [Tooltip("How far the shoreline wall drops below the surface. Must reach past the liquid bed.")]
    [SerializeField] private float skirtDepth = 1.1f;
    [Tooltip("Spread of the per-facet brightness jitter baked into vertex colours.")]
    [SerializeField, Range(0f, 1f)] private float colorVariation = 0.55f;

    [Header("Colour Zones")]
    [Tooltip("How many flat palette bands the field is divided into.")]
    [SerializeField, Range(2, 6)] private int colorZones = 4;
    [Tooltip("Size of those bands in world units.")]
    [SerializeField, Range(1f, 12f)] private float zoneScale = 3.2f;
    [Tooltip("Contrast of the zone noise. Low values leave every facet in the middle band.")]
    [SerializeField, Range(1f, 4f)] private float zoneContrast = 2.4f;

    [Header("Meadow")]
    [Tooltip("Amplitude of the smooth rolling undulation laid over the whole field.")]
    [SerializeField, Range(0f, 0.4f)] private float undulation = 0.09f;
    [Tooltip("Wavelength of that undulation, in cells.")]
    [SerializeField, Range(2f, 20f)] private float undulationScale = 6f;
    [Tooltip("How far inland (in cells) the shoreline dirt band reaches. Baked into vertex colour green.")]
    [SerializeField, Range(0.5f, 5f)] private float shoreWidth = 0.9f;
    [Tooltip("How far the ground dips as it approaches the water, giving a soft bank.")]
    [SerializeField, Range(0f, 0.3f)] private float shoreDip = 0.05f;
    [Tooltip("Minimum gap kept between the surface and the liquid plane, so water never pokes through the grass.")]
    [SerializeField, Range(0f, 0.3f)] private float waterClearance = 0.1f;

    private Transform _surface;
    private Transform _blocksParent;
    private Transform _lavaParent;

    private Dictionary<Vector2Int, float> _shoreDist;
    private float _shoreOriginX, _shoreOriginZ;
    private float _liquidTopY;

    void Awake() => Instance = this;

    void OnEnable()
    {
        Rebuild();
    }

    void OnDisable()
    {
        DestroySurface();
        // Cached lookups only - resolving here would hit GameObject.Find, which
        // asserts once the scene starts unloading or a domain reload begins.
        RestoreCachedTiles();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        DestroySurface();
        if (!ResolveParents()) return;

        var walkable = new HashSet<Vector2Int>();
        if (!BuildCellSets(walkable, out float topY, out float originX, out float originZ)) return;
        if (walkable.Count == 0) return;

        var mesh = BuildMesh(walkable, topY, originX, originZ);

        var go = new GameObject("GroundSurface");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.position = Vector3.zero;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = groundMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        _surface = go.transform;

        HideBlockTiles();
    }

    // Called after the level layout is reshuffled - which cells are walkable
    // changed, so the merged mesh has to be regenerated from scratch.
    public void Refresh() => Rebuild();

    public void HideBlockTiles()
    {
        if (_blocksParent == null) return;
        foreach (Transform t in _blocksParent) SetTileVisible(t, false);
    }

    [ContextMenu("Restore Tile Rendering")]
    public void RestoreTileRendering()
    {
        ResolveParents();
        RestoreCachedTiles();
    }

    private void RestoreCachedTiles()
    {
        if (_blocksParent != null) foreach (Transform t in _blocksParent) SetTileVisible(t, true);
    }

    private static void SetTileVisible(Transform tile, bool visible)
    {
        var mr = tile.GetComponent<MeshRenderer>();
        if (mr != null) mr.forceRenderingOff = !visible;
    }

    private void DestroySurface()
    {
        if (_surface == null) return;
        if (Application.isPlaying) Destroy(_surface.gameObject);
        else DestroyImmediate(_surface.gameObject);
        _surface = null;
    }

    // Cached so teardown never calls GameObject.Find, which asserts once the
    // scene starts unloading.
    private bool ResolveParents()
    {
        if (_blocksParent == null)
        {
            var go = GameObject.Find("Blocks");
            _blocksParent = go != null ? go.transform : null;
        }
        if (_lavaParent == null)
        {
            var go = GameObject.Find("Lava");
            _lavaParent = go != null ? go.transform : null;
        }
        return _blocksParent != null;
    }

    private bool BuildCellSets(HashSet<Vector2Int> walkable, out float topY, out float originX, out float originZ)
    {
        topY = 0f; originX = 0f; originZ = 0f;

        var tiles = new List<Transform>();
        foreach (Transform t in _blocksParent) tiles.Add(t);
        if (_lavaParent != null) foreach (Transform t in _lavaParent) tiles.Add(t);
        if (tiles.Count == 0) return false;

        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach (var t in tiles)
        {
            minX = Mathf.Min(minX, t.position.x);
            minZ = Mathf.Min(minZ, t.position.z);
        }

        var first = _blocksParent.GetChild(0);
        topY = first.position.y + first.lossyScale.y * 0.5f;

        // Same figure LiquidSurface parks its plane at: the lowest lava tile top.
        _liquidTopY = topY - 1f;
        if (_lavaParent != null)
        {
            bool any = false;
            foreach (Transform t in _lavaParent)
            {
                float top = t.position.y + t.lossyScale.y * 0.5f;
                _liquidTopY = any ? Mathf.Min(_liquidTopY, top) : top;
                any = true;
            }
        }

        foreach (Transform t in _blocksParent)
        {
            walkable.Add(new Vector2Int(
                Mathf.RoundToInt(t.position.x - minX),
                Mathf.RoundToInt(t.position.z - minZ)));
        }

        // Cell centres sit on the grid, so the corner grid starts half a cell out.
        originX = minX - 0.5f;
        originZ = minZ - 0.5f;
        return true;
    }

    private static readonly Vector2Int[] Neighbours =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    private Mesh BuildMesh(HashSet<Vector2Int> walkable, float topY, float originX, float originZ)
    {
        int s = Mathf.Max(1, facetsPerCell);
        float step = 1f / s;

        BuildShoreField(walkable, originX, originZ);

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        // The liquid plane sits at the top of the recessed lava tiles, so anything
        // the surface does below that line pokes the water up through the grass.
        // The clamp is the hard guarantee; the terms below are also shaped to stay
        // clear of it, so it only ever catches the extreme corners.
        float minY = _liquidTopY + waterClearance;

        // Height is a pure function of the corner's grid index, so neighbouring
        // cells agree on shared corners and the surface never cracks open. Three
        // terms: a smooth roll across the whole field (upward only, so it can
        // never eat into the clearance), per-vertex jitter for the faceting, and
        // a dip into the water so the bank is not a flat table.
        float HeightAt(int i, int j)
        {
            float x = originX + i * step;
            float z = originZ + j * step;
            float roll = SmoothNoise(x / undulationScale, z / undulationScale) * undulation;
            float jitter = (Hash01(i, j, 7) - 0.5f) * 2f * heightJitter;
            return Mathf.Max(topY + roll + jitter - ShoreAt(x, z) * shoreDip, minY);
        }
        Vector3 TopVert(int i, int j) => new Vector3(originX + i * step, HeightAt(i, j), originZ + j * step);

        float bottomY = topY - skirtDepth;

        foreach (var cell in walkable)
        {
            int i0 = cell.x * s, j0 = cell.y * s;

            for (int a = 0; a < s; a++)
            {
                for (int b = 0; b < s; b++)
                {
                    int i = i0 + a, j = j0 + b;
                    AddQuad(verts, norms, cols, tris,
                        TopVert(i, j), TopVert(i, j + 1), TopVert(i + 1, j + 1), TopVert(i + 1, j),
                        Vector3.up, true, i, j);
                }
            }

            // Skirt only along edges that actually border water or the board rim.
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

                    AddQuad(verts, norms, cols, tris, ta, tb, bb, ba,
                        new Vector3(d.x, 0f, d.y), false, ia, ja);
                }
            }
        }

        var mesh = new Mesh { name = "LowPolyGround" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Vertices are never shared between triangles: that is what makes the
    // shading faceted instead of smooth.
    private void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedNormal, bool isTop, int hi, int hj)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
        {
            (a, b, c, d) = (d, c, b, a);
        }

        float v0 = Variation(hi, hj, 11);
        float v1 = Variation(hi, hj, 29);

        // Alternating which way each quad is split breaks up the regular diagonal
        // banding you get from triangulating a grid the same way every time.
        if (Hash01(hi, hj, 41) < 0.5f)
        {
            AddTri(verts, norms, cols, tris, a, b, c, v0, isTop);
            AddTri(verts, norms, cols, tris, a, c, d, v1, isTop);
        }
        else
        {
            AddTri(verts, norms, cols, tris, a, b, d, v0, isTop);
            AddTri(verts, norms, cols, tris, b, c, d, v1, isTop);
        }
    }

    private float Variation(int i, int j, int salt) =>
        Mathf.Clamp01(0.5f + (Hash01(i, j, salt) - 0.5f) * colorVariation);

    // Which flat palette band a facet belongs to. Quantised here, on the CPU,
    // rather than in the shader: the earlier procedural attempt hid this noise in
    // HLSL where its output range could not be measured, and it silently sat in
    // the middle band the whole time. Here it can be printed and checked.
    private float ZoneAt(Vector3 centre)
    {
        float n = Fbm(centre.x / zoneScale, centre.z / zoneScale);
        n = Mathf.Clamp01((n - 0.5f) * zoneContrast + 0.5f);
        int steps = Mathf.Max(1, colorZones - 1);
        return Mathf.Round(n * steps) / steps;
    }

    private static float Fbm(float x, float z) =>
        SmoothNoise(x, z) * 0.65f + SmoothNoise(x * 2.4f + 11.3f, z * 2.4f + 5.7f) * 0.35f;

    // Vertex colour layout, read by LowPolyGround_URP:
    //   r = palette band 0..1, flat across the triangle - this is the facet colour
    //   g = shoreline proximity, per-vertex so the damp edge fades smoothly
    //   b = per-facet brightness jitter, what stops equal bands reading as one mass
    //   a = 1 on top faces, 0 on the skirt walls
    private void AddTri(List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, float variation, bool isTop)
    {
        int at = verts.Count;
        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
        float alpha = isTop ? 1f : 0f;
        float zone = ZoneAt((a + b + c) / 3f);

        verts.Add(a); verts.Add(b); verts.Add(c);
        norms.Add(n); norms.Add(n); norms.Add(n);
        cols.Add(new Color(zone, ShoreAt(a.x, a.z), variation, alpha));
        cols.Add(new Color(zone, ShoreAt(b.x, b.z), variation, alpha));
        cols.Add(new Color(zone, ShoreAt(c.x, c.z), variation, alpha));
        tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
    }

    // Distance (in cells) from every walkable cell to the water, so the mesh can
    // fade grass into dirt as it approaches the edge. Cells touching water sit
    // half a cell from the boundary line, which is where the value hits 1.
    private void BuildShoreField(HashSet<Vector2Int> walkable, float originX, float originZ)
    {
        _shoreOriginX = originX;
        _shoreOriginZ = originZ;
        _shoreDist = new Dictionary<Vector2Int, float>(walkable.Count);

        var frontier = new Queue<Vector2Int>();
        foreach (var cell in walkable)
        {
            foreach (var d in Neighbours)
            {
                if (walkable.Contains(cell + d)) continue;
                _shoreDist[cell] = 0.5f;
                frontier.Enqueue(cell);
                break;
            }
        }

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();
            float next = _shoreDist[cell] + 1f;
            foreach (var d in Neighbours)
            {
                var n = cell + d;
                if (!walkable.Contains(n)) continue;
                if (_shoreDist.TryGetValue(n, out float existing) && existing <= next) continue;
                _shoreDist[n] = next;
                frontier.Enqueue(n);
            }
        }
    }

    // Bilinear sample of that field at an arbitrary point. Water cells count as
    // half a cell beyond the boundary, which keeps the gradient continuous right
    // up to the shoreline instead of flattening out at the last row of cells.
    private float ShoreAt(float worldX, float worldZ)
    {
        if (_shoreDist == null || _shoreDist.Count == 0) return 0f;

        float cx = worldX - _shoreOriginX - 0.5f;
        float cz = worldZ - _shoreOriginZ - 0.5f;
        int i = Mathf.FloorToInt(cx), j = Mathf.FloorToInt(cz);
        float fx = cx - i, fz = cz - j;

        float d00 = DistAt(i, j), d10 = DistAt(i + 1, j);
        float d01 = DistAt(i, j + 1), d11 = DistAt(i + 1, j + 1);
        float d = Mathf.Lerp(Mathf.Lerp(d00, d10, fx), Mathf.Lerp(d01, d11, fx), fz);

        return Mathf.Clamp01(1f - d / Mathf.Max(shoreWidth, 0.01f));
    }

    private float DistAt(int i, int j) =>
        _shoreDist.TryGetValue(new Vector2Int(i, j), out float d) ? d : -0.5f;

    // Smoothed value noise on a unit lattice, for the rolling undulation.
    private static float SmoothNoise(float x, float z)
    {
        int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(z);
        float fx = x - i, fz = z - j;
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);

        float a = Hash01(i, j, 101), b = Hash01(i + 1, j, 101);
        float c = Hash01(i, j + 1, 101), d = Hash01(i + 1, j + 1, 101);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
    }

    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}
