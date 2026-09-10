#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Development builds only: measure active gameplay, excluding menus, paused
// popups, scene warm-up and app resume. Never present editor/recorder FPS as device FPS.
public sealed class MobileFrameProbe : MonoBehaviour
{
    readonly float[] samples = new float[2400];
    int count;
    float elapsed, readyAt;
    bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var obj = new GameObject("Development frame probe");
        DontDestroyOnLoad(obj);
        obj.AddComponent<MobileFrameProbe>();
    }

    void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
    void SceneLoaded(Scene scene, LoadSceneMode mode) => ResetWindow(8);
    void OnApplicationFocus(bool value) { focused = value; ResetWindow(2); }
    void ResetWindow(float warmup)
    {
        count = 0; elapsed = 0; readyAt = Time.realtimeSinceStartup + warmup;
    }

    void Update()
    {
        if (!focused || SceneManager.GetActiveScene().name != "LavaScene") return;
        if (Time.timeScale == 0) { ResetWindow(1); return; }
        if (Time.realtimeSinceStartup < readyAt) return;
        float dt = Time.unscaledDeltaTime;
        samples[count++] = dt * 1000;
        elapsed += dt;
        if (elapsed < 10 && count < samples.Length) return;
        Array.Sort(samples, 0, count);
        var report = new FrameReport {
            frames = count, seconds = elapsed, averageFps = count / elapsed,
            p95Ms = samples[Mathf.Clamp(Mathf.CeilToInt(count * .95f) - 1, 0, count - 1)],
            worstMs = samples[count - 1], targetFps = Application.targetFrameRate,
            width = Screen.width, height = Screen.height
        };
        Debug.Log("WARDEN_DEVICE_FRAMES " + JsonUtility.ToJson(report));
        count = 0; elapsed = 0;
    }

    [Serializable] sealed class FrameReport
    {
        public int frames, targetFps, width, height;
        public float seconds, averageFps, p95Ms, worstMs;
    }
}
#endif
