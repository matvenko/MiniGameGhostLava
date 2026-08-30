using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Cuts the four drawn ability icons off their backgrounds and stacks them down
// the left of the HUD, under the coin counter.
//
// Each one came with a count already painted into the badge on its corner - a
// "2" in a coloured circle - and the count has to come from the game, so the
// same trick as the other panels applies: the painted figure is lifted out and a
// live label put where it was. Where it was is measured rather than typed in:
// the digit is the largest white shape in the badge corner of the icon, which is
// how its box, and from that the label's size and place, are found. The four
// badges are drawn at different sizes and sit at slightly different heights, so
// there is nothing to hard-code that would hold for all of them.
//
// The abilities do not do anything yet. AbilityBarUI holds the counts so that
// whatever ends up owning them has somewhere to put them.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice.
public static class AbilityBarBuilder
{
    private static readonly string[] Abilities = { "trap", "freeze", "shield", "teleport" };

    private const string SourceFolder = "Assets/Materials/";
    private const string IconFolder = "Assets/UI/Icons/";

    private const string RootName = "Abilities_Ui";
    private const string CountName = "Count";
    private const string OldTrapButtonName = "TrapButton";   // the square one this replaces

    // ---- art ---------------------------------------------------------------

    // The painted digit carries an outline and a shadow that reach about this far
    // past the white of it, and the colour to refill with is taken from a band
    // this deep just beyond that.
    private const int DigitMargin = 22;
    private const int SampleBand = 18;

    // The badge is in the bottom-right corner of the icon. Searching only there
    // keeps the snowflake, the portal and the lettering out of the running for
    // largest white shape.
    private const float BadgeFromLeft = 0.55f, BadgeFromBottom = 0.45f;
    private const int SmallestDigit = 2000;   // pixels; smaller white shapes are lettering

    private const int Margin = 3;
    private const int Downscale = 4;   // 1254px of art for a 132-unit button

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    private const float IconSize = 132f;
    private const float Gap = 14f;
    private const float MarginLeft = 36f;
    private const float FirstTop = 132f;   // clear of the coin counter, which ends at 120

    // The label's box, as a fraction of the icon either side of where the painted
    // digit was centred. Wider than the digit, because TMP fits a whole line's
    // height and would shrink the figure to fit its own descender otherwise.
    private const float LabelHalf = 0.15f;

    private static readonly Color CountOutline = new Color(0.03f, 0.03f, 0.07f, 1f);

    [MenuItem("Tools/Build Ability Bar")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        var sprites = new Sprite[Abilities.Length];
        var digitBoxes = new Rect[Abilities.Length];   // in fractions of the finished sprite

        for (int i = 0; i < Abilities.Length; i++)
        {
            sprites[i] = BuildIcon(Abilities[i], out digitBoxes[i]);
            if (sprites[i] == null) return;
        }

        RectTransform root = BuildRoot(canvas);
        var buttons = new Button[Abilities.Length];
        var labels = new TextMeshProUGUI[Abilities.Length];
        for (int i = 0; i < Abilities.Length; i++)
            buttons[i] = BuildButton(root, i, sprites[i], digitBoxes[i], out labels[i]);

        WireBar(root, buttons, labels);
        WireTrap(canvas, buttons[(int)AbilityBarUI.Ability.Trap]);
        HudScene.HideWithHud(root.gameObject);
        HudScene.Save(canvas);

        Debug.Log("[AbilityBar] Rebuilt in " + canvas.gameObject.scene.name + ": " + Abilities.Length +
                  " icons of " + IconSize + " down the left, first at " + FirstTop + " from the top.");
    }

    // ---- art ---------------------------------------------------------------

