using UnityEngine;

public enum Difficulty
{
    Normal,
    Hard
}

// Which mode the player picked on the menu, and every rule that actually
// differs between the two. Kept in PlayerPrefs so the choice survives the menu
// to gameplay scene load - and the scene reload behind "main menu" on the game
// over screen - and so the last pick is remembered between sessions.
//
// Hard is the game exactly as it was tuned before modes existed: it never
// scales or overrides anything, so whatever is authored on a component in the
// scene is what hard plays with. Normal is aimed at younger players - the
// hunters chase slowly enough to be walked away from on purpose, a coin always
// pays the good end of its roll so shop progress does not stall while the board
// is still being learned, and there is one more life to spend learning it.
//
// Deliberately one file: the modes differ only by these numbers, so a third
// difference later is added here rather than as another "if normal" somewhere
// out in the game.
public static class DifficultySettings
{
    private const string ModeKey = "difficulty_mode";

    // Fraction of its authored speed a hunter actually moves at in normal mode.
    // They are authored at 2 - 2.5 against a player speed of 4, so 0.6 leaves
    // them slow enough that a small child can get away on purpose instead of
    // only ever being caught.
    public const float NormalEnemySpeedMultiplier = .6f;

    // What a coin pays in normal mode, in place of the roll authored on the
    // prefab (50 / 100 / 150 / 200 at the time of writing). The good end of
    // that roll, every time.
    public const int NormalCoinWalletValue = 100;

    // Lives a run starts with in normal mode, in place of the authored three.
    public const int NormalStartingLives = 4;

    private static bool _loaded;
    private static Difficulty _current;

    public static Difficulty Current
    {
        get
        {
            if (!_loaded)
            {
                // Normal is the default: a first-time player is as likely to be
                // the child this mode is for as the adult buying the game.
                _current = (Difficulty)PlayerPrefs.GetInt(ModeKey, (int)Difficulty.Normal);
                _loaded = true;
            }
            return _current;
        }
        set
        {
            _current = value;
            _loaded = true;
            PlayerPrefs.SetInt(ModeKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    public static bool IsNormal => Current == Difficulty.Normal;

    // Each rule takes the authored value and hands back what this mode should
    // use, so hard stays a pure pass-through and per-instance tuning in the
    // scene - every hunter has its own speed - keeps working.
    public static float EnemySpeed(float authoredSpeed) =>
        IsNormal ? authoredSpeed * NormalEnemySpeedMultiplier : authoredSpeed;

    public static int CoinWalletValue(int authoredValue) =>
        IsNormal ? NormalCoinWalletValue : authoredValue;

    public static int StartingLives(int authoredLives) =>
        IsNormal ? NormalStartingLives : authoredLives;
}
