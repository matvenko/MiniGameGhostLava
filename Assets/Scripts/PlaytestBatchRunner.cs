using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays test runs back to back with nobody watching, when the game is started
// with -playtest on its command line - a player build run with -batchmode
// -nographics, or the editor through PlaytestBatch. Each run is a new game on a
// fresh copy of the save, played by the bot to its last life, and its report is
// copied into -playtestOut. When the last one is done the program exits.
//
//   -playtest                     turn this on
//   -playtestDifficulty normal    or hard
//   -playtestBot auto             or normal / hard
//   -playtestRuns 5
//   -playtestOut <folder>
//   -playtestSpeed 1              game speed; the frame clock is already fixed
//   -playtestTimeout 1800         game seconds before a run is called off
//
// The frame clock is fixed at a sixtieth of a second, so every run is simulated
// exactly as a phone at 60 fps would play it, only as fast as the machine can go.
public class PlaytestBatchRunner : MonoBehaviour
{
    private const string GameplayScene = "LavaScene";

    private static int _runs = 1;
    private static string _outDir;
    private static float _timeout = 1800f;
    private static bool _startPending;
    private static int _boardFrame = int.MaxValue;

    private int _finished;
    private PlaytestReport _handled;
    private float _runStartedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-playtest") < 0) return;

        bool hard = Arg(args, "-playtestDifficulty", "normal").ToLowerInvariant() == "hard";
        DifficultySettings.OverrideForSession(hard ? Difficulty.Hard : Difficulty.Normal);

        string bot = Arg(args, "-playtestBot", "auto").ToLowerInvariant();
        TestModeSession.Choice = bot == "normal" ? BotChoice.Normal : bot == "hard" ? BotChoice.Hard : BotChoice.MatchDifficulty;

        _runs = Mathf.Max(1, (int)Number(args, "-playtestRuns", 1));
        _timeout = Number(args, "-playtestTimeout", 1800f);
        TestModeSession.PlaySpeed = Mathf.Max(.1f, Number(args, "-playtestSpeed", 1f));
        _outDir = Arg(args, "-playtestOut", null);
        if (!string.IsNullOrEmpty(_outDir)) Directory.CreateDirectory(_outDir);

        Application.runInBackground = true;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Time.captureDeltaTime = 1f / 60f;
        AudioListener.volume = 0f;

        PrepareRun();
        SceneManager.sceneLoaded += OnSceneLoaded;
        var host = new GameObject("Playtest Batch");
        DontDestroyOnLoad(host);
        host.AddComponent<PlaytestBatchRunner>();
        Debug.Log("PLAYTEST batch: " + _runs + " run(s), " + DifficultySettings.Current + ", bot " + TestModeSession.Choice);
    }

    private static void PrepareRun()
    {
        RunProgress.EnterSandbox(fresh: true);
        _startPending = true;
        _boardFrame = int.MaxValue;
    }

    // A player build opens on the menu; the batch goes straight to the board.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (LevelManager.Instance == null)
        {
            SceneManager.LoadScene(GameplayScene);
            return;
        }
        _boardFrame = Time.frameCount;
    }

    void Update()
    {
        // A frame after the board comes up, so its coins are down before the
        // recorder counts them.
        if (_startPending)
        {
            if (Time.frameCount <= _boardFrame + 1 || !TestModeSession.CanStart) return;
            _startPending = false;
            TestModeSession.Start();
            _runStartedAt = Time.time;
            return;
        }

        if (TestModeSession.Active && Time.time - _runStartedAt > _timeout) TestModeSession.Stop("timeout");

        var report = PlaytestLog.LastReport;
        if (report == null || report == _handled) return;
        _handled = report;
        _finished++;
        Keep(report);

        if (_finished >= _runs)
        {
            Exit();
            return;
        }

        TestModeSession.LeaveBoard();
        PrepareRun();
        SceneManager.LoadScene(GameplayScene);
    }

    private void Keep(PlaytestReport report)
    {
        Debug.Log("PLAYTEST run " + _finished + "/" + _runs + ": " + report.endReason + ", level " + report.startLevel
                  + " -> " + report.endLevel + ", " + report.deaths + " deaths (" + report.lavaDeaths + " lava), "
                  + report.coinsCollected + " coins, " + report.abilitiesUsed + " abilities, " + report.purchases
                  + " purchases, " + report.gameSeconds + "s game / " + report.realSeconds + "s real");

        if (string.IsNullOrEmpty(_outDir) || string.IsNullOrEmpty(PlaytestLog.LastFilePath)) return;
        try
        {
            string name = "run" + _finished.ToString("00") + "-" + Path.GetFileName(PlaytestLog.LastFilePath);
            File.Copy(PlaytestLog.LastFilePath, Path.Combine(_outDir, name), true);
            // The trace travels with its report under the same name, which is
            // how PlaytestAnalysis pairs them up.
            if (!string.IsNullOrEmpty(PlaytestLog.LastTracePath) && File.Exists(PlaytestLog.LastTracePath))
                File.Copy(PlaytestLog.LastTracePath, Path.Combine(_outDir, Path.ChangeExtension(name, ".trace.json")), true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("PLAYTEST could not copy the report: " + e.Message);
        }
    }

    private static void Exit()
    {
        Debug.Log("PLAYTEST batch done");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit();
#endif
    }

    private static string Arg(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static float Number(string[] args, string name, float fallback) =>
        float.TryParse(Arg(args, name, null), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
}
