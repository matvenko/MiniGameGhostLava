using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Builds the guide book that "Game Info" on the pause card opens.
//
// The book is a popup in the settings popup's own dress - the same navy body
// and lit rim, drawn by PanelArt to the sheet's colours, and the same title
// plate with "SETTINGS" lifted back out of it the way HudArt lifts painted
// values out of the HUD, the gear sockets either side filled with the sheet's
// yellow info badge. Inside, a rail of chapters down the left and one long
// scrolling page on the right: a card per character, ability and rule, with
// the thing itself on the left and what it does on the right.
//
// The characters on those cards are the real ones, in 3D. Each is copied off
// the board - the player's visual, each enemy kind's template, the friendly
// ghost - stripped down to its looks, and stood on a turntable of its own far
// off the board, in front of a camera and three lights that reach it and
// nothing else (GuideBookShowcase films it). The copies are made here, while
// the scene is at rest, rather than at runtime, when the thing being copied
// might be mid-freeze or halfway through dissolving.
//
// The words and the numbers in them come from GuideBookPages.
//
// Safe to run again: it throws away the book and the stage it built last time
// and builds them fresh, and never discards the scene.
public static class GuideBookBuilder
{
    private const string SheetPath = "Assets/UI/Icons/settings-popup-sprite.png";
    private const string PlatePath = "Assets/UI/Icons/settings.png";
    private const string SettingsDir = "Assets/UI/Icons/Settings/";
    private const string OutDir = "Assets/UI/Icons/GuideBook/";
    private const string FontDir = "Assets/UI/fonts/TMP/";
    private const string CoinIcon = "Assets/UI/Icons/Shop/coin.png";

    private const string BookName = "GuideBook";
    private const string StageName = "GuideBookStage";

    // ---- the stage ----------------------------------------------------------

    // The layer the main menu's portrait studio films on too. Only the stage's
    // cameras see it, and every other camera is told not to.
    private const int StageLayer = 31;
    // The rendering layer the stage's lights and models share ("Light Layer 7").
    // The sun lights "Default" only, so a model in the book looks the same at
    // midnight as at noon, and the book's lights never touch the board.
    private const int StudioLight = 7;
    private static readonly Vector3 StageOrigin = new Vector3(4000f, 0f, 4000f);
    private const float SlotSpacing = 30f;

    // ---- the plate the title comes on ------------------------------------------

    // Where "SETTINGS" is painted on the plate, outline and drop shadow and all,
    // in plate pixels counted from the bottom. Clear of the gear sockets on
    // either side, which is where the colour to fill it with is sampled from.
    private static readonly RectInt PlateWord = new RectInt(488, 244, 1184, 260);
    private const int PlateSample = 24;
    private static readonly Vector2 WordCentre = new Vector2(1080f, 395f);
    private static readonly Vector2 GearLeft = new Vector2(209f, 446f), GearRight = new Vector2(1923f, 446f);
    private const float GearSize = 274f;

    // The yellow info badge on the sheet, in sheet pixels from the bottom, with
    // as much margin as the gap to its neighbours allows.
    private static readonly RectInt InfoBadge = new RectInt(233, 83, 79, 79);
    private const int InfoPad = 5;

    // ---- layout, in canvas units against the 1920x1080 the scaler matches ---

    // Wider than the settings popup, because a page with a picture beside its
    // words wants the room, and a little shorter, so that on a phone wider than
    // the reference - a shorter canvas - it still fits with the plate standing
    // up off its top edge.
    private const float PopupW = 1600f, PopupH = 760f, PopupY = -40f;
    private const float PlateW = 600f;

    private const float ContentTop = 298f, ContentBottom = -352f;
    private const float RailLeft = -770f, RailW = 300f;
    private const float ViewLeft = -444f, ViewRight = 744f;
    private const float BarX = 766f, BarW = 14f;

    private const float CardW = 1150f, CardGap = 26f, ChapterGap = 18f;
    private const float Window = 330f, Inset = 24f;
    private const float TextX = Inset + Window + 36f;
    private const float TextW = CardW - TextX - 34f;

    private const float TabH = 76f, RowH = 50f, TabGap = 8f;

    // ---- the colours the settings sheet is painted in ----------------------

    private static readonly Color BodyTop = Hex(0x1E2C63), BodyBottom = Hex(0x101A44);
    private static readonly Color BodyRim = Hex(0x4E8BE0), BodyGlow = Hex(0x3E7FD8);
    private static readonly Color RowTop = Hex(0x0D2861), RowBottom = Hex(0x08214F), RowRim = Hex(0x4A7CC8);
    private static readonly Color CardTop = Hex(0x152852), CardBottom = Hex(0x071E4B), CardRim = Hex(0x4870B0);
    private static readonly Color BoxRim = Hex(0x2C4C92);
    private static readonly Color PickedRim = Hex(0x01F0EA);

    private static readonly Color HeadingInk = Hex(0x6FB0FF);
    private static readonly Color LabelInk = Color.white;
    private static readonly Color SubInk = Hex(0x93A9CE);
    private static readonly Color TaglineInk = Hex(0xA9C8F5);
    private static readonly Color BodyInk = new Color(0.90f, 0.94f, 1f);
    private static readonly Color Gold = Hex(0xFFD34A);
    private static readonly Color BeadOff = new Color(0.22f, 0.30f, 0.52f, 0.85f);

    private static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, float> Pads = new Dictionary<string, float>();
    private static RectInt _plateBody;
    private static TMP_FontAsset _semi, _medium, _regular;

    private sealed class Slot
    {
        public GameObject Root;
        public Transform Turntable, Subject;
        public Camera Camera;
    }

