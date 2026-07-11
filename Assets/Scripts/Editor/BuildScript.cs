using UnityEditor;
using UnityEngine;

/// <summary>
/// CLI build automation for WebGL. Called via:
///   Unity -batchmode -executeMethod BuildScript.BuildWebGL
/// </summary>
public static class BuildScript
{
    public static void BuildWebGL()
    {
        string buildPath = "Deploy/webgl-build";
        string[] scenes = { "Assets/Scenes/complete_track_demo.unity" };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] WebGL build failed: {report.summary.totalErrors} errors");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log($"[BuildScript] WebGL build succeeded: {buildPath}");
        }
    }
}
