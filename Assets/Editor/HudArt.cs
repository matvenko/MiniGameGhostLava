using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Turns a drawn HUD asset into a sprite the game can put a live value on.
//
// The art for these readouts arrives complete, with a specimen value already
// painted into it - "128" coins, "LEVEL 1". The value has to come from the game,
// so the painted one is lifted back out here and the hole it leaves is filled
// with the artwork's own interior, taken from either side of it.
//
// Row by row rather than as one flat colour: every one of these panels is shaded
// top to bottom, and a flat patch shows up immediately as a bruise across the
// middle of it. Averaging a band of columns rather than reading a single one
// keeps the grain in the painting from streaking sideways across the field.
//
// Whatever the field does not cover is untouched - bevels, gloss, the highlight
// on a cap - so the field wants to be as wide as the painted value and its
// shadow, and no wider.
internal static class HudArt
{
    // field is in source pixels with y counted from the bottom, the way
    // Texture2D reads them, and is half open the way every RectInt is: xMax and
    // yMax are the first row and column left alone. sampleWidth is how many clean
    // columns either side of it are averaged to fill it in - clean being the
    // word to check, since the soft shadow under painted lettering carries a good
    // 15px past anything that looks dark, and a band that reaches into it fills
    // the whole field with the shadow.
    public static Sprite BuildSprite(string sourcePath, string outPath, RectInt field, int sampleWidth, int downscale)
    {
        Color[] pixels = Load(sourcePath, out int w, out int h);
        if (pixels == null) return null;

        if (field.xMin - sampleWidth < 0 || field.xMax + sampleWidth > w ||
            field.yMin < 0 || field.yMax > h)
        {
            Debug.LogError("[HudArt] " + sourcePath + " is " + w + "x" + h + ", which the field " + field +
                           " and its " + sampleWidth + "px sample bands do not fit inside. " +
                           "Re-measure the field against the new art.");
            return null;
        }

        ErasePaintedValue(pixels, w, field, sampleWidth);
        return Write(outPath, pixels, w, h, downscale);
    }

