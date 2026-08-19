using UnityEngine;

public enum Difficulty
{
    Easy,
    Hard
}

// Which difficulty the player picked on the main menu, plus every rule that
// actually differs between the two. Kept in PlayerPrefs so the choice
// survives the menu -> gameplay scene load (and the scene reload behind
// "main menu" on the Game Over screen), and so the last pick is remembered
// between sessions.
//
// Hard is the game exactly as it was tuned before difficulties existed: it
// never scales or overrides anything, so whatever is authored on a
// component in the scene is what hard mode plays with. Easy is aimed at
// younger players - enemies chase noticeably slower, and a coin is worth
// enough that shop progress doesn't stall while they're learning the board.
//
// Deliberately one file: the two modes only differ by these numbers, so
// adding a third difference later means adding it here rather than hunting
// for scattered "if easy" checks.
public static class DifficultySettings
{
    private const string ModeKey = "difficulty_mode";

    // Fraction of its authored speed an enemy actually moves at in easy
    // mode. Enemies are authored at 2 - 2.5 against a player speed of 4, so
    // 0.6 (1.2 - 1.5) leaves them slow enough that a small child can walk
    // away from one on purpose instead of only ever being caught.
    public const float EasyEnemySpeedMultiplier = 0.6f;

    // What a single coin adds to the wallet in easy mode, replacing the
    // per-coin value authored on the prefab (50 at the time of writing).
    public const int EasyCoinWalletValue = 100;

    private static bool _loaded;
    private static Difficulty _current;

    public static Difficulty Current
    {
        get
        {
            if (!_loaded)
            {
                _current = (Difficulty)PlayerPrefs.GetInt(ModeKey, (int)Difficulty.Hard);
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

    public static bool IsEasy => Current == Difficulty.Easy;

    // Both rules take the authored value and hand back what this mode should
    // use, so hard mode stays a pure pass-through and per-instance tuning in
    // the scene (each enemy has its own speed) keeps working.
    public static float EnemySpeed(float authoredSpeed)
    {
        return IsEasy ? authoredSpeed * EasyEnemySpeedMultiplier : authoredSpeed;
    }

    public static int CoinWalletValue(int authoredValue)
    {
        return IsEasy ? EasyCoinWalletValue : authoredValue;
    }
}
