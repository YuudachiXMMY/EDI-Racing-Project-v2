using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Configuration for a single event rule.
/// Replaces the hardcoded RaceEventConfig with a generic attribute-based matching system.
/// Stored in EventSchedule ScriptableObject.
/// </summary>
[Serializable]
public struct EventRule
{
    [Tooltip("Display name shown in event panel and logs")]
    public string DisplayName;

    [Header("Rule Condition")]
    [Tooltip("Attribute name to check on the car (e.g., 'colorIndex', 'functions', 'teamName', or any custom attribute)")]
    public string AttributeName;

    [Tooltip("How to compare the attribute value")]
    public ComparisonOperator Operator;

    [Tooltip("Value to compare against (interpretation depends on operator)")]
    public string CompareValue;

    [Header("Effect")]
    [Tooltip("Speed change applied to matched cars (negative = penalty, positive = boost)")]
    public float SpeedDelta;

    [Tooltip("Duration in seconds the speed change lasts")]
    public float Duration;

    [Header("Weather (Optional)")]
    [Tooltip("Weather VFX to activate when this rule triggers (None for no VFX)")]
    public WeatherType Weather;

    [Header("Input")]
    [Tooltip("Keyboard shortcut to trigger this event (Alpha1-Alpha9)")]
    public Key TriggerKey;

    [Tooltip("Can this event be triggered multiple times?")]
    public bool AllowRepeat;

    [HideInInspector]
    public bool HasBeenTriggered;
}