    private static Sprite BuildIcon(string ability, out Rect digitBox)
    {
        digitBox = new Rect();

        Color[] px = HudArt.Load(SourceFolder + ability + ".png", out int w, out int h);
        if (px == null) return null;

        Color[] cut = HudArt.CutFromPaper(px, w, h);

        if (!FindPaintedDigit(cut, w, h, out RectInt digit))
        {
            Debug.LogError("[AbilityBar] No painted count found in the badge on " + ability +
                           ".png. It is the largest white shape in the bottom-right corner - if the art has " +
                           "changed, check BadgeFromLeft/BadgeFromBottom.");
            return null;
        }

        EraseDigit(cut, w, h, digit);

        RectInt content = HudArt.OpaqueBounds(cut, w, h);
        RectInt crop = Grow(content, Margin, w, h);
        // A whole number of downscaled pixels, so halving twice drops nothing.
        crop.width -= crop.width % Downscale;
        crop.height -= crop.height % Downscale;

        digitBox = new Rect((digit.xMin - crop.xMin) / (float)crop.width,
                            (digit.yMin - crop.yMin) / (float)crop.height,
                            digit.width / (float)crop.width,
                            digit.height / (float)crop.height);

        Debug.Log("[AbilityBar] " + ability + ": digit " + digit.width + "x" + digit.height +
                  " at " + digit.xMin + "," + digit.yMin + " in a " + crop.width + "x" + crop.height + " crop.");

        return HudArt.Write(IconFolder + ability + "_button.png", HudArt.Crop(cut, w, crop),
                            crop.width, crop.height, Downscale);
    }

    // The painted count: the largest white shape in the corner the badge sits in.
    // The icons' other white - a snowflake, lettering, a glint on the rim - is
    // either outside that corner or smaller than a numeral.
    private static bool FindPaintedDigit(Color[] px, int w, int h, out RectInt digit)
    {
        digit = new RectInt();

        bool White(int i)
        {
            Color c = px[i];
            if (c.a < 0.5f) return false;
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return mx > 0.90f && mx - mn < 0.08f;
        }

        int x0 = (int)(w * BadgeFromLeft), y1 = (int)(h * BadgeFromBottom);
        var seen = new bool[px.Length];
        int best = 0;

        for (int y = 0; y <= y1; y++)
        for (int x = x0; x < w; x++)
        {
            int start = y * w + x;
            if (seen[start] || !White(start)) continue;

            int bx0 = x, bx1 = x, by0 = y, by1 = y, count = 0;
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int ix = i % w, iy = i / w;
                count++;
                if (ix < bx0) bx0 = ix;
                if (ix > bx1) bx1 = ix;
                if (iy < by0) by0 = iy;
                if (iy > by1) by1 = iy;

                for (int d = 0; d < 4; d++)
                {
                    int nx = ix + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = iy + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (seen[ni] || !White(ni)) continue;
                    seen[ni] = true;
                    stack.Push(ni);
                }
            }

            if (count > best && count >= SmallestDigit)
            {
                best = count;
                digit = new RectInt(bx0, by0, bx1 - bx0 + 1, by1 - by0 + 1);
            }
        }

