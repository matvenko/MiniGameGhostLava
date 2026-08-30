using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Cuts the drawn gear out of its glow and puts it in the top-right corner beside
// the lives pill, wired to do what Escape does.
//
// This art is cut differently from the others. It has an alpha channel, but the
// alpha is a plain rectangle: the black behind the icon and the blue glow around
// it are painted, not transparency, so used as it came the gear would sit in a
// dark square on the grass.
//
// The glow shades off gradually, so there is no one value that separates it from
// the icon - and worse, the icon's own outline is darker than the brightest part
// of the glow it sits in. So the flood from the corners is told to swallow
// everything dark, which takes the outline with it and stops at the bright rim,
// and then what it swallowed is given back a fixed distance: growing the icon by
// the outline's own width puts the outline back and leaves the glow out. That is
// what OutlineWidth is, and it is the one figure here worth re-measuring if the
// icon is ever redrawn.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice.
public static class SettingsButtonBuilder
{
    private const string SourcePath = "Assets/Materials/settings.png";
    private const string IconPath = "Assets/UI/Icons/settings_button.png";

    private const string RootName = "Settings_Ui";

    // Anything this dark is the black behind the icon, the glow around it, or the
    // icon's outline. Measured off the art: the glow flares to 0.28 right against
    // the rim and falls away from there, while the rim itself is a full 1.0, so
    // there is a wide gap to sit this in. Under 0.28 and the flare survives the
    // flood as tabs stuck to the icon's sides.
    private const float DarkCeiling = 0.4f;

    // How far the icon is grown back afterwards, in source pixels: the thickness
    // of the dark line the artist drew around the rim.
    private const int OutlineWidth = 14;

    private const int Margin = 3;
    private const int Downscale = 4;

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    // In the corner beside the lives pill, matching its height - which is why
    // the pill's own right margin has to leave room for this and the gap.
    private const float Size = 70f;
    private const float MarginRight = 36f;
    private const float MarginTop = 19f;

    // The gear is the smallest thing on the HUD and the only one a thumb has to
    // find precisely, so the part that answers a touch is grown past the part
    // that is drawn. Negative padding is Unity's way of saying outwards.
    private const float TouchMargin = -12f;

    [MenuItem("Tools/Build Settings Button")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        Color[] px = HudArt.Load(SourcePath, out int w, out int h);
        if (px == null) return;

        Sprite icon = WriteCropped(CutFromGlow(px, w, h), w, h);
        if (icon == null) return;

        Button button = BuildButton(canvas, icon);
        WireController(button);

        HudScene.Save(canvas);
        Debug.Log("[SettingsButton] Rebuilt in " + canvas.gameObject.scene.name + ": " + Size + "x" + Size +
                  " button " + MarginRight + " in from the right and " + MarginTop + " down from the top, art from " +
                  SourcePath + ".");
    }

    // ---- art ---------------------------------------------------------------

    private static Color[] CutFromGlow(Color[] px, int w, int h)
    {
        bool[] dark = FloodDark(px, w, h);

        var lit = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) lit[i] = !dark[i];

        // The glow is not uniformly dark - it flares where the rim is brightest,
        // and those flares are left behind by the flood as islands of their own.
        // The icon is the big shape, and the dark outline the flood swallowed is a
        // moat between it and anything floating outside, so keeping only the
        // largest piece drops the flares without touching the icon.
        lit = LargestPiece(lit, w, h);

        // What is left is the icon without its outline; growing it by the
        // outline's width takes the outline back off the flood.
        bool[] icon = Grow(lit, w, h, OutlineWidth);

