using System;
using System.Collections;
using System.Collections.Generic;
using Sample;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The first time something turns up on the board, the board stops and it is
// introduced.
//
// On a player's very first level that is the goal itself - a coin, the counter of
// coins still out, the wallet the coins go into and the shop behind it - and then
// every enemy standing there. After that it is each new kind as it first appears:
// the Ember Hunter the first time a level fields one, the Friendly Spectral on
// level three, and so on. Each is shown once per install. NEW GAME does not bring
// them back: a player who has met the Frost Hunter has met it.
//
// It waits for the spawn sequence to finish (EnemySpawnManager.Spawned) - the
// countdown run, the portals closed, the camera settled on the player - so every
// enemy it talks about is really there. Then it stops time and walks the steps.
// Things on the board get a close-up, the camera tipped down beside them; things
// on the HUD get a spotlight. The card's cross is what moves the tour on.
//
// Everything it draws is built here at runtime, on a canvas of its own over the
// HUD. The scene holds only this component and what it points at (see
// FirstTimeTourBuilder). Time is stopped throughout, so all of it runs on
// unscaled time. It runs after the camera each frame, so the spotlight is laid
// over the picture the camera has just taken rather than the one before.
[DefaultExecutionOrder(100)]
public class FirstTimeTour : MonoBehaviour
{
    // True from the moment the tour claims the board until time is running again.
    // The pause menu stands aside while it is: the tour has stopped the game
    // itself, and Resume would start it again under the card.
    public static bool Running { get; private set; }

    [Header("What the tour points at")]
    [SerializeField] private RectTransform coinCounter;
    [SerializeField] private RectTransform shopButton;
    [SerializeField] private RectTransform settingsButton;
    [SerializeField] private GameObject friendlyGhost;
    [Tooltip("The HUD's canvas. The tour's own canvas copies its scaling, so the two line up.")]
    [SerializeField] private Canvas hudCanvas;

    [Header("Art (the guide book's)")]
    [SerializeField] private Sprite cardSprite;
    [SerializeField] private Sprite chipSprite;
    [SerializeField] private Sprite tipSprite;
    [SerializeField] private Sprite closeSprite;
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private TMP_FontAsset bodyFont;

    [Header("Close-up")]
    [Tooltip("How far the camera stands off whatever it is showing, in world units.")]
    [SerializeField] private float focusDistance = 3.6f;
    [Tooltip("Degrees the camera looks down in the close-up. The game is played at 90, straight down.")]
    [SerializeField] private float focusPitch = 52f;
    [Tooltip("How far left of centre the subject is framed, as a share of half the screen - the card takes the right.")]
    [SerializeField] private float focusShift = 0.4f;
    [SerializeField, Range(0f, 1f)] private float dim = 0.62f;

    private const string SeenKey = "tour.seen";
    private const string CoinKey = "basics.coin";
    private const string CounterKey = "basics.counter";
    private const string ShopKey = "basics.shop";
    private const string SettingsKey = "basics.settings";

    private const float CardWidth = 640f;
    private const float Margin = 40f;
    private const float CardGap = 28f;
    private const float PopIn = 0.2f;

    private static readonly Color GoalInk = new Color(1f, 0.79f, 0.23f);
    private static readonly Color ShopInk = new Color(0.44f, 0.69f, 1f);
    private static readonly Color TaglineInk = new Color(0.66f, 0.78f, 0.96f);
    private static readonly Color BodyInk = new Color(0.90f, 0.94f, 1f);
    private static readonly Color SubInk = new Color(0.58f, 0.66f, 0.81f);
    private static readonly Color RingInk = new Color(1f, 0.83f, 0.29f);

    private sealed class Step
    {
        public string Key, Name, Role, Tagline, Body, Tip;
        public Color RoleColour;
        public Transform World;   // the camera goes to look at this
        public RectTransform Ui;  // or the spotlight falls on this
        public Coin Coin;
    }

    private CameraFollow _camera;
    private Camera _cameraLens;
    private Transform _player;
    private bool _playing;

    private Step _current;
    private Renderer[] _subjectRenderers;
    private readonly List<KeyValuePair<Animator, AnimatorUpdateMode>> _subjectAnimators =
        new List<KeyValuePair<Animator, AnimatorUpdateMode>>();
    private bool _cardUp, _advance;
    private float _cardShownAt, _dimAlpha;

