using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the settings popup out of the drawn sheet the artwork arrived on.
//
// Everything the player can press is cut straight from that sheet - the title
// plate, the close cross, the three buttons, the checkboxes, the radios, the
// switches, the little joystick and ghost - so the popup is the artist's work,
// not a reconstruction of it. What the sheet does not carry is an empty box:
// every panel on it was painted with its lettering already in place. Those come
// from PanelArt, drawn to the sheet's own colours, and the lettering goes back on
// top as live text in Fredoka, so the game can change it and so it stays sharp
// whatever size the popup is given.
//
// Safe to run twice: it works on whatever scene is open, reuses the objects it
// made last time rather than piling up duplicates, and never discards the scene.
//
// Nothing in here decides anything. Resume, Quit, the close cross and the two
// audio switches go to PauseMenuController; the checkboxes and the four position
// cards go to SettingsPopupUI, which writes to GameSettings and lets the stick
// and the ability bar hear about it. This only builds them and points them at
// each other.
public static class SettingsPopupBuilder
{
    private const string AtlasPath = "Assets/UI/Icons/settings-popup-sprite.png";

    // The title plate came on its own as well as on the sheet, drawn four times
    // the size and with room around it. The one on the sheet is squeezed up
    // against its neighbours, and the frame's top edge is shaved flat where the
    // cut passes through it, so the plate is taken from here instead.
    private const string PlatePath = "Assets/UI/Icons/settings.png";
    private const string OutDir = "Assets/UI/Icons/Settings/";
    private const string FontDir = "Assets/UI/fonts/";

    private const string PopupName = "SettingsPopup";

    // ---- what to cut out of the sheet --------------------------------------

    // Art rects in sheet pixels with y counted from the bottom, and how much
    // clear margin to keep around each one for the glow the artist painted
    // outside it. The margins are as wide as the gap to the next piece allows.
    private struct Cut
    {
        public string Name;
        public RectInt Art;
        public int Pad;

        public Cut(string name, int x, int y, int w, int h, int pad)
        {
            Name = name;
            Art = new RectInt(x, y, w, h);
            Pad = pad;
        }
    }

    private static readonly Cut[] Cuts =
    {
        new Cut("close", 708, 875, 114, 115, 10),
        new Cut("btn_resume", 1020, 616, 480, 123, 12),
        new Cut("btn_info", 1020, 470, 480, 119, 12),
        new Cut("btn_quit", 1020, 320, 480, 124, 12),
        // The empty box and the ticked one, the empty ring and the lit one, are
        // cut to matching sizes - a couple of rows more than the art needs on the
        // empty ones - so that swapping the sprite at runtime swaps the picture
        // and nothing else.
        new Cut("check_off", 645, 540, 73, 73, 4),
        new Cut("check_on", 645, 459, 73, 73, 4),
        new Cut("radio_off", 738, 540, 72, 73, 4),
        new Cut("radio_on", 738, 459, 72, 73, 4),
        new Cut("switch_on", 843, 542, 139, 62, 4),
        new Cut("switch_off", 843, 385, 139, 61, 4),
        new Cut("icon_music", 39, 121, 73, 72, 5),
        new Cut("icon_music_off", 130, 121, 73, 72, 5),
        new Cut("icon_sfx", 39, 37, 73, 73, 5),
        new Cut("icon_sfx_off", 130, 37, 73, 73, 5),
        new Cut("icon_joystick", 426, 66, 85, 111, 6),
        new Cut("icon_ghost", 533, 79, 89, 87, 6),
    };

    // ---- the colours the sheet is painted in -------------------------------

