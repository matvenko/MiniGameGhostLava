using System.Collections.Generic;
using UnityEngine;

// Draws the pickup coin as a minted disc: two faces carrying the ghost emblem,
// joined by a plain gold rim.
//
// The prefab was Unity's built-in cylinder, which cannot show an emblem at all -
// its cap UVs are not a usable disc, so a face texture lands smeared across them.
// A coin is small enough to build outright, and building it here means the caps
// get the one mapping that matters: the texture's inscribed circle IS the face,
// so the 3D coin and the HUD icon are the same drawing.
//
// Same arrangement as GroundSurface: the generated mesh lives on a child marked
// DontSave, and the cylinder's own renderer is suppressed through the
// non-serialized Renderer.forceRenderingOff, so nothing leaks into the prefab.
[ExecuteAlways]
public class CoinSurface : MonoBehaviour
{
    [Tooltip("Sides around the rim. The coin is never large on screen, so this can stay low.")]
    [SerializeField, Range(8, 64)] private int segments = 32;
    [Tooltip("Where the rim band samples the face texture. Anywhere outside the face circle is flat gold.")]
    [SerializeField] private Vector2 rimUV = new Vector2(0.045f, 0.045f);

    // Unity's cylinder primitive is 1 unit across and 2 tall. Matching those
    // figures keeps every transform that was built around it the right size.
    private const float Radius = 0.5f;
    private const float HalfHeight = 1f;

    private Transform _surface;
    private MeshRenderer _host;

    void OnEnable() => Rebuild();

    void OnDisable()
    {
        DestroySurface();
        ShowHost(true);
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        DestroySurface();

        _host = GetComponent<MeshRenderer>();
        if (_host == null) return;

        var go = new GameObject("CoinSurface");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = BuildMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _host.sharedMaterial;
        mr.shadowCastingMode = _host.shadowCastingMode;
        mr.receiveShadows = _host.receiveShadows;
        _surface = go.transform;

        ShowHost(false);
    }

    private void ShowHost(bool visible)
    {
        if (_host != null) _host.forceRenderingOff = !visible;
    }

    private void DestroySurface()
    {
        if (_surface == null) return;
        if (Application.isPlaying) Destroy(_surface.gameObject);
        else DestroyImmediate(_surface.gameObject);
        _surface = null;
    }

    private Mesh BuildMesh()
    {
        int n = Mathf.Max(8, segments);

        var ring = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = 2f * Mathf.PI * i / n;
            ring[i] = new Vector3(Mathf.Sin(a) * Radius, 0f, Mathf.Cos(a) * Radius);
        }

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        AddCap(verts, norms, uvs, tris, ring, true);
        AddCap(verts, norms, uvs, tris, ring, false);
        AddRim(verts, norms, uvs, tris, ring);

        var mesh = new Mesh { name = "CoinDisc" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddCap(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3[] ring, bool up)
    {
        int n = ring.Length;
        int centre = verts.Count;
        float y = up ? HalfHeight : -HalfHeight;
        Vector3 nrm = up ? Vector3.up : Vector3.down;

        verts.Add(new Vector3(0f, y, 0f));
        norms.Add(nrm);
        uvs.Add(CapUV(Vector3.zero, up));

        for (int i = 0; i < n; i++)
        {
            verts.Add(new Vector3(ring[i].x, y, ring[i].z));
            norms.Add(nrm);
            uvs.Add(CapUV(ring[i], up));
        }

        // Wound clockwise seen from outside the cap, which is the way round Unity
        // treats as front-facing.
        for (int i = 0; i < n; i++)
        {
            int a = centre + 1 + i;
            int b = centre + 1 + (i + 1) % n;
            tris.Add(centre);
            tris.Add(up ? a : b);
            tris.Add(up ? b : a);
        }
    }

    private void AddRim(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris,
        Vector3[] ring)
    {
        int n = ring.Length;
        int first = verts.Count;

        // One shared pair of vertices per step, so the rim shades as a smooth
        // cylinder. There is no UV seam to split it at - the whole band sits on a
        // single flat-gold texel.
        for (int i = 0; i < n; i++)
        {
            Vector3 outward = new Vector3(ring[i].x, 0f, ring[i].z).normalized;
            verts.Add(new Vector3(ring[i].x, HalfHeight, ring[i].z));
            verts.Add(new Vector3(ring[i].x, -HalfHeight, ring[i].z));
            norms.Add(outward);
            norms.Add(outward);
            uvs.Add(rimUV);
            uvs.Add(rimUV);
        }

        for (int i = 0; i < n; i++)
        {
            int t0 = first + i * 2;
            int b0 = t0 + 1;
            int t1 = first + ((i + 1) % n) * 2;
            int b1 = t1 + 1;

            tris.Add(t0); tris.Add(b0); tris.Add(b1);
            tris.Add(t0); tris.Add(b1); tris.Add(t1);
        }
    }

    // The face texture's inscribed circle is the coin face, so the mapping is a
    // straight radial one. The back face mirrors u, and both flip v, so the ghost
    // stands upright and unmirrored on either side once the prefab stands the coin
    // on its edge - that +90 degree X rotation puts local -Z at world up.
    private static Vector2 CapUV(Vector3 p, bool up) =>
        new Vector2(0.5f + (up ? p.x : -p.x) / (2f * Radius),
                    0.5f - p.z / (2f * Radius));
}
