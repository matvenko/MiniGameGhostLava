using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Fits the drawn coin counter to the HUD and rebuilds it in the open scene.
//
// The counter is now one piece of art - coins-count.png: a green capsule with
// the gold coin hanging off its left end, drawn with a bevel, a gloss and a
// glow that nothing generated from distance fields was going to match. What was
// there before was the house pattern for every HUD box in this scene, a
// coloured rectangle with a darker one inset inside it, which reads as a debug
// readout rather than as part of the game.
//
// The art arrives with a count already painted into it and the count has to come
// from RewardSystem, so HudArt lifts those digits back out and the live number
// is laid over the same place through anchors expressed as fractions of the art,
// which keeps it there whatever size the panel is given.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice. Coins_Ui and coins_amount
// are kept rather than made afresh, because RewardSystem holds a reference to
// that text.
public static class CoinCounterBuilder
{
    private const string SourcePath = "Assets/Materials/coins-count.png";
    private const string PanelPath = "Assets/UI/Icons/coin_counter.png";
    private const string CoinIconPath = "Assets/UI/Icons/coin_icon.png";

    private const string RootName = "Coins_Ui";
    private const string FillName = "Fill";   // the inset box the old sandwich needed
    private const string IconName = "Icon";   // the coin is painted into the art now
    private const string TextName = "coins_amount";

    // ---- the painted count, in source pixels (y counted from the bottom) ----

    // The field to clear. Wider than the digits themselves, because they carry a
    // dark outline and a shadow that reach about 25px past the glyphs, and short
    // of the borders top and bottom so the bevel is never touched.
    private static readonly RectInt CountField = new RectInt(950, 196, 666, 411);
    private const int SampleWidth = 24;
    private const int Downscale = 2;   // 2048x768 of art is far more than a 272-unit panel needs

    // The box the live text is anchored in, as fractions of the whole image. Its
    // centre is where the painted digits were centred - x 0.636, y 0.497 - and
    // that is the part that matters, because the text is centred in it. It is
    // otherwise much larger than the digits were: TMP fits a line's full
    // ascent-to-descent box rather than the height of a numeral, so a rect drawn
    // tight around the painted digits made auto-sizing shrink the count to two
    // thirds of the size the artist drew it at.
    private const float TextLeft = 0.400f, TextRight = 0.872f;
    private const float TextBottom = 0.140f, TextTop = 0.855f;

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    // The image is mostly capsule but carries a glow and the coin's overhang, so
    // these are bigger than the pill looks: at this size the capsule itself comes
    // out about 236x65, which is the width the old readout had.
    private const float PanelWidth = 272f;
    private const float PanelHeight = 102f;
    private const float MarginLeft = 24f;
    private const float MarginTop = 18f;

    private const float FontSize = 38f;   // the painted digits stood 0.34 of the art tall
    private const float FontSizeMin = 24f;

    private static readonly Color CountTop = new Color(0.949f, 0.965f, 0.980f);
    private static readonly Color CountBottom = Color.white;
    private static readonly Color CountOutline = new Color(0.02f, 0.05f, 0.02f, 1f);

    [MenuItem("Tools/Build Coin Counter")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        Sprite panel = HudArt.BuildSprite(SourcePath, PanelPath, CountField, SampleWidth, Downscale);
        if (panel == null) return;

        RectTransform root = BuildPanel(canvas, panel);
        BuildCount(root);

        HudScene.Save(canvas);
        Debug.Log("[CoinCounter] Rebuilt in " + canvas.gameObject.scene.name + ": " + PanelWidth + "x" + PanelHeight +
                  " panel pinned " + MarginLeft + "/" + MarginTop + " in from the top-left, art from " + SourcePath + ".");
    }

    private static RectTransform BuildPanel(Canvas canvas, Sprite sprite)
    {
        RectTransform rt = HudScene.Panel(canvas, RootName, sprite);

        // Pinned to the corner rather than offset from the middle, which is how
        // the old one was placed: the canvas matches on width, so a
        // centre-anchored box slides down the screen as the aspect gets taller.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        rt.anchoredPosition = new Vector2(MarginLeft, -MarginTop);

        // Both of these were parts the drawing now contains: the inset box that
        // darkened the middle of the old coloured frame, and the separate coin
        // icon, which survives as an asset for the shop rows that still use it.
        HudScene.Remove(rt, FillName, IconName);

        return rt;
    }

    private static void BuildCount(RectTransform root)
    {
        TextMeshProUGUI tmp = HudScene.Value(root, TextName,
            new Vector2(TextLeft, TextBottom), new Vector2(TextRight, TextTop));

        tmp.fontSize = FontSize;
        tmp.enableAutoSizing = true;   // a level can start with three digits; the field cannot grow
        tmp.fontSizeMax = FontSize;
        tmp.fontSizeMin = FontSizeMin;
        // RewardSystem overwrites this on the first frame. It is set anyway so
        // the Scene view shows the shape the counter actually takes, rather than
        // the word "Coins" that was left in the field and never appears in play.
        tmp.text = "128";

        // The painted digits are white going slightly cooler towards the top, cut
        // out with a dark rim and dropped on a soft shadow. Matching that is what
        // keeps the live number from reading as a label pasted over the artwork.
        tmp.color = Color.white;
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(CountTop, CountTop, CountBottom, CountBottom);
        HudArt.StyleValue(tmp, CountOutline);
    }
}
