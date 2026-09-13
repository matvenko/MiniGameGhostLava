using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reads a folder of playtest runs - each report with the trace beside it, from
// the bot and from recordings of people - and puts the ways they play side by
// side, one column per player (a recorded person, the normal bot, the hard
// bot) and difficulty.
//
// Every number is measured off the trace the same way for all of them, so the
// bot is never compared against what its own settings say it does, only against
// what it was seen doing. Each row names the profile setting it mostly answers
// to (see PlaytestBotProfile): move that setting until the bot's column reads
// like the recordings', and the bot plays like the people who were recorded.
//
// Menu: Tools > Playtest > Compare Runs In Folder... - writes comparison.md into
// the folder it read. Subfolders are read too, so recordings pulled off a phone
// and a batch of bot runs can sit in one folder each under a common one.
public static class PlaytestAnalysis
{
    private const int Dead = 1, Held = 2, Shield = 4, Grace = 8, Card = 16;
    private const float Calm = 3f;

    [MenuItem("Tools/Playtest/Compare Runs In Folder...")]
    private static void Menu()
    {
        string folder = EditorUtility.OpenFolderPanel("Playtest runs", Directory.Exists(ProjectFolder) ? ProjectFolder : DefaultFolder, "");
        if (string.IsNullOrEmpty(folder)) return;
        Show(Write(folder));
    }

    // The recordings off the phone - REC in a development build writes them to
    // the app's own data folder - into Playtests/phone, and straight into a
    // comparison with everything else under Playtests.
    [MenuItem("Tools/Playtest/Pull Recordings From Phone")]
    private static void Pull()
    {
        string adb = Adb();
        if (adb == null)
        {
            EditorUtility.DisplayDialog("Pull recordings", "adb was not found in Unity's Android SDK.", "OK");
            return;
        }

        string target = Path.Combine(ProjectFolder, "phone");
        Directory.CreateDirectory(target);
        string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string remote = "/sdcard/Android/data/" + package + "/files/playtests/.";

        EditorUtility.DisplayProgressBar("Pull recordings", "adb pull " + remote, .5f);
        int code;
        string output;
        try { code = Run(adb, "pull \"" + remote + "\" \"" + target + "\"", out output); }
        finally { EditorUtility.ClearProgressBar(); }

        if (code != 0)
        {
            EditorUtility.DisplayDialog("Pull recordings",
                "adb could not copy the recordings. Is the phone plugged in with USB debugging on, and has REC been played on a development build?\n\n" + output, "OK");
            return;
        }
        Debug.Log("Playtest recordings pulled into " + target + "\n" + output);
        Show(Write(ProjectFolder));
    }

    private static void Show(string output)
    {
        Debug.Log("Playtest comparison written to " + output + "\n" + File.ReadAllText(output));
        EditorUtility.RevealInFinder(output);
    }

    public static string DefaultFolder => Path.Combine(Application.persistentDataPath, "playtests");

    // Where runs are kept to be compared: recordings pulled off the phone in
    // phone/, bot batches wherever -playtestOut pointed - bot/ by habit.
    public static string ProjectFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Playtests");

    private static string Adb()
    {
        string exe = Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";
        foreach (string sdk in new[]
                 {
                     Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK"),
                     EditorPrefs.GetString("AndroidSdkRoot", "")
                 })
        {
            if (string.IsNullOrEmpty(sdk)) continue;
            string path = Path.Combine(sdk, "platform-tools", exe);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static int Run(string exe, string arguments, out string output)
    {
        var info = new System.Diagnostics.ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (var process = System.Diagnostics.Process.Start(info))
        {
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(120000);
            output = (stdout + "\n" + stderr).Trim();
            return process.HasExited ? process.ExitCode : -1;
        }
    }

    public static string Write(string folder)
    {
        string output = Path.Combine(folder, "comparison.md");
        File.WriteAllText(output, Analyze(folder));
        return output;
    }

    public static string Analyze(string folder)
    {
        var groups = new SortedDictionary<string, Group>();
        int skipped = 0;
        foreach (string file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".trace.json", StringComparison.OrdinalIgnoreCase)) continue;
            PlaytestReport report;
            try { report = JsonUtility.FromJson<PlaytestReport>(File.ReadAllText(file)); }
            catch { skipped++; continue; }
            if (report == null || report.schema == null || !report.schema.StartsWith("mazeboo.playtest")) continue;

            string who = report.botProfile == TestModeSession.HumanName ? "Human" : PlaytestBotProfile.DisplayName(report.botProfile) + " bot";
            string key = who + " · " + report.difficulty;
            if (!groups.TryGetValue(key, out var group)) groups[key] = group = new Group { name = key };
            group.runs++;
            Outcomes(report, group);

            string tracePath = Path.ChangeExtension(file, ".trace.json");
            if (!File.Exists(tracePath)) continue;
            try
            {
                var trace = Trace.Load(tracePath);
                group.traced++;
                Behaviour(trace, report, group);
                Coins(trace, report, group);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Playtest trace could not be read: " + tracePath + " - " + e.Message);
            }
        }
        return Render(folder, groups.Values.ToList(), skipped);
    }

