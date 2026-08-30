using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Fits the drawn wallet to the HUD and rebuilds it in the open scene.
//
// wallet.png is a gold bar with a coin in its left end, a balance painted across
// it, and a shop badge on its right. Same as the coin counter: the painted figure
// comes out and EconomyManager's own total goes in its place.
//
// The badge on the end is why the whole bar is a button: it is the way into the
// shop now, so the separate shop button that used to sit under this is gone.
//
// The balance can run to more digits than the four the artist drew, so the label
// is given the whole slot between the coin and the shop badge and left to shrink
// into it. Four figures land at exactly the painted size.
//
// Same rule about scenes as JoystickArtBuilder: this works on whatever scene is
// open, never discards it, and is safe to run twice. Wallet_Ui and wallet_amount
// are kept rather than made afresh, because EconomyManager holds a reference to
// that text.
public static class WalletBadgeBuilder
{
    private const string SourcePath = "Assets/Materials/wallet.png";
    private const string PanelPath = "Assets/UI/Icons/wallet_panel.png";

    private const string RootName = "Wallet_Ui";
    private const string OldShopButtonName = "Shop_Ui";   // the square button this replaces
    private const string FillName = "Fill";   // the inset box the old sandwich needed
    private const string IconName = "Icon";   // the coin is painted into the art now
    private const string TextName = "wallet_amount";

    // ---- the art, in source pixels (y counted from the bottom) --------------

    private const float SrcWidth = 1881f, SrcHeight = 836f;
    private const float ContentRight = 1770f, ContentTop = 655f;

    // The field to clear: the four painted figures with their outline, and enough
    // of the interior's height either side that the shading is rebuilt in one
    // piece. The bands it is filled from stop short of the coin on one side and
    // the rule before the shop badge on the other, which is why they are narrow.
    private static readonly RectInt BalanceField = new RectInt(599, 270, 591, 310);
    private const int SampleWidth = 16;
    private const int Downscale = 2;

    // Where the painted figures sat, as fractions of the image: 621..1167 across
    // and 347..516 up.
    private const float PaintedCentreX = ((621f + 1167f) * 0.5f) / SrcWidth;
    private const float PaintedCentreY = ((347f + 516f) * 0.5f) / SrcHeight;
    private const float PaintedCapHeight = (516f - 347f) / SrcHeight;

    // Half the label's box either side of that centre - as much as the slot
    // between the coin and the rule allows. Taller than the figures are, because
    // TMP fits a whole line rather than the height of a numeral.
    private const float LabelHalfWidth = 0.17f;
    private const float LabelHalfHeight = 0.21f;

    // ---- layout ------------------------------------------------------------

    // Canvas units against the 1920x1080 reference the scaler matches on width.
    // 127 puts the bar itself at about 70 tall, matching the lives pill it hangs
    // under; the rest of the panel is the transparent margin round the drawing.
    private const float PanelHeight = 127f;
    private const float PanelWidth = PanelHeight * SrcWidth / SrcHeight;
    private const float ContentMarginRight = 36f;
    private const float ContentMarginTop = 105f;   // clear of the lives pill, which ends at 89

    private static readonly Color BalanceFace = new Color(1.00f, 0.99f, 0.93f);
    private static readonly Color BalanceOutline = new Color(0.10f, 0.04f, 0.01f, 1f);

    [MenuItem("Tools/Build Wallet Badge")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        // This one came on black rather than with an alpha channel, so the paper
        // comes off before the painted balance does.
        Color[] px = HudArt.Load(SourcePath, out int w, out int h);
        if (px == null) return;

        Color[] cut = HudArt.CutFromPaper(px, w, h);
        if (!HudArt.ErasePaintedValue(cut, w, h, BalanceField, SampleWidth)) return;

        Sprite panel = HudArt.Write(PanelPath, cut, w, h, Downscale);
        if (panel == null) return;

        RectTransform root = BuildPanel(canvas, panel);
        BuildBalance(root);
        WireShop(canvas, root);

        HudScene.Save(canvas);
        Debug.Log("[Wallet] Rebuilt in " + canvas.gameObject.scene.name + ": " + PanelWidth + "x" + PanelHeight +
                  " panel, its drawing " + ContentMarginRight + " in from the right and " + ContentMarginTop +
                  " down from the top, art from " + SourcePath + ".");
    }

    private static RectTransform BuildPanel(Canvas canvas, Sprite sprite)
    {
        RectTransform rt = HudScene.Panel(canvas, RootName, sprite);

        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        // Placed by the drawing's own edges rather than the image's, so the gap to
        // the lives pill above is the gap you can see, whatever transparent margin
        // the export happens to carry.
        rt.anchoredPosition = new Vector2(
            -(ContentMarginRight - (SrcWidth - ContentRight) / SrcWidth * PanelWidth),
            -(ContentMarginTop - (SrcHeight - ContentTop) / SrcHeight * PanelHeight));

        // Near the front of the Canvas, above the joystick's press area so the bar
        // can be pressed, and below every popup - it is a button now, and left
        // where it was it would sit on top of the pause menu and open the shop
        // from underneath it.
        rt.SetSiblingIndex(1);

        // Both are parts the drawing now contains: the inset box that darkened the
        // middle of the old frame, and the separate coin icon.
        HudScene.Remove(rt, FillName, IconName);

        var image = rt.GetComponent<Image>();
        image.raycastTarget = true;   // the whole bar is the way into the shop

        return rt;
    }

    // The shop badge is painted on the end of this bar, so the bar is the button.
    // The square shop button that used to sit under it goes: two ways in, one of
    // them unlabelled, is worse than one.
    private static void WireShop(Canvas canvas, RectTransform root)
    {
        var button = root.GetComponent<Button>();
        if (button == null) button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        var shop = Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
        if (shop == null)
        {
            Debug.LogWarning("[Wallet] No ShopUIController in the scene; the bar is built but opens nothing.");
            return;
        }

        var so = new SerializedObject(shop);
        so.FindProperty("hudOpenButton").objectReferenceValue = button;
        so.ApplyModifiedPropertiesWithoutUndo();
        HudScene.HideWithHud(root.gameObject);

        Transform old = canvas.transform.Find(OldShopButtonName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
            Debug.Log("[Wallet] Removed " + OldShopButtonName + " from the corner; the wallet bar opens the shop now.");
        }
    }

    private static void BuildBalance(RectTransform root)
    {
        TextMeshProUGUI tmp = HudScene.Value(root, TextName,
            new Vector2(PaintedCentreX - LabelHalfWidth, PaintedCentreY - LabelHalfHeight),
            new Vector2(PaintedCentreX + LabelHalfWidth, PaintedCentreY + LabelHalfHeight));

        float painted = PaintedCapHeight * PanelHeight / HudArt.CapHeightPerPoint;
        tmp.fontSize = painted;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = painted;
        tmp.fontSizeMin = painted * 0.55f;   // enough for six figures in the same slot
        // EconomyManager overwrites this on the first frame. It is set anyway so
        // the Scene view shows the shape the badge actually takes, rather than the
        // "Wallet: 0" that was left in the field and never appears in play.
        tmp.text = "8526";

        tmp.color = BalanceFace;
        HudArt.StyleValue(tmp, BalanceOutline);
    }
}
