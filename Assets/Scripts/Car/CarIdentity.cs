using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runtime state for a spawned race car.
/// Initialized from CarData, stores dynamic attributes and tracks race progress.
/// </summary>
public class CarIdentity : MonoBehaviour
{
    [Header("Identity")]
    public string TeamName;
    public AttributeEntry[] Attributes;

    [Header("Player Ownership")]
    public bool IsOwnCar;

    [Header("Race Progress")]
    public int CurrentCheckpointIndex;
    public int TotalCheckpointsPassed;
    public int CurrentLap;
    public float CheckpointTime;

    public void Initialize(CarData data)
    {
        TeamName = data.TeamName;
        Attributes = data.Attributes != null
            ? (AttributeEntry[])data.Attributes.Clone()
            : Array.Empty<AttributeEntry>();
        IsOwnCar = false;
        CurrentCheckpointIndex = 0;
        TotalCheckpointsPassed = 0;
        CurrentLap = 0;
        CheckpointTime = 0f;
    }

    // --- Attribute Accessors (mirror CarData) ---

    public string GetAttribute(string key, string defaultValue = "")
    {
        return Attributes.Get(key, defaultValue);
    }

    public int GetIntAttribute(string key, int defaultValue = 0)
    {
        string val = GetAttribute(key, null);
        if (val != null && int.TryParse(val, out int result)) return result;
        return defaultValue;
    }

    public bool HasAttribute(string key)
    {
        return Attributes.Has(key);
    }

    // --- Backward-Compatible Accessors ---

    public int ColorIndex => GetIntAttribute("colorIndex", 0);

    public string[] Functions
    {
        get
        {
            string val = GetAttribute("functions", "");
            if (string.IsNullOrEmpty(val)) return Array.Empty<string>();
            return val.Split('/').Select(f => f.Trim().ToLower()).Where(f => f.Length > 0).ToArray();
        }
    }

    private void Update()
    {
        CheckpointTime += Time.deltaTime;
    }
}
