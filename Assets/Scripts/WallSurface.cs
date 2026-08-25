using System.Collections.Generic;
using UnityEngine;

// Same treatment GroundSurface gives the floor, applied to the border wall: the
// 108 stone cubes are merged into ONE mesh so the rampart reads as a built wall
// instead of a row of boxes.
//
// Two things gave the cubes away. Every cube drew its own six faces, so the
// interior faces between neighbours produced a seam and a shading break at every
// join; and each cube restarted the stone texture, which put a visible brick grid
// on a surface that should run continuously. This walks the occupied cells and
// emits a face only where a cell has no neighbour in that direction.
//
// There are no UVs and no texture at all now: each face carries a palette band
// and a brightness jitter in its vertex colours, and LowPolyWall_URP flattens
// those into one solid colour per face. The wall and the ground share
// ArtStyle.hlsl, so they light identically.
//
// The crest is the one place a merged box shell still looks machined, so the
// vertices sitting on the top plane get a small hash-based drop. Neighbouring
// faces agree on it because it is a pure function of the corner's grid index.
//
// The cubes stay in the scene as colliders - only their rendering is suppressed,
// through the non-serialized Renderer.forceRenderingOff, so nothing leaks into
// the saved scene.
[ExecuteAlways]
public class WallSurface : MonoBehaviour
{
    public static WallSurface Instance { get; private set; }

    [Header("Material")]
    [SerializeField] private Material wallMaterial;

    [Header("Source")]
    [Tooltip("Name of the parent holding the wall cubes.")]
    [SerializeField] private string wallsParentName = "Walls";

    [Header("Shape")]
    [Tooltip("How far the crest vertices drop, so the top edge is weathered rather than machined.")]
    [SerializeField, Range(0f, 0.4f)] private float crestWear = 0.12f;
    [Tooltip("Share of crest vertices left untouched, keeping stretches of the top edge level.")]
    [SerializeField, Range(0f, 1f)] private float crestFlatness = 0.35f;

    [Header("Colour Zones")]
    [Tooltip("How many flat palette bands the stone is divided into.")]
    [SerializeField, Range(2, 6)] private int colorZones = 4;
    [Tooltip("Size of those bands in world units.")]
    [SerializeField, Range(0.5f, 8f)] private float zoneScale = 2.4f;
    [Tooltip("Contrast of the zone noise. Low values leave every face in the middle band.")]
    [SerializeField, Range(1f, 4f)] private float zoneContrast = 2.4f;
    [Tooltip("Spread of the per-face brightness jitter baked into vertex colours.")]
    [SerializeField, Range(0f, 1f)] private float colorVariation = 0.6f;

    [Header("Rendering")]
    [Tooltip("Let the rampart cast shadows onto the field. The cubes never did.")]
    [SerializeField] private bool castShadows = true;

    private Transform _surface;
    private Transform _wallsParent;

    void Awake() => Instance = this;

    void OnEnable() => Rebuild();

    void OnDisable()
    {
        DestroySurface();
        // Cached lookup only - resolving here would hit GameObject.Find, which
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
        if (!ResolveParent()) return;

        if (!BuildCellSet(out HashSet<Vector3Int> cells, out Vector3 origin, out Vector3 cell)) return;
        if (cells.Count == 0) return;

        var mesh = BuildMesh(cells, origin, cell);

        var go = new GameObject("WallSurface");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.position = Vector3.zero;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = wallMaterial;
        mr.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        _surface = go.transform;

        HideWallTiles();
    }

    // The border does not change when the level is reshuffled, but the hook is
    // here so a caller that moves the walls can put them back in sync.
    // Off means off: Instance survives on a disabled component, and rebuilding
    // from that hook would draw the merged wall over cubes meant to be visible.
    public void Refresh()
    {
        if (!isActiveAndEnabled) return;
        Rebuild();
    }

    public void HideWallTiles()
    {
        if (_wallsParent == null) return;
        foreach (Transform t in _wallsParent) SetTileVisible(t, false);
    }

    [ContextMenu("Restore Tile Rendering")]
    public void RestoreTileRendering()
    {
        ResolveParent();
        RestoreCachedTiles();
    }

