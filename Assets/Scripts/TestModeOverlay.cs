using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A small bar of test controls: pay the wallet, finish the level on the spot,
// and hand the board to the test bot. The first two are things a run normally
// takes a long time to reach, and all of it has to be reachable on the phone
// rather than only in the editor - a level that only misbehaves on the fifth
// board is not a level anyone should have to play four boards to see again.
//
// TEST MODE puts the bot on the board at double speed, on a copy of the save,
// until the last life is gone (see TestModeSession); then the report card
// comes up with what the run did level by level, and the same thing is written
// to a file (see PlaytestLog). The chip beside it picks the bot: by default the
// one the difficulty is for, or either one on purpose.
//
// REC is the same run with a person on the stick: a new game on a fresh copy
// of the save, recorded into the same report and trace the bot's runs are, so
// the bot can be tuned until it plays like the recordings. While it records the
// bar gets out of the thumb's way and leaves only its status line, coming back
// - with STOP REC - whenever the game is held still behind the pause card.
//
// STATS opens the same charts without running anything: the newest reports on
// the device, with the board held still behind them until CLOSE.
//
// It shows itself in the editor and in development builds and nowhere else, so
// a release APK handed to a player has no cheat bar in it and there is nothing
// to remember to switch off before shipping.
//
// Built in code rather than authored into the scene: it is a tool, it wants no
// art, and a scene that carries no cheat buttons cannot accidentally ship them
// wired to something.
public class TestModeOverlay : MonoBehaviour
{
    [Tooltip("Paid into the wallet each time the coin button is pressed.")]
    [SerializeField] private int coinsPerPress = 1000;
    [Tooltip("Leave off to build the bar in a release player too - for a tester who is not running a development build.")]
    [SerializeField] private bool developmentBuildsOnly = true;

    private static readonly Color Ink = new Color(.62f, .70f, .92f);
    private static readonly Color Gold = new Color(1f, .83f, .29f);
    private static readonly Color TestIdle = new Color(.12f, .47f, .55f);
    private static readonly Color TestRunning = new Color(.66f, .20f, .24f);
    private static readonly Color RecIdle = new Color(.55f, .16f, .22f);
    private static readonly Color RecInk = new Color(1f, .45f, .45f);

    private RectTransform _root;
    private Image _backing;
    private GameObject _controls;
    private TextMeshProUGUI _statusLabel;
    private Button _coinsButton;
    private Button _skipButton;
    private Button _recButton;
    private Button _statsButton;
    // The clock as STATS found it, to hand back on CLOSE.
    private float _heldTimeScale = 1f;
    private Button _botButton;
    private TextMeshProUGUI _botLabel;
    private Button _speedButton;
    private TextMeshProUGUI _speedLabel;
    private Button _testButton;
    private TextMeshProUGUI _testLabel;
    private GameObject _reportCard;

