using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// What happened in a test run, written down as it happens and summed up when it
// ends. Two views of the same run travel together in one file: the raw event
// list, which is what a dashboard can re-slice any way it likes later, and the
// per-level summary, which is what the report card shows and what anyone opening
// the file wants to read first.
//
// The game calls in from wherever a thing actually happens - the coin, the
// ability managers, the shop, the death - the same way it already tells RunStats.
// Outside a test run every call here is a no-op.

[Serializable]
public class PlaytestEvent
{
    // Game seconds since the run started: the clock the board plays on, so a
    // run at double speed reads the same as the same run played at normal speed.
    public float t;
    public int level;
    // run_start, level_start, coin, friendly_ghost, ability, purchase, death,
    // level_complete, speed, run_end
    public string type;
    // Which ability, which item, what killed the player, why the run ended.
    public string detail;
    // Coins paid or spent, lives left after a death, coins on the board.
    public int value;
    // Tiles to the nearest hunter when it happened; -1 when none was out.
    public float nearestEnemy = -1f;
    public float x;
    public float z;
}

[Serializable]
public class PlaytestLevel
{
    public int level;
    // The bot took over a board a player had already started: its coins and
    // its clock only count from the moment the test began.
    public bool joinedMidLevel;
    public bool completed;
    public float seconds;
    public int coinsOnBoard;
    public int coinsCollected;
    public int walletEarned;
    public int friendlyGhostsCaught;
    public int deaths;
    public int lavaDeaths;
    public int enemyDeaths;
    public int traps;
    public int freezes;
    public int shields;
    public int teleports;
    public int purchases;
    public int coinsSpent;
    public int livesAtStart;
    public int livesAtEnd;
}

[Serializable]
public class PlaytestInventory
{
    public int lives;
    public int wallet;
    public int traps;
    public int freezes;
    public int shields;
    public int teleports;
}

[Serializable]
public class PlaytestReport
{
    // Bumped whenever a field changes meaning, so a dashboard can tell files apart.
    public string schema = "mazeboo.playtest/1";
    public string runId;
    public string startedAtUtc;
    public string endedAtUtc;
    // out_of_lives, stopped, left_board
    public string endReason;
    public string difficulty;
    public string botProfile;
    public float speed;
    public string gameVersion;
    public string unityVersion;
    public string platform;
    public int startLevel;
    public int endLevel;
    public int levelsCompleted;
    public float gameSeconds;
    public float realSeconds;
    public int coinsCollected;
    public int walletEarned;
    public int coinsSpent;
    public int deaths;
    public int lavaDeaths;
    public int enemyDeaths;
    public int abilitiesUsed;
    public int purchases;
    // The level the most lives were lost on; 0 for a run that never died.
    public int mostDeathsLevel;
    public PlaytestInventory start;
    public PlaytestInventory end;
    public List<PlaytestLevel> levels = new List<PlaytestLevel>();
    public List<PlaytestEvent> events = new List<PlaytestEvent>();
}

public static class PlaytestLog
{
    public static bool Recording => _report != null && !_finished;

    // The last run to finish, and the file it was written to - what the report
    // card reads, and what the batch runner hands back.
    public static PlaytestReport LastReport { get; private set; }
    public static string LastFilePath { get; private set; }

    public static event Action<PlaytestReport> Finished;

    private static PlaytestReport _report;
    private static PlaytestLevel _level;
    private static bool _finished;
    private static float _startTime;
    private static float _startRealTime;
    private static float _levelStartTime;
    private static Sample.GhostScript _player;

    public static void Begin(string botProfile, float speed)
    {
        _finished = false;
        _startTime = Time.time;
        _startRealTime = Time.unscaledTime;
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 1;

        _report = new PlaytestReport
        {
            runId = Guid.NewGuid().ToString("N"),
            startedAtUtc = DateTime.UtcNow.ToString("o"),
            difficulty = DifficultySettings.Current.ToString(),
            botProfile = botProfile,
            speed = speed,
            gameVersion = Application.version,
            unityVersion = Application.unityVersion,
            platform = Application.isEditor ? "Editor" : Application.platform.ToString(),
            startLevel = level,
            start = Inventory()
        };
        Add("run_start", botProfile, 0);
        OpenLevel(level, Coin.Active.Count, joinedMidLevel: true);
    }

    public static void LevelStarted(int level, int coinsOnBoard)
    {
        if (!Recording) return;
        if (_level != null && !_level.completed) CloseLevel();
        OpenLevel(level, coinsOnBoard, joinedMidLevel: false);
    }

    public static void LevelCompleted(int level)
    {
        if (!Recording || _level == null) return;
        _level.completed = true;
        Add("level_complete", null, level);
        CloseLevel();
    }

    public static void CoinCollected(int walletValue)
    {
        if (!Recording || _level == null) return;
        _level.coinsCollected++;
        _level.walletEarned += walletValue;
        Add("coin", null, walletValue);
    }

    public static void FriendlyGhostCaught(int reward)
    {
        if (!Recording || _level == null) return;
        _level.friendlyGhostsCaught++;
        _level.walletEarned += reward;
        Add("friendly_ghost", null, reward);
    }

    public static void AbilityUsed(AbilityBarUI.Ability ability)
    {
        if (!Recording || _level == null) return;
        switch (ability)
        {
            case AbilityBarUI.Ability.Trap: _level.traps++; break;
            case AbilityBarUI.Ability.Freeze: _level.freezes++; break;
            case AbilityBarUI.Ability.Shield: _level.shields++; break;
            case AbilityBarUI.Ability.Teleport: _level.teleports++; break;
        }
        Add("ability", ability.ToString().ToLowerInvariant(), 0);
    }