    public static Color[] Load(string path, out int w, out int h)
    {
        w = h = 0;
        if (!File.Exists(path))
        {
            Debug.LogError("[HudArt] No art at " + path + ".");
            return null;
        }

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(path));
        w = src.width;
        h = src.height;
        Color[] pixels = src.GetPixels();
        Object.DestroyImmediate(src);
        return pixels;
    }

    // Cuts a drawn icon off the flat paper it was exported on.
    //
    // Some of this art arrives with no alpha channel at all - the checkerboard
    // behind an icon is painted into the file, not transparency, and one of them
    // came on black instead of white. So the paper is whatever colour the corners
    // are, and it is found by flooding in from those corners: light parts inside
    // the icon are fenced in by its rim and never reached, which a threshold over
    // the whole image could not manage.
    //
    // The pixels along the flood's edge are part icon and part paper, and are
    // given the coverage they actually have rather than being rounded up to
    // opaque - rounding them up is what leaves a pale fringe round a dark icon.
    // How far a pixel has been dragged from the paper is measured against how far
    // its own neighbours went, so this works whether the paper is lighter than
    // the icon or darker.
    public static Color[] CutFromPaper(Color[] px, int w, int h, float tolerance = 0.10f)
    {
        Color paperColour = (px[0] + px[w - 1] + px[(h - 1) * w] + px[h * w - 1]) * 0.25f;
        bool[] paper = FloodPaper(px, w, h, paperColour, tolerance);
        var cut = new Color[px.Length];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = y * w + x;
            if (paper[i]) { cut[i] = new Color(0f, 0f, 0f, 0f); continue; }
            if (!TouchesPaper(paper, w, h, x, y)) { cut[i] = Opaque(px[i]); continue; }

            // An edge pixel: how far it is from the paper, against how far the
            // icon proper is right beside it.
            Color c = px[i];
            if (!IconAround(px, paper, w, h, x, y, out Color inside)) { cut[i] = Opaque(c); continue; }

            float reach = Distance(inside, paperColour);
            float a = reach < 0.02f ? 1f : Mathf.Clamp01(Distance(c, paperColour) / reach);
            cut[i] = a <= 0.004f
                ? new Color(0f, 0f, 0f, 0f)
                : new Color(Mathf.Clamp01((c.r - (1f - a) * paperColour.r) / a),
                            Mathf.Clamp01((c.g - (1f - a) * paperColour.g) / a),
                            Mathf.Clamp01((c.b - (1f - a) * paperColour.b) / a), a);
        }

        return cut;
    }

    private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

    private static float Distance(Color a, Color b) =>
        Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

    private static bool TouchesPaper(bool[] paper, int w, int h, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int nx = x + dx, ny = y + dy;
            if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (paper[ny * w + nx]) return true;
        }
        return false;
    }

    // The colour of the icon just inside this edge pixel: the neighbours that are
    // icon and are not themselves on the edge, which is what a fully covered
    // pixel here would look like.
    private static bool IconAround(Color[] px, bool[] paper, int w, int h, int x, int y, out Color average)
    {
        var sum = new Color(0f, 0f, 0f, 0f);
        int found = 0;

        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (paper[ny * w + nx] || TouchesPaper(paper, w, h, nx, ny)) continue;
            sum += px[ny * w + nx];
            found++;
        }

        average = found > 0 ? sum / found : Color.clear;
        return found > 0;
    }

    private static bool[] FloodPaper(Color[] px, int w, int h, Color paperColour, float tolerance)
    {
        var paper = new bool[px.Length];
        var queue = new Queue<int>();

        void Push(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (paper[i] || Distance(px[i], paperColour) > tolerance) return;
            paper[i] = true;
            queue.Enqueue(i);
        }

        Push(0, 0);
        Push(w - 1, 0);
        Push(0, h - 1);
        Push(w - 1, h - 1);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            Push(x - 1, y);
            Push(x + 1, y);
            Push(x, y - 1);
            Push(x, y + 1);
        }

        return paper;
    }

    // The box round everything that is not see-through, which for a cut-out icon
    // is the icon.
    public static RectInt OpaqueBounds(Color[] px, int w, int h, float threshold = 0.02f)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (px[y * w + x].a <= threshold) continue;
            if (x < x0) x0 = x;
            if (x > x1) x1 = x;
            if (y < y0) y0 = y;
            if (y > y1) y1 = y;
        }
        return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    public static Color[] Crop(Color[] px, int w, RectInt box)
    {
        var outPixels = new Color[box.width * box.height];
        for (int y = 0; y < box.height; y++)
        for (int x = 0; x < box.width; x++)
            outPixels[y * box.width + x] = px[(box.yMin + y) * w + box.xMin + x];
        return outPixels;
    }

    // border is in source pixels, and is scaled down with everything else. A
    // panel that has to stretch - the lives pill grows and shrinks with the
    // count - needs it; one drawn at a fixed size does not.
    public static Sprite Write(string path, Color[] pixels, int w, int h, int downscale, Vector4 border = default)
    {
        return WriteSprite(path, Downsample(pixels, w, h, downscale), w / downscale, h / downscale,
                           border / downscale);
    }

    private static void ErasePaintedValue(Color[] pixels, int w, RectInt field, int sampleWidth)
    {
        // An average belongs at the middle of the band it was taken from, not at
        // the edge of the field, and the interpolation is run between those two
        // middles. Anchoring it to the field's edges instead leaves a visible
        // vertical seam wherever the interior is shading sideways - which it is
        // on both of these panels, towards the highlight on the right-hand cap.
        float leftAt = field.xMin - (sampleWidth + 1) * 0.5f;
        float rightAt = field.xMax + (sampleWidth - 1) * 0.5f;

        for (int y = field.yMin; y < field.yMax; y++)
        {
            Color left = AverageRun(pixels, w, y, field.xMin - sampleWidth, sampleWidth);
            Color right = AverageRun(pixels, w, y, field.xMax, sampleWidth);

            for (int x = field.xMin; x < field.xMax; x++)
                pixels[y * w + x] = Color.Lerp(left, right, (x - leftAt) / (rightAt - leftAt));
        }
    }

    private static Color AverageRun(Color[] pixels, int w, int y, int x0, int count)
    {
        var sum = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < count; i++) sum += pixels[y * w + x0 + i];
        return sum / count;
    }

    // Box filter, premultiplied, so the soft glow around the art does not pull
    // black in from the transparent pixels beside it as it shrinks.
    private static Color[] Downsample(Color[] pixels, int w, int h, int factor)
    {
        int ow = w / factor, oh = h / factor;
        var outPixels = new Color[ow * oh];

        for (int y = 0; y < oh; y++)
        for (int x = 0; x < ow; x++)
        {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            for (int sy = 0; sy < factor; sy++)
            for (int sx = 0; sx < factor; sx++)
            {
                Color c = pixels[(y * factor + sy) * w + x * factor + sx];
                r += c.r * c.a;
                g += c.g * c.a;
                b += c.b * c.a;
                a += c.a;
            }

            outPixels[y * ow + x] = a > 0f
                ? new Color(r / a, g / a, b / a, a / (factor * factor))
                : new Color(0f, 0f, 0f, 0f);
        }

        return outPixels;
    }

    private static Sprite WriteSprite(string path, Color[] pixels, int w, int h, Vector4 border)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        // These are drawn at a fraction of the size they were painted at, so they
        // are minified every frame; without mips that shows up as a crawling,
        // over-sharpened bevel.
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- the live value on top ---------------------------------------------

    // Dresses a TMP label to stand in for the digits that were painted out.
    //
    // Liberation Sans is a lighter face than the ones these panels were lettered
    // with, so it is fattened as well as rimmed. The rim and the shadow are drawn
    // outside the glyph and TMP only gives a quad room for them when it is told
    // the material changed - without that call the outline is cropped at the
    // glyph edge and the digits come out grey and soft instead of cut.
    public static void StyleValue(TMPro.TextMeshProUGUI tmp, Color outline)
    {
        Material mat = tmp.fontMaterial;
        mat.SetFloat(TMPro.ShaderUtilities.ID_FaceDilate, 0.12f);
        mat.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, 0.2f);
        mat.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, outline);
        if (mat.HasProperty(TMPro.ShaderUtilities.ID_UnderlayColor))
        {
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.5f));
            mat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            mat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            mat.SetFloat(TMPro.ShaderUtilities.ID_UnderlayDilate, 0f);
            mat.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        }
        tmp.fontMaterial = mat;

        tmp.UpdateMeshPadding();
        tmp.ForceMeshUpdate();
    }

    // What a digit's cap height comes out as, per point of font size, once
    // StyleValue has fattened it. Measured off a render rather than taken from
    // the font's metrics, because the dilate is part of what you see: a 38pt
    // count measured 34.5 units tall. Sizing off this is what lets a live number
    // match the one that was painted.
    public const float CapHeightPerPoint = 0.908f;
}