    // It puts itself into the game rather than being placed in the scene. A
    // scene that carries no cheat buttons cannot ship them by accident, there
    // is nothing to delete before a release build, and the tool cannot be lost
    // to someone tidying the hierarchy. It rides in the gameplay scene's own
    // object list, so it leaves with the level like everything else there.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Debug.isDebugBuild && !Application.isEditor) return;
        // The board is what these controls act on: no level manager, no bar.
        if (LevelManager.Instance == null) return;
        if (FindAnyObjectByType<TestModeOverlay>(FindObjectsInactive.Include) != null) return;
        new GameObject("Test Mode").AddComponent<TestModeOverlay>();
    }

    void Start()
    {
        if (developmentBuildsOnly && !Debug.isDebugBuild && !Application.isEditor)
        {
            enabled = false;
            return;
        }
        Build();
        PlaytestLog.Finished += ShowReport;

        // RUN AGAIN reloaded the board to get here: start the next run once it
        // has put its coins down.
        if (TestModeSession.RerunPending) StartCoroutine(StartRerun());
    }

    void OnDestroy()
    {
        PlaytestLog.Finished -= ShowReport;
    }

    private IEnumerator StartRerun()
    {
        yield return null;
        yield return null;
        TestModeSession.Start();
    }

    void Update()
    {
        if (_statusLabel == null) return;

        bool running = TestModeSession.Active;
        bool recording = running && TestModeSession.Human;
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 0;
        if (running)
        {
            int lives = LivesManager.Instance != null ? LivesManager.Instance.CurrentLives : 0;
            string who = recording
                ? "REC"
                : "BOT " + TestModeSession.Profile.name.ToUpperInvariant() + "  ·  x" + TestModeSession.PlaySpeed.ToString("0.#");
            _statusLabel.text = who + "  ·  LEVEL " + level + "  ·  LIVES " + lives;
        }
        else
        {
            _statusLabel.text = level > 0 ? "LEVEL " + level : "TEST";
        }
        _statusLabel.color = recording ? RecInk : Ink;

        // A thumb landing anywhere picks the joystick up, and the bar sits right
        // where one lands - so while a person plays, only the status line stays,
        // until the game is held still.
        bool showBar = !recording || Time.timeScale == 0f;
        if (_controls.activeSelf != showBar) _controls.SetActive(showBar);
        _backing.enabled = showBar;

        // The cheats would put numbers into the report that no play earned, and
        // the bot is chosen before a run, not during one.
        _coinsButton.interactable = !running;
        _skipButton.interactable = !running;
        _recButton.interactable = !running;
        _statsButton.interactable = !running;
        _botButton.interactable = !running;
        _speedButton.interactable = !recording;
        _botLabel.text = BotLabel();
        _speedLabel.text = SpeedLabel();
        _testButton.interactable = running || TestModeSession.CanStart;
        _testButton.image.color = running ? TestRunning : TestIdle;
        _testLabel.text = running ? (recording ? "STOP REC" : "STOP TEST") : "TEST MODE";
    }

    private static string SpeedLabel() => "x" + TestModeSession.PlaySpeed.ToString("0.#");

    private static string BotLabel()
    {
        switch (TestModeSession.Choice)
        {
            case BotChoice.Normal: return "BOT: NORMAL";
            case BotChoice.Hard: return "BOT: HARD";
            default: return "BOT: AUTO (" + TestModeSession.ProfileFor(BotChoice.MatchDifficulty).name.ToUpperInvariant() + ")";
        }
    }

    private void Build()
    {
        var root = new GameObject("Test Mode", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        _root = (RectTransform)root.transform;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the shop and the pause card: the point of it is to be reachable
        // while one of those is up.
        canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        // Bottom centre, clear of the joystick on the left and the ability bar
        // on the right, and clear of the HUD along the top.
        var bar = Rect(root.transform, "Bar", new Vector2(0, 112), new Vector2(740, 184));
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f);
        _backing = Box(bar, "Backing", Vector2.zero, new Vector2(740, 184), new Color(.04f, .05f, .12f, .82f));
        _statusLabel = Label(bar, "Level", "TEST", new Vector2(0, 70), new Vector2(540, 30), 22, Ink);

        // Everything but the status line, so a recording can put it all away at once.
        var controls = Rect(bar, "Controls", Vector2.zero, new Vector2(740, 184));
        _controls = controls.gameObject;

        _coinsButton = Button(controls, "Coins", "+" + coinsPerPress, new Vector2(-270, 18), new Vector2(170, 60),
            new Color(.16f, .52f, .25f), GiveCoins, out _);
        _skipButton = Button(controls, "Skip", "SKIP LEVEL", new Vector2(-90, 18), new Vector2(170, 60),
            new Color(.42f, .26f, .58f), SkipLevel, out var skipLabel);
        skipLabel.fontSize = 22;
        _recButton = Button(controls, "Rec", "REC", new Vector2(90, 18), new Vector2(170, 60),
            RecIdle, TestModeSession.StartRecording, out _);
        _statsButton = Button(controls, "Stats", "STATS", new Vector2(270, 18), new Vector2(170, 60),
            new Color(.20f, .36f, .55f), OpenStats, out _);
        _botButton = Button(controls, "Bot", BotLabel(), new Vector2(-166, -52), new Vector2(190, 60),
            new Color(.24f, .28f, .42f), CycleBot, out _botLabel);
        _botLabel.fontSize = 19;
        _speedButton = Button(controls, "Speed", SpeedLabel(), new Vector2(-6, -52), new Vector2(110, 60),
            new Color(.24f, .28f, .42f), TestModeSession.CycleSpeed, out _speedLabel);
        _testButton = Button(controls, "Test", "TEST MODE", new Vector2(166, -52), new Vector2(190, 60),
            TestIdle, ToggleTest, out _testLabel);
        _testLabel.fontSize = 24;
    }

    private void GiveCoins()
    {
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(coinsPerPress);
        // The shop reads the wallet when it opens, so it is only worth
        // refreshing while it is already up.
        if (ShopUIController.Instance != null && ShopUIController.Instance.IsOpen)
            ShopUIController.Instance.Refresh();
    }

    // Finishes the board through the same door the last coin does, so the level
    // complete card, its Next button and everything they set up behave exactly
    // as they do in a played-out level.
    private void SkipLevel()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.IsLevelCompleteActive) return;
        LevelManager.Instance.OnLevelComplete();
    }

    private static void CycleBot()
    {
        TestModeSession.Choice = (BotChoice)(((int)TestModeSession.Choice + 1) % 3);
    }

    private static void ToggleTest()
    {
        if (TestModeSession.Active) TestModeSession.Stop("stopped");
        else TestModeSession.StartBot();
    }

    // ---- the report card -----------------------------------------------------

    private void ShowReport(PlaytestReport r) => ShowCard(r, browsing: false);

    // STATS: the charts without a run. The board waits behind them, however
    // it was going - playing, or already held by a card of its own.
    private void OpenStats()
    {
        if (TestModeSession.Active || _reportCard != null) return;
        var latest = PlaytestLog.RecentReports(1);
        _heldTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ShowCard(latest.Count > 0 ? latest[0] : null, browsing: true);
    }

    private void CloseStats()
    {
        if (_reportCard != null) Destroy(_reportCard);
        _reportCard = null;
        Time.timeScale = _heldTimeScale;
    }

    private UnityEngine.UI.Button CloseButton(Transform card)
    {
        var close = Button(card, "Close", "CLOSE", new Vector2(790, -474), new Vector2(240, 52),
            new Color(.30f, .30f, .42f), CloseStats, out var label);
        label.fontSize = 22;
        return close;
    }

    // The report card. Straight after a run it is that run's report, with RUN
    // AGAIN and MAIN MENU; opened from STATS it is the newest report on the
    // device - or word that there is none yet - with CLOSE.
    private void ShowCard(PlaytestReport r, bool browsing)
    {
        if (this == null || _root == null) return;
        if (_reportCard != null) Destroy(_reportCard);

        // Full screen and catching every tap, so nothing on the board behind it
        // can be pressed while it is up.
        var backdrop = Box(_root, "Report", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, .62f));
        backdrop.raycastTarget = true;
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        _reportCard = backdrop.gameObject;

        // As much of the screen as there is: six charts side by side want the
        // room, and a wide phone is shorter than the layout's own 1080.
        var card = Box(backdrop.transform, "Card", Vector2.zero, new Vector2(1840, 1010), new Color(.071f, .078f, .161f, .97f)).transform;
        var area = _root.rect;
        float fit = Mathf.Min(1f, (area.height - 24f) / 1010f, (area.width - 24f) / 1840f);
        if (fit > 0f) card.localScale = Vector3.one * fit;

        Label(card, "Title", browsing ? "TEST STATISTICS" : "TEST REPORT", new Vector2(0, 470), new Vector2(1760, 50), 40, Gold);
        if (r == null)
        {
            Label(card, "Empty", "No test reports on this device yet - finish a TEST MODE or REC run first.",
                new Vector2(0, 40), new Vector2(1600, 40), 26, Ink);
            var closeEmpty = CloseButton(card);
            GamepadMenus.Register(backdrop.gameObject, 60, () => closeEmpty);
            return;
        }
        bool human = r.botProfile == TestModeSession.HumanName;
        Label(card, "Who", r.difficulty.ToUpperInvariant() + "  ·  "
                           + (human ? "PLAYER" : PlaytestBotProfile.DisplayName(r.botProfile).ToUpperInvariant() + " BOT") + "  ·  x"
                           + r.speed.ToString("0.#") + "  ·  " + EndReason(r.endReason),
            new Vector2(0, 432), new Vector2(1760, 30), 22, Ink);

        Label(card, "Summary",
            "Level " + r.startLevel + " → " + r.endLevel + "   ·   " + r.levelsCompleted + " cleared   ·   "
            + r.coinsCollected + " coins picked up   ·   " + RunRecord.Clock(Mathf.RoundToInt(r.gameSeconds))
            + " of game time, played in " + RunRecord.Clock(Mathf.RoundToInt(r.realSeconds)),
            new Vector2(0, 392), new Vector2(1760, 30), 21, Color.white);

        // Level by level, as charts - this run, the last few side by side, or
        // the table (see PlaytestReportView).
        new PlaytestReportView(card, r, !browsing, new Vector2(0, -40), new Vector2(1820, 780));

        // Opened from STATS there is no file just written - only the folder
        // they all live in.
        string where = browsing ? "Reports in " + System.IO.Path.Combine(Application.persistentDataPath, "playtests")
            : string.IsNullOrEmpty(PlaytestLog.LastFilePath) ? "Report file could not be written"
            : PlaytestLog.LastFilePath;
        var file = Label(card, "File", where,
            new Vector2(-330, -474), new Vector2(1100, 24), 15, new Color(.5f, .55f, .7f));
        file.alignment = TextAlignmentOptions.Left;

        if (browsing)
        {
            var close = CloseButton(card);
            GamepadMenus.Register(backdrop.gameObject, 60, () => close);
            return;
        }

        var again = Button(card, "Again", human ? "RECORD AGAIN" : "RUN AGAIN", new Vector2(540, -474), new Vector2(240, 52),
            TestIdle, TestModeSession.RunAgain, out var againLabel);
        againLabel.fontSize = 22;
        Button(card, "Menu", "MAIN MENU", new Vector2(790, -474), new Vector2(240, 52),
            new Color(.30f, .30f, .42f), TestModeSession.ExitToMenu, out var menuLabel);
        menuLabel.fontSize = 22;
        GamepadMenus.Register(backdrop.gameObject, 60, () => again);
    }

    // One row per level under a header, scrolling once a run gets deep enough
    // to need it - the report card's table view.
    internal static void Table(Transform parent, PlaytestReport r, Vector2 position, Vector2 size)
    {
        var header = Label(parent, "Header", Row("LV", "TIME", "COINS", "WALLET", "DEATHS", "TRAP", "FRZ", "SHLD", "TELE", "BOUGHT", "LIVES"),
            new Vector2(position.x, position.y + size.y * .5f - 15f), new Vector2(size.x, 30), 21, Gold);
        header.alignment = TextAlignmentOptions.Left;
        var viewport = Rect(parent, "Levels", new Vector2(position.x, position.y - 20f), new Vector2(size.x, size.y - 40f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, .001f);

        var content = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var text = content.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 21;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rows = new StringBuilder();
        foreach (var l in r.levels)
        {
            // The level the most lives went on stands out; a board joined part
            // way through is starred, since its numbers only count from there.
            bool worst = l.level == r.mostDeathsLevel && l.deaths > 0;
            if (worst) rows.Append("<color=#FF8A8A>");
            rows.Append(Row(
                l.level + (l.joinedMidLevel ? "*" : "") + (l.completed ? "" : " ×"),
                RunRecord.Clock(Mathf.RoundToInt(l.seconds)),
                l.coinsCollected + "/" + l.coinsOnBoard,
                "+" + l.walletEarned + (l.friendlyGhostsCaught > 0 ? " ♥" : ""),
                l.deaths + (l.deaths > 0 ? "  (" + l.lavaDeaths + "L " + l.enemyDeaths + "H)" : ""),
                l.traps.ToString(), l.freezes.ToString(), l.shields.ToString(), l.teleports.ToString(),
                l.purchases > 0 ? l.purchases + "  (−" + l.coinsSpent + ")" : "0",
                l.livesAtStart + " → " + l.livesAtEnd));
            if (worst) rows.Append("</color>");
            rows.Append('\n');
        }
        rows.Append("\n<size=17><color=#8088AA>* joined mid-level   × not cleared   ♥ friendly ghost caught   L lava   H hunters</color></size>");
        text.text = rows.ToString();

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private static readonly int[] Columns = { 0, 6, 14, 24, 35, 52, 59, 66, 73, 80, 93 };

    internal static string Row(params string[] cells)
    {
        var line = new StringBuilder();
        for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            line.Append("<pos=").Append(Columns[i]).Append("%>").Append(cells[i]);
        return line.ToString();
    }

    internal static string EndReason(string reason)
    {
        switch (reason)
        {
            case "out_of_lives": return "OUT OF LIVES";
            case "stopped": return "STOPPED";
            case "timeout": return "TIMED OUT";
            default: return reason.ToUpperInvariant();
        }
    }

    // ---- the little bit of UI it needs -------------------------------------

    internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        node.SetParent(parent, false);
        node.anchorMin = node.anchorMax = new Vector2(.5f, .5f);
        node.anchoredPosition = position;
        node.sizeDelta = size;
        return node;
    }

    internal static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    internal static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
        float fontSize, Color colour)
    {
        var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    internal static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size,
        Color colour, UnityEngine.Events.UnityAction action, out TextMeshProUGUI label)
    {
        var image = Box(parent, name, position, size, colour);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colours = button.colors;
        colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colours.pressedColor = new Color(.7f, .7f, .7f);
        colours.disabledColor = new Color(.45f, .45f, .45f, .6f);
        button.colors = colours;
        button.onClick.AddListener(action);
        label = Label(image.transform, "Label", text, Vector2.zero, size, 26, Color.white);
        return button;
    }
}
