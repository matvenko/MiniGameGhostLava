using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds a standalone Windows (x64) player from the scenes listed in Build Settings.
/// Menu: Build > Windows x64
/// CLI:  Unity.exe -quit -batchmode -projectPath . -executeMethod BuildWindows.BuildFromCommandLine
/// </summary>
public static class BuildWindows
{
    const string OutputFolder = "Builds/Windows";
    const string ExecutableName = "miniGame01.exe";
    const string ReportFile = "Builds/windows_build_report.txt";

    [MenuItem("Build/Windows x64")]
    public static void Build()
    {
        RunBuild();
    }

    public static void BuildFromCommandLine()
    {
        var report = RunBuild();
        if (report == null || report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    static BuildReport RunBuild()
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var outputDir = Path.Combine(projectRoot, OutputFolder);
        var reportPath = Path.Combine(projectRoot, ReportFile);

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, "STATUS: RUNNING\n");

        try
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new Exception("No enabled scenes in Build Settings.");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputDir, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            File.WriteAllText(reportPath, string.Join("\n", new[]
            {
                "STATUS: " + summary.result,
                "SCENES: " + string.Join(", ", scenes),
                "ERRORS: " + summary.totalErrors,
                "WARNINGS: " + summary.totalWarnings,
                "SIZE_BYTES: " + summary.totalSize,
                "DURATION: " + summary.totalTime,
                "OUTPUT: " + summary.outputPath,
                "",
            }));

            if (summary.result == BuildResult.Succeeded)
                Debug.Log("Windows build succeeded: " + summary.outputPath);
            else
                Debug.LogError("Windows build " + summary.result + " with " + summary.totalErrors + " error(s).");

            return report;
        }
        catch (Exception e)
        {
            File.WriteAllText(reportPath, "STATUS: EXCEPTION\n" + e + "\n");
            Debug.LogError("Windows build threw: " + e);
            return null;
        }
    }
}
