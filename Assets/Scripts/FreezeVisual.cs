using System.Collections.Generic;
using UnityEngine;

// The ice an enemy is encased in while it is stunned - by a trap it walked
// onto, or by the freeze ability stopping the whole board at once.
//
// It has to work on three enemies that came from three different asset packs,
// whose materials share no colour property to tint, so nothing here touches
// them: the shell is a separate mesh built around whatever the enemy's
// renderers occupy, and the enemy is simply seen through it. That also means a
// fourth kind of enemy needs no art of its own to be freezable.
//
// The mesh is generated once and shared - a jittered icosphere, flat-shaded so
// it reads as carved facets rather than a bubble, with each facet's brightness
// baked into vertex colour for the shader to spread out.
public class FreezeVisual : MonoBehaviour
{
    private const float PopDuration = 0.16f;
    private const float ShatterDuration = 0.3f;
    // How far past the enemy's own silhouette the ice reaches.
    private const float Padding = 1.18f;

    private static Mesh _sharedMesh;

    private Transform _shell;
    private Material _material;
    private Vector3 _restScale;
    private float _popTimer;
    private float _shatterTimer;
    private bool _shattering;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    public void Show()
    {
        if (_shell == null && !Build()) return;

        _shattering = false;
        _shatterTimer = 0f;
        if (!_shell.gameObject.activeSelf)
        {
            _shell.gameObject.SetActive(true);
            _popTimer = 0f;
        }
        if (_material != null) _material.SetFloat(FadeId, 1f);
    }

    // The stun ran out: the ice swells and fades instead of blinking off, which
    // is what sells it as breaking rather than as the effect being switched off.
    public void Shatter()
    {
        if (_shell == null || !_shell.gameObject.activeSelf) return;
        _shattering = true;
        _shatterTimer = 0f;
    }

    // For resets that aren't a thaw - a respawn, a level change - where there
    // is nothing to watch break.
    public void HideImmediate()
    {
        _shattering = false;
        if (_shell != null) _shell.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_shell == null || !_shell.gameObject.activeSelf) return;

        if (_shattering)
        {
            _shatterTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_shatterTimer / ShatterDuration);
            _shell.localScale = _restScale * Mathf.Lerp(1f, 1.3f, t);
            if (_material != null) _material.SetFloat(FadeId, 1f - t);
            if (t >= 1f) HideImmediate();
            return;
        }

        if (_popTimer < PopDuration)
        {
            _popTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_popTimer / PopDuration);
            // Out-cubic to full size with a small overshoot, so the ice closes
            // over the enemy with a snap rather than growing into place.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.Lerp(0.3f, 1f, eased) + Mathf.Sin(t * Mathf.PI) * 0.12f;
            _shell.localScale = _restScale * scale;
            return;
        }

        // Barely there, but it keeps a frozen enemy from being a still image on
        // a moving board.
        _shell.localScale = _restScale * (1f + Mathf.Sin(Time.time * 2.2f) * 0.012f);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    // Returns false when there is nothing to wrap - an enemy whose renderers
    // aren't there yet - so Show can be tried again on the next stun.
    private bool Build()
    {
        var shader = Shader.Find("Custom/FreezeShell_URP");
        if (shader == null) return false;

        if (!TryGetEnemyBounds(out Bounds bounds)) return false;

        var go = new GameObject("FreezeShell");
        _shell = go.transform;
        _shell.SetParent(transform, false);
        _shell.localPosition = transform.InverseTransformPoint(bounds.center);
        _shell.localRotation = Quaternion.identity;

        // The mesh is a unit-radius ball, so the scale is the enemy's own size
        // - taken out of the parent's scale, since it is a child of it.
        Vector3 lossy = transform.lossyScale;
        _restScale = new Vector3(
            bounds.size.x * Padding / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            bounds.size.y * Padding / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            bounds.size.z * Padding / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
        _shell.localScale = _restScale;

        go.AddComponent<MeshFilter>().sharedMesh = SharedMesh();

        _material = new Material(shader);
        var shellRenderer = go.AddComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = _material;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;

        _popTimer = 0f;
        return true;
    }

    // Every renderer under the enemy except any shell of our own, in world
    // space. Skinned meshes report the bounds of the pose they are in, which is
    // the whole point - the ice fits the enemy as it stands.
    private bool TryGetEnemyBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.transform == _shell) continue;
            if (any) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; any = true; }
        }
        return any;
    }

    private static Mesh SharedMesh()
    {
        if (_sharedMesh != null) return _sharedMesh;

        BuildIcosphere(out List<Vector3> positions, out List<int> faces);

        // Push each vertex off the sphere by a fixed amount for its direction,
        // so the ball comes out lumpy like a frozen-over shape instead of
        // perfectly round - and the same every time, since the hash is on the
        // position rather than on Random.
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 p = positions[i];
            float jitter = 1f + (Hash(p) - 0.5f) * 0.26f;
            positions[i] = p * jitter;
        }

        // Flat shading: every triangle gets its own three vertices, its own
        // normal, and its own brightness.
        var verts = new List<Vector3>(faces.Count);
        var normals = new List<Vector3>(faces.Count);
        var colors = new List<Color>(faces.Count);
        var tris = new List<int>(faces.Count);

        for (int i = 0; i < faces.Count; i += 3)
        {
            Vector3 a = positions[faces[i]];
            Vector3 b = positions[faces[i + 1]];
            Vector3 c = positions[faces[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            float shade = Hash((a + b + c) * 0.3333f + Vector3.one * 7.31f);
            var color = new Color(shade, shade, shade, 1f);

            foreach (var v in new[] { a, b, c })
            {
                tris.Add(verts.Count);
                verts.Add(v * 0.5f); // unit diameter, so scale can be the size
                normals.Add(normal);
                colors.Add(color);
            }
        }

        _sharedMesh = new Mesh { name = "FreezeShell" };
        _sharedMesh.SetVertices(verts);
        _sharedMesh.SetNormals(normals);
        _sharedMesh.SetColors(colors);
        _sharedMesh.SetTriangles(tris, 0);
        _sharedMesh.RecalculateBounds();
        return _sharedMesh;
    }

    // An icosahedron with every triangle split once and the new points pushed
    // out to the sphere - 80 facets, which is enough to read as faceted ice
    // and few enough that each facet is still visibly its own plane.
    private static void BuildIcosphere(out List<Vector3> positions, out List<int> faces)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        positions = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < positions.Count; i++) positions[i] = positions[i].normalized;

        int[] baseFaces =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };

        var midpoints = new Dictionary<long, int>();
        var positionsRef = positions;
        int Midpoint(int a, int b)
        {
            long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (midpoints.TryGetValue(key, out int existing)) return existing;
            int index = positionsRef.Count;
            positionsRef.Add(((positionsRef[a] + positionsRef[b]) * 0.5f).normalized);
            midpoints[key] = index;
            return index;
        }

        faces = new List<int>(baseFaces.Length * 4);
        for (int i = 0; i < baseFaces.Length; i += 3)
        {
            int a = baseFaces[i], b = baseFaces[i + 1], c = baseFaces[i + 2];
            int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
            faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
    }

    // Deterministic 0..1 noise from a position - the usual sine hash, so the
    // shell is identical every run and needs nothing seeded or stored.
    private static float Hash(Vector3 p)
    {
        float v = Mathf.Sin(p.x * 12.9898f + p.y * 78.233f + p.z * 37.719f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }
}
