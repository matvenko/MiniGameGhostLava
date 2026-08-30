using TMPro;
using UnityEditor;
using UnityEngine;

// Fits the drawn level badge to the HUD and rebuilds it in the open scene.
//
// lvl-indicator.png is a blue capsule with a star let into each end, the word
// LEVEL lettered across it in white and the number beside it in gold. Only the
// number changes in play, so only the number is lifted out: the word stays as
// painted, which is the whole reason for using the art rather than setting
// "LEVEL 1" in Liberation Sans and calling it done.
//
// That leaves the number in a fixed slot, and a slot is all the art gives it -
// the space between the word and the right-hand bevel is about as wide as one
// painted digit and a half. Levels in single figures land at exactly the size
// they were drawn; from ten on, auto-sizing takes a tenth off them. Widening the
// slot would mean redrawing the word.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice. Level_Ui and level_text are
// kept rather than made afresh, because LevelManager holds a reference to that
// text.
public static class LevelBadgeBuilder
{
    private const string SourcePath = "Assets/Materials/lvl-indicator.png";
    private const string PanelPath = "Assets/UI/Icons/level_badge.png";

    private const string RootName = "Level_Ui";
    private const string FillName = "Fill";   // the inset box the old sandwich needed
    private const string TextName = "level_text";

    // ---- the art, in source pixels (y counted from the bottom) --------------

    private const float SrcWidth = 1881f, SrcHeight = 836f;

    // Where the drawing actually is inside its image. The badge is centred left
    // to right but sits high, with most of the empty margin below it, so the
    // panel cannot simply be placed by its own top edge.
    private const float ContentTop = 681f;

    // The field to clear: the gold digit with its outline and shadow, from just
    // clear of the word's last L across to the bevel side, and the interior's
    // height between the two bevels so the shading is rebuilt in one piece.
    //
    // The bands either side of it are narrow because there is not much clean
    // capsule to work with: the word's shadow fades out at 1218 and the digit's
    // own reaches 1240 on the left and 1416 on the right, so the field runs
    // 1232..1425 with a dozen columns of capsule left over on each side. Anything
    // wider samples one shadow or the other and fills the slot with it.
    private static readonly RectInt DigitField = new RectInt(1232, 262, 194, 325);
    private const int SampleWidth = 12;
    private const int Downscale = 2;

    // The painted digit, as fractions of the whole image: 1251..1399 across and
    // 298..558 up. Its height is what the live one is sized from.
    private const float PaintedCapHeight = (558f - 298f) / SrcHeight;
    private const float PaintedCentreY = ((558f + 298f) * 0.5f) / SrcHeight;

    // The slot the live number is centred in: from just clear of the word to the
    // start of the bevel. Its centre sits a little right of where the painted
    // digit was, and that is deliberate - the artist left more room on the right
    // than on the left, and a number that grows a second digit has to grow
    // somewhere. Starting the slot at the word's edge instead puts LEVEL and 12
    // hard against each other.
    private const float SlotLeft = 1240f / SrcWidth, SlotRight = 1500f / SrcWidth;

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    private const float PanelWidth = 300f;
    private const float PanelHeight = PanelWidth * SrcHeight / SrcWidth;
    private const float ContentMarginTop = 20f;   // of the drawing, not of the image around it

    private static readonly Color DigitTop = new Color(1.00f, 0.88f, 0.15f);
    private static readonly Color DigitBottom = new Color(0.99f, 0.66f, 0.02f);
    private static readonly Color DigitOutline = new Color(0.12f, 0.05f, 0.01f, 1f);

    [MenuItem("Tools/Build Level Badge")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        Sprite panel = HudArt.BuildSprite(SourcePath, PanelPath, DigitField, SampleWidth, Downscale);
        if (panel == null) return;

        RectTransform root = BuildPanel(canvas, panel);
        BuildNumber(root);

        HudScene.Save(canvas);
        Debug.Log("[LevelBadge] Rebuilt in " + canvas.gameObject.scene.name + ": " + PanelWidth + "x" + PanelHeight +
                  " panel centred at the top, art from " + SourcePath + ".");
    }

    private static RectTransform BuildPanel(Canvas canvas, Sprite sprite)
    {
        RectTransform rt = HudScene.Panel(canvas, RootName, sprite);

        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        // Placed by the drawing's own top edge rather than the image's, so the
        // badge hangs the same distance below the top of the screen as the coin
        // counter does, whatever transparent margin the export happens to carry.
        float topMargin = (1f - ContentTop / SrcHeight) * PanelHeight;
        rt.anchoredPosition = new Vector2(0f, topMargin - ContentMarginTop);

        // Under every popup, so the pause menu and the shop cover the badge
        // instead of it floating over their backdrops.
        rt.SetSiblingIndex(1);

        HudScene.Remove(rt, FillName);
        return rt;
    }

    private static void BuildNumber(RectTransform root)
    {
        // Half the slot's width either side of its centre, and a tall enough box
        // for a whole line: TMP fits ascent to descent, not the height of a
        // numeral, so a rect drawn around the digit alone would shrink it.
        const float halfHeight = 0.35f;
        TextMeshProUGUI tmp = HudScene.Value(root, TextName,
            new Vector2(SlotLeft, PaintedCentreY - halfHeight),
            new Vector2(SlotRight, PaintedCentreY + halfHeight));

        float painted = PaintedCapHeight * PanelHeight / HudArt.CapHeightPerPoint;
        tmp.fontSize = painted;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = painted;
        tmp.fontSizeMin = painted * 0.5f;   // enough for three figures in the same slot
        // LevelManager overwrites this on the first frame; it is set so the Scene
        // view shows what the badge actually looks like.
        tmp.text = "1";

        tmp.color = Color.white;
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(DigitTop, DigitTop, DigitBottom, DigitBottom);
        HudArt.StyleValue(tmp, DigitOutline);
    }
}