    [MenuItem("Tools/Build Guide Book")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        Transform pause = canvas.transform.Find("PausePanel");
        Button info = pause != null ? Deep<Button>(pause, "GameInfoButton") : null;
        if (info == null)
        {
            Debug.LogError("[GuideBook] There is no Game Info button on the pause card to open the book from. " +
                           "Build the pause card first (Tools/Build Settings Popup).");
            return;
        }

        if (!LoadFonts() || !MakeArt()) return;
        List<GuideBookPages.Chapter> book = GuideBookPages.Compose();
        if (book == null) return;

        RemovePrevious(canvas);

        var stage = new GameObject(StageName);
        stage.transform.position = StageOrigin;
        stage.layer = StageLayer;

        RectTransform root = BuildBook(canvas, pause, info, book, stage.transform);

        stage.SetActive(false);
        KeepStageOffOtherCameras(stage.transform);

        HudScene.Save(canvas);
        int pages = 0;
        foreach (var chapter in book) pages += chapter.Pages.Count;
        Debug.Log("[GuideBook] Built " + book.Count + " chapters, " + pages + " pages and " +
                  stage.transform.childCount + " 3D models into " + canvas.gameObject.scene.name + ".");
        Selection.activeGameObject = root.gameObject;
    }

    private static void RemovePrevious(Canvas canvas)
    {
        Transform old = canvas.transform.Find(BookName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (GameObject top in canvas.gameObject.scene.GetRootGameObjects())
            if (top.name == StageName) Object.DestroyImmediate(top);
    }

    // ---- art ---------------------------------------------------------------

    private static bool MakeArt()
    {
        Art.Clear();
        Pads.Clear();
        Directory.CreateDirectory(OutDir);

        // Borrowed from the settings popup, which cut them off the same sheet.
        if (!Borrow("close", SettingsDir + "close.png", 10f) ||
            !Borrow("bead", SettingsDir + "bead.png", 0f) ||
            !Borrow("rule", SettingsDir + "rule.png", 0f) ||
            !Borrow("coin", CoinIcon, 0f))
            return false;

        if (!CutInfoBadge() || !CutPlate()) return false;

        Panel("body", PopupW, PopupH, 0f, new PanelArt.Style
        {
            radius = 46f, pad = 30f,
            fillTop = BodyTop, fillBottom = BodyBottom,
            rim = BodyRim, rimWidth = 4f,
            glow = Fade(BodyGlow, 0.5f), glowSize = 26f,
            sheen = Fade(Color.white, 0.10f), sheenHeight = 30f
        });

        Panel("window", Window, Window, 0f, new PanelArt.Style
        {
            radius = 22f, pad = 3f,
            fillTop = Hex(0x0B1738), fillBottom = Hex(0x050C24),
            rim = Fade(BoxRim, 0.9f), rimWidth = 2f,
            sheen = Fade(Color.white, 0.05f), sheenHeight = 14f
        });

        // Everything below stretches, so it is drawn once, small, as a nine-slice.
        Panel("card", 200f, 200f, 44f, new PanelArt.Style
        {
            radius = 24f, pad = 8f,
            fillTop = CardTop, fillBottom = CardBottom,
            rim = CardRim, rimWidth = 3f,
            sheen = Fade(Color.white, 0.06f), sheenHeight = 14f
        });

        // The tabs are the settings cards' rows, and the one being read is lit
        // the way a picked card is - a cyan rim - with a little bloom besides.
        var tab = new PanelArt.Style
        {
            radius = 24f, pad = 16f,
            fillTop = RowTop, fillBottom = RowBottom,
            rim = Fade(RowRim, 0.95f), rimWidth = 3f,
            sheen = Fade(Color.white, 0.07f), sheenHeight = 12f
        };
        Panel("tab_off", 160f, TabH, 46f, tab);
        tab.fillTop = Hex(0x12356E);
        tab.fillBottom = Hex(0x0A2659);
        tab.rim = PickedRim;
        tab.glow = Fade(PickedRim, 0.45f);
        tab.glowSize = 14f;
        Panel("tab_on", 160f, TabH, 46f, tab);

        Panel("row", 64f, 42f, 18f, new PanelArt.Style
        {
            radius = 14f, pad = 2f,
            fillTop = Fade(BodyRim, 0.30f), fillBottom = Fade(BodyRim, 0.22f),
            rim = Fade(BodyRim, 0.55f), rimWidth = 1.5f
        });

        // White, so each use can tint it: a role in its own colour, a fact in
        // the headings' blue, a price in gold.
        Panel("chip", 64f, 34f, 17f, new PanelArt.Style
        {
            radius = 17f, pad = 2f,
            fillTop = Fade(Color.white, 0.16f), fillBottom = Fade(Color.white, 0.12f),
            rim = Fade(Color.white, 0.85f), rimWidth = 2f
        });

        Panel("tip", 96f, 64f, 22f, new PanelArt.Style
        {
            radius = 18f, pad = 2f,
            fillTop = Fade(Hex(0x0C1C4A), 0.9f), fillBottom = Fade(Hex(0x081437), 0.9f),
            rim = Fade(Gold, 0.55f), rimWidth = 2f,
            sheen = Fade(Color.white, 0.04f), sheenHeight = 10f
        });

        Panel("track", BarW, 64f, 7f, new PanelArt.Style
        {
            radius = 7f, pad = 1f,
            fillTop = Fade(Hex(0x0A1438), 0.95f), fillBottom = Fade(Hex(0x0A1438), 0.95f),
            rim = Fade(BoxRim, 0.8f), rimWidth = 1.5f
        });
        Panel("handle", BarW, 64f, 7f, new PanelArt.Style
        {
            radius = 7f, pad = 1f,
            fillTop = Hex(0x6AA8F5), fillBottom = Hex(0x3A6FC4),
            rim = Fade(Hex(0xA9D8FF), 0.9f), rimWidth = 1.5f,
            sheen = Fade(Color.white, 0.15f), sheenHeight = 6f
        });

        Keep("glow", Glow(OutDir + "glow.png", 256), 0f);
        return true;
    }

    private static bool Borrow(string name, string path, float pad)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogError("[GuideBook] No sprite at " + path + ". Build the pause card and the shop first " +
                           "(Tools/Build Settings Popup, Tools/Maze Boo/Build Shop Popup).");
            return false;
        }
        Keep(name, sprite, pad);
        return true;
    }

    private static bool CutInfoBadge()
    {
        Color[] sheet = HudArt.Load(SheetPath, out int w, out int h);
        if (sheet == null) return false;
        if (InfoBadge.xMax > w || InfoBadge.yMax > h)
        {
            Debug.LogError("[GuideBook] The info badge falls outside the " + w + "x" + h + " sheet.");
            return false;
        }
        Sprite badge = HudArt.Write(OutDir + "info_badge.png", HudArt.Crop(sheet, w, InfoBadge),
                                    InfoBadge.width, InfoBadge.height, 1);
        if (badge == null) return false;
        Keep("info", badge, InfoPad);
        return true;
    }

    // The settings title plate with its word painted out, trimmed and hung the
    // way SettingsPopupBuilder hangs it, so the two popups wear the same plate.
    private static bool CutPlate()
    {
        Color[] px = HudArt.Load(PlatePath, out int w, out int h);
        if (px == null || !HudArt.ErasePaintedValue(px, w, h, PlateWord, PlateSample)) return false;

        RectInt body = HudArt.OpaqueBounds(px, w, h, 0.5f);
        if (body.width == 0)
        {
            Debug.LogError("[GuideBook] " + PlatePath + " is empty.");
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

        Keep("plate", plate, (halfW - body.width / 2) / (float)Downscale);
        _plateBody = body;
        return true;

        int Fit(int half) => half / Downscale * Downscale;
    }

    // A panel of the given visible size; the texture is that plus the style's
    // margin for the glow. A border makes it a nine-slice.
    private static void Panel(string name, float w, float h, float border, PanelArt.Style style)
    {
        int pad = Mathf.RoundToInt(style.pad);
        int b = border > 0f ? Mathf.RoundToInt(border) + pad : 0;
        Keep(name, PanelArt.Panel(OutDir + name + ".png", Mathf.RoundToInt(w) + pad * 2,
                                  Mathf.RoundToInt(h) + pad * 2, style, new Vector4(b, b, b, b)), pad);
    }

    // A soft white spot, tinted where it is used: the light behind a model and
    // the pool it stands in.
    private static Sprite Glow(string path, int size)
    {
        var px = new Color[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
            float a = Mathf.Exp(-d * d * 3.2f) * Mathf.Clamp01((1f - d) * 6f);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        return HudArt.Write(path, px, size, size, 1);
    }

    private static void Keep(string name, Sprite sprite, float pad)
    {
        Art[name] = sprite;
        Pads[name] = pad;
    }

    private static bool LoadFonts()
    {
        _semi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "Fredoka-SemiBold SDF.asset");
        _medium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "Fredoka-Medium SDF.asset");
        _regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "Fredoka-Regular SDF.asset");
        if (_semi != null && _medium != null && _regular != null) return true;
        Debug.LogError("[GuideBook] The Fredoka font assets are missing from " + FontDir +
                       ". Tools/Build Settings Popup makes them.");
        return false;
    }

    // ---- the book ------------------------------------------------------------

    private static RectTransform BuildBook(Canvas canvas, Transform pause, Button info,
                                           List<GuideBookPages.Chapter> book, Transform stage)
    {
        // The controller stays on while the book itself is hidden, so it is
        // there to answer the Game Info button from the start.
        RectTransform root = Stretch(canvas.transform, BookName);
        root.SetSiblingIndex(pause.GetSiblingIndex() + 1);
        var ui = root.gameObject.AddComponent<GuideBookUI>();

        RectTransform panel = Stretch(root, "GuideBookPanel");
        var dimmer = panel.gameObject.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.55f);
        dimmer.raycastTarget = true;

        RectTransform popup = MidNode(panel, "Popup", PopupW, PopupH, new Vector2(0f, PopupY));
        // Scaled down to fit a shorter canvas, with margin enough for the plate
        // above it and the offset it hangs at.
        var fit = new SerializedObject(popup.gameObject.AddComponent<UIFitToScreen>());
        fit.FindProperty("maxFill").floatValue = 0.86f;
        fit.ApplyModifiedPropertiesWithoutUndo();
        MidSheet(popup, "Body", "body", PopupW, Vector2.zero).raycastTarget = true;

        float viewW = ViewRight - ViewLeft, viewH = ContentTop - ContentBottom;
        float midY = (ContentTop + ContentBottom) * 0.5f;

        ScrollRect scroll = BuildScroll(popup, viewW, viewH, new Vector2((ViewLeft + ViewRight) * 0.5f, midY),
                                        out RectTransform viewport, out RectTransform content);

        // The pages, top to bottom.
        var headings = new List<RectTransform>();
        var cards = new List<RectTransform>();
        var cardChapters = new List<int>();
        float y = 10f;
        int slotIndex = 0;
        for (int c = 0; c < book.Count; c++)
        {
            if (c > 0) y += ChapterGap;
            headings.Add(BuildHeading(content, book[c], y, viewW, out float headingH));
            y += headingH;

            foreach (GuideBookPages.Page page in book[c].Pages)
            {
                Slot slot = page.Is3D ? BuildSlot(stage, slotIndex++, page) : null;
                RectTransform card = BuildCard(content, page, (viewW - CardW) * 0.5f, y, slot, viewport, scroll);
                cards.Add(card);
                cardChapters.Add(c);
                y += card.sizeDelta.y + CardGap;
            }
        }
        content.sizeDelta = new Vector2(0f, y + 4f);

        RectTransform rail = MidNode(popup, "Rail", RailW, viewH, new Vector2(RailLeft + RailW * 0.5f, midY));
        var tabs = new List<(Button button, Image panel, TextMeshProUGUI label, Image icon)>();
        var rows = new List<(Button button, Image highlight, Image marker, TextMeshProUGUI label)>();
        int p = 0;
        foreach (GuideBookPages.Chapter chapter in book)
        {
            tabs.Add(BuildTab(rail, chapter));
            foreach (GuideBookPages.Page page in chapter.Pages) rows.Add(BuildRow(rail, page, p++));
        }
        Paragraph(rail, "Hint", "Drag a character to turn it around.", _regular, 16f, Fade(SubInk, 0.85f),
                  16f, viewH - 36f, RailW - 32f, TextAlignmentOptions.Top);
        LayOutRail(tabs, rows, cardChapters);

        // Last, so they sit over the body: the plate straddles its top edge, the
        // cross its top-right corner.
        BuildTitle(popup);
        Image close = MidSheet(popup, "CloseButton", "close", 84f, new Vector2(PopupW * 0.5f, PopupH * 0.5f));
        Button closeButton = MakeButton(close);

        var so = new SerializedObject(ui);
        so.FindProperty("bookPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("pausePanel").objectReferenceValue = pause.gameObject;
        so.FindProperty("stage").objectReferenceValue = stage.gameObject;
        so.FindProperty("openButton").objectReferenceValue = info;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("scroll").objectReferenceValue = scroll;

        // The same pieces of HUD the shop puts away while it is up: the book is
        // wider than the pause card, and its close cross and plate would
        // otherwise sit across the wallet and the level badge.
        var shop = Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
        SerializedProperty shopHides = shop != null
            ? new SerializedObject(shop).FindProperty("hudElementsToHide")
            : null;
        SerializedProperty hides = so.FindProperty("hudElementsToHide");
        hides.arraySize = shopHides != null ? shopHides.arraySize : 0;
        for (int i = 0; i < hides.arraySize; i++)
            hides.GetArrayElementAtIndex(i).objectReferenceValue = shopHides.GetArrayElementAtIndex(i).objectReferenceValue;

        so.FindProperty("tabPicked").objectReferenceValue = Art["tab_on"];
        so.FindProperty("tabUnpicked").objectReferenceValue = Art["tab_off"];
        so.FindProperty("tabHeight").floatValue = TabH;
        so.FindProperty("rowHeight").floatValue = RowH;
        so.FindProperty("tabGap").floatValue = TabGap;
        so.FindProperty("inkPicked").colorValue = LabelInk;
        so.FindProperty("inkUnpicked").colorValue = SubInk;
        so.FindProperty("markerPicked").colorValue = PickedRim;
        so.FindProperty("markerUnpicked").colorValue = BeadOff;

        SerializedProperty chapters = so.FindProperty("chapters");
        chapters.arraySize = book.Count;
        for (int c = 0; c < book.Count; c++)
        {
            SerializedProperty entry = chapters.GetArrayElementAtIndex(c);
            entry.FindPropertyRelative("tab").objectReferenceValue = tabs[c].button;
            entry.FindPropertyRelative("tabPanel").objectReferenceValue = tabs[c].panel;
            entry.FindPropertyRelative("tabLabel").objectReferenceValue = tabs[c].label;
            entry.FindPropertyRelative("tabIcon").objectReferenceValue = tabs[c].icon;
            entry.FindPropertyRelative("heading").objectReferenceValue = headings[c];
        }

        SerializedProperty pages = so.FindProperty("pages");
        pages.arraySize = cards.Count;
        for (int i = 0; i < cards.Count; i++)
        {
            SerializedProperty entry = pages.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("card").objectReferenceValue = cards[i];
            entry.FindPropertyRelative("chapter").intValue = cardChapters[i];
            entry.FindPropertyRelative("row").objectReferenceValue = rows[i].button;
            entry.FindPropertyRelative("rowHighlight").objectReferenceValue = rows[i].highlight;
            entry.FindPropertyRelative("rowMarker").objectReferenceValue = rows[i].marker;
            entry.FindPropertyRelative("rowLabel").objectReferenceValue = rows[i].label;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);
        return root;
    }

    private static ScrollRect BuildScroll(RectTransform popup, float w, float h, Vector2 at,
                                          out RectTransform viewport, out RectTransform content)
    {
        RectTransform pages = MidNode(popup, "Pages", w, h, at);
        var scroll = pages.gameObject.AddComponent<ScrollRect>();

        viewport = Stretch(pages, "Viewport");
        // Clear but hit, so a drag that starts in the gap between two cards
        // still scrolls.
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        var mask = viewport.gameObject.AddComponent<RectMask2D>();
        // The page fades out under the top and bottom edges rather than being
        // sliced off by them.
        mask.softness = new Vector2Int(0, 22);

        var go = new GameObject("Content", typeof(RectTransform));
        content = (RectTransform)go.transform;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        RectTransform bar = MidNode(popup, "Scrollbar", BarW, h, new Vector2(BarX, at.y));
        Image track = bar.gameObject.AddComponent<Image>();
        Dress(track, "track");
        RectTransform area = Stretch(bar, "Sliding Area");
        RectTransform handle = Stretch(area, "Handle");
        Image handleImage = handle.gameObject.AddComponent<Image>();
        Dress(handleImage, "handle");
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var colours = scrollbar.colors;
        colours.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colours.fadeDuration = 0.06f;
        scrollbar.colors = colours;

        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.1f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return scroll;
    }

    // "GUIDE BOOK" on the settings plate, with the info badge where the gears were.
    private static void BuildTitle(RectTransform popup)
    {
        var top = new Vector2(0f, PopupH * 0.5f);
        MidSheet(popup, "TitlePlate", "plate", PlateW, top);

        float k = PlateW / _plateBody.width;
        Vector2 centre = _plateBody.center;
        MidSheet(popup, "BadgeLeft", "info", GearSize * k * 1.06f, top + (GearLeft - centre) * k);
        MidSheet(popup, "BadgeRight", "info", GearSize * k * 1.06f, top + (GearRight - centre) * k);

        RectTransform box = MidNode(popup, "Title", 360f, 84f, top + (WordCentre - centre) * k);
        var title = box.gameObject.AddComponent<TextMeshProUGUI>();
        title.font = _semi;
        title.text = "GUIDE BOOK";
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMax = 54f;
        title.fontSizeMin = 34f;
        title.characterSpacing = 2f;
        title.raycastTarget = false;
        HudArt.StyleValue(title, Hex(0x0A1C57));
    }

    // ---- a chapter heading ---------------------------------------------------

    private static RectTransform BuildHeading(RectTransform content, GuideBookPages.Chapter chapter, float top,
                                              float width, out float height)
    {
        RectTransform node = Node(content, "Chapter " + chapter.Title, 0f, top, width, 100f);

        TextMeshProUGUI title = Label(node, "Title", chapter.Title, _semi, 30f, HeadingInk,
                                      0f, 10f, width, 40f, TextAlignmentOptions.Center, 4f);
        float titleW = title.GetPreferredValues(chapter.Title).x;
        float mid = 10f + 20f;

        // The hairline is drawn bright at one end; the right-hand one is turned
        // round so both of them fade away from the heading.
        Place(node, "RuleLeft", "rule", 170f, new Vector2(width * 0.5f - titleW * 0.5f - 22f - 85f, mid));
        Image right = Place(node, "RuleRight", "rule", 170f, new Vector2(width * 0.5f + titleW * 0.5f + 22f + 85f, mid));
        right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        TextMeshProUGUI intro = Paragraph(node, "Intro", chapter.Intro, _regular, 19f, SubInk,
                                          110f, 56f, width - 220f, TextAlignmentOptions.Top);
        height = 56f + intro.rectTransform.sizeDelta.y + 22f;
        node.sizeDelta = new Vector2(width, height);
        return node;
    }

    // ---- a page ------------------------------------------------------------

    private static RectTransform BuildCard(RectTransform content, GuideBookPages.Page page, float x, float top,
                                           Slot slot, RectTransform viewport, ScrollRect scroll)
    {
        RectTransform card = Node(content, "Page " + page.Name, x, top, CardW, 400f);

        // Behind everything, and hit, so a drag on the words scrolls the page.
        Image panel = Box(card, "Panel", "card", 0f, 0f, CardW, 400f);
        panel.raycastTarget = true;
        BuildPicture(card, page, slot, viewport, scroll);

        float y = 26f;
        Chip(card, "Role", page.Role, _semi, 15f, page.RoleColour, Color.Lerp(page.RoleColour, Color.white, 0.5f),
             TextX, y, 32f, false, 3f);
        y += 32f + 10f;

        Label(card, "Name", page.Name, _semi, 40f, LabelInk, TextX, y, TextW, 48f, TextAlignmentOptions.Left);
        y += 52f;

        y += Paragraph(card, "Tagline", page.Tagline, _medium, 21f, TaglineInk, TextX, y, TextW,
                       TextAlignmentOptions.TopLeft).rectTransform.sizeDelta.y + 12f;
        y += Paragraph(card, "Body", page.Body, _regular, 20f, BodyInk, TextX, y, TextW,
                       TextAlignmentOptions.TopLeft).rectTransform.sizeDelta.y + 18f;

        if (page.Meters.Count > 0) y = Meters(card, page, y);
        if (page.Facts.Count > 0) y = Facts(card, page, y);
        if (!string.IsNullOrEmpty(page.Tip)) y = Tip(card, page, y);

        float height = Mathf.Max(y + 26f, Inset * 2f + Window);
        card.sizeDelta = new Vector2(CardW, height);
        Resize(panel, "card", CardW, height);
        return card;
    }

    // The thing itself: a 3D window onto its slot on the stage, or its icon.
    private static void BuildPicture(RectTransform card, GuideBookPages.Page page, Slot slot,
                                     RectTransform viewport, ScrollRect scroll)
    {
        RectTransform frame = Node(card, "Picture", Inset, Inset, Window, Window);
        Box(frame, "Frame", "window", 0f, 0f, Window, Window);
        Place(frame, "Glow", "glow", 300f, new Vector2(Window * 0.5f, Window * 0.46f)).color = Fade(page.Glow, 0.30f);

        if (slot != null)
        {
            Image floor = Place(frame, "Floor", "glow", 250f, new Vector2(Window * 0.5f, Window * 0.80f));
            floor.preserveAspect = false;
            floor.rectTransform.sizeDelta = new Vector2(250f, 54f);
            floor.color = Fade(page.Glow, 0.45f);

            RectTransform shot = Node(frame, "Model", 4f, 4f, Window - 8f, Window - 8f);
            var raw = shot.gameObject.AddComponent<RawImage>();
            raw.color = Color.clear;
            raw.raycastTarget = true;
            var showcase = shot.gameObject.AddComponent<GuideBookShowcase>();

            var so = new SerializedObject(showcase);
            so.FindProperty("slot").objectReferenceValue = slot.Root;
            so.FindProperty("stageCamera").objectReferenceValue = slot.Camera;
            so.FindProperty("turntable").objectReferenceValue = slot.Turntable;
            so.FindProperty("subject").objectReferenceValue = slot.Subject;
            so.FindProperty("viewport").objectReferenceValue = viewport;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("elevation").floatValue = page.Elevation;
            so.FindProperty("zoom").floatValue = page.Zoom;
            so.ApplyModifiedPropertiesWithoutUndo();
            return;
        }

        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(page.IconPath);
        if (icon == null)
        {
            Debug.LogWarning("[GuideBook] No icon at " + page.IconPath + " for " + page.Name + ".");
            return;
        }
        Place(frame, "Icon", icon, 0f, page.IconWidth, new Vector2(Window * 0.5f, Window * 0.48f));
    }

    // Three little rows of five beads: how fast, how clever, how dangerous.
    private static float Meters(RectTransform card, GuideBookPages.Page page, float y)
    {
        for (int i = 0; i < page.Meters.Count; i++)
        {
            float x = TextX + i * 232f;
            Label(card, "Meter " + page.Meters[i].Key, page.Meters[i].Key, _semi, 14f, SubInk,
                  x, y, 200f, 20f, TextAlignmentOptions.Left, 3f);
            for (int b = 0; b < 5; b++)
            {
                Image bead = Place(card, "Bead " + page.Meters[i].Key + " " + b, "bead", 21f,
                                   new Vector2(x + 11f + b * 27f, y + 36f));
                bead.color = b < page.Meters[i].Value ? Color.white : BeadOff;
            }
        }
        return y + 36f + 11f + 18f;
    }

    // The facts as a run of chips, wrapping onto a second line if they must.
    private static float Facts(RectTransform card, GuideBookPages.Page page, float y)
    {
        const float H = 36f, Gap = 10f;
        float x = TextX;
        for (int i = 0; i < page.Facts.Count; i++)
        {
            GuideBookPages.Fact fact = page.Facts[i];
            float w = Chip(card, "Fact " + i, fact.Text, _medium, 17f, fact.Price ? Gold : HeadingInk,
                           fact.Price ? Gold : LabelInk, x, y, H, fact.Price, 0f);
            if (x > TextX && x + w > TextX + TextW)
            {
                // Did not fit: the chip goes to the start of the next line.
                x = TextX;
                y += H + Gap;
                MoveChip(card, "Fact " + i, x, y);
            }
            x += w + Gap;
        }
        return y + H + 18f;
    }

    private static float Tip(RectTransform card, GuideBookPages.Page page, float y)
    {
        Image box = Box(card, "Tip", "tip", TextX, y, TextW, 58f);
        // TIP stands in a column of its own, so a tip that runs onto a second
        // line lines up under its own first word rather than under TIP.
        TextMeshProUGUI words = Paragraph(card, "TipText", page.Tip, _regular, 18f, BodyInk,
                                          TextX + 108f, y, TextW - 126f, TextAlignmentOptions.TopLeft);
        float textH = words.rectTransform.sizeDelta.y;
        float boxH = Mathf.Max(58f, textH + 24f);
        Resize(box, "tip", TextW, boxH);
        float top = y + (boxH - textH) * 0.5f;
        words.rectTransform.anchoredPosition = new Vector2(TextX + 108f, -top);
        Label(card, "TipLabel", "TIP", _semi, 18f, Gold, TextX + 60f, top, 46f, 24f, TextAlignmentOptions.TopLeft, 1f);
        Place(card, "TipBadge", "info", 32f, new Vector2(TextX + 32f, y + boxH * 0.5f));
        return y + boxH;
    }

    // A pill with a word in it, sized to the word. Returns how wide it came out.
    private static float Chip(RectTransform card, string name, string text, TMP_FontAsset font, float size,
                              Color tint, Color ink, float x, float y, float h, bool coin, float spacing)
    {
        Image box = Box(card, name, "chip", x, y, 60f, h);
        box.color = tint;
        TextMeshProUGUI label = Label(box.rectTransform, "Label", text, font, size, ink, 0f, 0f, 10f, h,
                                      TextAlignmentOptions.Center, spacing);
        float lead = coin ? 30f : 0f;
        float w = Mathf.Ceil(label.GetPreferredValues(text).x) + 30f + lead;
        Resize(box, "chip", w, h);

        float pad = Pads["chip"];
        label.rectTransform.anchoredPosition = new Vector2(pad + 15f + lead, -pad);
        label.rectTransform.sizeDelta = new Vector2(w - 30f - lead, h);
        if (coin) Place(box.rectTransform, "Coin", "coin", 24f, new Vector2(pad + 12f + 12f, pad + h * 0.5f));
        return w;
    }

    private static void MoveChip(RectTransform card, string name, float x, float y)
    {
        var chip = (RectTransform)card.Find(name);
        float pad = Pads["chip"];
        chip.anchoredPosition = new Vector2(x - pad, -(y - pad));
    }

    // ---- the rail --------------------------------------------------------------

    private static (Button, Image, TextMeshProUGUI, Image) BuildTab(RectTransform rail, GuideBookPages.Chapter chapter)
    {
        RectTransform tab = Node(rail, "Tab " + chapter.Title, 0f, 0f, RailW, TabH);
        Image panel = Box(tab, "Panel", "tab_off", 0f, 0f, RailW, TabH);
        panel.raycastTarget = true;
        // The button is the tab itself rather than the panel drawn on it, so
        // moving the tab down the rail takes its icon and its word along too.
        Button button = tab.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        var colours = button.colors;
        colours.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;

        Image icon = null;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(chapter.IconPath);
        if (sprite != null) icon = Place(tab, "Icon", sprite, chapter.IconPad, 40f, new Vector2(42f, TabH * 0.5f));

        TextMeshProUGUI label = Label(tab, "Label", chapter.Title, _semi, 21f, SubInk,
                                      76f, 0f, RailW - 88f, TabH, TextAlignmentOptions.Left, 2f);
        return (button, panel, label, icon);
    }

    private static (Button, Image, Image, TextMeshProUGUI) BuildRow(RectTransform rail, GuideBookPages.Page page, int index)
    {
        RectTransform row = Node(rail, "Row " + page.Name, 0f, 0f, RailW, RowH);
        // The whole line is the press target, not the word on it.
        var hit = row.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        Image highlight = Box(row, "Highlight", "row", 8f, 4f, RailW - 16f, RowH - 8f);
        highlight.enabled = index == 0;
        Image marker = Place(row, "Marker", "bead", 16f, new Vector2(34f, RowH * 0.5f));
        marker.color = index == 0 ? PickedRim : BeadOff;
        TextMeshProUGUI label = Label(row, "Label", page.Name, _medium, 19f, index == 0 ? LabelInk : SubInk,
                                      54f, 0f, RailW - 62f, RowH, TextAlignmentOptions.Left);
        return (button, highlight, marker, label);
    }

    // How the rail stands before the book is first opened: the first chapter
    // open and its first page lit. GuideBookUI.LayOutRail does the same from
    // then on.
    private static void LayOutRail(List<(Button button, Image panel, TextMeshProUGUI label, Image icon)> tabs,
                                   List<(Button button, Image highlight, Image marker, TextMeshProUGUI label)> rows,
                                   List<int> rowChapters)
    {
        float y = 0f;
        for (int c = 0; c < tabs.Count; c++)
        {
            ((RectTransform)tabs[c].button.transform).anchoredPosition = new Vector2(0f, -y);
            tabs[c].panel.sprite = Art[c == 0 ? "tab_on" : "tab_off"];
            tabs[c].label.color = c == 0 ? LabelInk : SubInk;
            if (tabs[c].icon != null) tabs[c].icon.color = new Color(1f, 1f, 1f, c == 0 ? 1f : 0.55f);
            y += TabH + TabGap;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rowChapters[i] != c) continue;
                rows[i].button.gameObject.SetActive(c == 0);
                if (c != 0) continue;
                ((RectTransform)rows[i].button.transform).anchoredPosition = new Vector2(0f, -y);
                y += RowH;
            }
            if (c == 0) y += TabGap * 2f;
        }
    }

    // ---- the stage -----------------------------------------------------------

    private static Slot BuildSlot(Transform stage, int index, GuideBookPages.Page page)
    {
        var root = new GameObject("Slot " + index.ToString("00") + " " + page.Name);
        root.transform.SetParent(stage, false);
        root.transform.localPosition = new Vector3(index * SlotSpacing, 0f, 0f);

        var turntable = new GameObject("Turntable").transform;
        turntable.SetParent(root.transform, false);

        GameObject subject = page.DioramaLava != null ? Diorama(page) : Copy(page);
        subject.transform.SetParent(turntable, false);
        subject.transform.localPosition = Vector3.zero;
        StripToLooks(subject);

        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.localPosition = new Vector3(0f, 2f, 6f);
        camera.transform.LookAt(root.transform.position + Vector3.up * 0.5f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << StageLayer;
        camera.fieldOfView = 24f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 40f;
        camera.depth = -50f;
        // An 8-bit target: no HDR, and no post-processing, which would also
        // throw away the transparent background the window shows through.
        camera.allowHDR = false;
        camera.allowMSAA = true;
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = false;

        // Key from the front left, a cool fill from the right, and a rim from
        // behind in the page's own colour to lift the silhouette off the dark.
        Lamp(root.transform, "Key", new Vector3(-2.4f, 4.2f, 3.4f), new Color(1f, 0.96f, 0.90f), 34f);
        Lamp(root.transform, "Fill", new Vector3(3f, 1.6f, 3.2f), new Color(0.72f, 0.82f, 1f), 12f);
        Lamp(root.transform, "Rim", new Vector3(0.4f, 3f, -3.6f), Color.Lerp(Color.white, page.Glow, 0.5f), 26f);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StageLayer;
        root.SetActive(false);
        return new Slot { Root = root, Turntable = turntable, Subject = subject.transform, Camera = camera };
    }

    // A copy of the look, facing the camera: turned the way it faces its own
    // gameplay root, which is the way it walks.
    private static GameObject Copy(GuideBookPages.Page page)
    {
        GameObject copy;
        Quaternion facing;
        Vector3 scale;

        if (EditorUtility.IsPersistent(page.Model))
        {
            copy = (GameObject)PrefabUtility.InstantiatePrefab(page.Model);
            facing = page.ModelTilt == Quaternion.identity ? page.Model.transform.localRotation : page.ModelTilt;
            scale = page.Model.transform.localScale;
        }
        else
        {
            copy = Object.Instantiate(page.Model);
            copy.SetActive(true);
            Transform frame = page.ModelFrame != null ? page.ModelFrame : page.Model.transform;
            facing = Quaternion.Inverse(frame.rotation) * page.Model.transform.rotation;
            scale = page.Model.transform.lossyScale;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(copy))
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(copy),
                                               PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        copy.name = page.Model.name;
        copy.transform.localRotation = facing;
        copy.transform.localScale = scale;
        return copy;
    }

    // A corner of the board in real tiles: a channel of lava cut through grass.
    private static GameObject Diorama(GuideBookPages.Page page)
    {
        var root = new GameObject("Lava corner");
        bool[,] lava =
        {
            { false, true, false },
            { false, true, true },
            { true, true, false },
        };
        for (int x = 0; x < 3; x++)
        for (int z = 0; z < 3; z++)
        {
            bool burning = lava[z, x];
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = burning ? "Lava tile" : "Grass tile";
            tile.transform.SetParent(root.transform, false);
            // Lava sits a little below the grass, the way the board lays it.
            tile.transform.localPosition = new Vector3(x - 1f, burning ? -0.37f : -0.25f, 1f - z);
            tile.transform.localScale = new Vector3(1f, 0.5f, 1f);
            tile.GetComponent<Renderer>().sharedMaterial = burning ? page.DioramaLava : page.DioramaGround;
        }
        return root;
    }

    // Everything that plays the game comes off: a copy with a chaser on it would
    // hunt, a coin would be counted towards the level, a collider could be
    // walked into. What stays is the look - renderers, the rig and its Animator,
    // and the two components that paint it.
    private static void StripToLooks(GameObject subject)
    {
        foreach (Transform t in subject.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            t.gameObject.tag = "Untagged";
        }

        foreach (var script in subject.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(script is WardenAppearance) && !(script is SpectralHunterPalette))
                Object.DestroyImmediate(script);
        foreach (var c in subject.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var b in subject.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(b);
        foreach (var l in subject.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        foreach (var a in subject.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        foreach (var c in subject.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);

        foreach (var r in subject.GetComponentsInChildren<Renderer>(true))
        {
            r.renderingLayerMask = 1u << StudioLight;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            // Framed off live bounds, which a skinned mesh only keeps honest
            // when it is told to.
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }

        // The book is read with the game paused.
        foreach (var animator in subject.GetComponentsInChildren<Animator>(true))
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }

        // A hunter's palette finds the hunter it paints by the chaser above it,
        // which the copy no longer has.
        foreach (var palette in subject.GetComponentsInChildren<SpectralHunterPalette>(true))
        {
            var so = new SerializedObject(palette);
            so.FindProperty("paintRoot").objectReferenceValue = subject.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void Lamp(Transform slot, string name, Vector3 at, Color colour, float intensity)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(slot, false);
        light.transform.localPosition = at;
        light.type = LightType.Point;
        light.range = 16f;
        light.intensity = intensity;
        light.color = colour;
        light.shadows = LightShadows.None;
        // URP reads a light's rendering layers off its additional data, which
        // hands them on to the light; written on the light alone they are lost.
        light.GetUniversalAdditionalLightData().renderingLayers = (RenderingLayerMask)(1u << StudioLight);
        light.cullingMask = 1 << StageLayer;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    // The stage is thousands of units off the board and no game camera will
    // ever point at it, but none of them is left able to see it either.
    private static void KeepStageOffOtherCameras(Transform stage)
    {
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.transform.IsChildOf(stage) || (camera.cullingMask & (1 << StageLayer)) == 0) continue;
            camera.cullingMask &= ~(1 << StageLayer);
            EditorUtility.SetDirty(camera);
        }
    }

    // ---- the pieces --------------------------------------------------------

    // Children placed from the parent's top-left corner, x to the right and y
    // down the page - which is how a page is read, and how its text flows.
    private static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static RectTransform Stretch(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // Children placed from the parent's middle, the way the settings popup
    // places everything on itself.
    private static RectTransform MidNode(Transform parent, string name, float w, float h, Vector2 at)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = at;
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    // A piece of art at a size quoted for the art itself, so the clear margin
    // around it does not change how big it looks or where its middle is.
    private static Image MidSheet(Transform parent, string name, string art, float artWidth, Vector2 at)
    {
        Sprite sprite = Art[art];
        float k = artWidth / (sprite.rect.width - Pads[art] * 2f);
        RectTransform rt = MidNode(parent, name, sprite.rect.width * k, sprite.rect.height * k, at);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Image Place(Transform parent, string name, string art, float artWidth, Vector2 centre) =>
        Place(parent, name, Art[art], Pads[art], artWidth, centre);

    private static Image Place(Transform parent, string name, Sprite sprite, float pad, float artWidth, Vector2 centre)
    {
        float k = artWidth / (sprite.rect.width - pad * 2f);
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(centre.x, -centre.y);
        rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height) * k;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // A panel whose visible box is x, y, w, h; the image reaches past that by
    // the art's margin, so the glow has somewhere to go.
    private static Image Box(Transform parent, string name, string art, float x, float y, float w, float h)
    {
        float pad = Pads[art];
        RectTransform rt = Node(parent, name, x - pad, y - pad, w + pad * 2f, h + pad * 2f);
        var image = rt.gameObject.AddComponent<Image>();
        Dress(image, art);
        image.raycastTarget = false;
        return image;
    }

    private static void Resize(Image box, string art, float w, float h)
    {
        float pad = Pads[art];
        box.rectTransform.sizeDelta = new Vector2(w + pad * 2f, h + pad * 2f);
    }

    private static void Dress(Image image, string art)
    {
        Sprite sprite = Art[art];
        image.sprite = sprite;
        image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.white;
    }

    private static Button MakeButton(Image image)
    {
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colours = button.colors;
        colours.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        return button;
    }

    // One line in a fixed box.
    private static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size,
                                         Color ink, float x, float y, float w, float h, TextAlignmentOptions align,
                                         float spacing = 0f)
    {
        RectTransform rt = Node(parent, name, x, y, w, h);
        TextMeshProUGUI tmp = Dress(rt.gameObject.AddComponent<TextMeshProUGUI>(), text, font, size, ink, align);
        tmp.characterSpacing = spacing;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // Wrapped text, its box made as tall as the words need.
    private static TextMeshProUGUI Paragraph(Transform parent, string name, string text, TMP_FontAsset font, float size,
                                             Color ink, float x, float y, float w, TextAlignmentOptions align)
    {
        RectTransform rt = Node(parent, name, x, y, w, 10f);
        TextMeshProUGUI tmp = Dress(rt.gameObject.AddComponent<TextMeshProUGUI>(), text, font, size, ink, align);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 4f;
        rt.sizeDelta = new Vector2(w, Mathf.Ceil(tmp.GetPreferredValues(text, w, 0f).y));
        return tmp;
    }

    private static TextMeshProUGUI Dress(TextMeshProUGUI tmp, string text, TMP_FontAsset font, float size, Color ink,
                                         TextAlignmentOptions align)
    {
        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = ink;
        tmp.alignment = align;
        tmp.richText = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }

    private static T Deep<T>(Transform root, string name) where T : Component
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.GetComponent<T>();
        return null;
    }

    private static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
