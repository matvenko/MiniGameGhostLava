using UnityEngine;

// Sprites for the menu UI, drawn into textures at runtime instead of being
// imported as art. The menu only needs a handful of primitives - rounded
// rectangles, capsules, rings, soft glows - and generating them keeps the
// look defined by numbers in code (radius, colour ramp) rather than by png
// files nobody can edit without leaving Unity. It also means the palette can
// change in one place and every widget follows.
//
// Everything is drawn with a signed-distance test and a one-pixel feather,
// so the edges are smooth at any size the canvas scaler picks.
public static class UIShapes
{
    private const float PixelsPerUnit = 100f;

    // A rounded rectangle meant to be used as a 9-sliced sprite: the corners
    // keep their radius while the middle stretches to whatever size the
    // RectTransform is. Square texture, so one sprite serves every card.
    public static Sprite RoundedRect(int size, int radius)
    {
        var tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        float b = radius;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    // A pill: fully rounded left and right caps, flat middle. Sliced
    // horizontally only, so a progress bar can grow without the caps
    // smearing. Optionally tinted along its length by a gradient, which is
    // how the fill gets a colour ramp out of a single image.
    public static Sprite Capsule(int width, int height, Gradient horizontal = null)
    {
        var tex = NewTexture(width, height);
        var pixels = new Color[width * height];
        float radius = height * 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, width, height, radius);
                Color c = horizontal != null ? horizontal.Evaluate((x + 0.5f) / width) : Color.white;
                c.a *= Mathf.Clamp01(0.5f - d);
                pixels[y * width + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
            SpriteMeshType.FullRect, new Vector4(radius, 0f, radius, 0f));
    }

    // Soft round glow, opaque at the centre and fading to nothing at the
    // edge. Used at large sizes for the drifting background lights and at
    // tiny sizes for the floating embers - same sprite, different scale.
    public static Sprite RadialGlow(int size, float falloff = 2f)
    {
        var tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // The backdrop ramp. Only four pixels wide because it is stretched
    // across the whole screen and nothing varies horizontally.
    public static Sprite VerticalGradient(Gradient gradient, int height = 256)
    {
        var tex = NewTexture(4, height);
        var pixels = new Color[4 * height];
        for (int y = 0; y < height; y++)
        {
            Color c = gradient.Evaluate(y / (height - 1f));
            for (int x = 0; x < 4; x++) pixels[y * 4 + x] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // A ring, optionally coloured by angle rather than by position: the
    // gradient is walked once clockwise from the top, which is exactly the
    // order a radial-filled Image reveals it in. That's what gives the
    // progress ring a colour sweep from a plain fillAmount.
    public static Sprite Ring(int size, float inner01, float outer01, Gradient angular = null)
    {
        var tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float c = size * 0.5f;
        float innerPix = inner01 * c;
        float outerPix = outer01 * c;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(outerPix - d + 0.5f) * Mathf.Clamp01(d - innerPix + 0.5f);
                Color col = angular != null ? angular.Evaluate(ClockwiseFromTop(dx, dy)) : Color.white;
                col.a *= a;
                pixels[y * size + x] = col;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // A comet-tail arc: solid at the head, fading out over `sweep01` of the
    // circle. Spun by a transform, this is the part of the loader that keeps
    // moving even while the progress number sits still.
    public static Sprite Arc(int size, float inner01, float outer01, float sweep01)
    {
        var tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float c = size * 0.5f;
        float innerPix = inner01 * c;
        float outerPix = outer01 * c;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float band = Mathf.Clamp01(outerPix - d + 0.5f) * Mathf.Clamp01(d - innerPix + 0.5f);
                float t = ClockwiseFromTop(dx, dy);
                float tail = t < sweep01 ? 1f - t / sweep01 : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, band * tail * tail);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    // Transparent in the middle, opaque at the corners - laid over the
    // backdrop it pulls the eye to the centre without darkening the art.
    public static Sprite Vignette(int size, float start01 = 0.55f, float end01 = 1.1f)
    {
        var tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start01, end01, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    public static Gradient MakeGradient(params (float time, Color color)[] stops)
    {
        var g = new Gradient();
        var colorKeys = new GradientColorKey[stops.Length];
        var alphaKeys = new GradientAlphaKey[stops.Length];
        for (int i = 0; i < stops.Length; i++)
        {
            colorKeys[i] = new GradientColorKey(stops[i].color, stops[i].time);
            alphaKeys[i] = new GradientAlphaKey(stops[i].color.a, stops[i].time);
        }
        g.SetKeys(colorKeys, alphaKeys);
        return g;
    }

    // 0 at the top of the circle, growing clockwise, wrapping at 1.
    private static float ClockwiseFromTop(float dx, float dy)
    {
        float ang = Mathf.Atan2(dx, dy);
        if (ang < 0f) ang += Mathf.PI * 2f;
        return ang / (Mathf.PI * 2f);
    }

    private static float RoundedRectDistance(float px, float py, float w, float h, float r)
    {
        float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
    }
}