    private static readonly Color BodyTop = Hex(0x1E2C63), BodyBottom = Hex(0x101A44);
    private static readonly Color BodyRim = Hex(0x4E8BE0), BodyGlow = Hex(0x3E7FD8);
    private static readonly Color BoxTop = Hex(0x16224F), BoxBottom = Hex(0x0D1638), BoxRim = Hex(0x2C4C92);
    private static readonly Color RowTop = Hex(0x0D2861), RowBottom = Hex(0x08214F), RowRim = Hex(0x4A7CC8);
    private static readonly Color CardTop = Hex(0x152852), CardBottom = Hex(0x071E4B), CardRim = Hex(0x4870B0);
    private static readonly Color PickedRim = Hex(0x01F0EA);

    private static readonly Color HeadingInk = Hex(0x6FB0FF);
    private static readonly Color LabelInk = Color.white;
    private static readonly Color SubInk = Hex(0x93A9CE);

    // ---- layout, in canvas units against the 1920x1080 the scaler matches ---

    // Short enough, and low enough, to leave the level badge at the top of the
    // HUD showing above it - the popup is what the design puts over the board,
    // not a curtain across the whole screen.
    private const float PopupW = 1330f, PopupH = 800f, PopupY = -34f;

    private const float ContentTop = 288f, ContentBottom = -372f;
    private const float ColumnW = 619f, ColumnX = 320.5f;

    private const float RowW = 575f;

    private static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, int> Pads = new Dictionary<string, int>();
    private static readonly Dictionary<RectTransform, string> Shown = new Dictionary<RectTransform, string>();

    private static TMP_FontAsset _semi, _medium, _regular;

    [MenuItem("Tools/Build Settings Popup")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        if (!CutSheet()) return;
        if (!LoadFonts()) return;

        Transform pausePanel = canvas.transform.Find("PausePanel");
        if (pausePanel == null)
        {
            Debug.LogError("[SettingsPopup] No PausePanel under the Canvas to build into.");
            return;
        }

        // The popup replaces the placeholder card the pause menu used to be, so
        // the card goes. Everything that pointed into it is re-pointed below;
        // the one thing that is not is the shop button it carried, and the shop
        // has its own way in from the wallet bar on the HUD.
        Transform oldCard = pausePanel.Find("Card");
        if (oldCard != null) Object.DestroyImmediate(oldCard.gameObject);

        var dimmer = pausePanel.GetComponent<Image>();
        if (dimmer != null) dimmer.color = new Color(0f, 0f, 0f, 0.55f);

        RectTransform popup = BuildPopup(pausePanel);
        Rewire(popup);

        HudScene.Save(canvas);
        Debug.Log("[SettingsPopup] Rebuilt in " + canvas.gameObject.scene.name + ": " + PopupW + "x" + PopupH +
                  " popup under PausePanel, art cut from " + AtlasPath + ".");
        Selection.activeGameObject = popup.gameObject;
    }

    // ---- art ---------------------------------------------------------------

    private static bool CutSheet()
    {
        Color[] sheet = HudArt.Load(AtlasPath, out int w, out int h);
        if (sheet == null) return false;

        Art.Clear();
        Pads.Clear();
        Shown.Clear();
        Directory.CreateDirectory(OutDir);

        foreach (Cut cut in Cuts)
        {
            var box = new RectInt(cut.Art.xMin - cut.Pad, cut.Art.yMin - cut.Pad,
                                  cut.Art.width + cut.Pad * 2, cut.Art.height + cut.Pad * 2);
            if (box.xMin < 0 || box.yMin < 0 || box.xMax > w || box.yMax > h)
            {
                Debug.LogError("[SettingsPopup] " + cut.Name + " and its " + cut.Pad +
                               "px margin fall outside the " + w + "x" + h + " sheet.");
                return false;
            }

            Sprite sprite = HudArt.Write(OutDir + cut.Name + ".png", HudArt.Crop(sheet, w, box),
                                         box.width, box.height, 1);
            if (sprite == null) return false;
            Keep(cut.Name, sprite, cut.Pad);
        }

        if (!CutPlate()) return false;

        BuildPanels();
        return true;
    }

