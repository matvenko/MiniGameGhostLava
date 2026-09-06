using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Rebuilds the Shop popup to the artwork in Assets/UI/shop/.
//
// The sheet the shop was drawn on is a finished picture, not a kit: the frame
// and the row boxes carry their own lettering, prices and counts, so they cannot
// be cut out and re-lettered. What can be cut is everything the picture holds
// only once - the SHOP plate, the Close button, the green price button and the
// five ability tiles - and that is what this takes. The empty boxes behind them
// are drawn here in the sheet's own colours, the way PanelArt draws the settings
// boxes, so the live text stays sharp at any size and the game can change it.
//
// The green button was drawn with 1500 already on it. The digits are painted out
// by copying a clean column of green across them, the coin is kept, and the
// button becomes a nine-slice with the coin locked into its left cap - so every
// price is the artist's button with the game's own number on it.
//
// Safe to run twice: it rewrites its sprites, rebuilds the card's contents and
// points ShopUIController back at the new objects. Everything the controller
// knows that is not on the card - the pause card, the HUD it hides, the two ways
// in - is left exactly as it was.
public static class ShopPopupBuilder
{
    private const string ArtDir = "Assets/UI/shop/";
    private const string OutDir = "Assets/UI/Icons/Shop/";
    private const string ScenePath = "Assets/LavaScene.unity";

    // ---- the card ----------------------------------------------------------

    private const float CardW = 700f, CardH = 920f;
    private const float RowW = 630f, RowH = 118f;
    private const float RowStep = 124.5f, FirstRowY = 240f;

    private struct Item
    {
        public string Key, Name, Description;
        public RectInt Tile; // where the tile sits on the sheet, top-left origin

        public Item(string key, string name, string description, int tileTop)
        {
            Key = key;
            Name = name;
            Description = description;
            Tile = new RectInt(77, tileTop, 164, 164);
        }
    }

    // The descriptions are the mock-up's own wording.
    private static readonly Item[] Items =
    {
        new Item("ExtraLife", "Extra Life", "Increase your maximum\nlives by 1.", 261),
        new Item("Trap", "Trap", "Place a trap to immobilize\nenemies for a few seconds.", 456),
        new Item("Freeze", "Freeze", "Freeze all enemies\nfor a few seconds.", 651),
        new Item("Teleport", "Teleport", "Teleport to a random\nposition on the map.", 846),
        new Item("Shield", "Shield", "Gain temporary invulnerability\nfor a few seconds.", 1041),
    };

    // ---- the sheet's colours ----------------------------------------------

