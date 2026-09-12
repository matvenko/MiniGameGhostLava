using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The four ability buttons on the HUD, and the number in each one's badge.
//
// All this does is hold a count per ability and put it in the badge; pressing a
// button does nothing until something subscribes to it. All four - trap,
// freeze, shield, teleport - have a manager that does: it calls SetCount as it
// spends and buys them, and takes its button from GetButton.
//
// Where the bar sits is the player's, not ours: any of the four corners, a column
// in the top two and a row along the bottom two, which is what the pictures in
// the settings popup are showing. The bar lays itself out from that choice rather
// than from anything placed by hand in the scene, so a count reaching zero can
// take a button out of the line and close the gap behind it in the same pass.
//
// A badge is one digit wide, which is where MaxCount comes from: the artist drew
// a single figure in a circle and nine is as far as that goes.
public class AbilityBarUI : MonoBehaviour
{
    public enum Ability { Trap = 0, Freeze = 1, Shield = 2, Teleport = 3 }
    public const int Count = 4;
    public const int MaxCount = 9;

    public static AbilityBarUI Instance { get; private set; }

    [SerializeField] private Button[] buttons = new Button[Count];
    [SerializeField] private TextMeshProUGUI[] countLabels = new TextMeshProUGUI[Count];
    [Tooltip("What each ability starts a run with, until something owns them properly.")]
    [SerializeField] private int[] startingCounts = { 2, 2, 2, 2 };

    [Header("Layout")]
    [Tooltip("One button, square, in canvas units.")]
    [SerializeField] private float buttonSize = 106f;
    [Tooltip("Centre to centre along the bar.")]
    [SerializeField] private float step = 117f;
    [Tooltip("How far in from the side edge the bar sits.")]
    [SerializeField] private float sideMargin = 36f;
    [Tooltip("How far down from the top, on the left - under the coin counter.")]
    [SerializeField] private float topMarginLeft = 132f;
    [Tooltip("And on the right, where the lives pill and the wallet stack deeper.")]
    [SerializeField] private float topMarginRight = 215f;
    [Tooltip("How far up from the bottom edge, in the bottom two corners.")]
    [SerializeField] private float bottomMargin = 36f;

    // Trap, freeze, shield, teleport, on the controller: A, X, Y, B, in the
    // colours an Xbox pad prints them in.
    private static readonly PadButton[] PadButtons = { PadButton.South, PadButton.West, PadButton.North, PadButton.East };
    private static readonly string[] PadLetters = { "A", "X", "Y", "B" };
    private static readonly Color[] PadColours =
    {
        new Color(.45f, .85f, .30f), new Color(.30f, .60f, 1f), new Color(1f, .82f, .18f), new Color(.95f, .32f, .28f)
    };

    private readonly int[] _held = new int[Count];
    private RectTransform _bar;
    private GameObject[] _padLetters;
    private bool _padLettersShown;

    void Awake()
    {
        Instance = this;
        _bar = (RectTransform)transform;
        for (int i = 0; i < Count; i++)
            SetCount((Ability)i, startingCounts != null && i < startingCounts.Length ? startingCounts[i] : 0);
        BuildPadLetters();
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplyLayout;
        ApplyLayout();
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplyLayout;
    }

    // On a keyboard the number keys press the buttons: 1 is the first ability the
    // player has any of, 2 the next, and so on in bar order - so with only freeze
    // and shield bought, 1 is freeze and 2 is shield.
    //
    // A controller's face buttons go the other way: each is always the same
    // ability, whatever else is bought - A trap, X freeze, Y shield, B teleport -
    // with its letter drawn in the corner of the button while the controller is in
    // use. Nobody looks down at a controller mid-run, and a button whose meaning
    // moves when something new is bought is a button pressed wrong.
    //
    // Either way the press goes through the button itself, which is what a tap
    // does, so each manager's own refusals still apply. What a tap can't reach -
    // anything behind the pause, shop or level-complete popups, all of which stop
    // the clock - a key can't either.
    void Update()
    {
        ShowPadLetters(Pad.InUse);
        if (!AcceptsKeys()) return;

        int slot = PressedDigit();
        for (int i = 0; slot >= 0 && i < Count; i++)
        {
            if (_held[i] > 0 && slot-- == 0) Press(i);
        }

        // The frame a menu had the controller is the menu's: the A that pressed
        // Resume is not also a trap on the way out (see GamepadMenus).
        if (GamepadMenus.BusyFrame == Time.frameCount) return;
        for (int i = 0; i < Count; i++)
            if (Pad.Pressed(PadButtons[i])) Press(i);
    }