    // The title plate, off its own file rather than off the sheet.
    //
    // It is trimmed to the plate with a margin, and the trim is kept centred on
    // the plate so the middle of the sprite is the middle of the plate - which is
    // what the popup hangs it by. The margin is only as wide as the file allows:
    // this art is cropped tight to left and right already, so there it comes out
    // at the few pixels that are there and the fitting still lands right.
    private static bool CutPlate()
    {
        Color[] px = HudArt.Load(PlatePath, out int w, out int h);
        if (px == null) return false;

        RectInt body = HudArt.OpaqueBounds(px, w, h, 0.5f);
        if (body.width == 0)
        {
            Debug.LogError("[SettingsPopup] " + PlatePath + " is empty.");
            return false;
        }

        const int Margin = 20, Downscale = 2;
        int cx = (body.xMin + body.xMax) / 2, cy = (body.yMin + body.yMax) / 2;
        int halfW = Fit(Mathf.Min(Mathf.Min(cx, w - cx), body.width / 2 + Margin));
        int halfH = Fit(Mathf.Min(Mathf.Min(cy, h - cy), body.height / 2 + Margin));

        var box = new RectInt(cx - halfW, cy - halfH, halfW * 2, halfH * 2);
        Sprite plate = HudArt.Write(OutDir + "title_plate.png", HudArt.Crop(px, w, box),
                                    box.width, box.height, Downscale);
        if (plate == null) return false;

        Keep("title_plate", plate, (halfW - body.width / 2) / Downscale);
        return true;

        // Halves have to survive the downscale as whole pixels, both of them.
        int Fit(int half) => half / Downscale * Downscale;
    }

    private static void BuildPanels()
    {
        Panel("panel_body", (int)PopupW, (int)PopupH, new PanelArt.Style
        {
            radius = 46f,
            pad = 30f,
            fillTop = BodyTop,
            fillBottom = BodyBottom,
            rim = BodyRim,
            rimWidth = 4f,
            glow = new Color(BodyGlow.r, BodyGlow.g, BodyGlow.b, 0.5f),
            glowSize = 26f,
            sheen = new Color(1f, 1f, 1f, 0.10f),
            sheenHeight = 30f
        });

        var box = new PanelArt.Style
        {
            radius = 22f,
            pad = 6f,
            fillTop = new Color(BoxTop.r, BoxTop.g, BoxTop.b, 0.85f),
            fillBottom = new Color(BoxBottom.r, BoxBottom.g, BoxBottom.b, 0.85f),
            rim = new Color(BoxRim.r, BoxRim.g, BoxRim.b, 0.9f),
            rimWidth = 2f,
            sheen = new Color(1f, 1f, 1f, 0.05f),
            sheenHeight = 16f
        };
        Panel("panel_box_hud", (int)ColumnW, 240, box);
        Panel("panel_box_abilities", (int)ColumnW, 404, box);
        Panel("panel_box_audio", (int)ColumnW, 250, box);

        var row = new PanelArt.Style
        {
            radius = 18f,
            pad = 6f,
            fillTop = RowTop,
            fillBottom = RowBottom,
            rim = new Color(RowRim.r, RowRim.g, RowRim.b, 0.95f),
            rimWidth = 3f,
            sheen = new Color(1f, 1f, 1f, 0.07f),
            sheenHeight = 12f
        };
        Panel("panel_row", (int)RowW, 78, row);
        Panel("panel_row_tall", (int)RowW, 84, row);

        Panel("panel_card", 285, 152, new PanelArt.Style
        {
            radius = 20f,
            pad = 8f,
            fillTop = CardTop,
            fillBottom = CardBottom,
            rim = CardRim,
            rimWidth = 3f,
            sheen = new Color(1f, 1f, 1f, 0.06f),
            sheenHeight = 14f
        });

        // The picked card differs from the others by its rim alone - a solid cyan
        // line, no bloom around it.
        Panel("panel_card_picked", 285, 152, new PanelArt.Style
        {
            radius = 20f,
            pad = 8f,
            fillTop = CardTop,
            fillBottom = CardBottom,
            rim = PickedRim,
            rimWidth = 4f,
            sheen = new Color(1f, 1f, 1f, 0.06f),
            sheenHeight = 14f
        });

        Keep("bead", PanelArt.Bead(OutDir + "bead.png", 32, Hex(0x1FA9E8), Hex(0x9DF0FF),
                                   new Color(0.13f, 0.83f, 1f, 0.75f)), 0);
        Keep("disc", PanelArt.Disc(OutDir + "disc.png", 96,
                                   new Color(0.77f, 0.84f, 0.94f, 0.30f),
                                   new Color(0.84f, 0.89f, 0.97f, 0.55f)), 0);
        Keep("rule", PanelArt.Rule(OutDir + "rule.png", 170, 3,
                                   new Color(BodyRim.r, BodyRim.g, BodyRim.b, 0.85f)), 0);
    }

