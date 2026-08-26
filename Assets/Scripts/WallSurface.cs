using System.Collections.Generic;
using UnityEngine;

// The border wall, merged into ONE mesh built out of painted stone modules.
//
// A module is a 1x1 footprint standing `moduleHeight` units tall, and that
// number is not free: the side art is painted 1:2 and the cap 1:1, so a module
// has to be exactly twice as tall as it is wide or the stones come out squat.
// The scene supplies the footprint as two stacked rows of cubes; this collapses
// them to a single 2D ring and extrudes one module per cell, which is the same
// wall the cubes described but with the horizontal joint between the rows gone.
//
// `thickness` narrows that footprint across the run. Only the outward face
// moves, so the rampart keeps hugging the edge of the field, and the art is
// cropped rather than squeezed - a half-width module reads half a texture, so
// its stones stay the size they were painted at.
//
// Every face carries a real UV rectangle - one full copy of a texture, 0..1 in
// both directions - rather than a projection. That is the whole point: the
// mortar joints are painted into the edges of the art, so they only land on the
// module edges if a face maps exactly one texture. Interior faces between
// neighbouring modules are never emitted, and neither is the underside.
//
// Four side elevations ship with the set. Which one a face gets is a hash of the
// cell and the direction it faces, so a corner shows two different stones, and
// the choice travels to the shader in uv2.x. uv2.y is a per-module brightness -
// 54 modules off one material would otherwise read as 54 copies.
//
// The cubes stay in the scene as colliders: only their rendering is suppressed,
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

    [Header("Module")]
    [Tooltip("Height of one module in world units. The side art is 1:2, so this wants to be twice the cell width.")]
    [SerializeField, Range(0.5f, 6f)] private float moduleHeight = 2f;
    [Tooltip("Raises or lowers the crest without moving the colliders underneath it.")]
    [SerializeField, Range(-1f, 1f)] private float crestOffset = 0f;
    [Tooltip("How much of its cell a module fills across the run. 1 is the full cell the cubes describe; 0.5 is a rampart half as thick. Only the outward face moves, so the wall keeps hugging the edge of the field and the colliders underneath are untouched.")]
    [SerializeField, Range(0.2f, 1f)] private float thickness = 0.5f;

    [Header("Variation")]
    [Tooltip("Mirror the art on roughly half the faces, so four elevations and one cap read as more than five.")]
    [SerializeField] private bool mirrorFaces = true;
    [Tooltip("Trims the painted mortar margin off both sides of an elevation. Without it two neighbouring panels butt their margins together and the joint comes out twice as wide as the art intends.")]
    [SerializeField, Range(0f, 0.2f)] private float sideInset = 0.05f;
    [Tooltip("Turn the cap art a quarter turn at a time, so the crest is not one stamp repeated along the run.")]
    [SerializeField] private bool rotateCaps = true;
    [Tooltip("Spread of the per-module brightness handed to the shader.")]
    [SerializeField, Range(0f, 1f)] private float moduleVariation = 0.7f;

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
        if (!BuildFootprint(out var cells, out Vector3 origin, out Vector2 cell, out float topY)) return;
        if (cells.Count == 0) return;

        var mesh = BuildMesh(cells, origin, cell, topY);

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

    // Collapses the cubes onto a 2D lattice: the wall is a ring one cell thick,
    // stacked two rows deep, and a module replaces a whole stack.
    //
    // Cell size is measured from how far apart the cubes actually sit rather
    // than from their scale, because those disagree here - the cubes carry a
    // randomised scale from when the wall was a heap of rocks, and it is the
    // spacing, not the scale, that says where the grid is.
    //
    // The crest sits half a row-pitch above the top row, which is where the top
    // of those cubes was; everything below it is the module's own business.
    private bool BuildFootprint(out HashSet<Vector2Int> cells, out Vector3 origin, out Vector2 cell, out float topY)
    {
        cells = new HashSet<Vector2Int>();
        origin = Vector3.zero;
        cell = Vector2.one;
        topY = 0f;

        if (_wallsParent.childCount == 0) return false;

        var positions = new List<Vector3>();
        foreach (Transform t in _wallsParent) positions.Add(t.position);

        Vector3 fallback = _wallsParent.GetChild(0).lossyScale;
        cell = new Vector2(Spacing(positions, 0, fallback.x), Spacing(positions, 2, fallback.z));
        if (cell.x <= 0f || cell.y <= 0f) return false;

        float minX = float.MaxValue, minZ = float.MaxValue, maxY = float.MinValue;
        foreach (var p in positions)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxY = Mathf.Max(maxY, p.y);
        }

        float rowPitch = Spacing(positions, 1, fallback.y);
        topY = maxY + rowPitch * 0.5f + crestOffset;

        foreach (var p in positions)
        {
            cells.Add(new Vector2Int(
                Mathf.RoundToInt((p.x - minX) / cell.x),
                Mathf.RoundToInt((p.z - minZ) / cell.y)));
        }

        origin = new Vector3(minX, 0f, minZ);
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

    private static readonly Vector2Int[] Sides =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    // The four corners of a unit UV square. Reading them at an offset rotates
    // the art a quarter turn, which is only valid because the cap is square.
    private static readonly Vector2[] Corners =
    {
        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
    };

    private Mesh BuildMesh(HashSet<Vector2Int> cells, Vector3 origin, Vector2 cell, float topY)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var mods = new List<Vector2>();
        var tris = new List<int>();

        float bottomY = topY - moduleHeight;

        // The ring is one cell thick, so a cell lies on the footprint's outer
        // boundary along whichever axis it sits at an extreme of - and that is
        // the face that gives way when the wall is thinned. Corner cells are at
        // an extreme on both axes and so give way on both, which is what keeps
        // the two thinned runs meeting cleanly.
        Vector2Int lo = new Vector2Int(int.MaxValue, int.MaxValue);
        Vector2Int hi = new Vector2Int(int.MinValue, int.MinValue);
        foreach (var c in cells)
        {
            lo = Vector2Int.Min(lo, c);
            hi = Vector2Int.Max(hi, c);
        }

        foreach (var c in cells)
        {
            float cx = origin.x + c.x * cell.x;
            float cz = origin.z + c.y * cell.y;
            float jitter = Hash01(c.x, c.y, 23);

            // Shrinking pulls the module toward the field, away from whichever
            // side is the outside, so the inner face never moves.
            float sizeX = cell.x, sizeZ = cell.y;
            if (c.x == lo.x || c.x == hi.x)
            {
                sizeX = cell.x * thickness;
                cx += (c.x == lo.x ? 1f : -1f) * (cell.x - sizeX) * 0.5f;
            }
            if (c.y == lo.y || c.y == hi.y)
            {
                sizeZ = cell.y * thickness;
                cz += (c.y == lo.y ? 1f : -1f) * (cell.y - sizeZ) * 0.5f;
            }

            // Cap. There is only one of these in the set and the game camera
            // looks almost straight down at it, so it turns a quarter turn and
            // mirrors per module: eight readings of one square instead of 54
            // copies of it.
            int rot = rotateCaps ? Mathf.FloorToInt(Hash01(c.x, c.y, 61) * 4f) & 3 : 0;
            bool flipCap = mirrorFaces && Hash01(c.x, c.y, 89) < 0.5f;
            AddQuad(verts, norms, uvs, mods, tris,
                new Vector3(cx - sizeX * 0.5f, topY, cz + sizeZ * 0.5f),
                Vector3.right * sizeX, Vector3.back * sizeZ, Vector3.up,
                rot, flipCap, 0f, 0f, jitter,
                new Vector2(sizeX / cell.x, sizeZ / cell.y));

            foreach (var d in Sides)
            {
                // The face between two neighbours is never visible, so it is
                // never built: that is what removes the per-cube seams.
                if (cells.Contains(c + d)) continue;

                var n = new Vector3(d.x, 0f, d.y);
                Vector3 u = Vector3.Cross(Vector3.up, n);
                bool alongX = Mathf.Abs(u.x) > 0.5f;
                float width = alongX ? sizeX : sizeZ;
                float fullWidth = alongX ? cell.x : cell.y;

                Vector3 faceCentre = new Vector3(cx, bottomY, cz)
                                   + new Vector3(n.x * sizeX, 0f, n.z * sizeZ) * 0.5f;
                Vector3 anchor = faceCentre - u * (width * 0.5f);

                int face = (d.x + 1) * 4 + (d.y + 1);
                float pick = Hash01(c.x * 7 + face, c.y * 13 + face, 137);
                float variant = Mathf.Floor(pick * 4f);
                if (variant > 3f) variant = 3f;
                bool mirror = mirrorFaces && Hash01(c.x + face, c.y - face, 211) < 0.5f;

                // Only a corner's outward faces are ever narrowed - a face
                // along a run still spans a whole cell.
                AddQuad(verts, norms, uvs, mods, tris,
                    anchor, u * width, Vector3.up * moduleHeight, n,
                    0, mirror, sideInset, variant, jitter,
                    new Vector2(width / fullWidth, 1f));
            }
        }

        var mesh = new Mesh { name = "MergedWall" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, mods);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // One quad spanning exactly one copy of a texture - or `crop` of one, when
    // the face is narrower than a full module and the art has to be cropped
    // instead of squeezed. `u` and `v` are the full edge vectors, ordered so
    // their cross product is the outward normal, which is what makes the
    // winding below come out front-facing; `crop` is measured along those same
    // two, not along the texture's axes.
    //
    // Vertices are never shared between faces: the wall wants crisp corners and
    // each face wants its own variant index.
    private void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
        List<Vector2> mods, List<int> tris,
        Vector3 anchor, Vector3 u, Vector3 v, Vector3 normal,
        int rot, bool mirror, float inset, float variant, float jitter, Vector2 crop)
    {
        var module = new Vector2(variant, Mathf.Clamp01(0.5f + (jitter - 0.5f) * moduleVariation));

        // An odd quarter turn has already swapped which edge runs along which
        // texture axis, so the crop has to follow it round.
        float cropU = (rot & 1) == 0 ? crop.x : crop.y;
        float cropV = (rot & 1) == 0 ? crop.y : crop.x;

        int at = verts.Count;
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = Corners[(i + rot) & 3];
            if (mirror) corner.x = 1f - corner.x;
            // Reading the art from just inside its own edge, so the mortar
            // painted along that edge is shared with the next panel instead of
            // doubled against it.
            corner.x = inset + corner.x * (1f - 2f * inset);
            corner.x *= cropU;
            corner.y *= cropV;

            verts.Add(anchor + u * Corners[i].x + v * Corners[i].y);
            norms.Add(normal);
            uvs.Add(corner);
            mods.Add(module);
        }

        tris.Add(at); tris.Add(at + 1); tris.Add(at + 2);
        tris.Add(at); tris.Add(at + 2); tris.Add(at + 3);
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
