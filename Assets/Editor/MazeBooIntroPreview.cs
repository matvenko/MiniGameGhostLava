using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MazeBooIntroPreview
{
    [MenuItem("Tools/Maze Boo/Preview Intro")]
    public static void Preview()
    {
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }
}