    // A generated panel is drawn inside a texture the given size plus its margin,
    // so the size quoted here is the box you see and the margin is the room the
    // glow needs around it.
    private static void Panel(string name, int w, int h, PanelArt.Style style)
    {
        int pad = Mathf.RoundToInt(style.pad);
        Keep(name, PanelArt.Panel(OutDir + name + ".png", w + pad * 2, h + pad * 2, style), pad);
    }

    private static void Keep(string name, Sprite sprite, int pad)
    {
        Art[name] = sprite;
        Pads[name] = pad;
    }

    // ---- fonts -------------------------------------------------------------

    private static bool LoadFonts()
    {
        _semi = FontAsset("SemiBold");
        _medium = FontAsset("Medium");
        _regular = FontAsset("Regular");
        return _semi != null && _medium != null && _regular != null;
    }

    private static TMP_FontAsset FontAsset(string face)
    {
        string outPath = FontDir + "TMP/Fredoka-" + face + " SDF.asset";
        var made = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
        if (made != null) return made;

        string ttf = FontDir + "Fredoka-" + face + ".ttf";
        AssetDatabase.ImportAsset(ttf, ImportAssetOptions.ForceUpdate);
        var source = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (source == null)
        {
            Debug.LogError("[SettingsPopup] No font at " + ttf + ".");
            return null;
        }

        Directory.CreateDirectory(FontDir + "TMP");
        made = TMP_FontAsset.CreateFontAsset(source);
        made.name = "Fredoka-" + face + " SDF";
        AssetDatabase.CreateAsset(made, outPath);

        // The material and the glyph atlas belong to the font asset rather than
        // being assets of their own; left outside it they come back null on the
        // next reload and every label goes pink.
        made.material.name = made.name + " Material";
        AssetDatabase.AddObjectToAsset(made.material, made);
        if (made.atlasTextures != null && made.atlasTextures.Length > 0)
        {
            made.atlasTextures[0].name = "Font Atlas";
            AssetDatabase.AddObjectToAsset(made.atlasTextures[0], made);
        }

        AssetDatabase.SaveAssets();
        return made;
    }

    // ---- the popup ---------------------------------------------------------

    private static RectTransform BuildPopup(Transform pausePanel)
    {
        RectTransform popup = Child(pausePanel, PopupName);
        popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
        popup.pivot = new Vector2(0.5f, 0.5f);
        popup.sizeDelta = new Vector2(PopupW, PopupH);
        popup.anchoredPosition = new Vector2(0f, PopupY);

        // The body swallows presses so a tap inside the popup is not also a tap on
        // the joystick behind it.
        Sheet(popup, "Body", "panel_body", PopupW, Vector2.zero).raycastTarget = true;

        BuildHudOptions(popup);
        BuildAbilitiesPosition(popup);
        BuildAudio(popup);
        BuildButtons(popup);

        // Last, so the plate and the cross sit over the body. Both straddle the
        // popup's edge rather than sitting inside it: the plate is centred on the
        // top edge, the cross on the top-right corner.
        Sheet(popup, "TitlePlate", "title_plate", 540f, new Vector2(0f, PopupH * 0.5f));
        Press(popup, "CloseButton", "close", 84f, new Vector2(PopupW * 0.5f, PopupH * 0.5f));

        return popup;
    }