    private void Press(int i)
    {
        var button = buttons != null && i < buttons.Length ? buttons[i] : null;
        if (button != null && button.isActiveAndEnabled && button.interactable)
            button.onClick.Invoke();
    }

    // The controller letter in the top corner of each button; the count badge has
    // the bottom one. A shadowed pair of labels rather than an outline, which would
    // mean a material of its own per label.
    private void BuildPadLetters()
    {
        if (buttons == null) return;
        _padLetters = new GameObject[Count];
        for (int i = 0; i < Count && i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var corner = new GameObject("Pad " + PadLetters[i], typeof(RectTransform));
            var rect = (RectTransform)corner.transform;
            rect.SetParent(buttons[i].transform, false);
            rect.anchorMin = new Vector2(.02f, .68f);
            rect.anchorMax = new Vector2(.32f, .98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TMP_FontAsset font = countLabels != null && i < countLabels.Length && countLabels[i] != null ? countLabels[i].font : null;
            PadLetter(rect, font, new Vector2(2f, -2f), new Color(.08f, .05f, .16f, .9f), PadLetters[i]);
            PadLetter(rect, font, Vector2.zero, PadColours[i], PadLetters[i]);

            corner.SetActive(false);
            _padLetters[i] = corner;
        }
    }

    private static void PadLetter(RectTransform corner, TMP_FontAsset font, Vector2 offset, Color colour, string letter)
    {
        var rect = new GameObject(letter, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(corner, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = offset;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = letter;
        label.fontSize = 28;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = colour;
        label.raycastTarget = false;
    }

    private void ShowPadLetters(bool show)
    {
        if (_padLetters == null || show == _padLettersShown) return;
        _padLettersShown = show;
        foreach (var letter in _padLetters)
            if (letter != null) letter.SetActive(show);
    }

    private static bool AcceptsKeys()
    {
        if (Time.timeScale == 0f) return false;
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return false;
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive) return false;
        if (GuideBookUI.Instance != null && GuideBookUI.Instance.IsOpen) return false;
        return true;
    }

    // Which of 1-4 went down this frame, top row or keypad, counted from zero;
    // -1 for none.
    private static int PressedDigit()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 3;
#endif
        return -1;
    }

    public Button GetButton(Ability ability)
    {
        int i = (int)ability;
        return buttons != null && i < buttons.Length ? buttons[i] : null;
    }

    public int GetCount(Ability ability) => _held[(int)ability];

    public void SetCount(Ability ability, int value)
    {
        int i = (int)ability;
        _held[i] = Mathf.Clamp(value, 0, MaxCount);
        if (countLabels != null && i < countLabels.Length && countLabels[i] != null)
            countLabels[i].text = _held[i].ToString();

        // Spending the last one of something can take its button out of the bar,
        // and buying one back puts it in again.
        ApplyLayout();
    }

    // Puts the bar in its corner and the buttons in it, in order, leaving out any
    // the player has none of and has asked not to see. Everything is set here
    // rather than trusted to the scene - the anchors as well as the positions -
    // so a bar that was last laid out down the left corner can be laid out along
    // the bottom right without anything left over from before.
    public void ApplyLayout()
    {
        if (_bar == null || buttons == null) return;

        bool left = GameSettings.AbilitiesOnLeft;
        bool top = GameSettings.AbilitiesOnTop;
        bool stacked = GameSettings.AbilitiesStacked;
        bool hideEmpty = GameSettings.HideEmptyAbilities;

        int shown = 0;
        for (int i = 0; i < Count && i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            bool visible = !hideEmpty || _held[i] > 0;
            buttons[i].gameObject.SetActive(visible);
            if (!visible) continue;

            var slot = (RectTransform)buttons[i].transform;
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0f, 1f);
            slot.anchoredPosition = stacked
                ? new Vector2(0f, -shown * step)
                : new Vector2(shown * step, 0f);
            shown++;
        }

        // The bar is only as long as what is left in it, so it stays against its
        // corner instead of hanging off an empty tail.
        float span = shown > 0 ? shown * step - (step - buttonSize) : 0f;
        _bar.sizeDelta = stacked ? new Vector2(buttonSize, span) : new Vector2(span, buttonSize);

        var corner = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
        _bar.anchorMin = _bar.anchorMax = _bar.pivot = corner;
        _bar.anchoredPosition = new Vector2(
            left ? sideMargin : -sideMargin,
            top ? -(left ? topMarginLeft : topMarginRight) : bottomMargin);
    }
}