    private void RestoreCachedTiles()
    {
        if (_wallsParent != null) foreach (Transform t in _wallsParent) SetTileVisible(t, true);
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

    private bool ResolveParent()
    {
        if (_wallsParent == null)
        {
            var go = GameObject.Find(wallsParentName);
            _wallsParent = go != null ? go.transform : null;
        }
        return _wallsParent != null;
    }

    // Snaps the cubes onto an integer lattice.
    //
    // The cell size is measured from how far apart the cubes actually sit, not
    // from their scale. These two disagree here: the wall is two rows of 1-unit
    // cubes stacked 0.902 apart, so they overlap. Sizing cells by the scale would
    // make the rows overlap in the merged shell too, and the coincident side
    // faces in the overlap band would z-fight. Sizing by the spacing makes the
    // rows stack flush - no gap, no overlap - for a 0.05 change in wall height.
    private bool BuildCellSet(out HashSet<Vector3Int> cells, out Vector3 origin, out Vector3 cell)
    {
        cells = new HashSet<Vector3Int>();
        origin = Vector3.zero;
        cell = Vector3.one;

        if (_wallsParent.childCount == 0) return false;

        var positions = new List<Vector3>();
        foreach (Transform t in _wallsParent) positions.Add(t.position);

        Vector3 fallback = _wallsParent.GetChild(0).lossyScale;
        cell = new Vector3(
            Spacing(positions, 0, fallback.x),
            Spacing(positions, 1, fallback.y),
            Spacing(positions, 2, fallback.z));
        if (cell.x <= 0f || cell.y <= 0f || cell.z <= 0f) return false;

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        foreach (var p in positions) min = Vector3.Min(min, p);

        foreach (var p in positions)
        {
            Vector3 d = p - min;
            cells.Add(new Vector3Int(
                Mathf.RoundToInt(d.x / cell.x),
                Mathf.RoundToInt(d.y / cell.y),
                Mathf.RoundToInt(d.z / cell.z)));
        }

        origin = min;
        return true;
    }

    // Smallest gap between two distinct coordinates on this axis - the pitch the
    // board was laid out on. Falls back to the cube's own size when every cube
    // shares the coordinate, which is what a single-row axis looks like.
    private static float Spacing(List<Vector3> positions, int axis, float fallback)
    {
        var values = new List<float>();
        foreach (var p in positions)
        {
            float v = Mathf.Round(p[axis] * 1000f) / 1000f;
            if (!values.Contains(v)) values.Add(v);
        }
        values.Sort();

        float best = float.MaxValue;
        for (int i = 1; i < values.Count; i++) best = Mathf.Min(best, values[i] - values[i - 1]);
        return best < float.MaxValue && best > 0.001f ? best : fallback;
    }

    private static readonly Vector3Int[] Faces =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    private Mesh BuildMesh(HashSet<Vector3Int> cells, Vector3 origin, Vector3 cell)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        int topLevel = int.MinValue;
        foreach (var c in cells) topLevel = Mathf.Max(topLevel, c.y);
        float topY = origin.y + topLevel * cell.y + cell.y * 0.5f;

        foreach (var c in cells)
        {
            Vector3 centre = origin + new Vector3(c.x * cell.x, c.y * cell.y, c.z * cell.z);

            foreach (var d in Faces)
            {
                // The face between two neighbours is never visible, so it is
                // never built: that is what removes the per-cube seams.
                if (cells.Contains(c + d)) continue;

                Vector3 n = new Vector3(d.x, d.y, d.z);
                GetFaceAxes(d, out Vector3 u, out Vector3 v);

                Vector3 half = new Vector3(n.x * cell.x, n.y * cell.y, n.z * cell.z) * 0.5f;
                Vector3 du = new Vector3(u.x * cell.x, u.y * cell.y, u.z * cell.z) * 0.5f;
                Vector3 dv = new Vector3(v.x * cell.x, v.y * cell.y, v.z * cell.z) * 0.5f;

                Vector3 a = centre + half - du - dv;
                Vector3 b = centre + half - du + dv;
                Vector3 e = centre + half + du + dv;
                Vector3 f = centre + half + du - dv;

                AddQuad(verts, norms, cols, tris,
                    Wear(a, topY, origin, cell), Wear(b, topY, origin, cell),
                    Wear(e, topY, origin, cell), Wear(f, topY, origin, cell), n, d.y > 0);
            }
        }

        var mesh = new Mesh { name = "MergedWall" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Only vertices sitting exactly on the crest move, and only downwards, so a
    // worn top edge can never punch a hole through the wall below it.
    private Vector3 Wear(Vector3 p, float topY, Vector3 origin, Vector3 cell)
    {
        if (crestWear <= 0f || Mathf.Abs(p.y - topY) > 0.001f) return p;

        int i = Mathf.RoundToInt((p.x - origin.x) / cell.x * 2f);
        int k = Mathf.RoundToInt((p.z - origin.z) / cell.z * 2f);

        float h = Hash01(i, k, 17);
        if (h < crestFlatness) return p;

        float t = (h - crestFlatness) / Mathf.Max(1f - crestFlatness, 0.0001f);
        p.y -= t * crestWear;
        return p;
    }

    private static void GetFaceAxes(Vector3Int d, out Vector3 u, out Vector3 v)
    {
        if (d.y != 0) { u = Vector3.right; v = Vector3.forward; }
        else if (d.x != 0) { u = Vector3.forward; v = Vector3.up; }
        else { u = Vector3.right; v = Vector3.up; }
    }

    // Vertices are never shared between faces, which keeps the shading flat and
    // the corners crisp.
    private void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Color> cols, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedNormal, bool isCrest)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
        {
            (a, b, c, d) = (d, c, b, a);
        }

        // One band and one jitter value for the whole quad: the wall should read
        // as blocks of stone, not as pairs of triangles.
        Vector3 centre = (a + b + c + d) * 0.25f;
        var col = new Color(ZoneAt(centre), 0f, Jitter(centre), isCrest ? 1f : 0f);

        AddTri(verts, norms, cols, tris, a, b, c, expectedNormal, col);
        AddTri(verts, norms, cols, tris, a, c, d, expectedNormal, col);
    }

