using UnityEditor;
using UnityEditor.SceneManagement;

// Command-line door into a playtest batch in the editor: opens the board and
// presses Play. PlaytestBatchRunner, woken by -playtest, plays the runs and
// closes the editor when they are done. The project must not be open in another
// editor at the same time.
//
//   Unity -batchmode -nographics -projectPath . -executeMethod PlaytestBatch.Run \
//         -playtest -playtestDifficulty normal -playtestRuns 5 -playtestOut ~/playtests
public static class PlaytestBatch
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/LavaScene.unity");
        EditorApplication.isPlaying = true;
    }
}
