using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sample;

// A collectible square-cut gemstone, independent of the ordinary coin objective.
//
// The board is seen from straight above, so the stone is authored as a flat
// diamond on the screen: a pale table in the middle, a ring of light and deep
// blue facets around it, a cyan halo on the ground, a soft shadow and two
// four-point glints. It does not spin - from above a spinning cut only reads as
// a turning outline - it bobs and rocks a few degrees so the facets shimmer.
public sealed class Moonshard : MonoBehaviour
{
    private const float Outer = .34f;   // centre to girdle corner
    private const float Table = .15f;   // centre to table corner
    private const float Crown = .11f;   // table height above the girdle
    private const float Culet = -.24f;  // pavilion point below the girdle
    private const float GroundDrop = -.32f;

    private static Mesh gemMesh, quadMesh;
    private static Material gemMaterial, haloMaterial, shadowMaterial, glintMaterial;
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    private Transform visual, halo, shadow;
    private readonly Transform[] glints = new Transform[2];
    private readonly Renderer[] glintRenderers = new Renderer[2];
    private MaterialPropertyBlock block;
    private GhostScript player;
    private bool collected;
    private float phase;

    // Every stone still out on the board, left the moment it is taken - the same
    // list the coins keep, for the HUD to see what it is covering.
    private static readonly List<Moonshard> Uncollected = new List<Moonshard>();
    public static IReadOnlyList<Moonshard> Active => Uncollected;

    private void OnEnable()
    {
        if (!collected) Uncollected.Add(this);
    }

    private void OnDisable()
    {
        Uncollected.Remove(this);
    }

    public static void Create(Vector3 position, Transform parent)
    {
        var go = new GameObject("Moonshard");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.AddComponent<Moonshard>();
    }

    // The camera looks down with up on the screen along world -Z and right
    // along world -X; facets are laid out in those screen terms.
    private static Vector3 OnScreen(float right, float height, float up) => new Vector3(-right, height, -up);

    private void Awake()
    {
        phase = Random.value * Mathf.PI * 2f;
        block = new MaterialPropertyBlock();

        shadow = Flat("Soft shadow", ShadowMaterial(), OnScreen(.17f, GroundDrop, -.19f), .78f);
        halo = Flat("Cyan halo", HaloMaterial(), new Vector3(0f, GroundDrop + .01f, 0f), 1.25f);

        visual = new GameObject("Square cut diamond").transform;
        visual.SetParent(transform, false);
        visual.gameObject.AddComponent<MeshFilter>().sharedMesh = GemMesh();
        var stone = visual.gameObject.AddComponent<MeshRenderer>();
        stone.sharedMaterial = GemMaterial();
        stone.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        glints[0] = Glint("Glint upper left", OnScreen(-.13f, .2f, .15f), out glintRenderers[0]);
        glints[1] = Glint("Glint lower right", OnScreen(.15f, .2f, -.13f), out glintRenderers[1]);
    }

    private Transform Flat(string name, Material material, Vector3 position, float size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(90f, 0f, 0f);
        t.localScale = Vector3.one * size;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
        var r = t.gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return t;
    }

