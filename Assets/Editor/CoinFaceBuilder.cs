using System.IO;
using UnityEditor;
using UnityEngine;

// Draws the coin's face at the contrast it needs to be found on a green board.
//
// The hand-drawn T_CoinFace is gold on gold: a mid-gold ghost sitting on a
// mid-gold disc, with nothing at the edge to separate the coin from what is
// behind it. That reads at texture resolution and not at coin resolution - on
// screen the coin is about forty pixels across, and at forty pixels a gold
// shape on a gold field is one flat blob.
//
// So this states the face as four bands of falling darkness from the middle
// out - pale emblem, bright body, deeper rim, dark outline - which is what
// makes a small round thing read as a coin rather than a dot. The original is
// left where it is; only the material's reference moves.
//
// The layout has to obey CoinSurface: the texture's inscribed circle IS the
// face, and the single texel at rimUV is the whole cylindrical edge, so the
// corners carry the edge colour.
public static class CoinFaceBuilder
{
    private const string FacePath = "Assets/Materials/T_CoinFace_Bold.png";
    private const string MaterialPath = "Assets/CoinMaterial.mat";

    private const string AssetSourcePath = "Assets/Materials/coin-3D.png";
    private const string AssetFacePath = "Assets/Materials/T_CoinFace_3D.png";

    // Sampled off the reference: a saturated yellow body rather than the tan
    // the old face used, because tan is the colour the grass goes when the
    // day-night cycle swings the sun warm.
    private static readonly Color Outline = new Color(0.36f, 0.20f, 0.05f);
    private static readonly Color Rim = new Color(0.87f, 0.58f, 0.09f);
    private static readonly Color Body = new Color(1.00f, 0.78f, 0.10f);
    private static readonly Color BodyLit = new Color(1.00f, 0.88f, 0.30f);
    private static readonly Color Emblem = new Color(1.00f, 0.97f, 0.86f);

    // Radii as fractions of the face, outermost first.
    private const float OutlineFrom = 0.925f;
    private const float RimFrom = 0.80f;

    [MenuItem("Tools/Build Coin Face")]
    public static void Build()
    {
        Assign(WriteTexture(FacePath, Render(512)), FacePath);
    }

    // Fits a drawn coin asset to what CoinSurface expects. A painted coin comes
    // as a disc floating in transparency, and the mapping wants two things it
    // has not got: the coin filling the texture's inscribed circle exactly, and
    // an opaque texel in the corner, because that one texel colours the whole
    // cylindrical edge. Dropped in raw, the coin would sit small and rattling
    // inside its own face with a black band around the side.
    [MenuItem("Tools/Build Coin Face From Asset")]
    public static void BuildFromAsset()
    {
        if (!File.Exists(AssetSourcePath))
        {
            Debug.LogError("[Coin] No asset at " + AssetSourcePath);
            return;
        }

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(AssetSourcePath));
        Color[] pixels = src.GetPixels();
        int w = src.width, h = src.height;

