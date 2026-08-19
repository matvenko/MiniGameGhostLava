using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The main menu's Easy / Hard choice: two cards the player taps instead of
// the old "press any key to start", so picking a mode and starting the game
// are the same action and nobody can fall into a mode they didn't choose.
//
// The cards are built from code rather than authored into the scene because
// they are the same card twice - identical size, spacing and text layout,
// differing only in colour and copy. Building them from one description
// keeps the pair in step, and means the menu scene keeps a single canvas
// with nothing to re-wire when the copy changes.
public class DifficultySelectController : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 cardSize = new Vector2(420f, 220f);
    [SerializeField] private float cardSpacing = 60f;
    [SerializeField] private float groupCenterY = -20f;
    [SerializeField] private float cornerRadius = 36f;

    // Shared with the loading screen so the cards look like they belong to
    // the same menu - see MenuPalette.
    private Sprite _cardSprite;
    private Sprite _glowSprite;

    private Action<Difficulty> _onChosen;
    private GameObject _root;

    // Shown only once loading has finished, so the choice is the last thing
    // between the player and a scene that is already in memory.
    public void Show(Action<Difficulty> onChosen)
    {
        _onChosen = onChosen;

        if (_root == null) Build();
        if (_root != null) _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Build()
    {
        RectTransform parent = canvasRoot;
        if (parent == null)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;
            parent = (RectTransform)canvas.transform;
        }

        _root = new GameObject("DifficultyChoice", typeof(RectTransform));
        var rootRect = (RectTransform)_root.transform;
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, groupCenterY);
        rootRect.sizeDelta = new Vector2(cardSize.x * 2f + cardSpacing, cardSize.y);
        // above the menu's background image, which is drawn first
        rootRect.SetAsLastSibling();

        int texSize = Mathf.RoundToInt(cornerRadius * 4f);
        _cardSprite = UIShapes.RoundedRect(texSize, Mathf.RoundToInt(cornerRadius));
        _glowSprite = UIShapes.RadialGlow(128, 2f);

        float offset = (cardSize.x + cardSpacing) * 0.5f;
        BuildCard(rootRect, -offset, MenuPalette.CardEasy, Difficulty.Easy, "EASY", "FOR YOUNGER PLAYERS\nSLOWER GHOSTS, BIGGER COIN REWARDS");
        BuildCard(rootRect, offset, MenuPalette.CardHard, Difficulty.Hard, "HARD", "FOR OLDER PLAYERS\nFULL-SPEED GHOSTS");
    }

    private void BuildCard(RectTransform parent, float x, Color color, Difficulty difficulty, string title, string subtitle)
    {
        var card = new GameObject(difficulty + "Button", typeof(RectTransform));
        var rect = (RectTransform)card.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = cardSize;

        // a wash of the card's own colour bleeding out behind it, so the two
        // cards light the backdrop instead of sitting flat on it
        var halo = new GameObject("Glow", typeof(RectTransform));
        var haloRect = (RectTransform)halo.transform;
        haloRect.SetParent(parent, false);
        haloRect.anchorMin = haloRect.anchorMax = new Vector2(0.5f, 0.5f);
        haloRect.anchoredPosition = new Vector2(x, -10f);
        haloRect.sizeDelta = cardSize * 1.75f;
        var haloImage = halo.AddComponent<Image>();
        haloImage.sprite = _glowSprite;
        haloImage.color = new Color(color.r, color.g, color.b, 0.35f);
        haloImage.raycastTarget = false;
        haloRect.SetSiblingIndex(rect.GetSiblingIndex());

        var image = card.AddComponent<Image>();
        image.sprite = _cardSprite;
        image.type = Image.Type.Sliced;
        image.color = color;

        var button = card.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => Choose(difficulty));

        BuildLabel(rect, "Title", title, 64f, FontStyles.Bold, new Vector2(0f, 42f), new Vector2(cardSize.x - 40f, 90f));
        BuildLabel(rect, "Subtitle", subtitle, 24f, FontStyles.Normal, new Vector2(0f, -52f), new Vector2(cardSize.x - 50f, 90f));
    }

    private void BuildLabel(RectTransform parent, string name, string content, float size, FontStyles style, Vector2 position, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        // the label must never eat the click meant for the card behind it
        text.raycastTarget = false;
    }

    private void Choose(Difficulty difficulty)
    {
        // Guard against a double tap landing two starts on the same frame -
        // the callback activates a scene load and must only run once.
        if (_onChosen == null) return;
        var callback = _onChosen;
        _onChosen = null;

        DifficultySettings.Current = difficulty;
        Hide();
        callback(difficulty);
    }
}
