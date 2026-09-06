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
// The shapes come generated from FlareMeshes and the material is the generated
// flare shader, so the effect needs no art asset, no prefab and nothing wired
// in the scene - TeleportFlare.Play is the whole interface.
public class TeleportFlare : MonoBehaviour
{
    private const float Duration = 0.55f;
    // How wide the ring gets, in world units, and how tall the column stands.
    private const float RingRadius = 1.9f;
    private const float ColumnHeight = 2.9f;

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

        _ring = MakePiece("Ring", FlareMeshes.Ring());
        _column = MakePiece("Column", FlareMeshes.Column());
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
        // Unscaled: half a second of light is not gameplay, and anything that
        // stops the clock in the middle of it - dying, finishing the level,
        // opening the shop - would otherwise leave the flare standing there for
        // as long as the game stayed stopped.
        _t += Time.unscaledDeltaTime / Duration;
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
}
