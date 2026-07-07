using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Saves/loads race sessions as JSON and exports results as CSV.
/// Files are stored in Application.persistentDataPath/Sessions/.
/// </summary>
public class SessionManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Subfolder name within Application.persistentDataPath for session files")]
    public string SaveFolder = "Sessions";

    public string SaveSession(SessionData session)
    {
        string dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"session_{timestamp}.json";
        string path = Path.Combine(dir, filename);

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

    public string ExportResults(RaceResults results)
    {
        string dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"results_{timestamp}.csv";
        string path = Path.Combine(dir, filename);

        string csv = ResultsExporter.ExportRankingsCsv(results);
        if (results.EventLog != null && results.EventLog.Length > 0)
        {
            csv += "\nEvent Log\n";
            csv += ResultsExporter.ExportEventLogCsv(results);
        }

        File.WriteAllText(path, csv);

        Debug.Log($"[SessionManager] Results exported: {path}");
        return path;
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
}
