using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages survey configuration files: save, load, list, and template access.
/// Configs are stored as JSON in Application.persistentDataPath/SurveyConfigs/.
/// Mirrors SessionManager patterns for file I/O.
/// </summary>
public class SurveyConfigManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Subfolder name within Application.persistentDataPath for survey config files")]
    public string SaveFolder = "SurveyConfigs";

    /// <summary>
    /// The currently active survey configuration for this session.
    /// </summary>
    public SurveyConfig ActiveConfig { get; private set; }

    public string SaveConfig(SurveyConfig config)
    {
        string dir = GetSaveDirectory();
        Directory.CreateDirectory(dir);

        string safeName = SanitizeFileName(config.ConfigName);
        if (string.IsNullOrEmpty(safeName))
            safeName = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string filename = $"survey_{safeName}.json";
        string path = Path.Combine(dir, filename);

        config.CreatedAt = DateTime.Now.ToString("o");
        string json = JsonUtility.ToJson(config, true);
        File.WriteAllText(path, json);

        Debug.Log($"[SurveyConfigManager] Config saved: {path}");
        return path;
    }

    public SurveyConfig LoadConfig(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[SurveyConfigManager] Config file not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        var config = JsonUtility.FromJson<SurveyConfig>(json);

        Debug.Log($"[SurveyConfigManager] Config loaded: {path} ({config.ConfigName})");
        return config;
    }

    public string[] GetSavedConfigPaths()
    {
        string dir = GetSaveDirectory();
        if (!Directory.Exists(dir)) return Array.Empty<string>();

        return Directory.GetFiles(dir, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToArray();
    }

    public SurveyConfig LoadTemplate(string templateName)
    {
        var config = SurveyTemplates.GetTemplate(templateName);
        if (config == null)
            Debug.LogWarning($"[SurveyConfigManager] Unknown template: {templateName}");
        else
            Debug.Log($"[SurveyConfigManager] Template loaded: {templateName}");
        return config;
    }

    public string[] GetTemplateNames()
    {
        return SurveyTemplates.TemplateNames;
    }

    public void SetActiveConfig(SurveyConfig config)
    {
        ActiveConfig = config;
        Debug.Log($"[SurveyConfigManager] Active config set: {config?.ConfigName ?? "(none)"}");
    }

    /// <summary>
    /// Applies the active config's event rules to an EventSchedule.
    /// Assigns TriggerKeys Digit1-Digit9 sequentially (max 9 rules).
    /// </summary>
    public void ApplyRulesToSchedule(EventSchedule schedule)
    {
        if (ActiveConfig == null || ActiveConfig.Rules == null || ActiveConfig.Rules.Length == 0)
        {
            Debug.LogWarning("[SurveyConfigManager] No active config or rules to apply");
            return;
        }

        var eventRules = EventRuleKeys.AssignKeys(ActiveConfig.Rules);
        schedule.Events = eventRules;

        if (ActiveConfig.Rules.Length > EventRuleKeys.DigitKeys.Length)
            Debug.LogWarning($"[SurveyConfigManager] Config has {ActiveConfig.Rules.Length} rules but only {EventRuleKeys.DigitKeys.Length} key bindings available. Extra rules skipped.");

        Debug.Log($"[SurveyConfigManager] Applied {eventRules.Length} rules to EventSchedule");
    }

    private string GetSaveDirectory()
    {
        return Path.Combine(Application.persistentDataPath, SaveFolder);
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray())
            .Replace(' ', '_')
            .ToLower();
    }
}