    // ---- the overlay -----------------------------------------------------------

    private GameObject _overlay;
    private RectTransform _root;
    private Image[] _dims;
    private Image _hole, _ring;
    private RectTransform _card;
    private CanvasGroup _cardGroup;
    private Image _chip, _tipBox;
    private TextMeshProUGUI _role, _name, _tagline, _body, _tip, _counter;
    private Button _close;
    private readonly Vector3[] _corners = new Vector3[4];

    void Awake()
    {
        Build();
        // A controller presses the cross with A, and B does the same.
        GamepadMenus.Register(_overlay, 60, () => _close, Advance);
    }

    void OnEnable() => EnemySpawnManager.Spawned += OnSpawned;
    void OnDisable() => EnemySpawnManager.Spawned -= OnSpawned;

    void OnDestroy()
    {
        if (_playing) Running = false;
    }

    void Start()
    {
        _camera = FindAnyObjectByType<CameraFollow>();
        _cameraLens = _camera != null ? _camera.GetComponent<Camera>() : Camera.main;
        var ghost = FindAnyObjectByType<GhostScript>();
        _player = ghost != null ? ghost.transform : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession() => Running = false;

    // ---- what has been seen ------------------------------------------------------

    // One preference holding every key shown so far, so forgetting them all - the
    // editor's reset - is a single delete.
    private static HashSet<string> Seen() =>
        new HashSet<string>(PlayerPrefs.GetString(SeenKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

    private static void MarkSeen(string key)
    {
        var seen = Seen();
        if (!seen.Add(key)) return;
        PlayerPrefs.SetString(SeenKey, string.Join("|", seen));
        PlayerPrefs.Save();
    }

    public static void ForgetAll()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }

    // ---- deciding what to show -----------------------------------------------------

    private void OnSpawned()
    {
        // The test bot plays the board without reading anything on it.
        if (Running || TestModeSession.Active) return;
        var steps = Compose();
        if (steps.Count > 0) StartCoroutine(Play(steps));
    }

    private List<Step> Compose()
    {
        var steps = new List<Step>();
        var seen = Seen();

        // The goal is explained where the player first meets it. Someone further
        // in than level one has already cleared a board.
        bool firstLevel = LevelManager.Instance == null || LevelManager.Instance.CurrentLevel == 1;
        if (firstLevel)
        {
            Coin coin = NearestCoin();
            if (coin != null && !seen.Contains(CoinKey))
                steps.Add(new Step
                {
                    Key = CoinKey, Name = "Coins", Role = "GOAL", RoleColour = GoalInk,
                    Tagline = "Collect them all to clear the level.",
                    Body = "Every coin on the board has to be picked up before you can move on. " +
                           "Steer over one to collect it.",
                    Tip = "When only one is left, a golden arrow points the way to it.",
                    World = coin.transform, Coin = coin
                });

            if (coinCounter != null && !seen.Contains(CounterKey))
                steps.Add(new Step
                {
                    Key = CounterKey, Name = "Coins Left", Role = "GOAL", RoleColour = GoalInk,
                    Tagline = "How many coins are still out on the board.",
                    Body = "This number counts down as you collect them. When it reaches zero the level is " +
                           "clear and you move on to the next one.",
                    Ui = coinCounter
                });

            if (shopButton != null && !seen.Contains(ShopKey))
                steps.Add(new Step
                {
                    Key = ShopKey, Name = "Wallet & Shop", Role = "SHOP", RoleColour = ShopInk,
                    Tagline = "Every coin you collect is paid in here.",
                    Body = "The coins you pick up turn into money in your wallet. Tap the wallet to open the " +
                           "Shop and spend it on abilities and extra lives.",
                    Tip = "Your wallet is kept from level to level, so you can save up for the bigger abilities.",
                    Ui = shopButton
                });
        }

        var onBoard = EnemySpawnManager.Instance != null
            ? EnemySpawnManager.Instance.OnBoard()
            : new Dictionary<string, GameObject>();
        foreach (var entry in CharacterCodex.Enemies)
        {
            if (seen.Contains(entry.Key) || !onBoard.TryGetValue(entry.Key, out GameObject enemy)) continue;
            steps.Add(FromEntry(entry, entry.Body, enemy.transform));
        }

        if (friendlyGhost != null && friendlyGhost.activeInHierarchy && !seen.Contains(CharacterCodex.FriendlyKey))
        {
            var flee = friendlyGhost.GetComponent<FriendlyGhostFlee>();
            string body = CharacterCodex.FriendlyBody(flee != null ? flee.CatchReward : 1000);
            steps.Add(FromEntry(CharacterCodex.Friendly, body, friendlyGhost.transform));
        }

        // Last, once the player knows what they are up against: where to make
        // the game fit their hands.
        if (firstLevel && settingsButton != null && !seen.Contains(SettingsKey))
            steps.Add(new Step
            {
                Key = SettingsKey, Name = "Settings", Role = "YOUR WAY", RoleColour = ShopInk,
                Tagline = "Set the game up the way that feels right to you.",
                Body = "Tap the gear to pause at any time. In there you can hide the joystick, move the " +
                       "ability bar to whichever corner suits your thumbs, and turn the music and sounds on or off.",
                Tip = "Game Info, in the same menu, is a guide to every character and ability.",
                Ui = settingsButton
            });

        return steps;
    }

    private static Step FromEntry(CharacterCodex.Entry entry, string body, Transform subject) => new Step
    {
        Key = entry.Key, Name = entry.Name, Role = entry.Role, RoleColour = entry.RoleColour,
        Tagline = entry.Tagline, Body = body, Tip = entry.Tip, World = subject
    };

    private Coin NearestCoin()
    {
        Coin best = null;
        float bestDistance = float.MaxValue;
        Vector3 from = _player != null ? _player.position : Vector3.zero;
        foreach (var coin in Coin.Active)
        {
            if (coin == null) continue;
            float d = (coin.transform.position - from).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = coin;
            }
        }
        return best;
    }

    // ---- playing it -------------------------------------------------------------

    private IEnumerator Play(List<Step> steps)
    {
        Running = true;
        _playing = true;

        // The last enemy lands on the frame the camera settles on the player;
        // give it that frame. Anything that has stopped the board meanwhile - a
        // popup - gets to finish first, and the tour starts once it lets go.
        while ((_camera != null && _camera.IsPlayingIntro) || Time.timeScale == 0f)
        {
            if (BoardTaken()) { Finish(); yield break; }
            yield return null;
        }
        if (BoardTaken()) { Finish(); yield break; }

        Time.timeScale = 0f;
        _dimAlpha = 0f;
        _overlay.SetActive(true);

        for (int i = 0; i < steps.Count; i++)
        {
            yield return ShowStep(steps[i], i, steps.Count);
            MarkSeen(steps[i].Key);
        }

        // Back to the shot the game is played at before anything moves again.
        _current = null;
        if (_camera != null)
        {
            _camera.ReleaseFocus();
            float giveUp = Time.unscaledTime + 2f;
            while (_camera.IsFocusing && Time.unscaledTime < giveUp)
            {
                FadeDim(0f);
                yield return null;
            }
        }

        _overlay.SetActive(false);
        GameSpeed.Resume();
        Finish();
    }

    private static bool BoardTaken() =>
        (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) ||
        (LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive);

    private void Finish()
    {
        _playing = false;
        Running = false;
    }

    private IEnumerator ShowStep(Step step, int index, int count)
    {
        _current = step;
        _advance = false;
        _card.gameObject.SetActive(false);
        _subjectRenderers = step.World != null ? SubjectRenderers(step.World) : null;
        WakeAnimators(step.World);

        if (_camera != null)
        {
            if (step.World != null) _camera.FocusOn(Centre(), focusDistance, focusPitch, focusShift);
            else _camera.ReleaseFocus();

            // The spotlight follows the subject in; the card waits for the camera
            // to arrive, so it is not laid out over a picture still on the move.
            float giveUp = Time.unscaledTime + 2f;
            while (Time.unscaledTime < giveUp &&
                   (step.World != null ? !_camera.FocusSettled : _camera.IsFocusing))
                yield return null;
        }

        Fill(step, index, count);
        PlaceCard(step);
        _card.gameObject.SetActive(true);
        _cardShownAt = Time.unscaledTime;
        _cardUp = true;

        while (!_advance) yield return null;
        SettleAnimators();
    }

    // The cross, Enter, Space, Escape (Android's back), and B on a controller.
    private void Advance()
    {
        if (!_cardUp) return;
        AudioManager.Play(GameSound.Click);
        _cardUp = false;
        _advance = true;
        _card.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_cardUp) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            Advance();
    }

