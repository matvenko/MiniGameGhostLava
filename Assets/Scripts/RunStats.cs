using System;
using UnityEngine;

// The run being played right now: how deep it has got, what it has picked up on
// the way, and how long it has been going.
//
// Separate from EconomyManager on purpose. The wallet is a balance that outlives
// the run and is spent down in the shop; this is a tally that starts at nothing
// every time the board loads and only ever goes up. The same coin feeds both,
// and they are meant to disagree - that is what stops a well-stocked shopper
// from topping the table without playing.
//
// Every change is pushed straight into the Leaderboard rather than held until
// the run ends, so nothing is riding on the game actually being told that it
// did (see Leaderboard.Submit).
public static class RunStats
{
    public static bool Active { get; private set; }
    public static int Level { get; private set; }
    public static int Coins { get; private set; }

    // The row this run owns in the table, so it can keep rewriting that one row.
    public static string RunId { get; private set; }

    // The difficulty is taken once, at the start, rather than read per coin: it
    // decides which table the run belongs to, and a run should not be able to
    // change tables halfway through.
    public static Difficulty Mode { get; private set; }

    private static float _startedAt;

    public static int Seconds => Active ? Mathf.Max(0, Mathf.RoundToInt(Time.unscaledTime - _startedAt)) : 0;

    // Called as the board is laid out for a new run - LevelManager.Awake, which
    // is the one moment that means "this is a fresh attempt" no matter whether
    // the player came from the menu, from quitting, or from a restart.
    //
    // The level the board is being laid out at comes with it, so a run resumed
    // from a save (see RunProgress) is counted from the depth it actually starts
    // at rather than being asked to climb back up to it.
    public static void Begin(int startLevel = 1)
    {
        Active = true;
        Level = Mathf.Max(1, startLevel);
        Coins = 0;
        RunId = Guid.NewGuid().ToString("N");
        Mode = DifficultySettings.Current;
        // Unscaled: the level-complete popup and the pause menu both stop the
        // clock the game plays on, and neither is a reason to say the run was
        // faster than it was.
        _startedAt = Time.unscaledTime;
    }

    public static void CoinCollected()
    {
        if (!Active) return;
        Coins++;
        Record();
    }

    public static void ReachedLevel(int level)
    {
        if (!Active) return;
        Level = Mathf.Max(Level, level);
        Record();
    }

    // A run that has not left the first tile has nothing to say, and writing it
    // down would fill the table with rows for every time the game was opened and
    // put down again.
    //
    // Nor does a run the test bot is playing on a copy of the save: it is not the
    // player's run, and the table is theirs.
    private static void Record()
    {
        if (Level <= 1 && Coins <= 0) return;
        if (RunProgress.Sandboxed) return;
        Leaderboard.Submit(Mode, new RunRecord
        {
            runId = RunId,
            level = Level,
            coins = Coins,
            seconds = Seconds,
            playedAtTicks = DateTime.UtcNow.Ticks
        });
    }

    // Statics survive the play session when the editor skips the domain reload,
    // which would leave the next run adding to the last one's totals.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewRun()
    {
        Active = false;
        Level = 1;
        Coins = 0;
        RunId = null;
    }
}
