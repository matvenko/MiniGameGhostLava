using UnityEngine;

// Renders the hazard liquid (lava or water) as ONE continuous surface stretched
// over the whole board instead of a texture repeating on every 1x1 tile.
//
// How it works: a single subdivided quad is generated across the full tile
// footprint and parked slightly below the top of the walkable blocks. The
// blocks are opaque cubes that span that height, so they punch the liquid out
// wherever the floor is solid, and it only stays visible in the recessed lava
// cells - exactly like water sitting in a pool between islands. Because the
// surface is one mesh, the AQUIS shader's waves, foam and depth fade run across
// the whole pool continuously rather than restarting at every cell border.
//
// The lava tiles themselves keep their trigger colliders and LavaHazard (that
// is what kills the player); only their rendering is switched off so the shared
// surface shows through.
//
// [ExecuteAlways] so the board looks the same in the editor as it does in play
// mode. The generated meshes are flagged DontSave and the tiles are hidden via
// Renderer.forceRenderingOff (which is not serialized), so nothing this class
// does can leak into the saved scene.
[ExecuteAlways]
public class LiquidSurface : MonoBehaviour
{
    public static LiquidSurface Instance { get; private set; }

    [Header("Materials")]
    [Tooltip("AQUIS material used for the shared surface (e.g. M_StylizedLava or M_StylizedColdWater).")]
    [SerializeField] private Material liquidMaterial;
    [Tooltip("Optional opaque material for the pool bottom, seen through the liquid. Leave empty to skip the bed.")]
    [SerializeField] private Material bedMaterial;

    [Header("Layout")]
    [Tooltip("Fine-tune the surface height relative to the top of the lava tiles.")]
    [SerializeField] private float surfaceYOffset = 0f;
    [Tooltip("How far below the surface the pool bottom sits. Drives the shader's depth fade / intersection foam.")]
    [SerializeField] private float bedDepth = 0.6f;
    [Tooltip("Extra size added around the tile footprint so the surface tucks under the surrounding walls.")]
    [SerializeField] private float padding = 0.5f;
    [Tooltip("Mesh density. The AQUIS shader displaces vertices for waves, so a flat 2-triangle quad would not ripple.")]
    [SerializeField] private float verticesPerUnit = 4f;

    [Header("Tiles")]
    [Tooltip("Hide the per-cell lava cubes so the shared surface is what you see. Turn off to keep them as the pool bottom.")]
    [SerializeField] private bool hideLavaTiles = true;

    private Transform _surface;
    private Transform _bed;

    // Cached so teardown never has to call GameObject.Find: during scene unload
    // and domain reload the parents are already deactivated and Find asserts.
    private Transform _blocksParent;
    private Transform _lavaParent;

    void Awake()
    {
        Instance = this;
    }

    // OnEnable rather than Start so this also runs after every domain reload in
    // the editor, where the DontSave meshes from the previous session are gone.
    void OnEnable()
    {
        Build();
        Refresh();
    }

