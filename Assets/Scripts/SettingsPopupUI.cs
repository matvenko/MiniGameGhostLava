using UnityEngine;
using UnityEngine.UI;

// The controls in the settings popup: two checkboxes and the four cards that say
// where the ability bar goes.
//
// It owns nothing but the drawing. A press writes to GameSettings and GameSettings
// tells the stick and the ability bar; this then redraws itself from what
// GameSettings says rather than from what it just did, so the popup can never show
// one thing while the board does another.
//
// The whole row is the press target, not the little box on it - the box is 50
// canvas units across and a thumb is not.
public class SettingsPopupUI : MonoBehaviour
{
    [Header("HUD options")]
    [SerializeField] private Button hideJoystickRow;
    [SerializeField] private Image hideJoystickBox;
    [SerializeField] private Button hideEmptyAbilitiesRow;
    [SerializeField] private Image hideEmptyAbilitiesBox;
    [SerializeField] private Sprite boxChecked;
    [SerializeField] private Sprite boxUnchecked;

    [Header("Abilities position")]
    [Tooltip("Left Top, Right Top, Left Bottom, Right Bottom - the order GameSettings.AbilityCorner is in.")]
    [SerializeField] private Button[] cardButtons = new Button[4];
    [SerializeField] private Image[] cardPanels = new Image[4];
    [SerializeField] private Image[] cardRadios = new Image[4];
    [SerializeField] private Sprite cardPicked;
    [SerializeField] private Sprite cardUnpicked;
    [SerializeField] private Sprite radioPicked;
    [SerializeField] private Sprite radioUnpicked;

    void Awake()
    {
        if (hideJoystickRow != null)
            hideJoystickRow.onClick.AddListener(() => GameSettings.HideJoystick = !GameSettings.HideJoystick);

        if (hideEmptyAbilitiesRow != null)
            hideEmptyAbilitiesRow.onClick.AddListener(() =>
                GameSettings.HideEmptyAbilities = !GameSettings.HideEmptyAbilities);

        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] == null) continue;
            var corner = (GameSettings.AbilityCorner)i;
            cardButtons[i].onClick.AddListener(() => GameSettings.Corner = corner);
        }
    }

    void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GameSettings.Changed -= Refresh;
    }

    private void Refresh()
    {
        Tick(hideJoystickBox, GameSettings.HideJoystick);
        Tick(hideEmptyAbilitiesBox, GameSettings.HideEmptyAbilities);

        int chosen = (int)GameSettings.Corner;
        for (int i = 0; i < cardPanels.Length; i++)
        {
            bool picked = i == chosen;
            if (cardPanels[i] != null) cardPanels[i].sprite = picked ? cardPicked : cardUnpicked;
            if (i < cardRadios.Length && cardRadios[i] != null)
                cardRadios[i].sprite = picked ? radioPicked : radioUnpicked;
        }
    }

    private void Tick(Image box, bool on)
    {
        if (box != null) box.sprite = on ? boxChecked : boxUnchecked;
    }
}
