using System;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Saves/loads race sessions as JSON and exports results as CSV.
/// Files are stored in Application.persistentDataPath/Sessions/.
/// In WebGL builds, CSV exports trigger a browser download dialog.
/// </summary>
public class SessionManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Subfolder name within Application.persistentDataPath for session files")]
    public string SaveFolder = "Sessions";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void WebSocketBridge_DownloadFile(string filename, string content);
#endif

    public string SaveSession(SessionData session)
    {
        string dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);

        string path = GetUniquePath(dir, "session", "json");

        string json = JsonUtility.ToJson(session, true);
        File.WriteAllText(path, json);

        Debug.Log($"[SessionManager] Session saved: {path}");
        return path;
    }

    public SessionData LoadSession(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[SessionManager] Session file not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        var session = JsonUtility.FromJson<SessionData>(json);

        Debug.Log($"[SessionManager] Session loaded: {path} ({session.Cars.Length} cars)");
        return session;
    }

    public string FindLatestSession()
    {
        string dir = GetSaveDirectory();
        if (!Directory.Exists(dir)) return null;

        var files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0) return null;

        return files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
    }

    public string ExportResults(RaceResults results, SurveyConfig config = null)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"results_{timestamp}.csv";

        string csv = config != null
            ? ResultsExporter.ExportRankingsCsv(results, config)
            : ResultsExporter.ExportRankingsCsv(results);
        if (results.EventLog != null && results.EventLog.Length > 0)
        {
            csv += "\nEvent Log\n";
            csv += ResultsExporter.ExportEventLogCsv(results);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        WebSocketBridge_DownloadFile(filename, csv);
        Debug.Log($"[SessionManager] Results download triggered: {filename}");
        return filename;
#else
        string dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);
        string path = GetUniquePath(dir, "results", "csv");
        File.WriteAllText(path, csv);
        Debug.Log($"[SessionManager] Results exported: {path}");
        return path;
#endif
    }

    public string[] GetSavedSessionPaths()
    {
        string dir = GetSaveDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        return Directory.GetFiles(dir, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToArray();
    }

    private string GetSaveDirectory()
    {
        return Path.Combine(Application.persistentDataPath, SaveFolder);
    }

    /// <summary>
    /// Builds a timestamped path guaranteed not to collide with an existing file,
    /// even when multiple saves happen within the same second. The base name keeps
    /// second precision (e.g. "session_20260727_200718.json"); on collision a
    /// numeric suffix is appended ("..._1", "..._2", ...).
    /// </summary>
    private static string GetUniquePath(string dir, string prefix, string extension)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = Path.Combine(dir, $"{prefix}_{timestamp}.{extension}");
        int suffix = 1;
        while (File.Exists(path))
            path = Path.Combine(dir, $"{prefix}_{timestamp}_{suffix++}.{extension}");
        return path;
    }
}