    private static void BuildHudOptions(RectTransform popup)
    {
        RectTransform box = Sheet(popup, "HudOptionsBox", "panel_box_hud", ColumnW,
                                  new Vector2(-ColumnX, ContentTop - 120f)).rectTransform;
        Heading(box, "HUD OPTIONS", 82f);

        // The row is the press target rather than the box drawn on it: the box is
        // fifty units across and a thumb is not.
        RectTransform row = Press(box, "Row_Joystick", "panel_row", RowW, new Vector2(0f, 19f)).rectTransform;
        Sheet(row, "Check", "check_off", 52f, new Vector2(-225f, 0f));
        Sheet(row, "Icon", "icon_joystick", 44f, new Vector2(-160f, 0f));
        Label(row, "Label", "Hide Joystick", _semi, 25f, LabelInk,
              TextAlignmentOptions.Left, new Vector2(74f, 0f), new Vector2(392f, 40f));

        RectTransform tall = Press(box, "Row_Abilities", "panel_row", RowW, new Vector2(0f, -65f)).rectTransform;
        Sheet(tall, "Check", "check_on", 52f, new Vector2(-225f, 0f));
        Sheet(tall, "Icon", "icon_ghost", 56f, new Vector2(-160f, 0f));
        Label(tall, "Label", "Hide Abilities When Count is Zero", _medium, 22f, LabelInk,
              TextAlignmentOptions.Left, new Vector2(74f, 0f), new Vector2(392f, 40f));
    }

    private static void BuildAbilitiesPosition(RectTransform popup)
    {
        RectTransform box = Sheet(popup, "AbilitiesPositionBox", "panel_box_abilities", ColumnW,
                                  new Vector2(-ColumnX, ContentBottom + 202f)).rectTransform;
        Heading(box, "ABILITIES POSITION", 166f);

        Card(box, "Card_LeftTop", "Left Top", "(Vertical)", true, new Vector2(-152.5f, 62f), true, true);
        Card(box, "Card_RightTop", "Right Top", "(Vertical)", false, new Vector2(152.5f, 62f), false, true);
        Card(box, "Card_LeftBottom", "Left Bottom", "(Horizontal)", false, new Vector2(-152.5f, -106f), true, false);
        Card(box, "Card_RightBottom", "Right Bottom", "(Horizontal)", false, new Vector2(152.5f, -106f), false, false);
    }

    // One of the four pictures of where the ability buttons could sit: a lit bead
    // for each of the four buttons, on the side and along the edge the card stands
    // for, and a pale disc for the joystick opposite them.
    private static void Card(RectTransform box, string name, string title, string kind, bool picked,
                             Vector2 at, bool left, bool vertical)
    {
        RectTransform card = Press(box, name, picked ? "panel_card_picked" : "panel_card", 285f, at).rectTransform;

        Sheet(card, "Radio", picked ? "radio_on" : "radio_off", 44f, new Vector2(-104f, 44f));
        Label(card, "Title", title, _semi, 22f, LabelInk,
              TextAlignmentOptions.Left, new Vector2(28f, 46f), new Vector2(200f, 32f));
        Label(card, "Kind", kind, _regular, 17f, SubInk,
              TextAlignmentOptions.Left, new Vector2(28f, 18f), new Vector2(200f, 26f));

        Sheet(card, "Disc", "disc", 70f, new Vector2(left ? 52f : -52f, -28f));

        for (int i = 0; i < 4; i++)
        {
            Vector2 spot = vertical
                ? new Vector2(left ? -108f : 108f, 8f - i * 24f)
                : new Vector2((left ? -108f : 36f) + i * 24f, -56f);
            Sheet(card, "Bead" + i, "bead", 21f, spot);
        }
    }

