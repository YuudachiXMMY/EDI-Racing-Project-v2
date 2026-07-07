using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Configuration for a single race event.
/// Stored in EventSchedule ScriptableObject.
/// </summary>
[Serializable]
public struct RaceEventConfig
{
    [Tooltip("Type of event to trigger")]
    public RaceEventType EventType;

    [Tooltip("Display name shown in logs and future UI")]
    public string DisplayName;

    [Header("Speed Modification")]
    [Tooltip("Speed change applied to affected cars (negative = penalty, positive = boost)")]
    public float SpeedDelta;

    [Tooltip("Duration in seconds the speed change lasts")]
    public float Duration;

    [Header("Targeting (type-specific)")]
    [Tooltip("For ColorBoost/ColorPenalty: target color index (0=green,1=black,2=red,3=blue,4=white)")]
    public int TargetColorIndex;

    [Tooltip("For FunctionBoost/FunctionPenalty: target function name (e.g. 'facerecog')")]
    public string TargetFunction;

    [Tooltip("For NameLengthPenalty: team names longer than this get penalized")]
    public int NameLengthThreshold;

    [Header("Input")]
    [Tooltip("Keyboard shortcut to trigger this event (Alpha1-Alpha7)")]
    public Key TriggerKey;

    [Tooltip("Can this event be triggered multiple times?")]
    public bool AllowRepeat;

    [HideInInspector]
    public bool HasBeenTriggered;
}
