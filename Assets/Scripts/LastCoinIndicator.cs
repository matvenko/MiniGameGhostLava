using System.Collections.Generic;
using UnityEngine;

// Points the way to the last coin on the board.
//
// The camera sits close over the character - a few units of board fit on
// screen at a time - so hunting the final coin of a level across a
// twenty-eight tile map is a walk in the dark, and it is the one coin the
// player cannot finish the level without. From the moment exactly one is left,
// this turns on and stays on until it is taken.
//
// Two pieces, both flat on the floor because the game is played from straight
// overhead and anything standing up is seen end on. A chevron beside the
// character, swung round to face the coin, with the shader's bands running up
// it toward the point - so the arrow reads as flowing the way it wants you to
// go rather than just lying there. And a ring pulsing on the ground around the
// coin itself, for when the player gets close enough to see it: the arrow
// hands over to the ring instead of both crowding the same few tiles.
//
// It borrows the teleport flare's shader and its generated ring, tinted gold
// rather than cyan, so a coin marker is never mistaken for a jump.
public class LastCoinIndicator : MonoBehaviour
{
    [SerializeField] private Transform player;
    [Tooltip("How far from the character the arrow floats, in world units.")]
    [SerializeField] private float orbitRadius = 1.5f;
    [Tooltip("How big the arrow is drawn.")]
    [SerializeField] private float arrowScale = 0.9f;
    [Tooltip("Inside this distance the coin is on screen, so the arrow bows out and leaves it to the ring.")]
    [SerializeField] private float handoverDistance = 3.2f;
    [Tooltip("How wide the ring around the coin is drawn.")]
    [SerializeField] private float ringRadius = 0.85f;
    [Tooltip("Seconds the pieces take to come up and go away.")]
    [SerializeField] private float fadeDuration = 0.3f;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
    private static readonly Color Gold = new Color(1f, 0.72f, 0.18f, 1f);
    private static readonly Color GoldCore = new Color(1f, 0.96f, 0.8f, 1f);

    private static Mesh _arrowMesh;

    private Transform _arrow;
    private Transform _ring;
    private Material _arrowMaterial;
    private Material _ringMaterial;
    private float _arrowFade;
    private float _ringFade;
    private bool _built;

    void Update()
    {
        if (player == null) return;

        // One coin left and one only. Two is not a hunt, and none is a level
        // that is already over.
        var coins = Coin.Active;
        Coin target = coins.Count == 1 ? coins[0] : null;

        if (target == null)
        {
            FadeAway();
            return;
        }

        if (!_built && !Build()) return;

        Vector3 from = player.position;
        Vector3 to = target.transform.position;
        Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
        float distance = flat.magnitude;

        // Standing on top of it, there is no direction left to point in - the
        // arrow keeps whatever way it was last facing rather than spinning.
        if (distance > 0.01f)
        {
            Vector3 dir = flat / distance;
            _arrow.position = new Vector3(from.x, from.y, from.z) + dir * orbitRadius;
            _arrow.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // A slow breath on both, so a marker that stays on screen for a long
        // walk never settles into a decal.
        float breath = 1f + Mathf.Sin(Time.time * 3.4f) * 0.06f;
        _arrow.localScale = Vector3.one * (arrowScale * breath);

        _ring.position = new Vector3(to.x, from.y, to.z);
        float ringBreath = 1f + Mathf.Sin(Time.time * 2.6f) * 0.09f;
        _ring.localScale = new Vector3(ringRadius * ringBreath, 1f, ringRadius * ringBreath);

        // Close in, the coin is on screen and the arrow is in the way of it.
        SetFades(distance > handoverDistance ? 1f : 0f, 1f);
    }

    private void FadeAway()
    {
        if (!_built) return;
        SetFades(0f, 0f);
    }

    private void SetFades(float arrowTarget, float ringTarget)
    {
        float step = fadeDuration > 0f ? Time.deltaTime / fadeDuration : 1f;
        _arrowFade = Mathf.MoveTowards(_arrowFade, arrowTarget, step);
        _ringFade = Mathf.MoveTowards(_ringFade, ringTarget, step);

        _arrowMaterial.SetFloat(FadeId, _arrowFade);
        _ringMaterial.SetFloat(FadeId, _ringFade);

        // Fully faded pieces are switched off rather than drawn at zero, so a
        // level with plenty of coins left costs nothing to render.
        _arrow.gameObject.SetActive(_arrowFade > 0.001f);
        _ring.gameObject.SetActive(_ringFade > 0.001f);
    }

    // Returns false if the shader is missing, leaving the indicator off rather
    // than half-built.
    private bool Build()
    {
        var shader = Shader.Find("Custom/TeleportFlare_URP");
        if (shader == null) return false;

        _arrowMaterial = MakeMaterial(shader);
        _ringMaterial = MakeMaterial(shader);

        _arrow = MakePiece("Arrow", ArrowMesh(), _arrowMaterial);
        _ring = MakePiece("CoinRing", FlareMeshes.Ring(), _ringMaterial);

        _built = true;
        return true;
    }

    private static Material MakeMaterial(Shader shader)
    {
        var material = new Material(shader);
        material.SetColor(ColorId, Gold);
        material.SetColor(CoreColorId, GoldCore);
        material.SetFloat(FadeId, 0f);
        return material;
    }

    private Transform MakePiece(string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        go.SetActive(false);
        return go.transform;
    }

    void OnDestroy()
    {
        if (_arrowMaterial != null) Destroy(_arrowMaterial);
        if (_ringMaterial != null) Destroy(_ringMaterial);
    }

    // A chevron lying flat, pointing along +Z so LookRotation can aim it, built
    // as two slanted arms rather than a solid triangle - a stroke reads as a
    // direction where a filled shape reads as a thing sitting on the floor.
    //
    // u runs from the tail of each arm to the point, which is what sends the
    // shader's travelling band up the arrow the way the player is meant to go,
    // and the alpha at the tails is what keeps the arms from ending in a cut.
    private static Mesh ArrowMesh()
    {
        if (_arrowMesh != null) return _arrowMesh;

        const float lift = 0.06f;
        const float halfSpan = 0.5f;   // how far back the arms sweep
        const float tipZ = 0.52f;
        const float thickness = 0.3f;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var tris = new List<int>();

        // Both arms, mirrored: -1 is the left one, +1 the right.
        for (int s = -1; s <= 1; s += 2)
        {
            int b = verts.Count;

            // Outer edge, tip to tail, then the inner edge a thickness behind
            // it - so the arm is a band of even width sweeping back from the
            // point.
            verts.Add(new Vector3(0f, lift, tipZ));
            verts.Add(new Vector3(0f, lift, tipZ - thickness));
            verts.Add(new Vector3(s * halfSpan, lift, tipZ - halfSpan));
            verts.Add(new Vector3(s * halfSpan, lift, tipZ - halfSpan - thickness));

            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(0f, 0f));

            colors.Add(new Color(1f, 1f, 1f, 1f));
            colors.Add(new Color(1f, 1f, 1f, 1f));
            colors.Add(new Color(1f, 1f, 1f, 0.18f));
            colors.Add(new Color(1f, 1f, 1f, 0.18f));

            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }

        _arrowMesh = FlareMeshes.Finish("CoinArrow", verts, uvs, colors, tris);
        return _arrowMesh;
    }
}
