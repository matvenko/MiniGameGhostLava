using UnityEngine;

// How fast the board plays while nothing has it stopped. Normally that is one;
// while the test bot is playing it is whatever the test bar's speed chip says,
// so a run gets through its levels in a fraction of the time.
//
// Every popup that stops the game - level complete, pause, the shop - puts the
// clock back when it closes, and it has to put it back to this rather than to a
// literal one, or the first popup a test run met would quietly drop it to half
// speed for the rest of the run.
public static class GameSpeed
{
    public static float Running { get; private set; } = 1f;

    // Changes the running speed. A game that is stopped right now stays stopped
    // and picks the new speed up when whatever stopped it lets go.
    public static void Set(float speed)
    {
        Running = Mathf.Max(0.01f, speed);
        if (Time.timeScale > 0f) Time.timeScale = Running;
    }

    // What a popup calls on its way out.
    public static void Resume() => Time.timeScale = Running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession() => Running = 1f;
}
