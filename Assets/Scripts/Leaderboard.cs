using System;
using System.Collections.Generic;
using UnityEngine;

// One finished - or still running - trip through the board, as the table keeps
// it. The run's own id travels with it so a run that is still being played can
// keep rewriting its row instead of adding a new one every time it picks up a
// coin (see Submit).
[Serializable]
public class RunRecord
{
    public string runId;
    public int level;
    public int coins;
    public int seconds;
    // Ticks rather than a formatted date: the table is only ever sorted and
    // shown, never parsed back, and ticks survive a change of device locale.
    public long playedAtTicks;

    // Minutes and seconds, for the two places a run is ever read out loud: the
    // menu card and the game over screen. Static because the game over screen
    // reads the clock off the run that is still being played, which has no row
    // of its own to ask.
    public static string Clock(int seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");
}

// How far past runs got, kept separately for each difficulty.
//
// Two tables, never one. Normal walks the hunters at 0.6 speed, pays the good
// end of every coin and hands out a spare life (see DifficultySettings), so a
// level reached there is not the same achievement as the same level reached on
// hard - pooling them would just mean normal owns the whole list.
//
// The wallet is deliberately not the score. EconomyManager's coins are currency:
// they are spent in the shop and halved on defeat, so the player who saves is
// the player who "wins". What a run is worth instead is how deep it got - the
// level it reached, with the coins it took along the way breaking ties.
//
// Kept in PlayerPrefs as one JSON blob per difficulty, in the same place every
// other lasting choice in this game lives. Nothing here talks to a server yet;
// Submit is the single door a future online leaderboard would go behind.
public static class Leaderboard
{
    // Ten is what the menu card shows, so ten is what is worth carrying.
    public const int Capacity = 10;

    private const string KeyPrefix = "leaderboard.";

    // JsonUtility will not serialize a bare list, so the rows travel inside a
    // holder that exists for no other reason.
    [Serializable]
    private class Table
    {
        public List<RunRecord> runs = new List<RunRecord>();
    }

    private static readonly Dictionary<Difficulty, Table> Cache = new Dictionary<Difficulty, Table>();

    // Best first. A level past the end of the list is the whole point of the
    // game, so it outranks everything; coins settle two runs that got equally
    // deep, and the quicker of two otherwise identical runs takes it from there.
    private static int Compare(RunRecord a, RunRecord b)
    {
        if (a.level != b.level) return b.level.CompareTo(a.level);
        if (a.coins != b.coins) return b.coins.CompareTo(a.coins);
        return a.seconds.CompareTo(b.seconds);
    }

    public static IReadOnlyList<RunRecord> Top(Difficulty difficulty) => Load(difficulty).runs;

    public static RunRecord Best(Difficulty difficulty)
    {
        var runs = Load(difficulty).runs;
        return runs.Count > 0 ? runs[0] : null;
    }

    // The record to beat from the point of view of a run that is already in the
    // table: itself does not count as its own record to beat.
    public static RunRecord BestExcluding(Difficulty difficulty, string runId)
    {
        foreach (var run in Load(difficulty).runs)
            if (run.runId != runId) return run;
        return null;
    }

    // Where a run currently sits, 1 for the top of the table; 0 when it has not
    // earned a place at all.
    public static int RankOf(Difficulty difficulty, string runId)
    {
        var runs = Load(difficulty).runs;
        for (int i = 0; i < runs.Count; i++)
            if (runs[i].runId == runId) return i + 1;
        return 0;
    }

    // Writes a run into its difficulty's table, replacing whatever that same run
    // last wrote.
    //
    // Called every time the run's standing changes rather than once at the end,
    // because there is no reliable end: the game over screen can be walked back
    // by a rewarded ad, quitting from the pause menu goes straight to
    // Application.Quit, and a phone can take the app away mid-level without
    // telling anyone. Upserting on the run's id means the table is always
    // already correct, and a run that never comes back still counts for as far
    // as it actually got.
    public static void Submit(Difficulty difficulty, RunRecord run)
    {
        if (run == null || string.IsNullOrEmpty(run.runId)) return;

        var table = Load(difficulty);
        table.runs.RemoveAll(existing => existing.runId == run.runId);
        table.runs.Add(run);
        table.runs.Sort(Compare);
        if (table.runs.Count > Capacity) table.runs.RemoveRange(Capacity, table.runs.Count - Capacity);

        PlayerPrefs.SetString(KeyPrefix + difficulty, JsonUtility.ToJson(table));
        PlayerPrefs.Save();
    }

    private static Table Load(Difficulty difficulty)
    {
        if (Cache.TryGetValue(difficulty, out var cached)) return cached;

        Table table = null;
        string stored = PlayerPrefs.GetString(KeyPrefix + difficulty, null);
        if (!string.IsNullOrEmpty(stored))
        {
            // A table that will not parse - an install from before this existed,
            // a half-written string - costs the player their history and nothing
            // else, so it is swallowed rather than thrown at them mid-menu.
            try { table = JsonUtility.FromJson<Table>(stored); }
            catch (Exception) { table = null; }
        }

        table ??= new Table();
        table.runs ??= new List<RunRecord>();
        table.runs.Sort(Compare);
        Cache[difficulty] = table;
        return table;
    }

    // The cache outlives a run when the editor is set to skip the domain reload,
    // which would otherwise show the menu a table from before the last play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewRun() => Cache.Clear();
}