    public static void Purchased(string item, int cost)
    {
        if (!Recording || _level == null) return;
        _level.purchases++;
        _level.coinsSpent += cost;
        Add("purchase", item, cost);
    }

    // The report's speed is the one the run started at; changes along the way
    // are events, in whole multiples.
    public static void SpeedChanged(float speed)
    {
        if (!Recording) return;
        Add("speed", null, Mathf.RoundToInt(speed));
    }

    // Logged before the life is taken, so what is left afterwards is one fewer
    // than what the player has right now.
    public static void Died(string cause)
    {
        if (!Recording || _level == null) return;
        _level.deaths++;
        if (cause == "lava") _level.lavaDeaths++;
        else _level.enemyDeaths++;
        int livesLeft = LivesManager.Instance != null ? Mathf.Max(0, LivesManager.Instance.CurrentLives - 1) : 0;
        Add("death", cause, livesLeft);
    }

    public static void Finish(string reason)
    {
        if (!Recording) return;
        if (_level != null && !_level.completed) CloseLevel();
        Add("run_end", reason, 0);

        var r = _report;
        r.endReason = reason;
        r.endedAtUtc = DateTime.UtcNow.ToString("o");
        r.endLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : r.startLevel;
        r.gameSeconds = Round(Time.time - _startTime);
        r.realSeconds = Round(Time.unscaledTime - _startRealTime);
        r.end = Inventory();

        int worstDeaths = 0;
        foreach (var l in r.levels)
        {
            if (l.completed) r.levelsCompleted++;
            r.coinsCollected += l.coinsCollected;
            r.walletEarned += l.walletEarned;
            r.coinsSpent += l.coinsSpent;
            r.deaths += l.deaths;
            r.lavaDeaths += l.lavaDeaths;
            r.enemyDeaths += l.enemyDeaths;
            r.abilitiesUsed += l.traps + l.freezes + l.shields + l.teleports;
            r.purchases += l.purchases;
            if (l.deaths > worstDeaths)
            {
                worstDeaths = l.deaths;
                r.mostDeathsLevel = l.level;
            }
        }

        _finished = true;
        LastReport = r;
        LastFilePath = Write(r);
        Finished?.Invoke(r);
    }

    private static void OpenLevel(int level, int coinsOnBoard, bool joinedMidLevel)
    {
        _levelStartTime = Time.time;
        _level = new PlaytestLevel
        {
            level = level,
            joinedMidLevel = joinedMidLevel,
            coinsOnBoard = coinsOnBoard,
            livesAtStart = Lives()
        };
        _report.levels.Add(_level);
        Add("level_start", null, coinsOnBoard);
    }

    private static void CloseLevel()
    {
        _level.seconds = Round(Time.time - _levelStartTime);
        _level.livesAtEnd = Lives();
    }

    private static void Add(string type, string detail, int value)
    {
        Vector3 at = PlayerPosition(out bool found);
        _report.events.Add(new PlaytestEvent
        {
            t = Round(Time.time - _startTime),
            level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 0,
            type = type,
            detail = detail,
            value = value,
            nearestEnemy = found ? NearestEnemy(at) : -1f,
            x = Round(at.x),
            z = Round(at.z)
        });
    }

    private static Vector3 PlayerPosition(out bool found)
    {
        if (_player == null) _player = UnityEngine.Object.FindAnyObjectByType<Sample.GhostScript>();
        found = _player != null;
        return found ? _player.transform.position : Vector3.zero;
    }

    private static float NearestEnemy(Vector3 from)
    {
        float nearest = float.MaxValue;
        foreach (var enemy in EnemyChaser.Active)
        {
            if (enemy == null) continue;
            Vector3 d = enemy.transform.position - from;
            d.y = 0f;
            nearest = Mathf.Min(nearest, d.magnitude);
        }
        return nearest == float.MaxValue ? -1f : Round(nearest);
    }

    private static int Lives() => LivesManager.Instance != null ? LivesManager.Instance.CurrentLives : 0;

    private static PlaytestInventory Inventory() => new PlaytestInventory
    {
        lives = Lives(),
        wallet = EconomyManager.Instance != null ? EconomyManager.Instance.TotalCoins : 0,
        traps = TrapManager.Instance != null ? TrapManager.Instance.TrapsOwned : 0,
        freezes = FreezeManager.Instance != null ? FreezeManager.Instance.FreezesOwned : 0,
        shields = ShieldManager.Instance != null ? ShieldManager.Instance.ShieldsOwned : 0,
        teleports = TeleportManager.Instance != null ? TeleportManager.Instance.TeleportsOwned : 0
    };

    private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    // One file per run under the app's own data folder - on a phone that is
    // Android/data/<package>/files/playtests, reachable with adb pull. A file
    // that cannot be written costs the file, not the run.
    private static string Write(PlaytestReport report)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "playtests");
            Directory.CreateDirectory(dir);
            string name = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + report.difficulty.ToLowerInvariant()
                          + "-" + report.botProfile.ToLowerInvariant() + ".json";
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Playtest report written to " + path);
            return path;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Playtest report could not be written: " + e.Message);
            return null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        _report = null;
        _level = null;
        _finished = false;
        _player = null;
        LastReport = null;
        LastFilePath = null;
        Finished = null;
    }
}