    void OnDisable()
    {
        DestroyPlane(_surface);
        DestroyPlane(_bed);
        _surface = null;
        _bed = null;
        // Cached lookups only - resolving here would hit GameObject.Find, which
        // asserts once the scene starts unloading or a domain reload begins.
        RestoreCachedTiles();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Swaps the whole pool between themes (lava level vs water level) without
    // touching any tile - the surface is a single renderer.
    public void SetLiquidMaterial(Material material)
    {
        liquidMaterial = material;
        if (_surface != null) _surface.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // Called after the level layout is reshuffled: the set of cells that count
    // as lava changed, so re-decide which tile renderers are visible.
    [ContextMenu("Refresh")]
    public void Refresh()
    {
        ResolveParents();

        // Ownership is split strictly by parent: this component controls only the
        // Lava tiles, GroundSurface controls only the Blocks tiles. An earlier
        // version touched both and the two fought - whichever ran last won, so
        // the block cubes reappeared depending on enable order.
        if (_lavaParent != null)
        {
            foreach (Transform t in _lavaParent) SetTileVisible(t, !hideLavaTiles);
        }
    }

    private void ResolveParents()
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
    }

    private static void DestroyPlane(Transform plane)
    {
        if (plane == null) return;
        if (Application.isPlaying) Destroy(plane.gameObject);
        else DestroyImmediate(plane.gameObject);
    }

    // Puts every tile renderer back, so disabling or deleting this component
    // never leaves the board with invisible cells.
    [ContextMenu("Restore Tile Rendering")]
    public void RestoreTileRendering()
    {
        ResolveParents();
        RestoreCachedTiles();
    }

    private void RestoreCachedTiles()
    {
        if (_lavaParent != null) foreach (Transform t in _lavaParent) SetTileVisible(t, true);
    }

    // forceRenderingOff instead of MeshRenderer.enabled: it is a runtime-only
    // flag that is never serialized, so toggling it in the editor neither
    // dirties the scene nor risks being saved with tiles left hidden.
    private static void SetTileVisible(Transform tile, bool visible)
    {
        var mr = tile.GetComponent<MeshRenderer>();
        if (mr != null) mr.forceRenderingOff = !visible;
    }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        // Rebuild is exposed as a context menu, so it can legitimately run while
        // the editor is not playing - where Destroy() is an error.
        DestroyPlane(_surface);
        DestroyPlane(_bed);

        if (!TryGetFootprint(out Bounds footprint, out float lavaTopY)) return;

        float sizeX = footprint.size.x + padding * 2f;
        float sizeZ = footprint.size.z + padding * 2f;
        float surfaceY = lavaTopY + surfaceYOffset;

        if (liquidMaterial != null)
        {
            _surface = CreatePlane("LiquidSurface", footprint.center, surfaceY, sizeX, sizeZ,
                liquidMaterial, Mathf.Max(1, Mathf.RoundToInt(verticesPerUnit)));
        }

        if (bedMaterial != null)
        {
            // A single flat bottom is enough: everything outside the lava cells
            // is hidden behind the solid blocks anyway.
            _bed = CreatePlane("LiquidBed", footprint.center, surfaceY - bedDepth, sizeX, sizeZ,
                bedMaterial, 1);
        }
    }

    // The board is authored in the scene rather than spawned, so the footprint
    // is read back from the tiles instead of being configured twice.
    private bool TryGetFootprint(out Bounds footprint, out float lavaTopY)
    {
        footprint = default;
        lavaTopY = 0f;

        var blocksParent = GameObject.Find("Blocks");
        var lavaParent = GameObject.Find("Lava");
        if (blocksParent == null && lavaParent == null) return false;

        var extents = new Bounds();
        bool any = false;
        float lavaTop = 0f, blockTop = 0f;
        bool anyLava = false, anyBlock = false;

        if (blocksParent != null)
        {
            foreach (Transform t in blocksParent.transform)
            {
                Accumulate(t, ref extents, ref any);
                float top = TopOf(t);
                blockTop = anyBlock ? Mathf.Max(blockTop, top) : top;
                anyBlock = true;
            }
        }

        if (lavaParent != null)
        {
            foreach (Transform t in lavaParent.transform)
            {
                Accumulate(t, ref extents, ref any);
                float top = TopOf(t);
                lavaTop = anyLava ? Mathf.Min(lavaTop, top) : top;
                anyLava = true;
            }
        }

        if (!any) return false;

        // A layout can legitimately contain zero lava cells; fall back to just
        // under the walkable floor so the surface still hides behind the blocks.
        lavaTopY = anyLava ? lavaTop : blockTop - 0.18f;

        footprint = new Bounds(
            new Vector3(extents.center.x, 0f, extents.center.z),
            new Vector3(extents.size.x, 0f, extents.size.z));
        return true;
    }

    private static float TopOf(Transform tile) => tile.position.y + tile.lossyScale.y * 0.5f;

    private static void Accumulate(Transform tile, ref Bounds extents, ref bool any)
    {
        var tileBounds = new Bounds(tile.position, tile.lossyScale);
        if (!any)
        {
            extents = tileBounds;
            any = true;
        }
        else
        {
            extents.Encapsulate(tileBounds);
        }
    }

    private Transform CreatePlane(string name, Vector3 center, float y, float sizeX, float sizeZ, Material material, int density)
    {
        var go = new GameObject(name);
        // Generated every time the component enables, so it must never be
        // serialized into the scene alongside the copy that gets rebuilt.
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(center.x, y, center.z);

        int cols = Mathf.Max(1, Mathf.CeilToInt(sizeX * density));
        int rows = Mathf.Max(1, Mathf.CeilToInt(sizeZ * density));

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildGridMesh(sizeX, sizeZ, cols, rows);

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go.transform;
    }

    // Flat grid in the XZ plane, centered on the origin, normals up. Subdivided
    // because the shader animates the waves by moving vertices.
    private static Mesh BuildGridMesh(float sizeX, float sizeZ, int cols, int rows)
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

        var mesh = new Mesh { name = "LiquidPlane" };
        if (verts.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