    private static void BuildAudio(RectTransform popup)
    {
        RectTransform box = Sheet(popup, "AudioBox", "panel_box_audio", ColumnW,
                                  new Vector2(ColumnX, ContentTop - 125f)).rectTransform;
        Heading(box, "AUDIO", 89f);

        AudioRow(box, "Row_Music", "Music", "icon_music", 23f);
        AudioRow(box, "Row_Sfx", "SFX", "icon_sfx", -69f);
    }

    private static void AudioRow(RectTransform box, string name, string label, string icon, float y)
    {
        RectTransform row = Sheet(box, name, "panel_row_tall", RowW, new Vector2(0f, y)).rectTransform;
        Sheet(row, "Icon", icon, 56f, new Vector2(-225f, 0f));
        Label(row, "Label", label, _semi, 24f, LabelInk,
              TextAlignmentOptions.Left, new Vector2(-30f, 0f), new Vector2(300f, 40f));

        // The switch is the lit one with the dark one laid over it, so muting is a
        // matter of showing the cover - which is what PauseMenuController already
        // knows how to do.
        RectTransform mounted = Press(row, "Switch", "switch_on", 124f, new Vector2(205f, 0f)).rectTransform;
        Image off = Sheet(mounted, "Off", "switch_off", 124f, Vector2.zero);
        off.gameObject.SetActive(false);
    }

    private static void BuildButtons(RectTransform popup)
    {
        Press(popup, "ResumeButton", "btn_resume", 440f, new Vector2(ColumnX, -40.5f));
        Press(popup, "GameInfoButton", "btn_info", 440f, new Vector2(ColumnX, -175.5f));
        Press(popup, "QuitButton", "btn_quit", 440f, new Vector2(ColumnX, -310.5f));
    }

    // ---- the pieces --------------------------------------------------------

    private static void Heading(RectTransform box, string text, float y)
    {
        Label(box, "Heading", text, _semi, 26f, HeadingInk,
              TextAlignmentOptions.Center, new Vector2(0f, y), new Vector2(420f, 34f));

        float half = box.sizeDelta.x * 0.5f - Pads[Shown[box]];
        RectTransform left = Sheet(box, "RuleLeft", "rule", 170f, new Vector2(-half + 105f, y)).rectTransform;
        RectTransform right = Sheet(box, "RuleRight", "rule", 170f, new Vector2(half - 105f, y)).rectTransform;
        // The hairline is drawn bright at one end; the right-hand one is turned
        // round so both of them fade away from the heading.
        left.localScale = Vector3.one;
        right.localScale = new Vector3(-1f, 1f, 1f);
    }