    // ---- what the reports alone say -------------------------------------------

    private static void Outcomes(PlaytestReport r, Group g)
    {
        g.Add("levelsCleared", r.levelsCompleted);
        g.Add("endLevel", r.endLevel);
        foreach (var l in r.levels)
        {
            g.Add("deathsPerLevel", l.deaths);
            g.Add("abilitiesPerLevel", l.traps + l.freezes + l.shields + l.teleports);
            g.Add("purchasesPerLevel", l.purchases);
            if (l.completed && !l.joinedMidLevel)
            {
                g.Add("secondsPerLevel", l.seconds);
                if (l.seconds > 0f) g.Add("coinsPerMinute", l.coinsCollected / (l.seconds / 60f));
            }
            if (!g.byLevel.TryGetValue(l.level, out var row)) g.byLevel[l.level] = row = new int[3];
            row[0]++;
            row[1] += l.deaths;
            row[2] += l.lavaDeaths;
        }
        foreach (var e in r.events)
        {
            if (e.type == "death") g.Add("lavaShare", e.detail == "lava" ? 1f : 0f);
            if (e.type == "purchase" && e.detail == "life") g.Add("livesBought", 1f);
            if (e.type == "ability")
            {
                if (e.nearestEnemy >= 0f) g.Add("abilityDist", e.nearestEnemy);
                g.Add("abilityProactive", e.nearestEnemy < 0f || e.nearestEnemy > 2.5f ? 1f : 0f);
            }
        }
    }

    // ---- what the film says -----------------------------------------------------