        return best > 0;
    }

    // Paints the badge back over the digit. The badge shades from top to bottom,
    // so the fill is a vertical blend between the clean disc just under the digit
    // and the clean disc just over it.
    //
    // Both colours are read from the columns in the middle of the digit and used
    // for the whole field. Reading each column's own would be closer to the truth
    // in the middle and wrong at the edges, where a column that far out has left
    // the disc and would sample the icon behind it.
    private static void EraseDigit(Color[] px, int w, int h, RectInt digit)
    {
        RectInt field = Grow(digit, DigitMargin, w, h);
        int inset = Mathf.Max(1, digit.width / 4);

        Color below = Average(px, w, digit.xMin + inset, digit.width - 2 * inset, field.yMin - SampleBand, SampleBand);
        Color above = Average(px, w, digit.xMin + inset, digit.width - 2 * inset, field.yMax, SampleBand);

        float belowAt = field.yMin - (SampleBand + 1) * 0.5f;
        float aboveAt = field.yMax + (SampleBand - 1) * 0.5f;

        for (int y = field.yMin; y < field.yMax; y++)
        {
            Color row = Color.Lerp(below, above, (y - belowAt) / (aboveAt - belowAt));
            for (int x = field.xMin; x < field.xMax; x++)
                px[y * w + x] = row;
        }
    }

    private static Color Average(Color[] px, int w, int x0, int width, int y0, int height)
    {
        var sum = new Color(0f, 0f, 0f, 0f);
        int n = 0;
        for (int y = y0; y < y0 + height; y++)
        for (int x = x0; x < x0 + width; x++)
        {
            sum += px[y * w + x];
            n++;
        }
        return n == 0 ? Color.clear : sum / n;
    }

    private static RectInt Grow(RectInt box, int by, int w, int h)
    {
        int x0 = Mathf.Max(0, box.xMin - by), y0 = Mathf.Max(0, box.yMin - by);
        int x1 = Mathf.Min(w, box.xMax + by), y1 = Mathf.Min(h, box.yMax + by);
        return new RectInt(x0, y0, x1 - x0, y1 - y0);
    }

    // ---- scene -------------------------------------------------------------

    private static RectTransform BuildRoot(Canvas canvas)
    {
        Transform found = canvas.transform.Find(RootName);
        GameObject go = found != null ? found.gameObject : new GameObject(RootName, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(IconSize, Abilities.Length * IconSize + (Abilities.Length - 1) * Gap);
        rt.anchoredPosition = new Vector2(MarginLeft, -FirstTop);

        // Above the joystick's press area so the buttons can be hit, below every
        // popup so the pause menu and the shop cover them.
        rt.SetSiblingIndex(1);
        return rt;
    }

    private static Button BuildButton(RectTransform root, int index, Sprite sprite, Rect digitBox,
                                      out TextMeshProUGUI label)
    {
        string name = char.ToUpper(Abilities[index][0]) + Abilities[index].Substring(1) + "_Ui";
        Transform found = root.Find(name);
        GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));

        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(IconSize, IconSize);
        rt.anchoredPosition = new Vector2(0f, -index * (IconSize + Gap));
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        label = BuildCount(rt, digitBox);
        return button;
    }

    private static TextMeshProUGUI BuildCount(RectTransform icon, Rect digitBox)
    {
        Vector2 centre = digitBox.center;
        TextMeshProUGUI tmp = HudScene.Value(icon, CountName,
            new Vector2(centre.x - LabelHalf, centre.y - LabelHalf),
            new Vector2(centre.x + LabelHalf, centre.y + LabelHalf));

        // Sized off the figure that was painted there, so the live one sits at the
        // same weight in the badge as the artist's did.
        float painted = digitBox.height * IconSize / HudArt.CapHeightPerPoint;
        tmp.fontSize = painted;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = painted;
        tmp.fontSizeMin = painted * 0.6f;
        tmp.color = Color.white;
        tmp.text = "2";    // AbilityBarUI overwrites this on the first frame
        HudArt.StyleValue(tmp, CountOutline);
        return tmp;
    }

    // Trap is the one ability that already works, so its button is the real
    // thing: TrapManager places a trap from it and reports the count back to the
    // bar. The old square button in the bottom-right corner did both of those
    // jobs and goes - two ways to lay a trap is one too many, and that button was
    // also the last child of the Canvas, drawing over the pause menu and the shop.
    private static void WireTrap(Canvas canvas, Button trapButton)
    {
        var traps = Object.FindAnyObjectByType<TrapManager>(FindObjectsInactive.Include);
        if (traps == null)
        {
            Debug.LogWarning("[AbilityBar] No TrapManager in the scene; the trap button is built but places nothing.");
            return;
        }

        var so = new SerializedObject(traps);
        so.FindProperty("placeButton").objectReferenceValue = trapButton;
        // Emptied: the badge belongs to AbilityBarUI now, and TrapManager reports
        // its count to that instead of writing a label of its own.
        so.FindProperty("countText").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        Transform old = canvas.transform.Find(OldTrapButtonName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
            Debug.Log("[AbilityBar] Removed the old " + OldTrapButtonName + " from the corner; " +
                      "TrapManager now lays traps from the TRAP button in the bar.");
        }
    }

    private static void WireBar(RectTransform root, Button[] buttons, TextMeshProUGUI[] labels)
    {
        var bar = root.GetComponent<AbilityBarUI>();
        if (bar == null) bar = root.gameObject.AddComponent<AbilityBarUI>();

        var so = new SerializedObject(bar);
        SerializedProperty buttonArray = so.FindProperty("buttons");
        SerializedProperty labelArray = so.FindProperty("countLabels");
        buttonArray.arraySize = buttons.Length;
        labelArray.arraySize = labels.Length;
        for (int i = 0; i < buttons.Length; i++)
        {
            buttonArray.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            labelArray.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
        }

        // Trap's count is real - TrapManager reads it out of PlayerPrefs and
        // reports it - so the bar must not put a made-up number in that badge
        // first. The other three are still placeholders.
        SerializedProperty starting = so.FindProperty("startingCounts");
        starting.arraySize = buttons.Length;
        starting.GetArrayElementAtIndex((int)AbilityBarUI.Ability.Trap).intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
