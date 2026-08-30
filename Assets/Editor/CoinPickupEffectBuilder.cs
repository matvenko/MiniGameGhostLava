using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the coin pickup burst outright - textures, materials and prefab - so
// the effect is ours rather than a downloaded package. The same reasoning as
// CoinSurface: what a coin pickup needs is a soft dot, a thin ring and some
// gold streaks, all of which are cheaper to generate than to import, and
// generating them keeps the effect sized to this board (a tile is 1 unit, the
// coin disc 0.38 across) instead of to whatever scale a store asset assumed.
//
// A menu command rather than hand-authored .prefab YAML because particle
// systems carry dozens of serialised curve and gradient structs that are only
// safely written through the API. Running it twice is safe - it replaces what
// it made last time, including the reference on Coin.prefab.
public static class CoinPickupEffectBuilder
{
    private const string TextureFolder = "Assets/Textures";
    private const string MaterialFolder = "Assets/Materials";
    private const string PrefabPath = "Assets/Prefabs/CoinPickupEffect.prefab";
    private const string CoinPrefabPath = "Assets/Coin.prefab";

    private const string SparkTexPath = TextureFolder + "/fx_spark.png";
    private const string RingTexPath = TextureFolder + "/fx_ring.png";
    private const string SparkMatPath = MaterialFolder + "/FX_SparkAdditive.mat";
    private const string RingMatPath = MaterialFolder + "/FX_RingAdditive.mat";

    // Struck-gold, and a paler core so the flash reads as light rather than as
    // one more yellow object on a board that already has a gold coin on it.
    private static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
    private static readonly Color PaleGold = new Color(1f, 0.95f, 0.72f);

    // Both colours are kept inside 0..1 on purpose. Particle colours are packed
    // to 8 bits per channel before they reach the shader, so pushing a gold past
    // 1 to make it "hot" does not brighten it - it clips red and green first and
    // turns struck gold into white and lime.

    [MenuItem("Tools/Build Coin Pickup Effect")]
    public static void Build()
    {
        Directory.CreateDirectory(TextureFolder);
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory("Assets/Prefabs");

        Texture2D spark = WriteTexture(SparkTexPath, BuildDotTexture(64));
        Texture2D ring = WriteTexture(RingTexPath, BuildRingTexture(128));

        Material sparkMat = WriteAdditiveMaterial(SparkMatPath, spark);
        Material ringMat = WriteAdditiveMaterial(RingMatPath, ring);

        var root = new GameObject("CoinPickupEffect");
        BuildFlash(root.transform, sparkMat);
        BuildRing(root.transform, ringMat);
        BuildSparks(root.transform, sparkMat);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssignToCoin(prefab);

        AssetDatabase.SaveAssets();
        Debug.Log("Coin pickup effect built at " + PrefabPath + " and assigned to " + CoinPrefabPath);
    }

    // ---- pieces -----------------------------------------------------------

    // A single soft flare where the coin was, gone in a quarter second. It
    // grows as it fades, which is what separates a flash from a lamp being
    // switched off.
    private static void BuildFlash(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem("Flash", parent, mat, ParticleSystemRenderMode.Billboard);

        var main = ps.main;
        main.duration = 0.3f;
        main.startLifetime = 0.12f;
        main.startSpeed = 0f;
        main.startSize = 0.3f;
        // Left at pale gold rather than overdriven. A white flash clips to white
        // at full alpha, but half way through its fade it is adding half of white
        // to a dark board, and half of white is grey - which parks a grey ball
        // over the sparks for the rest of the burst. Gold at half strength is
        // still gold.
        main.startColor = PaleGold;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Curve(0.35f, 1.1f));

