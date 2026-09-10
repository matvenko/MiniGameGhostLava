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

    private RectTransform _root;
    private TextMeshProUGUI _statusLabel;
    private Button _coinsButton;
    private Button _skipButton;
    private Button _botButton;
    private TextMeshProUGUI _botLabel;
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
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 0;
        if (running)
        {
            int lives = LivesManager.Instance != null ? LivesManager.Instance.CurrentLives : 0;
            _statusLabel.text = "BOT " + TestModeSession.Profile.name.ToUpperInvariant()
                                + "  ·  x" + TestModeSession.PlaySpeed.ToString("0.#")
                                + "  ·  LEVEL " + level + "  ·  LIVES " + lives;
        }
        else
        {
            _statusLabel.text = level > 0 ? "LEVEL " + level : "TEST";
        }

        // The cheats would put numbers into the report that no play earned, and
        // the bot is chosen before a run, not during one.
        _coinsButton.interactable = !running;
        _skipButton.interactable = !running;
        _botButton.interactable = !running;
        _botLabel.text = BotLabel();
        _speedLabel.text = SpeedLabel();
        _testButton.interactable = running || TestModeSession.CanStart;
        _testButton.image.color = running ? TestRunning : TestIdle;
        _testLabel.text = running ? "STOP TEST" : "TEST MODE";
    }

    private static string SpeedLabel() => "x" + TestModeSession.PlaySpeed.ToString("0.#");

    private static string BotLabel()
    {
        switch (TestModeSession.Choice)
        {
            case BotChoice.Kid: return "BOT: KID";
            case BotChoice.Teen: return "BOT: TEEN";
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
        var bar = Rect(root.transform, "Bar", new Vector2(0, 112), new Vector2(560, 184));
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f);
        Box(bar, "Backing", Vector2.zero, new Vector2(560, 184), new Color(.04f, .05f, .12f, .82f));
        _statusLabel = Label(bar, "Level", "TEST", new Vector2(0, 70), new Vector2(540, 30), 22, Ink);

        _coinsButton = Button(bar, "Coins", "+" + coinsPerPress, new Vector2(-136, 18), new Vector2(250, 60),
            new Color(.16f, .52f, .25f), GiveCoins, out _);
        _skipButton = Button(bar, "Skip", "SKIP LEVEL", new Vector2(136, 18), new Vector2(250, 60),
            new Color(.42f, .26f, .58f), SkipLevel, out _);
        _botButton = Button(bar, "Bot", BotLabel(), new Vector2(-166, -52), new Vector2(190, 60),
            new Color(.24f, .28f, .42f), CycleBot, out _botLabel);
        _botLabel.fontSize = 19;
        Button(bar, "Speed", SpeedLabel(), new Vector2(-6, -52), new Vector2(110, 60),
            new Color(.24f, .28f, .42f), TestModeSession.CycleSpeed, out _speedLabel);
        _testButton = Button(bar, "Test", "TEST MODE", new Vector2(166, -52), new Vector2(190, 60),
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
        else TestModeSession.Start();
    }

    // ---- the report card -----------------------------------------------------

    private void ShowReport(PlaytestReport r)
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

        var card = Box(backdrop.transform, "Card", Vector2.zero, new Vector2(1280, 920), new Color(.07f, .08f, .17f, .97f)).transform;

        Label(card, "Title", "TEST REPORT", new Vector2(0, 410), new Vector2(1200, 60), 44, Gold);
        Label(card, "Who", r.difficulty.ToUpperInvariant() + "  ·  " + r.botProfile.ToUpperInvariant() + " BOT  ·  x"
                           + r.speed.ToString("0.#") + "  ·  " + EndReason(r.endReason),
            new Vector2(0, 360), new Vector2(1200, 34), 24, Ink);

        string mostDeaths = r.mostDeathsLevel > 0 ? "most on level " + r.mostDeathsLevel : "none";
        Label(card, "Summary",
            "Level " + r.startLevel + " → " + r.endLevel + "   ·   " + r.levelsCompleted + " cleared   ·   "
            + r.coinsCollected + " coins  (+" + r.walletEarned + " wallet, −" + r.coinsSpent + " spent)\n"
            + r.deaths + " deaths  (" + r.lavaDeaths + " lava, " + r.enemyDeaths + " hunters) — " + mostDeaths
            + "   ·   " + r.abilitiesUsed + " abilities   ·   " + r.purchases + " purchases\n"
            + RunRecord.Clock(Mathf.RoundToInt(r.gameSeconds)) + " of game time, played in "
            + RunRecord.Clock(Mathf.RoundToInt(r.realSeconds)),
            new Vector2(0, 270), new Vector2(1200, 120), 25, Color.white);

        var header = Label(card, "Header", Row("LV", "TIME", "COINS", "WALLET", "DEATHS", "TRAP", "FRZ", "SHLD", "TELE", "BOUGHT", "LIVES"),
            new Vector2(0, 186), new Vector2(1160, 30), 21, Gold);
        header.alignment = TextAlignmentOptions.Left;

        Table(card, r);

        Label(card, "File", string.IsNullOrEmpty(PlaytestLog.LastFilePath) ? "Report file could not be written"
                : PlaytestLog.LastFilePath,
            new Vector2(0, -318), new Vector2(1200, 28), 16, new Color(.5f, .55f, .7f));

        Button(card, "Again", "RUN AGAIN", new Vector2(-170, -385), new Vector2(300, 70),
            TestIdle, TestModeSession.RunAgain, out _);
        Button(card, "Menu", "MAIN MENU", new Vector2(170, -385), new Vector2(300, 70),
            new Color(.30f, .30f, .42f), TestModeSession.ExitToMenu, out _);
    }

    // One row per level, scrolling once a run gets deep enough to need it.
    private static void Table(Transform card, PlaytestReport r)
    {
        var viewport = Rect(card, "Levels", new Vector2(0, -60), new Vector2(1160, 460));
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
                l.level + (l.joinedMidLevel ? "*" : "") + (l.completed ? "" : " ✕"),
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
        rows.Append("\n<size=17><color=#8088AA>* joined mid-level   ✕ not cleared   ♥ friendly ghost caught   L lava   H hunters</color></size>");
        text.text = rows.ToString();

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private static readonly int[] Columns = { 0, 6, 14, 24, 35, 52, 59, 66, 73, 80, 93 };

    private static string Row(params string[] cells)
    {
        var line = new StringBuilder();
        for (int i = 0; i < cells.Length && i < Columns.Length; i++)
            line.Append("<pos=").Append(Columns[i]).Append("%>").Append(cells[i]);
        return line.ToString();
    }

    private static string EndReason(string reason)
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

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        node.SetParent(parent, false);
        node.anchorMin = node.anchorMax = new Vector2(.5f, .5f);
        node.anchoredPosition = position;
        node.sizeDelta = size;
        return node;
    }

    private static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
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

    private static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size,
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
