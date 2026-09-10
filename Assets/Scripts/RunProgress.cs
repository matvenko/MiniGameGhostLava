using UnityEngine;

// The saved game: how far the player got, the wallet they got there with, and
// the abilities still in their pocket. One save per difficulty.
//
// Two saves, never one, for the same reason the Leaderboard keeps two tables
// (see Leaderboard): normal walks the hunters at 0.6 speed, pays the good end
// of every coin and hands out a spare life, so a level reached there is not the
// same achievement as the same level on hard - and a wallet filled on normal is
// not a wallet earned on hard. Switching mode on the menu switches saves whole:
// level, coins and abilities all follow the pills.
//
// This is the single owner of everything that outlives a run. EconomyManager,
// ShieldManager, FreezeManager, TeleportManager and TrapManager each keep their
// own live count while the board is up and hand it here to be written down, so
// there is exactly one place that knows what "progress" means and exactly one
// place NEW GAME has to clear.
public static class RunProgress
{
    private const string Prefix = "progress.";
    private const string MigratedKey = "progress.migrated";

    // Where a single shared save used to live, before there was one per mode.
    private const string LegacyWalletKey = "wallet_coins";
    private const string LegacyShieldsKey = "shields_owned";
    private const string LegacyFreezesKey = "freezes_owned";
    private const string LegacyTeleportsKey = "teleports_owned";
    private const string LegacyTrapsKey = "traps_owned";

    public static int Level
    {
        get => Mathf.Max(1, Read("level", 1));
        set => Write("level", Mathf.Max(1, value));
    }

    public static int Coins { get => Read("coins", 0); set => Write("coins", Mathf.Max(0, value)); }
    public static int Shields { get => Read("shields", 0); set => Write("shields", Mathf.Max(0, value)); }
    public static int Freezes { get => Read("freezes", 0); set => Write("freezes", Mathf.Max(0, value)); }
    public static int Teleports { get => Read("teleports", 0); set => Write("teleports", Mathf.Max(0, value)); }
    public static int Traps { get => Read("traps", 0); set => Write("traps", Mathf.Max(0, value)); }

    // Lives left in the saved run. A save that has never written one down is a
    // run that has not started losing yet, so it reads as a full set for the
    // mode it belongs to (see DifficultySettings.StartingLives).
    public static int Lives
    {
        get => LivesOf(DifficultySettings.Current);
        set => Write("lives", Mathf.Max(0, value));
    }

    public static int LivesOf(Difficulty mode)
    {
        int stored = Read(mode, "lives", -1);
        return stored >= 0 ? stored : DifficultySettings.StartingLives(DifficultySettings.AuthoredStartingLives);
    }

    // Walking away from a board costs a life, whichever door was used: the pause
    // menu, or the game over screen once the last one is already spent. It is
    // the price of keeping the level, the wallet and the abilities, and it is
    // what eventually empties the run out and makes a continue cost an ad.
    public static void LeaveRun()
    {
        Started = true;
        Lives = Mathf.Max(0, Lives - 1);
    }

    // Whether a board has ever been laid out on this save. Without it a run that
    // was quit on level one, before it had picked anything up, would look
    // exactly like a game that was never started - and its spent lives would
    // quietly vanish.
    public static bool Started
    {
        get => Read("started", 0) != 0;
        set => Write("started", value ? 1 : 0);
    }
    // Whether there is anything worth continuing. A game that was never started,
    // never left level one and never picked anything up is indistinguishable from
    // a new game, so the menu offers to start one instead of resuming it.
    public static bool Exists(Difficulty mode) =>
        Read(mode, "started", 0) != 0 || Read(mode, "level", 1) > 1
        || Read(mode, "coins", 0) > 0 || AbilitiesOwned(mode) > 0;

    public static int AbilitiesOwned(Difficulty mode) =>
        Read(mode, "shields", 0) + Read(mode, "freezes", 0)
        + Read(mode, "teleports", 0) + Read(mode, "traps", 0);

    public static int LevelOf(Difficulty mode) => Mathf.Max(1, Read(mode, "level", 1));
    public static int CoinsOf(Difficulty mode) => Read(mode, "coins", 0);

    // What NEW GAME does. Deleting rather than zeroing, so a save that has been
    // started over reads exactly like one that never existed.
    public static void Reset(Difficulty mode)
    {
        foreach (string field in new[] { "level", "coins", "shields", "freezes", "teleports", "traps", "lives", "started" })
            PlayerPrefs.DeleteKey(Key(mode, field));
        PlayerPrefs.Save();
    }

    private static int Read(string field, int fallback) => Read(DifficultySettings.Current, field, fallback);

    private static int Read(Difficulty mode, string field, int fallback)
    {
        Migrate();
        return PlayerPrefs.GetInt(Key(mode, field), fallback);
    }

    private static void Write(string field, int value)
    {
        Migrate();
        PlayerPrefs.SetInt(Key(DifficultySettings.Current, field), value);
        PlayerPrefs.Save();
    }

    private static string Key(Difficulty mode, string field) => Prefix + mode + "." + field;

    // A player who already has coins and abilities from before the save was
    // split in two keeps them, in the mode they were last playing - which is the
    // only mode they can have earned them in. The other mode starts empty, which
    // is what it would have done had it always been kept apart.
    private static bool _migrated;

    private static void Migrate()
    {
        if (_migrated) return;
        _migrated = true;
        if (PlayerPrefs.GetInt(MigratedKey, 0) != 0) return;
        PlayerPrefs.SetInt(MigratedKey, 1);

        Difficulty mode = DifficultySettings.Current;
        Carry(LegacyWalletKey, Key(mode, "coins"));
        Carry(LegacyShieldsKey, Key(mode, "shields"));
        Carry(LegacyFreezesKey, Key(mode, "freezes"));
        Carry(LegacyTeleportsKey, Key(mode, "teleports"));
        Carry(LegacyTrapsKey, Key(mode, "traps"));
        PlayerPrefs.Save();
    }

    private static void Carry(string from, string to)
    {
        if (!PlayerPrefs.HasKey(from)) return;
        PlayerPrefs.SetInt(to, PlayerPrefs.GetInt(from, 0));
        PlayerPrefs.DeleteKey(from);
    }
}
