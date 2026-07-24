using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Logic operator for combining multiple conditions within a single rule.
/// </summary>
public enum LogicOperator
{
    And,  // All conditions must match
    Or    // Any condition can match
}

/// <summary>
/// A single attribute condition used within compound rules.
/// </summary>
[Serializable]
public struct RuleCondition
{
    public string AttributeName;
    public ComparisonOperator Operator;
    public string CompareValue;
}

/// <summary>
/// Configuration for a single event rule.
/// Replaces the hardcoded RaceEventConfig with a generic attribute-based matching system.
/// Stored in EventSchedule ScriptableObject.
/// Supports compound conditions: when Conditions array is non-empty, evaluates
/// multiple sub-conditions joined by Logic (AND/OR). When empty, falls back to
/// legacy single-condition fields (AttributeName/Operator/CompareValue).
/// </summary>
[Serializable]
public struct EventRule
{
    [Tooltip("Display name shown in event panel and logs")]
    public string DisplayName;

    [Header("Compound Conditions (optional)")]
    [Tooltip("Logic operator for combining conditions (AND = all must match, OR = any can match)")]
    public LogicOperator Logic;

    [Tooltip("Multiple conditions to evaluate. When non-empty, overrides legacy single-condition fields below.")]
    public RuleCondition[] Conditions;

    [Header("Legacy Single Condition")]
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
