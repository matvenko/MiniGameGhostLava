using System.Collections.Generic;
using UnityEngine;

// The burst of light at each end of a teleport: one where the character was
// standing as it leaves, one over the tile it lands on.
//
// Both ends use the same two pieces - a ring lying on the floor and a column
// of light standing in it - played in opposite directions. Leaving, the ring
// throws itself outward and the column shoots up and thins away, as if the
// character were pulled out through it. Arriving, the ring sweeps inward and
// the column drops down into the floor, gathering into the spot the character
// is about to be standing on. Same shapes, read backwards, so the two ends
// belong to the same trick.
//
// The meshes are generated here and shared by every flare, and the material is
// the generated flare shader, so the effect needs no art asset, no prefab and
// nothing wired in the scene - TeleportFlare.Play is the whole interface.
public class TeleportFlare : MonoBehaviour
{
    private const float Duration = 0.55f;
    // How wide the ring gets, in world units, and how tall the column stands.
    private const float RingRadius = 1.9f;
    private const float ColumnHeight = 2.9f;

    private static Mesh _ringMesh;
    private static Mesh _columnMesh;
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private Transform _ring;
    private Transform _column;
    private Material _material;
    private bool _arriving;
    private float _t;

    // position is the floor the character stands on - the flare is built up
    // from there. Nothing to dispose of: it takes itself off the board when it
    // has finished playing.
    public static void Play(Vector3 position, bool arriving)
    {
        var shader = Shader.Find("Custom/TeleportFlare_URP");
        if (shader == null) return;

        var go = new GameObject(arriving ? "TeleportFlare (in)" : "TeleportFlare (out)");
        go.transform.position = position;
        go.AddComponent<TeleportFlare>().Build(shader, arriving);
    }

    private void Build(Shader shader, bool arriving)
    {
        _arriving = arriving;
        _material = new Material(shader);

        _ring = MakePiece("Ring", RingMesh());
        _column = MakePiece("Column", ColumnMesh());
        Apply(0f);
    }

    private Transform MakePiece(string name, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        // Light thrown by a flare that lasts half a second has no business in
        // the light probes or reflection probes it would otherwise disturb.
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    void Update()
    {
        _t += Time.deltaTime / Duration;
        if (_t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Apply(_t);
    }

    // t runs 0..1 over the life of the flare. Leaving reads it forwards,
    // arriving backwards - which is the whole difference between the two.
    private void Apply(float t)
    {
        float shape = _arriving ? 1f - t : t;

        // Out-cubic, so the ring is already at speed on the first frame and
        // eases out - a burst rather than something growing.
        float eased = 1f - Mathf.Pow(1f - shape, 3f);

        float ringScale = Mathf.Lerp(0.25f, RingRadius, eased);
        _ring.localScale = new Vector3(ringScale, 1f, ringScale);

        // The column climbs as it narrows, so the light looks like it is being
        // drawn off the floor rather than stretched.
        float columnWidth = Mathf.Lerp(0.95f, 0.28f, eased);
        _column.localScale = new Vector3(columnWidth, Mathf.Lerp(0.35f, ColumnHeight, eased), columnWidth);

        // The fade always runs with real time, not with the shape: arriving,
        // the flare has to be gone by the time the character is standing there,
        // and leaving it has to be gone before the eye goes looking for the
        // character that isn't there any more.
        if (_material != null) _material.SetFloat(FadeId, 1f - t * t);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    // A flat band on the floor, one unit across, sitting just clear of the
    // tile so it doesn't fight the tile's own surface for depth. Three rows of
    // vertices: the middle one carries the light and the two edges are at zero,
    // which is what gives the band its soft edges without a texture.
    private static Mesh RingMesh()
    {
        if (_ringMesh != null) return _ringMesh;

        const int segments = 56;
        const float lift = 0.06f;
        float[] radii = { 0.55f, 0.8f, 1f };
        float[] energy = { 0f, 1f, 0f };

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var tris = new List<int>();

        for (int s = 0; s <= segments; s++)
        {
            float u = s / (float)segments;
            float angle = u * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);

            for (int r = 0; r < radii.Length; r++)
            {
                verts.Add(new Vector3(cos * radii[r], lift, sin * radii[r]));
                uvs.Add(new Vector2(u, r * 0.5f));
                colors.Add(new Color(1f, 1f, 1f, energy[r]));
            }
        }

        for (int s = 0; s < segments; s++)
        {
            int a = s * radii.Length;
            int b = (s + 1) * radii.Length;
            for (int r = 0; r < radii.Length - 1; r++)
            {
                tris.Add(a + r); tris.Add(a + r + 1); tris.Add(b + r);
                tris.Add(b + r); tris.Add(a + r + 1); tris.Add(b + r + 1);
            }
        }

        _ringMesh = Finish("TeleportRing", verts, uvs, colors, tris);
        return _ringMesh;
    }

    // An open tube standing on the floor, one unit tall and one across, bright
    // at the bottom and fading out at the top so it ends in air rather than in
    // a hard edge. No caps: seeing straight up the inside of it is what makes
    // it a shaft of light instead of a cylinder.
    private static Mesh ColumnMesh()
    {
        if (_columnMesh != null) return _columnMesh;

        const int segments = 40;
        const int rows = 5;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var tris = new List<int>();

        for (int s = 0; s <= segments; s++)
        {
            float u = s / (float)segments;
            float angle = u * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);

            for (int r = 0; r < rows; r++)
            {
                float v = r / (float)(rows - 1);
                // The tube narrows as it rises, which reads as the light
                // tapering off rather than being cut off.
                float radius = Mathf.Lerp(1f, 0.45f, v);
                verts.Add(new Vector3(cos * radius, v, sin * radius));
                uvs.Add(new Vector2(u, v));
                colors.Add(new Color(1f, 1f, 1f, (1f - v) * (1f - v)));
            }
        }

        for (int s = 0; s < segments; s++)
        {
            int a = s * rows;
            int b = (s + 1) * rows;
            for (int r = 0; r < rows - 1; r++)
            {
                tris.Add(a + r); tris.Add(a + r + 1); tris.Add(b + r);
                tris.Add(b + r); tris.Add(a + r + 1); tris.Add(b + r + 1);
            }
        }

        _columnMesh = Finish("TeleportColumn", verts, uvs, colors, tris);
        return _columnMesh;
    }

    private static Mesh Finish(string name, List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
