using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The four ability buttons down the left of the HUD, and the number in each
// one's badge.
//
// The abilities themselves do not exist yet - this is the art and the counters
// going in ahead of them. So all this does is hold a count per ability and put
// it in the badge; pressing a button does nothing until something subscribes to
// it. Whatever ends up owning trap, freeze, shield and teleport calls SetCount
// as it spends and buys them, and takes the buttons from GetButton.
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

    private readonly int[] _held = new int[Count];

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < Count; i++)
            SetCount((Ability)i, startingCounts != null && i < startingCounts.Length ? startingCounts[i] : 0);
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
    }
}