    private static void Behaviour(Trace trace, PlaytestReport report, Group g)
    {
        var s = trace.samples;
        float dt = trace.interval;
        var abilityTimes = report.events.Where(e => e.type == "ability").Select(e => e.t).ToList();
        var deathTimes = report.events.Where(e => e.type == "death").Select(e => e.t).ToList();

        int idle = 0;
        int toward = 0;
        bool inCall = false;
        float callStart = 0f;
        Sample prev = null;
        Vector2Int lastCell = default;
        Level lastLevel = null;

        for (int i = 0; i < s.Count; i++)
        {
            var x = s[i];
            // Alive and playing, and standing over a tile of floor - the centre
            // can hang out over the lava for a moment, most of all through a
            // corner gap, and those moments are no reason to lose the thread.
            bool alive = (x.flags & (Dead | Held | Card)) == 0;
            bool live = alive && x.node >= 0;

            // A close call is over the moment the player is out of it - dead
            // included, which is how the worst of them end.
            if (inCall && (!live || x.nearDist > 2.6f))
            {
                inCall = false;
                g.Add("callAbility", abilityTimes.Any(t => t >= callStart - .3f && t <= x.t) ? 1f : 0f);
                g.Add("callDeath", deathTimes.Any(t => t >= callStart && t <= x.t + .3f) ? 1f : 0f);
            }

            if (!live)
            {
                FlushIdle(g, ref idle, dt);
                prev = null;
                toward = 0;
                if (!alive) lastLevel = null;
                continue;
            }

            g.liveSeconds += dt;
            float mag = x.stick.magnitude;
            bool moving = mag > .2f;
            Vector2 dir = moving ? x.stick / mag : Vector2.zero;
            bool calm = x.pathDist > Calm;

            // Standing still with nothing near: what a moment of looking away
            // looks like from outside.
            if (calm) g.Add("still", moving ? 0f : 1f);
            if (calm && !moving) idle++;
            else FlushIdle(g, ref idle, dt);

            // How far off the lanes the stick points. The board is all right
            // angles, so every degree off the nearest axis is the hand, not the
            // plan.
            if (mag > .5f) g.Add("aim", AxisAngle(dir));

            // Where the corner was taken: how far from the middle of its tile
            // the character was when the stick swung from one axis to the other.
            if (prev != null && moving && prev.stick.magnitude > .2f)
            {
                bool wasX = Mathf.Abs(prev.stick.x) > Mathf.Abs(prev.stick.y);
                bool isX = Mathf.Abs(x.stick.x) > Mathf.Abs(x.stick.y);
                if (wasX != isX && AxisAngle(prev.stick.normalized) < 30f && AxisAngle(dir) < 30f)
                    g.Add("corner", Vector2.Distance(x.at, x.level.tiles[x.node]));
            }

            // How far off the middle of the row the character is while the
            // stick says straight along it - what a lane between two pools of
            // lava leaves no room for.
            if (mag > .5f && AxisAngle(dir) < 15f)
            {
                Vector2 middle = x.level.tiles[x.node];
                g.Add("offLane", Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? Mathf.Abs(x.at.y - middle.y) : Mathf.Abs(x.at.x - middle.x));
            }

            // Through a corner gap: from one tile of floor to the one diagonally
            // past it, with lava on both of the other two.
            var cell = x.level.CellAt(x.at);
            if (lastLevel == x.level && cell != lastCell)
            {
                var step = cell - lastCell;
                if (Mathf.Abs(step.x) == 1 && Mathf.Abs(step.y) == 1
                    && x.level.IsLava(new Vector2Int(lastCell.x + step.x, lastCell.y))
                    && x.level.IsLava(new Vector2Int(lastCell.x, lastCell.y + step.y)))
                    g.diagonalCrossings++;
            }
            lastCell = cell;
            lastLevel = x.level;

            if (x.view.x >= 0f) g.Add("timeCorner", InCorner(x.view) ? 1f : 0f);

            // Turning away: heading at the nearest hunter, then heading off.
            if (x.pathDist > 5f) toward = 0;
            else if (moving)
            {
                float dot = Vector2.Dot(dir, x.toHunter);
                if (dot > .5f) toward++;
                else if (dot < -.2f)
                {
                    if (toward >= 2)
                    {
                        g.Add("turnPath", x.pathDist);
                        g.Add("turnDist", x.nearDist);
                    }
                    toward = 0;
                }
            }

            // Reaction: a hunter comes within three steps while the player is
            // walking at it - how long until they stop walking at it.
            if (prev != null && prev.pathDist > Calm && x.pathDist <= Calm && moving && Vector2.Dot(dir, x.toHunter) > .5f)
            {
                float reacted = -1f;
                for (int j = i + 1; j < s.Count && s[j].t - x.t <= 2f; j++)
                {
                    var y = s[j];
                    if ((y.flags & (Dead | Held | Card)) != 0) break;
                    float m = y.stick.magnitude;
                    if (m < .2f || Vector2.Dot(y.stick / m, y.toHunter) < .2f)
                    {
                        reacted = y.t - x.t;
                        break;
                    }
                }
                g.Add("reacted", reacted >= 0f ? 1f : 0f);
                if (reacted >= 0f) g.Add("reaction", reacted);
            }

            // A close call: a hunter within touching distance, with nothing
            // making the player untouchable.
            if (!inCall && (x.flags & (Shield | Grace)) == 0 && x.nearDist <= 1.6f)
            {
                inCall = true;
                callStart = x.t;
            }

            prev = x;
        }
        FlushIdle(g, ref idle, dt);

        // Where on the screen each life was lost, and which of the lava deaths
        // were in a corner gap.
        foreach (var e in report.events)
        {
            if (e.type != "death") continue;
            Sample at = null;
            foreach (var y in s)
            {
                if (y.t > e.t + .01f) break;
                at = y;
            }
            if (at != null && at.view.x >= 0f) g.Add("deathCorner", InCorner(at.view) ? 1f : 0f);
            if (e.detail != "lava") continue;
            var level = trace.levels.LastOrDefault(l => l.number == e.level && l.t <= e.t + .01f);
            if (level != null && level.NearDiagonalGap(new Vector2(e.x, e.z), .4f)) g.diagonalDeaths++;
        }
    }

