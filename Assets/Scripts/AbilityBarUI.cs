using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The four ability buttons on the HUD, and the number in each one's badge.
//
// The abilities themselves do not exist yet - this is the art and the counters
// going in ahead of them. So all this does is hold a count per ability and put
// it in the badge; pressing a button does nothing until something subscribes to
// it. Whatever ends up owning trap, freeze, shield and teleport calls SetCount
// as it spends and buys them, and takes the buttons from GetButton.
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

    private readonly int[] _held = new int[Count];
    private RectTransform _bar;

    void Awake()
    {
        Instance = this;
        _bar = (RectTransform)transform;
        for (int i = 0; i < Count; i++)
            SetCount((Ability)i, startingCounts != null && i < startingCounts.Length ? startingCounts[i] : 0);
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