    private Transform Glint(string name, Vector3 position, out Renderer renderer)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.localPosition = position;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
        renderer = t.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GlintMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return t;
    }

    private void Update()
    {
        if (collected) return;
        float t = Time.time * 2.2f + phase;
        float bob = Mathf.Sin(t);
        visual.localPosition = Vector3.up * (.04f * bob);
        visual.localRotation = Quaternion.Euler(6f * Mathf.Sin(t * .6f), 0f, 6f * Mathf.Sin(t * .45f + 1f));
        shadow.localScale = Vector3.one * (.78f - .04f * bob);
        halo.localScale = Vector3.one * (1.25f + .06f * Mathf.Sin(t * 1.3f));
        Twinkle(0, Time.time * 1.1f + phase);
        Twinkle(1, Time.time * 1.1f + phase + .5f);

        if (Time.timeScale <= 0 || LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive) return;
        if (player == null)
        {
            var actor = GameObject.Find("Player");
            if (actor != null) player = actor.GetComponent<GhostScript>();
        }
        if (player == null || player.IsDead) return;
        var p = player.transform.position;
        if (Mathf.Abs(p.x - transform.position.x) < .5f && Mathf.Abs(p.z - transform.position.z) < .5f)
            StartCoroutine(Collect());
    }

    // Each glint flashes once per cycle and rests in between, the two half a
    // cycle apart so there is almost always one catching the light.
    private void Twinkle(int i, float cycle)
    {
        float p = Mathf.Repeat(cycle, 1f) / .55f;
        float s = p < 1f ? Mathf.Sin(p * Mathf.PI) : 0f;
        s *= s;
        glints[i].localScale = Vector3.one * (.16f + .26f * s);
        block.SetColor(TintId, new Color(2.6f, 3.6f, 4f, 1f));
        block.SetFloat(StrengthId, s);
        glintRenderers[i].SetPropertyBlock(block);
    }

    private IEnumerator Collect()
    {
        collected = true;
        Uncollected.Remove(this);
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddMoonshard();
        AudioManager.Play(GameSound.Reward);
        foreach (var g in glints) g.gameObject.SetActive(false);
        float elapsed = 0f;
        while (elapsed < .55f)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / .55f);
            float k = (1f + .4f * Mathf.Sin(p * Mathf.PI)) * (1f - p);
            visual.localScale = Vector3.one * k;
            visual.localPosition = Vector3.up * (.35f * p);
            halo.localScale = Vector3.one * (1.25f + .8f * p);
            shadow.localScale = Vector3.one * (.78f * (1f - p));
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Mesh GemMesh()
    {
        if (gemMesh != null) return gemMesh;
        var points = new List<Vector3>();
        var colours = new List<Color>();
        var bary = new List<Vector3>();
        var triangles = new List<int>();
        void Face(Vector3 a, Vector3 b, Vector3 c, Color colour, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            { var swap = b; b = c; c = swap; }
            // Vertex colours reach the shader untouched; in a linear project the
            // output is then brightened to sRGB, which washed the blues out.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) colour = colour.linear;
            int start = points.Count;
            points.Add(a); points.Add(b); points.Add(c);
            colours.Add(colour); colours.Add(colour); colours.Add(colour);
            // Barycentric corners let the shader draw a hairline on every facet edge.
            bary.Add(Vector3.right); bary.Add(Vector3.up); bary.Add(Vector3.forward);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        var pale = new Color(.6f, .9f, 1f);
        var light = new Color(.32f, .8f, 1f);
        var mid = new Color(.13f, .6f, 1f);
        var blue = new Color(.06f, .4f, .95f);
        var deep = new Color(.04f, .22f, .78f);

        // Girdle corners clockwise from the top of the screen, the table's above them.
        Vector3[] outer = { OnScreen(0, 0, Outer), OnScreen(Outer, 0, 0), OnScreen(0, 0, -Outer), OnScreen(-Outer, 0, 0) };
        Vector3[] table = { OnScreen(0, Crown, Table), OnScreen(Table, Crown, 0), OnScreen(0, Crown, -Table), OnScreen(-Table, Crown, 0) };
        var top = new Vector3(0f, Crown, 0f);
        var culet = new Vector3(0f, Culet, 0f);

        // Light from the upper left: bright facets there, deep blue lower right.
        Color[,] crown =
        {
            { light, mid, blue },   // upper right
            { deep, blue, mid },    // lower right
            { mid, blue, deep },    // lower left
            { pale, light, pale },  // upper left
        };
        Color[] tableColours = { pale, light, mid, pale };

        for (int i = 0; i < 4; i++)
        {
            int n = (i + 1) % 4;
            var edgeMid = (outer[i] + outer[n]) * .5f;
            var outward = edgeMid.normalized;
            Face(top, table[i], table[n], tableColours[i], Vector3.up);
            Face(outer[i], table[i], edgeMid, crown[i, 0], outward + Vector3.up);
            Face(table[i], table[n], edgeMid, crown[i, 1], outward + Vector3.up);
            Face(edgeMid, table[n], outer[n], crown[i, 2], outward + Vector3.up);
            Face(outer[i], edgeMid, culet, i % 2 == 0 ? blue : deep, outward - Vector3.up);
            Face(edgeMid, outer[n], culet, i % 2 == 0 ? deep : blue, outward - Vector3.up);
        }

        gemMesh = new Mesh { name = "Moonshard square cut diamond" };
        gemMesh.SetVertices(points);
        gemMesh.SetColors(colours);
        gemMesh.SetUVs(0, bary);
        gemMesh.SetTriangles(triangles, 0);
        gemMesh.RecalculateNormals();
        gemMesh.RecalculateBounds();
        return gemMesh;
    }

    private static Mesh QuadMesh()
    {
        if (quadMesh != null) return quadMesh;
        quadMesh = new Mesh { name = "Moonshard quad" };
        quadMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        return quadMesh;
    }

    private static Material GemMaterial()
    {
        if (gemMaterial != null) return gemMaterial;
        var shader = Resources.Load<Shader>("MoonshardGem");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        gemMaterial = new Material(shader) { name = "Luminous cut gemstone" };
        return gemMaterial;
    }

    private static Material HaloMaterial()
    {
        if (haloMaterial != null) return haloMaterial;
        haloMaterial = new Material(Resources.Load<Shader>("MoonshardHalo")) { name = "Moonshard halo" };
        haloMaterial.SetColor("_Color", new Color(.35f, .9f, 1f, .55f));
        haloMaterial.SetFloat("_Power", 2.2f);
        haloMaterial.renderQueue = 3001;
        return haloMaterial;
    }

    private static Material ShadowMaterial()
    {
        if (shadowMaterial != null) return shadowMaterial;
        shadowMaterial = new Material(Resources.Load<Shader>("MoonshardHalo")) { name = "Moonshard shadow" };
        shadowMaterial.SetColor("_Color", new Color(0f, .1f, 0f, .5f));
        shadowMaterial.SetFloat("_Power", 1.4f);
        shadowMaterial.renderQueue = 3000;
        return shadowMaterial;
    }

    private static Material GlintMaterial()
    {
        if (glintMaterial != null) return glintMaterial;
        glintMaterial = new Material(Resources.Load<Shader>("CoinEffects/CoinGlint")) { name = "Moonshard glint" };
        glintMaterial.renderQueue = 3002;
        return glintMaterial;
    }
}
