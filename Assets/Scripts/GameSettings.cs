using System;
using UnityEngine;

// What the player has chosen in the settings popup, and where it is kept.
//
// Three small facts that outlive a run: whether the stick is drawn, whether an
// ability button goes away when its count reaches zero, and which corner the
// ability bar sits in. They come out of PlayerPrefs the first time anything asks
// and go back the moment they change, so the game comes up the way it was left -
// after a level, after a quit, after an install that survives.
//
// Whoever answers to a setting listens for Changed rather than being told about
// it. The popup writes; the stick and the ability bar hear. That is what lets the
// board rearrange itself behind the popup while the popup is still open, which is
// the whole point of showing four pictures of the layout: you pick one and you
// can see what you picked.
public static class GameSettings
{
    public enum AbilityCorner { LeftTop = 0, RightTop = 1, LeftBottom = 2, RightBottom = 3 }

    private const string HideJoystickKey = "hud.hideJoystick";
    private const string HideEmptyAbilitiesKey = "hud.hideEmptyAbilities";
    private const string AbilityCornerKey = "hud.abilityCorner";

    public static event Action Changed;

    private static bool _loaded;
    private static bool _hideJoystick;
    private static bool _hideEmptyAbilities;
    private static AbilityCorner _corner;

    // The stick stops being drawn; it does not stop working. Steering is a press
    // anywhere on the board either way, so hiding it only takes the picture away
    // from a player who has stopped needing it.
    public static bool HideJoystick
    {
        get { Load(); return _hideJoystick; }
        set
        {
            Load();
            if (_hideJoystick == value) return;
            _hideJoystick = value;
            PlayerPrefs.SetInt(HideJoystickKey, value ? 1 : 0);
            Commit();
        }
    }

    // An ability with nothing left in it leaves the bar rather than sitting there
    // greyed out, and the ones below it close the gap.
    public static bool HideEmptyAbilities
    {
        get { Load(); return _hideEmptyAbilities; }
        set
        {
            Load();
            if (_hideEmptyAbilities == value) return;
            _hideEmptyAbilities = value;
            PlayerPrefs.SetInt(HideEmptyAbilitiesKey, value ? 1 : 0);
            Commit();
        }
    }

    public static AbilityCorner Corner
    {
        get { Load(); return _corner; }
        set
        {
            Load();
            if (_corner == value) return;
            _corner = value;
            PlayerPrefs.SetInt(AbilityCornerKey, (int)value);
            Commit();
        }
    }

    public static bool AbilitiesOnLeft => Corner == AbilityCorner.LeftTop || Corner == AbilityCorner.LeftBottom;
    public static bool AbilitiesOnTop => Corner == AbilityCorner.LeftTop || Corner == AbilityCorner.RightTop;

    // The bar runs down the screen in the top corners and across it in the bottom
    // ones, which is what the two pictures either side of each card are saying.
    public static bool AbilitiesStacked => AbilitiesOnTop;

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        // The defaults are the state the design was drawn in: stick shown, empty
        // abilities hidden, bar down the left.
        _hideJoystick = PlayerPrefs.GetInt(HideJoystickKey, 0) != 0;
        _hideEmptyAbilities = PlayerPrefs.GetInt(HideEmptyAbilitiesKey, 1) != 0;
        _corner = (AbilityCorner)Mathf.Clamp(PlayerPrefs.GetInt(AbilityCornerKey, (int)AbilityCorner.LeftTop), 0, 3);
    }

    private static void Commit()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // Statics are not reloaded between runs when the editor is set to skip the
    // domain reload, which would leave last run's listeners subscribed to objects
    // that no longer exist.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewRun()
    {
        Changed = null;
        _loaded = false;
    }
}