    void LateUpdate()
    {
        if (!_playing || _current == null || !_overlay.activeSelf) return;

        // The coin had been tumbling when the board stopped, and could have been
        // caught edge on. It is laid face up and turned slowly instead.
        if (_current.Coin != null) _current.Coin.Present(Time.unscaledTime * 70f);

        FadeDim(dim);
        LayOut(_current.Ui != null ? UiHole(_current.Ui) : WorldHole());

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        _ring.color = new Color(RingInk.r, RingInk.g, RingInk.b, pulse * (_dimAlpha / Mathf.Max(dim, 0.01f)));

        if (_cardUp)
        {
            float p = Mathf.Clamp01((Time.unscaledTime - _cardShownAt) / PopIn);
            float ease = 1f - (1f - p) * (1f - p);
            _cardGroup.alpha = ease;
            _card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, ease);
        }
    }

    private void FadeDim(float to)
    {
        _dimAlpha = Mathf.MoveTowards(_dimAlpha, to, Time.unscaledDeltaTime * 2.5f);
        var colour = new Color(0f, 0f, 0f, _dimAlpha);
        foreach (var d in _dims) d.color = colour;
        _hole.color = colour;
        if (to <= 0f) _ring.color = Color.clear;
    }

    // ---- the subject ------------------------------------------------------------

    private static Renderer[] SubjectRenderers(Transform subject)
    {
        var found = new List<Renderer>();
        foreach (var r in subject.GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) found.Add(r);
        return found.ToArray();
    }

    private bool SubjectBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        if (_subjectRenderers == null) return false;
        foreach (var r in _subjectRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    private Vector3 Centre()
    {
        if (SubjectBounds(out Bounds bounds)) return bounds.center;
        return _current != null && _current.World != null ? _current.World.position : Vector3.zero;
    }

    // Characters hover and blink on their Animators, which stopped with the
    // clock. The one being introduced is let move while it is on show.
    private void WakeAnimators(Transform subject)
    {
        _subjectAnimators.Clear();
        if (subject == null) return;
        foreach (var animator in subject.GetComponentsInChildren<Animator>())
        {
            _subjectAnimators.Add(new KeyValuePair<Animator, AnimatorUpdateMode>(animator, animator.updateMode));
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private void SettleAnimators()
    {
        foreach (var pair in _subjectAnimators)
            if (pair.Key != null) pair.Key.updateMode = pair.Value;
        _subjectAnimators.Clear();
    }

    // ---- where the light falls, in the tour canvas's units ----------------------------

    private Rect UiHole(RectTransform target)
    {
        target.GetWorldCorners(_corners);
        // The HUD is an overlay canvas, so its world corners are screen pixels.
        Vector2 min = ToCanvas(_corners[0]), max = ToCanvas(_corners[2]);
        var rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                                   Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        const float Pad = 22f;
        return Rect.MinMaxRect(rect.xMin - Pad, rect.yMin - Pad, rect.xMax + Pad, rect.yMax + Pad);
    }

    private Rect WorldHole()
    {
        if (_cameraLens == null || !SubjectBounds(out Bounds b)) return new Rect(0f, 0f, 0f, 0f);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x,
                                     (i / 2) % 2 == 0 ? b.min.y : b.max.y,
                                     i / 4 == 0 ? b.min.z : b.max.z);
            Vector3 screen = _cameraLens.WorldToScreenPoint(corner);
            if (screen.z <= 0f) continue;
            Vector2 p = ToCanvas(screen);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            any = true;
        }
        if (!any) return new Rect(0f, 0f, 0f, 0f);

        // A circle round the subject, with room for it to hover inside.
        float size = Mathf.Max(max.x - min.x, max.y - min.y) * 1.25f + 70f;
        Vector2 centre = (min + max) * 0.5f;
        return new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);
    }

    private Vector2 ToCanvas(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
        return local;
    }

    // Four dark panes round the hole and the soft-edged hole itself, so the
    // board is dimmed everywhere but the one thing being talked about.
    private void LayOut(Rect hole)
    {
        Rect c = _root.rect;
        Place(_dims[0], Rect.MinMaxRect(c.xMin, hole.yMax, c.xMax, c.yMax));
        Place(_dims[1], Rect.MinMaxRect(c.xMin, c.yMin, c.xMax, hole.yMin));
        Place(_dims[2], Rect.MinMaxRect(c.xMin, hole.yMin, hole.xMin, hole.yMax));
        Place(_dims[3], Rect.MinMaxRect(hole.xMax, hole.yMin, c.xMax, hole.yMax));
        Place(_hole, hole);
        Place(_ring, hole);

        // The hole is a nine-slice whose corners are its whole curve, scaled so
        // they meet in the middle of the short side: a circle round a character,
        // a pill round a strip of HUD.
        float shortSide = Mathf.Max(1f, Mathf.Min(hole.width, hole.height));
        _hole.pixelsPerUnitMultiplier = _ring.pixelsPerUnitMultiplier = HoleBorder * 2f / shortSide;
    }

    private static void Place(Image image, Rect rect)
    {
        var rt = image.rectTransform;
        rt.anchoredPosition = rect.position;
        rt.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
    }

    // ---- the card -----------------------------------------------------------------

    private void Fill(Step step, int index, int count)
    {
        _role.text = step.Role;
        _role.color = step.RoleColour;
        _chip.color = step.RoleColour;
        _name.text = step.Name;

        _tagline.text = step.Tagline;
        _tagline.gameObject.SetActive(!string.IsNullOrEmpty(step.Tagline));
        _body.text = step.Body;
        _body.gameObject.SetActive(!string.IsNullOrEmpty(step.Body));

        _tip.text = "<color=#FFD34A>TIP</color>   " + step.Tip;
        _tipBox.gameObject.SetActive(!string.IsNullOrEmpty(step.Tip));

        _counter.text = (index + 1) + " / " + count;
        _counter.gameObject.SetActive(count > 1);

        _cardGroup.alpha = 0f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
    }

    // A close-up leaves the right-hand side of the screen for the card. A piece
    // of HUD has it hung underneath, on its own side of the screen.
    private void PlaceCard(Step step)
    {
        Rect c = _root.rect;
        Vector2 size = _card.rect.size;
        Vector2 at;
        if (step.Ui != null)
        {
            Rect hole = UiHole(step.Ui);
            at = new Vector2(hole.center.x, hole.yMin - CardGap - size.y * 0.5f);
        }
        else
        {
            at = new Vector2(c.width * 0.22f, 0f);
        }

        at.x = Mathf.Clamp(at.x, c.xMin + Margin + size.x * 0.5f, Mathf.Max(c.xMin + Margin + size.x * 0.5f, c.xMax - Margin - size.x * 0.5f));
        at.y = Mathf.Clamp(at.y, c.yMin + Margin + size.y * 0.5f, Mathf.Max(c.yMin + Margin + size.y * 0.5f, c.yMax - Margin - size.y * 0.5f));
        _card.anchoredPosition = at;
    }

    // ---- building the overlay -------------------------------------------------------

    private const int HoleTexture = 256;
    private const float HoleBorder = 127f;

    private void Build()
    {
        var go = new GameObject("TourCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Over the HUD, under the controller's focus ring.
        canvas.sortingOrder = 100;

        var scaler = go.GetComponent<CanvasScaler>();
        var hudScaler = hudCanvas != null ? hudCanvas.GetComponent<CanvasScaler>() : null;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = hudScaler != null ? hudScaler.referenceResolution : new Vector2(1920f, 1080f);
        scaler.screenMatchMode = hudScaler != null ? hudScaler.screenMatchMode : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = hudScaler != null ? hudScaler.matchWidthOrHeight : 0.5f;

        _overlay = go;
        _root = (RectTransform)go.transform;

        // Every pane catches the taps meant for the board and the HUD under it -
        // the wallet in the spotlight included: this is a look, not a press.
        _dims = new Image[4];
        for (int i = 0; i < _dims.Length; i++) _dims[i] = Pane("Dim", null);
        _hole = Pane("Hole", HoleSprite(false));
        _ring = Pane("Ring", HoleSprite(true));
        _ring.raycastTarget = false;

        BuildCard();
        go.SetActive(false);
    }

    private Image Pane(string name, Sprite sprite)
    {
        RectTransform rt = Node(_root, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = Vector2.zero;
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = Color.clear;
        image.raycastTarget = true;
        return image;
    }

    private void BuildCard()
    {
        _card = Node(_root, "Card");
        _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
        _card.sizeDelta = new Vector2(CardWidth, 0f);
        var back = _card.gameObject.AddComponent<Image>();
        back.sprite = cardSprite;
        back.type = cardSprite != null && cardSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        back.color = cardSprite != null ? Color.white : new Color(0.07f, 0.13f, 0.30f, 0.96f);
        _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();

        var column = _card.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(44, 44, 36, 30);
        column.spacing = 10f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        var fit = _card.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform roleRow = Node(_card, "RoleRow");
        var row = roleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.MiddleLeft;

        RectTransform chip = Node(roleRow, "Chip");
        _chip = chip.gameObject.AddComponent<Image>();
        _chip.sprite = chipSprite;
        _chip.type = chipSprite != null && chipSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        _chip.raycastTarget = false;
        var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        chipLayout.padding = new RectOffset(18, 18, 5, 5);
        chipLayout.childControlWidth = chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
        _role = Text(chip, "Role", labelFont, 20f, Color.white);
        _role.textWrappingMode = TextWrappingModes.NoWrap;
        _role.characterSpacing = 8f;

        _name = Text(_card, "Name", titleFont, 46f, Color.white);
        _tagline = Text(_card, "Tagline", labelFont, 26f, TaglineInk);
        _body = Text(_card, "Body", bodyFont, 25f, BodyInk);

        RectTransform tip = Node(_card, "Tip");
        _tipBox = tip.gameObject.AddComponent<Image>();
        _tipBox.sprite = tipSprite;
        _tipBox.type = tipSprite != null && tipSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        _tipBox.color = tipSprite != null ? Color.white : new Color(0.05f, 0.11f, 0.29f, 0.9f);
        _tipBox.raycastTarget = false;
        var tipLayout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        tipLayout.padding = new RectOffset(20, 20, 14, 16);
        tipLayout.childControlWidth = tipLayout.childControlHeight = true;
        tipLayout.childForceExpandWidth = true;
        tipLayout.childForceExpandHeight = false;
        _tip = Text(tip, "TipText", bodyFont, 23f, BodyInk);

        _counter = Text(_card, "Counter", labelFont, 19f, SubInk);
        _counter.alignment = TextAlignmentOptions.Right;

        // The cross sits on the card's corner, the way it sits on the guide book's.
        RectTransform close = Node(_card, "Close");
        close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        close.anchorMin = close.anchorMax = Vector2.one;
        close.pivot = new Vector2(0.5f, 0.5f);
        close.anchoredPosition = new Vector2(-14f, -14f);
        close.sizeDelta = new Vector2(84f, 84f);
        var cross = close.gameObject.AddComponent<Image>();
        cross.sprite = closeSprite;
        cross.preserveAspect = true;
        cross.color = closeSprite != null ? Color.white : new Color(0.9f, 0.3f, 0.35f);
        _close = close.gameObject.AddComponent<Button>();
        _close.targetGraphic = cross;
        _close.onClick.AddListener(Advance);

        _card.gameObject.SetActive(false);
    }

    private static RectTransform Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color colour)
    {
        RectTransform rt = Node(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = colour;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        return tmp;
    }

    // A dark square with a soft round hole in it - or, for the ring, just a
    // bright soft line where the hole's edge is. Nine-sliced about its middle,
    // so however it is stretched the curve stays round.
    private static Sprite HoleSprite(bool ring)
    {
        const int S = HoleTexture;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = ring ? "TourRing" : "TourHole"
        };
        var px = new Color32[S * S];
        float c = S * 0.5f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
            float a;
            if (ring)
            {
                float line = (d - 0.74f) / 0.03f, bloom = (d - 0.74f) / 0.10f;
                a = Mathf.Clamp01(Mathf.Exp(-line * line) + 0.35f * Mathf.Exp(-bloom * bloom));
            }
            else
            {
                a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.70f) / 0.22f));
            }
            px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(HoleBorder, HoleBorder, HoleBorder, HoleBorder));
    }
}
