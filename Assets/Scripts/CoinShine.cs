using UnityEngine;
using UnityEngine.Rendering;

// Now and then a hard four-point sparkle flashes on the coin's edge, the way
// light catches polished gold. It is what makes a coin read as treasure from
// across the board, without a halo fogging the ground around it.
//
// The sparkle hangs off the prefab root rather than the tumbling disc, so it
// stays a point of light instead of spinning with the coin, and it is drawn
// with Graphics.DrawMesh so nothing extra is serialized into the prefab.
[RequireComponent(typeof(Coin))]
public sealed class CoinShine : MonoBehaviour
{
    [Tooltip("Seconds between sparkles, picked at random from this range for every flash.")]
    [SerializeField] private Vector2 interval = new Vector2(1.4f, 3.2f);
    [Tooltip("How long one sparkle lasts, from nothing to full and back.")]
    [SerializeField] private float flashDuration = 0.45f;
    [Tooltip("Sparkle size in world units at its brightest.")]
    [SerializeField] private float size = 0.5f;
    [SerializeField, ColorUsage(false, true)] private Color tint = new Color(4f, 3.2f, 1.5f);

    private static Mesh _quad;
    private static Material _material;

    private Collider _pickup;
    private float _nextFlash;
    private float _flashStart = float.NegativeInfinity;
    private Vector3 _offset;

    void Start()
    {
        _pickup = GetComponent<Collider>();
        EnsureShared();
        // Coins spawn together; without this they would all sparkle in step.
        _nextFlash = Time.time + Random.Range(0f, interval.y);
    }

    void LateUpdate()
    {
        if (_material == null) return;
        // Taken coins shrink away; a sparkle then would read as a second pickup.
        if (_pickup != null && !_pickup.enabled) return;

        if (Time.time >= _nextFlash)
        {
            _flashStart = Time.time;
            _nextFlash = Time.time + flashDuration + Random.Range(interval.x, interval.y);
            // Somewhere on the rim, a little above the disc so the coin never hides it.
            float a = Random.Range(0f, Mathf.PI * 2f);
            float rim = Radius() * 0.8f;
            _offset = new Vector3(Mathf.Cos(a) * rim, 0.12f, Mathf.Sin(a) * rim);
        }

        float p = (Time.time - _flashStart) / flashDuration;
        if (p < 0f || p > 1f) return;
        // Snaps on and eases off: a glint, not a fade.
        float strength = Mathf.Sin(p * Mathf.PI);
        strength *= strength;

        Transform root = transform.parent != null ? transform.parent : transform;
        float scale = size * (0.6f + 0.4f * strength);
        var matrix = Matrix4x4.TRS(root.position + _offset, Quaternion.identity, Vector3.one * scale);
        var block = Block(strength);
        Graphics.DrawMesh(_quad, matrix, _material, gameObject.layer, null, 0, block,
            ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
    }

    // Half the disc's width on the board, taken from its scale so a resized
    // prefab keeps the sparkle on its edge.
    private float Radius() => 0.5f * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

    private MaterialPropertyBlock _block;
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");

    private MaterialPropertyBlock Block(float strength)
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        _block.SetColor(TintId, tint);
        _block.SetFloat(StrengthId, strength);
        return _block;
    }

    // Every coin draws the same quad with the same material; only the property
    // block differs, so twenty coins cost one material and one mesh.
    private static void EnsureShared()
    {
        if (_material == null)
        {
            var shader = Resources.Load<Shader>("CoinEffects/CoinGlint");
            if (shader != null) _material = new Material(shader) { name = "Coin glint (runtime)" };
        }
        if (_quad == null)
        {
            _quad = new Mesh { name = "Coin glint quad" };
            _quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one);
        }
    }
}