    // A piece of art at a size quoted for the art itself, so the clear margin
    // around it does not change how big the thing looks or where its middle is.
    private static Image Sheet(Transform parent, string name, string sprite, float artWidth, Vector2 at)
    {
        RectTransform rt = Child(parent, name);
        var image = rt.GetComponent<Image>();
        if (image == null) image = rt.gameObject.AddComponent<Image>();

        Sprite art = Art[sprite];
        image.sprite = art;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;

        int pad = Pads[sprite];
        float k = artWidth / (art.rect.width - pad * 2f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(art.rect.width, art.rect.height) * k;
        rt.anchoredPosition = at;

        Shown[rt] = sprite;
        return image;
    }

    private static Image Press(Transform parent, string name, string sprite, float artWidth, Vector2 at)
    {
        Image image = Sheet(parent, name, sprite, artWidth, at);
        image.raycastTarget = true;

        var button = image.GetComponent<Button>();
        if (button == null) button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colours = button.colors;
        colours.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font,
                                         float size, Color ink, TextAlignmentOptions align,
                                         Vector2 at, Vector2 box)
    {
        RectTransform rt = Child(parent, name);
        var tmp = rt.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();

        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = ink;
        tmp.text = text;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = at;
        return tmp;
    }

    // ---- what the pause menu already knows how to do -----------------------

    private static void Rewire(RectTransform popup)
    {
        WireControls(popup);
        WirePauseMenu(popup);
    }

    // The two checkboxes and the four cards, handed to the component that answers
    // for them along with the pair of sprites each one switches between.
    private static void WireControls(RectTransform popup)
    {
        var ui = popup.GetComponent<SettingsPopupUI>();
        if (ui == null) ui = popup.gameObject.AddComponent<SettingsPopupUI>();

        var so = new SerializedObject(ui);
        so.FindProperty("hideJoystickRow").objectReferenceValue = Find<Button>(popup, "HudOptionsBox/Row_Joystick");
        so.FindProperty("hideJoystickBox").objectReferenceValue = Find<Image>(popup, "HudOptionsBox/Row_Joystick/Check");
        so.FindProperty("hideEmptyAbilitiesRow").objectReferenceValue = Find<Button>(popup, "HudOptionsBox/Row_Abilities");
        so.FindProperty("hideEmptyAbilitiesBox").objectReferenceValue = Find<Image>(popup, "HudOptionsBox/Row_Abilities/Check");
        so.FindProperty("boxChecked").objectReferenceValue = Art["check_on"];
        so.FindProperty("boxUnchecked").objectReferenceValue = Art["check_off"];
        so.FindProperty("cardPicked").objectReferenceValue = Art["panel_card_picked"];
        so.FindProperty("cardUnpicked").objectReferenceValue = Art["panel_card"];
        so.FindProperty("radioPicked").objectReferenceValue = Art["radio_on"];
        so.FindProperty("radioUnpicked").objectReferenceValue = Art["radio_off"];

        // In the order GameSettings.AbilityCorner is in, because that is the index
        // a press on card n turns into.
        string[] cards = { "Card_LeftTop", "Card_RightTop", "Card_LeftBottom", "Card_RightBottom" };
        SerializedProperty buttons = so.FindProperty("cardButtons");
        SerializedProperty panels = so.FindProperty("cardPanels");
        SerializedProperty radios = so.FindProperty("cardRadios");
        buttons.arraySize = panels.arraySize = radios.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
        {
            string path = "AbilitiesPositionBox/" + cards[i];
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = Find<Button>(popup, path);
            panels.GetArrayElementAtIndex(i).objectReferenceValue = Find<Image>(popup, path);
            radios.GetArrayElementAtIndex(i).objectReferenceValue = Find<Image>(popup, path + "/Radio");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WirePauseMenu(RectTransform popup)
    {
        var pause = Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        if (pause == null)
        {
            Debug.LogWarning("[SettingsPopup] No PauseMenuController in the scene; the popup is built but nothing " +
                             "in it answers a press.");
            return;
        }

        var so = new SerializedObject(pause);
        so.FindProperty("resumeButton").objectReferenceValue = Find<Button>(popup, "ResumeButton");
        so.FindProperty("quitButton").objectReferenceValue = Find<Button>(popup, "QuitButton");
        so.FindProperty("musicToggleButton").objectReferenceValue = Find<Button>(popup, "AudioBox/Row_Music/Switch");
        so.FindProperty("sfxToggleButton").objectReferenceValue = Find<Button>(popup, "AudioBox/Row_Sfx/Switch");
        so.FindProperty("musicMuteSlash").objectReferenceValue = Find<Transform>(popup, "AudioBox/Row_Music/Switch/Off").gameObject;
        so.FindProperty("sfxMuteSlash").objectReferenceValue = Find<Transform>(popup, "AudioBox/Row_Sfx/Switch/Off").gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The cross does what Escape does. Toggle is the only way in from outside
        // the controller, and with the popup up it closes it.
        var close = Find<Button>(popup, "CloseButton");
        close.onClick = new Button.ButtonClickedEvent();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, pause.Toggle);
    }

    private static T Find<T>(RectTransform popup, string path) where T : Component
    {
        Transform found = popup.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static RectTransform Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.SetAsLastSibling();
        return rt;
    }

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
