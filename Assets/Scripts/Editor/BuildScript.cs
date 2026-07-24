using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CLI build automation for WebGL. Called via:
///   Unity -batchmode -executeMethod BuildScript.BuildWebGL
///   Unity -batchmode -executeMethod BuildScript.VerifyBuildArtifacts
/// </summary>
public static class BuildScript
{
    private const string BuildPath = "Deploy/webgl-build";

    public static void BuildWebGL()
    {
        string[] scenes = { "Assets/Scenes/complete_track_demo.unity" };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BuildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] WebGL build failed: {report.summary.totalErrors} errors");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[BuildScript] WebGL build succeeded: {BuildPath}");

        if (!VerifyArtifacts(BuildPath))
        {
            Debug.LogError("[BuildScript] Post-build verification failed");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Verify WebGL build artifacts are complete and index.html references valid files.
    /// Can be called standalone via -executeMethod BuildScript.VerifyBuildArtifacts
    /// </summary>
    public static void VerifyBuildArtifacts()
    {
        if (!VerifyArtifacts(BuildPath))
        {
            EditorApplication.Exit(1);
        }
    }

    private static bool VerifyArtifacts(string buildPath)
    {
        string buildDir = Path.Combine(buildPath, "Build");
        string indexPath = Path.Combine(buildPath, "index.html");
        int errors = 0;

        // Check Build/ directory exists
        if (!Directory.Exists(buildDir))
        {
            Debug.LogError($"[BuildScript] Build directory not found: {buildDir}");
            return false;
        }

        // Check required artifact patterns
        var patterns = new[] { "*.loader.js", "*.framework.js*", "*.wasm*", "*.data*" };
        foreach (string pattern in patterns)
        {
            var matches = Directory.GetFiles(buildDir, pattern);
            if (matches.Length == 0)
            {
                Debug.LogError($"[BuildScript] Missing artifact: {pattern}");
                errors++;
            }
            else
            {
                var info = new FileInfo(matches[0]);
                Debug.Log($"[BuildScript] Found: {info.Name} ({info.Length} bytes)");
            }
        }

        // Check index.html exists and references valid files
        if (!File.Exists(indexPath))
        {
            Debug.LogError($"[BuildScript] index.html not found: {indexPath}");
            errors++;
        }
        else
        {
            string html = File.ReadAllText(indexPath);
            var refs = Regex.Matches(html, @"Build/[^""]+");
            foreach (Match m in refs)
            {
                string refPath = Path.Combine(buildPath, m.Value);
                if (!File.Exists(refPath))
                {
                    Debug.LogError($"[BuildScript] index.html references missing file: {m.Value}");
                    errors++;
                }
            }

            if (!html.Contains("createUnityInstance"))
            {
                Debug.LogError("[BuildScript] index.html missing createUnityInstance call");
                errors++;
            }
        }

        if (errors > 0)
        {
            Debug.LogError($"[BuildScript] Verification failed: {errors} error(s)");
            return false;
        }

        Debug.Log("[BuildScript] All build artifacts verified");
        return true;
    }
}
