using System.Collections.Generic;
using UnityEngine;

// The bubble around the character while the shield ability is up.
//
// It is built the way the freeze shell is - a generated sphere sized to
// whatever the character's renderers occupy, sitting alongside them rather
// than touching their materials - so the character needs no art of its own to
// be shieldable, and the two effects can be on screen at once without
// interfering.
//
// What it has that the ice does not is a clock. Five seconds of being
// untouchable is only useful if the player can feel it running out, so the
// last stretch pulses, faster and harder as it goes, and the bubble bursts
// rather than blinking off at zero.
public class ShieldBubble : MonoBehaviour
{
    private const float PopInDuration = 0.22f;
    private const float BurstDuration = 0.28f;
    // How long before the end the warning pulse starts.
    private const float WarnWindow = 1.6f;
    // How far past the character's own silhouette the bubble reaches.
    private const float Padding = 1.45f;

    private static Mesh _sharedMesh;
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private Transform _bubble;
    private Material _material;
    private Vector3 _restScale;
    private float _remaining;
    private float _popTimer;
    private float _burstTimer;
    private bool _bursting;

    // duration is what the ability granted, so the warning pulse knows when to
    // start. Re-showing while one is already up just resets the clock.
    public void Show(float duration)
    {
        if (_bubble == null && !Build()) return;

        _bursting = false;
        _burstTimer = 0f;
        _remaining = duration;

        if (!_bubble.gameObject.activeSelf)
        {
            _bubble.gameObject.SetActive(true);
            _popTimer = 0f;
        }
    }

    // The shield ran out: the bubble swells and thins away instead of
    // vanishing, which is what sells it as spent rather than switched off.
    public void Burst()
    {
        if (_bubble == null || !_bubble.gameObject.activeSelf) return;
        _bursting = true;
        _burstTimer = 0f;
    }

    // For resets that aren't the shield expiring - a death, a level change -
    // where there is nothing to watch break.
    public void HideImmediate()
    {
        _bursting = false;
        _remaining = 0f;
        if (_bubble != null) _bubble.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_bubble == null || !_bubble.gameObject.activeSelf) return;

        if (_bursting)
        {
            _burstTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_burstTimer / BurstDuration);
            _bubble.localScale = _restScale * Mathf.Lerp(1f, 1.45f, t);
            SetFade(1f - t);
            if (t >= 1f) HideImmediate();
            return;
        }

        _remaining -= Time.deltaTime;

        if (_popTimer < PopInDuration)
        {
            _popTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_popTimer / PopInDuration);
            // Out-cubic past full size and back, so the bubble snaps shut
            // around the character instead of growing into place.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _bubble.localScale = _restScale * (Mathf.Lerp(0.25f, 1f, eased) + Mathf.Sin(t * Mathf.PI) * 0.16f);
            SetFade(t);
            return;
        }

        // Steady state is a slow breath. Inside the last seconds that breath
        // turns into a pulse that quickens as the time goes, so the player
        // reads how much is left off the rhythm without a number anywhere.
        float urgency = _remaining > 0f ? Mathf.Clamp01(1f - _remaining / WarnWindow) : 1f;
        float rate = Mathf.Lerp(1.6f, 13f, urgency);
        float wave = Mathf.Sin(Time.time * rate);

        _bubble.localScale = _restScale * (1f + wave * Mathf.Lerp(0.015f, 0.055f, urgency));
        SetFade(1f - urgency * (0.5f + wave * 0.5f) * 0.75f);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    private void SetFade(float value)
    {
        if (_material != null) _material.SetFloat(FadeId, Mathf.Clamp01(value));
    }

    // Returns false when there is nothing to wrap - renderers that aren't
    // there yet - so Show can be tried again on the next activation.
    private bool Build()
    {
        var shader = Shader.Find("Custom/ShieldBubble_URP");
        if (shader == null) return false;

        if (!TryGetCharacterBounds(out Bounds bounds)) return false;

        var go = new GameObject("ShieldBubble");
        _bubble = go.transform;
        _bubble.SetParent(transform, false);
        _bubble.localPosition = transform.InverseTransformPoint(bounds.center);
        _bubble.localRotation = Quaternion.identity;

        // The mesh is a unit-diameter ball, so the scale is the character's own
        // size - taken out of the parent's scale, since it is a child of it.
        // Rounded to a single radius so the bubble is a sphere around a tall
        // thin character rather than an egg stretched over it.
        Vector3 lossy = transform.lossyScale;
        float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * Padding;
        _restScale = new Vector3(
            diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            diameter / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
        _bubble.localScale = _restScale;

        go.AddComponent<MeshFilter>().sharedMesh = SharedMesh();

        _material = new Material(shader);
        var bubbleRenderer = go.AddComponent<MeshRenderer>();
        bubbleRenderer.sharedMaterial = _material;
        bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bubbleRenderer.receiveShadows = false;
        bubbleRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        bubbleRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _popTimer = 0f;
        return true;
    }

    // Every renderer under the character except a bubble of our own, in world
    // space. Skinned meshes report the pose they are in, which is what keeps
    // the bubble fitted to the character as it moves.
    private bool TryGetCharacterBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (_bubble != null && r.transform == _bubble) continue;
            if (any) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; any = true; }
        }
        return any;
    }

    // A plain latitude/longitude sphere rather than the icosphere the ice uses:
    // the shield's cell pattern is drawn in the shader off the UVs, and this is
    // the sphere that has them - with the seam column duplicated, so the
    // pattern meets itself round the back instead of smearing across.
    private static Mesh SharedMesh()
    {
        if (_sharedMesh != null) return _sharedMesh;

        const int segments = 40; // around
        const int rings = 24;    // pole to pole

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int y = 0; y <= rings; y++)
        {
            float v = y / (float)rings;
            float polar = v * Mathf.PI;
            float sinPolar = Mathf.Sin(polar), cosPolar = Mathf.Cos(polar);

            for (int x = 0; x <= segments; x++)
            {
                float u = x / (float)segments;
                float azimuth = u * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(azimuth) * sinPolar, cosPolar, Mathf.Sin(azimuth) * sinPolar);

                verts.Add(dir * 0.5f); // unit diameter, so scale can be the size
                normals.Add(dir);
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = segments + 1;
        for (int y = 0; y < rings; y++)
        {
            for (int x = 0; x < segments; x++)
            {
                int a = y * stride + x;
                int b = a + stride;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        _sharedMesh = new Mesh { name = "ShieldBubble" };
        _sharedMesh.SetVertices(verts);
        _sharedMesh.SetNormals(normals);
        _sharedMesh.SetUVs(0, uvs);
        _sharedMesh.SetTriangles(tris, 0);
        _sharedMesh.RecalculateBounds();
        return _sharedMesh;
    }
}