    private static void AddTri(List<Vector3> verts, List<Vector3> norms, List<Color> cols,
        List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 fallbackNormal, Color col)
    {
        int at = verts.Count;
        Vector3 n = Vector3.Cross(b - a, c - a);
        // The crest drop can collapse a triangle; fall back to the face normal
        // rather than emitting a zero-length one and blackening the facet.
        n = n.sqrMagnitude > 1e-10f ? n.normalized : fallbackNormal.normalized;

        verts.Add(a); verts.Add(b); verts.Add(c);
        norms.Add(n); norms.Add(n); norms.Add(n);
        cols.Add(col); cols.Add(col); cols.Add(col);
        tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
    }

    // Same scheme the ground uses: the noise that picks a palette band runs here,
    // where its output can be measured, not in the shader where it cannot.
    private float ZoneAt(Vector3 centre)
    {
        float n = Fbm(centre.x / zoneScale, (centre.y + centre.z) / zoneScale);
        n = Mathf.Clamp01((n - 0.5f) * zoneContrast + 0.5f);
        int steps = Mathf.Max(1, colorZones - 1);
        return Mathf.Round(n * steps) / steps;
    }

    private float Jitter(Vector3 centre)
    {
        float h = Hash01(Mathf.RoundToInt(centre.x * 97f), Mathf.RoundToInt((centre.y + centre.z) * 97f), 23);
        return Mathf.Clamp01(0.5f + (h - 0.5f) * colorVariation);
    }

    private static float Fbm(float x, float y) =>
        SmoothNoise(x, y) * 0.65f + SmoothNoise(x * 2.4f + 11.3f, y * 2.4f + 5.7f) * 0.35f;

    private static float SmoothNoise(float x, float y)
    {
        int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(y);
        float fx = x - i, fy = y - j;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float a = Hash01(i, j, 101), b = Hash01(i + 1, j, 101);
        float c = Hash01(i, j + 1, 101), d = Hash01(i + 1, j + 1, 101);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
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
