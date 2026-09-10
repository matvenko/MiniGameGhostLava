using UnityEngine;
using UnityEngine.SceneManagement;

// Which bot plays a test run. By default the one the chosen difficulty is for -
// normal is the mode aimed at 10-15 year olds, hard at everyone older - but
// either can be put on either board, which is how "is hard too hard for a
// child" gets asked.
public enum BotChoice
{
    MatchDifficulty,
    Kid,
    Teen
}

// One test run from the button being pressed to the report card: the save is
// swapped for a copy, the clock doubled, the recorder started and the bot put on
// the board; and all of it undone when the run ends or the board is left, by
// whatever route.
public static class TestModeSession
{
    public const float Speed = 4f;
    private const string MainMenuScene = "MainMenu";

    public static bool Active { get; private set; }
    public static BotChoice Choice { get; set; } = BotChoice.MatchDifficulty;
    public static PlaytestBotProfile Profile { get; private set; }

    // Set by RUN AGAIN: the board is reloaded from the real save and the next
    // one to come up starts a new test on its own.
    public static bool RerunPending { get; private set; }

    private static PlaytestBot _bot;

    public static PlaytestBotProfile ProfileFor(BotChoice choice)
    {
        switch (choice)
        {
            case BotChoice.Kid: return PlaytestBotProfile.Kid;
            case BotChoice.Teen: return PlaytestBotProfile.Teen;
            default: return DifficultySettings.IsNormal ? PlaytestBotProfile.Kid : PlaytestBotProfile.Teen;
        }
    }

    // A run needs a board with a life on it. The level complete card is fine -
    // the bot presses Next itself - but the game over panel is a run that is
    // already finished.
    public static bool CanStart =>
        !Active
        && LevelManager.Instance != null
        && (GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOverActive)
        && (LivesManager.Instance == null || LivesManager.Instance.CurrentLives > 0);

    // The speed a run plays at. The player walks two tiles a second, so even at
    // four times that a 60 fps frame moves them a seventh of a tile - well inside
    // what the lava triggers catch, and the hunters move on the fixed physics
    // step, which the speed does not stretch. Slower settings are for watching
    // what the bot does. The batch runner asks for normal speed: it already runs
    // as fast as the machine allows.
    public static float PlaySpeed { get; set; } = Speed;

    public static readonly float[] SpeedChoices = { 1f, 2f, 4f };

    // The speed chip on the test bar. Works mid-run too - slowing down to watch a
    // moment and speeding back up - and the change goes into the report.
    public static void CycleSpeed()
    {
        int i = System.Array.IndexOf(SpeedChoices, PlaySpeed);
        PlaySpeed = SpeedChoices[(i + 1) % SpeedChoices.Length];
        if (!Active) return;
        GameSpeed.Set(PlaySpeed);
        PlaytestLog.SpeedChanged(PlaySpeed);
    }

    public static void Start()
    {
        if (!CanStart) return;
        RerunPending = false;
        Profile = ProfileFor(Choice);

        // A batch run has already put a fresh copy in place before the board was
        // laid out; anything else copies the real save as it stands.
        if (!RunProgress.Sandboxed) RunProgress.EnterSandbox();
        GameSpeed.Set(PlaySpeed);
        PlaytestLog.Begin(Profile.name, PlaySpeed);

        _bot = new GameObject("Playtest Bot").AddComponent<PlaytestBot>();
        _bot.Init(Profile);
        Active = true;
    }

    // The run is over - out of lives, or the STOP button. The board is held
    // still behind the report card; the copy of the save stays in use until the
    // board is left, so nothing the bot did can leak into the real one.
    public static void Stop(string reason)
    {
        if (!Active) return;
        Active = false;
        if (_bot != null) Object.Destroy(_bot.gameObject);
        _bot = null;
        GameSpeed.Set(1f);
        Time.timeScale = 0f;
        PlaytestLog.Finish(reason);
    }

    // GameOverManager asks before it shows its panel.
    public static bool OnOutOfLives()
    {
        if (!Active) return false;
        Stop("out_of_lives");
        return true;
    }

    // Another run from the same starting point: the real save was never
    // touched, so reloading the board from it puts everything back as it was
    // when the first run began.
    public static void RunAgain()
    {
        RerunPending = true;
        LeaveBoard();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void ExitToMenu()
    {
        RerunPending = false;
        LeaveBoard();
        SceneManager.LoadScene(MainMenuScene);
    }

    // Called on every way off the board - the report card's buttons, the pause
    // menu, the game over panel, the batch runner - before the next scene is
    // loaded, so nothing in it can wake up still reading the copy. A run still
    // going is written down as far as it got. (Stopping play in the editor needs
    // none of this: every static here starts over with the next play.)
    public static void LeaveBoard()
    {
        if (Active)
        {
            Active = false;
            PlaytestLog.Finish("left_board");
        }
        _bot = null;
        RunProgress.LeaveSandbox();
        GameSpeed.Set(1f);
        Time.timeScale = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        Active = false;
        RerunPending = false;
        Profile = null;
        PlaySpeed = Speed;
        _bot = null;
    }
}
