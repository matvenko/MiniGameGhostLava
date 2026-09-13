using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Sample;

// The run as a film rather than a list of events: twenty times a second of game
// time, where the character is, which way the stick is pushed, and where every
// hunter is. PlaytestLog says what happened; this says how - how close a hunter
// got before the player turned, how long they stood still, how straight they
// steered. Those are what the bot's profile numbers are measured against (see
// PlaytestBotProfile), and the film is shot the same way whether a person or the
// bot is holding the stick, so the two can be laid side by side (see
// PlaytestAnalysis).
//
// Written next to the report as <report>.trace.json, compact and by hand:
// JsonUtility writes every float at full precision, and twenty minutes of play is
// some twenty-four thousand samples.
//
// Each sample is [t, level, x, z, stickX, stickZ, flags, [id, x, z, stunned, ...],
// viewX, viewY, thumbX, thumbY, botMode, goalX, goalZ] with flags 1 dead, 2 held
// by the spawn countdown, 4 shield, 8 respawn grace, 16 level complete card. The
// stick is in world axes, the same vector that moves the character, and reads
// zero whenever nothing is steering. view and thumb are 0..1 across and up the
// screen - where the character is drawn, and where the steering finger is
// pressing (-1 with no finger down). botMode is what the bot is about (see
// PlaytestBot.TraceState), -1 for a person.
public class PlaytestTrace : MonoBehaviour
{
    public const float Interval = .05f;

    private static PlaytestTrace _current;

    private readonly StringBuilder _samples = new StringBuilder(1 << 20);
    private readonly StringBuilder _levels = new StringBuilder();
    private readonly StringBuilder _enemies = new StringBuilder();
    private readonly Dictionary<EnemyChaser, int> _ids = new Dictionary<EnemyChaser, int>();
    private readonly List<Vector3> _points = new List<Vector3>();
    private float _startTime;
    private float _next;
    private GhostScript _player;
    private Camera _camera;

    // Same clock as the report's events, so a death at t in one is the sample at
    // t in the other.
    public static void Begin(float startTime)
    {
        if (_current != null) Destroy(_current.gameObject);
        _current = new GameObject("Playtest Trace").AddComponent<PlaytestTrace>();
        _current._startTime = startTime;
        _current._next = Time.time;
    }

    // The board the level is played on - its floor and its coins - so the film
    // can be read without the scene it was shot in.
    public static void LevelStarted(int level)
    {
        if (_current != null) _current.AddLevel(level);
    }

    // Written beside the report; where to, or null when it could not be.
    public static string Finish(PlaytestReport report, string reportPath)
    {
        if (_current == null) return null;
        var trace = _current;
        _current = null;
        trace.Sample();
        string path = trace.Write(report, reportPath);
        Destroy(trace.gameObject);
        return path;
    }

    void OnDestroy()
    {
        if (_current == this) _current = null;
    }

    // Game time, so the level complete card and the pause menu, which both stop
    // the clock, leave no gap full of identical samples.
    void Update()
    {
        if (Time.time < _next) return;
        _next += Interval;
        if (_next < Time.time) _next = Time.time + Interval;
        Sample();
    }

