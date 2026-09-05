using System.Collections.Generic;
using UnityEngine;

// The generated shapes the flare shader is drawn on, shared by everything that
// uses it - the teleport burst and the last-coin indicator so far.
//
// They are built once and handed out, never modified by their users: a caller
// scales and positions the object, it does not touch the mesh. Both lie about
// the origin with the floor at y = 0, so a caller sets the object down on the
// ground and the shape stands up from there.
//
// UVs are what the shader's travelling bands run on: u goes round (or along),
// v goes across (or up). Vertex alpha carries the soft edges, which is how the
// shapes end in air rather than in a hard line without a texture anywhere.
public static class FlareMeshes
{
    private static Mesh _ring;
    private static Mesh _column;

    // A flat band on the floor, one unit across, sitting just clear of the
    // ground so it doesn't fight the tile's own surface for depth. Three rows
    // of vertices: the middle one carries the light and the two edges are at
    // zero, which is what gives the band its soft edges.
    public static Mesh Ring()
    {
        if (_ring != null) return _ring;

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

        AddGridTriangles(tris, segments, radii.Length);
        _ring = Finish("FlareRing", verts, uvs, colors, tris);
        return _ring;
    }

    // An open tube standing on the floor, one unit tall and one across, bright
    // at the bottom and fading out at the top so it ends in air rather than in
    // a hard edge. No caps: seeing straight up the inside of it is what makes
    // it a shaft of light instead of a cylinder.
    public static Mesh Column()
    {
        if (_column != null) return _column;

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

        AddGridTriangles(tris, segments, rows);
        _column = Finish("FlareColumn", verts, uvs, colors, tris);
        return _column;
    }

    // Both shapes are the same grid of rings by rows, so they are stitched the
    // same way.
    private static void AddGridTriangles(List<int> tris, int segments, int rows)
    {
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
    }

    public static Mesh Finish(string name, List<Vector3> verts, List<Vector2> uvs, List<Color> colors, List<int> tris)
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