        // Fading from the first frame, with none of the hold the other pieces
        // get. Held even briefly, a flash spread over a growing quad stops being
        // a flash and becomes a grey ball parked over the sparks.
        Fade(ps, 1f, 0f, 0f);
    }

    // A thin expanding hoop. Sparks alone read as an explosion; the hoop is
    // what makes it read as something being taken from a spot.
    private static void BuildRing(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem("Ring", parent, mat, ParticleSystemRenderMode.Billboard);

        var main = ps.main;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 0.18f;
        main.startColor = Gold;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        // Fast at first and easing off, the way a shock front spreads.
        // A tile is 1 unit across, so the hoop stops at about one tile: any wider
        // and the burst starts reading as belonging to the squares next door.
        size.size = new ParticleSystem.MinMaxCurve(1f, EaseOutCurve(1f, 5.5f));

        Fade(ps, 0.9f, 0f, 0.15f);
    }

    // The gold itself, thrown outward and upward and pulled back down. Drawn
    // stretched along velocity so each grain reads as a streak rather than a
    // dot, which is most of what sells the speed at this size.
    private static void BuildSparks(Transform parent, Material mat)
    {
        ParticleSystem ps = NewSystem("Sparks", parent, mat, ParticleSystemRenderMode.Stretch);
        // Unity emits a hemisphere into its own +Z, so an unrotated one throws
        // the sparks sideways across the board. Tipped back a quarter turn, the
        // dome opens upward, which is the only direction with room on a board
        // seen from above.
        ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        var main = ps.main;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.35f);
        // Quick off the mark and heavy. Thrown slowly, the whole burst spends its
        // first tenth of a second as one overlapping clump of soft quads, which
        // adds up to a muddy haze where the coin used to be; leaving fast enough
        // to separate within a couple of frames is what keeps it reading as
        // individual grains of gold.
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(Gold, PaleGold);
        main.gravityModifier = 2.2f;
        // World space, so the streaks hang where they were thrown while the
        // coin's own root rises and shrinks away underneath them.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        // A hemisphere, not a sphere: the board is seen from above, and sparks
        // aimed down are swallowed by the ground on the frame they appear.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.12f;
        shape.radiusThickness = 0f;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, Curve(1f, 0.2f));

        Fade(ps, 1f, 0f, 0.55f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 2.6f;
    }

    // ---- helpers ----------------------------------------------------------

    private static ParticleSystem NewSystem(string name, Transform parent, Material mat,
        ParticleSystemRenderMode mode)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;
        // Local, so the prefab's own scale is the one knob that resizes the
        // whole burst.
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = mode;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = -10f;

        return ps;
    }

    private static void Fade(ParticleSystem ps, float from, float to, float holdUntil)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var colors = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        var alphas = holdUntil > 0f
            ? new[]
            {
                new GradientAlphaKey(from, 0f),
                new GradientAlphaKey(from, holdUntil),
                new GradientAlphaKey(to, 1f)
            }
            : new[] { new GradientAlphaKey(from, 0f), new GradientAlphaKey(to, 1f) };

        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        col.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static AnimationCurve Curve(float from, float to) =>
        AnimationCurve.EaseInOut(0f, from, 1f, to);

    private static AnimationCurve EaseOutCurve(float from, float to) =>
        new AnimationCurve(
            new Keyframe(0f, from, 0f, (to - from) * 2.6f),
            new Keyframe(1f, to, 0f, 0f));

    // A soft round dot with a small solid core. The cubed falloff and the flat
    // middle are both there for the same reason: an even gradient spread across
    // a quad adds a little light everywhere and reads as a grey smudge, while a
    // core that clips to white with the glow falling off fast around it reads as
    // a point of light.
    private static Color[] BuildDotTexture(int size)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        const float core = 0.14f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = d <= core ? 1f : Mathf.Clamp01((1f - d) / (1f - core));
            a = a * a * a;
            pixels[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        return pixels;
    }

    // A band at 0.42 of the radius, feathered on both sides so the hoop has no
    // hard edge to alias against once it is scaled up several times over.
    private static Color[] BuildRingTexture(int size)
    {
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        const float radius = 0.44f;
        const float thickness = 0.055f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - half) / half;
            float dy = (y + 0.5f - half) / half;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / thickness);
            pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
        }
        return pixels;
    }

    private static Texture2D WriteTexture(string path, Color[] pixels)
    {
        int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Additive, unlit, no depth write. Written through the blend factors
    // themselves as well as URP's _Surface/_Blend switches: the switches drive
    // the inspector, the factors drive what actually gets drawn.
    private static Material WriteAdditiveMaterial(string path, Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
        if (mat.HasProperty("_ColorMode")) mat.SetFloat("_ColorMode", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mat, existing);
            Object.DestroyImmediate(mat);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Coin.cs lives on the prefab's Cylinder child, not its root, so this goes
    // through the loaded contents rather than guessing at the hierarchy.
    private static void AssignToCoin(GameObject effect)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(CoinPrefabPath);
        try
        {
            var coin = contents.GetComponentInChildren<Coin>(true);
            if (coin == null)
            {
                Debug.LogWarning("No Coin component in " + CoinPrefabPath + "; assign the effect by hand.");
                return;
            }

            var so = new SerializedObject(coin);
            so.FindProperty("pickupEffect").objectReferenceValue = effect;
            so.FindProperty("pickupEffectLifetime").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, CoinPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
