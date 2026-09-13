using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// A batch of bot runs inside the open editor, for when the project cannot be
// closed for PlaytestBatch: each run is a new game on a fresh copy of the save,
// played by the bot to its last life, and its report and trace are copied into
// Playtests/bot for PlaytestAnalysis to compare against the recordings.
//
// The frame clock is fixed at a sixtieth of a second, as a phone at 60 fps
// would play it, and the board runs at double speed - still under a twentieth
// of a second of game time per frame, which is what the trace samples at.
//
// The queue is kept in SessionState, so a script reload part way through picks
// it up again at the next run.
[InitializeOnLoad]
public static class PlaytestEditorBatch
{
    private const string QueueKey = "PlaytestEditorBatch.queue";
    private const string DoneKey = "PlaytestEditorBatch.done";
    private const float Speed = 2f;
    private const float Timeout = 1800f;

    private enum Phase { Load, Wait, Running }

    private static Phase _phase;
    private static int _loadFrame;
    private static float _startedAt;
    private static PlaytestReport _handled;

    static PlaytestEditorBatch()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Playtest/Run Bots In Editor (Normal + Hard, 3 each)")]
    private static void RunDefault() => Queue("normal,hard,normal,hard,normal,hard");

    [MenuItem("Tools/Playtest/Stop Bots In Editor")]
    private static void StopMenu()
    {
        SessionState.EraseString(QueueKey);
        if (EditorApplication.isPlaying && TestModeSession.Active) TestModeSession.Stop("stopped");
        Time.captureDeltaTime = 0f;
        Debug.Log("PLAYTEST editor batch stopped");
    }

    // A comma-separated list of bots, one run each, in order.
    public static void Queue(string bots)
    {
        SessionState.SetString(QueueKey, bots);
        SessionState.SetInt(DoneKey, 0);
        _phase = Phase.Load;
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        Debug.Log("PLAYTEST editor batch queued: " + bots);
    }

    public static string Remaining => SessionState.GetString(QueueKey, "");

    private static void Tick()
    {
        string queue = Remaining;
        if (string.IsNullOrEmpty(queue) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;

        switch (_phase)
        {
            case Phase.Load:
                TestModeSession.LeaveBoard();
                string bot = queue.Split(',')[0].Trim().ToLowerInvariant();
                TestModeSession.Choice = bot == "hard" ? BotChoice.Hard : bot == "normal" ? BotChoice.Normal : BotChoice.MatchDifficulty;
                TestModeSession.PlaySpeed = Speed;
                Application.runInBackground = true;
                Time.captureDeltaTime = 1f / 60f;
                RunProgress.EnterSandbox(fresh: true);
                SceneManager.LoadScene("LavaScene");
                _loadFrame = Time.frameCount;
                _phase = Phase.Wait;
                break;

            // A couple of frames after the board comes up, so its coins are down
            // before the recorder counts them.
            case Phase.Wait:
                if (Time.frameCount <= _loadFrame + 2 || LevelManager.Instance == null || !TestModeSession.CanStart) return;
                _handled = PlaytestLog.LastReport;
                TestModeSession.StartBot();
                _startedAt = Time.time;
                _phase = Phase.Running;
                break;

            case Phase.Running:
                if (TestModeSession.Active && Time.time - _startedAt > Timeout) TestModeSession.Stop("timeout");
                var report = PlaytestLog.LastReport;
                if (report == null || report == _handled) return;
                Keep(report);
                int comma = queue.IndexOf(',');
                string rest = comma < 0 ? "" : queue.Substring(comma + 1);
                SessionState.SetString(QueueKey, rest);
                _phase = Phase.Load;
                if (string.IsNullOrEmpty(rest))
                {
                    TestModeSession.LeaveBoard();
                    Time.captureDeltaTime = 0f;
                    Debug.Log("PLAYTEST editor batch done - " + PlaytestAnalysis.Write(PlaytestAnalysis.ProjectFolder));
                }
                break;
        }
    }

    private static void Keep(PlaytestReport report)
    {
        int done = SessionState.GetInt(DoneKey, 0) + 1;
        SessionState.SetInt(DoneKey, done);
        Debug.Log("PLAYTEST editor run " + done + " (" + report.botProfile + "): " + report.endReason + ", level " + report.startLevel
                  + " -> " + report.endLevel + ", " + report.deaths + " deaths (" + report.lavaDeaths + " lava), "
                  + report.gameSeconds + "s game / " + report.realSeconds + "s real");

        string dir = Path.Combine(PlaytestAnalysis.ProjectFolder, "bot");
        Directory.CreateDirectory(dir);
        Copy(PlaytestLog.LastFilePath, dir);
        Copy(PlaytestLog.LastTracePath, dir);
    }

    private static void Copy(string file, string dir)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
        File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), true);
    }
}
