using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds an Android APK from the scenes listed in Build Settings.
/// Menu: Build > Android APK (and > Android APK (development))
/// CLI:  Unity.exe -quit -batchmode -projectPath . -executeMethod BuildAndroid.BuildFromCommandLine
///
/// The report file is written before the build starts and rewritten when it
/// finishes, so a build that outlives whatever started it can still be asked
/// how it went.
/// </summary>
public static class BuildAndroid
{
    const string OutputFolder = "Builds/Android";
    const string ApkName = "MazeBoo.apk";
    const string ReportFile = "Builds/android_build_report.txt";

    [MenuItem("Build/Android APK")]
    public static void Build() => RunBuild(false);

    [MenuItem("Build/Android APK (development)")]
    public static void BuildDevelopment() => RunBuild(true);

    public static void BuildFromCommandLine()
    {
        var report = RunBuild(false);
        if (report == null || report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    public static BuildReport RunBuild(bool development)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var outputDir = Path.Combine(projectRoot, OutputFolder);
        var apkPath = Path.Combine(outputDir, ApkName);
        var reportPath = Path.Combine(projectRoot, ReportFile);

        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, "STATUS: RUNNING\nSTARTED: " + DateTime.Now + "\n");

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            File.WriteAllText(reportPath, "STATUS: FAILED\nNo enabled scenes in Build Settings.\n");
            Debug.LogError("Android build: no enabled scenes in Build Settings.");
            return null;
        }

        string signingProblem = PrepareSigning();
        if (signingProblem != null)
        {
            File.WriteAllText(reportPath, "STATUS: FAILED\n" + signingProblem + "\n");
            Debug.LogError("Android build: " + signingProblem);
            return null;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        // Every build gets its own version code, so a phone treats the new APK
        // as an upgrade of the old one rather than refusing to install over it.
        PlayerSettings.Android.bundleVersionCode++;

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        string text = "STATUS: " + (summary.result == BuildResult.Succeeded ? "SUCCEEDED" : summary.result.ToString().ToUpper()) + "\n"
                    + "FINISHED: " + DateTime.Now + "\n"
                    + "APK: " + apkPath + "\n"
                    + "VERSION: " + PlayerSettings.bundleVersion + " (code " + PlayerSettings.Android.bundleVersionCode + ")\n"
                    + "DEVELOPMENT: " + development + "\n"
                    + "SIZE: " + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB\n"
                    + "TIME: " + summary.totalTime + "\n"
                    + "ERRORS: " + summary.totalErrors + "  WARNINGS: " + summary.totalWarnings + "\n"
                    + "SCENES:\n   " + string.Join("\n   ", scenes) + "\n";
        File.WriteAllText(reportPath, text);

        if (summary.result == BuildResult.Succeeded) Debug.Log("Android build succeeded: " + apkPath);
        else Debug.LogError("Android build " + summary.result + " - see " + ReportFile);
        return report;
    }

    // Unity forgets keystore passwords on restart, and a debug-signed APK cannot update a keystore-signed one.
    static string PrepareSigning()
    {
        if (!PlayerSettings.Android.useCustomKeystore)
            return "Custom Keystore is off (Player > Android > Publishing Settings); builds must be signed with the Maze Boo keystore.";

        var keystore = PlayerSettings.Android.keystoreName;
        if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
            return "Keystore not found at '" + keystore + "'. Copy mazeboo.keystore there or pick it in Publishing Settings.";

        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("MAZEBOO_KEYSTORE_PASS") ?? "";
        if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("MAZEBOO_KEY_PASS") ?? "";
        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            return "Keystore passwords are not set. Enter them in Publishing Settings (Unity forgets them on restart) or set MAZEBOO_KEYSTORE_PASS and MAZEBOO_KEY_PASS.";

        return null;
    }
}