    private void Sample()
    {
        if (_player == null) _player = FindAnyObjectByType<GhostScript>();
        if (_player == null) return;

        var level = LevelManager.Instance;
        Vector3 at = _player.transform.position;
        Vector3 stick = _player.InputDirection;
        int flags = (_player.IsDead ? 1 : 0)
                    | (EnemySpawnManager.PlayerFrozen ? 2 : 0)
                    | (_player.ShieldActive ? 4 : 0)
                    | (_player.GraceActive ? 8 : 0)
                    | (level != null && level.IsLevelCompleteActive ? 16 : 0);

        var s = _samples;
        if (s.Length > 0) s.Append(',');
        s.Append('[').Append(F(Time.time - _startTime))
            .Append(',').Append(level != null ? level.CurrentLevel : 0)
            .Append(',').Append(F(at.x)).Append(',').Append(F(at.z))
            .Append(',').Append(F(stick.x)).Append(',').Append(F(stick.z))
            .Append(',').Append(flags).Append(",[");
        bool first = true;
        foreach (var enemy in EnemyChaser.Active)
        {
            if (enemy == null) continue;
            if (!first) s.Append(',');
            first = false;
            Vector3 e = enemy.transform.position;
            s.Append(Id(enemy)).Append(',').Append(F(e.x)).Append(',').Append(F(e.z))
                .Append(',').Append(enemy.IsStunned ? 1 : 0);
        }
        s.Append(']');

        if (_camera == null)
        {
            var follow = FindAnyObjectByType<CameraFollow>();
            _camera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
        }
        Vector3 view = _camera != null ? _camera.WorldToViewportPoint(at) : new Vector3(-1f, -1f, 0f);
        var joystick = VirtualJoystick.Instance;
        Vector2 thumb = joystick != null && joystick.Held && Screen.width > 0 && Screen.height > 0
            ? new Vector2(joystick.PointerPosition.x / Screen.width, joystick.PointerPosition.y / Screen.height)
            : new Vector2(-1f, -1f);
        PlaytestBot.TraceState(out int mode, out Vector3 goal);
        s.Append(',').Append(F(view.x)).Append(',').Append(F(view.y))
            .Append(',').Append(F(thumb.x)).Append(',').Append(F(thumb.y))
            .Append(',').Append(mode).Append(',').Append(F(goal.x)).Append(',').Append(F(goal.z))
            .Append(']');
    }

    // Each hunter once, by the number the samples call it: what it is, and
    // whether it is chasing at all - a wanderer walking past is not a threat
    // anyone reacts to.
    private int Id(EnemyChaser enemy)
    {
        if (_ids.TryGetValue(enemy, out int id)) return id;
        id = _ids.Count;
        _ids[enemy] = id;
        if (_enemies.Length > 0) _enemies.Append(',');
        _enemies.Append("{\"id\":").Append(id)
            .Append(",\"name\":\"").Append(enemy.name.Replace('"', '\'')).Append('"')
            .Append(",\"strategy\":\"").Append(enemy.Strategy).Append('"')
            .Append(",\"speed\":").Append(F(enemy.Speed)).Append('}');
        return id;
    }

    private void AddLevel(int level)
    {
        var grid = EnemyPathGrid.Instance;
        grid.EnsureBuilt();

        var l = _levels;
        if (l.Length > 0) l.Append(',');
        l.Append("{\"level\":").Append(level).Append(",\"t\":").Append(F(Time.time - _startTime)).Append(",\"tiles\":[");
        Points(l, grid.AllNodes);
        l.Append("],\"coins\":[");
        _points.Clear();
        foreach (var coin in Coin.Active)
            if (coin != null) _points.Add(coin.transform.position);
        Points(l, _points);
        l.Append("]}");
    }

    private static void Points(StringBuilder sb, IReadOnlyList<Vector3> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(F(points[i].x)).Append(',').Append(F(points[i].z));
        }
    }

    private string Write(PlaytestReport report, string reportPath)
    {
        try
        {
            string path = !string.IsNullOrEmpty(reportPath)
                ? Path.ChangeExtension(reportPath, ".trace.json")
                : Path.Combine(Application.persistentDataPath, "playtests", report.runId + ".trace.json");

            var json = new StringBuilder(_samples.Length + _levels.Length + _enemies.Length + 256);
            json.Append("{\"schema\":\"mazeboo.trace/2\",\"runId\":\"").Append(report.runId)
                .Append("\",\"difficulty\":\"").Append(report.difficulty)
                .Append("\",\"player\":\"").Append(report.botProfile)
                .Append("\",\"interval\":").Append(F(Interval))
                .Append(",\"enemies\":[").Append(_enemies)
                .Append("],\"levels\":[").Append(_levels)
                .Append("],\"samples\":[").Append(_samples)
                .Append("]}");

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json.ToString());
            Debug.Log("Playtest trace written to " + path);
            return path;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Playtest trace could not be written: " + e.Message);
            return null;
        }
    }

    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession() => _current = null;
}
