using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Drives the Shop screen: the painted shop room behind, the SHOP sign, the coin
// and moonshard balances and a row of ability cards, each with its price, how
// many the player already holds, and a Buy that goes through ShopManager.
//
// The screen is built at runtime inside the scene's shop panel, from the sprites
// in Resources/Shop (cut from the approved mockup - the cards with their prices
// and descriptions painted out, so both can be the real ones). Nothing about it
// is wired in the scene, so a new ability is one more entry in Catalogue() and,
// when it has art, one more card sprite.
//
// The room is a cover that never moves; only the cards scroll, sideways, once
// there are more of them than the screen is wide.
//
// There are two ways in and closing has to undo whichever it was. From the pause
// menu the game is already stopped and the pause card is only hidden, so closing
// puts that card back and leaves time stopped. From the button on the HUD the
// game is still running, so opening stops it and closing starts it again -
// otherwise the ghosts keep hunting a player who is reading prices.
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }
    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject pausePanel; // hidden while the shop is open so its dim backdrop doesn't stack/bleed through
    [SerializeField] private GameObject[] hudElementsToHide; // top HUD (coins/wallet/hearts) - hidden outright rather than trusted to the backdrop alone
    [SerializeField] private Button openButton;      // on the pause card
    [SerializeField] private Button hudOpenButton;   // in the corner of the HUD, during play
    [SerializeField] private TextMeshProUGUI walletText; // the old card's balance - only its font is still used

    // Everything below is placed in the mockup's own pixels, top-left origin,
    // and scaled to whatever the screen has room for.
    private const float DesignWidth = 1672f, DesignHeight = 941f;
    // The band the cards scroll in, a little taller than a card so the count
    // badge on its corner is not clipped.
    private const float CardsTop = 258f, CardsBottom = 866f, CardTop = 278f;
    private const float CardHeight = 573f, SidePadding = 100f, CardGap = 6f;

    // Where the painted-out parts of each card are: the pill between the coin
    // and BUY, the text block under the artwork, and the BUY button itself.
    // They move a few pixels from card to card; new art cut from the same
    // template can lean on the blank card's.
    private struct CardArt
    {
        public float Width, PriceLeft, PriceRight, TextLeft, TextRight, BuyLeft;
        public CardArt(float width, float priceLeft, float priceRight, float textLeft, float textRight, float buyLeft)
        {
            Width = width; PriceLeft = priceLeft; PriceRight = priceRight;
            TextLeft = textLeft; TextRight = textRight; BuyLeft = buyLeft;
        }
    }

    private static readonly Dictionary<string, CardArt> Art = new Dictionary<string, CardArt>
    {
        { "trap",     new CardArt(377, 107, 193, 27, 350, 202) },
        { "freeze",   new CardArt(356,  98, 184, 18, 337, 193) },
        { "shield",   new CardArt(377, 109, 195, 30, 351, 204) },
        { "teleport", new CardArt(385, 105, 193, 24, 353, 202) },
        { "blank",    new CardArt(377, 107, 193, 27, 350, 202) },
    };

    // One thing for sale. Card names the sprites (Resources/Shop/card_<Card> and
    // its greyed-out BUY, buy_off_<Card>); Title and Icon are only drawn on the
    // blank card, which has neither painted on.
    private sealed class Item
    {
        public string Card, Title, Icon, Description;
        public Func<int> Cost, Owned;
        public Func<bool> CanBuy, Buy, Maxed;
    }

    private static int Held(bool present, Func<int> count) => present ? count() : 0;

    private static Item[] Catalogue()
    {
        ShopManager shop() => ShopManager.Instance;
        return new[]
        {
            new Item
            {
                Card = "trap", Description = "Immobilizes one enemy for 4 seconds.",
                Cost = () => shop().GetTrapCost(), CanBuy = () => shop().CanBuyTrap(), Buy = () => shop().BuyTrap(),
                Owned = () => Held(TrapManager.Instance != null, () => TrapManager.Instance.TrapsOwned),
            },
            new Item
            {
                Card = "freeze", Description = "Freezes all enemies for 5 seconds.",
                Cost = () => shop().GetFreezeCost(), CanBuy = () => shop().CanBuyFreeze(), Buy = () => shop().BuyFreeze(),
                Owned = () => Held(FreezeManager.Instance != null, () => FreezeManager.Instance.FreezesOwned),
            },
            new Item
            {
                Card = "shield", Description = "Protects you from enemies for 5 seconds.",
                Cost = () => shop().GetShieldCost(), CanBuy = () => shop().CanBuyShield(), Buy = () => shop().BuyShield(),
                Owned = () => Held(ShieldManager.Instance != null, () => ShieldManager.Instance.ShieldsOwned),
            },
            new Item
            {
                Card = "teleport", Description = "Moves you to a random safe place.",
                Cost = () => shop().GetTeleportCost(), CanBuy = () => shop().CanBuyTeleport(), Buy = () => shop().BuyTeleport(),
                Owned = () => Held(TeleportManager.Instance != null, () => TeleportManager.Instance.TeleportsOwned),
            },
            new Item
            {
                Card = "blank", Title = "Extra Life", Icon = "icon_life", Description = "Brings back one lost life.",
                Cost = () => shop().GetExtraLifeCost(), CanBuy = () => shop().CanBuyExtraLife(), Buy = () => shop().BuyExtraLife(),
                Maxed = () => shop().IsExtraLifeMaxed(),
                Owned = () => Held(LivesManager.Instance != null, () => LivesManager.Instance.CurrentLives),
            },
        };
    }

    private sealed class Card
    {
        public Item Item;
        public RectTransform Rect;
        public Button Buy;
        public GameObject Off, Badge;
        public TextMeshProUGUI Price, Count;
    }

    private static readonly Color32 Ink = new Color32(18, 12, 58, 255);
    private static readonly Color32 Lilac = new Color32(226, 228, 255, 255);

    private readonly List<Card> _cards = new List<Card>();
    private TMP_FontAsset _font;
    private float _scale;
    private Vector2 _builtFor;
    private Button _close;
    private TextMeshProUGUI _coins, _gems, _toast;
    private ScrollRect _scroll;
    private Coroutine _toastFade;

    void Awake()
    {
        Instance = this;
        _font = walletText != null ? walletText.font : TMP_Settings.defaultFontAsset;
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
            // The old hand-built card still sits in the scene; the screen is drawn
            // fresh on first open.
            for (int i = shopPanel.transform.childCount - 1; i >= 0; i--)
                Destroy(shopPanel.transform.GetChild(i).gameObject);
        }
        if (openButton != null) openButton.onClick.AddListener(OpenFromPause);
        if (hudOpenButton != null) hudOpenButton.onClick.AddListener(OpenFromHud);

        GamepadMenus.Register(shopPanel, 30, FirstChoice, Close);
    }

    // Select on a controller is the shop button on the HUD: it opens the shop from
    // the board and closes it again. Only from the board - the button it stands in
    // for is under every other popup, where no finger could reach it.
    void Update()
    {
        if (IsOpen) KeepSelectionInView();

        if (!Pad.Pressed(PadButton.Select)) return;
        if (IsOpen) Close();
        else if (CanOpenFromBoard()) OpenFromHud();
    }

    private bool CanOpenFromBoard()
    {
        if (Time.timeScale == 0f) return false;
        if (hudOpenButton == null || !hudOpenButton.isActiveAndEnabled || !hudOpenButton.interactable) return false;
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return false;
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive) return false;
        return true;
    }

    // With a controller the screen starts on the first thing there is money for,
    // or on the way out when there is nothing.
    private Selectable FirstChoice()
    {
        foreach (var card in _cards)
            if (card.Buy.interactable) return card.Buy;
        return _close;
    }

    private bool _openedFromPause;

    private void OpenFromPause()
    {
        _openedFromPause = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        Open();
    }

    private void OpenFromHud()
    {
        // Nothing else is stopping the game on this route, so the shop does.
        // Anything that already owns the screen has its own popup over the HUD
        // button, so there is no state to check first.
        _openedFromPause = false;
        Time.timeScale = 0f;
        Open();
    }

    private void Open()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(true);
        Build();
        if (_scroll != null) _scroll.horizontalNormalizedPosition = 0f;
        if (_toast != null) _toast.alpha = 0f;
        SetHudVisible(false);
        SetCountdownCovered(true);
        Refresh();
    }

    private void Close()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (_openedFromPause)
        {
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else
        {
            GameSpeed.Resume();
        }
        SetHudVisible(true);
        // Closing back into the pause menu is not being back on the board: the
        // countdown stays off until that closes too.
        SetCountdownCovered(_openedFromPause);
    }

    // Opened during the warm-up beat at the start of a level, the shop would have
    // the countdown sitting frozen behind it.
    private static void SetCountdownCovered(bool covered)
    {
        if (SpawnCountdownController.Instance != null) SpawnCountdownController.Instance.SetCovered(covered);
    }

    private void SetHudVisible(bool visible)
    {
        if (hudElementsToHide == null) return;
        foreach (var go in hudElementsToHide)
        {
            if (go != null) go.SetActive(visible);
        }
    }

    private void OnBuy(Card card)
    {
        if (ShopManager.Instance == null || !card.Item.Buy()) return;
        Refresh();
        StartCoroutine(Pop(card.Rect));
    }

    // Public so anything that changes the wallet behind the shop's back - the
    // test bar paying coins in while it is open - can have the prices and the
    // buy buttons say so without the shop being closed and reopened.
    public void Refresh()
    {
        if (EconomyManager.Instance != null)
        {
            if (_coins != null) _coins.text = EconomyManager.Instance.TotalCoins.ToString();
            if (_gems != null) _gems.text = EconomyManager.Instance.TotalMoonshards.ToString();
        }

        if (ShopManager.Instance == null) return;
        foreach (var card in _cards)
        {
            bool maxed = card.Item.Maxed != null && card.Item.Maxed();
            bool affordable = !maxed && card.Item.CanBuy();
            card.Price.text = maxed ? "MAX" : card.Item.Cost().ToString();
            card.Buy.interactable = affordable;
            card.Off.SetActive(!affordable);
            int owned = card.Item.Owned();
            card.Badge.SetActive(owned > 0);
            card.Count.text = owned.ToString();
        }
    }

    // ---- building the screen ----

    // Built on first open rather than in Awake, when the canvas may not have its
    // final size yet, and again only if the screen has changed shape since.
    private void Build()
    {
        var root = (RectTransform)shopPanel.transform;
        Vector2 size = root.rect.size;
        if (_cards.Count > 0 && size == _builtFor) return;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        _cards.Clear();
        _builtFor = size;
        _scale = Mathf.Min(size.x / DesignWidth, size.y / DesignHeight);

        // The room covers the whole screen whatever its shape, cropping rather
        // than letterboxing, and catches every tap that isn't on the shop itself.
        var room = Stretch(New("Room", root));
        Picture(room, "background", true);
        var cover = room.gameObject.AddComponent<AspectRatioFitter>();
        cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        cover.aspectRatio = DesignWidth / DesignHeight;

        BuildCards(root);

        // The top row hugs the screen's corners on a wider screen; the sign and
        // the tabs stay centred.
        Picture(Place(root, "Header", 510, 0, 1142, 151, .5f), "header", false);

        _close = Place(root, "Close", 28, 14, 132, 116, 0f).gameObject.AddComponent<Button>();
        _close.targetGraphic = Picture((RectTransform)_close.transform, "close", true);
        _close.onClick.AddListener(Close);

        var coins = Place(root, "Coins", 1184, 28, 1425, 113, 1f);
        Picture(coins, "coin_counter", false);
        _coins = Label(Local(coins, 97, 8, 216, 76), 38, Color.white, TextAlignmentOptions.Center);

        var gems = Place(root, "Moonshards", 1428, 25, 1654, 116, 1f);
        Picture(gems, "gem_counter", false);
        _gems = Label(Local(gems, 110, 10, 206, 82), 38, Color.white, TextAlignmentOptions.Center);

        Picture(Place(root, "Abilities tab", 413, 152, 883, 261, .5f), "tab_abilities", false);
        var characters = Place(root, "Characters tab", 883, 152, 1340, 261, .5f).gameObject.AddComponent<Button>();
        characters.targetGraphic = Picture((RectTransform)characters.transform, "tab_characters", true);
        characters.onClick.AddListener(() => ShowToast("Coming soon"));

        _toast = Label(Place(root, "Toast", 436, 470, 1236, 600, .5f), 64, new Color32(255, 214, 90, 255), TextAlignmentOptions.Center);
        _toast.outlineWidth = .28f;
        _toast.alpha = 0f;
    }

    private void BuildCards(RectTransform root)
    {
        float s = _scale;
        var viewport = New("Cards", root);
        viewport.anchorMin = new Vector2(0, .5f);
        viewport.anchorMax = new Vector2(1, .5f);
        viewport.sizeDelta = new Vector2(0, (CardsBottom - CardsTop) * s);
        viewport.anchoredPosition = new Vector2(0, (DesignHeight / 2 - (CardsTop + CardsBottom) / 2) * s);
        var mask = viewport.gameObject.AddComponent<RectMask2D>();
        mask.softness = new Vector2Int(Mathf.RoundToInt(40 * s), 0);
        // An invisible catcher so a drag that starts between two cards still scrolls.
        viewport.gameObject.AddComponent<Image>().color = Color.clear;

        var content = New("Row", viewport);
        float x = SidePadding * s;
        foreach (var item in Catalogue())
        {
            var card = BuildCard(content, item, x);
            x += card.Rect.sizeDelta.x + CardGap * s;
        }
        float rowWidth = x - CardGap * s + SidePadding * s;

        // A row that fits is simply centred; only a longer one scrolls.
        bool scrolls = rowWidth > root.rect.width;
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(scrolls ? 0 : .5f, 1);
        content.sizeDelta = new Vector2(rowWidth, viewport.sizeDelta.y);
        content.anchoredPosition = Vector2.zero;

        _scroll = viewport.gameObject.AddComponent<ScrollRect>();
        _scroll.viewport = viewport;
        _scroll.content = content;
        _scroll.horizontal = scrolls;
        _scroll.vertical = false;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.scrollSensitivity = 40f;
    }

    private Card BuildCard(RectTransform row, Item item, float left)
    {
        float s = _scale;
        if (!Art.TryGetValue(item.Card, out var art)) art = Art["blank"];

        var rect = New(item.Card, row);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(art.Width, CardHeight) * s;
        rect.anchoredPosition = new Vector2(left + rect.sizeDelta.x / 2, -((CardTop - CardsTop) * s + rect.sizeDelta.y / 2));
        Picture(rect, "card_" + item.Card, true);

        if (!string.IsNullOrEmpty(item.Title))
        {
            var title = Label(Local(rect, art.TextLeft, 26, art.TextRight, 108), 58, Color.white, TextAlignmentOptions.Center);
            title.text = item.Title.ToUpperInvariant();
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, Lilac, new Color32(196, 176, 255, 255));
            title.outlineWidth = .26f;
        }
        if (!string.IsNullOrEmpty(item.Icon))
        {
            float middle = art.Width / 2;
            Picture(Local(rect, middle - 110, 118, middle + 110, 338), item.Icon, false).preserveAspect = true;
        }

        var text = Label(Local(rect, art.TextLeft + 16, 362, art.TextRight - 16, 458), 29, Lilac, TextAlignmentOptions.Center);
        text.text = item.Description.ToUpperInvariant();
        text.lineSpacing = -6;

        var price = Label(Local(rect, art.PriceLeft, 486, art.PriceRight, 530), 38, Color.white, TextAlignmentOptions.Center);
        price.enableAutoSizing = true;
        price.fontSizeMin = 20 * s;
        price.fontSizeMax = 38 * s;

        // The BUY is painted on the card; the button is a clear patch over it,
        // and the greyed-out copy of the card's own BUY goes on top when there
        // is not the money for it.
        var buyRect = Local(rect, art.BuyLeft, 468, art.BuyLeft + 148, 550);
        var buy = buyRect.gameObject.AddComponent<Button>();
        buy.targetGraphic = Clear(buyRect);
        buy.transition = Selectable.Transition.None;
        var off = Stretch(New("Out of reach", rect));
        Picture(off, "buy_off_" + item.Card, false);

        var badge = Local(rect, art.Width - 84, 4, art.Width - 6, 82);
        Picture(badge, "badge", false);
        var count = Label(Stretch(New("Count", badge)), 36, Color.white, TextAlignmentOptions.Center);
        count.margin = new Vector4(0, 0, 0, 4 * s);

        var card = new Card { Item = item, Rect = rect, Buy = buy, Off = off.gameObject, Badge = badge.gameObject, Price = price, Count = count };
        buy.onClick.AddListener(() => OnBuy(card));
        _cards.Add(card);
        return card;
    }

    // A rect placed by the mockup's own pixels, pinned to the left (0), middle
    // (.5) or right (1) of the screen.
    private RectTransform Place(RectTransform parent, string name, float x0, float y0, float x1, float y1, float side)
    {
        var rect = New(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(side, .5f);
        rect.sizeDelta = new Vector2(x1 - x0, y1 - y0) * _scale;
        rect.anchoredPosition = new Vector2((x0 + x1) / 2 - DesignWidth * side, DesignHeight / 2 - (y0 + y1) / 2) * _scale;
        return rect;
    }

    // A rect placed by the pixels of the sprite it sits on, from its top-left.
    // Cards and counters are drawn at their mockup size times _scale, so their
    // own pixels map straight through.
    private RectTransform Local(RectTransform parent, float x0, float y0, float x1, float y1)
    {
        var rect = New("Part", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(x1 - x0, y1 - y0) * _scale;
        rect.anchoredPosition = new Vector2((x0 + x1) / 2, -(y0 + y1) / 2) * _scale;
        return rect;
    }

    private RectTransform New(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = shopPanel.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Picture(RectTransform rect, string sprite, bool catchesTaps)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("Shop/" + sprite);
        image.raycastTarget = catchesTaps;
        if (image.sprite == null) Debug.LogWarning("Shop: missing sprite Resources/Shop/" + sprite);
        return image;
    }

    private static Image Clear(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        return image;
    }

    private TextMeshProUGUI Label(RectTransform rect, float size, Color color, TextAlignmentOptions alignment)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) label.font = _font;
        label.fontSize = size * _scale;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.outlineWidth = .2f;
        label.outlineColor = Ink;
        return label;
    }

    // ---- small motions (unscaled: the game is stopped while the shop is up) ----

    private static IEnumerator Pop(RectTransform rect)
    {
        const float Length = .22f;
        for (float t = 0; t < Length; t += Time.unscaledDeltaTime)
        {
            if (rect == null) yield break;
            rect.localScale = Vector3.one * (1f + .07f * Mathf.Sin(t / Length * Mathf.PI));
            yield return null;
        }
        if (rect != null) rect.localScale = Vector3.one;
    }

    private void ShowToast(string message)
    {
        if (_toast == null) return;
        _toast.text = message.ToUpperInvariant();
        if (_toastFade != null) StopCoroutine(_toastFade);
        _toastFade = StartCoroutine(FadeToast());
    }

    private IEnumerator FadeToast()
    {
        const float Hold = 1f, Fade = .5f;
        for (float t = 0; t < Hold + Fade; t += Time.unscaledDeltaTime)
        {
            _toast.alpha = t < Hold ? 1f : 1f - (t - Hold) / Fade;
            yield return null;
        }
        _toast.alpha = 0f;
        _toastFade = null;
    }

    // A controller steps from card to card; the row follows so the one with the
    // ring round it is always on screen.
    private void KeepSelectionInView()
    {
        if (_scroll == null || !_scroll.horizontal || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return;
        foreach (var card in _cards)
        {
            if (card.Buy.gameObject != selected) continue;
            RectTransform viewport = _scroll.viewport;
            float margin = 40 * _scale;
            Vector3 left = viewport.InverseTransformPoint(card.Rect.TransformPoint(card.Rect.rect.min));
            Vector3 right = viewport.InverseTransformPoint(card.Rect.TransformPoint(card.Rect.rect.max));
            float shift = 0f;
            if (left.x < viewport.rect.xMin + margin) shift = viewport.rect.xMin + margin - left.x;
            else if (right.x > viewport.rect.xMax - margin) shift = viewport.rect.xMax - margin - right.x;
            if (Mathf.Abs(shift) > .5f)
                _scroll.content.anchoredPosition += new Vector2(shift * Mathf.Min(1f, Time.unscaledDeltaTime * 12f), 0);
            return;
        }
    }
}
