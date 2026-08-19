using UnityEngine;

// The menu's colour language in one place. The loading screen and the
// difficulty cards share it, so the screen reads as one design instead of
// two widgets that happen to sit on the same canvas - and re-tinting the
// whole menu means editing this file and nothing else.
//
// Deep violet night at the top of the screen falling into lava heat at the
// bottom, with cyan / violet / magenta / amber as the accents that light it.
public static class MenuPalette
{
    public static readonly Color BackdropTop = Hex("0D0722");
    public static readonly Color BackdropMid = Hex("221055");
    public static readonly Color BackdropLow = Hex("5B1A57");
    public static readonly Color BackdropBottom = Hex("8E2A46");

    public static readonly Color Cyan = Hex("4CC9F0");
    public static readonly Color Violet = Hex("7B2FF7");
    public static readonly Color Magenta = Hex("F72585");
    public static readonly Color Amber = Hex("FF9E00");

    // Ice for the gentle mode, lava for the hard one - the same contrast the
    // board itself is built on, and readable apart without relying on the
    // red/green pair.
    public static readonly Color CardEasy = Hex("1EC8A5");
    public static readonly Color CardHard = Hex("FF4D6D");

    public static Gradient Backdrop()
    {
        return UIShapes.MakeGradient(
            (0f, BackdropBottom),
            (0.35f, BackdropLow),
            (0.72f, BackdropMid),
            (1f, BackdropTop));
    }

    // Walked once around the progress ring. The last stop repeats the first
    // so a full ring closes on itself with no seam at the top.
    public static Gradient RingSweep()
    {
        return UIShapes.MakeGradient(
            (0f, Cyan),
            (0.35f, Violet),
            (0.65f, Magenta),
            (0.85f, Amber),
            (1f, Cyan));
    }

    public static Gradient BarFill()
    {
        return UIShapes.MakeGradient(
            (0f, Magenta),
            (0.55f, Hex("FF6B4A")),
            (1f, Amber));
    }

    public static Color Hex(string rgb)
    {
        return new Color(
            int.Parse(rgb.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(rgb.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(rgb.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            1f);
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