        // The coin is found by colour, not by transparency. This asset arrived
        // with no alpha channel at all - the checkerboard it is previewed on is
        // the viewer's, and in the file the coin sits on flat near-white. Gold is
        // strongly saturated and both the background and the soft grey shadow
        // around the coin are not, so saturation separates them cleanly where an
        // alpha test finds nothing at all.
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!IsCoin(pixels[y * w + x])) continue;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (maxX < 0)
        {
            Debug.LogError("[Coin] Found no coloured shape in " + AssetSourcePath + ".");
            Object.DestroyImmediate(src);
            return;
        }

        float cx = (minX + maxX + 1) * 0.5f;
        float cy = (minY + maxY + 1) * 0.5f;
        // Half the larger side of the box round the coin. Taken off the box
        // rather than off the furthest coloured pixel, so one stray speck in the
        // corner of the export cannot decide the scale.
        float radius = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 0.5f;
        float side = radius * 2f;

        Color edge = SampleEdge(pixels, w, h, cx, cy, radius);

        const int size = 512;
        var outPixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f - half) / half;
            float v = (y + 0.5f - half) / half;

            // Everything outside the inscribed circle is the coin's edge colour.
            // Nothing needs masking against the white background: the coin has
            // just been scaled to fill that circle exactly, so inside it is coin
            // and outside it is only ever reached by the rim texel.
            if (u * u + v * v > 1f)
            {
                outPixels[y * size + x] = edge;
                continue;
            }

            Color c = Bilinear(pixels, w, h, cx + u * radius, cy + v * radius);
            outPixels[y * size + x] = new Color(c.r, c.g, c.b, 1f);
        }
        Object.DestroyImmediate(src);

        Assign(WriteTexture(AssetFacePath, outPixels), AssetFacePath);
        Debug.Log("[Coin] Edge colour taken from the asset: " + edge);
    }

    private static void Assign(Texture2D tex, string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Debug.LogError("[Coin] No material at " + MaterialPath);
            return;
        }

        mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("[Coin] Face is now " + path + ", assigned to " + MaterialPath +
                  ". The original T_CoinFace is untouched - swap the material's Base Map back to undo this.");
    }

    // The colour of the coin's outermost band, averaged all the way round so the
    // painted highlight on one side does not decide what the whole edge looks
    // like. Sampled right at the boundary rather than across the bevel: a coin
    // seen edge on should show the dark line the artist drew around it, not the
    // white glint from the top of the face.
    private static Color SampleEdge(Color[] pixels, int w, int h, float cx, float cy, float radius)
    {
        var sum = new Color(0f, 0f, 0f, 0f);
        int count = 0;
        for (int i = 0; i < 720; i++)
        {
            float a = i * Mathf.PI / 360f;
            for (float r = 0.955f; r <= 0.99f; r += 0.01f)
            {
                Color c = Bilinear(pixels, w, h, cx + Mathf.Cos(a) * radius * r, cy + Mathf.Sin(a) * radius * r);
                if (!IsCoin(c)) continue;
                sum += new Color(c.r, c.g, c.b, 1f);
                count++;
            }
        }
        return count == 0 ? new Color(0.87f, 0.58f, 0.09f, 1f) : sum / count;
    }

    // Coloured enough to be part of the coin. The page behind it and the shadow
    // it casts are both greys, and a grey has no spread between its channels.
    private static bool IsCoin(Color c) =>
        Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > 0.15f;

    private static Color Bilinear(Color[] pixels, int w, int h, float x, float y)
    {
        x = Mathf.Clamp(x - 0.5f, 0f, w - 1.001f);
        y = Mathf.Clamp(y - 0.5f, 0f, h - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        float fx = x - x0, fy = y - y0;
        int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);

        Color a = Color.Lerp(pixels[y0 * w + x0], pixels[y0 * w + x1], fx);
        Color b = Color.Lerp(pixels[y1 * w + x0], pixels[y1 * w + x1], fx);
        return Color.Lerp(a, b, fy);
    }

    private static Color[] Render(int size)
    {
        const int samples = 3;
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Color sum = new Color(0f, 0f, 0f, 0f);
            for (int sy = 0; sy < samples; sy++)
            for (int sx = 0; sx < samples; sx++)
            {
                float px = (x + (sx + 0.5f) / samples - half) / half;
                float py = (y + (sy + 0.5f) / samples - half) / half;
                sum += Sample(new Vector2(px, py));
            }
            pixels[y * size + x] = sum / (samples * samples);
        }

        return pixels;
    }

    private static Color Sample(Vector2 p)
    {
        float d = p.magnitude;

        // Everything outside the inscribed circle is the coin's edge band, since
        // that is all rimUV can reach.
        if (d > 1f) return Rim;
        if (d >= OutlineFrom) return Outline;
        if (d >= RimFrom) return Rim;

        if (InGhost(p)) return Emblem;

        // A gentle lift towards the middle. Flat, the face reads as a sticker;
        // this is enough to suggest a struck disc without pretending to be lit.
        return Color.Lerp(BodyLit, Body, Mathf.Clamp01(d / RimFrom));
    }

    // The ghost: a domed head on a straight body, three feet along the bottom,
    // and two eyes punched back out to the body colour.
    private static bool InGhost(Vector2 p)
    {
        const float halfWidth = 0.34f;
        const float shoulder = 0.10f;   // where the dome meets the straight sides
        const float hem = -0.30f;       // where the body ends and the feet begin
        const float footRadius = 0.113f;

        if (Mathf.Abs(p.x) > halfWidth) return false;

        bool inside;
        if (p.y > shoulder)
            inside = (p - new Vector2(0f, shoulder)).magnitude <= halfWidth;
        else if (p.y >= hem)
            inside = true;
        else
            inside = Mathf.Min(
                (p - new Vector2(-2f * footRadius, hem)).magnitude,
                Mathf.Min((p - new Vector2(0f, hem)).magnitude,
                          (p - new Vector2(2f * footRadius, hem)).magnitude)) <= footRadius;

        if (!inside) return false;

        // Eyes are holes in the emblem, so they show whatever the face is doing
        // underneath - which keeps them gold however the body gradient falls.
        const float eyeRadius = 0.085f;
        if ((p - new Vector2(-0.145f, 0.16f)).magnitude <= eyeRadius) return false;
        if ((p - new Vector2(0.145f, 0.16f)).magnitude <= eyeRadius) return false;

        return true;
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
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
