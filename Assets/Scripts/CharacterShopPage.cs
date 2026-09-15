using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterShopPage : MonoBehaviour
{
    [SerializeField] private GameObject visualPrefab;
    [SerializeField] private GameObject foxVisualPrefab;
    [SerializeField] private WardenSkin[] skins;
    [SerializeField] private Sprite roundedPanel;
    [SerializeField] private TMP_FontAsset font;
    private readonly Color muted = new Color(.52f, .65f, .83f);
    private readonly Color accent = new Color(.25f, .86f, 1f);
    private readonly Color warning = new Color(1f, .47f, .42f);
    private const float WarningTime = 2.5f;
    private static readonly Vector2 ActionPosition = new Vector2(0, -254);
    private Button action;
    private TextMeshProUGUI title, status, actionText, counter;
    private Button[] tiles;
    private TextMeshProUGUI[] badges;
    private GameObject[,] portraits;
    private GameObject[,] colorPortraits;
    private Transform studio;
    private CanvasGroup reveal;
    private RectTransform hero;
    private int current;
    private int colorPreview;
    private float shownAt;
    // When a tap on UNLOCK came up short: the status line says so and the
    // button shakes, until WarningTime has passed.
    private float warnedAt = float.NegativeInfinity;
    private bool built;

    void OnEnable()
    {
        // Unity does not restore multidimensional arrays after a script reload.
        if (portraits == null && built)
        {
            if (studio != null) { studio.gameObject.SetActive(false); Destroy(studio.gameObject); }
            var oldContent = transform.Find("Collection");
            if (oldContent != null) { oldContent.gameObject.SetActive(false); Destroy(oldContent.gameObject); }
            built = false;
        }
        if (!built) Build();
        current = CharacterCollection.Selected;
        colorPreview = CharacterCollection.SelectedColor(current);
        shownAt = Time.unscaledTime;
        warnedAt = float.NegativeInfinity;
        CharacterCollection.Changed += Refresh;
        Refresh();
    }
    void OnDisable() { CharacterCollection.Changed -= Refresh; if (studio != null) studio.gameObject.SetActive(false); }
    void OnDestroy() { if (studio != null) Destroy(studio.gameObject); }
    void Update()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / .28f);
        reveal.alpha = t;
        hero.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, 1f - Mathf.Pow(1f - t, 3f));

        float warned = Time.unscaledTime - warnedAt;
        float shake = warned < .35f ? Mathf.Sin(warned * 60f) * 10f * (1f - warned / .35f) : 0f;
        ((RectTransform)action.transform).anchoredPosition = ActionPosition + new Vector2(shake, 0);
        if (warned >= WarningTime && warned - Time.unscaledDeltaTime < WarningTime) Refresh();
    }

    void Build()
    {
        built = true;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        studio = new GameObject("Shop portrait studio").transform;
        studio.position = new Vector3(18000, -18000, 18000);
        var content = Rect("Collection", transform, Vector2.zero, Vector2.zero);
        reveal = content.gameObject.AddComponent<CanvasGroup>();
        Box("Display", content, new Vector2(0, -55), new Vector2(630, 530), new Color(.025f, .055f, .13f, .96f));
        Label("Collection label", content, "CHOOSE YOUR COMPANION", new Vector2(0, 175), new Vector2(540, 32), 17, muted);
        hero = Rect("Hero", content, new Vector2(0, 12), new Vector2(360, 300));
        portraits = new GameObject[2, 4];
        colorPortraits = new GameObject[2, 4];
        for (int character = 0; character < 2; character++)
            for (int color = 0; color < 4; color++)
            {
                portraits[character, color] = Portrait(hero, character, color, Vector2.zero, new Vector2(330, 300), 512);
                portraits[character, color].SetActive(false);
            }
        MakeButton("Previous", content, "<", new Vector2(-265, 5), new Vector2(66, 66), () => Browse(-1));
        MakeButton("Next", content, ">", new Vector2(265, 5), new Vector2(66, 66), () => Browse(1));
        title = Label("Name", content, "", new Vector2(0, -155), new Vector2(570, 46), 31, Color.white);
        status = Label("Ownership", content, "", new Vector2(0, -194), new Vector2(570, 32), 17, muted);
        action = MakeButton("Character action", content, "", ActionPosition, new Vector2(340, 62), Activate);
        actionText = action.GetComponentInChildren<TextMeshProUGUI>();
        counter = Label("Page", content, "", new Vector2(0, -301), new Vector2(300, 25), 14, muted);
        tiles = new Button[skins.Length]; badges = new TextMeshProUGUI[skins.Length];
        for (int i = 0; i < skins.Length; i++)
        {
            int index = i;
            float x = (i - (skins.Length - 1) * .5f) * 150;
            tiles[i] = MakeButton("Color " + i, content, "", new Vector2(x, -381), new Vector2(136, 112), () => ChooseColor(index));
            for (int character = 0; character < 2; character++)
            {
                colorPortraits[character, i] = Portrait(tiles[i].transform, character, i, new Vector2(0, 10), new Vector2(95, 85), 128);
                colorPortraits[character, i].SetActive(false);
            }
            badges[i] = Label("State", tiles[i].transform, "", new Vector2(0, -40), new Vector2(130, 24), 13, Color.white);
        }
    }
    void Browse(int delta)
    {
        current = (current + delta + CharacterCollection.Ids.Length) % CharacterCollection.Ids.Length;
        colorPreview = CharacterCollection.SelectedColor(current);
        shownAt = Time.unscaledTime;
        warnedAt = float.NegativeInfinity;
        Refresh();
    }
    void ChooseColor(int color)
    {
        colorPreview = color;
        shownAt = Time.unscaledTime;
        // Locked characters can try every color in the preview; only ownership is sold.
        if (CharacterCollection.Owned(current)) CharacterCollection.SelectColor(current, color);
        Refresh();
    }
    void Activate()
    {
        if (CharacterCollection.Owned(current) || CharacterCollection.Unlock(current))
        {
            warnedAt = float.NegativeInfinity;
            CharacterCollection.SelectColor(current, colorPreview);
            CharacterCollection.Select(current);
        }
        else warnedAt = Time.unscaledTime;
        ShopUIController.Instance?.Refresh();
        Refresh();
    }
    public void Refresh()
    {
        if (!built) return;
        if (isActiveAndEnabled) studio.gameObject.SetActive(true);
        bool owned = CharacterCollection.Owned(current), selected = CharacterCollection.Selected == current;
        int balance = EconomyManager.Instance != null ? EconomyManager.Instance.TotalBooGems : 0;
        int price = CharacterCollection.Prices[current];
        bool short_ = !owned && balance < price;
        bool warned = short_ && Time.unscaledTime - warnedAt < WarningTime;
        title.text = CharacterCollection.Names[current];
        status.text = owned ? "All 4 colors included · choose your favorite"
            : warned ? "Not enough Boo Gems · " + (price - balance) + " more needed"
            : short_ ? (price - balance) + " more Boo Gems · all 4 colors included"
            : "Unlock once · enjoy all 4 colors";
        status.color = warned ? warning : muted;
        actionText.text = selected ? "SELECTED" : owned ? "SELECT CHARACTER" : price + "  BOO GEMS  ·  UNLOCK";
        // Stays tappable when short, so the tap can say why it did nothing.
        action.interactable = !selected;
        action.image.color = selected ? new Color(.13f, .52f, .47f) : short_ ? new Color(.09f, .22f, .36f) : new Color(.07f, .42f, .65f);
        counter.text = (current + 1) + "  /  " + CharacterCollection.Ids.Length + "     ·     " + (owned ? "CHOOSE COLOR" : "PREVIEW COLORS");
        for (int i = 0; i < skins.Length; i++)
        {
            for (int character = 0; character < 2; character++)
            {
                portraits[character, i].SetActive(character == current && i == colorPreview);
                colorPortraits[character, i].SetActive(character == current);
            }
            tiles[i].image.color = i == colorPreview ? new Color(.08f, .4f, .58f) : new Color(.045f, .09f, .19f);
            badges[i].text = CharacterCollection.ColorNames[current][i];
            badges[i].color = i == colorPreview ? accent : muted;
        }
    }
    GameObject Portrait(Transform parent, int character, int index, Vector2 position, Vector2 size, int resolution)
    {
        var rect = Rect("Portrait " + index, parent, position, size);
        rect.gameObject.AddComponent<RawImage>().raycastTarget = resolution > 128;
        var slot = new GameObject("Portrait slot"); slot.transform.SetParent(studio, false);
        slot.transform.localPosition = new Vector3(studio.childCount * 30, 0, 0);
        var pivot = new GameObject("Turntable").transform; pivot.SetParent(slot.transform, false);
        var model = Instantiate(character == 0 ? visualPrefab : foxVisualPrefab, pivot, false);
        if (character == 0)
            foreach (var a in model.GetComponentsInChildren<WardenAppearance>(true)) a.SetSkin(skins[index]);
        else model.AddComponent<FoxAppearance>().SetColor(index, true);
        foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.updateMode = AnimatorUpdateMode.UnscaledTime;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var camera = new GameObject("Portrait camera").AddComponent<Camera>(); camera.transform.SetParent(slot.transform, false);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << 31; camera.allowHDR = false;
        var light = new GameObject("Portrait light").AddComponent<Light>(); light.transform.SetParent(slot.transform, false);
        light.type = LightType.Point; light.range = 12; light.intensity = 3; light.cullingMask = 1 << 31;
        light.transform.localPosition = new Vector3(1, 3, 4);
        rect.gameObject.AddComponent<GuideBookShowcase>().Configure(slot, camera, pivot, model.transform, resolution);
        slot.SetActive(false);
        return rect.gameObject;
    }
    RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.gameObject.layer = parent.gameObject.layer;
        r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = roundedPanel; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false; return image;
    }
    TextMeshProUGUI Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = fontSize * .7f; label.fontSizeMax = fontSize; return label;
    }
    Button MakeButton(string name, Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        var image = Box(name, parent, position, size, new Color(.06f, .2f, .35f)); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.7f, 1, 1); colors.pressedColor = new Color(.5f, .8f, .95f); colors.disabledColor = new Color(.65f, .7f, .8f); colors.fadeDuration = .12f; button.colors = colors;
        Label("Label", image.transform, text, Vector2.zero, size - new Vector2(12, 8), 22, Color.white);
        button.onClick.AddListener(click); return button;
    }
}