        var cut = new Color[px.Length];
        for (int i = 0; i < px.Length; i++)
            cut[i] = icon[i] ? new Color(px[i].r, px[i].g, px[i].b, 1f) : new Color(0f, 0f, 0f, 0f);
        return cut;
    }

    private static bool[] FloodDark(Color[] px, int w, int h)
    {
        var dark = new bool[px.Length];
        var queue = new Queue<int>();

        void Push(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (dark[i]) return;
            Color c = px[i];
            // Transparent counts as dark: outside the rectangle the alpha channel
            // covers, there is nothing to keep.
            if (c.a > 0.5f && Mathf.Max(c.r, Mathf.Max(c.g, c.b)) >= DarkCeiling) return;
            dark[i] = true;
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

        return dark;
    }

    private static bool[] LargestPiece(bool[] mask, int w, int h)
    {
        var seen = new bool[mask.Length];
        var best = new List<int>();
        var piece = new List<int>();
        var stack = new Stack<int>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (seen[start] || !mask[start]) continue;

            piece.Clear();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                piece.Add(i);
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (seen[ni] || !mask[ni]) continue;
                    seen[ni] = true;
                    stack.Push(ni);
                }
            }

            if (piece.Count > best.Count) best = new List<int>(piece);
        }

        var kept = new bool[mask.Length];
        foreach (int i in best) kept[i] = true;
        return kept;
    }

    // Spreads a mask out by radius, along the rows and then the columns. Two
    // sliding windows rather than a box per pixel, because a 14px reach over a
    // million and a half pixels is a lot of looking at the same pixel otherwise.
    private static bool[] Grow(bool[] mask, int w, int h, int radius)
    {
        var rows = new bool[mask.Length];
        for (int y = 0; y < h; y++)
        {
            int count = 0;
            for (int x = 0; x <= radius && x < w; x++) if (mask[y * w + x]) count++;
            for (int x = 0; x < w; x++)
            {
                rows[y * w + x] = count > 0;
                int add = x + radius + 1, drop = x - radius;
                if (add < w && mask[y * w + add]) count++;
                if (drop >= 0 && mask[y * w + drop]) count--;
            }
        }

        var grown = new bool[mask.Length];
        for (int x = 0; x < w; x++)
        {
            int count = 0;
            for (int y = 0; y <= radius && y < h; y++) if (rows[y * w + x]) count++;
            for (int y = 0; y < h; y++)
            {
                grown[y * w + x] = count > 0;
                int add = y + radius + 1, drop = y - radius;
                if (add < h && rows[add * w + x]) count++;
                if (drop >= 0 && rows[drop * w + x]) count--;
            }
        }

        return grown;
    }

    private static Sprite WriteCropped(Color[] cut, int w, int h)
    {
        RectInt content = HudArt.OpaqueBounds(cut, w, h);
        if (content.width == 0)
        {
            Debug.LogError("[SettingsButton] Nothing left after cutting the glow away from " + SourcePath + ".");
            return null;
        }

        int x0 = Mathf.Max(0, content.xMin - Margin), y0 = Mathf.Max(0, content.yMin - Margin);
        int x1 = Mathf.Min(w, content.xMax + Margin), y1 = Mathf.Min(h, content.yMax + Margin);
        int cw = (x1 - x0) / Downscale * Downscale;
        int ch = (y1 - y0) / Downscale * Downscale;

        return HudArt.Write(IconPath, HudArt.Crop(cut, w, new RectInt(x0, y0, cw, ch)), cw, ch, Downscale);
    }

    // ---- scene -------------------------------------------------------------

    private static Button BuildButton(Canvas canvas, Sprite icon)
    {
        RectTransform rt = HudScene.Panel(canvas, RootName, icon);

        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(Size, Size);
        rt.anchoredPosition = new Vector2(-MarginRight, -MarginTop);

        // Near the front of the Canvas, above the joystick's press area so a tap
        // on the gear is a tap on the gear, and below every popup - including the
        // pause menu it opens, which covers it and offers Resume instead.
        rt.SetSiblingIndex(1);

        var image = rt.GetComponent<Image>();
        image.raycastTarget = true;
        image.raycastPadding = new Vector4(TouchMargin, TouchMargin, TouchMargin, TouchMargin);

        var button = rt.GetComponent<Button>();
        if (button == null) button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        return button;
    }

    private static void WireController(Button button)
    {
        var pause = Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        if (pause == null)
        {
            Debug.LogWarning("[SettingsButton] No PauseMenuController in the scene; the button is built but opens nothing.");
            return;
        }

        var so = new SerializedObject(pause);
        so.FindProperty("openButton").objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();

        HudScene.HideWithHud(button.gameObject);
    }
}