    // The bottom corners, where the thumbs rest.
    private static bool InCorner(Vector2 view) => Mathf.Min(view.x, 1f - view.x) < .3f && view.y < .4f;

    private static void FlushIdle(Group g, ref int idle, float dt)
    {
        if (idle * dt >= .3f)
        {
            g.Add("idleLen", idle * dt);
            g.idlePauses++;
        }
        idle = 0;
    }

    // Degrees between a direction and the nearest of the four lanes: 0 is
    // straight down a corridor, 45 is dead diagonal.
    private static float AxisAngle(Vector2 dir) =>
        Mathf.Atan2(Mathf.Min(Mathf.Abs(dir.x), Mathf.Abs(dir.y)), Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y))) * Mathf.Rad2Deg;

    // Which coin the player went for next: the one that looked nearest, the one
    // that was nearest to walk to, or neither.
    private static void Coins(Trace trace, PlaytestReport report, Group g)
    {
        for (int li = 0; li < trace.levels.Count; li++)
        {
            var level = trace.levels[li];
            float until = li + 1 < trace.levels.Count ? trace.levels[li + 1].t : float.MaxValue;
            var remaining = new List<Vector2>(level.coins);
            Vector2? from = null;

            foreach (var e in report.events)
            {
                if (e.type != "coin" || e.level != level.number || e.t < level.t || e.t >= until) continue;
                if (remaining.Count == 0) break;
                var at = new Vector2(e.x, e.z);
                int taken = Nearest(remaining, at);

                if (from.HasValue && remaining.Count > 1)
                {
                    g.Add("coinNearest", Nearest(remaining, from.Value) == taken ? 1f : 0f);
                    int start = level.Node(from.Value);
                    if (start >= 0)
                    {
                        var steps = level.Distances(start);
                        int best = -1;
                        int bestSteps = int.MaxValue;
                        for (int c = 0; c < remaining.Count; c++)
                        {
                            int node = level.Node(remaining[c]);
                            if (node < 0 || steps[node] < 0 || steps[node] >= bestSteps) continue;
                            bestSteps = steps[node];
                            best = c;
                        }
                        if (best >= 0)
                        {
                            int takenNode = level.Node(remaining[taken]);
                            g.Add("coinPathNearest", takenNode >= 0 && steps[takenNode] == bestSteps ? 1f : 0f);
                        }
                    }
                }
                remaining.RemoveAt(taken);
                from = at;
            }
        }
    }

    private static int Nearest(List<Vector2> points, Vector2 to)
    {
        int best = 0;
        for (int i = 1; i < points.Count; i++)
            if ((points[i] - to).sqrMagnitude < (points[best] - to).sqrMagnitude) best = i;
        return best;
    }

    // ---- the table ----------------------------------------------------------------

    private enum Stat { Mean, Median, P90, Share }

    private struct Row
    {
        public string label, key, format, knob;
        public Stat stat;
        public Row(string label, string key, Stat stat, string format, string knob)
        {
            this.label = label; this.key = key; this.stat = stat; this.format = format; this.knob = knob;
        }
    }

    private static readonly Row[] Outcome =
    {
        new Row("Levels cleared per run", "levelsCleared", Stat.Mean, "0.0", "everything"),
        new Row("Level the run ended on", "endLevel", Stat.Mean, "0.0", "everything"),
        new Row("Deaths per level played", "deathsPerLevel", Stat.Mean, "0.00", "everything"),
        new Row("Deaths that were lava", "lavaShare", Stat.Share, "0%", "cornerTolerance, aimError"),
        new Row("Seconds per cleared level", "secondsPerLevel", Stat.Median, "0", "plansRoute, dangerWeight, fleeDistance"),
        new Row("Coins per minute", "coinsPerMinute", Stat.Median, "0.0", "plansRoute, safeDistance"),
        new Row("Abilities used per level", "abilitiesPerLevel", Stat.Mean, "0.00", "forgetChance, proactiveAbilities"),
        new Row("Purchases per level", "purchasesPerLevel", Stat.Mean, "0.00", "strategicShopping"),
    };

    private static readonly Row[] Hands =
    {
        new Row("Standing still with no hunter within 3 steps", "still", Stat.Share, "0%", "distractionEvery"),
        new Row("Pause length (0.3 s or more), median s", "idleLen", Stat.Median, "0.00", "distractionMin / Max"),
        new Row("Stick off the lane, median degrees", "aim", Stat.Median, "0.0", "aimError"),
        new Row("Stick off the lane, 90th percentile", "aim", Stat.P90, "0.0", "aimError"),
        new Row("Distance from tile centre when turning", "corner", Stat.Median, "0.00", "cornerTolerance"),
        new Row("Off the middle of the lane going straight, median tiles", "offLane", Stat.Median, "0.00", "aimError"),
        new Row("Off the middle of the lane going straight, 90th pct", "offLane", Stat.P90, "0.00", "aimError"),
    };

    private static readonly Row[] ScreenRows =
    {
        new Row("Time drawn in a bottom corner of the screen", "timeCorner", Stat.Share, "0%", "thumbCover"),
        new Row("Deaths drawn in a bottom corner of the screen", "deathCorner", Stat.Share, "0%", "thumbCover"),
    };

    private static readonly Row[] Head =
    {
        new Row("Turns away from a hunter at, walking steps", "turnPath", Stat.Median, "0.0", "fleeDistance, pathDistanceDanger"),
        new Row("Turns away from a hunter at, straight tiles", "turnDist", Stat.Median, "0.00", "fleeDistance"),
        new Row("Reaction to a hunter coming within 3 steps, s", "reaction", Stat.Median, "0.00", "reactionTime"),
        new Row("Reacted to it within 2 s", "reacted", Stat.Share, "0%", "reactionTime, awareness"),
        new Row("Close calls (hunter within 1.6) with an ability used", "callAbility", Stat.Share, "0%", "panicDistance, forgetChance"),
        new Row("Close calls that cost a life", "callDeath", Stat.Share, "0%", "smartFlee, fleeDistance"),
        new Row("Nearest hunter when an ability was used, tiles", "abilityDist", Stat.Median, "0.0", "panicDistance"),
        new Row("Abilities used ahead of trouble (over 2.5 tiles)", "abilityProactive", Stat.Share, "0%", "proactiveAbilities"),
        new Row("Next coin was the nearest in a straight line", "coinNearest", Stat.Share, "0%", "plansRoute"),
        new Row("Next coin was the nearest to walk to", "coinPathNearest", Stat.Share, "0%", "plansRoute"),
    };

    private static string Render(string folder, List<Group> groups, int skipped)
    {
        var sb = new StringBuilder();
        sb.Append("# Playtest comparison\n\n");
        sb.Append(folder).Append(" - ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\n\n");
        if (groups.Count == 0)
        {
            sb.Append("No playtest reports found.\n");
            return sb.ToString();
        }

        sb.Append("| | ").Append(string.Join(" | ", groups.Select(g => g.name))).Append(" | tuned by |\n");
        sb.Append("|---|").Append(string.Concat(groups.Select(_ => "---|"))).Append("---|\n");
        sb.Append("| Runs (with trace) | ").Append(string.Join(" | ", groups.Select(g => g.runs + " (" + g.traced + ")"))).Append(" | |\n");
        sb.Append("| Minutes on the stick | ").Append(string.Join(" | ", groups.Select(g => (g.liveSeconds / 60f).ToString("0.0", CultureInfo.InvariantCulture)))).Append(" | |\n");

        Section(sb, "Outcome", Outcome, groups);
        Section(sb, "Hands", Hands, groups);
        sb.Append("| Pauses (0.3 s or more) per minute | ")
            .Append(string.Join(" | ", groups.Select(g => g.liveSeconds > 0f
                ? (g.idlePauses / (g.liveSeconds / 60f)).ToString("0.0", CultureInfo.InvariantCulture) : "-")))
            .Append(" | distractionEvery |\n");
        sb.Append("| Diagonal gaps crossed per minute | ")
            .Append(string.Join(" | ", groups.Select(g => g.liveSeconds > 0f
                ? (g.diagonalCrossings / (g.liveSeconds / 60f)).ToString("0.0", CultureInfo.InvariantCulture) : "-")))
            .Append(" | diagonalCutCost |\n");
        sb.Append("| Diagonal gap tries that ended in the lava | ")
            .Append(string.Join(" | ", groups.Select(g => g.diagonalCrossings + g.diagonalDeaths > 0
                ? (g.diagonalDeaths / (float)(g.diagonalCrossings + g.diagonalDeaths)).ToString("0%", CultureInfo.InvariantCulture)
                  + " (" + (g.diagonalCrossings + g.diagonalDeaths) + ")"
                : "-")))
            .Append(" | aimError |\n");
        Section(sb, "Head", Head, groups);
        Section(sb, "Screen", ScreenRows, groups);

        // Where the lives go, level by level: the thing the bot is ultimately
        // for, and the thing the other rows should add up to.
        int maxLevel = groups.SelectMany(g => g.byLevel.Keys).DefaultIfEmpty(0).Max();
        if (maxLevel > 0)
        {
            sb.Append("\n## Deaths per play of each level (lava in brackets)\n\n");
            sb.Append("| Level | ").Append(string.Join(" | ", groups.Select(g => g.name))).Append(" |\n");
            sb.Append("|---|").Append(string.Concat(groups.Select(_ => "---|"))).Append('\n');
            for (int level = 1; level <= maxLevel; level++)
            {
                sb.Append("| ").Append(level).Append(" | ");
                sb.Append(string.Join(" | ", groups.Select(g =>
                {
                    if (!g.byLevel.TryGetValue(level, out var row) || row[0] == 0) return "-";
                    return (row[1] / (float)row[0]).ToString("0.0", CultureInfo.InvariantCulture)
                           + " (" + (row[2] / (float)row[0]).ToString("0.0", CultureInfo.InvariantCulture) + ") ×" + row[0];
                })));
                sb.Append(" |\n");
            }
        }

        if (skipped > 0) sb.Append("\n").Append(skipped).Append(" file(s) could not be read.\n");
        sb.Append("\nSample counts in brackets. A median over a handful of samples is a hint, not a measurement.\n");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, Row[] rows, List<Group> groups)
    {
        sb.Append("| **").Append(title).Append("** |").Append(string.Concat(groups.Select(_ => " |"))).Append(" |\n");
        foreach (var row in rows)
        {
            sb.Append("| ").Append(row.label).Append(" | ");
            sb.Append(string.Join(" | ", groups.Select(g => Cell(g, row))));
            sb.Append(" | ").Append(row.knob).Append(" |\n");
        }
    }

    private static string Cell(Group g, Row row)
    {
        if (!g.values.TryGetValue(row.key, out var v) || v.Count == 0) return "-";
        float value;
        switch (row.stat)
        {
            case Stat.Median: value = Percentile(v, .5f); break;
            case Stat.P90: value = Percentile(v, .9f); break;
            default: value = v.Average(); break;
        }
        return value.ToString(row.format, CultureInfo.InvariantCulture) + " (" + v.Count + ")";
    }

    private static float Percentile(List<float> values, float p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        float index = p * (sorted.Count - 1);
        int lo = Mathf.FloorToInt(index);
        int hi = Mathf.Min(lo + 1, sorted.Count - 1);
        return Mathf.Lerp(sorted[lo], sorted[hi], index - lo);
    }

    private class Group
    {
        public string name;
        public int runs;
        public int traced;
        public float liveSeconds;
        public int idlePauses;
        public int diagonalCrossings;
        public int diagonalDeaths;
        public readonly Dictionary<string, List<float>> values = new Dictionary<string, List<float>>();
        // level -> plays, deaths, lava deaths
        public readonly SortedDictionary<int, int[]> byLevel = new SortedDictionary<int, int[]>();

        public void Add(string key, float value)
        {
            if (!values.TryGetValue(key, out var list)) values[key] = list = new List<float>();
            list.Add(value);
        }
    }

    // ---- reading a trace ------------------------------------------------------------

    private class Level
    {
        public int number;
        public float t;
        public Vector2[] tiles;
        public Vector2[] coins;
        public int[][] adj;
        private readonly Dictionary<Vector2Int, int> _index = new Dictionary<Vector2Int, int>();
        private float _originX, _originZ;
        private Vector2Int _min, _max;
        private int[] _stamp;
        private int[] _depth;
        private int[] _queue;
        private int _pass;

        public void Build()
        {
            if (tiles.Length > 0)
            {
                _originX = tiles[0].x;
                _originZ = tiles[0].y;
            }
            for (int i = 0; i < tiles.Length; i++) _index[Cell(tiles[i])] = i;
            _min = new Vector2Int(int.MaxValue, int.MaxValue);
            _max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var cell in _index.Keys)
            {
                _min = Vector2Int.Min(_min, cell);
                _max = Vector2Int.Max(_max, cell);
            }
            adj = new int[tiles.Length][];
            var n = new List<int>(4);
            for (int i = 0; i < tiles.Length; i++)
            {
                n.Clear();
                var c = Cell(tiles[i]);
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    if (_index.TryGetValue(c + d, out int j)) n.Add(j);
                adj[i] = n.ToArray();
            }
            _stamp = new int[tiles.Length];
            _depth = new int[tiles.Length];
            _queue = new int[tiles.Length];
        }

        private Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x - _originX), Mathf.RoundToInt(p.y - _originZ));

        public int Node(Vector2 p) => _index.TryGetValue(Cell(p), out int i) ? i : -1;

        public Vector2Int CellAt(Vector2 p) => Cell(p);
        public bool Walkable(Vector2Int c) => _index.ContainsKey(c);

        // Inside the board and not floor. Measured off the floor's own extent,
        // so a board whose outer row is all lava reads a row small - rare.
        public bool IsLava(Vector2Int c) =>
            c.x >= _min.x && c.x <= _max.x && c.y >= _min.y && c.y <= _max.y && !_index.ContainsKey(c);

        // Within reach of a corner where two pools of lava touch across the gap
        // between two tiles of floor.
        public bool NearDiagonalGap(Vector2 p, float within)
        {
            float u = p.x - _originX, v = p.y - _originZ;
            float kx = Mathf.Round(u - .5f) + .5f, kz = Mathf.Round(v - .5f) + .5f;
            if ((u - kx) * (u - kx) + (v - kz) * (v - kz) > within * within) return false;
            var a = new Vector2Int(Mathf.RoundToInt(kx - .5f), Mathf.RoundToInt(kz - .5f));
            var b = new Vector2Int(a.x + 1, a.y + 1);
            var c = new Vector2Int(a.x, a.y + 1);
            var d = new Vector2Int(a.x + 1, a.y);
            return IsLava(a) && IsLava(b) && Walkable(c) && Walkable(d)
                   || IsLava(c) && IsLava(d) && Walkable(a) && Walkable(b);
        }

        // Walking steps from a tile to the nearest of some others, giving up
        // past a limit - nobody reacts to a hunter nine corridors away.
        public int StepsTo(int from, List<int> targets, int limit)
        {
            if (targets.Count == 0) return int.MaxValue;
            if (targets.Contains(from)) return 0;
            _pass++;
            int head = 0, tail = 0;
            _queue[tail++] = from;
            _stamp[from] = _pass;
            _depth[from] = 0;
            while (head < tail)
            {
                int cur = _queue[head++];
                if (_depth[cur] >= limit) continue;
                foreach (int next in adj[cur])
                {
                    if (_stamp[next] == _pass) continue;
                    _stamp[next] = _pass;
                    _depth[next] = _depth[cur] + 1;
                    if (targets.Contains(next)) return _depth[next];
                    _queue[tail++] = next;
                }
            }
            return int.MaxValue;
        }

        public int[] Distances(int from)
        {
            var steps = Enumerable.Repeat(-1, tiles.Length).ToArray();
            var queue = new Queue<int>();
            steps[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int next in adj[cur])
                {
                    if (steps[next] >= 0) continue;
                    steps[next] = steps[cur] + 1;
                    queue.Enqueue(next);
                }
            }
            return steps;
        }
    }

    private class Sample
    {
        public float t;
        public Vector2 at;
        public Vector2 stick;
        public int flags;
        public Level level;
        public int node = -1;
        // Where the character was drawn on the screen, 0..1; -1 in a trace too
        // old to say.
        public Vector2 view = new Vector2(-1f, -1f);
        // To the nearest hunter that is hunting: a wanderer or a frozen one is
        // scenery.
        public float pathDist = 99f;
        public float nearDist = 99f;
        public Vector2 toHunter;
    }

    private class Trace
    {
        public float interval;
        public readonly List<Level> levels = new List<Level>();
        public readonly List<Sample> samples = new List<Sample>();

        public static Trace Load(string path)
        {
            var root = (Dictionary<string, object>)Json.Parse(File.ReadAllText(path));
            var trace = new Trace { interval = F(root["interval"]) };

            var chases = new Dictionary<int, bool>();
            foreach (Dictionary<string, object> e in (List<object>)root["enemies"])
                chases[(int)F(e["id"])] = (string)e["strategy"] != "Wander";

            var byNumber = new Dictionary<int, Level>();
            foreach (Dictionary<string, object> l in (List<object>)root["levels"])
            {
                var level = new Level
                {
                    number = (int)F(l["level"]),
                    t = F(l["t"]),
                    tiles = Points((List<object>)l["tiles"]),
                    coins = Points((List<object>)l["coins"])
                };
                level.Build();
                trace.levels.Add(level);
                byNumber[level.number] = level;
            }

            var targets = new List<int>();
            foreach (List<object> a in (List<object>)root["samples"])
            {
                var x = new Sample
                {
                    t = F(a[0]),
                    at = new Vector2(F(a[2]), F(a[3])),
                    stick = new Vector2(F(a[4]), F(a[5])),
                    flags = (int)F(a[6])
                };
                if (a.Count > 9) x.view = new Vector2(F(a[8]), F(a[9]));
                byNumber.TryGetValue((int)F(a[1]), out x.level);
                if (x.level != null) x.node = x.level.Node(x.at);

                targets.Clear();
                var enemies = (List<object>)a[7];
                for (int k = 0; k + 3 < enemies.Count; k += 4)
                {
                    int id = (int)F(enemies[k]);
                    if (F(enemies[k + 3]) > 0f || (chases.TryGetValue(id, out bool hunts) && !hunts)) continue;
                    var e = new Vector2(F(enemies[k + 1]), F(enemies[k + 2]));
                    float d = Vector2.Distance(e, x.at);
                    if (d < x.nearDist)
                    {
                        x.nearDist = d;
                        x.toHunter = d > .001f ? (e - x.at) / d : Vector2.zero;
                    }
                    if (x.level != null)
                    {
                        int node = x.level.Node(e);
                        if (node >= 0) targets.Add(node);
                    }
                }
                if (x.node >= 0)
                {
                    int steps = x.level.StepsTo(x.node, targets, 8);
                    x.pathDist = steps == int.MaxValue ? 99f : steps;
                }
                trace.samples.Add(x);
            }
            return trace;
        }

        private static Vector2[] Points(List<object> flat)
        {
            var points = new Vector2[flat.Count / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(F(flat[i * 2]), F(flat[i * 2 + 1]));
            return points;
        }

        private static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);
    }

    // Just enough JSON for a trace: objects, arrays, numbers, strings, true,
    // false, null.
    private static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            return Value(s, ref i);
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Word(s, i, "true")) { i += 4; return true; }
            if (Word(s, i, "false")) { i += 5; return false; }
            if (Word(s, i, "null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var o = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return o; }
            while (true)
            {
                Skip(s, ref i);
                string key = ReadString(s, ref i);
                Skip(s, ref i);
                i++; // :
                o[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i++] == '}') return o;
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var a = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i++] == ']') return a;
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'u')
                    {
                        sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4;
                    }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static bool Word(string s, int at, string word) =>
            string.CompareOrdinal(s, at, word, 0, word.Length) == 0;
    }
}
