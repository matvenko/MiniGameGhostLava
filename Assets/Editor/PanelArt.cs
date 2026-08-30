using UnityEngine;

// The panels the settings popup is built on, drawn in code.
//
// The drawn sheet the popup comes from has every control on it - the switches,
// the checkboxes, the buttons, the title - but not one empty panel to put them
// on: the boxes in the artwork all arrived with their lettering already painted
// into them, and the lettering has to come from the game. So the boxes are drawn
// here instead, to the same recipe the sheet uses - a rounded navy field shading
// from top to bottom, a lit rim around it, a sheen along the top inside edge, and
// on the ones that are meant to stand out, a glow bleeding outwards.
//
// The colours are not invented. They are read off the sheet: the row fill is the
// navy behind "Hide Joystick", the rim is the brightest pixel along that row's
// top edge, and the cyan a selected card is ringed with is the cyan the artist
// ringed "Left Top" with.
//
// Every panel is drawn at the size it is used at, so the gradient and the glow
// are right and nothing is stretched. That is affordable because there are ten of
// them and they are flat.
internal static class PanelArt
{
    public struct Style
    {
        public float radius;         // corner radius, in pixels
        public float pad;            // clear margin kept inside the texture for the glow to bleed into
        public Color fillTop;
        public Color fillBottom;
        public Color rim;
        public float rimWidth;
        public Color glow;           // alpha carries how strong it is
        public float glowSize;       // pixels the glow reaches past the rim
        public Color sheen;          // highlight along the top inside edge
        public float sheenHeight;
    }

    // A rounded panel filling the texture, inset by the style's pad.
    public static Sprite Panel(string path, int w, int h, Style s)
    {
        var px = new Color[w * h];

        float halfW = (w - 2f * s.pad) * 0.5f;
        float halfH = (h - 2f * s.pad) * 0.5f;
        float r = Mathf.Min(s.radius, Mathf.Min(halfW, halfH));
        float cx = w * 0.5f, cy = h * 0.5f;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float d = RoundedBox(x + 0.5f - cx, y + 0.5f - cy, halfW, halfH, r);

            Color c = new Color(0f, 0f, 0f, 0f);

            if (s.glow.a > 0f && s.glowSize > 0f && d > -s.rimWidth)
            {
                float reach = Mathf.Max(d, 0f) / s.glowSize;
                float a = s.glow.a * Mathf.Exp(-reach * reach * 3f);
                c = Over(c, new Color(s.glow.r, s.glow.g, s.glow.b, a));
            }

            float inside = Mathf.Clamp01(0.5f - d);
            if (inside > 0f)
            {
                float t = Mathf.Clamp01((y - s.pad) / Mathf.Max(1f, h - 2f * s.pad));
                Color fill = Color.Lerp(s.fillBottom, s.fillTop, t);
                c = Over(c, new Color(fill.r, fill.g, fill.b, fill.a * inside));

                if (s.sheen.a > 0f && s.sheenHeight > 0f)
                {
                    float below = (h - s.pad) - y;
                    float a = s.sheen.a * (1f - Mathf.Clamp01(below / s.sheenHeight)) * inside;
                    if (a > 0f) c = Over(c, new Color(s.sheen.r, s.sheen.g, s.sheen.b, a));
                }
            }

            if (s.rimWidth > 0f)
            {
                float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + s.rimWidth + 0.5f);
                if (a > 0f) c = Over(c, new Color(s.rim.r, s.rim.g, s.rim.b, s.rim.a * a));
            }

            px[y * w + x] = c;
        }

        return HudArt.Write(path, px, w, h, 1);
    }

    // One of the little lit beads a card uses to show where the ability buttons
    // would sit. A filled bead with a lighter core and a halo, the way they are
    // drawn on the sheet.
    public static Sprite Bead(string path, int size, Color fill, Color core, Color glow)
    {
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        float rim = size * 0.30f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c0, dy = y + 0.5f - c0;
            float d = Mathf.Sqrt(dx * dx + dy * dy) - rim;

            Color c = new Color(0f, 0f, 0f, 0f);
            float halo = glow.a * Mathf.Exp(-Mathf.Pow(Mathf.Max(d, 0f) / (size * 0.20f), 2f) * 3f);
            c = Over(c, new Color(glow.r, glow.g, glow.b, halo));

            float inside = Mathf.Clamp01(0.5f - d);
            if (inside > 0f)
            {
                c = Over(c, new Color(fill.r, fill.g, fill.b, fill.a * inside));
                float coreA = Mathf.Clamp01(0.5f - (d + rim * 0.45f));
                if (coreA > 0f) c = Over(c, new Color(core.r, core.g, core.b, core.a * coreA));
            }

            px[y * size + x] = c;
        }

        return HudArt.Write(path, px, size, size, 1);
    }

    // The pale disc a card uses to stand for the joystick.
    public static Sprite Disc(string path, int size, Color outer, Color inner)
    {
        var px = new Color[size * size];
        float c0 = size * 0.5f;
        float rOuter = size * 0.5f - 1f;
        float rInner = size * 0.26f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c0, dy = y + 0.5f - c0;
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            Color c = new Color(0f, 0f, 0f, 0f);
            float a = Mathf.Clamp01(rOuter - d + 0.5f);
            if (a > 0f) c = Over(c, new Color(outer.r, outer.g, outer.b, outer.a * a));
            float b = Mathf.Clamp01(rInner - d + 0.5f);
            if (b > 0f) c = Over(c, new Color(inner.r, inner.g, inner.b, inner.a * b));

            px[y * size + x] = c;
        }

        return HudArt.Write(path, px, size, size, 1);
    }

    // The hairline that runs out from a section heading and fades away.
    public static Sprite Rule(string path, int w, int h, Color colour)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float t = (x + 0.5f) / w;              // 1 at the heading end, 0 at the far end
            float thickness = Mathf.Clamp01(Mathf.Min(y + 0.5f, h - y - 0.5f) / Mathf.Max(0.5f, h * 0.5f));
            px[y * w + x] = new Color(colour.r, colour.g, colour.b, colour.a * t * t * thickness);
        }
        return HudArt.Write(path, px, w, h, 1);
    }

    // ---- the maths ---------------------------------------------------------

    // Distance to the edge of a rounded box, negative inside it.
    private static float RoundedBox(float x, float y, float halfW, float halfH, float r)
    {
        float qx = Mathf.Abs(x) - (halfW - r);
        float qy = Mathf.Abs(y) - (halfH - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
        float r = (over.r * over.a + under.r * under.a * (1f - over.a)) / a;
        float g = (over.g * over.a + under.g * under.a * (1f - over.a)) / a;
        float b = (over.b * over.a + under.b * under.a * (1f - over.a)) / a;
        return new Color(r, g, b, a);
    }
}