    private static readonly Color PanelFillTop = new Color32(0x22, 0x2E, 0x63, 0xFF);
    private static readonly Color PanelFillBottom = new Color32(0x16, 0x1F, 0x48, 0xFF);
    private static readonly Color PanelLine = new Color32(0x35, 0x5A, 0xB8, 0xFF);
    private static readonly Color PanelEdge = new Color32(0x07, 0x0D, 0x22, 0xFF);
    private static readonly Color RowFill = new Color32(0x15, 0x23, 0x4A, 0xFF);
    private static readonly Color RowLine = new Color32(0x2B, 0x40, 0x7C, 0xFF);
    private static readonly Color PillFill = new Color32(0x0E, 0x17, 0x33, 0xFF);
    private static readonly Color Ink = Color.white;
    private static readonly Color Muted = new Color32(0xAF, 0xC0, 0xE6, 0xFF);
    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3A, 0xFF);

    [MenuItem("Tools/Pac Ghost/Build Shop Popup")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Leave play mode before rebuilding the shop popup.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath);

        Directory.CreateDirectory(OutDir);
        var art = CutArtwork();
        var card = FindCard();
        if (card == null)
        {
            Debug.LogError("ShopPanel/Card not found in " + ScenePath);
            return;
        }
        BuildCard(card, art);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Shop popup rebuilt from " + ArtDir);
    }

    // ---- artwork -----------------------------------------------------------

    private class Art
    {
        public Sprite Panel, Row, Pill, Header, Close, Price, Coin;
        public Sprite[] Tiles;
        public TMP_FontAsset Font;
    }

    private static Art CutArtwork()
    {
        var art = new Art();
        var sheet = Load(ArtDir + "shop.png");

        // Drawn: the sheet has no empty boxes to cut.
        art.Panel = SaveDrawn("panel", RoundedBox(256, 256, 88, PanelFillTop, PanelFillBottom, PanelLine, 5, PanelEdge, 7), 110, 2f);
        art.Row = SaveDrawn("row", RoundedBox(128, 128, 46, RowFill, RowFill, RowLine, 3, default, 0), 56, 2f);
        art.Pill = SaveDrawn("pill", RoundedBox(112, 112, 50, PillFill, PillFill, RowLine, 3, default, 0), 52, 2f);

        // Cut: the pieces the picture holds only once.
        art.Header = SaveCut("header", Downscale(Trim(Load(ArtDir + "shop-btn.png")), 512), 0, 340f);
        art.Close = SaveCut("close", Downscale(Trim(Load(ArtDir + "close-btn.png")), 512), 0, 300f);

        // The coin has to sit in the left cap of the nine-slice, or stretching the
        // button to a wider price would stretch the coin with it.
        var price = Downscale(PaintOutDigits(Trim(Load(ArtDir + "price-btn.png"))), 512);
        art.Price = SaveCut("price", price, 0, 187f,
            new Vector4(Mathf.RoundToInt(price.width * .46f), Mathf.RoundToInt(price.height * .42f),
                Mathf.RoundToInt(price.width * .12f), Mathf.RoundToInt(price.height * .42f)));

        // The wallet coin sits on the sheet's own navy, so a round cut of it
        // carries no fringe that the pill behind it will not swallow.
        art.Coin = SaveCut("coin", RoundCut(sheet, new RectInt(103, 158, 74, 74)), 0, 38f);

        art.Tiles = new Sprite[Items.Length];
        for (int i = 0; i < Items.Length; i++)
            art.Tiles[i] = SaveCut("tile_" + Items[i].Key.ToLowerInvariant(), RoundedCut(sheet, Items[i].Tile, 34), 0, 105f);

        Object.DestroyImmediate(sheet);
        art.Font = FindFont();
        return art;
    }

    private static Texture2D Load(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(path));
        return texture;
    }

    // Everything the artist painted outside the shape - glow, shadow - is
    // transparent, so the drawn size is whatever is left once that is dropped.
    private static Texture2D Trim(Texture2D source)
    {
        var pixels = source.GetPixels32();
        int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
                if (pixels[y * source.width + x].a > 6)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < minX) return source;
        var trimmed = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
        trimmed.SetPixels(source.GetPixels(minX, minY, trimmed.width, trimmed.height));
        trimmed.Apply();
        Object.DestroyImmediate(source);
        return trimmed;
    }

    private static Texture2D Downscale(Texture2D source, int maxWidth)
    {
        if (source.width <= maxWidth) return source;
        int width = maxWidth, height = Mathf.Max(1, Mathf.RoundToInt(source.height * (float)maxWidth / source.width));
        var buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        source.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, buffer);
        var previous = RenderTexture.active;
        RenderTexture.active = buffer;
        var small = new Texture2D(width, height, TextureFormat.RGBA32, false);
        small.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        small.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(buffer);
        Object.DestroyImmediate(source);
        return small;
    }

    // The price on the button is white where the button is green, so the digits
    // give themselves away by colour. They are painted out with a column of clean
    // green from the gap between the coin and the first digit, copied whole so
    // the gloss along the top and the shading along the bottom come with it.
    //
    // The coin carries a white sparkle of its own, so the search for digits only
    // starts past the half of the button the coin can reach.
    private static Texture2D PaintOutDigits(Texture2D button)
    {
        var pixels = button.GetPixels32();
        int band0 = Mathf.RoundToInt(button.height * .34f), band1 = Mathf.RoundToInt(button.height * .74f);
        int first = -1, last = -1;
        for (int x = Mathf.RoundToInt(button.width * .40f); x < button.width; x++)
        {
            int white = 0;
            for (int y = band0; y < band1; y++)
            {
                var p = pixels[y * button.width + x];
                if (p.a > 200 && p.r > 190 && p.g > 190 && p.b > 190) white++;
            }
            if (white < 6) continue;
            if (first < 0) first = x;
            last = x;
        }
        if (first < 0) return button;

        int coinEnd = 0;
        for (int x = 0; x < first; x++)
            for (int y = band0; y < band1; y++)
            {
                var p = pixels[y * button.width + x];
                if (p.a > 200 && p.r > 150 && p.b < 90 && x > coinEnd) coinEnd = x;
            }
        int clean = Mathf.Clamp((coinEnd + first) / 2, 0, button.width - 1);
        int from = Mathf.Max(coinEnd + 4, first - 24);
        int to = Mathf.Min(button.width - 1, last + 24);
        for (int x = from; x <= to; x++)
            for (int y = 0; y < button.height; y++)
                pixels[y * button.width + x] = pixels[y * button.width + clean];
        button.SetPixels32(pixels);
        button.Apply();
        return button;
    }

    private static Texture2D RoundCut(Texture2D sheet, RectInt art)
    {
        var cut = Cut(sheet, art);
        var pixels = cut.GetPixels32();
        float cx = (cut.width - 1) * .5f, cy = (cut.height - 1) * .5f, radius = Mathf.Min(cx, cy);
        for (int y = 0; y < cut.height; y++)
            for (int x = 0; x < cut.width; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius;
                var p = pixels[y * cut.width + x];
                p.a = (byte)(p.a * Mathf.Clamp01(.5f - d));
                pixels[y * cut.width + x] = p;
            }
        cut.SetPixels32(pixels);
        cut.Apply();
        return cut;
    }

    private static Texture2D RoundedCut(Texture2D sheet, RectInt art, float radius)
    {
        var cut = Cut(sheet, art);
        var pixels = cut.GetPixels32();
        for (int y = 0; y < cut.height; y++)
            for (int x = 0; x < cut.width; x++)
            {
                float d = BoxDistance(x, y, cut.width, cut.height, radius);
                var p = pixels[y * cut.width + x];
                p.a = (byte)(p.a * Mathf.Clamp01(.5f - d));
                pixels[y * cut.width + x] = p;
            }
        cut.SetPixels32(pixels);
        cut.Apply();
        return cut;
    }

    // Sheet rects are quoted the way the picture reads, from the top down.
    private static Texture2D Cut(Texture2D sheet, RectInt art)
    {
        var cut = new Texture2D(art.width, art.height, TextureFormat.RGBA32, false);
        cut.SetPixels(sheet.GetPixels(art.x, sheet.height - (art.y + art.height), art.width, art.height));
        cut.Apply();
        return cut;
    }

    // ---- the boxes the sheet does not carry --------------------------------

    private static float BoxDistance(float x, float y, int w, int h, float radius)
    {
        float dx = Mathf.Abs(x - (w - 1) * .5f) - (w * .5f - radius);
        float dy = Mathf.Abs(y - (h - 1) * .5f) - (h * .5f - radius);
        float outside = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0));
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
    }

    private static Texture2D RoundedBox(int w, int h, float radius, Color top, Color bottom,
        Color line, float lineWidth, Color edge, float edgeWidth)
    {
        var box = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = BoxDistance(x, y, w, h, radius);
                Color fill = Color.Lerp(bottom, top, (float)y / (h - 1));
                Color colour = fill;
                float inner = -(edgeWidth + lineWidth);
                if (d > -edgeWidth && edgeWidth > 0) colour = edge;
                else if (d > inner) colour = line;
                colour.a = Mathf.Clamp01(.5f - d);
                pixels[y * w + x] = colour;
            }
        box.SetPixels(pixels);
        box.Apply();
        return box;
    }

    // ---- writing the sprites out ------------------------------------------

    private static Sprite SaveDrawn(string name, Texture2D texture, int border, float unitsPerTexel)
    {
        return Write(name, texture, new Vector4(border, border, border, border), texture.width / unitsPerTexel);
    }

    private static Sprite SaveCut(string name, Texture2D texture, int border, float displayWidth)
    {
        return Write(name, texture, border > 0 ? new Vector4(border, border, border, border) : Vector4.zero, displayWidth);
    }

    private static Sprite SaveCut(string name, Texture2D texture, int _, float displayWidth, Vector4 border)
    {
        return Write(name, texture, border, displayWidth);
    }

    // The sprite is imported at the size the card gives it, so a nine-slice's
    // borders hold their drawn thickness however wide the piece is stretched.
    private static Sprite Write(string name, Texture2D texture, Vector4 border, float displayWidth)
    {
        string path = OutDir + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteBorder = border;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        // A canvas measures a nine-slice's borders against 100 pixels per unit, so
        // the import scale has to be quoted in those hundredths - otherwise the
        // corners collapse and the frame renders as a stretched blob.
        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        importer.spritePixelsPerUnit = loaded != null && displayWidth > 0 ? 100f * loaded.width / displayWidth : 100f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- the card ----------------------------------------------------------

    private static RectTransform FindCard()
    {
        foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "ShopPanel")
                for (int i = 0; i < t.childCount; i++)
                    if (t.GetChild(i).name == "Card")
                        return (RectTransform)t.GetChild(i);
        return null;
    }

    private static TMP_FontAsset FindFont()
    {
        foreach (var label in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (label.font != null && label.font.name.Contains("Fredoka"))
                return label.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static void BuildCard(RectTransform card, Art art)
    {
        var controller = Object.FindAnyObjectByType<ShopUIController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            Debug.LogError("ShopUIController not found in the scene.");
            return;
        }

        for (int i = card.childCount - 1; i >= 0; i--) Object.DestroyImmediate(card.GetChild(i).gameObject);
        card.sizeDelta = new Vector2(CardW, CardH);
        var frame = card.GetComponent<Image>();
        if (frame == null) frame = card.gameObject.AddComponent<Image>();
        frame.sprite = art.Panel;
        frame.type = Image.Type.Sliced;
        frame.color = Color.white;

        // The plate hangs over the frame's top edge, the way the sheet has it.
        var header = Node("Header", card, new Vector2(0, 425), new Vector2(340, 340 * art.Header.rect.height / art.Header.rect.width));
        var headerImage = header.gameObject.AddComponent<Image>();
        headerImage.sprite = art.Header;
        headerImage.raycastTarget = false;

        var pill = Node("Wallet", card, new Vector2(-208, 338), new Vector2(180, 52));
        var pillImage = pill.gameObject.AddComponent<Image>();
        pillImage.sprite = art.Pill;
        pillImage.type = Image.Type.Sliced;
        pillImage.raycastTarget = false;
        var coin = Node("WalletIcon", pill, new Vector2(-58, 0), new Vector2(40, 40));
        var coinImage = coin.gameObject.AddComponent<Image>();
        coinImage.sprite = art.Coin;
        coinImage.raycastTarget = false;
        var wallet = Label(pill, "WalletAmount", "0", new Vector2(14, 0), new Vector2(100, 44), 30, Gold, art.Font,
            TextAlignmentOptions.Left, FontStyles.Bold);

        var buyButtons = new Button[Items.Length];
        var buyTexts = new TextMeshProUGUI[Items.Length];
        var statusTexts = new TextMeshProUGUI[Items.Length];

        for (int i = 0; i < Items.Length; i++)
        {
            var item = Items[i];
            var row = Node(item.Key + "Row", card, new Vector2(0, FirstRowY - RowStep * i), new Vector2(RowW, RowH));
            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.sprite = art.Row;
            rowImage.type = Image.Type.Sliced;
            rowImage.raycastTarget = false;

            var tile = Node(item.Key + "Icon", row, new Vector2(-248, 0), new Vector2(105, 105));
            var tileImage = tile.gameObject.AddComponent<Image>();
            tileImage.sprite = art.Tiles[i];
            tileImage.raycastTarget = false;

            Label(row, item.Key + "Name", item.Name, new Vector2(-8, 31), new Vector2(330, 40), 27, Ink, art.Font,
                TextAlignmentOptions.Left, FontStyles.Bold);
            Label(row, item.Key + "Description", item.Description, new Vector2(22, -6), new Vector2(390, 52), 19, Muted, art.Font,
                TextAlignmentOptions.TopLeft, FontStyles.Normal);
            statusTexts[i] = Label(row, item.Key + "Status", "", new Vector2(-8, -42), new Vector2(330, 30), 19, Muted, art.Font,
                TextAlignmentOptions.Left, FontStyles.Normal);

            var buy = Node("Buy" + item.Key + "Button", row, new Vector2(208, 0), new Vector2(187, 64));
            var buyImage = buy.gameObject.AddComponent<Image>();
            buyImage.sprite = art.Price;
            buyImage.type = Image.Type.Sliced;
            buyButtons[i] = buy.gameObject.AddComponent<Button>();
            buyButtons[i].targetGraphic = buyImage;
            var colours = buyButtons[i].colors;
            colours.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colours.pressedColor = new Color(.82f, .82f, .82f);
            colours.disabledColor = new Color(.55f, .58f, .55f, .85f);
            buyButtons[i].colors = colours;
            // The coin is painted into the button's left cap, so the price sits
            // in what is left of it rather than in the middle of the whole pill.
            buyTexts[i] = Label(buy, "Price", "0", new Vector2(43, 1), new Vector2(96, 46), 28, Ink, art.Font,
                TextAlignmentOptions.Center, FontStyles.Bold);
            buyTexts[i].enableAutoSizing = true;
            buyTexts[i].fontSizeMin = 16;
            buyTexts[i].fontSizeMax = 28;
        }

        var close = Node("CloseButton", card, new Vector2(0, -373), new Vector2(300, 300 * art.Close.rect.height / art.Close.rect.width));
        var closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = art.Close;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        var closeColours = closeButton.colors;
        closeColours.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
        closeColours.pressedColor = new Color(.82f, .82f, .82f);
        closeButton.colors = closeColours;

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
        serialized.FindProperty("walletText").objectReferenceValue = wallet;
        for (int i = 0; i < Items.Length; i++)
        {
            string key = Items[i].Key == "ExtraLife" ? "ExtraLife" : Items[i].Key;
            string lower = char.ToLowerInvariant(key[0]) + key.Substring(1);
            serialized.FindProperty("buy" + key + "Button").objectReferenceValue = buyButtons[i];
            serialized.FindProperty("buy" + key + "ButtonText").objectReferenceValue = buyTexts[i];
            serialized.FindProperty(lower + "StatusText").objectReferenceValue = statusTexts[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Node(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        node.SetParent(parent, false);
        node.anchorMin = node.anchorMax = new Vector2(.5f, .5f);
        node.pivot = new Vector2(.5f, .5f);
        node.anchoredPosition = position;
        node.sizeDelta = size;
        return node;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
        float fontSize, Color colour, TMP_FontAsset font, TextAlignmentOptions alignment, FontStyles style)
    {
        var label = Node(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = colour;
        label.alignment = alignment;
        label.fontStyle = style;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }
}
